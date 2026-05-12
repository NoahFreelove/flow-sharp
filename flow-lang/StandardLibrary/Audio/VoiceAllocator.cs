namespace FlowLang.StandardLibrary.Audio;

/// <summary>
/// Manages polyphonic voice allocation with a configurable maximum voice count.
/// When the voice limit is exceeded, the quietest voices are dropped to prevent
/// clipping and excessive resource usage in dense polyphonic passages.
/// </summary>
public static class VoiceAllocator
{
    /// <summary>
    /// Phase 28 SPEC-7 test instrumentation: records the pool size most recently
    /// passed to <see cref="AllocateWithPool"/>. Tests read this to verify that
    /// the SequenceRenderer wired the resolved pool size through (default 32 vs
    /// composer-supplied via <c>voicePool N { ... }</c>). Production code never
    /// reads this; it's set unconditionally because the overhead is one
    /// AsyncLocal assignment per allocation.
    ///
    /// Backed by <see cref="AsyncLocal{T}"/> so xUnit's parallel test execution
    /// across classes doesn't race the value — each test's logical execution
    /// flow sees only the pool size it triggered, regardless of what other
    /// concurrent tests are rendering.
    /// </summary>
    private static readonly AsyncLocal<int?> _lastPoolSizeUsedForTests = new();
    public static int? LastPoolSizeUsedForTests
    {
        get => _lastPoolSizeUsedForTests.Value;
        set => _lastPoolSizeUsedForTests.Value = value;
    }

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

    /// <summary>
    /// Phase 28 SPEC-7: voice-pool allocation with steal-oldest policy. Walks
    /// voices in onset order (sorted by <see cref="Voice.OffsetBeats"/>); maintains
    /// an active set bounded by <paramref name="poolSize"/>. When a new voice
    /// would exceed the pool, the active voice with the EARLIEST onset (= "oldest"
    /// — has been audible longest) is truncated at the new voice's onset and
    /// removed from the active set. Tiebreaker for equal onsets: original input
    /// index (deterministic across runs).
    ///
    /// Range: 1..256. Out-of-range raises <see cref="ArgumentOutOfRangeException"/>.
    ///
    /// Returns the original <paramref name="voices"/> list (in original order) —
    /// stolen voices are mutated in place via <see cref="TruncateVoiceBuffer"/>
    /// (their tails zeroed with a 5ms fade) so the downstream
    /// <see cref="SongRenderer"/> mix sums them correctly. Per Phase 18/25/27
    /// two-run determinism contract, the deterministic onset+index sort
    /// guarantees byte-identical output across consecutive runs.
    /// </summary>
    public static List<Voice> AllocateWithPool(List<Voice> voices, int sampleRate, int poolSize, double bpm)
    {
        if (poolSize < 1 || poolSize > 256)
            throw new ArgumentOutOfRangeException(nameof(poolSize),
                $"Voice pool size must be in [1, 256], got {poolSize}");

        // Test instrumentation — records the resolved pool size for VoicePoolTests.
        // Set BEFORE the early-return so tests can verify the size even when no
        // voices need stealing.
        LastPoolSizeUsedForTests = poolSize;

        if (voices.Count <= poolSize) return voices;

        double secondsPerBeat = 60.0 / bpm;

        // Sort by onset (deterministic: ThenBy original index for equal onsets)
        var ordered = voices
            .Select((v, i) => (voice: v, idx: i, onsetSec: v.OffsetBeats * secondsPerBeat))
            .OrderBy(x => x.onsetSec).ThenBy(x => x.idx)
            .ToList();

        // Active set: voices whose buffers haven't ended yet at the current
        // iteration's onset. (voice, idx, onsetSec, endSec).
        var active = new List<(Voice voice, int idx, double onsetSec, double endSec)>();

        foreach (var entry in ordered)
        {
            // Drop voices that ended before this onset
            active.RemoveAll(a => a.endSec <= entry.onsetSec);

            if (active.Count >= poolSize)
            {
                // Steal oldest: smallest onsetSec, then smallest idx
                var oldest = active.OrderBy(a => a.onsetSec).ThenBy(a => a.idx).First();
                int truncFrames = Math.Max(0, (int)((entry.onsetSec - oldest.onsetSec) * sampleRate));
                TruncateVoiceBuffer(oldest.voice, truncFrames, sampleRate);
                active.Remove(oldest);
            }

            double durSec = (double)entry.voice.Buffer.Frames / sampleRate;
            active.Add((entry.voice, entry.idx, entry.onsetSec, entry.onsetSec + durSec));
        }

        // Preserve original ordering for downstream consumers.
        return voices;
    }

    /// <summary>
    /// Truncates a voice's buffer to <paramref name="newFrameCount"/> samples,
    /// applying a 5ms fade-out at the new end to prevent click artifacts. Frames
    /// beyond <paramref name="newFrameCount"/> are zeroed. Mirrors the existing
    /// <see cref="ApplyFadeOut"/> pattern but operates at an arbitrary truncation
    /// point inside the buffer instead of always at the buffer's natural end.
    /// </summary>
    private static void TruncateVoiceBuffer(Voice voice, int newFrameCount, int sampleRate)
    {
        int total = voice.Buffer.Frames;
        if (newFrameCount >= total) return;
        if (newFrameCount < 0) newFrameCount = 0;

        int fadeSamples = Math.Min((int)(0.005 * sampleRate), newFrameCount);
        for (int i = 0; i < fadeSamples; i++)
        {
            int frame = newFrameCount - fadeSamples + i;
            if (frame < 0) continue;
            float fadeGain = 1.0f - ((float)i / fadeSamples);
            for (int ch = 0; ch < voice.Buffer.Channels; ch++)
                voice.Buffer.SetSample(frame, ch, voice.Buffer.GetSample(frame, ch) * fadeGain);
        }
        for (int frame = newFrameCount; frame < total; frame++)
            for (int ch = 0; ch < voice.Buffer.Channels; ch++)
                voice.Buffer.SetSample(frame, ch, 0f);
    }
}
