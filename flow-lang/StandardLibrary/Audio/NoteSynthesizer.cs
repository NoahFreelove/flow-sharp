using System;
using FlowLang.StandardLibrary.Audio.Synthesizers;
using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Audio
{
    /// <summary>
    /// Interface for synthesizers that can render musical notes to audio buffers.
    /// Phase 23 Pattern A: <paramref name="tuning"/> threads the resolved render-time
    /// tuning context through to <see cref="PitchConversion.NoteToFrequency(MusicalNoteData, RenderTuning)"/>.
    /// When <c>tuning</c> equals <see cref="RenderTuning.Default"/> (or its System is
    /// <see cref="TuningSystem.EqualTemperament"/>), the byte-identical 12-TET path
    /// is taken via Pitfall 6 short-circuit.
    /// </summary>
    public interface INoteSynthesizer
    {
        AudioBuffer RenderNote(MusicalNoteData note, int sampleRate, double durationBeats, double bpm, RenderTuning tuning);
    }

    /// <summary>
    /// Sine wave synthesizer - produces pure sine wave tones.
    /// </summary>
    public class SineSynthesizer : INoteSynthesizer
    {
        public AudioBuffer RenderNote(MusicalNoteData note, int sampleRate, double durationBeats, double bpm, RenderTuning tuning)
        {
            if (note.IsRest)
                return CreateSilence(sampleRate, durationBeats, bpm);

            double frequency = PitchConversion.NoteToFrequency(note, tuning);
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
        public AudioBuffer RenderNote(MusicalNoteData note, int sampleRate, double durationBeats, double bpm, RenderTuning tuning)
        {
            if (note.IsRest)
                return CreateSilence(sampleRate, durationBeats, bpm);

            double frequency = PitchConversion.NoteToFrequency(note, tuning);
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
        public AudioBuffer RenderNote(MusicalNoteData note, int sampleRate, double durationBeats, double bpm, RenderTuning tuning)
        {
            if (note.IsRest)
                return CreateSilence(sampleRate, durationBeats, bpm);

            double frequency = PitchConversion.NoteToFrequency(note, tuning);
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
        public AudioBuffer RenderNote(MusicalNoteData note, int sampleRate, double durationBeats, double bpm, RenderTuning tuning)
        {
            if (note.IsRest)
                return CreateSilence(sampleRate, durationBeats, bpm);

            double frequency = PitchConversion.NoteToFrequency(note, tuning);
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

        public AudioBuffer RenderNote(MusicalNoteData note, int sampleRate, double durationBeats, double bpm, RenderTuning tuning)
        {
            // Phase 23: lambda synthesizers receive the resolved RenderTuning indirectly via
            // the Flow function's frequency-computing logic. The user lambda contract
            // (MusicalNote, Double duration, Double bpm) -> Buffer remains stable; per
            // CONTEXT D-08 a future expansion may surface the active tuning to user
            // lambdas, but Wave 2 keeps the lambda signature unchanged so existing
            // composer scripts continue to work. The lambda call path only sees 12-TET
            // semantics today.
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
        /// Phase 29 SPEC D-22 — guards <c>WavetableVariants.RegisterBuiltinVariants()</c>
        /// so the three built-in variants ("warm", "bright", "buzz") are registered
        /// on the FIRST Create call. Without this gate the names are only available
        /// after a FlowEngine instance has been constructed; with it, direct callers
        /// (unit tests, low-level integrators) get the variants too. Interlocked
        /// exchange makes the first-call gate thread-safe.
        /// </summary>
        private static int _builtinVariantsRegistered;

        private static void EnsureBuiltinVariantsRegistered()
        {
            if (System.Threading.Interlocked.Exchange(ref _builtinVariantsRegistered, 1) == 0)
                WavetableVariants.RegisterBuiltinVariants();
        }

        /// <summary>
        /// Registers a custom wavetable (single-cycle waveform) under the given name.
        /// Once registered, the name can be used as an instrument in renderSong().
        /// </summary>
        public static void RegisterWavetable(string name, float[] wavetable)
        {
            _customWavetables[name.ToLowerInvariant()] = wavetable;
        }

        /// <summary>
        /// Backward-compatible factory entry. Delegates to the cache-aware overload
        /// using <see cref="FlowLang.Core.FlowEngine.CurrentSampleCache"/> so existing
        /// callers (every pre-Phase-29 site) keep working unchanged. Phase 29 Plan 03
        /// rewires the tonal synth classes to delegate to <c>SampledInstrumentRenderer</c>
        /// using the injected cache; the cache argument is accepted now so Plan 03 / 04
        /// can land without modifying NoteSynthesizer.cs again.
        /// </summary>
        public static INoteSynthesizer Create(string synthType)
        {
            return Create(synthType, FlowLang.Core.FlowEngine.CurrentSampleCache);
        }

        /// <summary>
        /// Phase 29 — cache-aware factory overload. <paramref name="cache"/> is currently
        /// accepted but unused; the tonal Synthesizer classes (Piano/Brass/Sax/Strings/Flute/
        /// Bell) will start using it when Plan 03 / 04 convert them to delegating shells
        /// over <c>SampledInstrumentRenderer</c>. Drums/Organ/Wavetable continue to ignore
        /// the cache permanently (they stay synth-based per REQ-6).
        /// </summary>
        public static INoteSynthesizer Create(string synthType, SampleCache? cache)
        {
            EnsureBuiltinVariantsRegistered();
            string key = synthType.ToLowerInvariant();

            if (_customWavetables.TryGetValue(key, out var wavetable))
                return new WavetableSynthesizer(wavetable);

            return key switch
            {
                "sine" => new SineSynthesizer(),
                "saw" or "sawtooth" => new SawSynthesizer(),
                "square" => new SquareSynthesizer(),
                "triangle" => new TriangleSynthesizer(),
                "piano" => new PianoSynthesizer(),   // Plan 03 will wire to SampledInstrumentRenderer via cache
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
