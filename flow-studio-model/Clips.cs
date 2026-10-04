namespace Flow.Studio.Model;

/// <summary>A linked score source window. Bounds need not fit the current source:
/// shortened regeneration leaves the missing portion silent rather than moving it.</summary>
public sealed record ScoreClip
{
    public Guid Id { get; }
    public Guid TrackId { get; }
    public Guid SourceId { get; }
    public string LayerId { get; }
    public double AnchorQuarters { get; }
    public double SourceOffsetQuarters { get; }
    public double LengthQuarters { get; }
    public TimeOffset Nudge { get; }

    public ScoreClip(Guid id, Guid trackId, Guid sourceId, string layerId, double anchorQuarters,
        double sourceOffsetQuarters, double lengthQuarters, TimeOffset nudge = default)
    {
        Id = Validate.Id(id, nameof(id));
        TrackId = Validate.Id(trackId, nameof(trackId));
        SourceId = Validate.Id(sourceId, nameof(sourceId));
        ArgumentException.ThrowIfNullOrWhiteSpace(layerId);
        LayerId = layerId;
        AnchorQuarters = Validate.Nonnegative(anchorQuarters, nameof(anchorQuarters));
        SourceOffsetQuarters = Validate.Nonnegative(sourceOffsetQuarters, nameof(sourceOffsetQuarters));
        LengthQuarters = Validate.Positive(lengthQuarters, nameof(lengthQuarters));
        Validate.Finite(anchorQuarters + lengthQuarters, nameof(lengthQuarters));
        Validate.Finite(sourceOffsetQuarters + lengthQuarters, nameof(lengthQuarters));
        Nudge = nudge;
    }

    public double SecondsAtSourceQuarter(double sourceQuarter, ProjectTempoMap tempo) =>
        Validate.Finite(tempo.SecondsAt(AnchorQuarters + Validate.Finite(sourceQuarter, nameof(sourceQuarter)) - SourceOffsetQuarters) + Nudge.Seconds, nameof(sourceQuarter));
}

/// <summary>Audio keeps source-frame duration; moving its musical anchor never stretches samples.</summary>
public sealed record AudioClip
{
    public Guid Id { get; }
    public Guid TrackId { get; }
    public Guid SourceId { get; }
    public double AnchorQuarters { get; }
    public long SourceOffsetFrames { get; }
    public long LengthFrames { get; }
    public int SampleRate { get; }
    public TimeOffset Nudge { get; }
    public Flow.Music.Model.AudioClipEnvelope? Envelope { get; }

    public AudioClip(Guid id, Guid trackId, Guid sourceId, double anchorQuarters,
        long sourceOffsetFrames, long lengthFrames, int sampleRate, TimeOffset nudge = default, Flow.Music.Model.AudioClipEnvelope? envelope = null)
    {
        Id = Validate.Id(id, nameof(id));
        TrackId = Validate.Id(trackId, nameof(trackId));
        SourceId = Validate.Id(sourceId, nameof(sourceId));
        AnchorQuarters = Validate.Nonnegative(anchorQuarters, nameof(anchorQuarters));
        ArgumentOutOfRangeException.ThrowIfNegative(sourceOffsetFrames);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lengthFrames);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        _ = checked(sourceOffsetFrames + lengthFrames);
        SourceOffsetFrames = sourceOffsetFrames;
        LengthFrames = lengthFrames;
        SampleRate = sampleRate;
        Nudge = nudge; Envelope = envelope;
    }

    public double StartSeconds(ProjectTempoMap tempo) => Validate.Finite(tempo.SecondsAt(AnchorQuarters) + Nudge.Seconds, nameof(tempo));
}

public static class ClipOperations
{
    /// <summary>Move the visible left edge by signed source quarters and set its
    /// new length. Surviving source events retain project times; negative deltas
    /// extend left only when both source offset and project anchor stay nonnegative.</summary>
    public static ScoreClip Trim(ScoreClip clip, double startDeltaQuarters, double lengthQuarters) =>
        new(clip.Id, clip.TrackId, clip.SourceId, clip.LayerId,
            clip.AnchorQuarters + Validate.Finite(startDeltaQuarters, nameof(startDeltaQuarters)),
            clip.SourceOffsetQuarters + startDeltaQuarters, lengthQuarters, clip.Nudge);

