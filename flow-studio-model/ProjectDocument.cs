using System.Collections.ObjectModel;

namespace Flow.Studio.Model;

public enum GeneratedRole { Score, Audio, Graph, Instrument }
public sealed record OutputKey(GeneratedRole Role, string LayerId);
/// <summary>Stable host binding identity, retained when an output disappears.</summary>
public sealed record OutputBinding(Guid Id, OutputKey Output, bool Available);

public sealed record EditableSourceOrigin(Guid? SourceId, string LayerId, long Revision);

public sealed class ProjectSource
{
    public GeneratorDescriptor Descriptor { get; }
    public string Code { get; }
    public EditableSourceOrigin? EditableOrigin { get; }
    public bool IsEditable => EditableOrigin is not null;
    /// <summary>Visual mixer gestures may regenerate this dedicated canonical
    /// source. Ordinary code acceptance clears ownership; manual score ownership
    /// remains a separate IsEditable contract.</summary>
    public bool IsManagedGraph { get; }
    public PluginPackage? Plugin { get; }
    public IReadOnlyList<Guid> AssetGrants { get; }
    public IReadOnlyDictionary<string, double> PluginValues { get; }
    public GeneratedSourceOutput Result { get; }
    public IReadOnlyList<OutputBinding> Bindings { get; }
    internal static ProjectSource Restore(GeneratorDescriptor descriptor, string code, GeneratedSourceOutput result,
        OutputBinding[] bindings, EditableSourceOrigin? editableOrigin = null, bool isManagedGraph = false, PluginPackage? plugin = null, IReadOnlyDictionary<string, double>? pluginValues = null, IEnumerable<Guid>? assetGrants = null)
    {
        if (code is null || code.Length > 2 * 1024 * 1024 || descriptor.SourceId != result.SourceId)
            throw new ArgumentException("Invalid saved source");
        var fresh = new ProjectSource(descriptor, code, result, (ProjectSource?)null, isManagedGraph, plugin);
        if (bindings is null || bindings.Length > 4096) throw new ArgumentException("Invalid saved bindings");
        var ids = new HashSet<Guid>(); var keys = new HashSet<OutputKey>();
        var available = fresh.Bindings.Select(b => b.Output).ToHashSet();
        foreach (var b in bindings)
        {
            if (b is null || b.Id == Guid.Empty || b.Output is null || !Enum.IsDefined(b.Output.Role) ||
                string.IsNullOrWhiteSpace(b.Output.LayerId) || !ids.Add(b.Id) || !keys.Add(b.Output) ||
                b.Available != available.Contains(b.Output)) throw new ArgumentException("Invalid saved binding identity/availability");
        }
        if (!available.IsSubsetOf(keys)) throw new ArgumentException("Missing output bindings");
        if (editableOrigin is not null && (editableOrigin.SourceId == Guid.Empty || string.IsNullOrWhiteSpace(editableOrigin.LayerId) ||
            editableOrigin.Revision < 0 || (editableOrigin.SourceId is null && editableOrigin.Revision != 0) || result.ScoreLayers.Count != 1 || result.AudioLayers.Count != 0 ||
            result.GraphLayers.Count != 0 || result.InstrumentLayers.Count != 0 || code.Length != 0))
            throw new ArgumentException("Invalid editable source provenance/content");
        return new ProjectSource(descriptor, code, result, bindings, editableOrigin, isManagedGraph, plugin, pluginValues, assetGrants);
    }
    private ProjectSource(GeneratorDescriptor descriptor, string code, GeneratedSourceOutput result, OutputBinding[] bindings, EditableSourceOrigin? editableOrigin, bool isManagedGraph, PluginPackage? plugin, IReadOnlyDictionary<string, double>? pluginValues, IEnumerable<Guid>? assetGrants)
    { AssetGrants = CopyGrants(assetGrants, plugin, editableOrigin); Descriptor = descriptor; Code = code; Result = result; Bindings = Array.AsReadOnly(bindings.ToArray()); EditableOrigin = editableOrigin; IsManagedGraph = isManagedGraph; Plugin = plugin; PluginValues = CopyPluginValues(plugin, pluginValues); }
    internal ProjectSource(GeneratorDescriptor descriptor, string code, GeneratedSourceOutput result,
        ProjectSource? previous, bool isManagedGraph = false, PluginPackage? plugin = null)
    {
        if (isManagedGraph && (code.Length == 0 || descriptor.EntryPoint != "generate" || result.ScoreLayers.Count != 0 ||
            result.AudioLayers.Count != 0 || result.InstrumentLayers.Count != 0 || result.GraphLayers.Count != 1 || result.GraphLayers[0].Id != "mixer"))
            throw new ArgumentException("Managed mixer sources require a single mixer graph and construction code");
        if (plugin is not null && (isManagedGraph || code != plugin.Source || descriptor.EntryPoint != plugin.Manifest.Builder))
            throw new ArgumentException("Plugin snapshot does not match source identity/ownership");
        AssetGrants = CopyGrants(previous?.AssetGrants, plugin, null);
        Plugin = plugin;
        // Freeze the old effective values, including implicit defaults. New public
        // controls use their new defaults; a compatible reload must not reset a sound.
        PluginValues = CopyPluginValues(plugin, plugin?.Manifest.Kind is FlowPluginKind.OfflineAudio or FlowPluginKind.NoteTransform
            ? plugin.Manifest.Parameters.ToDictionary(p => p.Id, p => result.Context.Parameters.GetValueOrDefault(p.Id, p.Default))
            : plugin is not null && previous?.Plugin?.Manifest.Id == plugin.Manifest.Id
                ? previous.Plugin.Manifest.Parameters.ToDictionary(p => p.Id, p => previous.PluginValues.GetValueOrDefault(p.Id, p.Default))
                : null);
        IsManagedGraph = isManagedGraph;
        Descriptor = descriptor; Code = code; Result = result;
        var keys = result.ScoreLayers.Select(l => new OutputKey(GeneratedRole.Score, l.Id))
            .Concat(result.AudioLayers.Select(l => new OutputKey(GeneratedRole.Audio, l.Id)))
            .Concat(result.GraphLayers.Select(l => new OutputKey(GeneratedRole.Graph, l.Id)))
            .Concat(result.InstrumentLayers.Select(l => new OutputKey(GeneratedRole.Instrument, l.Id))).ToHashSet();
        var bindings = (previous?.Bindings ?? []).Select(b => b with { Available = keys.Remove(b.Output) }).ToList();
        bindings.AddRange(keys.OrderBy(k => k.Role).ThenBy(k => k.LayerId, StringComparer.Ordinal)
            .Select(k => new OutputBinding(Guid.NewGuid(), k, true)));
        if (bindings.Count > 4096) throw new InvalidOperationException("Source binding history exceeds 4096 outputs; repair unused bindings first");
        Bindings = bindings.AsReadOnly();
    }
    private static IReadOnlyList<Guid> CopyGrants(IEnumerable<Guid>? grants, PluginPackage? plugin, EditableSourceOrigin? editable)
    {
        var copy = (grants ?? []).Take(33).ToArray();
        if (copy.Length > 32 || copy.Any(id => id == Guid.Empty) || copy.Distinct().Count() != copy.Length ||
            (copy.Length != 0 && (plugin is not null || editable is not null)))
            throw new ArgumentException("Asset grants require distinct project identities on an ordinary generator");
        return Array.AsReadOnly(copy.Order().ToArray());
    }
    private static IReadOnlyDictionary<string, double> CopyPluginValues(PluginPackage? plugin, IReadOnlyDictionary<string, double>? values)
    {
        var copy = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var pair in values ?? new Dictionary<string, double>())
        {
            var parameter = plugin?.Manifest.Parameters.SingleOrDefault(p => p.Id == pair.Key)
                ?? throw new ArgumentException("Saved value has no declared plugin parameter");
            _ = parameter.ToNormalized(pair.Value); // Validate typed range and discrete values.
            copy.Add(pair.Key, pair.Value);
        }
        return new ReadOnlyDictionary<string, double>(copy);
    }

}

