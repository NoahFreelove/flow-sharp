using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Audio.Synthesizers;

/// <summary>
/// Strings synthesizer producing a warm, detuned sawtooth pad sound.
/// Two sawtooth oscillators are slightly detuned (4 cents) to create
/// a slow beating/chorus effect characteristic of string ensembles.
/// </summary>
public class StringsSynthesizer : INoteSynthesizer
{
    private const double DetuneCents = 4.0;

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
        double baseAmp = 0.15 * note.Velocity;

        // Two detuned sawtooth waves for warm ensemble sound
        double detunedFreq = frequency * Math.Pow(2.0, DetuneCents / 1200.0);

        SynthUtils.GenerateSaw(samples, frequency, baseAmp * 0.5, sampleRate);
        SynthUtils.GenerateSaw(samples, detunedFreq, baseAmp * 0.5, sampleRate);

        // Slow attack, moderate sustain ADSR for pad-like character
        float[] envelope = SynthUtils.GenerateADSR(
            attack: 0.1, decay: 0.2, sustain: 0.7, release: 0.3,
            frames: numSamples, sampleRate: sampleRate);
        SynthUtils.ApplyEnvelope(samples, envelope);

        // Gentle lowpass to soften the saw harmonics for warmth
        SynthUtils.OnePoleLP(samples, 2500.0 + frequency, sampleRate);

        return SynthUtils.ToMonoBuffer(samples, sampleRate);
    }
}
