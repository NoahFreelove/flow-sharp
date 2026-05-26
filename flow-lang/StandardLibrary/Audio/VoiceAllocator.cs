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
    ///
    /// <para>
    /// Phase 38 Plan 38-03 LIVE-03 (RESEARCH §B line 685): consumed by the
    /// live-block swap path (<c>LiveReloadManager.PreserveVoiceState</c>) for
    /// voices in the DiffByVoiceName "Dropped" set — voices whose Name is no
    /// longer present in the new render need a clean tail before being
    /// released from the mix. Exposing this as <c>public</c> (was
    /// <c>private</c> through Phase 37) lets the cross-assembly live consumer
    /// reuse the same 5ms primitive rather than duplicating the fade math.
    /// Phase 28 in-class callers (<see cref="Allocate"/>) continue to work
    /// unchanged.
    /// </para>
    /// </summary>
    public static void ApplyFadeOut(Voice voice, int sampleRate)
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
    /// Phase 38 Plan 38-03 LIVE-03 (RESEARCH §B lines 662-684) — partitions
    /// <paramref name="prev"/> and <paramref name="next"/> by stable
    /// <see cref="Voice.Name"/> into three lists for the live-block swap path:
    ///
    /// <list type="bullet">
    ///   <item><description><b>Preserved</b> — names appearing in BOTH prev and
    ///   next. Uses the <i>new</i> voice instances (per RESEARCH §B line 675
    ///   "preserved.Add(newVoice)"), so the swap consumer's CopyStateFrom call
    ///   mutates the freshly rendered voice in place: the new ADSR envelope
    ///   shape is preserved while the previous OffsetBeats cursor is
    ///   transferred, eliminating envelope retrigger clicks on save.</description></item>
    ///   <item><description><b>Dropped</b> — names in prev but not next. The
    ///   swap consumer fades these out via
    ///   <see cref="ApplyFadeOut"/>.</description></item>
    ///   <item><description><b>Added</b> — names in next but not prev. Mixed
    ///   in fresh on the next bar boundary; nothing to preserve.</description></item>
    /// </list>
    ///
    /// <para>
    /// Name comparison uses <see cref="StringComparer.Ordinal"/> for
    /// deterministic case-sensitive matching. Voices with empty Name (legacy
    /// offline-render path) are NOT eligible for preservation — they fall
    /// into the "no-key" bucket and are treated as if absent from both prev
    /// and next. This keeps Phase 28 byte-identical determinism intact for
    /// every offline code path (<c>writeWav</c> / <c>writeMidi</c>) where the
    /// SongRenderer doesn't tag voices with Name.
    /// </para>
    ///
    /// <para>
    /// Threat T-38-VOI mitigation: collisions on the <c>"{instrument}:{ordinal}"</c>
    /// format are bounded by the SongRenderer's allocation loop — the ordinal
    /// is monotonic per (instrument, render) pair so within a single render
    /// the Name is unique. Across re-renders the same instrument's ordinal
    /// counts in source order, so identical input source produces identical
    /// Name sets (Phase 28 deterministic-onset contract).
    /// </para>
    /// </summary>
    /// <param name="prev">Voices from the previous render. May be empty (cold
    /// start) — all next voices become Added.</param>
    /// <param name="next">Voices from the freshly rendered next pass. May be
    /// empty (composer removed all sequences) — all prev voices become Dropped.</param>
    /// <returns>Three-tuple (Preserved, Dropped, Added) with the ownership
    /// rules above.</returns>
    public static (List<Voice> Preserved, List<Voice> Dropped, List<Voice> Added)
        DiffByVoiceName(IReadOnlyList<Voice> prev, IReadOnlyList<Voice> next)
    {
        if (prev == null) throw new ArgumentNullException(nameof(prev));
        if (next == null) throw new ArgumentNullException(nameof(next));

        // Build name → voice maps. Voices with empty Name are excluded from
        // the preservation eligibility set entirely (legacy offline path
        // preservation).
        var prevByName = new Dictionary<string, Voice>(StringComparer.Ordinal);
        for (int i = 0; i < prev.Count; i++)
        {
            var v = prev[i];
            if (!string.IsNullOrEmpty(v.Name))
                prevByName[v.Name] = v;
        }
        var nextByName = new Dictionary<string, Voice>(StringComparer.Ordinal);
        for (int i = 0; i < next.Count; i++)
        {
            var v = next[i];
            if (!string.IsNullOrEmpty(v.Name))
                nextByName[v.Name] = v;
        }

        var preserved = new List<Voice>();
        var added = new List<Voice>();
        var dropped = new List<Voice>();

        // Walk next in input order — Preserved keeps the new instances; Added
        // gets next-side voices whose name isn't in prev. Voices with empty
        // Name are treated as Added (they can't be preserved across a swap
        // because they have no stable key).
        for (int i = 0; i < next.Count; i++)
        {
            var v = next[i];
            if (string.IsNullOrEmpty(v.Name) || !prevByName.ContainsKey(v.Name))
                added.Add(v);
            else
                preserved.Add(v);
        }

        // Walk prev in input order — Dropped gets prev-side voices whose name
        // isn't in next. Voices with empty Name are also treated as Dropped
        // (no preservation possible, but the swap path may still want to fade
        // them out cleanly).
        for (int i = 0; i < prev.Count; i++)
        {
            var v = prev[i];
            if (string.IsNullOrEmpty(v.Name) || !nextByName.ContainsKey(v.Name))
                dropped.Add(v);
        }

        return (preserved, dropped, added);
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
