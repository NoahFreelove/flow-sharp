using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Audio.Synthesizers;

/// <summary>
/// Risset bell synthesizer with inharmonic partials and per-partial
/// exponential decay. Produces metallic, shimmering bell tones where
/// higher partials decay faster than lower ones.
/// </summary>
public class BellSynthesizer : INoteSynthesizer
{
    // Inharmonic partial frequency ratios (Risset-style)
    private static readonly double[] PartialRatios = { 1.0, 2.2, 3.6, 4.1, 5.8 };

    // Relative amplitude weights per partial
    private static readonly double[] PartialAmplitudes = { 1.0, 0.6, 0.4, 0.3, 0.2 };

    // Exponential decay rates per partial (higher partials decay faster)
    private static readonly double[] DecayRates = { 2.0, 3.0, 4.5, 5.5, 7.0 };

    // Short attack ramp length in samples to avoid click
    private const int AttackSamples = 50;

    public AudioBuffer RenderNote(MusicalNoteData note, int sampleRate, double durationBeats, double bpm, RenderTuning tuning)
    {
        if (note.IsRest)
            return SynthUtils.CreateSilence(sampleRate, durationBeats, bpm);

        double frequency = PitchConversion.NoteToFrequency(note, tuning);
        double durationSeconds = SynthUtils.BeatsToSeconds(durationBeats, bpm);
        int numSamples = (int)(durationSeconds * sampleRate);
        if (numSamples <= 0)
            return new AudioBuffer(0, 1, sampleRate);

        var samples = new float[numSamples];
        double baseAmp = 0.15 * note.Velocity;
        double nyquist = sampleRate / 2.0;
        double twoPi = 2.0 * Math.PI;

        // Render each inharmonic partial with its own exponential decay
        for (int p = 0; p < PartialRatios.Length; p++)
        {
            double partialFreq = frequency * PartialRatios[p];
            if (partialFreq >= nyquist)
                continue;

            double amp = baseAmp * PartialAmplitudes[p];
            double rate = DecayRates[p];
            double phaseInc = twoPi * partialFreq / sampleRate;
            double phase = 0.0;

            for (int i = 0; i < numSamples; i++)
            {
                double t = i / (double)sampleRate;
                double envelope = Math.Exp(-rate * t);
                samples[i] += (float)(amp * envelope * Math.Sin(phase));
                phase += phaseInc;
            }
        }

        // Short linear attack ramp to avoid initial click
        int rampLen = Math.Min(AttackSamples, numSamples);
        for (int i = 0; i < rampLen; i++)
        {
            samples[i] *= i / (float)rampLen;
        }

        return SynthUtils.ToMonoBuffer(samples, sampleRate);
    }
}
