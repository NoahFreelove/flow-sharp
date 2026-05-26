using System;
using System.Collections.Generic;
using System.Linq;
using FlowLang.Diagnostics;
using FlowLang.StandardLibrary.Audio.DSP;
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

    /// <summary>
    /// Phase 37 SAMP-01 — per-region-group round-robin counter. The key tuple
    /// identifies a round-robin GROUP (regions sharing key+vel range that
    /// declare <c>seq_length &gt; 1</c>). The stored counter is the number of
    /// triggers seen so far against that group; the picked region's
    /// <c>seq_position</c> is <c>(counter % seqLength) + 1</c>.
    ///
    /// <para><b>Pitfall 6 reset discipline</b>: the counter resets at the
    /// renderSong / writeWav boundary via <see cref="ResetAtRenderBoundary"/>
    /// so two consecutive renders of the same song produce byte-identical
    /// output. In practice every <c>SongRenderer.RenderSongWithSfz</c> call
    /// constructs a FRESH <see cref="SfzRenderer"/> (line ~525) so the
    /// counters are naturally clear at the boundary; the explicit Reset is
    /// for test callers + future reuse scenarios.</para>
    /// </summary>
    private readonly Dictionary<(int loKey, int hiKey, int loVel, int hiVel), int> _rrCounter = new();

    public SfzRenderer(SfzSampleCache cache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    /// <summary>
    /// Phase 37 SAMP-01 — clear the round-robin counters. Called from the
    /// renderSong / writeWav boundary (Pitfall 6) alongside
    /// <see cref="FlowLang.Runtime.PrngRegistry.ResetAtRenderBoundary"/> so
    /// two-run cmp-clean determinism holds.
    /// </summary>
    public void ResetAtRenderBoundary()
    {
        _rrCounter.Clear();
    }

    /// <summary>
    /// Render a single <paramref name="note"/> through the SFZ patch
    /// <paramref name="patch"/>. Returns an <see cref="AudioBuffer"/> at the
    /// requested <paramref name="sampleRate"/> covering
    /// <paramref name="durationBeats"/> beats at <paramref name="bpm"/> BPM.
    ///
    /// <para>5-arg back-compat surface — routes to the 6-arg overload with
    /// <c>voicePan = 0.0</c> so the effective pan equals <c>region.Pan</c>.
    /// Phase 33 callers see identical pan semantics modulo the B2 lock
    /// (centered now produces stereo with equal L/R at √0.5 rather than
    /// the pre-Phase 37 mono buffer).</para>
    /// </summary>
    public AudioBuffer Render(MusicalNoteData note, int sampleRate, double durationBeats, double bpm, SfzData patch)
        => RenderInternal(note, sampleRate, durationBeats, bpm, patch, voicePan: 0.0);

    /// <summary>
    /// Phase 37 MIX-02 / OQ4 — voice-pan-aware overload. Composes the
    /// per-region <c>region.Pan</c> (from the SFZ opcode) additively with the
    /// per-voice <paramref name="voicePan"/> (from the composer's musical
    /// context), then clamps to <c>[-1.0, +1.0]</c> per the OQ4 locked
    /// composition rule. Always emits stereo (B2 lock).
    /// </summary>
    public AudioBuffer Render(
        MusicalNoteData note, int sampleRate, double durationBeats, double bpm,
        SfzData patch, double voicePan)
        => RenderInternal(note, sampleRate, durationBeats, bpm, patch, voicePan);

    /// <summary>
    /// Inner render path used by both the 5-arg back-compat surface and the
    /// 6-arg voice-pan surface. Threading <paramref name="voicePan"/> here
    /// lets us compute the effective pan once and apply it in one pass.
    /// </summary>
    private AudioBuffer RenderInternal(
        MusicalNoteData note, int sampleRate, double durationBeats, double bpm,
        SfzData patch, double voicePan)
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
            RenderingDiagnostics.WarnOnce(
                $"sfz:oob:{patch.Description}:{targetMidi}",
                $"[sfz] pitch {targetMidi} out of MIDI range under '{patch.Description}' — rendered as rest");
            return SynthUtils.CreateSilence(sampleRate, durationBeats, bpm);
        }

        int vel = Math.Clamp((int)Math.Round(note.Velocity * 127.0), 1, 127);

        SfzRegion? region = PickRoundRobinCandidate(patch, targetMidi, vel)
                            ?? patch.Grid[targetMidi, vel];
        if (region is null)
        {
            if (patch.SortedByPitch is { Length: > 0 })
            {
                int nearestPitch = FindNearestPitch(patch.SortedByPitch, targetMidi);
                region = patch.Grid[nearestPitch, vel] ?? FindAnyRegionAtPitch(patch, nearestPitch);
            }
            if (region is null)
            {
                RenderingDiagnostics.WarnOnce(
                    $"sfz:missing:{patch.Description}:{targetMidi}:{vel}",
                    $"[sfz] no region for ({targetMidi}, {vel}) in '{patch.Description}' — rendered as rest");
                return SynthUtils.CreateSilence(sampleRate, durationBeats, bpm);
            }
        }

        int semitonesShift = targetMidi - region.PitchKeycenter;

        // Phase 37 DRUM-01 (D-37-14 + W7 LOCK) — percussion patches use
        // PitchShiftEngine's #auto path (PSOLA for transient kits, vocoder
        // for sustained cymbal/gong) instead of the Phase 33 varispeed
        // route (which couples pitch + time). The gate is
        // patch.IsPercussion — set at SfzBuiltins LOAD TIME by the
        // dict-symbol (#drums), NOT by filename inspection. This is the
        // W7 LOCK revision: composers can rename GM-StylePerc.sfz, fork
        // VSCO-CE, or extend the GM dict with custom percussion symbols
        // without losing transient-preserving pitch shift, and loading a
        // non-drum patch via #piano never accidentally routes through
        // PitchShiftEngine.
        //
        // Non-percussion patches (IsPercussion=false default) keep the
        // Phase 33 byte-identical varispeed path — Phase 33/34 regression
        // tests preserve their pinned baselines.
        //
        // semitonesShift=0 is the identity case in BOTH paths:
        //   * Varispeed: GetVarispeed short-circuits to the raw buffer.
        //   * PitchShiftEngine: Pitfall 11 identity fast-path (cents=0
        //     returns input verbatim).
        AudioBuffer? source;
        if (patch.IsPercussion && semitonesShift != 0)
        {
            // >12 semitone advisory per OQ3 resolution + RESEARCH §Pattern 11
            // sub-recommendation — varispeed-style artifacts dominate at
            // large shifts even through PSOLA. Composer trust: don't reject,
            // just warn once per (patch, sample-center, target-MIDI) tuple.
            if (Math.Abs(semitonesShift) > 12)
            {
                RenderingDiagnostics.WarnOnce(
                    $"pitchShift:drum:large:{patch.Description}:{region.PitchKeycenter}:{targetMidi}",
                    $"[pitchShift] >12st shift on drum sample at MIDI {targetMidi} " +
                    $"(sample center MIDI {region.PitchKeycenter}, patch '{patch.Description}') — " +
                    "varispeed artifacts likely dominate (D-37-14 + RESEARCH Pattern 11 advisory)");
            }

            // Load the RAW sample at sample-center (semitonesShift=0
            // short-circuits to the raw buffer in GetVarispeed) — then
            // run through PitchShiftEngine which preserves duration via
            // its internal stretch(1/r) + resample(r) inverse remap.
            AudioBuffer? raw = _cache.GetVarispeed(patch, region.SamplePath, 0);
            if (raw is null)
            {
                RenderingDiagnostics.WarnOnce(
                    $"sfz:nosample:{patch.Description}:{region.SamplePath}",
                    $"[sfz] sample '{region.SamplePath}' under '{patch.Description}' not loaded — rendered as rest");
                return SynthUtils.CreateSilence(sampleRate, durationBeats, bpm);
            }
            double cents = semitonesShift * 100.0;
            source = PitchShiftEngine.Process(raw, cents, StretchMode.Auto);
        }
        else
        {
            // Phase 33 varispeed path — preserved for non-percussion
            // patches (default IsPercussion=false) AND the
            // semitonesShift=0 identity case for percussion patches
            // (GetVarispeed short-circuits to the raw buffer at shift=0,
            // making this branch byte-identical to a PitchShiftEngine
            // identity fast-path).
            source = _cache.GetVarispeed(patch, region.SamplePath, semitonesShift);
            if (source is null)
            {
                RenderingDiagnostics.WarnOnce(
                    $"sfz:nosample:{patch.Description}:{region.SamplePath}",
                    $"[sfz] sample '{region.SamplePath}' under '{patch.Description}' not loaded — rendered as rest");
                return SynthUtils.CreateSilence(sampleRate, durationBeats, bpm);
            }
        }

        float[] fitted = AssembleBody(source, region, targetFrames);

        if (region.Volume != 1.0)
        {
            float volScale = (float)region.Volume;
            for (int i = 0; i < fitted.Length; i++) fitted[i] *= volScale;
        }

        double xfadeGain = ComputeXfadeGain(region, vel);
        if (xfadeGain != 1.0)
        {
            bool siblingInBand = false;
            foreach (var sibling in patch.Regions)
            {
                if (ReferenceEquals(sibling, region)) continue;
                if (sibling.LoKey > targetMidi || sibling.HiKey < targetMidi) continue;
                if (sibling.LoVel > vel || sibling.HiVel < vel) continue;
                if (ComputeXfadeGain(sibling, vel) != 1.0) { siblingInBand = true; break; }
            }
            if (siblingInBand) xfadeGain *= 0.7071;
            float xfadeScale = (float)xfadeGain;
            for (int i = 0; i < fitted.Length; i++) fitted[i] *= xfadeScale;
        }

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

        // Phase 37 SAMP-03 (Pitfall 10 — Phase 28 helper unchanged).
        var sampleMult = SamplePathArticulationMultipliers.For(note.Articulation);
        if (sampleMult.IsNontrivial)
        {
            for (int i = 0; i < fitted.Length; i++)
                fitted[i] *= sampleMult.Sample(i, fitted.Length);
        }

        // Phase 37 MIX-02 + B2 lock — unconditional stereo + OQ4
        // additive-with-clamp pan composition.
        double effectivePan = Math.Clamp(region.Pan + voicePan, -1.0, 1.0);
        return ToStereoBufferWithPan(fitted, sampleRate, effectivePan);
    }

    /// <summary>
    /// Phase 37 SAMP-01 round-robin region picker (RESEARCH §Pattern 5).
    /// When the patch has multiple regions covering <paramref name="midiPitch"/>
    /// + <paramref name="midiVelocity"/> AND those regions declare
    /// <c>seq_length &gt; 1</c>, advance the per-group counter and return the
    /// region matching the picked <c>seq_position</c>. Otherwise return null
    /// so the caller falls through to the standard grid lookup.
    /// </summary>
    private SfzRegion? PickRoundRobinCandidate(SfzData patch, int midiPitch, int midiVelocity)
    {
        // Walk the patch's region list (declaration order) to enumerate
        // candidate alternates for the requested key+vel pair. The list is
        // typically &lt; 100 entries for orchestral patches; linear scan is
        // acceptable.
        var candidates = new List<SfzRegion>();
        int maxSeqLength = 1;
        foreach (var r in patch.Regions)
        {
            if (r.LoKey > midiPitch || r.HiKey < midiPitch) continue;
            if (r.LoVel > midiVelocity || r.HiVel < midiVelocity) continue;
            if (r.SeqLength > maxSeqLength) maxSeqLength = r.SeqLength;
            candidates.Add(r);
        }

        // Only invoke round-robin when the group declares seq_length > 1.
        // A single-candidate region (or all-default seq_length=1 regions) is
        // not a round-robin group — fall through to the grid lookup so the
        // Phase 33 last-declared-wins contract holds.
        if (candidates.Count < 2 || maxSeqLength < 2) return null;

        // Restrict the group to regions matching the dominant seq_length so
        // mixed-config patches (some declare RR, some don't) don't pull
        // non-RR regions into the rotation.
        var rrGroup = candidates.Where(r => r.SeqLength == maxSeqLength).ToList();
        if (rrGroup.Count < 2) return null;

        // Per RESEARCH §Pattern 5 the counter advances modulo seqLength on
        // each trigger. Key the counter by the FIRST region's key+vel range
        // (representative of the group — all members share the same span).
        var groupKey = (rrGroup[0].LoKey, rrGroup[0].HiKey, rrGroup[0].LoVel, rrGroup[0].HiVel);
        int counter = _rrCounter.TryGetValue(groupKey, out var c) ? c : 0;
        int targetSeqPos = (counter % maxSeqLength) + 1;
        _rrCounter[groupKey] = counter + 1;

        // Return the region at the picked seq_position. If a patch declares
        // seq_length=N but is missing one of the positions (composer error),
        // fall back to the first candidate so the song still plays.
        return rrGroup.FirstOrDefault(r => r.SeqPosition == targetSeqPos) ?? rrGroup[0];
    }

    /// <summary>
    /// Phase 37 SAMP-02 — equal-power velocity-layer crossfade gain
    /// computation (RESEARCH §Pattern 6). Returns 1.0 when the region's
    /// xfin/xfout opcodes are both absent (the Phase 33 hard-switch default).
    /// When the note velocity falls in <c>[XfinLoVel, XfinHiVel]</c>, returns
    /// <c>sin(normVel · π/2)</c> (fade-in as velocity rises). When in
    /// <c>[XfoutLoVel, XfoutHiVel]</c>, returns <c>cos(normVel · π/2)</c>
    /// (fade-out as velocity rises). Outside any declared band returns 1.0.
    /// </summary>
    private static double ComputeXfadeGain(SfzRegion region, int midiVelocity)
    {
        if (region.XfinLoVel != -1 && region.XfinHiVel != -1
            && midiVelocity >= region.XfinLoVel && midiVelocity <= region.XfinHiVel)
        {
            int bandWidth = Math.Max(1, region.XfinHiVel - region.XfinLoVel);
            double normVel = (midiVelocity - region.XfinLoVel) / (double)bandWidth;
            return Math.Sin(normVel * Math.PI / 2.0);
        }
        if (region.XfoutLoVel != -1 && region.XfoutHiVel != -1
            && midiVelocity >= region.XfoutLoVel && midiVelocity <= region.XfoutHiVel)
        {
            int bandWidth = Math.Max(1, region.XfoutHiVel - region.XfoutLoVel);
            double normVel = (midiVelocity - region.XfoutLoVel) / (double)bandWidth;
            return Math.Cos(normVel * Math.PI / 2.0);
        }
        return 1.0;
    }

    // ----- test-only surface --------------------------------------------

    /// <summary>
    /// Test-only entry point exposing <see cref="PickRoundRobinCandidate"/>
    /// for SfzRoundRobinDeterminismTests. Production callers reach the picker
    /// indirectly via <see cref="Render"/>.
    /// </summary>
    internal SfzRegion PickRegion_TestOnly(SfzData patch, int midiPitch, int midiVelocity)
    {
        var picked = PickRoundRobinCandidate(patch, midiPitch, midiVelocity);
        if (picked is not null) return picked;
        // Fall back to grid lookup so callers can still exercise the path
        // when only one region matches.
        return patch.Grid[midiPitch, midiVelocity]
               ?? throw new InvalidOperationException(
                   $"PickRegion_TestOnly: no region for ({midiPitch}, {midiVelocity})");
    }

    /// <summary>
    /// Test-only entry point exposing <see cref="ComputeXfadeGain"/> for
    /// SfzHardSwitchRegression. Production callers reach the helper
    /// indirectly via <see cref="Render"/>.
    /// </summary>
    public static double ComputeXfadeGain_TestOnly(SfzRegion region, int midiVelocity)
        => ComputeXfadeGain(region, midiVelocity);

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
            // NoLoop / OneShot — straight copy and zero-pad. fitted is mono;
            // source may be stereo (VSCO-CE patches all ship L-R interleaved),
            // so read each frame via ReadFrameMono to downmix on the fly.
            int copyLen = Math.Min(source.Frames, targetFrames);
            for (int i = 0; i < copyLen; i++) fitted[i] = ReadFrameMono(source, i);
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
                fitted[dst0] = ReadFrameMono(source, dst0);
                dst0++;
            }
            while (dst0 < targetFrames)
            {
                int rel = (dst0 - region.LoopStart) % loopLen;
                fitted[dst0] = ReadFrameMono(source, region.LoopStart + rel);
                dst0++;
            }
            return fitted;
        }

        // Stage 1: pre-attack [0, LoopStart) plays once at the head.
        int dst = 0;
        int headEnd = Math.Min(region.LoopStart, targetFrames);
        for (; dst < headEnd; dst++) fitted[dst] = ReadFrameMono(source, dst);

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
                fitted[dst++] = ReadFrameMono(source, srcReadPos++);
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
                fitted[dst++] = wA * ReadFrameMono(source, srcA) + wB * ReadFrameMono(source, srcB);
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
    /// Read a single frame from <paramref name="source"/> as a mono sample,
    /// averaging across all channels. Stereo sources (VSCO-CE patches are all
    /// L-R interleaved) require this downmix because <c>AssembleBody</c>'s
    /// <c>fitted[]</c> buffer is mono. Indexing <c>source.Data[frame]</c>
    /// directly would alias the interleaved stream as half-rate mono — every
    /// note plays an octave low with R-channel fizz between L samples.
    /// </summary>
    private static float ReadFrameMono(AudioBuffer source, int frame)
    {
        int ch = source.Channels;
        if (ch == 1) return source.Data[frame];
        int baseIdx = frame * ch;
        float sum = 0f;
        for (int c = 0; c < ch; c++) sum += source.Data[baseIdx + c];
        return sum / ch;
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