    /// <summary>Audio edge edits preserve sample duration and resolve their new
    /// anchor through the project tempo map. Nudge is retained, never baked twice.</summary>
    public static AudioClip TrimFrames(AudioClip clip, long startDeltaFrames, long lengthFrames, ProjectTempoMap tempo) =>
        new(clip.Id, clip.TrackId, clip.SourceId,
            startDeltaFrames == 0 ? clip.AnchorQuarters : tempo.QuarterAt(tempo.SecondsAt(clip.AnchorQuarters) + (double)startDeltaFrames / clip.SampleRate),
            checked(clip.SourceOffsetFrames + startDeltaFrames), lengthFrames, clip.SampleRate, clip.Nudge, clip.Envelope);

    /// <summary>Additional linked occurrences of the current visible window.
    /// IDs are supplied/captured by the caller; the original is not returned.</summary>
    public static IReadOnlyList<ScoreClip> Repeat(ScoreClip clip, IEnumerable<Guid> copyIds)
    {
        var ids = RepeatIds(clip.Id, copyIds);
        return Array.AsReadOnly(ids.Select((id, index) => new ScoreClip(id, clip.TrackId, clip.SourceId, clip.LayerId,
            clip.AnchorQuarters + (index + 1) * clip.LengthQuarters, clip.SourceOffsetQuarters, clip.LengthQuarters, clip.Nudge)).ToArray());
    }
    public static IReadOnlyList<AudioClip> Repeat(AudioClip clip, IEnumerable<Guid> copyIds, ProjectTempoMap tempo)
    {
        var ids = RepeatIds(clip.Id, copyIds);
        double start = tempo.SecondsAt(clip.AnchorQuarters);
        return Array.AsReadOnly(ids.Select((id, index) => new AudioClip(id, clip.TrackId, clip.SourceId,
            tempo.QuarterAt(start + (index + 1) * ((double)clip.LengthFrames / clip.SampleRate)),
            clip.SourceOffsetFrames, clip.LengthFrames, clip.SampleRate, clip.Nudge, clip.Envelope)).ToArray());
    }
    private static Guid[] RepeatIds(Guid original, IEnumerable<Guid> copyIds)
    {
        ArgumentNullException.ThrowIfNull(copyIds);
        var ids = copyIds.Take(100000).ToArray();
        if (ids.Length is < 1 or > 99999 || ids.Any(id => id == Guid.Empty || id == original) || ids.Distinct().Count() != ids.Length)
            throw new ArgumentException("Repeat requires 1–99,999 distinct new clip IDs");
        return ids;
    }
    public static ScoreClip AlignToBar(ScoreClip clip, int bar, ProjectMeterMap meter) =>
        Score(clip, anchor: meter.QuarterAtBar(bar), nudge: default(TimeOffset));
    public static ScoreClip SetOffsetMs(ScoreClip clip, TimeOffset offset) => Score(clip, nudge: offset);
    public static ScoreClip RelativeOffsetMs(ScoreClip clip, TimeOffset offset) =>
        SetOffsetMs(clip, new(clip.Nudge.Milliseconds + offset.Milliseconds));
    public static ScoreClip MoveByQuarters(ScoreClip clip, double quarters) =>
        Score(clip, anchor: clip.AnchorQuarters + Validate.Finite(quarters, nameof(quarters)));

    public static (ScoreClip Left, ScoreClip Right) Split(ScoreClip clip, MusicalDuration point, Guid rightId)
    {
        ValidateSplit(point.Quarters, clip.LengthQuarters, clip.Id, rightId);
        return (Score(clip, length: point.Quarters),
            new(rightId, clip.TrackId, clip.SourceId, clip.LayerId, clip.AnchorQuarters + point.Quarters,
                clip.SourceOffsetQuarters + point.Quarters, clip.LengthQuarters - point.Quarters, clip.Nudge));
    }

