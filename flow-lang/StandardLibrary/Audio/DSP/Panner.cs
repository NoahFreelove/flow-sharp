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
        Flow.Audio.Graph.StereoPanKernel.Gains(pan, out float leftGain, out float rightGain);

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
