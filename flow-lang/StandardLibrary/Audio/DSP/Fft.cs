namespace FlowLang.StandardLibrary.Audio.DSP;

/// <summary>
/// Phase 37 Plan 37-01 Task 2 — radix-2 Cooley-Tukey FFT used by Plan 37-02's
/// vocoder STFT pipeline (DSP-02) and Plan 37-01's HPS transient detector
/// (<see cref="Hps"/>). Per 37-RESEARCH.md §Standard Stack "~80 lines" +
/// §Anti-Patterns "Don't write a generic FFT for one use" — this is the ONE
/// FFT for the phase, not a library.
///
/// <para>
/// Surface: <see cref="Forward"/> + <see cref="Inverse"/>, both power-of-2
/// length only. Non-power-of-2 inputs throw <see cref="ArgumentException"/>
/// at entry per Security Domain DoS mitigation (T-37-01-02 — prevents buffer
/// overflow from caller miscalculation).
/// </para>
///
/// <para>
/// Implementation: in-place bit-reversal permutation, then iterative butterfly
/// with <see cref="Math.Cos(double)"/> / <see cref="Math.Sin(double)"/> twiddle
/// factors. Single Cooley-Tukey loop nest — no recursion (stack-safe for frame
/// sizes ≥ 4096). Output covers the full spectrum (length N, not N/2 + 1) —
/// the caller can read the positive-half if needed.
/// </para>
///
/// <para>
/// Inverse is normalized by 1/N so Forward → Inverse roundtrips reproduce
/// the input to within float epsilon (verified by Plan 37-02's
/// StretchIdentityTests once filled).
/// </para>
/// </summary>
public static class Fft
{
    /// <summary>
    /// Forward FFT: time-domain real signal → complex frequency-domain
    /// (real + imag arrays of length N).
    /// </summary>
    /// <param name="timeDomain">Power-of-2-length real input.</param>
    /// <param name="real">Output: real part of the spectrum (length = input length).</param>
    /// <param name="imag">Output: imaginary part of the spectrum (length = input length).</param>
    /// <exception cref="ArgumentException">If input length is not a power of 2.</exception>
    public static void Forward(float[] timeDomain, out double[] real, out double[] imag)
    {
        ArgumentNullException.ThrowIfNull(timeDomain);
        int n = timeDomain.Length;
        if (!IsPowerOfTwo(n))
            throw new ArgumentException(
                $"Fft input length must be power of 2; got {n}.");

        real = new double[n];
        imag = new double[n];

        // Bit-reversal permutation: copy time-domain into reordered positions.
        // For a length-N FFT we reverse log2(N) bits of each index.
        int logN = Log2(n);
        for (int i = 0; i < n; i++)
        {
            int j = ReverseBits(i, logN);
            real[j] = timeDomain[i];
            // imag stays zero (real input).
        }

        // Iterative Cooley-Tukey butterfly.
        // For each stage size m = 2, 4, 8, ..., N:
        //   For each block start k = 0, m, 2m, ...:
        //     For each butterfly pair j = 0..m/2-1:
        //       twiddle = exp(-2πi · j / m)
        //       a = pair[k + j], b = pair[k + j + m/2] · twiddle
        //       pair[k + j]       = a + b
        //       pair[k + j + m/2] = a - b
        for (int m = 2; m <= n; m <<= 1)
        {
            int halfM = m >> 1;
            double angleStep = -2.0 * Math.PI / m;
            for (int k = 0; k < n; k += m)
            {
                for (int j = 0; j < halfM; j++)
                {
                    double angle = angleStep * j;
                    double tCos = Math.Cos(angle);
                    double tSin = Math.Sin(angle);

                    int idxA = k + j;
                    int idxB = k + j + halfM;

                    // Twiddle-multiply b = (real[idxB] + i·imag[idxB]) × (tCos + i·tSin).
                    double bRe = real[idxB] * tCos - imag[idxB] * tSin;
                    double bIm = real[idxB] * tSin + imag[idxB] * tCos;

                    double aRe = real[idxA];
                    double aIm = imag[idxA];

                    real[idxA] = aRe + bRe;
                    imag[idxA] = aIm + bIm;
                    real[idxB] = aRe - bRe;
                    imag[idxB] = aIm - bIm;
                }
            }
        }
    }

