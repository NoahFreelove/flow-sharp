using System;
using FlowLang.Diagnostics;
using FlowLang.StandardLibrary.Audio.Synthesizers;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Audio.Sfz;

/// <summary>
/// Phase 33 — sample-based SFZ patch renderer. Conceptually the 10th
/// synthesizer (alongside Piano/Brass/Sax/Strings/Flute/Bell/Organ/Drums/
/// Bell/Wavetable per Phase 29's class doc) — sources its waveform from
/// a sampled buffer instead of synthesis, but layers Phase 28's locked
/// articulation envelope (SPEC-8) on top of every rendered note.
///
/// Renderer pipeline (REQ-1 / REQ-4 / REQ-5 / REQ-8 — see
/// .planning/phases/33-sfz-orchestral-sampler/33-SPEC.md):
///
/// <list type="number">
///   <item><description>Rest short-circuit → return silence buffer of the
///   authored duration (mirrors <see cref="SampledInstrumentRenderer"/>'s
///   <c>note.IsRest</c> branch).</description></item>
///   <item><description>Compute target MIDI pitch + clamped MIDI velocity
///   (Pitfall 9: clamp to [1, 127] so the SFZ default of <c>lovel=1</c> still
///   matches even when the composer authored a velocity-0 note).</description></item>
///   <item><description>Region match via O(1) grid lookup
///   <c>patch.Grid[targetMidi, vel]</c>.</description></item>
///   <item><description>Nearest-pitch fallback (SPEC-4) when the grid cell is
///   null: walk <c>patch.SortedByPitch[]</c> (typically &lt; 128 entries,
///   linear scan acceptable) for the closest pitch, then try
///   <c>Grid[nearestPitch, vel]</c>; on still-null, scan that pitch's
///   velocity row for ANY covering region. On still-null after fallback,
///   emit a charitable <see cref="RenderingDiagnostics.WarnOnce"/> advisory
///   and return silence — the song doesn't die from one missing
///   region.</description></item>
///   <item><description>Varispeed shift (zero new resample code — verbatim
///   reuse of <see cref="FileIO.VarispeedResample"/> per RESEARCH §Don't
///   Hand-Roll, memoized inside <see cref="SfzSampleCache.GetVarispeed"/>).</description></item>
///   <item><description>Sustain loop (SPEC-5) when the region's
///   <c>LoopMode</c> is <see cref="SfzLoopMode.LoopContinuous"/> or
///   <see cref="SfzLoopMode.LoopSustain"/>: extend the source body across
///   the full authored duration using the 441-frame equal-power sin/cos
///   crossfade. <c>cos²(πt/2N) + sin²(πt/2N) = 1</c> for all t — constant
///   power across the seam, so the loop doesn't tick at the boundary
///   (the failure-analyst's flagged worst-case for Phase 34).
///   <para>
///   Defensive <c>effectiveLoopEnd = Math.Min(region.LoopEnd, source.Frames - 1)</c>
///   at the top of the loop branch per Pitfall 3 / T-33-LOOP-01 — a
///   malformed .sfz can declare <c>loop_end</c> past sample length.
///   </para></description></item>
///   <item><description>Apply <c>region.Volume</c> (parser-converted to
///   linear per Pitfall 8) and <c>region.Pan</c> (parser-converted to Flow's
///   <c>[-1.0, +1.0]</c> per Pitfall 7) BEFORE the articulation envelope.</description></item>
///   <item><description>Phase 28 articulation envelope (SPEC-8) ON TOP via
///   <see cref="SynthUtils.GenerateArticulationADSR"/> +
///   <see cref="SynthUtils.ApplyEnvelope"/>. The SFZ <c>ampeg_attack</c> /
///   <c>ampeg_release</c> values OVERRIDE Phase 28's baseline when
///   <c>&gt; 0</c>; otherwise the near-transparent baseline
///   (attack 0.005s, decay 0.05s, sustain 1.0, release 0.05s) lets the
///   articulation rules layer cleanly on top of the natural sample
///   envelope (same rationale as Phase 29 — see SampledInstrumentRenderer
///   class doc).</description></item>
/// </list>
///
/// This class is isolated from <see cref="Core.FlowEngine"/> and
/// <c>SongRenderer</c>: tests in <c>flow-lang.Tests/Unit/Phase33/</c> invoke
/// <see cref="Render"/> directly with a pre-populated
/// <see cref="SfzSampleCache"/> + a programmatic <see cref="SfzData"/>.
/// Plan 33-07 wires it into <c>SongRenderer</c>'s <c>sampler:NAME</c>
/// dispatch branch alongside Phase 29's <c>FlowEngine.CurrentSampleCache</c>
/// surface.
/// </summary>
public class SfzRenderer
{
    private readonly SfzSampleCache _cache;