public sealed class ProjectSnapshot
{
    public ArrangementSnapshot Arrangement { get; }
    public GenerationContext Context { get; }
    public ProjectRouting Routing { get; }
    public IReadOnlyList<AudioAssetReference> Assets { get; }
    public IReadOnlyList<ProjectAutomationLane> Automation { get; }
    public ProjectRenderSettings RenderSettings { get; }
    public IReadOnlyDictionary<Guid, ProjectSource> Sources { get; }
    public ProjectSnapshot(ArrangementSnapshot arrangement, GenerationContext context,
        IEnumerable<ProjectSource>? sources = null, ProjectRouting? routing = null, IEnumerable<AudioAssetReference>? assets = null, IEnumerable<ProjectAutomationLane>? automation = null,
        ProjectRenderSettings? renderSettings = null)
    {
        ArgumentNullException.ThrowIfNull(arrangement);
        ArgumentNullException.ThrowIfNull(context);
        // Context timing must be the actual arrangement snapshot, not just a matching revision number.
        if (!ReferenceEquals(arrangement.Tempo, context.Tempo) || !ReferenceEquals(arrangement.Meter, context.Meter))
            throw new ArgumentException("Generation context must use arrangement timing");
        Arrangement = arrangement; Context = context; Routing = routing ?? new ProjectRouting([], null);
        RenderSettings = renderSettings ?? new();
        var copy = (sources ?? []).ToDictionary(s => s.Descriptor.SourceId);
        if (copy.Count > 4096) throw new ArgumentException("Project exceeds source limit");
        var assetCopy = (assets ?? []).ToArray();
        if (assetCopy.Length > 4096 || assetCopy.Any(a => a is null) || assetCopy.Select(a => a.Id).Distinct().Count() != assetCopy.Length)
            throw new ArgumentException("Invalid project assets");
        Assets = Array.AsReadOnly(assetCopy);
        var lanes = (automation ?? []).Take(257).ToArray();
        if (lanes.Length > 256 || lanes.Any(l => l is null) || lanes.Sum(l => (long)l.Points.Count) > 100000 ||
            lanes.Select(l => l.Id).Distinct().Count() != lanes.Length ||
            lanes.Select(l => (l.TargetKind, l.GraphBinding, l.NodeId, l.ParameterId)).Distinct().Count() != lanes.Length)
            throw new ArgumentException("Invalid project automation identities/budget");
        Automation = Array.AsReadOnly(lanes);
        var bindings = copy.Values.SelectMany(s => s.Bindings).Select(b => b.Id).ToArray();
        if (bindings.Distinct().Count() != bindings.Length || assetCopy.Any(a => bindings.Contains(a.Id))) throw new ArgumentException("Duplicate project binding identity");
        Sources = new ReadOnlyDictionary<Guid, ProjectSource>(copy);
    }
}

