using Flow.Music.Model;
using Flow.Music.Model.Editing;
namespace Flow.Studio.Model;

public sealed record ImportedNoteClip(Guid SourceId, Guid ClipId, Guid TrackId, double AnchorQuarters, string Name, PluginNoteInput Input);
public sealed record SequenceEdit(Guid SourceId, Guid SectionId, Guid SequenceId, Func<SequenceSnapshot, SequenceSnapshot> Apply);

public static class ProjectNoteCommands
{
    /// <summary>Install detached imported note windows as one captured action.
    /// Each window becomes independently editable; timing context is unchanged.</summary>
    public static void Import(ProjectDocument document, IEnumerable<ImportedNoteClip> clips)
    {
        var captured = clips.Take(4097).ToArray();
        if (captured.Length is < 1 or > 4096 || captured.Any(c => c is null || c.Input is null) ||
            captured.Sum(c => (long)c.Input.Notes.Count) > 100000 || captured.Select(c => c.SourceId).Distinct().Count() != captured.Length)
            throw new ArgumentException("Invalid imported clip identities or note budget");
        document.Edit("Import note clips", p =>
        {
            var sources = p.Sources.Values.ToList(); var scoreClips = p.Arrangement.ScoreClips.ToList();
            foreach (var item in captured)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(item.Name);
                if (item.Name.Length > 1024 || p.Sources.ContainsKey(item.SourceId) || !p.Routing.Tracks.Any(t => t.Id == item.TrackId))
                    throw new ArgumentException("Invalid imported source name, identity or target track");
                var sequence = new SequenceSnapshot(Guid.NewGuid(), item.Name, item.Input.DurationQuarters, item.Input.Notes);
                var section = new SectionSnapshot(Guid.NewGuid(), item.Name, new(Bpm: p.Context.Tempo.Changes[0].Bpm), [sequence]);
                var result = new GeneratedSourceOutput(item.SourceId, 0, p.Context, [new("main", new(Guid.NewGuid(), [new(Guid.NewGuid(), section)]))]);
                sources.Add(ProjectSource.Restore(new(1, item.SourceId, "editable"), "", result,
                    [new(Guid.NewGuid(), new(GeneratedRole.Score, "main"), true)], new(null, "main", 0)));
                scoreClips.Add(new(item.ClipId, item.TrackId, item.SourceId, "main", item.AnchorQuarters, 0, item.Input.DurationQuarters));
            }
            return new(new(p.Arrangement.Id, p.Arrangement.Tempo, p.Arrangement.Meter, scoreClips, p.Arrangement.AudioClips),
                p.Context, sources, p.Routing, p.Assets, p.Automation, p.RenderSettings);
        });
    }
    public static void CreateBlank(ProjectDocument document, Guid sourceId, Guid clipId, Guid trackId,
        double anchorQuarters, double lengthQuarters, string name = "Notes")
        => CreateFromNotes(document, sourceId, clipId, trackId, anchorQuarters, lengthQuarters, [], name);

    /// <summary>Capture an editable source and its clip in one history action.</summary>
    public static void CreateFromNotes(ProjectDocument document, Guid sourceId, Guid clipId, Guid trackId,
        double anchorQuarters, double lengthQuarters, IEnumerable<NoteEvent> notes, string name = "Recorded notes")
    {
        var captured = notes.Take(100001).ToArray();
        if (captured.Length > 100000 || captured.Any(n => n is null || n.Id == Guid.Empty ||
            !double.IsFinite(n.OffsetQuarters) || !double.IsFinite(n.DurationQuarters) || n.OffsetQuarters < 0 || n.DurationQuarters <= 0 ||
            n.OffsetQuarters + n.DurationQuarters > lengthQuarters + 1e-9) || captured.Select(n => n.Id).Distinct().Count() != captured.Length)
            throw new ArgumentException("Invalid captured note clip or budget");
        document.Edit("Create note clip", p =>
        {
            Validate.Id(sourceId, nameof(sourceId)); Validate.Positive(lengthQuarters, nameof(lengthQuarters));
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            if (p.Sources.ContainsKey(sourceId) || !p.Routing.Tracks.Any(t => t.Id == trackId))
                throw new ArgumentException("Duplicate source identity or unknown track");
            var sequence = new SequenceSnapshot(Guid.NewGuid(), name, lengthQuarters, captured);
            var section = new SectionSnapshot(Guid.NewGuid(), name, new(), [sequence]);
            var score = new CompositionSnapshot(Guid.NewGuid(), [new(Guid.NewGuid(), section)]);
            var result = new GeneratedSourceOutput(sourceId, 0, p.Context, [new("main", score)]);
            var source = ProjectSource.Restore(new(1, sourceId, "editable"), "", result,
                [new(Guid.NewGuid(), new(GeneratedRole.Score, "main"), true)], new(null, "main", 0));
            var clip = new ScoreClip(clipId, trackId, sourceId, "main", anchorQuarters, 0, lengthQuarters);
            return new(new(p.Arrangement.Id, p.Arrangement.Tempo, p.Arrangement.Meter,
                p.Arrangement.ScoreClips.Append(clip), p.Arrangement.AudioClips), p.Context,
                p.Sources.Values.Append(source), p.Routing, p.Assets, p.Automation, p.RenderSettings);
        });
    }

    /// <summary>Detach this clip only. Other linked clips remain code-owned. Copy the
    /// full source layer so existing source-window offsets and shared repeats survive.</summary>
    public static void MakeEditable(ProjectDocument document, Guid clipId, Guid newSourceId)
    {
        document.Edit("Convert clip to editable notes", p =>
        {
            Validate.Id(newSourceId, nameof(newSourceId));
            if (p.Sources.ContainsKey(newSourceId)) throw new ArgumentException("Source identity already exists");
            var clip = p.Arrangement.ScoreClips.SingleOrDefault(c => c.Id == clipId) ?? throw new ArgumentException("Unknown clip");
            if (!p.Sources.TryGetValue(clip.SourceId, out var original)) throw new ArgumentException("Missing source");
            var layer = original.Result.ScoreLayers.SingleOrDefault(l => l.Id == clip.LayerId) ?? throw new ArgumentException("Missing source layer");
            var result = new GeneratedSourceOutput(newSourceId, 0, p.Context, [layer]);
            var binding = new OutputBinding(Guid.NewGuid(), new(GeneratedRole.Score, layer.Id), true);
            var source = ProjectSource.Restore(new(1, newSourceId, "editable"), "", result, [binding],
                new(original.Descriptor.SourceId, layer.Id, original.Result.SourceRevision));
            var replacement = new ScoreClip(clip.Id, clip.TrackId, newSourceId, clip.LayerId, clip.AnchorQuarters,
                clip.SourceOffsetQuarters, clip.LengthQuarters, clip.Nudge);
            return new(new(p.Arrangement.Id, p.Arrangement.Tempo, p.Arrangement.Meter,
                p.Arrangement.ScoreClips.Select(c => c.Id == clipId ? replacement : c), p.Arrangement.AudioClips),
                p.Context, p.Sources.Values.Append(source), p.Routing, p.Assets, p.Automation, p.RenderSettings);
        });
    }
    /// <summary>One captured action per gesture. Edits affect all clips linked to
    /// this editable source; generated sources require explicit conversion first.</summary>
    public static void EditSequence(ProjectDocument document, Guid sourceId, Guid sectionId, Guid sequenceId,
        string description, Func<SequenceSnapshot, SequenceSnapshot> edit)
        => EditSequences(document, description, [new(sourceId, sectionId, sequenceId, edit)]);

    /// <summary>One atomic captured gesture across sources/sequences. Callbacks run
    /// once during execution; redo restores snapshots rather than re-running edits.</summary>
    public static void EditSequences(ProjectDocument document, string description, IEnumerable<SequenceEdit> edits)
    {
        var captured = edits.Take(1025).ToArray();
        if (captured.Length is < 1 or > 1024 || captured.Any(e => e is null || e.Apply is null) ||
            captured.Select(e => (e.SourceId, e.SectionId, e.SequenceId)).Distinct().Count() != captured.Length)
            throw new ArgumentException("Invalid grouped note edit");
        document.Edit(description, p =>
        {
            var sources = p.Sources.ToDictionary(s => s.Key, s => s.Value);
            foreach (var group in captured.GroupBy(e => e.SourceId))
            {
                if (!sources.TryGetValue(group.Key, out var source) || !source.IsEditable)
                    throw new InvalidOperationException("Convert generated content to an editable source before editing notes");
                var layer = source.Result.ScoreLayers.Single();
                var composition = layer.Composition;
                foreach (var change in group)
                    composition = ScoreEditing.EditSequence(composition, change.SectionId, change.SequenceId, change.Apply);
                var result = new GeneratedSourceOutput(group.Key, checked(source.Result.SourceRevision + 1), p.Context, [new(layer.Id, composition)]);
                sources[group.Key] = ProjectSource.Restore(source.Descriptor, "", result, source.Bindings.ToArray(), source.EditableOrigin);
            }
            return new(p.Arrangement, p.Context, sources.Values,
                p.Routing, p.Assets, p.Automation, p.RenderSettings);
        });
    }
}
