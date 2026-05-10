namespace FlowLang.StandardLibrary.Audio.DSP;

/// <summary>
/// Dynamic range compressor with threshold, ratio, attack, and release controls.
/// All processing returns new buffers — inputs are never modified.
/// </summary>
public static class Compressor
{
    /// <summary>
    /// Applies dynamic range compression to a buffer.
    /// </summary>
    /// <param name="input">Source audio buffer (not modified).</param>
    /// <param name="thresholdDb">Threshold in dB (must be &lt;= 0). Signals above this are compressed.</param>
    /// <param name="ratio">Compression ratio (must be &gt;= 1.0). 1.0 = no compression, infinity = limiter.</param>
    /// <param name="attackMs">Attack time in milliseconds. How fast the compressor reacts to increases. Default 10ms.</param>
    /// <param name="releaseMs">Release time in milliseconds. How fast the compressor recovers. Default 100ms.</param>
    /// <returns>A new buffer with compression applied.</returns>
    public static AudioBuffer Apply(AudioBuffer input, float thresholdDb, float ratio,
        float attackMs = 10f, float releaseMs = 100f)
    {
        if (thresholdDb > 0f)
            throw new ArgumentException(
                $"Compressor threshold must be <= 0 dB (got {thresholdDb} dB). Positive values indicate expansion, not compression.");

        if (ratio < 1f)
            throw new ArgumentException(
                $"Compressor ratio must be >= 1.0 (got {ratio}). Use 1.0 for no compression.");

        if (attackMs < 0f) attackMs = 0f;
        if (releaseMs < 0f) releaseMs = 0f;

        var result = new AudioBuffer(input.Frames, input.Channels, input.SampleRate);

        // Compute envelope follower coefficients
        float attackCoeff = attackMs > 0f
            ? (float)Math.Exp(-1.0 / (attackMs * 0.001 * input.SampleRate))
            : 0f;
        float releaseCoeff = releaseMs > 0f
            ? (float)Math.Exp(-1.0 / (releaseMs * 0.001 * input.SampleRate))
            : 0f;

        // Process using peak detection across all channels per frame
        float envelopeDb = -96f; // Start at silence floor

        for (int frame = 0; frame < input.Frames; frame++)
        {
            // Find peak amplitude across all channels for this frame
            float peak = 0f;
            for (int ch = 0; ch < input.Channels; ch++)
            {
                float abs = Math.Abs(input.GetSample(frame, ch));
                if (abs > peak) peak = abs;
            }

            // Convert to dB (with floor to avoid log(0))
            float inputDb = peak > 1e-10f
                ? 20f * (float)Math.Log10(peak)
                : -96f;

            // Compute gain reduction
            float gainReductionDb = 0f;
            if (inputDb > thresholdDb)
            {
                float overshoot = inputDb - thresholdDb;
                gainReductionDb = overshoot * (1f - 1f / ratio);
            }

            // Smooth the gain reduction with attack/release envelope
            float targetDb = -gainReductionDb;
            if (targetDb < envelopeDb)
            {
                // Attack (gain going down = louder signal detected)
                envelopeDb = attackCoeff * envelopeDb + (1f - attackCoeff) * targetDb;
            }
            else
            {
                // Release (gain coming back up)
                envelopeDb = releaseCoeff * envelopeDb + (1f - releaseCoeff) * targetDb;
            }

            // Convert gain from dB to linear
            float gainLinear = (float)Math.Pow(10.0, envelopeDb / 20.0);

            // Apply gain to all channels
            for (int ch = 0; ch < input.Channels; ch++)
            {
                float sample = input.GetSample(frame, ch) * gainLinear;
                result.SetSample(frame, ch, sample);
            }
        }

        return result;
    }
}
