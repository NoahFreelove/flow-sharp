namespace FlowLang.StandardLibrary.Audio;

/// <summary>
/// Manages polyphonic voice allocation with a configurable maximum voice count.
/// When the voice limit is exceeded, the quietest voices are dropped to prevent
/// clipping and excessive resource usage in dense polyphonic passages.
/// </summary>
public static class VoiceAllocator
{
    /// <summary>
    /// Allocates voices from a list, enforcing the voice limit.
    /// If the list exceeds maxVoices, the quietest voices are dropped.
    /// </summary>
    /// <param name="voices">List of voices to allocate.</param>
    /// <param name="sampleRate">Sample rate for fade calculations.</param>
    /// <param name="maxVoices">Maximum number of voices to keep.</param>
    /// <returns>A list containing at most maxVoices voices, keeping the loudest.</returns>
    public static List<Voice> Allocate(List<Voice> voices, int sampleRate, int maxVoices)
    {
        if (voices.Count <= maxVoices)
            return voices;

        // Sort by peak amplitude descending — keep the loudest voices
        var sorted = voices
            .Select(v => (voice: v, peak: GetPeakAmplitude(v)))
            .OrderByDescending(x => x.peak)
            .ToList();

        // Keep the loudest maxVoices voices
        var kept = sorted.Take(maxVoices).Select(x => x.voice).ToList();

        // Apply fade-out to stolen voices for clean removal
        // (safety measure in case they were partially mixed elsewhere)
        for (int i = maxVoices; i < sorted.Count; i++)
        {
            ApplyFadeOut(sorted[i].voice, sampleRate);
        }

        return kept;
    }

    /// <summary>
    /// Computes the peak amplitude of a voice, scaled by its gain.
    /// Samples up to 1 second of audio for efficiency.
    /// </summary>
    private static float GetPeakAmplitude(Voice voice)
    {
        float peak = 0f;
        int maxFrames = Math.Min(voice.Buffer.Frames, voice.Buffer.SampleRate);

        for (int i = 0; i < maxFrames; i++)
        {
            for (int ch = 0; ch < voice.Buffer.Channels; ch++)
            {
                float abs = Math.Abs(voice.Buffer.GetSample(i, ch));
                if (abs > peak) peak = abs;
            }
        }

        return peak * (float)voice.Gain;
    }

    /// <summary>
    /// Applies a 5ms fade-out to the end of a voice's buffer to prevent click artifacts.
    /// Used on stolen voices before they are removed from the mix.
    /// </summary>
    private static void ApplyFadeOut(Voice voice, int sampleRate)
    {
        int fadeSamples = (int)(0.005 * sampleRate); // 5ms
        fadeSamples = Math.Min(fadeSamples, voice.Buffer.Frames);

        for (int i = 0; i < fadeSamples; i++)
        {
            float fadeGain = 1.0f - ((float)i / fadeSamples);
            int frame = voice.Buffer.Frames - fadeSamples + i;
            if (frame < 0) continue;

            for (int ch = 0; ch < voice.Buffer.Channels; ch++)
            {
                float sample = voice.Buffer.GetSample(frame, ch);
                voice.Buffer.SetSample(frame, ch, sample * fadeGain);
            }
        }
    }
}
