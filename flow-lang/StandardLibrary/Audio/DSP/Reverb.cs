namespace FlowLang.StandardLibrary.Audio.DSP;

/// <summary>
/// Schroeder reverb implementation using 4 parallel comb filters and 2 series allpass filters.
/// All processing returns new buffers — inputs are never modified.
///
/// §3.8 tail extension (audit-0609): the output buffer is extended beyond the input
/// length to carry reverberant decay energy, mirroring Delay.Apply's CalculateTailFrames
/// approach.  CombFilter and AllpassFilter operate over inputLength + tailFrames, feeding
/// zeros past the input end.  The tail length is derived from the comb-filter feedback
/// coefficient: frames until the network decays below -60 dB, capped at 10 s.
///
/// §3.6 denormal flush (audit-0609): filterStore and the recirculating buffer entries
/// are flushed to zero when subnormal, matching the house idiom in Delay.cs:63 and
/// Filter.ApplyBiquad:168.
/// </summary>
public static class Reverb
{
    // Comb filter delay times in samples at 44100 Hz (scaled by room size)
    private static readonly int[] CombDelays = [1116, 1188, 1277, 1356];

    // Allpass filter delay times in samples at 44100 Hz
    private static readonly int[] AllpassDelays = [556, 441];

    // Allpass feedback coefficient
    private const float AllpassFeedback = 0.5f;

    /// <summary>
    /// Applies Schroeder reverb to a buffer.
    /// </summary>
    /// <param name="input">Source audio buffer (not modified).</param>
    /// <param name="roomSize">Room size in range [0, 1]. Controls feedback amount and delay scaling.</param>
    /// <param name="damping">Damping in range [0, 1]. Higher values attenuate high frequencies faster.</param>
    /// <param name="mix">Wet/dry mix in range [0, 1]. 0 = fully dry, 1 = fully wet.</param>
    /// <returns>A new buffer with reverb applied; may be longer than input to carry the decay tail.</returns>
    public static AudioBuffer Apply(AudioBuffer input, float roomSize, float damping, float mix)
    {
        // Clamp parameters to valid ranges
        roomSize = Math.Clamp(roomSize, 0f, 1f);
        damping = Math.Clamp(damping, 0f, 1f);
        mix = Math.Clamp(mix, 0f, 1f);

        // Scale delay times for the actual sample rate
        double rateScale = input.SampleRate / 44100.0;

        // Map room size to feedback range [0.7, 0.98]. Moved out of ProcessChannel
        // (Phase 15 Plan 03 strict refactor) so the new RT60 overload can share
        // ProcessChannel by passing a pre-computed Schroeder feedback coefficient.
        float feedback = 0.7f + roomSize * 0.28f;

        int tailFrames = CalculateTailFrames(feedback, input.SampleRate);
        int outputFrames = input.Frames + tailFrames;
        var result = new AudioBuffer(outputFrames, input.Channels, input.SampleRate);

        // Process each channel independently
        for (int ch = 0; ch < input.Channels; ch++)
        {
            var dry = ExtractChannel(input, ch);
            var wet = ProcessChannel(dry, feedback, damping, rateScale, outputFrames);

            // Mix wet/dry into result; past input.Frames dry is silence (tail is wet-only).
            for (int frame = 0; frame < outputFrames; frame++)
            {
                float dryVal = frame < input.Frames ? dry[frame] : 0f;
                float mixed = dryVal * (1f - mix) + wet[frame] * mix;
                result.SetSample(frame, ch, mixed);
            }
        }

        return result;
    }

