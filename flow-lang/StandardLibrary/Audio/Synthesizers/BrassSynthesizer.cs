using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Audio.Synthesizers;

/// <summary>
/// MIDI-style brass / horn synthesizer. Sawtooth fundamental with an octave-up saw
/// for edge, slow-attack ADSR for the brass swell, and a warm lowpass filter.
/// </summary>
public class BrassSynthesizer : INoteSynthesizer
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

        // Sawtooth fundamental + octave-up saw for brightness
        SynthUtils.GenerateSaw(samples, frequency, 0.20 * note.Velocity, sampleRate);
        SynthUtils.GenerateSaw(samples, frequency * 2, 0.05 * note.Velocity, sampleRate);

        // Slow brass swell ADSR
        float[] envelope = SynthUtils.GenerateADSR(
            attack: 0.12, decay: 0.1, sustain: 0.7, release: 0.15,
            frames: numSamples, sampleRate: sampleRate);
        SynthUtils.ApplyEnvelope(samples, envelope);

        // Warm lowpass filter
        SynthUtils.OnePoleLP(samples, 1500.0 + frequency * 0.5, sampleRate);

        return SynthUtils.ToMonoBuffer(samples, sampleRate);
    }
}
