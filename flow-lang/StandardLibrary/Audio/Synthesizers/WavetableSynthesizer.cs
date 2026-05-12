using System;
using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Audio.Synthesizers;

/// <summary>
/// Wavetable synthesizer that plays back a user-defined single-cycle waveform
/// using phase-increment traversal with linear interpolation.
///
/// Phase 29 Plan 05 (REQ-6 / SPEC D-22): the synth class itself stays
/// composition-agnostic — the new "warm" / "bright" / "buzz" preset variants
/// live in <see cref="WavetableVariants"/> and are registered with
/// <see cref="SynthesizerFactory"/> on the first <c>Create</c> call via
/// <c>EnsureBuiltinVariantsRegistered</c>. The named tables flow through
/// <see cref="SynthesizerFactory.RegisterWavetable"/> and are then accessible
/// to composers as ordinary instrument names:
///
///     renderSong s "warm"   // upper-partial-boosted saw (vintage-pad timbre)
///     renderSong s "bright" // narrow-pulse train (~10% duty cycle)
///     renderSong s "buzz"   // 15-harmonic 1/√n-weighted additive supersaw
///
/// Each variant's wavetable is intentionally richer in upper partials than
/// the canonical Phase 28 sawtooth, satisfying the SPEC D-23 ≥ 20%
/// harmonic-richness-ratio gain over the pinned baseline.
/// </summary>
public class WavetableSynthesizer : INoteSynthesizer
{
    private readonly float[] _wavetable;

    public WavetableSynthesizer(float[] wavetable)
    {
        _wavetable = wavetable ?? throw new ArgumentNullException(nameof(wavetable));
        if (_wavetable.Length == 0)
            throw new ArgumentException("Wavetable must contain at least one sample", nameof(wavetable));
    }

    public AudioBuffer RenderNote(MusicalNoteData note, int sampleRate, double durationBeats, double bpm, RenderTuning tuning)
    {
        if (note.IsRest)
            return SynthUtils.CreateSilence(sampleRate, durationBeats, bpm);

        double frequency = PitchConversion.NoteToFrequency(note, tuning);
        double durationSeconds = SynthUtils.BeatsToSeconds(durationBeats, bpm);
        int numSamples = (int)(durationSeconds * sampleRate);
        if (numSamples <= 0)
            return new AudioBuffer(0, 1, sampleRate);

        float[] samples = new float[numSamples];
        double phase = 0.0;
        double phaseInc = frequency / sampleRate;
        double amplitude = 0.3 * note.Velocity;
        int tableSize = _wavetable.Length;

        for (int i = 0; i < numSamples; i++)
        {
            double tablePos = phase * tableSize;
            int idx0 = (int)tablePos;
            int idx1 = (idx0 + 1) % tableSize;
            double frac = tablePos - idx0;
            samples[i] = (float)(amplitude * (_wavetable[idx0] * (1.0 - frac) + _wavetable[idx1] * frac));
            phase += phaseInc;
            phase -= Math.Floor(phase); // wrap 0..1
        }

        // Apply ADSR envelope for clean attack/release (Phase 28 SPEC-5: articulation-aware)
        // Baseline: attack 0.005, decay 0.05, sustain 0.7, release 0.05
        float[] envelope = SynthUtils.GenerateArticulationADSR(
            note.Articulation,
            baseAttack: 0.005, baseDecay: 0.05, baseSustain: 0.7, baseRelease: 0.05,
            frames: numSamples, sampleRate: sampleRate);
        SynthUtils.ApplyEnvelope(samples, envelope);

        return SynthUtils.ToMonoBuffer(samples, sampleRate);
    }
}
