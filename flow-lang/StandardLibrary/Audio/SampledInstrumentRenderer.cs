using System;
using FlowLang.Diagnostics;
using FlowLang.StandardLibrary.Audio.Synthesizers;
using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Audio;

/// <summary>
/// Phase 29 — sample-based instrument renderer for the 6 tonal instruments
/// (Piano, Brass, Sax, Strings, Flute, Bell).
///
/// Implements the INoteSynthesizer-shaped <see cref="Render"/> method without
/// (yet) implementing the interface directly — Plan 03 wires the existing
/// tonal Synthesizer classes (PianoSynthesizer, etc.) to delegate here. For
/// this plan the renderer exists alongside the synth path so any infrastructure
/// bugs surface before the production tonal synths start depending on it.
///
/// Rendering algorithm (REQ-1):
///   1. Look up the closest-pitched sample via <see cref="SampleCache.NearestSamplePitch"/>.
///   2. Varispeed-shift to the exact target pitch via <see cref="SampleCache.GetVarispeed"/>.
///   3. Apply velocity:
///       * Piano (hasVelocityLayers = true): linear crossfade between pp and ff layers
///         using note velocity as the mix coefficient (SPEC REQ-3 formula).
///       * Other tonal instruments: linear amplitude scaling by velocity.
///   4. Trim or zero-pad the resulting mono buffer to the authored note duration.
///   5. Apply the Phase 28 articulation envelope on top of the fitted sample buffer
///      (REQ-5) — see Phase 28 envelope helper note below.
///   6. Wrap in an AudioBuffer at the engine's sample rate via <see cref="SynthUtils.ToMonoBuffer"/>.
///
/// Phase 28 envelope helper:
///   <c>SynthUtils.GenerateArticulationADSR(Articulation, baseAttack, baseDecay,
///   baseSustain, baseRelease, frames, sampleRate, isPercussion)</c> → float[] envelope curve.
///   Applied in-place via <c>SynthUtils.ApplyEnvelope(float[] buffer, float[] envelope)</c>.
/// Located in: flow-lang/StandardLibrary/Audio/SynthUtils.cs (Phase 28 SPEC-5 helper).
/// Invokes the locked rules: Staccato/Marcato (attack × 0.66, sustain = 0, release × 0.5),
///   Tenuto (release × 1.2 soft), Legato/Accent/Normal (synth-default ADSR baseline),
///   Sforzando (synth-default ADSR + 1.5× → 1.0× spike over the first 15% of frames).
/// (Per Phase 28 SPEC Req 4 — locked rules; Articulation.Legato is a first-class enum value
/// per Phase 28 SPEC-3, distinct from the Phase 22 legato() transform that adjusts
/// DurationOverlap — both compose.)
///
/// Baseline ADSR choice for sampled instruments: the recorded WAV already carries the
/// instrument's natural attack/decay envelope, so we pick a near-transparent baseline
/// (fast attack, full sustain, NO release) and let the articulation rules layer cleanly
/// on top without double-shaping the natural sample envelope:
///   <c>baseAttack = 0.005s, baseDecay = 0.05s, baseSustain = 1.0, baseRelease = 0.0s</c>.
/// With Articulation.Normal this is effectively unity gain through the sample's body;
/// Staccato/Marcato fast-truncate (sustain = 0); Sforzando spikes the head.
///
/// <para>baseRelease is 0.0 by design (debug session <c>varispeed-aliasing-static</c>,
/// 2026-06-26). A non-zero ADSR release ramped sustained notes DOWN to ~0 at the
/// authored-end frame, but the exponential release tail below restarts at level=1.0
/// on the RAW sample — so the signal jumped from ~0 back to full amplitude in a single
/// sample, a per-note step discontinuity that stacked into an audible per-beat "static"
/// in dense short-note passages (ragtime RH). With baseRelease=0 the envelope holds at
/// the sustain level through the authored end (1.0 for sustained articulations), meeting
/// the tail's level=1.0 start CONTINUOUSLY. The exponential tail IS the release for
/// sustained notes; its length stays composer-controlled via the <c>release=</c> knob.
/// Staccato/Marcato keep sustain=0 (their short, detached character) and are unchanged by
/// this — they already ended at 0 regardless of the release ramp.</para>
/// </summary>
public class SampledInstrumentRenderer
{
    private readonly SampleCache _cache;
    private readonly string _instrument;
    private readonly bool _hasVelocityLayers;

