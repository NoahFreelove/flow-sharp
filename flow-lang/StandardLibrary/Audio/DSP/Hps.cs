namespace FlowLang.StandardLibrary.Audio.DSP;

/// <summary>
/// Phase 37 Plan 37-01 Task 2 — Harmonic-Percussive Source separator via
/// median filtering (Fitzgerald 2010 DAFx). Used by Plan 37-02's
/// <c>stretch mode=#auto</c> dispatch to decide per-frame whether to apply
/// <c>#vocoder</c> (harmonic-dominant frames) or <c>#psola</c> (percussive-
/// dominant frames). Per 37-RESEARCH.md §Pattern 3.
///
/// <para>
/// Algorithm:
/// <code>
/// Given STFT magnitude spectrogram S (frame × bin):
///   H = horizontal_median_filter(S, length=horizKernel) → smooths time → harmonic enhanced
///   P = vertical_median_filter(S, length=vertKernel)    → smooths freq → percussive enhanced
///   ratio[frame] = mean(P[frame]) / (mean(H[frame]) + ε)
/// </code>
/// The caller compares each frame's ratio against <c>transientThreshold</c>
/// (default 0.3 normalized per A1 / D-37-07) to make the binary
/// vocoder-vs-PSOLA decision.
/// </para>
///
/// <para>
/// Default kernel sizes 17×17 are Fitzgerald's tuning for 2048-frame /
/// 512-hop STFT at 44.1 kHz (≈200 ms time / 366 Hz freq smoothing) —
/// reproduced in librosa. Per Pitfall 4 of 37-RESEARCH.md, callers using
/// a different frame size should scale the kernels: roughly
/// <c>horizKernel = round(17 × (frameSize / 2048))</c>.
/// </para>
///
/// <para>
/// Edge handling: clamp the kernel window indices into <c>[0, length-1]</c>
/// — no zero-padding (matches librosa convention).
/// </para>
/// </summary>
public static class Hps
{
    /// <summary>
    /// Computes the per-frame percussive-to-harmonic ratio for a magnitude
    /// spectrogram. On pure sustained tonal content all frames return ~0;
    /// on transient hits (drum, plucked-string attack) the transient
    /// frame returns &gt; 0.5.
    /// </summary>
    /// <param name="spectrogram">Magnitude spectrogram indexed
    /// <c>spectrogram[frame][bin]</c>. Caller pre-computes via
    /// <see cref="Fft.Forward"/> + per-bin magnitude.</param>
    /// <param name="horizKernel">Horizontal (time-axis) median-filter kernel
    /// length in frames. Default 17 (Fitzgerald 2010).</param>
    /// <param name="vertKernel">Vertical (frequency-axis) median-filter kernel
    /// length in bins. Default 17 (Fitzgerald 2010).</param>
    /// <returns>One ratio per frame, in <c>[0, +∞)</c>. Higher = more
    /// percussive content.</returns>
    /// <exception cref="ArgumentException">If kernels are non-positive or
    /// the spectrogram is empty / ragged.</exception>
    public static double[] ComputePercussiveRatio(
        float[][] spectrogram,
        int horizKernel = 17,
        int vertKernel = 17)
    {
        ArgumentNullException.ThrowIfNull(spectrogram);
        if (spectrogram.Length == 0)
            throw new ArgumentException(
                "Hps.ComputePercussiveRatio: spectrogram must contain at least one frame.");
        if (horizKernel <= 0)
            throw new ArgumentException(
                $"Hps.ComputePercussiveRatio: horizKernel must be positive; got {horizKernel}.");
        if (vertKernel <= 0)
            throw new ArgumentException(
                $"Hps.ComputePercussiveRatio: vertKernel must be positive; got {vertKernel}.");

        int frameCount = spectrogram.Length;
        int binCount = spectrogram[0]?.Length ?? 0;
        if (binCount == 0)
            throw new ArgumentException(
                "Hps.ComputePercussiveRatio: spectrogram first frame must contain at least one bin.");
        for (int f = 1; f < frameCount; f++)
        {
            if (spectrogram[f] == null || spectrogram[f].Length != binCount)
                throw new ArgumentException(
                    $"Hps.ComputePercussiveRatio: spectrogram ragged at frame {f}; expected {binCount} bins.");
        }

        const double Epsilon = 1e-12;
        var ratio = new double[frameCount];

        for (int f = 0; f < frameCount; f++)
        {
            double sumH = 0.0;
            double sumP = 0.0;
            for (int b = 0; b < binCount; b++)
            {
                sumH += HorizontalMedian(spectrogram, f, b, horizKernel, frameCount);
                sumP += VerticalMedian(spectrogram, f, b, vertKernel, binCount);
            }
            double meanH = sumH / binCount;
            double meanP = sumP / binCount;
            ratio[f] = meanP / (meanH + Epsilon);
        }
        return ratio;
    }

    /// <summary>
    /// Median across <c>spectrogram[frame ± kernel/2][bin]</c> at fixed
    /// <paramref name="bin"/> — smooths along the time axis to enhance
    /// harmonic (frame-coherent) content.
    /// </summary>
    private static float HorizontalMedian(
        float[][] spectrogram, int frame, int bin, int kernel, int frameCount)
    {
        int half = kernel / 2;
        int lo = Math.Max(0, frame - half);
        int hi = Math.Min(frameCount - 1, frame + half);
        int count = hi - lo + 1;
        var buf = new float[count];
        for (int i = 0; i < count; i++)
            buf[i] = spectrogram[lo + i][bin];
        return Median(buf);
    }

    /// <summary>
    /// Median across <c>spectrogram[frame][bin ± kernel/2]</c> at fixed
    /// <paramref name="frame"/> — smooths along the frequency axis to
    /// enhance percussive (broadband, frame-isolated) content.
    /// </summary>
    private static float VerticalMedian(
        float[][] spectrogram, int frame, int bin, int kernel, int binCount)
    {
        int half = kernel / 2;
        int lo = Math.Max(0, bin - half);
        int hi = Math.Min(binCount - 1, bin + half);
        int count = hi - lo + 1;
        var buf = new float[count];
        for (int i = 0; i < count; i++)
            buf[i] = spectrogram[frame][lo + i];
        return Median(buf);
    }

    /// <summary>
    /// Returns the median of a (small) float buffer. Sort + pick midpoint —
    /// the kernels are small (17 elements default) so the O(k log k) cost is
    /// negligible compared to overall HPS throughput.
    /// </summary>
    private static float Median(float[] buf)
    {
        if (buf.Length == 0) return 0f;
        if (buf.Length == 1) return buf[0];
        Array.Sort(buf);
        int mid = buf.Length / 2;
        if ((buf.Length & 1) == 1) return buf[mid];
        return 0.5f * (buf[mid - 1] + buf[mid]);
    }
}
