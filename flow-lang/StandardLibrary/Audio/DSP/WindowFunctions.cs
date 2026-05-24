namespace FlowLang.StandardLibrary.Audio.DSP;

/// <summary>
/// Phase 37 Plan 37-01 Task 2 — closed-form window helpers shared by the
/// DSP foundation. Used by Plan 37-01's granular engine (DSP-01) and Plan
/// 37-02's vocoder + PSOLA pipelines (DSP-02 / DSP-03). Per 37-RESEARCH.md
/// §Pattern 4 closed-form definitions + 37-PATTERNS.md §WindowFunctions.cs.
///
/// <para>
/// All three windows return <c>float[length]</c> whose envelope shape is
/// verified by <c>flow-lang.Tests/Integration/Phase37/WindowFunctionTests</c>:
/// endpoints &lt; 0.05, center &gt; 0.9, and pairwise distinct at index 256.
/// </para>
///
/// <para>
/// Defaults locked in this plan per CONTEXT §Claude's Discretion (A2, A3):
/// Gaussian σ = 0.4 (Krzyzaniak working range; σ &gt; 0.5 has audible endpoint
/// discontinuity), Tukey α = 0.5 (flat 50% center + Hann roll-off 25% each
/// side — composer-ergonomic).
/// </para>
///
/// <para>
/// Class shape mirrors <see cref="Filter"/> — pure static helpers, no state,
/// each method validates inputs and throws <see cref="ArgumentException"/>
/// on bad input (Security Domain V5).
/// </para>
/// </summary>
public static class WindowFunctions
{
    /// <summary>
    /// Hann window: <c>w[n] = 0.5 × (1 − cos(2π n / (length−1)))</c>.
    /// First + last samples are zero by construction; peak at center is 1.0.
    /// Default windowing choice for granular synthesis (smooth roll-off,
    /// minimal spectral leakage).
    /// </summary>
    /// <param name="length">Window length in samples. Must be positive.</param>
    /// <returns>Window curve of <paramref name="length"/> floats.</returns>
    /// <exception cref="ArgumentException">If <paramref name="length"/> &lt;= 0.</exception>
    public static float[] Hann(int length)
    {
        if (length <= 0)
            throw new ArgumentException(
                $"WindowFunctions.Hann: length must be positive; got {length}.");

        var result = new float[length];
        // Single-sample length is a degenerate case (cos(0/0) division — use 1.0
        // since a single sample is by definition "at center").
        if (length == 1)
        {
            result[0] = 1f;
            return result;
        }
        double denom = length - 1.0;
        for (int n = 0; n < length; n++)
        {
            result[n] = (float)(0.5 * (1.0 - Math.Cos(2.0 * Math.PI * n / denom)));
        }
        return result;
    }

    /// <summary>
    /// Gaussian window: <c>w[n] = exp(−0.5 × ((n − (length−1)/2) / (σ × (length−1)/2))²)</c>.
    /// Tapers smoothly toward zero at the edges with no discontinuity (assuming
    /// σ ≤ 0.5). Good choice for granular when you want a softer onset/offset
    /// than Hann's cosine roll.
    /// </summary>
    /// <param name="length">Window length in samples. Must be positive.</param>
    /// <param name="sigma">Gaussian width parameter. Must be positive. Default 0.4
    /// (Krzyzaniak working range; σ &gt; 0.5 produces audible endpoint
    /// discontinuity).</param>
    /// <returns>Window curve of <paramref name="length"/> floats.</returns>
    /// <exception cref="ArgumentException">
    /// If <paramref name="length"/> &lt;= 0 or <paramref name="sigma"/> &lt;= 0.
    /// </exception>
    public static float[] Gaussian(int length, double sigma = 0.4)
    {
        if (length <= 0)
            throw new ArgumentException(
                $"WindowFunctions.Gaussian: length must be positive; got {length}.");
        if (sigma <= 0.0)
            throw new ArgumentException(
                $"WindowFunctions.Gaussian: sigma must be positive; got {sigma}.");

        var result = new float[length];
        if (length == 1)
        {
            result[0] = 1f;
            return result;
        }
        double center = (length - 1.0) / 2.0;
        double denom = sigma * center;
        for (int n = 0; n < length; n++)
        {
            double x = (n - center) / denom;
            result[n] = (float)Math.Exp(-0.5 * x * x);
        }
        return result;
    }

    /// <summary>
    /// Tukey (cosine-tapered) window: Hann roll-on for the first <c>α/2</c>
    /// fraction, flat 1.0 in the middle, Hann roll-off for the last <c>α/2</c>.
    /// At α = 0 reduces to a rectangular window; at α = 1 reduces to Hann.
    /// Useful when you want most of the source grain at full amplitude with
    /// only soft edges to suppress clicks.
    /// </summary>
    /// <param name="length">Window length in samples. Must be positive.</param>
    /// <param name="alpha">Fraction of the window devoted to the cosine taper
    /// (half on each side). Must be in [0, 1]. Default 0.5 — flat 50% center +
    /// Hann roll-off 25% each side.</param>
    /// <returns>Window curve of <paramref name="length"/> floats.</returns>
    /// <exception cref="ArgumentException">
    /// If <paramref name="length"/> &lt;= 0 or <paramref name="alpha"/> outside [0, 1].
    /// </exception>
    public static float[] Tukey(int length, double alpha = 0.5)
    {
        if (length <= 0)
            throw new ArgumentException(
                $"WindowFunctions.Tukey: length must be positive; got {length}.");
        if (alpha < 0.0 || alpha > 1.0)
            throw new ArgumentException(
                $"WindowFunctions.Tukey: alpha must be in [0, 1]; got {alpha}.");

        var result = new float[length];
        if (length == 1)
        {
            result[0] = 1f;
            return result;
        }
        // alpha == 0 → rectangular (all 1s).
        if (alpha == 0.0)
        {
            for (int n = 0; n < length; n++) result[n] = 1f;
            return result;
        }
        // alpha == 1 → Hann.
        if (alpha >= 1.0) return Hann(length);

        // Standard Tukey definition (Harris 1978):
        //   For n in [0, α(N-1)/2):              w[n] = 0.5 * (1 + cos(π * (2n / (α(N-1)) - 1)))
        //   For n in [α(N-1)/2, (N-1)(1-α/2)]:   w[n] = 1
        //   For n in ((N-1)(1-α/2), N-1]:        w[n] = 0.5 * (1 + cos(π * (2n / (α(N-1)) - 2/α + 1)))
        double nMinusOne = length - 1.0;
        double taperLen = alpha * nMinusOne / 2.0;
        for (int n = 0; n < length; n++)
        {
            if (n < taperLen)
            {
                double x = Math.PI * (2.0 * n / (alpha * nMinusOne) - 1.0);
                result[n] = (float)(0.5 * (1.0 + Math.Cos(x)));
            }
            else if (n <= nMinusOne - taperLen)
            {
                result[n] = 1f;
            }
            else
            {
                double x = Math.PI * (2.0 * n / (alpha * nMinusOne) - 2.0 / alpha + 1.0);
                result[n] = (float)(0.5 * (1.0 + Math.Cos(x)));
            }
        }
        return result;
    }
}
