using System.Text.Json;
using System.Text.Json.Serialization;
namespace Flow.Studio.Model;

/// <summary>Versioned project interchange. Loading never executes source.
/// Inline audio retains the generated-content limits; external assets are separate work.</summary>
public static class ProjectJson
{
    public const int MaxCharacters = 64 * 1024 * 1024;
    private sealed record Context(long Revision, int Seed, TempoChange[] Tempo, MeterChange[] Meter, Dictionary<string, double> Parameters, ProjectTuning? Tuning = null);
    private sealed record Source(int ApiVersion, Guid Id, string EntryPoint, string Code, long Revision,
        Context Context, string Contents, OutputBinding[] Bindings, EditableSourceOrigin? EditableOrigin = null, bool ManagedGraph = false, string? Plugin = null, Dictionary<string, double>? PluginValues = null, Guid[]? AssetGrants = null);
    private sealed record Lane(Guid Id, Guid GraphBinding, string NodeId, string ParameterId,
        Flow.Audio.Graph.AutomationShape Shape, MusicalAutomationPoint[] Points, AutomationTargetKind TargetKind = AutomationTargetKind.GraphNode);
    private sealed record Data(int Version, string Arrangement, Context Context, Source[] Sources,
        ProjectTrack[] Tracks, Guid? GraphBinding, AudioAssetReference[]? Assets = null, Lane[]? Automation = null, ProjectEffect[]? Effects = null,
        ProjectRenderSettings? RenderSettings = null);
    private static readonly JsonSerializerOptions Options = new()
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectRequiredConstructorParameters = true, MaxDepth = 32 };
    private static Context Encode(GenerationContext c) => new(c.Revision, c.Seed, c.Tempo.Changes.ToArray(), c.Meter.Changes.ToArray(), new(c.Parameters), c.Tuning);
    private static GenerationContext Decode(Context c) => new(c.Revision, c.Seed, new(c.Tempo), new(c.Meter), c.Parameters, c.Tuning);
    public static string Serialize(ProjectSnapshot project)
    {
        var sources = project.Sources.Values.OrderBy(s => s.Descriptor.SourceId).Select(s => new Source(
            s.Descriptor.ApiVersion, s.Descriptor.SourceId, s.Descriptor.EntryPoint, s.Code, s.Result.SourceRevision,
            Encode(s.Result.Context), GeneratedContentJson.Serialize(s.Result), s.Bindings.ToArray(), s.EditableOrigin, s.IsManagedGraph, s.Plugin?.Serialize(), new(s.PluginValues), s.AssetGrants.ToArray())).ToArray();
        var json = JsonSerializer.Serialize(new Data(17, ArrangementJson.Serialize(project.Arrangement), Encode(project.Context),
            sources, project.Routing.Tracks.ToArray(), project.Routing.GraphBinding, project.Assets.ToArray(), project.Automation.Select(l => new Lane(l.Id, l.GraphBinding, l.NodeId, l.ParameterId, l.Shape, l.Points.ToArray(), l.TargetKind)).ToArray(), project.Routing.Effects.ToArray(), project.RenderSettings), Options);
        if (json.Length > MaxCharacters) throw new InvalidDataException("Project exceeds interchange budget");
        return json;
    }
    public static ProjectSnapshot Deserialize(string json)
    {
        if (json.Length > MaxCharacters) throw new InvalidDataException("Project exceeds interchange budget");
        var data = JsonSerializer.Deserialize<Data>(json, Options) ?? throw new JsonException("Missing project");
        if (data.Version is not (1 or 2 or 3 or 4 or 5 or 6 or 7 or 8 or 9 or 10 or 11 or 12 or 13 or 14 or 15 or 16 or 17) || (data.Version >= 2 && data.Assets is null) || (data.Version >= 3 && data.Automation is null) || data.Context is null || data.Sources is null || data.Sources.Length > 4096 || data.Tracks is null ||
            (data.Version >= 6 && data.Tracks.Any(t => t is null || t.InputBus is null)))
            throw new JsonException("Unsupported or incomplete project schema");
        var contexts = data.Sources.Select(s => s.Context).Prepend(data.Context);
        if (contexts.Any(c => c is null || (data.Version >= 15 ? c.Tuning is null : c.Tuning is not null && c.Tuning != new ProjectTuning())))
            throw new JsonException("Captured tuning requires project schema 15");
        if (data.Version >= 13 ? data.RenderSettings is null : data.RenderSettings is not null && data.RenderSettings != new ProjectRenderSettings())
            throw new JsonException("Render preferences require project schema 13");
        if (data.Version < 12 && data.Tracks.Any(t => t is not null && (t.Muted || t.Solo)))
            throw new JsonException("Track mute/solo requires project schema 12");
        if (data.Version < 17 && data.Tracks.Any(t => t?.ColorRgb is not null))
            throw new JsonException("Track colors require project schema 17");
        if ((data.Version >= 14 && data.Sources.Any(s => s.AssetGrants is null)) || (data.Version < 14 && data.Sources.Any(s => s.AssetGrants is { Length: > 0 })))
            throw new JsonException("Source asset grants require project schema 14");
        if (data.Version < 8 && data.Sources.Any(s => s.Plugin is not null)) throw new JsonException("Plugin packages require project schema 8");
        if (data.Version < 9 && data.Sources.Any(s => s.PluginValues is { Count: > 0 })) throw new JsonException("Plugin values require project schema 9");
        if (data.Version < 10 && data.Automation?.Any(l => l.TargetKind != AutomationTargetKind.GraphNode) == true)
            throw new JsonException("Public plugin automation requires project schema 10");
        if ((data.Version >= 11 && data.Effects is null) || (data.Version < 11 && data.Effects is { Length: > 0 }))
            throw new JsonException("Effect chains require project schema 11");
        var arrangement = ArrangementJson.Deserialize(data.Arrangement);
        if (data.Version < 16 && arrangement.AudioClips.Any(c => c.Envelope is not null))
            throw new JsonException("Clip envelopes require project schema 16");
        var savedContext = Decode(data.Context);
        if (!savedContext.Tempo.Changes.SequenceEqual(arrangement.Tempo.Changes) || !savedContext.Meter.Changes.SequenceEqual(arrangement.Meter.Changes))
            throw new JsonException("Project context timing does not match arrangement");
        var context = new GenerationContext(savedContext.Revision, savedContext.Seed, arrangement.Tempo, arrangement.Meter, savedContext.Parameters, savedContext.Tuning);
        var sources = data.Sources.Select(s => ProjectSource.Restore(new(s.ApiVersion, s.Id, s.EntryPoint), s.Code,
            GeneratedContentJson.Deserialize(s.Contents, s.Id, s.Revision, Decode(s.Context)), s.Bindings, s.EditableOrigin, s.ManagedGraph, s.Plugin is null ? null : PluginPackage.Deserialize(s.Plugin), s.PluginValues, s.AssetGrants)).ToArray();
        return new(arrangement, context, sources, new(data.Tracks, data.GraphBinding, data.Effects), data.Assets, data.Automation?.Select(l => new ProjectAutomationLane(l.Id, l.GraphBinding, l.NodeId, l.ParameterId, l.Points, l.Shape, l.TargetKind)), data.RenderSettings);
    }
}
