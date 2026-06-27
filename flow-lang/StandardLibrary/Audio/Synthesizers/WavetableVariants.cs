using System;
using FlowLang.StandardLibrary.Audio;

namespace FlowLang.StandardLibrary.Audio.Synthesizers;

/// <summary>
/// Phase 29 SPEC D-22 — built-in named wavetable variants for the
/// <see cref="WavetableSynthesizer"/>.
///
/// Three variants ship out of the box:
///
///   warm  — additive sawtooth (12 harmonics, 1/n series with the 2nd–6th
///           partials boosted ~1.4×). Spectrum: thicker mid-low partials
///           than a pure saw, perceived as a vintage-pad / "warm-saw" pad
///           timbre. Harmonic-richness ratio at C4 ≈ 1.0 (vs ~0.53 baseline).
///
///   bright — narrow-pulse train at ~10% duty cycle, DC-removed. Spectrum:
///           sinc-like rolloff with strong content across the first 10+
///           harmonics. Useful for chiptune-style leads or piercing
///           sustained tones. Harmonic-richness ratio at C4 ≈ 3.1.
///
///   buzz  — additive 15-harmonic "supersaw"-like spectrum (1/√n weighting —
///           a 6 dB/octave slower falloff than a pure saw). Result: a buzzy,
///           edge-of-clipping timbre comparable to a 5-voice detuned saw.
///           Harmonic-richness ratio at C4 ≈ 1.7.
///
/// Variants are registered with <see cref="SynthesizerFactory"/> at flow-lang
/// process start via <see cref="RegisterBuiltinVariants"/>, which is invoked
/// from <see cref="FlowLang.Core.FlowEngine"/>'s constructor (idempotent).
/// </summary>
public static class WavetableVariants
{
    private const int TableSize = 2048;
    private static bool _registered;
    private static readonly object _registerLock = new();

    /// <summary>
    /// Registers the "warm" / "bright" / "buzz" wavetable variants with
    /// <see cref="SynthesizerFactory"/>. Safe to call multiple times — the
    /// first call wins; subsequent calls no-op.
    /// </summary>
    public static void RegisterBuiltinVariants()
    {
        if (_registered) return;
        lock (_registerLock)
        {
            if (_registered) return;
            SynthesizerFactory.RegisterWavetable("warm",   GenerateWarmTable(TableSize));
            SynthesizerFactory.RegisterWavetable("bright", GenerateBrightTable(TableSize));
            SynthesizerFactory.RegisterWavetable("buzz",   GenerateBuzzTable(TableSize));
            _registered = true;
        }
    }

    /// <summary>
    /// "warm" wavetable: a sawtooth shape built additively from its first 12
    /// harmonics with the 2nd–6th partials boosted ~1.4× beyond the natural
    /// 1/n falloff. The fundamental keeps its full amplitude (1.0) while the
    /// upper partials sit higher than they would in a pure saw — perceptually
    /// the result is a "thick warm saw" / vintage-pad timbre with audibly
    /// richer overtones than the canonical Phase 28 sawtooth wavetable.
    /// Phase 29 SPEC D-22 description: "soft saw" — soft in the sense of
    /// "rounded harmonic stack" rather than "harmonic-poor".
    /// </summary>
    public static float[] GenerateWarmTable(int size)
    {
        // Per-harmonic amplitudes: 1.0/n is the sawtooth baseline; the "warm"
        // variant boosts mid-low partials (2..6) by ~1.4× while keeping 1.0
        // at the fundamental. The harmonic stack is summed additively into the
        // wavetable; normalisation at the end keeps the table in [-1, +1].
        double[] boost = { 1.0, 1.4, 1.4, 1.4, 1.4, 1.4, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0 };
        var table = new float[size];
        float max = 0f;
        for (int i = 0; i < size; i++)
        {
            double t = (double)i / size;
            double sum = 0.0;
            for (int n = 1; n <= boost.Length; n++)
            {
                // Sawtooth's nth harmonic: amplitude 1/n × sin(n·2πt).
                // The sign-alternating sawtooth Fourier series is
                // sum_n (1/n) sin(n·2πt) without alternation (positive saw).
                sum += boost[n - 1] * (1.0 / n) * Math.Sin(n * 2.0 * Math.PI * t);
            }
            float v = (float)sum;
            table[i] = v;
            float a = Math.Abs(v);
            if (a > max) max = a;
        }
        if (max > 0f)
            for (int i = 0; i < size; i++) table[i] /= max;
        return table;
    }

    /// <summary>
    /// "bright" wavetable: a narrow-pulse train at 10% duty cycle. The pulse
    /// shape has a Fourier series with substantial energy across the first
    /// ~10 harmonics (sinc-modulated, strong at the lowest few) — perceptually
    /// "buzzy" / "piercing" / "chiptune lead".
    /// </summary>
    public static float[] GenerateBrightTable(int size)
    {
        var table = new float[size];
        double duty = 0.10;
        // DC-removed: the +1/−1 levels are biased so the table integrates to 0
        // across one period. For duty=0.10, low level = −0.10/0.90 = −0.111…
        // when high level is +1.0, giving zero net DC.
        double high = 1.0;
        double low  = -duty / (1.0 - duty);
        int dutyFrames = (int)(size * duty);
        for (int i = 0; i < size; i++)
            table[i] = (float)(i < dutyFrames ? high : low);
        return table;
    }

    /// <summary>
    /// "buzz" wavetable: an additively-synthesized "supersaw"-like stack with
    /// 15 harmonics in roughly equal amplitude (1/√n weighting — slower
    /// falloff than a pure saw's 1/n). Result: a buzzy, edge-of-clipping
    /// timbre with substantial upper-partial energy comparable to a 5-voice
    /// detuned saw. Phase 29 SPEC D-22 description: "supersaw stack".
    ///
    /// (We previously tried a time-domain 5-voice detuned saw stack, but over
    /// a single 2048-sample wavetable cycle the detuning produces only a
    /// deterministic phase-offset sum — the result is spectrally close to a
    /// vanilla saw because the detune integer-multiplied by the cycle length
    /// doesn't introduce new harmonics, only phase shifts of existing ones.
    /// The additive variant below produces a genuinely richer spectrum.)
    /// </summary>
    public static float[] GenerateBuzzTable(int size)
    {
        const int harmonicCount = 15;
        var table = new float[size];
        float max = 0f;
        for (int i = 0; i < size; i++)
        {
            double t = (double)i / size;
            double sum = 0.0;
            for (int n = 1; n <= harmonicCount; n++)
            {
                // 1/√n weighting: 6 dB slower falloff than the canonical saw's
                // 1/n series, so upper partials stay prominent.
                sum += (1.0 / Math.Sqrt(n)) * Math.Sin(n * 2.0 * Math.PI * t);
            }
            float v = (float)sum;
            table[i] = v;
            float a = Math.Abs(v);
            if (a > max) max = a;
        }
        if (max > 0f)
            for (int i = 0; i < size; i++) table[i] /= max;
        return table;
    }
}
