using FlowLang.StandardLibrary.Audio;

namespace FlowLang.StandardLibrary.Audio.DSP;

/// <summary>
/// Phase 37 Plan 37-02 Task 1 — STFT-based phase vocoder with Laroche-Dolson
/// 1999 identity phase locking. Buffer-in / Buffer-out time-stretch that
/// preserves pitch by repropagating per-bin phase at the synthesis hop rate.
///
/// <para>
/// References:
/// <list type="bullet">
///   <item><description>Laroche &amp; Dolson, "New phase-vocoder techniques
///   for pitch-shifting, harmonizing and other exotic effects" (1999) —
///   identity phase locking eliminates the classic "phasiness" artifact.</description></item>
///   <item><description>Stanford CCRMA — "Choice of Hop Size" — hop = frame/4
///   (75% overlap) is the practical minimum for Hann-windowed STFT-OLA.</description></item>
///   <item><description>Průša &amp; Holighaus, "Phase Vocoder Done Right"
///   (2022) — modern best-practices reference.</description></item>
/// </list>
/// </para>
///
/// <para>
/// Pitfall 1 from 37-RESEARCH.md (phasiness) is the only artifact this
/// algorithm explicitly fights. Pitfall 2 (transient smearing) is intrinsic
/// to phase vocoders on percussive material — Plan 37-02's <c>StretchEngine</c>
/// uses <see cref="Hps"/> to switch to <see cref="Psola"/> for transient
/// frames in <c>#auto</c> mode.
/// </para>
///
/// <para>
/// Algorithm sketch (per 37-RESEARCH.md §Pattern 1):
/// <list type="number">
///   <item><description>Window each analysis frame with sqrt-Hann (CCRMA COLA
///   convention).</description></item>
///   <item><description>Compute per-bin magnitude + instantaneous frequency
///   from phase delta vs previous frame.</description></item>
///   <item><description>Pick magnitude peaks; for each peak's region of
///   influence, propagate the peak's phase advance × synthHop to all bins
///   (identity phase locking).</description></item>
///   <item><description>IFFT, multiply by sqrt-Hann again, overlap-add at
///   the synthesis-hop position.</description></item>
/// </list>
/// </para>
///
/// <para>
/// W4 LOCK: <c>frameSize</c>, <c>hopSize</c>, <c>overlap</c> are composer-
/// tunable per D-37-08. <c>StretchEngine.Process</c> threads composer-supplied
/// values through unchanged.
/// </para>
/// </summary>
public static class PhaseVocoder
{
    /// <summary>
    /// Time-stretch <paramref name="input"/> by <paramref name="factor"/>
    /// while preserving pitch. <c>factor &gt; 1</c> = longer output;
    /// <c>factor &lt; 1</c> = shorter output. Output length is roughly
    /// <c>round(input.Frames * factor)</c> plus a single-frame boundary
    /// slack to absorb tail-end OLA accumulation.
    /// </summary>
    /// <param name="input">Source buffer. Channels preserved.</param>
    /// <param name="factor">Stretch factor. Must be positive.</param>
    /// <param name="frameSize">FFT length per analysis frame. Must be a
    /// power of 2. Default 2048 — ~46 ms at 44.1 kHz, good for music.</param>
    /// <param name="hopSize">Analysis stride between frames in samples.
    /// Must be ≥ 1. Default 512 (75% overlap with frame 2048).</param>
    /// <param name="overlap">Required overlap factor (frameSize / hopSize).
    /// Default 4 — minimum for Hann-windowed COLA reconstruction. The
    /// parameter exists for composer-facing documentation; the actual hop
    /// is determined by <paramref name="hopSize"/>.</param>
    /// <exception cref="ArgumentException">If factor ≤ 0, frameSize is not
    /// a power of 2, hopSize &lt; 1, or overlap &lt; 2.</exception>
    public static AudioBuffer Process(
        AudioBuffer input,
        double factor,
        int frameSize = 2048,
        int hopSize = 512,
        int overlap = 4)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (factor <= 0.0)
            throw new ArgumentException(
                $"PhaseVocoder.Process: factor must be positive; got {factor}.");
        if (frameSize <= 0 || (frameSize & (frameSize - 1)) != 0)
            throw new ArgumentException(
                $"PhaseVocoder.Process: frameSize must be a positive power of 2; got {frameSize}.");
        if (hopSize < 1)
            throw new ArgumentException(
                $"PhaseVocoder.Process: hopSize must be ≥ 1; got {hopSize}.");
        if (overlap < 2)
            throw new ArgumentException(
                $"PhaseVocoder.Process: overlap must be ≥ 2 (Hann COLA requirement); got {overlap}.");

        int channels = input.Channels;
        int sampleRate = input.SampleRate;
        int inFrames = input.Frames;

