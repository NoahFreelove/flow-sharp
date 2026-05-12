using System;
using System.Numerics;
using FlowLang.StandardLibrary.Audio;

namespace FlowLang.Tests.Helpers;

/// <summary>
/// Phase 29 spectral-analysis helper for harmonic-richness measurement.
///
/// Phase 29 REQ-6 / SPEC D-23 requires each of Drums / Organ / Wavetable
/// to show a ≥ 20% gain in "harmonic richness" vs the pinned Phase 28 baseline.
/// "Harmonic richness" is defined here as the ratio of total energy in the
/// 2nd..Nth partials of a tone to the energy in the fundamental:
///
///     richness = Σ E(k·f₀ for k in 2..N) / E(f₀)
///
/// Higher richness ⇒ more upper-partial content ⇒ a "fuller" / less pure-sine
/// timbre. The metric is robust to fundamental detection, deterministic, and
/// trivial to implement via the Goertzel algorithm — no full FFT is required
/// because we only ever care about a small set of bins centered on harmonics.
///
/// For percussive sounds with a poorly-defined fundamental (kick drum), the
/// "fundamental" is the nominal pitch the synthesizer is generating at (~50 Hz
/// for a typical kick body), and partials above it capture the click transient,
/// body decay, and harmonic colour. Even when the spectrum is broadband, the
/// ratio still trends upward as multi-component synthesis adds energy across
/// the spectrum.
///
/// Threat T-29-V5-09: this runs only in tests, on ≤ 1 second of 44.1 kHz
/// mono audio (≤ 44100 samples). The 7-partial Goertzel sweep is O(7N) ≈
/// 300k FLOPs per call — negligible. No DoS surface.
/// </summary>
public static class Phase29Fft
{
    /// <summary>
    /// Default number of upper partials to integrate when computing richness.
    /// 7 covers 2f₀..8f₀ — enough to capture the perceptually-significant
    /// upper-harmonic stack for most instruments while staying below Nyquist
    /// for typical fundamentals (e.g. 50 Hz × 8 = 400 Hz, 261 Hz × 8 ≈ 2.1 kHz).
    /// </summary>
    public const int DefaultUpperPartialCount = 7;

    /// <summary>
    /// Computes the harmonic-richness ratio of a mono buffer relative to a
    /// stated fundamental frequency.
    ///
    /// Returns Σ E(k·f₀) for k in 2..(2+upperPartialCount-1) divided by E(f₀),
    /// where E is the squared Goertzel magnitude at the target frequency.
    ///
    /// Guards:
    ///   - if E(f₀) is 0 (silence or no energy at the fundamental), returns
    ///     0.0 to avoid Infinity / NaN. Callers should treat 0.0 as a failed
    ///     measurement, not as "perfectly pure" — the baseline JSON pins
    ///     non-zero values per instrument, so 0.0 in a result is a regression
    ///     signal.
    ///   - partials at or above Nyquist (sampleRate / 2) are skipped — they
    ///     would alias and pollute the ratio.
    /// </summary>
    public static double HarmonicRichnessRatio(AudioBuffer buffer, double fundamentalHz, int upperPartialCount = DefaultUpperPartialCount)
    {
        if (buffer is null) throw new ArgumentNullException(nameof(buffer));
        if (fundamentalHz <= 0.0) throw new ArgumentException("Fundamental frequency must be positive.", nameof(fundamentalHz));
        if (upperPartialCount < 1) throw new ArgumentException("upperPartialCount must be >= 1.", nameof(upperPartialCount));

        // Extract mono samples (mix stereo down to mono if needed).
        float[] mono = ToMonoSamples(buffer);
        if (mono.Length == 0) return 0.0;

        double nyquist = buffer.SampleRate / 2.0;

        double e1 = GoertzelEnergy(mono, buffer.SampleRate, fundamentalHz);
        if (e1 <= 0.0) return 0.0;

        double upperEnergy = 0.0;
        for (int k = 2; k <= 1 + upperPartialCount; k++)
        {
            double targetHz = k * fundamentalHz;
            if (targetHz >= nyquist) break;
            upperEnergy += GoertzelEnergy(mono, buffer.SampleRate, targetHz);
        }

        return upperEnergy / e1;
    }

    /// <summary>
    /// Convenience overload that accepts a raw float[] mono sample array.
    /// </summary>
    public static double HarmonicRichnessRatio(float[] monoSamples, int sampleRate, double fundamentalHz, int upperPartialCount = DefaultUpperPartialCount)
    {
        if (monoSamples is null) throw new ArgumentNullException(nameof(monoSamples));
        if (monoSamples.Length == 0) return 0.0;

        double nyquist = sampleRate / 2.0;

        double e1 = GoertzelEnergy(monoSamples, sampleRate, fundamentalHz);
        if (e1 <= 0.0) return 0.0;

        double upperEnergy = 0.0;
        for (int k = 2; k <= 1 + upperPartialCount; k++)
        {
            double targetHz = k * fundamentalHz;
            if (targetHz >= nyquist) break;
            upperEnergy += GoertzelEnergy(monoSamples, sampleRate, targetHz);
        }

        return upperEnergy / e1;
    }

