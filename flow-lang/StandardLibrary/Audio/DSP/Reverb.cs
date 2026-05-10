namespace FlowLang.StandardLibrary.Audio.DSP;

/// <summary>
/// Schroeder reverb implementation using 4 parallel comb filters and 2 series allpass filters.
/// All processing returns new buffers — inputs are never modified.
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
    /// <returns>A new buffer with reverb applied.</returns>
    public static AudioBuffer Apply(AudioBuffer input, float roomSize, float damping, float mix)
    {
        // Clamp parameters to valid ranges
        roomSize = Math.Clamp(roomSize, 0f, 1f);
        damping = Math.Clamp(damping, 0f, 1f);
        mix = Math.Clamp(mix, 0f, 1f);

        var result = new AudioBuffer(input.Frames, input.Channels, input.SampleRate);

        // Scale delay times for the actual sample rate
        double rateScale = input.SampleRate / 44100.0;

        // Map room size to feedback range [0.7, 0.98]. Moved out of ProcessChannel
        // (Phase 15 Plan 03 strict refactor) so the new RT60 overload can share
        // ProcessChannel by passing a pre-computed Schroeder feedback coefficient.
        float feedback = 0.7f + roomSize * 0.28f;

        // Process each channel independently
        for (int ch = 0; ch < input.Channels; ch++)
        {
            var dry = ExtractChannel(input, ch);
            var wet = ProcessChannel(dry, feedback, damping, rateScale);

            // Mix wet/dry into result
            for (int frame = 0; frame < input.Frames; frame++)
            {
                float mixed = dry[frame] * (1f - mix) + wet[frame] * mix;
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
    /// <returns>A new buffer with reverb applied.</returns>
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

        var result = new AudioBuffer(input.Frames, input.Channels, input.SampleRate);
        for (int ch = 0; ch < input.Channels; ch++)
        {
            var dry = ExtractChannel(input, ch);
            var wet = ProcessChannel(dry, feedback, damping, rateScale);
            for (int frame = 0; frame < input.Frames; frame++)
            {
                float mixed = dry[frame] * (1f - mix) + wet[frame] * mix;
                result.SetSample(frame, ch, mixed);
            }
        }
        return result;
    }

    /// <summary>
    /// Processes a single channel through the Schroeder reverb network. Accepts a
    /// pre-computed feedback coefficient so both Apply overloads (roomSize + rt60)
    /// can share the implementation without duplicating the comb-filter network.
    /// </summary>
    private static float[] ProcessChannel(float[] input, float feedback, float damping, double rateScale)
    {
        int length = input.Length;

        // 4 parallel comb filters
        var combOutputs = new float[4][];
        for (int i = 0; i < 4; i++)
        {
            int delay = (int)(CombDelays[i] * rateScale);
            combOutputs[i] = CombFilter(input, delay, feedback, damping);
        }

        // Sum comb filter outputs
        var summed = new float[length];
        for (int i = 0; i < length; i++)
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
    /// </summary>
    private static float[] CombFilter(float[] input, int delay, float feedback, float damping)
    {
        int length = input.Length;
        if (delay < 1) delay = 1;

        var output = new float[length];
        var buffer = new float[delay];
        int bufferIndex = 0;
        float filterStore = 0f;

        for (int i = 0; i < length; i++)
        {
            float bufOut = buffer[bufferIndex];

            // One-pole lowpass in the feedback path (damping)
            filterStore = bufOut * (1f - damping) + filterStore * damping;

            buffer[bufferIndex] = input[i] + filterStore * feedback;
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
            buffer[bufferIndex] = input[i] + gain * temp;
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
