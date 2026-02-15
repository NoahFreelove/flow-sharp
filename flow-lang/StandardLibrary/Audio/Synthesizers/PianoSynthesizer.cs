using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Audio.Synthesizers;

/// <summary>
/// MIDI-style piano synthesizer. Additive sine harmonics with a percussive ADSR
/// envelope and pitch-tracking lowpass filter for a plucked-string character.
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

        var samples = new float[numSamples];

        // Additive sine harmonics: fundamental + 2nd + 3rd + 4th
        SynthUtils.GenerateSine(samples, frequency, 0.20, sampleRate);
        SynthUtils.GenerateSine(samples, frequency * 2, 0.10, sampleRate);
        SynthUtils.GenerateSine(samples, frequency * 3, 0.05, sampleRate);
        SynthUtils.GenerateSine(samples, frequency * 4, 0.025, sampleRate);

        // Percussive ADSR: fast attack, medium decay, low sustain, moderate release
        float[] envelope = SynthUtils.GenerateADSR(
            attack: 0.005, decay: 0.3, sustain: 0.25, release: 0.4,
            frames: numSamples, sampleRate: sampleRate);
        SynthUtils.ApplyEnvelope(samples, envelope);

        // Pitch-tracking lowpass to tame upper harmonics
        SynthUtils.OnePoleLP(samples, 2000.0 + frequency, sampleRate);

        return SynthUtils.ToMonoBuffer(samples, sampleRate);
    }
}
