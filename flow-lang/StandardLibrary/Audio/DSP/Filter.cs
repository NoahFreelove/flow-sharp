namespace FlowLang.StandardLibrary.Audio.DSP;

/// <summary>
/// Biquad filter implementation supporting lowpass, highpass, and bandpass modes.
/// All processing returns new buffers — inputs are never modified.
/// </summary>
public static class Filter
{
    /// <summary>
    /// Applies a lowpass biquad filter to a buffer.
    /// </summary>
    /// <param name="input">Source audio buffer (not modified).</param>
    /// <param name="cutoffHz">Cutoff frequency in Hz. Must be positive and below Nyquist (sampleRate / 2).</param>
    /// <param name="q">Resonance (Q factor). Default 0.707 for Butterworth response. Must be positive.</param>
    /// <returns>A new buffer with the filter applied.</returns>
    public static AudioBuffer Lowpass(AudioBuffer input, float cutoffHz, float q = 0.707f)
    {
        ValidateCutoff(cutoffHz, input.SampleRate);
        if (q <= 0f) q = 0.707f;

        ComputeLowpassCoefficients(cutoffHz, q, input.SampleRate,
            out float b0, out float b1, out float b2, out float a1, out float a2);

        return ApplyBiquad(input, b0, b1, b2, a1, a2);
    }

    /// <summary>
    /// Applies a highpass biquad filter to a buffer.
    /// </summary>
    /// <param name="input">Source audio buffer (not modified).</param>
    /// <param name="cutoffHz">Cutoff frequency in Hz. Must be positive and below Nyquist (sampleRate / 2).</param>
    /// <param name="q">Resonance (Q factor). Default 0.707 for Butterworth response. Must be positive.</param>
    /// <returns>A new buffer with the filter applied.</returns>
    public static AudioBuffer Highpass(AudioBuffer input, float cutoffHz, float q = 0.707f)
    {
        ValidateCutoff(cutoffHz, input.SampleRate);
        if (q <= 0f) q = 0.707f;

        ComputeHighpassCoefficients(cutoffHz, q, input.SampleRate,
            out float b0, out float b1, out float b2, out float a1, out float a2);

        return ApplyBiquad(input, b0, b1, b2, a1, a2);
    }

    /// <summary>
    /// Applies a bandpass biquad filter to a buffer.
    /// </summary>
    /// <param name="input">Source audio buffer (not modified).</param>
    /// <param name="lowHz">Lower cutoff frequency in Hz.</param>
    /// <param name="highHz">Upper cutoff frequency in Hz.</param>
    /// <returns>A new buffer with the filter applied.</returns>
    public static AudioBuffer Bandpass(AudioBuffer input, float lowHz, float highHz)
    {
        if (lowHz <= 0f)
            throw new ArgumentException("Lower cutoff frequency must be positive.");
        if (highHz <= lowHz)
            throw new ArgumentException("Upper cutoff frequency must be greater than lower cutoff.");

        float nyquist = input.SampleRate / 2f;
        if (highHz >= nyquist)
            throw new ArgumentException(
                $"Upper cutoff frequency ({highHz} Hz) must be below Nyquist frequency ({nyquist} Hz).");

        // Bandpass: center frequency and bandwidth
        float centerHz = (float)Math.Sqrt(lowHz * highHz);
        float bw = highHz - lowHz;
        float q = centerHz / bw;

        ComputeBandpassCoefficients(centerHz, q, input.SampleRate,
            out float b0, out float b1, out float b2, out float a1, out float a2);

        return ApplyBiquad(input, b0, b1, b2, a1, a2);
    }

    /// <summary>
    /// Validates cutoff frequency is positive and below Nyquist.
    /// </summary>
    private static void ValidateCutoff(float cutoffHz, int sampleRate)
    {
        if (cutoffHz <= 0f)
            throw new ArgumentException("Cutoff frequency must be positive.");

        float nyquist = sampleRate / 2f;
        if (cutoffHz >= nyquist)
            throw new ArgumentException(
                $"Cutoff frequency ({cutoffHz} Hz) must be below Nyquist frequency ({nyquist} Hz).");
    }

    /// <summary>
    /// Computes biquad coefficients for lowpass filter.
    /// </summary>
    private static void ComputeLowpassCoefficients(float cutoff, float q, int sampleRate,
        out float b0, out float b1, out float b2, out float a1, out float a2)
    {
        double w0 = 2.0 * Math.PI * cutoff / sampleRate;
        double cosW0 = Math.Cos(w0);
        double sinW0 = Math.Sin(w0);
        double alpha = sinW0 / (2.0 * q);

        double a0 = 1.0 + alpha;
        b0 = (float)((1.0 - cosW0) / 2.0 / a0);
        b1 = (float)((1.0 - cosW0) / a0);
        b2 = b0;
        a1 = (float)(-2.0 * cosW0 / a0);
        a2 = (float)((1.0 - alpha) / a0);
    }

    /// <summary>
    /// Computes biquad coefficients for highpass filter.
    /// </summary>
    private static void ComputeHighpassCoefficients(float cutoff, float q, int sampleRate,
        out float b0, out float b1, out float b2, out float a1, out float a2)
    {
        double w0 = 2.0 * Math.PI * cutoff / sampleRate;
        double cosW0 = Math.Cos(w0);
        double sinW0 = Math.Sin(w0);
        double alpha = sinW0 / (2.0 * q);

        double a0 = 1.0 + alpha;
        b0 = (float)((1.0 + cosW0) / 2.0 / a0);
        b1 = (float)(-(1.0 + cosW0) / a0);
        b2 = b0;
        a1 = (float)(-2.0 * cosW0 / a0);
        a2 = (float)((1.0 - alpha) / a0);
    }

    /// <summary>
    /// Computes biquad coefficients for bandpass filter (constant skirt gain).
    /// </summary>
    private static void ComputeBandpassCoefficients(float center, float q, int sampleRate,
        out float b0, out float b1, out float b2, out float a1, out float a2)
    {
        double w0 = 2.0 * Math.PI * center / sampleRate;
        double cosW0 = Math.Cos(w0);
        double sinW0 = Math.Sin(w0);
        double alpha = sinW0 / (2.0 * q);

        double a0 = 1.0 + alpha;
        b0 = (float)(alpha / a0);
        b1 = 0f;
        b2 = (float)(-alpha / a0);
        a1 = (float)(-2.0 * cosW0 / a0);
        a2 = (float)((1.0 - alpha) / a0);
    }

    /// <summary>
    /// Applies biquad filter coefficients to a buffer, processing each channel independently.
    /// Uses Direct Form I implementation.
    /// </summary>
    private static AudioBuffer ApplyBiquad(AudioBuffer input,
        float b0, float b1, float b2, float a1, float a2)
    {
        var result = new AudioBuffer(input.Frames, input.Channels, input.SampleRate);

        for (int ch = 0; ch < input.Channels; ch++)
        {
            // State variables per channel
            float x1 = 0f, x2 = 0f; // Previous input samples
            float y1 = 0f, y2 = 0f; // Previous output samples

            for (int frame = 0; frame < input.Frames; frame++)
            {
                float x0 = input.GetSample(frame, ch);

                float y0 = b0 * x0 + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;

                // Prevent denormals
                if (float.IsSubnormal(y0)) y0 = 0f;

                result.SetSample(frame, ch, y0);

                x2 = x1;
                x1 = x0;
                y2 = y1;
                y1 = y0;
            }
        }

        return result;
    }
}
