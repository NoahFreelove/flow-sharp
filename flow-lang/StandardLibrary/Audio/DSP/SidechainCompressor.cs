namespace FlowLang.StandardLibrary.Audio.DSP;

/// <summary>
/// Sidechain compressor that uses a separate trigger buffer to control
/// gain reduction on a source buffer. Produces the classic EDM "pumping" effect
/// where one sound ducks under another (e.g., bass ducks under kick).
/// All processing returns new buffers -- inputs are never modified.
/// </summary>
public static class SidechainCompressor
{
    /// <summary>
    /// Applies sidechain compression to a source buffer using a trigger buffer.
    /// </summary>
    /// <param name="source">The audio to compress (not modified).</param>
    /// <param name="trigger">The sidechain signal that drives gain reduction (not modified).</param>
    /// <param name="thresholdDb">Threshold in dB (must be &lt;= 0). Trigger levels above this cause ducking.</param>
    /// <param name="ratio">Compression ratio (must be &gt;= 1.0). Higher = more aggressive ducking.</param>
    /// <param name="attackMs">Attack time in ms. How fast the duck engages. Default 10ms.</param>
    /// <param name="releaseMs">Release time in ms. How fast the duck releases. Default 100ms.</param>
    /// <returns>A new buffer with sidechain compression applied. Same length as source.</returns>
    public static AudioBuffer Apply(AudioBuffer source, AudioBuffer trigger,
        float thresholdDb, float ratio, float attackMs = 10f, float releaseMs = 100f)
    {
        if (source.Frames == 0)
            return new AudioBuffer(0, source.Channels, source.SampleRate);

        if (thresholdDb > 0f)
            throw new ArgumentException(
                $"Sidechain threshold must be <= 0 dB (got {thresholdDb} dB).");

        if (ratio < 1f)
            throw new ArgumentException(
                $"Sidechain ratio must be >= 1.0 (got {ratio}).");

        if (attackMs < 0f) attackMs = 0f;
        if (releaseMs < 0f) releaseMs = 0f;

        var result = new AudioBuffer(source.Frames, source.Channels, source.SampleRate);

        // Compute envelope follower coefficients (same as Compressor.cs)
        float attackCoeff = attackMs > 0f
            ? (float)Math.Exp(-1.0 / (attackMs * 0.001 * source.SampleRate))
            : 0f;
        float releaseCoeff = releaseMs > 0f
            ? (float)Math.Exp(-1.0 / (releaseMs * 0.001 * source.SampleRate))
            : 0f;

        float envelopeDb = -96f; // Start at silence floor

        // Iterate over ALL source frames (not min of trigger/source)
        for (int frame = 0; frame < source.Frames; frame++)
        {
            // Peak detection on TRIGGER buffer
            float trigPeak = 0f;
            if (frame < trigger.Frames)
            {
                for (int ch = 0; ch < trigger.Channels; ch++)
                {
                    float abs = Math.Abs(trigger.GetSample(frame, ch));
                    if (abs > trigPeak) trigPeak = abs;
                }
            }
            // If frame >= trigger.Frames, trigPeak stays 0 -- trigger naturally decays via release

            // Convert trigger peak to dB
            float inputDb = trigPeak > 1e-10f
                ? 20f * MathF.Log10(trigPeak)
                : -96f;

            // Compute gain reduction based on trigger level
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
                // Attack (gain going down = trigger signal detected)
                envelopeDb = attackCoeff * envelopeDb + (1f - attackCoeff) * targetDb;
            }
            else
            {
                // Release (gain coming back up = trigger signal gone)
                envelopeDb = releaseCoeff * envelopeDb + (1f - releaseCoeff) * targetDb;
            }

            // Convert gain from dB to linear
            float gainLinear = MathF.Pow(10f, envelopeDb / 20f);

            // Apply gain to all source channels
            for (int ch = 0; ch < source.Channels; ch++)
            {
                float sample = source.GetSample(frame, ch) * gainLinear;
                result.SetSample(frame, ch, sample);
            }
        }

        return result;
    }
}