        int synthHop = Math.Max(1, (int)Math.Round(hopSize * factor));
        int outFrames = (int)Math.Round(inFrames * factor) + frameSize;

        // Pre-compute sqrt(Hann) once — used both at analysis and synthesis
        // per CCRMA OLA convention (analysis × synthesis = Hann²/2 → smooth
        // reconstruction across the 75% overlap factor).
        float[] hann = WindowFunctions.Hann(frameSize);
        float[] sqrtHann = new float[frameSize];
        for (int i = 0; i < frameSize; i++)
            sqrtHann[i] = MathF.Sqrt(hann[i]);

        var result = new AudioBuffer(outFrames, channels, sampleRate);

        for (int ch = 0; ch < channels; ch++)
        {
            var channelInput = ExtractChannel(input, ch);
            var channelOutput = ProcessChannel(
                channelInput, factor, frameSize, hopSize, synthHop, sqrtHann, outFrames);
            // Re-interleave channel output back into result.Data.
            int copyLen = Math.Min(channelOutput.Length, outFrames);
            for (int frame = 0; frame < copyLen; frame++)
            {
                result.Data[frame * channels + ch] = channelOutput[frame];
            }
        }
        return result;
    }

    /// <summary>
    /// Extract a single channel as a contiguous float[] (mono = direct copy;
    /// stereo / multi-channel = stride extraction from interleaved storage).
    /// </summary>
    private static float[] ExtractChannel(AudioBuffer input, int channel)
    {
        int frames = input.Frames;
        int channels = input.Channels;
        var result = new float[frames];
        for (int i = 0; i < frames; i++)
            result[i] = input.Data[i * channels + channel];
        return result;
    }

    /// <summary>
    /// Process a single channel through the STFT phase-vocoder pipeline.
    /// Per-channel state (previous-frame phase + accumulated synthesis phase)
    /// is local to each invocation — channels are processed independently.
    /// </summary>
    private static float[] ProcessChannel(
        float[] input,
        double factor,
        int frameSize,
        int hopSize,
        int synthHop,
        float[] sqrtHann,
        int outFrames)
    {
        int halfFft = frameSize / 2 + 1;
        var prevPhase = new double[halfFft];
        var phaseAccum = new double[halfFft];

        // First-frame init flag — prevPhase is zero on the first analysis
        // frame, which is intentional (no deviation from expected phase yet).
        bool firstFrame = true;

        var output = new float[outFrames + frameSize];

        // Parallel window-energy accumulator for COLA normalization. Each OLA
        // contribution is weighted by sqrtHann (analysis) × sqrtHann
        // (synthesis) = Hann at that tap. Without dividing the summed output
        // by the summed window energy, the reconstruction gain is
        // Σₘ Hann(n − m·synthHop) ≈ frameSize/(2·synthHop) = 1/factor at the
        // 2048/512 defaults (and develops amplitude-modulation ripple once
        // synthHop exceeds frameSize/2). Normalizing by the accumulated window
        // energy (with an epsilon floor at samples no frame covers) makes the
        // output level exactly unity and INDEPENDENT of factor.
        var windowEnergy = new float[output.Length];

        var frame = new float[frameSize];

        // Walk analysis frames at hopSize stride across the input.
        for (int t = 0; t < input.Length; t += hopSize)
        {
            // Build analysis frame; zero-pad past end-of-input.
            for (int k = 0; k < frameSize; k++)
            {
                int src = t + k;
                frame[k] = src < input.Length ? input[src] * sqrtHann[k] : 0f;
            }

            Fft.Forward(frame, out double[] re, out double[] im);

            // Convert to magnitude + phase, compute instantaneous frequency,
            // and accumulate synthesis-frame phase per Laroche-Dolson identity
            // phase locking.
            var mag = new double[halfFft];
            var ph = new double[halfFft];
            for (int k = 0; k < halfFft; k++)
            {
                mag[k] = Math.Sqrt(re[k] * re[k] + im[k] * im[k]);
                ph[k] = Math.Atan2(im[k], re[k]);
            }

            // Per-bin instantaneous frequency = (phaseDelta - expected
            // analysis-phase advance) / hopSize, wrapped to [-pi, pi].
            // Then phaseAdvance for synthesis = freq × synthHop.
            // Locked at peaks — non-peak bins inherit nearest peak's advance.
            int[] peaks = PickPeaks(mag);

            if (firstFrame)
            {
                // Seed the synthesis phase from the analysis phase on the very
                // first frame so initial output isn't phase-rotated relative
                // to source.
                for (int k = 0; k < halfFft; k++)
                {
                    phaseAccum[k] = ph[k];
                    prevPhase[k] = ph[k];
                }
                firstFrame = false;
            }
            else
            {
                // Compute per-bin phase advance.
                var advance = new double[halfFft];
                for (int k = 0; k < halfFft; k++)
                {
                    double expected = 2.0 * Math.PI * k * hopSize / frameSize;
                    double delta = ph[k] - prevPhase[k] - expected;
                    delta = WrapToPi(delta);
                    double freq = (expected + delta) / hopSize; // radians per sample
                    advance[k] = freq * synthHop;
                }

                // Identity phase locking — for each magnitude peak, find its
                // region of influence (halfway to nearest peak on each side)
                // and force every bin in the region to inherit the peak's
                // phase advance. This preserves vertical coherence per
                // Pitfall 1 mitigation.
                if (peaks.Length == 0)
                {
                    // No peaks (e.g. silence frame) — propagate per-bin advance
                    // directly, identical to a vanilla vocoder.
                    for (int k = 0; k < halfFft; k++)
                        phaseAccum[k] += advance[k];
                }
                else
                {
                    int peakIdx = 0;
                    for (int k = 0; k < halfFft; k++)
                    {
                        // Advance the peak cursor if k passed the midpoint
                        // between current peak and next peak.
                        while (peakIdx + 1 < peaks.Length)
                        {
                            int mid = (peaks[peakIdx] + peaks[peakIdx + 1]) / 2;
                            if (k <= mid) break;
                            peakIdx++;
                        }
                        int peakBin = peaks[peakIdx];
                        phaseAccum[k] += advance[peakBin];
                    }
                }
            }

            // Reconstruct the synthesis spectrum from (mag, phaseAccum).
            // Mirror bins to satisfy real-valued IFFT symmetry.
            var synthRe = new double[frameSize];
            var synthIm = new double[frameSize];
            for (int k = 0; k < halfFft; k++)
            {
                synthRe[k] = mag[k] * Math.Cos(phaseAccum[k]);
                synthIm[k] = mag[k] * Math.Sin(phaseAccum[k]);
            }
            for (int k = 1; k < halfFft - 1; k++)
            {
                int mirror = frameSize - k;
                synthRe[mirror] = synthRe[k];
                synthIm[mirror] = -synthIm[k];
            }

            Fft.Inverse(synthRe, synthIm, out float[] outFrame);

            // Apply sqrt-Hann again at synthesis + overlap-add at scaled
            // synthesis-hop position. The synthesis position scales by factor
            // — write index = (analysis time) × factor.
            int writePos = (int)Math.Round(t * factor);
            for (int k = 0; k < frameSize; k++)
            {
                int idx = writePos + k;
                if (idx >= output.Length) break;
                output[idx] += outFrame[k] * sqrtHann[k];
                // Accumulate the window energy contributed at this tap:
                // sqrtHann (analysis) × sqrtHann (synthesis) = Hann.
                windowEnergy[idx] += sqrtHann[k] * sqrtHann[k];
            }

            // Save current frame's phase for the next iteration.
            for (int k = 0; k < halfFft; k++)
                prevPhase[k] = ph[k];
        }

        // COLA normalization — divide each output sample by the accumulated
        // window energy so the reconstruction gain is unity regardless of
        // factor. Epsilon floor avoids divide-by-zero at samples no analysis
        // frame covered (zeros stay zeros).
        const float Epsilon = 1e-6f;
        for (int i = 0; i < output.Length; i++)
        {
            if (windowEnergy[i] > Epsilon)
                output[i] /= windowEnergy[i];
        }

        return output;
    }

    /// <summary>
    /// Wrap a phase delta to the (-pi, pi] interval — standard inst-freq
    /// computation idiom. Avoids Math.IEEERemainder for portability across
    /// runtimes (per CLAUDE.md Phase 36 chaos primitive cross-platform note).
    /// </summary>
    private static double WrapToPi(double v)
    {
        const double TwoPi = 2.0 * Math.PI;
        while (v > Math.PI) v -= TwoPi;
        while (v <= -Math.PI) v += TwoPi;
        return v;
    }

    /// <summary>
    /// Picks magnitude peaks per Laroche-Dolson 1999 definition: bin k is a
    /// peak iff <c>mag[k] &gt; mag[k±1]</c> AND <c>mag[k] &gt; mag[k±2]</c>.
    /// Returns sorted bin indices (0 and Nyquist are excluded — they have
    /// no full ±2 neighbourhood).
    /// </summary>
    private static int[] PickPeaks(double[] magnitude)
    {
        int n = magnitude.Length;
        if (n < 5) return Array.Empty<int>();

        var peaks = new List<int>();
        for (int k = 2; k < n - 2; k++)
        {
            double m = magnitude[k];
            if (m > magnitude[k - 1] && m > magnitude[k - 2]
                && m > magnitude[k + 1] && m > magnitude[k + 2])
            {
                peaks.Add(k);
            }
        }
        return peaks.ToArray();
    }
}