    /// <summary>
    /// Applies Schroeder reverb with an RT60 (decay-time) parameter instead of room
    /// size. The target decay time maps to the comb-filter feedback coefficient via
    /// Schroeder's closed-form: <c>feedback = 10^(-3 · D/fs / RT60)</c> where D is
    /// the mean comb-delay length in samples and fs is the sample rate. The result
    /// is feedback clamped to [0, 0.99] — an upper cap prevents runaway amplification
    /// for pathologically large RT60 (CONTEXT D-13, RESEARCH Open Q 3 locked at 0.99).
    /// </summary>
    /// <param name="input">Source audio buffer (not modified).</param>
    /// <param name="rt60Seconds">
    /// Desired decay time to -60dB in seconds. Values &lt;= 0 are coerced to 0.001
    /// internally to avoid div-by-zero; the SongRenderer owns the "rt60 == 0 = dry"
    /// short-circuit per CONTEXT D-02 (DSP stays pure).
    /// </param>
    /// <param name="damping">Damping in [0, 1]. Higher values attenuate highs faster.</param>
    /// <param name="mix">Wet/dry mix in [0, 1]. 0 = fully dry, 1 = fully wet.</param>
    /// <returns>A new buffer with reverb applied; may be longer than input to carry the decay tail.</returns>
    public static AudioBuffer Apply(AudioBuffer input, double rt60Seconds, float damping, float mix)
    {
        // Guard against div-by-zero; dry short-circuit lives in SongRenderer per D-02.
        if (rt60Seconds <= 0.0) rt60Seconds = 0.001;

        // Scale delay times for the actual sample rate (mirrors roomSize overload).
        double rateScale = input.SampleRate / 44100.0;
        double avgDelaySamples = (CombDelays[0] + CombDelays[1] + CombDelays[2] + CombDelays[3]) / 4.0 * rateScale;
        double avgDelaySeconds = avgDelaySamples / input.SampleRate;

        // Schroeder RT60 → feedback: feedback^N = 10^-3 where N = RT60 / (D/fs).
        // Cap at 0.99 per RESEARCH Open Question 3 — pre-empts pathological feedback
        // when rt60 is orders-of-magnitude larger than the comb-delay period.
        float feedback = (float)Math.Clamp(
            Math.Pow(10.0, -3.0 * avgDelaySeconds / rt60Seconds), 0.0, 0.99);

        damping = Math.Clamp(damping, 0f, 1f);
        mix = Math.Clamp(mix, 0f, 1f);

        int tailFrames = CalculateTailFrames(feedback, input.SampleRate);
        int outputFrames = input.Frames + tailFrames;
        var result = new AudioBuffer(outputFrames, input.Channels, input.SampleRate);

        for (int ch = 0; ch < input.Channels; ch++)
        {
            var dry = ExtractChannel(input, ch);
            var wet = ProcessChannel(dry, feedback, damping, rateScale, outputFrames);
            for (int frame = 0; frame < outputFrames; frame++)
            {
                float dryVal = frame < input.Frames ? dry[frame] : 0f;
                float mixed = dryVal * (1f - mix) + wet[frame] * mix;
                result.SetSample(frame, ch, mixed);
            }
        }
        return result;
    }

    /// <summary>
    /// Calculates how many extra frames of reverb tail to include.
    /// Uses the longest comb delay as the period and computes frames until
    /// the network decays below -60 dB, capped at 10 s (mirrors Delay.CalculateTailFrames).
    /// </summary>
    internal static int CalculateTailFrames(float feedback, int sampleRate)
    {
        if (feedback <= 0f) return 0;
        if (feedback >= 1f) return sampleRate * 10;

        // Longest comb delay drives the slowest-decaying partial.
        int longestDelay = CombDelays[^1]; // 1356 samples at 44100 Hz

        // -60 dB = feedback^n  =>  n = -60 / (20 * log10(feedback))
        double repeats = -60.0 / (20.0 * Math.Log10(feedback));
        int tailSamples = (int)(repeats * longestDelay);

        // Cap at 10 seconds
        int maxTail = sampleRate * 10;
        return Math.Min(tailSamples, maxTail);
    }

