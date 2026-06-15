using FlowLang.Diagnostics;

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
        cutoffHz = ClampCutoff(cutoffHz, input.SampleRate);
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
        cutoffHz = ClampCutoff(cutoffHz, input.SampleRate);
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
        float nyquist = input.SampleRate / 2f;

        // Charitable clamps (CLAUDE.md charitable-interpretation policy): a
        // degenerate band must yield a sane filter + WarnOnce advisory, never a
        // session-killing throw. Mirrors the FormantSynthesizer convention
        // (pre-clamp highHz to nyquist - 1) and the Q-clamp WarnOnce below.
        if (lowHz <= 0f)
        {
            RenderingDiagnostics.WarnOnce(
                "bandpass:low_clamp",
                $"[filter] bandpass lower cutoff ({lowHz} Hz) <= 0 — clamped to 20 Hz.");
            lowHz = 20f;
        }
        if (highHz >= nyquist)
        {
            RenderingDiagnostics.WarnOnce(
                "bandpass:high_nyquist_clamp",
                $"[filter] bandpass upper cutoff ({highHz} Hz) >= Nyquist ({nyquist} Hz) — " +
                $"clamped to {nyquist - 1f} Hz.");
            highHz = nyquist - 1f;
        }
        if (highHz <= lowHz)
        {
            // Widen to a minimal realisable band rather than throwing. One ULP
            // above lowHz then routes through the existing Q clamp below.
            float widened = MathF.Min(MathF.BitIncrement(lowHz), nyquist - 1f);
            RenderingDiagnostics.WarnOnce(
                "bandpass:band_inverted",
                $"[filter] bandpass upper cutoff ({highHz} Hz) <= lower ({lowHz} Hz) — " +
                $"widened to a minimal band at {lowHz} Hz.");
            highHz = widened;
        }

        // Bandpass: center frequency and bandwidth
        float centerHz = (float)Math.Sqrt(lowHz * highHz);
        float bw = highHz - lowHz;
        float q = centerHz / bw;

        // Charitable clamp: ulp-narrow bands push Q to extreme values that drive
        // the biquad pole onto the unit circle (endless ringing, output never
        // decays).  Cap at 100 (≈0.7 cents bandwidth at 1 kHz) and warn once so
        // the composer knows their band is narrower than the filter can realise.
        // Mirrors the house clamp style in improv/StyleRegistry (WarnOnce pattern).
        const float MaxQ = 100f;
        if (q > MaxQ)
        {
            RenderingDiagnostics.WarnOnce(
                $"bandpass:Q_clamp:{centerHz:F1}",
                $"[filter] bandpass Q={q:F1} exceeds maximum ({MaxQ}); clamped to {MaxQ}. " +
                $"The requested bandwidth ({bw:F3} Hz) is narrower than the filter can realise.");
            q = MaxQ;
        }

        ComputeBandpassCoefficients(centerHz, q, input.SampleRate,
            out float b0, out float b1, out float b2, out float a1, out float a2);

        return ApplyBiquad(input, b0, b1, b2, a1, a2);
    }

    /// <summary>
    /// Charitably clamps the cutoff frequency into the realisable
    /// (0, Nyquist) range, emitting a one-shot stderr advisory when it has to.
    /// Per CLAUDE.md charitable-interpretation policy a degenerate cutoff must
    /// produce a sane filter + WarnOnce, never a throw that kills the
    /// interpreter session. Mirrors the FormantSynthesizer convention of
    /// pre-clamping to <c>sampleRate / 2 - 1</c>.
    /// </summary>
    /// <returns>The clamped cutoff frequency to feed the coefficient math.</returns>
    private static float ClampCutoff(float cutoffHz, int sampleRate)
    {
        if (cutoffHz <= 0f)
        {
            RenderingDiagnostics.WarnOnce(
                "filter:cutoff_low_clamp",
                $"[filter] cutoff ({cutoffHz} Hz) <= 0 — clamped to 20 Hz.");
            return 20f;
        }

        float nyquist = sampleRate / 2f;
        if (cutoffHz >= nyquist)
        {
            RenderingDiagnostics.WarnOnce(
                "filter:cutoff_nyquist_clamp",
                $"[filter] cutoff ({cutoffHz} Hz) >= Nyquist ({nyquist} Hz) — " +
                $"clamped to {nyquist - 1f} Hz.");
            return nyquist - 1f;
        }

        return cutoffHz;
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
