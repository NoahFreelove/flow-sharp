using Flow.Music.Model;
using Flow.Music.Model.Editing;
namespace Flow.Studio.Model;
public static class ProjectNoteConstruction
{
    /// <summary>Pure reconstruction of one sequence's events, retaining the score
    /// structure, bars, controls, identities and source ownership metadata.</summary>
    public static ProjectSnapshot Replace(ProjectSnapshot p, Guid sourceId, string layerId, Guid sectionId, Guid sequenceId,
        IEnumerable<NoteEvent> notes)
    {
        if (!p.Sources.TryGetValue(sourceId, out var source)) throw new ArgumentException("Unknown source");
        var output = source.Result;
        var layer = output.ScoreLayers.SingleOrDefault(l => l.Id == layerId) ?? throw new ArgumentException("Unknown score layer");
        var copy = notes.Take(1000001).ToArray();
        if (copy.Length > 1000000) throw new ArgumentException("Score event budget exceeded");
        var score = ScoreEditing.EditSequence(layer.Composition, sectionId, sequenceId,
            s => new(s.Id, s.Name, s.DurationQuarters, copy, s.Bars));
        // Reuse interchange validation for complete authored metadata, including
        // negative source offsets that are valid in generated scores.
        _ = CompositionJson.Deserialize(CompositionJson.Serialize(score));
        var result = new GeneratedSourceOutput(sourceId, output.SourceRevision, output.Context,
            output.ScoreLayers.Select(l => l.Id == layerId ? new GeneratedScoreLayer(l.Id, score) : l), output.AudioLayers, output.GraphLayers, output.InstrumentLayers);
        var changed = ProjectSource.Restore(source.Descriptor, source.Code, result, source.Bindings.ToArray(), source.EditableOrigin, source.IsManagedGraph, source.Plugin, source.PluginValues, source.AssetGrants);
        return new(p.Arrangement, p.Context, p.Sources.Values.Select(s => s.Descriptor.SourceId == sourceId ? changed : s), p.Routing, p.Assets, p.Automation, p.RenderSettings);
    }
}