    /// <summary>
    /// 441 frames = 10 ms at 44.1 kHz, locked by SPEC-5. Phase 33 §33-SPEC
    /// fixes this constant — do not adjust without re-running the SPEC-5
    /// spectral-centroid acceptance gate.
    /// </summary>
    private const int CrossfadeFrames = 441;

    public SfzRenderer(SfzSampleCache cache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    /// <summary>
    /// Render a single <paramref name="note"/> through the SFZ patch
    /// <paramref name="patch"/>. Returns an <see cref="AudioBuffer"/> at the
    /// requested <paramref name="sampleRate"/> covering
    /// <paramref name="durationBeats"/> beats at <paramref name="bpm"/> BPM.
    /// </summary>
    public AudioBuffer Render(MusicalNoteData note, int sampleRate, double durationBeats, double bpm, SfzData patch)
    {
        if (note is null) throw new ArgumentNullException(nameof(note));
        if (patch is null) throw new ArgumentNullException(nameof(patch));

        if (note.IsRest)
            return SynthUtils.CreateSilence(sampleRate, durationBeats, bpm);

        double durationSeconds = SynthUtils.BeatsToSeconds(durationBeats, bpm);
        int targetFrames = (int)(durationSeconds * sampleRate);
        if (targetFrames <= 0)
            return new AudioBuffer(0, 1, sampleRate);

        int targetMidi = PitchConversion.GetMidiNote(note.NoteName, note.Octave, note.Alteration);
        if (targetMidi < 0 || targetMidi > 127)
        {
            // Out-of-range MIDI pitch — treat like missing region.
            RenderingDiagnostics.WarnOnce(
                $"sfz:oob:{patch.Description}:{targetMidi}",
                $"[sfz] pitch {targetMidi} out of MIDI range under '{patch.Description}' — rendered as rest");
            return SynthUtils.CreateSilence(sampleRate, durationBeats, bpm);
        }

        // Pitfall 9 velocity clamp: SFZ default is lovel=1 (not 0). A composer's
        // note.Velocity == 0.0 maps to raw MIDI 0 which would never match a
        // lovel=1 region; clamp to [1, 127] so charitable rendering preserves
        // the default region match.
        int vel = Math.Clamp((int)Math.Round(note.Velocity * 127.0), 1, 127);

        // Region match (D-01 O(1) grid lookup).
        SfzRegion? region = patch.Grid[targetMidi, vel];
        int semitonesShift = 0;
        if (region is null)
        {
            // Nearest-pitch fallback (SPEC-4). SortedByPitch carries
            // deduplicated ascending MIDI pitches with ANY coverage; an
            // empty list short-circuits to the "no region anywhere" branch.
            if (patch.SortedByPitch is { Length: > 0 })
            {
                int nearestPitch = FindNearestPitch(patch.SortedByPitch, targetMidi);
                // Try the exact velocity slot at the nearest pitch first;
                // fall through to any velocity-row covering region if absent.
                region = patch.Grid[nearestPitch, vel] ?? FindAnyRegionAtPitch(patch, nearestPitch);
                if (region is not null)
                    semitonesShift = targetMidi - nearestPitch;
            }

            if (region is null)
            {
                // Charitable: emit a one-shot advisory and render silence
                // (matches Phase 32's [tuning] unmapped-MIDI-key handling).
                RenderingDiagnostics.WarnOnce(
                    $"sfz:missing:{patch.Description}:{targetMidi}:{vel}",
                    $"[sfz] no region for ({targetMidi}, {vel}) in '{patch.Description}' — rendered as rest");
                return SynthUtils.CreateSilence(sampleRate, durationBeats, bpm);
            }
        }

        // Pull the (possibly varispeed-shifted) buffer from the cache.
        // GetVarispeed returns null only when the underlying WAV wasn't
        // eager-loaded — production code paths guarantee eager-load before
        // render (Plan 33-07 sequences EagerLoad → render). Defensive
        // advisory + silence still keeps the song alive if a test calls
        // Render without populating the cache.
        AudioBuffer? source = _cache.GetVarispeed(patch, region.SamplePath, semitonesShift);
        if (source is null)
        {
            RenderingDiagnostics.WarnOnce(
                $"sfz:nosample:{patch.Description}:{region.SamplePath}",
                $"[sfz] sample '{region.SamplePath}' under '{patch.Description}' not loaded — rendered as rest");
            return SynthUtils.CreateSilence(sampleRate, durationBeats, bpm);
        }

        // Body assembly: either NoLoop / OneShot copy-and-zero-pad, or
        // LoopContinuous / LoopSustain 441-frame equal-power crossfade.
        float[] fitted = AssembleBody(source, region, targetFrames);

        // region.Volume is LINEAR (parser converted from dB at parse time
        // per Pitfall 8). SFZ default volume=0 dB → linear 1.0; -6 dB →
        // linear ≈ 0.501.
        if (region.Volume != 1.0)
        {
            float volScale = (float)region.Volume;
            for (int i = 0; i < fitted.Length; i++) fitted[i] *= volScale;
        }

        // Phase 28 articulation envelope ON TOP (SPEC-8). The SFZ
        // ampeg_attack / ampeg_release override the near-transparent Phase 28
        // baseline only when > 0 — composer-authored values take precedence.
        float[] envelope = SynthUtils.GenerateArticulationADSR(
            note.Articulation,
            baseAttack:  region.AmpegAttack  > 0 ? region.AmpegAttack  : 0.005,
            baseDecay:                                                   0.05,
            baseSustain:                                                 1.0,
            baseRelease: region.AmpegRelease > 0 ? region.AmpegRelease : 0.05,
            frames: targetFrames,
            sampleRate: sampleRate,
            isPercussion: false);
        SynthUtils.ApplyEnvelope(fitted, envelope);

        // Pan via constant-power stereo split (Pitfall 7). Center (pan == 0)
        // stays mono so unaffected patches don't double their channel count.
        if (region.Pan != 0.0)
        {
            return ToStereoBufferWithPan(fitted, sampleRate, region.Pan);
        }
        return SynthUtils.ToMonoBuffer(fitted, sampleRate);
    }

    /// <summary>
    /// NoLoop / OneShot: copy the source body into the fitted buffer and
    /// zero-pad anything past the source length. LoopContinuous /
    /// LoopSustain: produce the equal-power 441-frame sin/cos crossfade
    /// body per SPEC-5.
    ///
    /// Loop algorithm:
    ///   * The loop body is <c>[LoopStart, effectiveLoopEnd]</c> in source frames.
    ///   * Pre-attack region <c>[0, LoopStart)</c> plays once at the head.
    ///   * Then the body repeats. At every loop seam, the last
    ///     <c>CrossfadeFrames</c> samples of one iteration blend (equal-power,
    ///     <c>cos(πt/2N) · A + sin(πt/2N) · B</c>) with the FIRST
    ///     <c>CrossfadeFrames</c> samples of the next iteration.
    ///   * After the crossfade has played, the next iteration RESUMES from
    ///     <c>LoopStart + CrossfadeFrames</c> — those samples were already
    ///     emitted as the "B" channel of the crossfade. Skipping them
    ///     prevents the click that would otherwise occur if playback
    ///     re-played frames <c>[LoopStart, LoopStart + N]</c> right after
    ///     the seam, because the crossfade already moved sample energy
    ///     through that range.
    ///
    /// <para>
    /// Defensive: <c>effectiveLoopEnd = Math.Min(region.LoopEnd, source.Frames - 1)</c>
    /// per Pitfall 3 / T-33-LOOP-01 — a malformed .sfz can declare
    /// <c>loop_end</c> past the sample length.
    /// </para>
    /// </summary>
    private static float[] AssembleBody(AudioBuffer source, SfzRegion region, int targetFrames)
    {
        var fitted = new float[targetFrames];
        if (source.Frames == 0) return fitted;

        bool isLooped = region.LoopMode == SfzLoopMode.LoopContinuous
                     || region.LoopMode == SfzLoopMode.LoopSustain;

        // Pitfall 3 / T-33-LOOP-01 clamp.
        int effectiveLoopEnd = Math.Min(region.LoopEnd, source.Frames - 1);
        int loopLen = effectiveLoopEnd - region.LoopStart;

        if (!isLooped || loopLen <= 0)
        {
            // NoLoop / OneShot — straight copy and zero-pad. Mirrors
            // SampledInstrumentRenderer.cs lines 116-119.
            int copyLen = Math.Min(source.Frames, targetFrames);
            for (int i = 0; i < copyLen; i++) fitted[i] = source.Data[i];
            return fitted;
        }

        // Crossfade window size — clamp to the loop length so a very short
        // loop doesn't try to crossfade more samples than exist.
        int xfade = Math.Min(CrossfadeFrames, loopLen / 2);
        if (xfade <= 0)
        {
            // Loop too short to support a crossfade — fall back to plain
            // wrap with no smoothing (the discontinuity is unavoidable at
            // such a short loop, but tests for the SPEC-5 acceptance gate
            // use loops far longer than 441 frames).
            int dst0 = 0;
            // Pre-attack head.
            while (dst0 < region.LoopStart && dst0 < targetFrames)
            {
                fitted[dst0] = source.Data[dst0];
                dst0++;
            }
            while (dst0 < targetFrames)
            {
                int rel = (dst0 - region.LoopStart) % loopLen;
                fitted[dst0] = source.Data[region.LoopStart + rel];
                dst0++;
            }
            return fitted;
        }

        // Stage 1: pre-attack [0, LoopStart) plays once at the head.
        int dst = 0;
        int headEnd = Math.Min(region.LoopStart, targetFrames);
        for (; dst < headEnd; dst++) fitted[dst] = source.Data[dst];

        // Stage 2: loop body with crossfade. Each iteration emits
        //   - bodyLen samples (loopLen - xfade frames, straight read)
        //   - xfade samples of equal-power crossfade
        // and the next iteration RESUMES from LoopStart + xfade — those
        // first-N samples were already covered by the previous iteration's
        // crossfade tail (the "B" channel), so skipping them avoids the
        // discontinuity.
        //
        // First iteration is special: it plays the FULL body once (LoopStart
        // .. effectiveLoopEnd) including the entire pre-crossfade region,
        // but the last xfade frames blend with samples starting at
        // LoopStart. After that first crossfade, every subsequent iteration
        // begins at LoopStart + xfade.
        int srcReadPos = region.LoopStart;
        bool firstIteration = true;
        while (dst < targetFrames)
        {
            int iterStartSrc = srcReadPos;
            int iterStartDst = dst;

            // Straight-read region inside this iteration.
            int straightEnd = effectiveLoopEnd - xfade; // exclusive upper bound of straight read in source
            while (srcReadPos < straightEnd && dst < targetFrames)
            {
                fitted[dst++] = source.Data[srcReadPos++];
            }

            if (dst >= targetFrames) break;

            // Crossfade region: last xfade frames of this iteration's body
            // blend with first xfade frames of the next iteration's body
            // (which starts at LoopStart). At t=0 we fully play THIS body's
            // tail; at t=1 we fully play the NEXT body's head. cos² + sin²
            // preserves total power across the transition.
            for (int k = 0; k < xfade && dst < targetFrames; k++)
            {
                float t = (float)k / xfade;
                float wA = MathF.Cos(MathF.PI * t / 2.0f);
                float wB = MathF.Sin(MathF.PI * t / 2.0f);
                int srcA = srcReadPos + k;                  // approaching effectiveLoopEnd
                int srcB = region.LoopStart + k;            // approaching LoopStart + xfade
                if (srcA > source.Frames - 1) srcA = source.Frames - 1;
                if (srcB > source.Frames - 1) srcB = source.Frames - 1;
                fitted[dst++] = wA * source.Data[srcA] + wB * source.Data[srcB];
            }

            // Next iteration begins at LoopStart + xfade — those first
            // xfade samples have already played as the B channel of the
            // crossfade we just emitted.
            srcReadPos = region.LoopStart + xfade;
            firstIteration = false;
        }
        return fitted;
    }

    /// <summary>
    /// Returns the entry of <paramref name="sortedAscending"/> closest to
    /// <paramref name="target"/>. Linear scan — SortedByPitch is typically
    /// &lt; 128 entries so a binary search isn't worth the complexity.
    /// </summary>
    private static int FindNearestPitch(int[] sortedAscending, int target)
    {
        int nearest = sortedAscending[0];
        int bestDist = Math.Abs(target - nearest);
        for (int i = 1; i < sortedAscending.Length; i++)
        {
            int d = Math.Abs(target - sortedAscending[i]);
            if (d < bestDist) { nearest = sortedAscending[i]; bestDist = d; }
        }
        return nearest;
    }

    /// <summary>
    /// Scan <c>patch.Grid[pitch, *]</c> for any non-null region and return
    /// the first hit. Used when the nearest-pitch fallback finds a pitch
    /// with coverage but not at the exact requested velocity slot
    /// (Pattern 4 step b in 33-RESEARCH.md). Returns null if no velocity
    /// slot has any coverage at this pitch.
    /// </summary>
    private static SfzRegion? FindAnyRegionAtPitch(SfzData patch, int pitch)
    {
        for (int v = 1; v <= 127; v++)
        {
            var r = patch.Grid[pitch, v];
            if (r is not null) return r;
        }
        return null;
    }

    /// <summary>
    /// Wraps <paramref name="mono"/> in a stereo <see cref="AudioBuffer"/>
    /// with constant-power panning per Pitfall 7. The
    /// <paramref name="pan"/> argument is in Flow's <c>[-1.0, +1.0]</c>
    /// range (the parser already converted from SFZ's <c>[-100, +100]</c>).
    ///
    /// Constant-power split:
    ///   <c>theta = (pan + 1) * π/4</c> maps -1 → 0, +1 → π/2
    ///   <c>left  = cos(theta) * sample</c>
    ///   <c>right = sin(theta) * sample</c>
    ///   <c>cos² + sin² = 1 → total power preserved across the pan range</c>
    /// </summary>
    private static AudioBuffer ToStereoBufferWithPan(float[] mono, int sampleRate, double pan)
    {
        pan = Math.Clamp(pan, -1.0, 1.0);
        double theta = (pan + 1.0) * Math.PI / 4.0;
        float wL = (float)Math.Cos(theta);
        float wR = (float)Math.Sin(theta);

        var stereo = new AudioBuffer(mono.Length, 2, sampleRate);
        for (int i = 0; i < mono.Length; i++)
        {
            stereo.Data[i * 2]     = mono[i] * wL;
            stereo.Data[i * 2 + 1] = mono[i] * wR;
        }
        return stereo;
    }
}
