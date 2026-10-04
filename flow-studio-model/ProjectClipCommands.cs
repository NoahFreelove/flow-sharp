namespace Flow.Studio.Model;

/// <summary>Captured project gestures. Duplicates/splits share sources; deletion
/// retains source/assets for reuse and undo. All selections validate before commit.</summary>
public static class ProjectClipCommands
{
    public static void SetOffset(ProjectDocument document, IEnumerable<Guid> ids, TimeOffset offset) => Offset(document, ids, offset, false);
    public static void RelativeOffset(ProjectDocument document, IEnumerable<Guid> ids, TimeOffset offset) => Offset(document, ids, offset, true);
    private static void Offset(ProjectDocument document, IEnumerable<Guid> ids, TimeOffset offset, bool relative)
    {
        var selected = ids.Take(100001).ToArray();
        document.Edit(relative ? "Nudge clips" : "Set clip offset", p =>
        {
            var set = Selection(p, selected);
            var scores = p.Arrangement.ScoreClips.Select(c => !set.Contains(c.Id) ? c : relative ? ClipOperations.RelativeOffsetMs(c, offset) : ClipOperations.SetOffsetMs(c, offset)).ToArray();
            var audio = p.Arrangement.AudioClips.Select(c => !set.Contains(c.Id) ? c : relative ? ClipOperations.RelativeOffsetMs(c, offset) : ClipOperations.SetOffsetMs(c, offset)).ToArray();
            return scores.SequenceEqual(p.Arrangement.ScoreClips) && audio.SequenceEqual(p.Arrangement.AudioClips) ? p :
                WithArrangement(p, new(p.Arrangement.Id, p.Arrangement.Tempo, p.Arrangement.Meter, scores, audio));
        });
    }
    public static void TrimScore(ProjectDocument document, Guid clipId, double startDeltaQuarters, double lengthQuarters) =>
        document.Edit("Trim note clip", p =>
        {
            var clip = p.Arrangement.ScoreClips.SingleOrDefault(c => c.Id == clipId) ?? throw new ArgumentException("Unknown score clip");
            var trimmed = ClipOperations.Trim(clip, startDeltaQuarters, lengthQuarters);
            return trimmed == clip ? p : WithArrangement(p, new(p.Arrangement.Id, p.Arrangement.Tempo, p.Arrangement.Meter,
                p.Arrangement.ScoreClips.Select(c => c.Id == clipId ? trimmed : c), p.Arrangement.AudioClips));
        });
    public static void TrimAudio(ProjectDocument document, Guid clipId, long startDeltaFrames, long lengthFrames) =>
        document.Edit("Trim audio clip", p =>
        {
            var clip = p.Arrangement.AudioClips.SingleOrDefault(c => c.Id == clipId) ?? throw new ArgumentException("Unknown audio clip");
            // Avoid a tempo roundtrip changing an otherwise unchanged anchor.
            if (startDeltaFrames == 0 && lengthFrames == clip.LengthFrames) return p;
            var trimmed = ClipOperations.TrimFrames(clip, startDeltaFrames, lengthFrames, p.Arrangement.Tempo);
            return WithArrangement(p, new(p.Arrangement.Id, p.Arrangement.Tempo, p.Arrangement.Meter,
                p.Arrangement.ScoreClips, p.Arrangement.AudioClips.Select(c => c.Id == clipId ? trimmed : c)));
        });
    public static void Repeat(ProjectDocument document, Guid clipId, IEnumerable<Guid> copyIds)
    {
        ArgumentNullException.ThrowIfNull(copyIds);
        var ids = copyIds.Take(100000).ToArray();
        document.Edit("Repeat clip", p =>
        {
            if (p.Arrangement.ScoreClips.SingleOrDefault(c => c.Id == clipId) is { } score)
                return WithArrangement(p, new(p.Arrangement.Id, p.Arrangement.Tempo, p.Arrangement.Meter,
                    p.Arrangement.ScoreClips.Concat(ClipOperations.Repeat(score, ids)), p.Arrangement.AudioClips));
            var audio = p.Arrangement.AudioClips.SingleOrDefault(c => c.Id == clipId) ?? throw new ArgumentException("Unknown clip");
            return WithArrangement(p, new(p.Arrangement.Id, p.Arrangement.Tempo, p.Arrangement.Meter,
                p.Arrangement.ScoreClips, p.Arrangement.AudioClips.Concat(ClipOperations.Repeat(audio, ids, p.Arrangement.Tempo))));
        });
    }
    public static void Move(ProjectDocument document, IEnumerable<Guid> ids, double quarters)
    {
        var selected = ids.Take(100001).ToArray();
        document.Edit("Move clips", p =>
        {
            var set = Selection(p, selected);
            return WithArrangement(p, new(p.Arrangement.Id, p.Arrangement.Tempo, p.Arrangement.Meter,
                p.Arrangement.ScoreClips.Select(c => set.Contains(c.Id) ? ClipOperations.MoveByQuarters(c, quarters) : c),
                p.Arrangement.AudioClips.Select(c => set.Contains(c.Id) ? ClipOperations.MoveByQuarters(c, quarters) : c)));
        });
    }
    public static void Duplicate(ProjectDocument document, IReadOnlyDictionary<Guid, Guid> copies, double quarters)
    {
        var captured = new Dictionary<Guid, Guid>(copies);
        document.Edit("Duplicate clips", p =>
        {
            var selected = Selection(p, captured.Keys.ToArray());
            var scores = p.Arrangement.ScoreClips.Where(c => selected.Contains(c.Id)).Select(c =>
                new ScoreClip(captured[c.Id], c.TrackId, c.SourceId, c.LayerId, c.AnchorQuarters + quarters, c.SourceOffsetQuarters, c.LengthQuarters, c.Nudge));
            var audio = p.Arrangement.AudioClips.Where(c => selected.Contains(c.Id)).Select(c =>
                new AudioClip(captured[c.Id], c.TrackId, c.SourceId, c.AnchorQuarters + quarters, c.SourceOffsetFrames, c.LengthFrames, c.SampleRate, c.Nudge, c.Envelope));
            return WithArrangement(p, new(p.Arrangement.Id, p.Arrangement.Tempo, p.Arrangement.Meter,
                p.Arrangement.ScoreClips.Concat(scores), p.Arrangement.AudioClips.Concat(audio)));
        });
    }
    public static void Delete(ProjectDocument document, IEnumerable<Guid> ids)
    {
        var selected = ids.Take(100001).ToArray();
        document.Edit("Delete clips", p =>
        {
            var set = Selection(p, selected);
            return WithArrangement(p, new(p.Arrangement.Id, p.Arrangement.Tempo, p.Arrangement.Meter,
                p.Arrangement.ScoreClips.Where(c => !set.Contains(c.Id)), p.Arrangement.AudioClips.Where(c => !set.Contains(c.Id))));
        });
    }
    public static void SplitScore(ProjectDocument document, Guid clipId, MusicalDuration point, Guid rightId) =>
        document.Edit("Split note clip", p => WithArrangement(p, p.Arrangement.SplitScore(clipId, point, rightId)));
    public static void SplitAudio(ProjectDocument document, Guid clipId, long sourceFrame, Guid rightId) =>
        document.Edit("Split audio clip", p =>
        {
            var clip = p.Arrangement.AudioClips.SingleOrDefault(c => c.Id == clipId) ?? throw new ArgumentException("Unknown audio clip");
            var split = ClipOperations.SplitFrames(clip, sourceFrame, p.Arrangement.Tempo, rightId);
            return WithArrangement(p, new(p.Arrangement.Id, p.Arrangement.Tempo, p.Arrangement.Meter, p.Arrangement.ScoreClips,
                p.Arrangement.AudioClips.SelectMany(c => c.Id == clipId ? new[] { split.Left, split.Right } : new[] { c })));
        });
    private static HashSet<Guid> Selection(ProjectSnapshot p, Guid[] ids)
    {
        if (ids.Length is < 1 or > 100000) throw new ArgumentException("Invalid clip selection size");
        var set = ids.ToHashSet();
        var existing = p.Arrangement.ScoreClips.Select(c => c.Id).Concat(p.Arrangement.AudioClips.Select(c => c.Id)).ToHashSet();
        if (set.Count != ids.Length || !set.IsSubsetOf(existing)) throw new ArgumentException("Duplicate or unknown selected clips");
        return set;
    }
    private static ProjectSnapshot WithArrangement(ProjectSnapshot p, ArrangementSnapshot arrangement) =>
        new(arrangement, p.Context, p.Sources.Values, p.Routing, p.Assets, p.Automation, p.RenderSettings);
}