    /// <summary>
    /// Inverse FFT: complex frequency-domain → time-domain real signal.
    /// Scaled by 1/N (standard normalization) so Forward → Inverse roundtrip
    /// reproduces the input within float epsilon.
    /// </summary>
    /// <param name="real">Real part of spectrum, power-of-2 length.</param>
    /// <param name="imag">Imaginary part of spectrum, same length as <paramref name="real"/>.</param>
    /// <param name="timeDomain">Output: real-valued time-domain signal (length = input length).</param>
    /// <exception cref="ArgumentException">
    /// If <paramref name="real"/>.Length is not a power of 2 or
    /// <paramref name="imag"/>.Length differs from <paramref name="real"/>.Length.
    /// </exception>
    public static void Inverse(double[] real, double[] imag, out float[] timeDomain)
    {
        ArgumentNullException.ThrowIfNull(real);
        ArgumentNullException.ThrowIfNull(imag);
        int n = real.Length;
        if (imag.Length != n)
            throw new ArgumentException(
                $"Fft.Inverse: real ({n}) and imag ({imag.Length}) lengths must match.");
        if (!IsPowerOfTwo(n))
            throw new ArgumentException(
                $"Fft input length must be power of 2; got {n}.");

        // Inverse FFT = conjugate → forward FFT → conjugate → scale by 1/N.
        // Avoid mutating caller-owned arrays — work on local copies.
        var workRe = new double[n];
        var workIm = new double[n];
        for (int i = 0; i < n; i++)
        {
            workRe[i] = real[i];
            workIm[i] = -imag[i]; // conjugate
        }

        // Run the forward butterfly on the conjugated input.
        // Bit-reversal permutation first.
        int logN = Log2(n);
        var permRe = new double[n];
        var permIm = new double[n];
        for (int i = 0; i < n; i++)
        {
            int j = ReverseBits(i, logN);
            permRe[j] = workRe[i];
            permIm[j] = workIm[i];
        }
        workRe = permRe;
        workIm = permIm;

        for (int m = 2; m <= n; m <<= 1)
        {
            int halfM = m >> 1;
            double angleStep = -2.0 * Math.PI / m;
            for (int k = 0; k < n; k += m)
            {
                for (int j = 0; j < halfM; j++)
                {
                    double angle = angleStep * j;
                    double tCos = Math.Cos(angle);
                    double tSin = Math.Sin(angle);

                    int idxA = k + j;
                    int idxB = k + j + halfM;

                    double bRe = workRe[idxB] * tCos - workIm[idxB] * tSin;
                    double bIm = workRe[idxB] * tSin + workIm[idxB] * tCos;

                    double aRe = workRe[idxA];
                    double aIm = workIm[idxA];

                    workRe[idxA] = aRe + bRe;
                    workIm[idxA] = aIm + bIm;
                    workRe[idxB] = aRe - bRe;
                    workIm[idxB] = aIm - bIm;
                }
            }
        }

        // Conjugate the result + scale by 1/N + emit real part as float[].
        timeDomain = new float[n];
        double invN = 1.0 / n;
        for (int i = 0; i < n; i++)
        {
            // The imaginary part of the result should be ~0 for inverse of
            // forward(real signal); we discard it. Final conjugate inverts
            // the sign but the imaginary part is zero anyway.
            timeDomain[i] = (float)(workRe[i] * invN);
        }
    }

    /// <summary>
    /// True iff <paramref name="n"/> is a positive power of 2.
    /// </summary>
    private static bool IsPowerOfTwo(int n) => n > 0 && (n & (n - 1)) == 0;

    /// <summary>
    /// log2 of a power-of-2 input. Caller is responsible for verifying the input.
    /// </summary>
    private static int Log2(int n)
    {
        int log = 0;
        while ((1 << log) < n) log++;
        return log;
    }

    /// <summary>
    /// Reverses the low <paramref name="bits"/> bits of <paramref name="value"/>.
    /// Used for the bit-reversal permutation that precedes the butterfly.
    /// </summary>
    private static int ReverseBits(int value, int bits)
    {
        int result = 0;
        for (int i = 0; i < bits; i++)
        {
            result = (result << 1) | (value & 1);
            value >>= 1;
        }
        return result;
    }
}