/// <summary>Opaque request capability; only its issuing document may accept it.</summary>
public sealed class SourceBuildTicket
{
    public GeneratorDescriptor Descriptor { get; }
    public string Code { get; }
    public long Revision { get; }
    public GenerationContext Context { get; }
    internal SourceBuildTicket(GeneratorDescriptor descriptor, string code, long revision, GenerationContext context)
    { Descriptor = descriptor; Code = code; Revision = revision; Context = context; }
}

/// <summary>Single control-thread project owner. Draft builds are transient; accepted
/// source code, results and bindings commit together. Playback publication follows commit.</summary>
public sealed class ProjectDocument
{
    private readonly Dictionary<Guid, SourceBuildTicket> _pending = [];
    private long _nextBuild;
    public ProjectSnapshot Snapshot { get; private set; }
    /// <summary>Transient monotonic edit generation, including undo/redo. Unlike
    /// snapshot identity it cannot revive a stale asynchronous result after undo.</summary>
    public long ChangeVersion { get; private set; }
    public ActionHistory History { get; }
    public ProjectDocument(ProjectSnapshot snapshot, int historyCapacity = 128, long historyBudgetBytes = 256 * 1024 * 1024)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Snapshot = snapshot; History = new(historyCapacity, historyBudgetBytes);
        _nextBuild = snapshot.Sources.Values.Select(s => s.Result.SourceRevision).DefaultIfEmpty().Max();
    }
    public SourceBuildTicket BeginBuild(GeneratorDescriptor descriptor, string code)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(code);
        if (Snapshot.Sources.TryGetValue(descriptor.SourceId, out var existing) && existing.IsEditable)
            throw new InvalidOperationException("Editable sources cannot be regenerated");
        if (code.Length > 2 * 1024 * 1024) throw new ArgumentException("Source exceeds build budget");
        var ticket = new SourceBuildTicket(descriptor, code, checked(++_nextBuild), Snapshot.Context);
        _pending[descriptor.SourceId] = ticket;
        return ticket;
    }
    public bool Accept(SourceBuildTicket ticket, GeneratedSourceOutput result)
        => Accept(ticket, result, "Accept generated source", static snapshot => snapshot);

    /// <summary>Accept generated content and dependent routing/clip changes in one
    /// captured action. The transform runs before any document mutation. Only
    /// canonical mixer construction opts into managedGraph; ordinary code builds
    /// return the source to user ownership.</summary>
    public bool Accept(SourceBuildTicket ticket, GeneratedSourceOutput result, string description,
        Func<ProjectSnapshot, ProjectSnapshot> transform, bool managedGraph = false, PluginPackage? plugin = null)
    {
        ArgumentNullException.ThrowIfNull(transform);
        ArgumentNullException.ThrowIfNull(ticket); ArgumentNullException.ThrowIfNull(result);
        if (!_pending.TryGetValue(ticket.Descriptor.SourceId, out var current) || !ReferenceEquals(ticket, current)) return false;
        if (result.SourceId != ticket.Descriptor.SourceId || result.SourceRevision != ticket.Revision ||
            !ReferenceEquals(result.Context, ticket.Context) || !ReferenceEquals(Snapshot.Context, ticket.Context)) return false;
        Snapshot.Sources.TryGetValue(result.SourceId, out var previous);
        if (previous?.Plugin is not null && plugin is not null)
            PluginReloadContract.Validate(previous, plugin, result, Snapshot.Automation);
        var source = new ProjectSource(ticket.Descriptor, ticket.Code, result, previous, managedGraph, plugin);
        var after = new ProjectSnapshot(Snapshot.Arrangement, Snapshot.Context,
            Snapshot.Sources.Values.Where(s => s.Descriptor.SourceId != result.SourceId).Append(source), Snapshot.Routing, Snapshot.Assets, Snapshot.Automation, Snapshot.RenderSettings);
        Commit(description, transform(after) ?? throw new ArgumentException("Missing project result"));
        return true;
    }
    /// <summary>Release an abandoned draft without invalidating a newer request.</summary>
    public bool DiscardBuild(SourceBuildTicket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        if (!_pending.TryGetValue(ticket.Descriptor.SourceId, out var current) || !ReferenceEquals(ticket, current)) return false;
        return _pending.Remove(ticket.Descriptor.SourceId);
    }
    public void Edit(string description, Func<ProjectSnapshot, ProjectSnapshot> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);
        var after = transform(Snapshot) ?? throw new ArgumentException("Missing project result");
        if (!ReferenceEquals(after, Snapshot)) Commit(description, after);
    }
    private void Commit(string description, ProjectSnapshot after)
    {
        if (after.Arrangement.Id != Snapshot.Arrangement.Id) throw new ArgumentException("Cannot change project identity");
        // Includes detached contents as conservative serialized-size accounting; not heap telemetry.
        long cost = checked(Estimate(Snapshot) + Estimate(after));
        History.Execute(new EditAction(this, description, Snapshot, after), cost);
    }
    private static long Estimate(ProjectSnapshot state) => checked(1024L +
        state.Routing.Tracks.Sum(t => 128L + t.Name.Length * 2L) +
        state.Routing.Effects.Count * 64L +
        state.Assets.Sum(a => 256L + a.RelativePath.Length * 2L) +
        state.Automation.Sum(a => 256L + a.Points.Count * 32L + (a.NodeId.Length + a.ParameterId.Length) * 2L) +
        state.Arrangement.ScoreClips.Sum(c => 256L + c.LayerId.Length * 2L) + state.Arrangement.AudioClips.Count * 256L +
        state.Sources.Values.Sum(s => 1024L + s.Code.Length * 2L + (s.Plugin?.Serialize().Length ?? 0) * 2L + s.PluginValues.Sum(p => 32L + p.Key.Length * 2L) + GeneratedContentJson.Serialize(s.Result).Length * 2L +
            s.Bindings.Sum(b => 128L + b.Output.LayerId.Length * 2L)));
    private sealed class EditAction(ProjectDocument owner, string description, ProjectSnapshot before, ProjectSnapshot after) : IUndoableAction
    {
        private bool _applied;
        public string Description => description;
        public void Undo() => Install(after, before);
        public void Redo() => Install(before, after);
        private void Install(ProjectSnapshot expected, ProjectSnapshot next)
        {
            if (!ReferenceEquals(owner.Snapshot, expected)) throw new InvalidOperationException("Project revision changed");
            // Reserve invalidation work before installation. Independent builds and clip moves
            // remain valid; changing the relevant source or context invalidates a request.
            var invalid = owner._pending.Keys.Where(id => _applied ||
                !ReferenceEquals(expected.Context, next.Context) ||
                !ReferenceEquals(expected.Sources.GetValueOrDefault(id), next.Sources.GetValueOrDefault(id))).ToArray();
            long nextVersion = checked(owner.ChangeVersion + 1);
            owner.Snapshot = next;
            owner.ChangeVersion = nextVersion;
            foreach (var id in invalid) owner._pending.Remove(id);
            _applied = true; // Undo/redo must never revive an in-flight request (including ABA).
        }
    }
}