    /// <summary>
    /// Standard Goertzel algorithm — returns squared magnitude at the target
    /// frequency. Cheap, in-place, allocation-free; we only need a single bin
    /// per call. Mathematically equivalent to |X[k]|² of a DFT centered on
    /// the closest bin to <paramref name="targetHz"/>, but without computing
    /// the rest of the spectrum.
    ///
    /// Reference: <a href="https://en.wikipedia.org/wiki/Goertzel_algorithm">Wikipedia</a>.
    /// </summary>
    private static double GoertzelEnergy(float[] samples, int sampleRate, double targetHz)
    {
        int n = samples.Length;
        if (n == 0) return 0.0;

        double omega = 2.0 * Math.PI * targetHz / sampleRate;
        double coeff = 2.0 * Math.Cos(omega);

        double s0 = 0.0, s1 = 0.0, s2 = 0.0;
        for (int i = 0; i < n; i++)
        {
            s0 = samples[i] + coeff * s1 - s2;
            s2 = s1;
            s1 = s0;
        }

        // |X|² = s1² + s2² − coeff·s1·s2
        return s1 * s1 + s2 * s2 - coeff * s1 * s2;
    }

    /// <summary>
    /// Flattens an interleaved AudioBuffer to a mono float[] sample array.
    /// Stereo (or any multi-channel) buffers are mixed down by averaging
    /// channels per frame; this preserves the spectral content needed for
    /// the Goertzel sweep without introducing phase artefacts.
    /// </summary>
    private static float[] ToMonoSamples(AudioBuffer buffer)
    {
        if (buffer.Channels == 1)
        {
            // Defensive copy so callers can't mutate the buffer's internal data array.
            var mono = new float[buffer.Frames];
            Array.Copy(buffer.Data, mono, buffer.Frames);
            return mono;
        }

        var result = new float[buffer.Frames];
        for (int frame = 0; frame < buffer.Frames; frame++)
        {
            double sum = 0.0;
            for (int ch = 0; ch < buffer.Channels; ch++)
                sum += buffer.GetSample(frame, ch);
            result[frame] = (float)(sum / buffer.Channels);
        }
        return result;
    }

    // === Radix-2 FFT + spectrum helpers (originally from Plan 29-03, merged
    // here with Plan 29-05's Goertzel-based HarmonicRichnessRatio so both
    // VelocityLayerTests / ArticulationOnSampleTests (which need a full
    // magnitude spectrum + cosine similarity) AND HarmonicRichnessTests
    // (which need just the Goertzel ratio) share one helper file.) ===

    /// <summary>
    /// Recursive Cooley-Tukey radix-2 FFT. Input length must be a power of 2.
    /// Callers pad to NextPowerOf2 above the buffer's frame count.
    /// </summary>
    public static Complex[] Fft(Complex[] x)
    {
        int n = x.Length;
        if (n == 0) return Array.Empty<Complex>();
        if ((n & (n - 1)) != 0) throw new ArgumentException("Length must be power of 2", nameof(x));
        if (n == 1) return new[] { x[0] };

        var even = new Complex[n / 2];
        var odd = new Complex[n / 2];
        for (int i = 0; i < n / 2; i++)
        {
            even[i] = x[2 * i];
            odd[i] = x[2 * i + 1];
        }
        var evenT = Fft(even);
        var oddT = Fft(odd);

        var output = new Complex[n];
        for (int k = 0; k < n / 2; k++)
        {
            var twiddle = Complex.FromPolarCoordinates(1.0, -2.0 * Math.PI * k / n) * oddT[k];
            output[k] = evenT[k] + twiddle;
            output[k + n / 2] = evenT[k] - twiddle;
        }
        return output;
    }

    /// <summary>
    /// Mixes an AudioBuffer to mono, zero-pads to the next power of 2, FFTs it,
    /// and returns the magnitude spectrum (length = N/2 + 1).
    /// </summary>
    public static double[] ComputeMagnitudeSpectrum(AudioBuffer buffer)
    {
        if (buffer is null) throw new ArgumentNullException(nameof(buffer));
        if (buffer.Frames == 0) return Array.Empty<double>();

        int n = NextPowerOf2(buffer.Frames);
        var input = new Complex[n];
        int channels = buffer.Channels;
        for (int i = 0; i < buffer.Frames; i++)
        {
            double sample = channels == 1
                ? buffer.Data[i]
                : (buffer.Data[i * channels] + buffer.Data[i * channels + 1]) / 2.0;
            input[i] = new Complex(sample, 0);
        }

        var spectrum = Fft(input);
        int half = n / 2;
        var mag = new double[half + 1];
        for (int i = 0; i <= half; i++) mag[i] = spectrum[i].Magnitude;
        return mag;
    }

    /// <summary>
    /// Cosine similarity between two equal-length magnitude vectors.
    /// Returns 0 if either input is all-zero (avoids NaN).
    /// </summary>
    public static double CosineSimilarity(double[] a, double[] b)
    {
        if (a is null) throw new ArgumentNullException(nameof(a));
        if (b is null) throw new ArgumentNullException(nameof(b));
        int n = Math.Min(a.Length, b.Length);
        double dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < n; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        if (normA <= 0 || normB <= 0) return 0;
        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }

    private static int NextPowerOf2(int n)
    {
        int p = 1;
        while (p < n) p *= 2;
        return p;
    }
}
