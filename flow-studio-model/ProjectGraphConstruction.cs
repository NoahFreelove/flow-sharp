using Flow.Audio.Graph;
using Flow.Audio;
namespace Flow.Studio.Model;

/// <summary>Pure construction operation for exported project definitions. Preserves
/// source identity/revision, output bindings and code as historical authoring data;
/// does not execute code or publish a live graph.</summary>
public static class ProjectGraphConstruction
{
    public static ProjectSnapshot Replace(ProjectSnapshot project, Guid sourceId, string layerId, AudioGraphDefinition graph)
    {
        if (!project.Sources.TryGetValue(sourceId, out var source) || !source.Result.GraphLayers.Any(l => l.Id == layerId))
            throw new ArgumentException("Unknown project graph output");
        var output = source.Result;
        var result = new GeneratedSourceOutput(sourceId, output.SourceRevision, output.Context, output.ScoreLayers, output.AudioLayers,
            output.GraphLayers.Select(l => l.Id == layerId ? new GeneratedGraphLayer(l.Id, graph) : l), output.InstrumentLayers);
        var changed = ProjectSource.Restore(source.Descriptor, source.Code, result, source.Bindings.ToArray(), source.EditableOrigin, source.IsManagedGraph, source.Plugin, source.PluginValues, source.AssetGrants);
        return new(project.Arrangement, project.Context, project.Sources.Values.Select(s => s.Descriptor.SourceId == sourceId ? changed : s),
            project.Routing, project.Assets, project.Automation, project.RenderSettings);
    }
    public static ProjectSnapshot ReplaceInstrument(ProjectSnapshot project, Guid sourceId, string layerId, SineVoiceSettings instrument)
    {
        if (!project.Sources.TryGetValue(sourceId, out var source) || !source.Result.InstrumentLayers.Any(l => l.Id == layerId))
            throw new ArgumentException("Unknown project instrument output");
        var output = source.Result;
        var result = new GeneratedSourceOutput(sourceId, output.SourceRevision, output.Context, output.ScoreLayers, output.AudioLayers,
            output.GraphLayers, output.InstrumentLayers.Select(l => l.Id == layerId ? new GeneratedInstrumentLayer(l.Id, instrument) : l));
        var changed = ProjectSource.Restore(source.Descriptor, source.Code, result, source.Bindings.ToArray(), source.EditableOrigin, source.IsManagedGraph, source.Plugin, source.PluginValues, source.AssetGrants);
        return new(project.Arrangement, project.Context, project.Sources.Values.Select(s => s.Descriptor.SourceId == sourceId ? changed : s),
            project.Routing, project.Assets, project.Automation, project.RenderSettings);
    }

}
