using FlowLang.StandardLibrary.Audio;

namespace FlowLang.StandardLibrary.Audio.DSP;

/// <summary>
/// Phase 37 Plan 37-02 Task 1 — Time-Domain Pitch-Synchronous Overlap-Add
/// (TD-PSOLA) for transient-preserving time-stretch. Extracts pitch-period-
/// sized grains anchored on input "epochs" (peak-pick positions) and OLAs
/// them at scaled output epoch positions. Per 37-RESEARCH.md §Pattern 2.
///
/// <para>
/// PSOLA's key advantage over phase vocoder: transient onsets stay aligned
/// because the algorithm operates in the time domain on grain-aligned
/// boundaries, not on STFT frame boundaries. Pitfall 2 (transient smearing)
/// avoided; tradeoff is Pitfall 3 (octave errors in YIN pitch detection),
/// mitigated by the cumulative-mean-normalized-difference formulation.
/// </para>
///
/// <para>
/// References:
/// <list type="bullet">
///   <item><description>de Cheveigné &amp; Kawahara, "YIN, a fundamental
///   frequency estimator for speech and music" (JASA 2002) — pitch detector
///   with cumulative-mean-normalized-difference octave-error mitigation.</description></item>
///   <item><description>Moulines &amp; Charpentier, "Pitch-synchronous waveform
///   processing techniques for text-to-speech synthesis using diphones"
///   (Speech Communication 1990) — original TD-PSOLA reference.</description></item>
/// </list>
/// </para>
///
/// <para>
/// W4 LOCK: <c>pitchPeriodOverride</c> and <c>windowSizeOverride</c> let
/// composers bypass YIN and force a specific period or grain length. When
/// <c>pitchPeriodOverride</c> is supplied, YIN detection is SKIPPED entirely
/// (faster + deterministic). When <c>windowSizeOverride</c> is supplied,
/// grain length is the override value instead of <c>2 × effectivePeriod</c>.
/// </para>
/// </summary>
public static class Psola
{
    /// <summary>
    /// Time-stretch <paramref name="input"/> by <paramref name="factor"/>
    /// while preserving transient onsets. Output length is
    /// <c>round(input.Frames * factor)</c> samples.
    /// </summary>
    /// <param name="input">Source buffer. Channels preserved.</param>
    /// <param name="factor">Stretch factor. Must be positive.</param>
    /// <param name="defaultPeriodSamples">Period used for unvoiced (noise)
    /// segments where YIN finds no fundamental. Default 441 samples =
    /// 10 ms at 44.1 kHz (RESEARCH §Pattern 2).</param>
    /// <param name="pitchPeriodOverride">W4 LOCK — when non-null, SKIP YIN
    /// detection and use this period across all epochs.</param>
    /// <param name="windowSizeOverride">W4 LOCK — when non-null, use this for
    /// the grain window length instead of <c>2 × effectivePeriod</c>.</param>
    /// <exception cref="ArgumentException">If factor ≤ 0 or
    /// defaultPeriodSamples ≤ 0.</exception>
    public static AudioBuffer Process(
        AudioBuffer input,
        double factor,
        int defaultPeriodSamples = 441,
        int? pitchPeriodOverride = null,
        int? windowSizeOverride = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (factor <= 0.0)
            throw new ArgumentException(
                $"Psola.Process: factor must be positive; got {factor}.");
        if (defaultPeriodSamples <= 0)
            throw new ArgumentException(
                $"Psola.Process: defaultPeriodSamples must be positive; got {defaultPeriodSamples}.");
        if (pitchPeriodOverride.HasValue && pitchPeriodOverride.Value <= 0)
            throw new ArgumentException(
                $"Psola.Process: pitchPeriodOverride must be positive when supplied; got {pitchPeriodOverride.Value}.");
        if (windowSizeOverride.HasValue && windowSizeOverride.Value <= 0)
            throw new ArgumentException(
                $"Psola.Process: windowSizeOverride must be positive when supplied; got {windowSizeOverride.Value}.");

        int channels = input.Channels;
        int sampleRate = input.SampleRate;
        int inFrames = input.Frames;
        int outFrames = (int)Math.Round(inFrames * factor);

        var result = new AudioBuffer(outFrames, channels, sampleRate);

        for (int ch = 0; ch < channels; ch++)
        {
            var channelInput = ExtractChannel(input, ch);
            var channelOutput = ProcessChannel(
                channelInput, factor, sampleRate, defaultPeriodSamples,
                pitchPeriodOverride, windowSizeOverride, outFrames);
            int copyLen = Math.Min(channelOutput.Length, outFrames);
            for (int frame = 0; frame < copyLen; frame++)
            {
                result.Data[frame * channels + ch] = channelOutput[frame];
            }
        }
        return result;
    }

