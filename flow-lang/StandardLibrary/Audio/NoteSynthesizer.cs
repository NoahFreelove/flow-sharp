using System;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Audio
{
    /// <summary>
    /// Interface for synthesizers that can render musical notes to audio buffers.
    /// </summary>
    public interface INoteSynthesizer
    {
        AudioBuffer RenderNote(MusicalNoteData note, int sampleRate, double durationBeats, double bpm);
    }

    /// <summary>
    /// Sine wave synthesizer - produces pure sine wave tones.
    /// </summary>
    public class SineSynthesizer : INoteSynthesizer
    {
        public AudioBuffer RenderNote(MusicalNoteData note, int sampleRate, double durationBeats, double bpm)
        {
            if (note.IsRest)
                return CreateSilence(sampleRate, durationBeats, bpm);

            double frequency = PitchConversion.NoteToFrequency(note);
            double durationSeconds = BeatsToSeconds(durationBeats, bpm);
            int numSamples = (int)(durationSeconds * sampleRate);

            AudioBuffer buffer = new AudioBuffer(numSamples, 1, sampleRate);
            double amplitude = 0.3; // Moderate amplitude to avoid clipping

            for (int i = 0; i < numSamples; i++)
            {
                double t = i / (double)sampleRate;
                float sample = (float)(amplitude * Math.Sin(2.0 * Math.PI * frequency * t));
                buffer.SetSample(i, 0, sample);
            }

            return buffer;
        }

        private double BeatsToSeconds(double beats, double bpm)
        {
            return (beats / bpm) * 60.0;
        }

        private AudioBuffer CreateSilence(int sampleRate, double durationBeats, double bpm)
        {
            double durationSeconds = BeatsToSeconds(durationBeats, bpm);
            int numSamples = (int)(durationSeconds * sampleRate);
            return new AudioBuffer(numSamples, 1, sampleRate);
        }
    }

    /// <summary>
    /// Sawtooth wave synthesizer - produces bright, buzzy tones.
    /// </summary>
    public class SawSynthesizer : INoteSynthesizer
    {
        public AudioBuffer RenderNote(MusicalNoteData note, int sampleRate, double durationBeats, double bpm)
        {
            if (note.IsRest)
                return CreateSilence(sampleRate, durationBeats, bpm);

            double frequency = PitchConversion.NoteToFrequency(note);
            double durationSeconds = BeatsToSeconds(durationBeats, bpm);
            int numSamples = (int)(durationSeconds * sampleRate);

            AudioBuffer buffer = new AudioBuffer(numSamples, 1, sampleRate);
            double amplitude = 0.2; // Lower amplitude for sawtooth (more harmonics)

            for (int i = 0; i < numSamples; i++)
            {
                double t = i / (double)sampleRate;
                double phase = (frequency * t) % 1.0;
                float sample = (float)(amplitude * (2.0 * phase - 1.0));
                buffer.SetSample(i, 0, sample);
            }

            return buffer;
        }

        private double BeatsToSeconds(double beats, double bpm)
        {
            return (beats / bpm) * 60.0;
        }

        private AudioBuffer CreateSilence(int sampleRate, double durationBeats, double bpm)
        {
            double durationSeconds = BeatsToSeconds(durationBeats, bpm);
            int numSamples = (int)(durationSeconds * sampleRate);
            return new AudioBuffer(numSamples, 1, sampleRate);
        }
    }

    /// <summary>
    /// Square wave synthesizer - produces hollow, retro video game tones.
    /// </summary>
    public class SquareSynthesizer : INoteSynthesizer
    {
        public AudioBuffer RenderNote(MusicalNoteData note, int sampleRate, double durationBeats, double bpm)
        {
            if (note.IsRest)
                return CreateSilence(sampleRate, durationBeats, bpm);

            double frequency = PitchConversion.NoteToFrequency(note);
            double durationSeconds = BeatsToSeconds(durationBeats, bpm);
            int numSamples = (int)(durationSeconds * sampleRate);

            AudioBuffer buffer = new AudioBuffer(numSamples, 1, sampleRate);
            double amplitude = 0.2; // Lower amplitude for square wave (many harmonics)

            for (int i = 0; i < numSamples; i++)
            {
                double t = i / (double)sampleRate;
                double phase = (frequency * t) % 1.0;
                float sample = (float)(amplitude * (phase < 0.5 ? 1.0 : -1.0));
                buffer.SetSample(i, 0, sample);
            }

            return buffer;
        }

        private double BeatsToSeconds(double beats, double bpm)
        {
            return (beats / bpm) * 60.0;
        }

        private AudioBuffer CreateSilence(int sampleRate, double durationBeats, double bpm)
        {
            double durationSeconds = BeatsToSeconds(durationBeats, bpm);
            int numSamples = (int)(durationSeconds * sampleRate);
            return new AudioBuffer(numSamples, 1, sampleRate);
        }
    }

    /// <summary>
    /// Factory for creating synthesizers by name.
    /// </summary>
    public static class SynthesizerFactory
    {
        public static INoteSynthesizer Create(string synthType)
        {
            return synthType.ToLowerInvariant() switch
            {
                "sine" => new SineSynthesizer(),
                "saw" or "sawtooth" => new SawSynthesizer(),
                "square" => new SquareSynthesizer(),
                _ => throw new ArgumentException($"Unknown synthesizer type: {synthType}")
            };
        }
    }
}
