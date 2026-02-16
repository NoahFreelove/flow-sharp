using System;
using FlowLang.StandardLibrary.Audio.DSP;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Audio.Synthesizers;

/// <summary>
/// MIDI-style flute synthesizer. Square wave with vibrato (5 Hz, ~6 cents depth),
/// a nasal sine harmonic at 3x frequency, ADSR shaping, then lowpass + bandpass
/// formant filtering for a breathy, pipe-like character.
/// </summary>
public class FluteSynthesizer : INoteSynthesizer
{
    private const double VibratoRate = 5.0;   // Hz
    private const double VibratoDepth = 0.003464; // ~6 cents: 2^(6/1200) - 1

    public AudioBuffer RenderNote(MusicalNoteData note, int sampleRate, double durationBeats, double bpm)
    {
        if (note.IsRest)
            return SynthUtils.CreateSilence(sampleRate, durationBeats, bpm);

        double frequency = PitchConversion.NoteToFrequency(note);
        double durationSeconds = SynthUtils.BeatsToSeconds(durationBeats, bpm);
        int numSamples = (int)(durationSeconds * sampleRate);
        if (numSamples <= 0)
            return new AudioBuffer(0, 1, sampleRate);

        var samples = new float[numSamples];

        // Square wave with vibrato (manual sample loop for FM)
        double phase = 0.0;
        for (int i = 0; i < numSamples; i++)
        {
            double t = i / (double)sampleRate;
            double vibrato = 1.0 + VibratoDepth * Math.Sin(2.0 * Math.PI * VibratoRate * t);
            double freq = frequency * vibrato;
            double phaseInc = freq / sampleRate;
            samples[i] += (float)(0.18 * note.Velocity * (phase < 0.5 ? 1.0 : -1.0));
            phase += phaseInc;
            if (phase >= 1.0) phase -= 1.0;
        }

        // Nasal sine harmonic at 3x frequency
        SynthUtils.GenerateSine(samples, frequency * 3, 0.04 * note.Velocity, sampleRate);

        // ADSR envelope
        float[] envelope = SynthUtils.GenerateADSR(
            attack: 0.06, decay: 0.08, sustain: 0.65, release: 0.12,
            frames: numSamples, sampleRate: sampleRate);
        SynthUtils.ApplyEnvelope(samples, envelope);

        // Lowpass for general brightness control
        SynthUtils.OnePoleLP(samples, 2500.0 + frequency, sampleRate);

        // Bandpass formant shaping via biquad Filter — wrap into AudioBuffer, filter, extract
        float nyquist = sampleRate / 2f;
        float bpLow = (float)(frequency * 0.8);
        float bpHigh = (float)Math.Min(frequency * 6.0, nyquist - 1.0);

        if (bpHigh > bpLow + 1f)
        {
            var tempBuf = SynthUtils.ToMonoBuffer(samples, sampleRate);
            var filtered = Filter.Bandpass(tempBuf, bpLow, bpHigh);
            Array.Copy(filtered.Data, samples, numSamples);
        }

        return SynthUtils.ToMonoBuffer(samples, sampleRate);
    }
}