    /// <summary>
    /// YIN-based fundamental period detection in samples (de Cheveigné &amp;
    /// Kawahara 2002). Returns -1 for unvoiced frames (no τ falls below
    /// <paramref name="yinThreshold"/>).
    /// </summary>
    /// <param name="frame">Audio frame to analyze (typically 1024+ samples).</param>
    /// <param name="sampleRate">Sample rate of the frame (used for max-τ bound).</param>
    /// <param name="yinThreshold">YIN voicing threshold; 0.1 is the paper default.</param>
    /// <returns>Period in samples, or -1 if unvoiced.</returns>
    public static int DetectPitchPeriod(float[] frame, int sampleRate, double yinThreshold = 0.1)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (frame.Length < 8) return -1;

        int n = frame.Length;
        // Bound τ search range: skip very-short periods (above ~2000 Hz = 22 samples
        // at 44.1 kHz) and very-long periods (below ~50 Hz = 882 samples at 44.1 kHz).
        int minTau = Math.Max(2, sampleRate / 2000);
        int maxTau = Math.Min(n / 2, sampleRate / 50);
        if (maxTau <= minTau) return -1;

        // Difference function d(τ) = Σ (x[n] - x[n+τ])²
        var diff = new double[maxTau + 1];
        for (int tau = 1; tau <= maxTau; tau++)
        {
            double sum = 0.0;
            int upper = n - tau;
            for (int i = 0; i < upper; i++)
            {
                double delta = frame[i] - frame[i + tau];
                sum += delta * delta;
            }
            diff[tau] = sum;
        }

        // Cumulative-mean-normalized-difference d'(τ) = d(τ) / [(1/τ) Σ d(j)]
        // — penalizes the τ=0 spurious peak that plain autocorrelation suffers
        // from (Pitfall 3: PSOLA octave errors).
        var dPrime = new double[maxTau + 1];
        dPrime[0] = 1.0; // by convention, see YIN paper §3
        double runningSum = 0.0;
        for (int tau = 1; tau <= maxTau; tau++)
        {
            runningSum += diff[tau];
            dPrime[tau] = diff[tau] * tau / (runningSum + 1e-12);
        }