    public static (ScoreClip Left, ScoreClip Right) SplitAtProjectSeconds(ScoreClip clip, double seconds,
        ProjectTempoMap tempo, Guid rightId) => Split(clip,
            new(tempo.QuarterAt(Validate.Finite(seconds, nameof(seconds)) - clip.Nudge.Seconds) - clip.AnchorQuarters), rightId);

    public static AudioClip AlignToBar(AudioClip clip, int bar, ProjectMeterMap meter) =>
        Audio(clip, anchor: meter.QuarterAtBar(bar), nudge: default(TimeOffset));
    public static AudioClip SetOffsetMs(AudioClip clip, TimeOffset offset) => Audio(clip, nudge: offset);
    public static AudioClip RelativeOffsetMs(AudioClip clip, TimeOffset offset) =>
        SetOffsetMs(clip, new(clip.Nudge.Milliseconds + offset.Milliseconds));
    public static AudioClip MoveByQuarters(AudioClip clip, double quarters) =>
        Audio(clip, anchor: clip.AnchorQuarters + Validate.Finite(quarters, nameof(quarters)));

    /// <summary>Exact local source-frame cut. Nudge is kept on both pieces. The right
    /// musical anchor resolves through the full tempo map, not a single BPM.</summary>
    public static (AudioClip Left, AudioClip Right) SplitFrames(AudioClip clip, long frame,
        ProjectTempoMap tempo, Guid rightId)
    {
        if (frame <= 0 || frame >= clip.LengthFrames) throw new ArgumentOutOfRangeException(nameof(frame));
        ValidateSplitId(clip.Id, rightId);
        double anchor = tempo.QuarterAt(tempo.SecondsAt(clip.AnchorQuarters) + (double)frame / clip.SampleRate);
        return (Audio(clip, length: frame),
            new(rightId, clip.TrackId, clip.SourceId, anchor, checked(clip.SourceOffsetFrames + frame),
                clip.LengthFrames - frame, clip.SampleRate, clip.Nudge, clip.Envelope));
    }

    /// <summary>Ruler cuts snap to the nearest source frame, ties away from zero.</summary>
    public static (AudioClip Left, AudioClip Right) SplitAtProjectSeconds(AudioClip clip, double seconds,
        ProjectTempoMap tempo, Guid rightId)
    {
        double frame = Math.Round((Validate.Finite(seconds, nameof(seconds)) - clip.StartSeconds(tempo)) * clip.SampleRate,
            MidpointRounding.AwayFromZero);
        return SplitFrames(clip, checked((long)frame), tempo, rightId);
    }

    private static void ValidateSplit(double point, double length, Guid left, Guid right)
    {
        if (!double.IsFinite(point) || point <= 0 || point >= length) throw new ArgumentOutOfRangeException(nameof(point));
        ValidateSplitId(left, right);
    }
    private static void ValidateSplitId(Guid left, Guid right)
    {
        Validate.Id(right, nameof(right));
        if (left == right) throw new ArgumentException("Split pieces require distinct IDs", nameof(right));
    }
    private static ScoreClip Score(ScoreClip clip, double? anchor = null, double? length = null, TimeOffset? nudge = null) =>
        new(clip.Id, clip.TrackId, clip.SourceId, clip.LayerId, anchor ?? clip.AnchorQuarters,
            clip.SourceOffsetQuarters, length ?? clip.LengthQuarters, nudge ?? clip.Nudge);
    private static AudioClip Audio(AudioClip clip, double? anchor = null, long? length = null, TimeOffset? nudge = null) =>
        new(clip.Id, clip.TrackId, clip.SourceId, anchor ?? clip.AnchorQuarters,
            clip.SourceOffsetFrames, length ?? clip.LengthFrames, clip.SampleRate, nudge ?? clip.Nudge, clip.Envelope);
}
