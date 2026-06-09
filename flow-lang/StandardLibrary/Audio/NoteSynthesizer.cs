using System;
using FlowLang.StandardLibrary.Audio.Synthesizers;
using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.TypeSystem.SpecialTypes;
using SynthUtils = FlowLang.StandardLibrary.Audio.Synthesizers.SynthUtils;

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
    /// Band-limiting residual for the Saw + Square oscillators (v1.6 "Sound Design 2.0",
    /// D-37-09 pulled forward). The naive ramp (<c>2·phase − 1</c>) and step
    /// (<c>phase &lt; 0.5 ? +1 : −1</c>) oscillators alias badly on low notes — the
    /// hard discontinuity injects energy above Nyquist that folds back as harsh
    /// inharmonic "corruption" when bassy saws stack. PolyBLEP (the 2-sample
    /// polynomial Band-Limited stEP, Välimäki/Pekonen) rounds each discontinuity
    /// over the two samples straddling it, removing the folded-back energy while
    /// leaving every legitimate sub-Nyquist harmonic intact.
    ///
    /// DETERMINISM: this is pure deterministic float math — no RNG, no clock, no
    /// incremental phase accumulator. The Saw/Square loops keep the absolute-time
    /// <c>(frequency · t) % 1.0</c> phase formula (the byte-determinism contract);
    /// the residual width <c>dt</c> is derived as <c>frequency / sampleRate</c>.
    /// Two-run cmp-clean is preserved.
    /// </summary>
    internal static class BlepOscillator
    {
        /// <summary>
        /// Standard PolyBLEP residual. <paramref name="t"/> is the current phase in
        /// [0,1); <paramref name="dt"/> is the per-sample phase increment
        /// (frequency / sampleRate). Returns the correction for a discontinuity that
        /// jumps +1 (the downward saw reset at the 0/1 wrap, or the square's falling
        /// edge). For a saw, SUBTRACT this from the naive ramp; for a square, the
        /// rising and falling edges are corrected with +/- residuals respectively.
        /// </summary>
        public static double PolyBlep(double t, double dt)
        {
            if (t < dt)
            {
                t = t / dt;
                return t + t - t * t - 1.0;   // start of period (just after the wrap)
            }
            else if (t > 1.0 - dt)
            {
                t = (t - 1.0) / dt;
                return t * t + t + t + 1.0;    // end of period (just before the wrap)
            }
            else
            {
                return 0.0;
            }
        }
    }

    /// <summary>
    /// Sine wave synthesizer - produces pure sine wave tones.
    /// </summary>
    public class SineSynthesizer : INoteSynthesizer
    {
        public AudioBuffer RenderNote(MusicalNoteData note, int sampleRate, double durationBeats, double bpm, RenderTuning tuning)
        {
            if (note.IsRest)
                return SynthUtils.CreateSilence(sampleRate, durationBeats, bpm);

            double frequency = PitchConversion.NoteToFrequency(note, tuning);
            double durationSeconds = SynthUtils.BeatsToSeconds(durationBeats, bpm);
            int numSamples = (int)(durationSeconds * sampleRate);

            AudioBuffer buffer = new AudioBuffer(numSamples, 1, sampleRate);
            double amplitude = 0.3 * note.Velocity; // Moderate amplitude to avoid clipping

            // D-03 FALLBACK (Plan 46-06): oscillator loop kept inline. The Wave 0 byte
            // guard (NoteSynthesizerByteGuardTests) went RED against SynthUtils.GenerateSine —
            // SynthUtils' incremental phase accumulation (phase += phaseInc; wrap) diverges
            // by ±1 ULP in IEEE-754 from this absolute-time formula (Math.Sin(2π·f·t),
            // t = i/sampleRate). Since these generators are composer-callable builtins,
            // the inline math IS the byte contract and stays verbatim.
            for (int i = 0; i < numSamples; i++)
            {
                double t = i / (double)sampleRate;
                float sample = (float)(amplitude * Math.Sin(2.0 * Math.PI * frequency * t));
                buffer.SetSample(i, 0, sample);
            }

            return buffer;
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
                return SynthUtils.CreateSilence(sampleRate, durationBeats, bpm);

            double frequency = PitchConversion.NoteToFrequency(note, tuning);
            double durationSeconds = SynthUtils.BeatsToSeconds(durationBeats, bpm);
            int numSamples = (int)(durationSeconds * sampleRate);

            AudioBuffer buffer = new AudioBuffer(numSamples, 1, sampleRate);
            double amplitude = 0.2 * note.Velocity; // Lower amplitude for sawtooth (more harmonics)

            // D-03 FALLBACK (Plan 46-06): inline oscillator kept — SynthUtils.GenerateSaw
            // incremental-phase wrap diverges in IEEE-754 from this (frequency * t) % 1.0
            // absolute-time formula (byte guard RED). See SineSynthesizer note.
            //
            // SOUND DESIGN 2.0 (quick 260608-wcy, D-37-09 pulled forward): the naive ramp
            // is now PolyBLEP band-limited. The sawtooth has ONE +1 discontinuity per
            // period at the 0/1 phase wrap; subtract the PolyBlep residual to round it over
            // the two samples straddling the reset, killing the folded-back aliasing while
            // keeping every legit sub-Nyquist harmonic. dt = frequency / sampleRate is the
            // residual width. The absolute-time (frequency * t) % 1.0 phase formula is
            // PRESERVED — only the per-sample value gains the BLEP correction (deterministic
            // float math, two-run cmp-clean intact).
            double dt = frequency / (double)sampleRate;
            for (int i = 0; i < numSamples; i++)
            {
                double t = i / (double)sampleRate;
                double phase = (frequency * t) % 1.0;
                double naive = 2.0 * phase - 1.0;
                double value = naive - BlepOscillator.PolyBlep(phase, dt);
                float sample = (float)(amplitude * value);
                buffer.SetSample(i, 0, sample);
            }

            return buffer;
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
                return SynthUtils.CreateSilence(sampleRate, durationBeats, bpm);

            double frequency = PitchConversion.NoteToFrequency(note, tuning);
            double durationSeconds = SynthUtils.BeatsToSeconds(durationBeats, bpm);
            int numSamples = (int)(durationSeconds * sampleRate);

            AudioBuffer buffer = new AudioBuffer(numSamples, 1, sampleRate);
            double amplitude = 0.2 * note.Velocity; // Lower amplitude for square wave (many harmonics)

            // D-03 FALLBACK (Plan 46-06): inline oscillator kept — SynthUtils.GenerateSquare
            // incremental-phase wrap diverges in IEEE-754 from this (frequency * t) % 1.0
            // absolute-time formula (byte guard RED). See SineSynthesizer note.
            //
            // SOUND DESIGN 2.0 (quick 260608-wcy, D-37-09 pulled forward): the naive step is
            // now PolyBLEP band-limited. The square has TWO discontinuities per period — a
            // RISING +1 edge at the 0/1 phase wrap and a FALLING −1 edge at phase 0.5. The
            // standard band-limited square adds the residual at the rising edge and subtracts
            // it at the falling edge (the latter measured at the half-period-shifted phase):
            //   value = naive + PolyBlep(phase, dt) − PolyBlep((phase + 0.5) % 1, dt)
            // dt = frequency / sampleRate is the residual width. The absolute-time
            // (frequency * t) % 1.0 phase formula is PRESERVED — deterministic float math,
            // two-run cmp-clean intact.
            double dt = frequency / (double)sampleRate;
            for (int i = 0; i < numSamples; i++)
            {
                double t = i / (double)sampleRate;
                double phase = (frequency * t) % 1.0;
                double naive = phase < 0.5 ? 1.0 : -1.0;
                double value = naive + BlepOscillator.PolyBlep(phase, dt) - BlepOscillator.PolyBlep((phase + 0.5) % 1.0, dt);
                float sample = (float)(amplitude * value);
                buffer.SetSample(i, 0, sample);
            }

            return buffer;
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
                return SynthUtils.CreateSilence(sampleRate, durationBeats, bpm);

            double frequency = PitchConversion.NoteToFrequency(note, tuning);
            double durationSeconds = SynthUtils.BeatsToSeconds(durationBeats, bpm);
            int numSamples = (int)(durationSeconds * sampleRate);

            AudioBuffer buffer = new AudioBuffer(numSamples, 1, sampleRate);
            double amplitude = 0.3 * note.Velocity;

            // D-03 FALLBACK (Plan 46-06): inline oscillator kept — SynthUtils.GenerateTriangle
            // incremental-phase wrap diverges in IEEE-754 from this (frequency * t) % 1.0
            // absolute-time formula (byte guard RED on the sine/saw/square siblings; all four
            // share the same accumulation-vs-absolute-time class of drift). See SineSynthesizer note.
            for (int i = 0; i < numSamples; i++)
            {
                double t = i / (double)sampleRate;
                double phase = (frequency * t) % 1.0;
                float sample = (float)(amplitude * (phase < 0.5 ? 4 * phase - 1 : 3 - 4 * phase));
                buffer.SetSample(i, 0, sample);
            }

            return buffer;
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
