using Flow.Music.Model;

namespace Flow.Audio;

/// <summary>
/// Prepared dry sine score with an independent, frame-addressed playback cursor.
/// Prepare on a worker; Read/Seek/Reset must be serialized by the owning host.
/// Valid Read/Seek/Reset calls allocate no managed memory and perform no IO.
/// This is a bounded prototype, not a device-deadline guarantee: each block scans
/// the current section's prepared voices in source order to preserve sample bits.
/// </summary>
public sealed class PreparedSinePlayback : IPreparedAudioPlayback
{
    private sealed record Section(long Frames, SineCompositionRenderer.Voice[] Voices,
        float Left, float Right);
    private sealed record Placement(long Start, long End, Section Section);

    private readonly Placement[] _placements;
    public int SampleRate { get; }
    public int MaxBlockFrames { get; }
    public long TotalFrames { get; }
    public long PositionFrames { get; private set; }

    private PreparedSinePlayback(Placement[] placements, RenderOptions options, long total)
    {
        _placements = placements;
        SampleRate = options.SampleRate;
        MaxBlockFrames = options.BlockFrames;
        TotalFrames = total;
    }

    /// <summary>
    /// Validates and prepares the entire score before publication. Repeated placements
    /// share section metadata; no PCM is cached and repeats are never expanded.
    /// maxPreparedNotes counts authored notes/rests across distinct section objects.
    /// maxPlacements bounds input placements, including empty ones. Cancellation or
    /// validation failure publishes nothing. Output retains no interpreter objects.
    /// </summary>
    public static PreparedSinePlayback Prepare(CompositionSnapshot composition,
        RenderOptions? options = null, int maxPreparedNotes = 1_000_000,
        int maxPlacements = 10_000, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(composition);
        options ??= new();
        options.Validate();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPreparedNotes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPlacements);
        cancellation.ThrowIfCancellationRequested();
        if (composition.Placements.Count > maxPlacements)
            throw new InvalidOperationException("Score exceeds the prepared placement budget");

        // Check global metadata limits before allocating voice preparation storage.
        var seen = new HashSet<SectionSnapshot>(ReferenceEqualityComparer.Instance);
        long notes = 0;
        foreach (var placement in composition.Placements)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!seen.Add(placement.Section)) continue;
            foreach (var sequence in placement.Section.Sequences)
            {
                cancellation.ThrowIfCancellationRequested();
                notes += sequence.Notes.Count;
                if (notes > maxPreparedNotes)
                    throw new InvalidOperationException("Score exceeds the prepared note budget");
            }
        }
        long total = SineCompositionRenderer.GetFrameCount(composition, options, cancellation);
        var sections = new Dictionary<SectionSnapshot, Section>(ReferenceEqualityComparer.Instance);
        var placements = new List<Placement>();
        long offset = 0;
        foreach (var placement in composition.Placements)
        {
            cancellation.ThrowIfCancellationRequested();
            long frames = SineCompositionRenderer.SectionFrames(placement.Section, options.SampleRate);
            if (frames == 0 || placement.RepeatCount == 0) continue;
            if (!sections.TryGetValue(placement.Section, out var section))
            {
                var voices = SineCompositionRenderer.Prepare(placement.Section, options.SampleRate, cancellation).ToArray();
                float angle = (float)((placement.Section.Settings.Pan + 1.0) * 0.25 * Math.PI);
                float gain = (float)placement.Section.Settings.Gain;
                section = new(frames, voices, MathF.Cos(angle) * gain, MathF.Sin(angle) * gain);
                sections.Add(placement.Section, section);
            }
            long end = checked(offset + checked(frames * placement.RepeatCount));
            placements.Add(new(offset, end, section));
            offset = end;
        }
        cancellation.ThrowIfCancellationRequested();
        return new(placements.ToArray(), options, total);
    }

    /// <summary>
    /// Writes interleaved stereo into caller-owned memory, crossing section/repeat
    /// boundaries as needed. Returns score frames written and zeroes the remainder
    /// at end of score. Empty spans are allowed; odd lengths and blocks exceeding
    /// MaxBlockFrames fail before modifying output or cursor. No tail after score end.
    /// </summary>
    public int Read(Span<float> output)
    {
        if (output.Length % 2 != 0 || output.Length / 2 > MaxBlockFrames)
            throw new ArgumentException("Output must contain stereo frames within the prepared block limit", nameof(output));
        output.Clear();
        int written = 0;
        while (written < output.Length / 2 && PositionFrames < TotalFrames)
        {
            var placement = _placements[FindPlacement(PositionFrames)];
            var section = placement.Section;
            long local = (PositionFrames - placement.Start) % section.Frames;
            int count = (int)Math.Min(output.Length / 2 - written, section.Frames - local);
            var block = output.Slice(written * 2, count * 2);
            foreach (var voice in section.Voices)
                SineCompositionRenderer.MixVoice(voice, block, local, SampleRate, section.Left, section.Right);
            written += count;
            PositionFrames += count;
        }
        return written;
    }

    /// <summary>
    /// Exact dry-sine seek, including into held or stolen notes; no preroll needed.
    /// TotalFrames selects EOF. Invalid positions leave the cursor unchanged.
    /// Future stateful effects will need their own reset/preroll policy.
    /// </summary>
    public void Seek(long frame)
    {
        if (frame < 0 || frame > TotalFrames) throw new ArgumentOutOfRangeException(nameof(frame));
        PositionFrames = frame;
    }

    public void Reset() => PositionFrames = 0;

    private int FindPlacement(long frame)
    {
        int low = 0, high = _placements.Length - 1;
        while (low < high)
        {
            int middle = low + (high - low) / 2;
            if (frame >= _placements[middle].End) low = middle + 1;
            else high = middle;
        }
        return low;
    }
}