    /// <summary>
    /// Processes a single channel through the Schroeder reverb network. Accepts a
    /// pre-computed feedback coefficient so both Apply overloads (roomSize + rt60)
    /// can share the implementation without duplicating the comb-filter network.
    /// The output array has length <paramref name="outputLength"/>, which may be
    /// longer than <paramref name="input"/> to carry the reverb tail.
    /// </summary>
    private static float[] ProcessChannel(float[] input, float feedback, float damping, double rateScale, int outputLength)
    {
        // 4 parallel comb filters
        var combOutputs = new float[4][];
        for (int i = 0; i < 4; i++)
        {
            int delay = (int)(CombDelays[i] * rateScale);
            combOutputs[i] = CombFilter(input, delay, feedback, damping, outputLength);
        }

        // Sum comb filter outputs
        var summed = new float[outputLength];
        for (int i = 0; i < outputLength; i++)
        {
            summed[i] = (combOutputs[0][i] + combOutputs[1][i] +
                         combOutputs[2][i] + combOutputs[3][i]) * 0.25f;
        }

        // 2 series allpass filters
        var current = summed;
        for (int i = 0; i < 2; i++)
        {
            int delay = (int)(AllpassDelays[i] * rateScale);
            current = AllpassFilter(current, delay, AllpassFeedback);
        }

        return current;
    }

    /// <summary>
    /// Lowpass feedback comb filter.
    /// output[n] = input[n] + feedback * lpf(output[n - delay])
    ///
    /// Operates over <paramref name="outputLength"/> frames, feeding zeros past
    /// the end of <paramref name="input"/> so the reverb tail decays naturally.
    /// </summary>
    private static float[] CombFilter(float[] input, int delay, float feedback, float damping, int outputLength)
    {
        if (delay < 1) delay = 1;

        var output = new float[outputLength];
        var buffer = new float[delay];
        int bufferIndex = 0;
        float filterStore = 0f;

        for (int i = 0; i < outputLength; i++)
        {
            float bufOut = buffer[bufferIndex];

            // One-pole lowpass in the feedback path (damping)
            filterStore = bufOut * (1f - damping) + filterStore * damping;

            // Flush denormals from the feedback state variables so that the
            // exponentially-decaying tail does not degrade to subnormal-float
            // arithmetic (10–100× slower on x86/ARM when subnormals remain).
            // Mirrors the house idiom in Delay.cs:63 and Filter.ApplyBiquad:168.
            if (float.IsSubnormal(filterStore)) filterStore = 0f;

            // Past the input end feed silence (zero) so the tail decays naturally.
            float inSample = i < input.Length ? input[i] : 0f;
            float newBuf = inSample + filterStore * feedback;
            if (float.IsSubnormal(newBuf)) newBuf = 0f;
            buffer[bufferIndex] = newBuf;
            output[i] = bufOut;

            bufferIndex++;
            if (bufferIndex >= delay) bufferIndex = 0;
        }

        return output;
    }

    /// <summary>
    /// Allpass filter for diffusion.
    /// output[n] = -g * input[n] + input[n - delay] + g * output[n - delay]
    /// </summary>
    private static float[] AllpassFilter(float[] input, int delay, float gain)
    {
        int length = input.Length;
        if (delay < 1) delay = 1;

        var output = new float[length];
        var buffer = new float[delay];
        int bufferIndex = 0;

        for (int i = 0; i < length; i++)
        {
            float bufOut = buffer[bufferIndex];
            float temp = -gain * input[i] + bufOut;
            float newBuf = input[i] + gain * temp;
            // Flush denormals from the allpass feedback path.
            if (float.IsSubnormal(temp)) temp = 0f;
            if (float.IsSubnormal(newBuf)) newBuf = 0f;
            buffer[bufferIndex] = newBuf;
            output[i] = temp;

            bufferIndex++;
            if (bufferIndex >= delay) bufferIndex = 0;
        }

        return output;
    }

    /// <summary>
    /// Extracts a single channel from an interleaved buffer.
    /// </summary>
    private static float[] ExtractChannel(AudioBuffer buffer, int channel)
    {
        var result = new float[buffer.Frames];
        for (int i = 0; i < buffer.Frames; i++)
        {
            result[i] = buffer.GetSample(i, channel);
        }
        return result;
    }
}
