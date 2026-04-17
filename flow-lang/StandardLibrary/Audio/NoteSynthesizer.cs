using System;
using FlowLang.StandardLibrary.Audio.Synthesizers;
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
            double amplitude = 0.3 * note.Velocity; // Moderate amplitude to avoid clipping

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
            double amplitude = 0.2 * note.Velocity; // Lower amplitude for sawtooth (more harmonics)

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
            double amplitude = 0.2 * note.Velocity; // Lower amplitude for square wave (many harmonics)

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
    /// Triangle wave synthesizer - produces smooth, mellow tones.
    /// </summary>
    public class TriangleSynthesizer : INoteSynthesizer
    {
        public AudioBuffer RenderNote(MusicalNoteData note, int sampleRate, double durationBeats, double bpm)
        {
            if (note.IsRest)
                return CreateSilence(sampleRate, durationBeats, bpm);

            double frequency = PitchConversion.NoteToFrequency(note);
            double durationSeconds = BeatsToSeconds(durationBeats, bpm);
            int numSamples = (int)(durationSeconds * sampleRate);

            AudioBuffer buffer = new AudioBuffer(numSamples, 1, sampleRate);
            double amplitude = 0.3 * note.Velocity;

            for (int i = 0; i < numSamples; i++)
            {
                double t = i / (double)sampleRate;
                double phase = (frequency * t) % 1.0;
                float sample = (float)(amplitude * (phase < 0.5 ? 4 * phase - 1 : 3 - 4 * phase));
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
    /// Synthesizer that delegates note rendering to a Flow function.
    /// The function is expected to take (MusicalNote, Double duration, Double bpm) and return a Buffer.
    /// </summary>
    public class FlowFunctionSynthesizer : INoteSynthesizer
    {
        private readonly Func<MusicalNoteData, double, double, AudioBuffer> _renderFunc;

        public FlowFunctionSynthesizer(Func<MusicalNoteData, double, double, AudioBuffer> renderFunc)
        {
            _renderFunc = renderFunc ?? throw new ArgumentNullException(nameof(renderFunc));
        }

        public AudioBuffer RenderNote(MusicalNoteData note, int sampleRate, double durationBeats, double bpm)
        {
            return _renderFunc(note, durationBeats, bpm);
        }
    }

    /// <summary>
    /// Factory for creating synthesizers by name.
    /// Supports both built-in synthesizers and user-registered custom wavetables.
    /// </summary>
    public static class SynthesizerFactory
    {
        private static readonly Dictionary<string, float[]> _customWavetables = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Registers a custom wavetable (single-cycle waveform) under the given name.
        /// Once registered, the name can be used as an instrument in renderSong().
        /// </summary>
        public static void RegisterWavetable(string name, float[] wavetable)
        {
            _customWavetables[name.ToLowerInvariant()] = wavetable;
        }

        public static INoteSynthesizer Create(string synthType)
        {
            string key = synthType.ToLowerInvariant();

            if (_customWavetables.TryGetValue(key, out var wavetable))
                return new WavetableSynthesizer(wavetable);

            return key switch
            {
                "sine" => new SineSynthesizer(),
                "saw" or "sawtooth" => new SawSynthesizer(),
                "square" => new SquareSynthesizer(),
                "triangle" => new TriangleSynthesizer(),
                "piano" => new PianoSynthesizer(),
                "brass" or "horn" => new BrassSynthesizer(),
                "sax" or "saxophone" => new SaxSynthesizer(),
                "flute" => new FluteSynthesizer(),
                "strings" or "string" => new StringsSynthesizer(),
                "organ" => new OrganSynthesizer(),
                "bell" => new BellSynthesizer(),
                "drums" or "drum" => new DrumSynthesizer(),
                _ => throw new ArgumentException($"Unknown synthesizer type: {synthType}")
            };
        }
    }
}
