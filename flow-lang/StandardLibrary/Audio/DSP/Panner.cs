namespace FlowLang.StandardLibrary.Audio.DSP;

/// <summary>
/// Constant-power stereo panner using cos/sin pan law.
/// Always produces stereo output — mono inputs are promoted to stereo.
/// All processing returns new buffers — inputs are never modified.
/// </summary>
public static class Panner
{
    /// <summary>
    /// Applies constant-power stereo panning to an audio buffer.
    /// </summary>
    /// <param name="input">Source audio buffer (not modified).</param>
    /// <param name="pan">Pan position: -1.0 = hard left, 0.0 = center, 1.0 = hard right.</param>
    /// <returns>A new stereo buffer with panning applied. Mono inputs are promoted to stereo.</returns>
    public static AudioBuffer Apply(AudioBuffer input, float pan)
    {
        // Clamp pan to [-1.0, 1.0]
        pan = Math.Clamp(pan, -1f, 1f);

        // Map pan from [-1, 1] to [0, PI/2] for constant-power law
        float angle = (pan + 1f) * 0.25f * MathF.PI;
        float leftGain = MathF.Cos(angle);
        float rightGain = MathF.Sin(angle);

        // Always create stereo output (mono promoted to stereo)
        var result = new AudioBuffer(input.Frames, 2, input.SampleRate);

        for (int frame = 0; frame < input.Frames; frame++)
        {
            // Get mono sample from input (downmix if stereo)
            float mono;
            if (input.Channels == 1)
            {
                mono = input.GetSample(frame, 0);
            }
            else
            {
                mono = 0f;
                for (int ch = 0; ch < input.Channels; ch++)
                    mono += input.GetSample(frame, ch);
                mono /= input.Channels;
            }

            result.SetSample(frame, 0, mono * leftGain);
            result.SetSample(frame, 1, mono * rightGain);
        }

        return result;
    }
}
