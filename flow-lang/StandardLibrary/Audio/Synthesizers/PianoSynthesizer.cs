using FlowLang.StandardLibrary.Audio.DSP;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Audio.Synthesizers;

/// <summary>
/// Grand piano synthesizer with inharmonic partials, hammer strike transient,
/// string beating, and pitch-dependent filtering for a realistic tone.
/// </summary>
public class PianoSynthesizer : INoteSynthesizer
{
    public AudioBuffer RenderNote(MusicalNoteData note, int sampleRate, double durationBeats, double bpm)
    {
        if (note.IsRest)
            return SynthUtils.CreateSilence(sampleRate, durationBeats, bpm);

        double frequency = PitchConversion.NoteToFrequency(note);
        double durationSeconds = SynthUtils.BeatsToSeconds(durationBeats, bpm);
        int numSamples = (int)(durationSeconds * sampleRate);
        if (numSamples <= 0)
            return new AudioBuffer(0, 1, sampleRate);

        int midiNote = PitchConversion.GetMidiNote(note.NoteName, note.Octave, note.Alteration);

        // Pitch-dependent inharmonicity: higher for bass strings, lower for treble
        double inharmonicity = 0.0004 * Math.Exp((60 - midiNote) * 0.02);

        var samples = new float[numSamples];
        double baseAmp = 0.18 * note.Velocity;

        // --- 1. Inharmonic partials with string beating ---
        // Render fundamental as a detuned pair (~1.7 cents) for slow amplitude beating
        double detune = 0.001;
        SynthUtils.GenerateSine(samples, frequency, baseAmp * 0.5, sampleRate);
        SynthUtils.GenerateSine(samples, frequency * (1.0 + detune), baseAmp * 0.5, sampleRate);

        // Upper partials (2-8) with inharmonic stretch and 1/n^2 rolloff
        double nyquistFreq = sampleRate / 2.0;
        for (int n = 2; n <= 8; n++)
        {
            double partialFreq = frequency * n * (1.0 + inharmonicity * n * n);
            if (partialFreq >= nyquistFreq)
                break;

            double partialAmp = baseAmp / (n * n);
            SynthUtils.GenerateSine(samples, partialFreq, partialAmp, sampleRate);
        }

        // --- 2. Main ADSR envelope ---
        float[] envelope = SynthUtils.GenerateADSR(
            attack: 0.003, decay: 0.6, sustain: 0.12, release: 0.3,
            frames: numSamples, sampleRate: sampleRate);
        SynthUtils.ApplyEnvelope(samples, envelope);

        // --- 3. Filtering ---
        // Pitch-dependent biquad lowpass: lower notes darker, higher notes brighter
        float biquadCutoff = (float)(1500.0 + frequency * 3.0);
        float nyquistHz = sampleRate / 2.0f;
        if (biquadCutoff >= nyquistHz - 100f)
            biquadCutoff = nyquistHz - 100f;

        var tempBuffer = SynthUtils.ToMonoBuffer(samples, sampleRate);
        tempBuffer = Filter.Lowpass(tempBuffer, biquadCutoff);
        Array.Copy(tempBuffer.Data, samples, numSamples);

        // Gentle one-pole warmth filter
        SynthUtils.OnePoleLP(samples, 3000.0 + frequency * 2.0, sampleRate);

        // --- 4. Hammer strike transient (added after filtering to preserve click) ---
        var transient = new float[numSamples];
        SynthUtils.GenerateWhiteNoise(transient, 0.025 * note.Velocity);

        float[] transientEnv = SynthUtils.GenerateADSR(
            attack: 0.0003, decay: 0.002, sustain: 0.0, release: 0.0005,
            frames: numSamples, sampleRate: sampleRate);
        SynthUtils.ApplyEnvelope(transient, transientEnv);

        for (int i = 0; i < numSamples; i++)
            samples[i] += transient[i];

        return SynthUtils.ToMonoBuffer(samples, sampleRate);
    }
}