    public SampledInstrumentRenderer(SampleCache cache, string instrument, bool hasVelocityLayers)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _instrument = (instrument ?? string.Empty).ToLowerInvariant();
        _hasVelocityLayers = hasVelocityLayers;
    }

    /// <summary>
    /// Renders a single note to an AudioBuffer using the sample bundle. Signature mirrors
    /// <c>INoteSynthesizer.RenderNote</c> so Plan 03's delegating-shell synth classes can
    /// forward straight through. The <paramref name="tuning"/> argument is accepted for
    /// signature compatibility but is not consumed here — sample-based rendering uses the
    /// 12-TET MIDI number from <see cref="PitchConversion.GetMidiNote"/> directly. Phase 23
    /// non-12-TET tuning support for sample paths is deferred (samples are recorded at fixed
    /// pitches, so honouring just-intonation / Pythagorean offsets would require per-render
    /// varispeed math beyond Phase 29's scope).
    /// </summary>
    /// <summary>
    /// Phase 37 PIANO-01 (Plan 37-04) D-37-11 LOCK — release tail default in seconds.
    /// 1.5s reference value per Lehtonen 2007 / RESEARCH §Pattern 8: matches typical
    /// sustained-piano expectations and the upright-piano decay envelope of the
    /// U-Iowa MIS source. Composer overrides via the <c>release=</c> named arg on
    /// <c>renderSong</c>, threaded through this renderer via the <c>releaseSec</c>
    /// parameter on the per-note <see cref="Render"/> call (T-37-04-04 clamps the
    /// override to [0.05, 10.0]).
    /// </summary>
    public const double DefaultReleaseSec = 1.5;

    private const double MinReleaseSec = 0.05;
    private const double MaxReleaseSec = 10.0;

    /// <summary>
    /// Phase 29 entry point — Phase 37 PIANO-01 (Plan 37-04) adds the
    /// <paramref name="releaseSec"/> overload below. This zero-arg form
    /// preserves the existing call-site contract (BrassSynthesizer, etc. that
    /// never need a per-render release knob) by deferring to the locked
    /// <see cref="DefaultReleaseSec"/>.
    /// </summary>
    public AudioBuffer Render(MusicalNoteData note, int sampleRate, double durationBeats, double bpm, RenderTuning tuning)
        => Render(note, sampleRate, durationBeats, bpm, tuning, DefaultReleaseSec);

    /// <summary>
    /// Phase 37 PIANO-01 (Plan 37-04) — release-aware overload.
    /// <paramref name="releaseSec"/> drives both the post-authored tail window
    /// AND the tail decay time constant (RESEARCH §Pattern 8: time-constant =
    /// releaseSec × 0.3 so a 1.5s release produces an audible tail across the
    /// full 1.5s window, a 0.3s release produces a sharper cutoff, etc.).
    /// Clamped to [<see cref="MinReleaseSec"/>, <see cref="MaxReleaseSec"/>]
    /// per T-37-04-04 with a one-shot stderr advisory on clamp.
    /// </summary>
    public AudioBuffer Render(
        MusicalNoteData note, int sampleRate, double durationBeats, double bpm,
        RenderTuning tuning, double releaseSec)
    {
        if (note.IsRest)
            return SynthUtils.CreateSilence(sampleRate, durationBeats, bpm);

        // T-37-04-04 — clamp release knob to a sane band. Charitable interpretation
        // (CLAUDE.md): never throw on a bad knob, clamp + advise once per process.
        double clampedRelease = releaseSec;
        if (double.IsNaN(releaseSec) || releaseSec < MinReleaseSec)
        {
            clampedRelease = MinReleaseSec;
            if (releaseSec < MinReleaseSec)
                RenderingDiagnostics.WarnOnce("piano:release:clamp-low",
                    $"[piano] release={releaseSec:F3}s below floor {MinReleaseSec}s — clamped");
        }
        else if (releaseSec > MaxReleaseSec)
        {
            clampedRelease = MaxReleaseSec;
            RenderingDiagnostics.WarnOnce("piano:release:clamp-high",
                $"[piano] release={releaseSec:F3}s above ceiling {MaxReleaseSec}s — clamped");
        }

        double durationSeconds = SynthUtils.BeatsToSeconds(durationBeats, bpm);
        // Phase 37 PIANO-01 — tail window scales with the composer's release= knob.
        // 1.5s default (D-37-11) sustains naturally; 2.0s feels concert-grand-like;
        // 0.8s feels upright-piano-like. Sample's natural decay is exposed across the
        // window via an exponential tail-fade (see post-envelope ramp below).
        double tailSeconds = clampedRelease;
        int authoredFrames = (int)(durationSeconds * sampleRate);
        int targetFrames = authoredFrames + (int)(tailSeconds * sampleRate);
        if (targetFrames <= 0)
            return new AudioBuffer(0, 1, sampleRate);

        int targetMidi = PitchConversion.GetMidiNote(note.NoteName, note.Octave, note.Alteration);
        int sampleMidi = _cache.NearestSamplePitch(_instrument, targetMidi);
        int semitonesShift = targetMidi - sampleMidi;

        float[] mono;
        if (_hasVelocityLayers)
        {
            // Phase 37 PIANO-01 (Plan 37-04) D-37-09 LOCK — 4-way velocity crossfade
            // (pp/mp/mf/ff) replaces the Phase 29 2-way (pp/ff). mp is synthesized by
            // SampleCache at eager-load per RESEARCH §Pattern 9 Path 1; mf is a real
            // U-Iowa MIS recording. Charitable fallback (T-37-04-02): if mp OR mf is
            // missing (composer skipped the Task 2 user_setup drop), fall back to the
            // existing 2-way pp/ff path with a one-shot advisory.
            var pp = _cache.GetVarispeed(_instrument, sampleMidi, "pp", semitonesShift);
            var ff = _cache.GetVarispeed(_instrument, sampleMidi, "ff", semitonesShift);
            if (pp is null || ff is null)
            {
                // sweep-0614 fix — sample missing (Web target strips Samples/**;
                // a fresh clone may not have fetched the bundle). Render as a rest
                // so downstream mixing keeps the authored duration, but advise once
                // per instrument so the silent render isn't diagnostic-free
                // (mirrors SfzRenderer's missing-sample contract).
                WarnMissingSamples(sampleMidi);
                return SynthUtils.CreateSilence(sampleRate, durationBeats, bpm);
            }
            var mp = _cache.GetVarispeed(_instrument, sampleMidi, "mp", semitonesShift);
            var mf = _cache.GetVarispeed(_instrument, sampleMidi, "mf", semitonesShift);
            double v = Math.Clamp(note.Velocity, 0.0, 1.0);
            if (mp is null || mf is null)
            {
                RenderingDiagnostics.WarnOnce("piano:mp_mf:missing",
                    "[piano] 4-way velocity crossfade unavailable (mp/mf layer missing) — falling back to 2-way pp/ff crossfade. Drop the 5 _mf.wav files at flow-lang/Samples/piano/ to enable the warmer 4-way path (Plan 37-04 user_setup).");
                mono = LoudnessNormalizedCrossfade(pp.Data, ff.Data, v);
            }
            else
            {
                mono = LoudnessNormalized4WayCrossfade(pp.Data, mp.Data, mf.Data, ff.Data, v);
            }
        }
        else
        {
            // Single-velocity path: linear amplitude scaling
            var mf = _cache.GetVarispeed(_instrument, sampleMidi, "mf", semitonesShift);
            if (mf is null)
            {
                // sweep-0614 fix — see velocity-layer branch above. Warn once
                // instead of returning diagnostic-free silence.
                WarnMissingSamples(sampleMidi);
                return SynthUtils.CreateSilence(sampleRate, durationBeats, bpm);
            }
            mono = new float[mf.Data.Length];
            double v = Math.Clamp(note.Velocity, 0.0, 1.0);
            for (int i = 0; i < mono.Length; i++) mono[i] = (float)(mf.Data[i] * v);
        }

        // Trim or pad to authored duration. Beyond mono.Length, fitted defaults to zero
        // (the array initializer) — natural silence pad when the sample is shorter than
        // the authored note duration.
        var fitted = new float[targetFrames];
        int copyLen = Math.Min(mono.Length, targetFrames);
        Array.Copy(mono, fitted, copyLen);

        // Phase 29 REQ-5 / REQ-D-17 / D-18 / D-19: Phase 28 articulation envelope applies
        // ON TOP of the sample. The envelope is shaped against the AUTHORED duration so
        // the release ramp lands at the authored end-of-note; the additional tail past
        // that point fades exponentially via a separate post-envelope ramp that lets the
        // natural sample decay ring out (piano-pedal-like sustain).
        // baseRelease = 0.0: the envelope holds the sustain level through the authored
        // end so it meets the exponential release tail (which restarts at level=1.0 on
        // the raw sample) CONTINUOUSLY. A non-zero release used to ramp sustained notes
        // to ~0 right before the tail jumped back to full amplitude — a per-note step
        // discontinuity that stacked into audible per-beat "static" (debug session
        // varispeed-aliasing-static, 2026-06-26). The tail is the release for sustained
        // notes; Staccato/Marcato keep their sustain=0 short character (unaffected).
        float[] envelope = SynthUtils.GenerateArticulationADSR(
            note.Articulation,
            baseAttack: 0.005, baseDecay: 0.05, baseSustain: 1.0, baseRelease: 0.0,
            frames: authoredFrames, sampleRate: sampleRate, isPercussion: false);
        for (int i = 0; i < authoredFrames && i < fitted.Length; i++)
            fitted[i] *= envelope[i];

        // Phase 37 SAMP-03 (Pitfall 10) — sample-path articulation multiplier overlay.
        // Stacks AFTER the Phase 28 envelope; Phase 28's
        // SynthUtils.GenerateArticulationADSR is unchanged so synth-path
        // regression baselines stay green.
        //
        // sweep-0614 fix — bound the multiplier to the AUTHORED window and sample
        // its A/D/S/R quartiles against authoredFrames (NOT fitted.Length). The
        // Phase 28 envelope above is shaped over authoredFrames, but the buffer
        // carries an extra release tail (up to 1.5s for piano). Sampling the
        // quartile buckets over the full fitted.Length smeared the staccato
        // 'attack' bucket (0.5×) across the authored note PLUS the start of the
        // tail, and landed the 'decay' (1.2×) / 'release' (0.8×) buckets deep in
        // the tail — overlaying a non-monotonic bump on what should be a smooth
        // exponential ring-out. Bounding the window aligns the quartiles with the
        // ADSR stages they reshape and matches the SFZ path (where the multiplier
        // window equals the note duration).
        var sampleMult = SamplePathArticulationMultipliers.For(note.Articulation);
        if (sampleMult.IsNontrivial)
        {
            int multFrames = Math.Min(authoredFrames, fitted.Length);
            for (int i = 0; i < multFrames; i++)
                fitted[i] *= sampleMult.Sample(i, authoredFrames);
        }

        // Phase 37 PIANO-01 — tail fade decay time-constant scales with releaseSec
        // (RESEARCH §Pattern 8: time-constant = releaseSec × 0.3). A 1.5s release →
        // 0.45 time-constant → exp(-1/(sr*0.45)) per frame → audible energy across the
        // full 1.5s window. A 0.3s release → 0.09 → near-silence after 0.3s. Pre-Phase-37
        // used a hard-coded 0.15 time constant + 0.5s window (Phase 29). The scaling
        // factor (×0.3) is the locked default per Pattern 8 — releaseSec is the
        // composer-facing knob, time-constant is derived.
        if (authoredFrames < fitted.Length)
        {
            double tailDecayPerFrame = Math.Exp(-1.0 / (sampleRate * clampedRelease * 0.3));
            double level = 1.0;
            for (int i = authoredFrames; i < fitted.Length; i++)
            {
                fitted[i] = (float)(fitted[i] * level);
                level *= tailDecayPerFrame;
            }
        }

        return SynthUtils.ToMonoBuffer(fitted, sampleRate);
    }

    /// <summary>
    /// REQ-3 velocity-layer crossfade with loudness normalization + transition band.
    ///
    /// Why not a plain <c>(1 - v) * a + v * b</c>? The raw pp and ff samples are
    /// recorded at very different acoustic levels — for example the bundled
    /// University of Iowa C4 samples have pp peak ≈ 0.008 (~ -42 dBFS) and ff peak
    /// ≈ 0.187 (~ -15 dBFS), a factor of 23×. A naive crossfade leaves ff dominant
    /// in BOTH the soft (v=0.2) and loud (v=0.95) outputs — because 0.2 × 0.187 is
    /// still 4× larger than 0.8 × 0.008. The spectral mix collapses to "mostly ff"
    /// for any non-trivial v, defeating the timbre-vs-amplitude distinction REQ-3
    /// is built on.
    ///
    /// Three-stage formula:
    ///   1. Per-array RMS-normalize pp and ff to a common reference level
    ///      (max of the two RMS values), so the spectral mix coefficient cleanly
    ///      controls which timbre dominates without dynamic-range domination.
    ///   2. Map velocity to a mix coefficient via a piecewise-linear curve with a
    ///      transition band (Phase 29 SPEC, Plan 03 success criteria):
    ///        v ≤ <see cref="VelocityTransitionLow"/>  → pure pp (mix = 0)
    ///        v ≥ <see cref="VelocityTransitionHigh"/> → pure ff (mix = 1)
    ///        in between → linear interpolation.
    ///      This makes soft notes (v ≤ 0.4) carry the pp timbre cleanly and loud
    ///      notes (v ≥ 0.6) the ff timbre cleanly, ensuring REQ-3's cosSim &lt; 0.92
    ///      acceptance gate holds even when the raw pp/ff pair are themselves
    ///      moderately similar (raw cosSim ≈ 0.88 for the bundled samples).
    ///   3. Apply a velocity-driven amplitude envelope so loud notes are audibly
    ///      louder than soft notes. The envelope scales the normalized output by
    ///      <c>ppRms × (1 - v) + ffRms × v</c> — the linear interpolation
    ///      between the two source RMS levels, normalized by refRms.
    /// </summary>
    private static float[] LoudnessNormalizedCrossfade(float[] a, float[] b, double v)
    {
        int n = Math.Min(a.Length, b.Length);
        var output = new float[n];
        if (n == 0) return output;

        double rmsA = Rms(a, n);
        double rmsB = Rms(b, n);
        // Reference loudness (max of pp / ff RMS) is the level both normalized
        // arrays sit at before the per-note velocity envelope re-scales them.
        double refRms = Math.Max(rmsA, rmsB);
        if (refRms < 1e-9)
            return output; // both arrays silent — nothing to crossfade.

        double scaleA = rmsA > 1e-9 ? refRms / rmsA : 0.0;
        double scaleB = rmsB > 1e-9 ? refRms / rmsB : 0.0;

        // Mix coefficient — piecewise linear with transition band (REQ-3).
        double mix = MapVelocityToMix(v);

        // Per-note dynamic-range envelope: linearly interpolate between the two
        // source-RMS levels so loud > soft in absolute amplitude. Normalize by
        // refRms so the final scaling factor is in [0, 1] of the louder source.
        double targetRmsRatio = (rmsA * (1.0 - v) + rmsB * v) / refRms;

        for (int i = 0; i < n; i++)
        {
            double mixed = (1.0 - mix) * a[i] * scaleA + mix * b[i] * scaleB;
            output[i] = (float)(mixed * targetRmsRatio);
        }
        return output;
    }

    // REQ-3 velocity-to-mix transition band. Below VelocityTransitionLow the mix is
    // pure pp (mix = 0); above VelocityTransitionHigh it's pure ff (mix = 1); between
    // the two bounds, mix interpolates linearly. The 0.4 / 0.6 split sits symmetrically
    // around v=0.5 so the "favor pp/ff at the velocity boundary" success criterion
    // resolves cleanly.
    private const double VelocityTransitionLow = 0.4;
    private const double VelocityTransitionHigh = 0.6;

    private static double MapVelocityToMix(double v)
    {
        if (v <= VelocityTransitionLow) return 0.0;
        if (v >= VelocityTransitionHigh) return 1.0;
        return (v - VelocityTransitionLow) / (VelocityTransitionHigh - VelocityTransitionLow);
    }

    /// <summary>
    /// sweep-0614 fix — one-shot advisory when no WAV is loaded for this
    /// instrument (Web target strips the U-Iowa MIS bundle; a fresh clone may
    /// not have fetched <c>flow-lang/Samples/</c>). Keyed by instrument so it
    /// fires once per process per instrument, matching the
    /// <c>SfzRenderer</c> missing-sample contract. Charitable: the render still
    /// returns a duration-correct silence buffer (a rest), it just stops being
    /// diagnostic-free.
    /// </summary>
    private void WarnMissingSamples(int sampleMidi)
    {
        RenderingDiagnostics.WarnOnce(
            $"sample:missing:{_instrument}",
            $"[sample] no WAV loaded for '{_instrument}' (nearest MIDI {sampleMidi}) — rendered as rest. " +
            "On the Web target the U-Iowa MIS bundle is stripped; build with FlowTarget=Desktop " +
            "or fetch flow-lang/Samples/ to enable sampled playback.");
    }

    private static double Rms(float[] samples, int n)
    {
        if (n <= 0) return 0.0;
        double sumSq = 0.0;
        for (int i = 0; i < n; i++)
        {
            double s = samples[i];
            sumSq += s * s;
        }
        return Math.Sqrt(sumSq / n);
    }

    /// <summary>
    /// Phase 37 PIANO-01 (Plan 37-04) D-37-09 LOCK — 4-way velocity crossfade
    /// (pp/mp/mf/ff). Splits the velocity axis [0, 1] into 3 transition bands:
    ///   v in [0.00, 0.33) → pp ↔ mp
    ///   v in [0.33, 0.66) → mp ↔ mf
    ///   v in [0.66, 1.00] → mf ↔ ff
    /// Within each band, delegates to the existing
    /// <see cref="LoudnessNormalizedCrossfade"/> with a band-local velocity (so
    /// the per-band pp↔ff transition-band semantics are inherited verbatim — soft
    /// notes carry the lower-velocity timbre cleanly, loud notes carry the upper).
    /// Phase 29 REQ-3 cosSim &lt; 0.92 acceptance gate holds within each band
    /// because LoudnessNormalizedCrossfade's own transition-band mapping fires.
    /// </summary>
    private static float[] LoudnessNormalized4WayCrossfade(
        float[] pp, float[] mp, float[] mf, float[] ff, double v)
    {
        // 3 transition bands across [0, 1].
        if (v < 0.33)
        {
            double vLocal = v / 0.33;
            return LoudnessNormalizedCrossfade(pp, mp, vLocal);
        }
        else if (v < 0.66)
        {
            double vLocal = (v - 0.33) / 0.33;
            return LoudnessNormalizedCrossfade(mp, mf, vLocal);
        }
        else
        {
            double vLocal = (v - 0.66) / 0.34;
            return LoudnessNormalizedCrossfade(mf, ff, vLocal);
        }
    }
}