        // Pick the first τ below threshold in [minTau, maxTau]. YIN step 4 of
        // the paper — "absolute threshold" — sets the simplest selection rule.
        for (int tau = minTau; tau <= maxTau; tau++)
        {
            if (dPrime[tau] < yinThreshold)
            {
                // Refine: walk to the local minimum within the threshold valley.
                while (tau + 1 <= maxTau && dPrime[tau + 1] < dPrime[tau])
                    tau++;
                return tau;
            }
        }
        // No τ below threshold — unvoiced segment.
        return -1;
    }

    /// <summary>
    /// Extract a single channel as a contiguous float[] (mirrors PhaseVocoder).
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
    /// Per-channel PSOLA pipeline: detect epochs, then OLA grains anchored on
    /// each epoch at the scaled output position.
    /// </summary>
    private static float[] ProcessChannel(
        float[] input,
        double factor,
        int sampleRate,
        int defaultPeriod,
        int? pitchPeriodOverride,
        int? windowSizeOverride,
        int outFrames)
    {
        var output = new float[outFrames];
        if (input.Length == 0) return output;

        // Locate input epochs (period-spaced peak positions).
        int[] epochs = FindEpochs(input, sampleRate, defaultPeriod, pitchPeriodOverride);
        if (epochs.Length == 0) return output;

        // Correct TD-PSOLA time-stretch (Moulines-Charpentier 1990): output
        // epochs are placed on a uniform ONE-PERIOD grid over the output
        // length, and each output epoch sources its grain from the NEAREST
        // input epoch (mapped back via outEpoch / factor). This duplicates
        // grains for factor > 1 and decimates them for factor < 1 while
        // keeping output epochs ONE period apart — so the Hann grains (spanning
        // 2 × period) always overlap at 50%, giving constant overlap and unity
        // level at EVERY factor.
        //
        // The previous mapping (outEpoch = round(inEpoch × factor)) spaced
        // output epochs period × factor apart, so at factor 2 adjacent grains
        // abutted at their near-zero Hann tails → amplitude nulls at the pitch
        // rate (buzzy tremolo), and at factor < 1 grains piled up above unity.

        int outPos = 0;
        while (outPos < outFrames)
        {
            // Map this output position back to input time, then snap to the
            // nearest input epoch to source the grain.
            double inPos = outPos / factor;
            int srcEpoch = NearestEpoch(epochs, inPos);

            // Effective period for THIS source epoch — override wins, else
            // re-detect via a local frame anchored at the source epoch.
            int effectivePeriod;
            if (pitchPeriodOverride.HasValue)
            {
                effectivePeriod = pitchPeriodOverride.Value;
            }
            else
            {
                int frameLen = Math.Min(1024, input.Length - srcEpoch);
                if (frameLen < 256) effectivePeriod = defaultPeriod;
                else
                {
                    var frame = new float[frameLen];
                    Array.Copy(input, srcEpoch, frame, 0, frameLen);
                    int detected = DetectPitchPeriod(frame, sampleRate);
                    effectivePeriod = detected > 0 ? detected : defaultPeriod;
                }
            }
            if (effectivePeriod < 1) effectivePeriod = defaultPeriod;

            // W4 LOCK — grain length is windowSizeOverride OR 2 × effectivePeriod.
            int grainLen = windowSizeOverride ?? (2 * effectivePeriod);
            if (grainLen < 2) grainLen = 2;

            float[] grain = ExtractWindowedGrain(input, srcEpoch, grainLen);
            OverlapAddGrain(output, grain, outPos);

            // Advance the output cursor by ONE pitch period — independent of
            // factor. Constant one-period spacing keeps grain overlap at 50%.
            outPos += effectivePeriod;
        }

        return output;
    }

    /// <summary>
    /// Returns the input epoch nearest to <paramref name="inPos"/> (samples).
    /// <paramref name="epochs"/> is ascending; a linear scan with an early
    /// exit past the crossover is adequate at the epoch counts PSOLA produces.
    /// </summary>
    private static int NearestEpoch(int[] epochs, double inPos)
    {
        int best = epochs[0];
        double bestDist = Math.Abs(epochs[0] - inPos);
        for (int i = 1; i < epochs.Length; i++)
        {
            double dist = Math.Abs(epochs[i] - inPos);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = epochs[i];
            }
            else if (epochs[i] > inPos)
            {
                // epochs ascending → once past inPos, distance only grows.
                break;
            }
        }
        return best;
    }

    /// <summary>
    /// Walk the input in fixed analysis windows; per window detect the pitch
    /// period (or use the override) and mark epochs at <c>period</c>-spaced
    /// peaks. When YIN finds nothing, fall back to <paramref name="defaultPeriod"/>
    /// for that window (unvoiced regions use uniform spacing).
    ///
    /// <para>
    /// W4 LOCK: <paramref name="pitchPeriodOverride"/>, when supplied, makes
    /// every window use the override period — no YIN calls happen at all.
    /// </para>
    /// </summary>
    private static int[] FindEpochs(
        float[] channelInput, int sampleRate, int defaultPeriod, int? pitchPeriodOverride)
    {
        const int AnalysisWindow = 1024;
        var epochs = new List<int>();
        int pos = 0;
        int frames = channelInput.Length;

        while (pos < frames)
        {
            int windowLen = Math.Min(AnalysisWindow, frames - pos);
            if (windowLen < 32) break;

            int period;
            if (pitchPeriodOverride.HasValue)
            {
                period = pitchPeriodOverride.Value;
            }
            else
            {
                var window = new float[windowLen];
                Array.Copy(channelInput, pos, window, 0, windowLen);
                int detected = DetectPitchPeriod(window, sampleRate);
                period = detected > 0 ? detected : defaultPeriod;
            }
            if (period < 1) period = defaultPeriod;

            // Mark epochs at uniform period spacing within this window. For
            // voiced material, this approximates the pitch-period peak-picking
            // shape without a separate peak-pick pass (which would need a
            // signed-amplitude scan inside the period band). The OLA result
            // is invariant to whether epochs land exactly on waveform peaks
            // as long as they're period-spaced.
            for (int e = pos; e < pos + windowLen; e += period)
            {
                if (e >= frames) break;
                epochs.Add(e);
            }
            pos += windowLen;
        }
        return epochs.ToArray();
    }

    /// <summary>
    /// Extract <paramref name="grainLen"/> samples centered on
    /// <paramref name="epoch"/>, Hann-windowed. Out-of-bounds samples (epoch
    /// near buffer edge) become zero — equivalent to zero-padding.
    /// </summary>
    private static float[] ExtractWindowedGrain(float[] input, int epoch, int grainLen)
    {
        var grain = new float[grainLen];
        float[] window = WindowFunctions.Hann(grainLen);
        int half = grainLen / 2;
        int start = epoch - half;
        for (int k = 0; k < grainLen; k++)
        {
            int src = start + k;
            if (src < 0 || src >= input.Length) continue;
            grain[k] = input[src] * window[k];
        }
        return grain;
    }

    /// <summary>
    /// Overlap-add <paramref name="grain"/> into <paramref name="output"/>
    /// at <paramref name="outEpoch"/>. Grain is centered on outEpoch (the
    /// inverse of <see cref="ExtractWindowedGrain"/>'s centering).
    /// </summary>
    private static void OverlapAddGrain(float[] output, float[] grain, int outEpoch)
    {
        int half = grain.Length / 2;
        int start = outEpoch - half;
        for (int k = 0; k < grain.Length; k++)
        {
            int dst = start + k;
            if (dst < 0 || dst >= output.Length) continue;
            output[dst] += grain[k];
        }
    }
}
