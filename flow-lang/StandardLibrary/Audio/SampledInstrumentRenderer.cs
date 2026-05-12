using System;
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
/// (fast attack, full sustain, short release) and let the articulation rules layer cleanly
/// on top without double-shaping the natural sample envelope:
///   <c>baseAttack = 0.005s, baseDecay = 0.05s, baseSustain = 1.0, baseRelease = 0.05s</c>.
/// With Articulation.Normal this is effectively unity gain through the sample's body;
/// Staccato/Marcato fast-truncate; Tenuto softens the release; Sforzando spikes the head.
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
    public AudioBuffer Render(MusicalNoteData note, int sampleRate, double durationBeats, double bpm, RenderTuning tuning)
    {
        if (note.IsRest)
            return SynthUtils.CreateSilence(sampleRate, durationBeats, bpm);

        double durationSeconds = SynthUtils.BeatsToSeconds(durationBeats, bpm);
        int targetFrames = (int)(durationSeconds * sampleRate);
        if (targetFrames <= 0)
            return new AudioBuffer(0, 1, sampleRate);

        int targetMidi = PitchConversion.GetMidiNote(note.NoteName, note.Octave, note.Alteration);
        int sampleMidi = _cache.NearestSamplePitch(_instrument, targetMidi);
        int semitonesShift = targetMidi - sampleMidi;

        float[] mono;
        if (_hasVelocityLayers)
        {
            // Piano path: crossfade pp + ff (REQ-3 velocity-driven timbre).
            var pp = _cache.GetVarispeed(_instrument, sampleMidi, "pp", semitonesShift);
            var ff = _cache.GetVarispeed(_instrument, sampleMidi, "ff", semitonesShift);
            if (pp is null || ff is null)
            {
                // Sample missing — return silence (caller's responsibility to populate bundle).
                // The render still respects the authored duration so downstream mixing is unaffected.
                return SynthUtils.CreateSilence(sampleRate, durationBeats, bpm);
            }
            double v = Math.Clamp(note.Velocity, 0.0, 1.0);
            mono = LoudnessNormalizedCrossfade(pp.Data, ff.Data, v);
        }
        else
        {
            // Single-velocity path: linear amplitude scaling
            var mf = _cache.GetVarispeed(_instrument, sampleMidi, "mf", semitonesShift);
            if (mf is null)
                return SynthUtils.CreateSilence(sampleRate, durationBeats, bpm);
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
        // ON TOP of the sample. The recorded WAV provides the instrument timbre; the
        // envelope shapes attack/sustain/release per Phase 28 SPEC-5 locked rules
        // (Staccato/Marcato truncate, Tenuto softens release, Sforzando spikes the head,
        // etc.). Near-transparent baseline ADSR chosen because the sample already carries
        // the natural attack/decay envelope — see class doc-comment for the rationale.
        float[] envelope = SynthUtils.GenerateArticulationADSR(
            note.Articulation,
            baseAttack: 0.005, baseDecay: 0.05, baseSustain: 1.0, baseRelease: 0.05,
            frames: targetFrames, sampleRate: sampleRate, isPercussion: false);
        SynthUtils.ApplyEnvelope(fitted, envelope);

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
}
