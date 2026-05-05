using System;
using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Audio.Synthesizers;

/// <summary>
/// Wavetable synthesizer that plays back a user-defined single-cycle waveform
/// using phase-increment traversal with linear interpolation.
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

        // Apply ADSR envelope for clean attack/release
        float[] envelope = SynthUtils.GenerateADSR(0.005, 0.05, 0.7, 0.05, numSamples, sampleRate);
        SynthUtils.ApplyEnvelope(samples, envelope);

        return SynthUtils.ToMonoBuffer(samples, sampleRate);
    }
}
