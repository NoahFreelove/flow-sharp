namespace FlowLang.Audio;

/// <summary>
/// Shared audio utility methods used across playback and backend components.
/// </summary>
public static class AudioUtils
{
    /// <summary>
    /// Clamps all samples to the valid range [-1.0, 1.0] and replaces NaN/Infinity with 0.
    /// Returns a new array if clamping was needed, otherwise returns the original array.
    /// </summary>
    public static float[] ClampSamples(float[] samples)
    {
        bool needsClamp = false;
        for (int i = 0; i < samples.Length; i++)
        {
            if (float.IsNaN(samples[i]) || float.IsInfinity(samples[i]) ||
                samples[i] > 1.0f || samples[i] < -1.0f)
            {
                needsClamp = true;
                break;
            }
        }

        if (!needsClamp)
            return samples;

        var clamped = new float[samples.Length];
        for (int i = 0; i < samples.Length; i++)
        {
            float s = samples[i];
            if (float.IsNaN(s) || float.IsInfinity(s))
                clamped[i] = 0f;
            else
                clamped[i] = Math.Clamp(s, -1.0f, 1.0f);
        }
        return clamped;
    }
}
