namespace FlowLang.StandardLibrary.Audio.DSP;

/// <summary>
/// Feedback delay line with wet/dry mix control.
/// All processing returns new buffers — inputs are never modified.
/// </summary>
public static class Delay
{
    /// <summary>
    /// Applies a feedback delay effect to a buffer.
    /// </summary>
    /// <param name="input">Source audio buffer (not modified).</param>
    /// <param name="delayMs">Delay time in milliseconds. Must be positive.</param>
    /// <param name="feedback">Feedback amount in range [0, 1). Controls how much of the delayed signal is fed back.
    /// Values of 1.0 create infinite feedback (warned). Values above 1.0 are rejected.</param>
    /// <param name="mix">Wet/dry mix in range [0, 1]. 0 = fully dry, 1 = fully wet.</param>
    /// <returns>A new buffer with delay applied. May be longer than input if delay tail extends beyond.</returns>
    public static AudioBuffer Apply(AudioBuffer input, float delayMs, float feedback, float mix)
    {
        if (delayMs <= 0f)
            throw new ArgumentException(
                $"Delay time must be positive (got {delayMs} ms).");

        if (feedback > 1f)
            throw new ArgumentException(
                $"Delay feedback must be <= 1.0 (got {feedback}). Values > 1.0 cause unstable infinite growth.");

        if (feedback < 0f) feedback = 0f;
        mix = Math.Clamp(mix, 0f, 1f);

        // feedback of exactly 1.0 is allowed but produces infinite sustain
        // (caller was warned via robustness docs; practical use is finite due to float precision)

        int delaySamples = (int)(delayMs * 0.001f * input.SampleRate);
        if (delaySamples < 1) delaySamples = 1;

        // Extend output buffer to include delay tail (up to a reasonable limit)
        int tailFrames = feedback > 0f
            ? CalculateTailFrames(delaySamples, feedback, input.SampleRate)
            : 0;
        int outputFrames = input.Frames + tailFrames;

        var result = new AudioBuffer(outputFrames, input.Channels, input.SampleRate);

        // Process each channel independently
        for (int ch = 0; ch < input.Channels; ch++)
        {
            var delayBuffer = new float[delaySamples];
            int writePos = 0;

            for (int frame = 0; frame < outputFrames; frame++)
            {
                // Input sample (zero-padded beyond input length)
                float dry = frame < input.Frames ? input.GetSample(frame, ch) : 0f;

                // Read from delay line
                float wet = delayBuffer[writePos];

                // Write to delay line: input + feedback * delayed
                delayBuffer[writePos] = dry + wet * feedback;

                // Prevent denormals in the delay buffer
                if (float.IsSubnormal(delayBuffer[writePos]))
                    delayBuffer[writePos] = 0f;

                // Mix output
                float output = dry * (1f - mix) + wet * mix;
                result.SetSample(frame, ch, output);

                writePos++;
                if (writePos >= delaySamples) writePos = 0;
            }
        }

        return result;
    }

    /// <summary>
    /// Calculates how many extra frames of tail to include based on feedback decay.
    /// Limits to a maximum of 10 seconds of tail.
    /// </summary>
    private static int CalculateTailFrames(int delaySamples, float feedback, int sampleRate)
    {
        if (feedback <= 0f) return 0;
        if (feedback >= 1f) return sampleRate * 10; // Cap at 10 seconds for feedback = 1.0

        // Number of repeats until signal drops below -60 dB
        // -60dB = feedback^n => n = -60 / (20 * log10(feedback))
        double repeats = -60.0 / (20.0 * Math.Log10(feedback));
        int tailSamples = (int)(repeats * delaySamples);

        // Cap at 10 seconds
        int maxTail = sampleRate * 10;
        return Math.Min(tailSamples, maxTail);
    }
}
