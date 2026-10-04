using Flow.Audio.Graph;
using Flow.Studio.Model;
using FlowLang.Hosting;
namespace Flow.Studio.Host;

public sealed class MixerEditRequest
{
    public ProjectSnapshot Before { get; }
    public SourceBuildTicket Ticket { get; }
    public ProjectTrack? AddedTrack { get; }
    public string Description { get; }
    internal Guid PreviousGraph { get; }
    internal string? RemovedNode { get; }
    internal MixerEditRequest(ProjectSnapshot before, SourceBuildTicket ticket, ProjectTrack? track, Guid previousGraph, string description, string? removedNode = null)
    { Before = before; Ticket = ticket; AddedTrack = track; PreviousGraph = previousGraph; Description = description; RemovedNode = removedNode; }
}
public sealed class PreparedMixerEdit
{
    public MixerEditRequest Request { get; }
    internal GeneratedSourceOutput Output { get; }
    internal PreparedMixerEdit(MixerEditRequest request, GeneratedSourceOutput output) { Request = request; Output = output; }
}

/// <summary>Flow-authored mixer gestures. Begin/Commit belong to the document owner;
/// Prepare belongs to a bounded worker. User-owned graphs are copied to a managed
/// canonical source; later visual edits reuse that source and its output bindings.</summary>
public static class ProjectMixerAuthoring
{
    public static MixerEditRequest BeginAddTrack(ProjectDocument document, Guid trackId, string name,
        string destinationNodeId, int inputIndex = 0, Guid? instrumentBinding = null)
    {
        var (before, owner, bindingId, graph) = Selected(document);
        var destination = graph.Nodes.SingleOrDefault(n => n.Id == destinationNodeId) ?? throw new ArgumentException("Unknown destination node");
        if (inputIndex < 0 || inputIndex >= destination.Inputs.Count) throw new ArgumentOutOfRangeException(nameof(inputIndex));
        var used = before.Routing.Tracks.Select(t => t.InputBus!.Value)
            .Concat(graph.Nodes.Where(n => n.DeviceId == "flow.input").Select(n => (int)n.Parameters["bus"])).ToHashSet();
        int bus = Enumerable.Range(0, 64).FirstOrDefault(b => !used.Contains(b), -1);
        if (bus < 0) throw new InvalidOperationException("No unused graph input bus remains");
        var track = new ProjectTrack(trackId, name, instrumentBinding, bus);
        _ = new ProjectRouting(before.Routing.Tracks.Append(track), bindingId, before.Routing.Effects);
        if (instrumentBinding is not null && !before.Sources.Values.Any(s => s.Bindings.Any(b => b.Id == instrumentBinding && b.Available && b.Output.Role == GeneratedRole.Instrument)))
            throw new ArgumentException("Instrument output is unavailable");
        string prefix = "track" + trackId.ToString("N");
        string input = prefix + "Input", gain = prefix + "Gain", sum = prefix + "Sum";
        var connections = destination.Inputs.ToArray(); connections[inputIndex] = sum;
        var expanded = new AudioGraphDefinition(graph.Nodes.Select(n => n.Id == destinationNodeId
            ? new AudioGraphNode(n.Id, n.DeviceId, n.Version, connections, n.Parameters, n.Bypassed) : n)
            .Concat([
                new AudioGraphNode(input, "flow.input", 1, [], new Dictionary<string, double> { ["bus"] = bus }),
                new AudioGraphNode(gain, "flow.gain", 1, [input]),
                new AudioGraphNode(sum, "flow.sum", 1, [destination.Inputs[inputIndex], gain])]), graph.OutputId);
        return BeginGraphEdit(document, before, owner, expanded, bindingId, track, "Add track and Flow routing");
    }

    public static MixerEditRequest BeginSetParameter(ProjectDocument document, string nodeId, string parameterId, double value)
    {
        var (before, owner, bindingId, graph) = Selected(document);
        var node = graph.Nodes.SingleOrDefault(n => n.Id == nodeId) ?? throw new ArgumentException("Unknown device node");
        if (node.DeviceId == "flow.input") throw new ArgumentException("Input bus changes require an explicit routing operation");
        if (before.Automation.Any(l => l.GraphBinding == bindingId && l.NodeId == nodeId && l.ParameterId == parameterId))
            throw new InvalidOperationException("Edit or remove automation before changing its parameter directly");
        var parameters = node.Parameters.ToDictionary(p => p.Key, p => p.Value); parameters[parameterId] = value;
        var changed = new AudioGraphNode(node.Id, node.DeviceId, node.Version, node.Inputs, parameters, node.Bypassed);
        var edited = new AudioGraphDefinition(graph.Nodes.Select(n => n.Id == nodeId ? changed : n), graph.OutputId);
        return BeginGraphEdit(document, before, owner, edited, bindingId, null, "Set device parameter");
    }

    public static MixerEditRequest BeginInsertEffect(ProjectDocument document, string nodeId, string deviceId,
        string destinationNodeId, int inputIndex = 0, IReadOnlyDictionary<string, double>? parameters = null, int version = 1)
    {
        var (before, owner, binding, graph) = Selected(document);
        var destination = graph.Nodes.SingleOrDefault(n => n.Id == destinationNodeId) ?? throw new ArgumentException("Unknown destination node");
        if (inputIndex < 0 || inputIndex >= destination.Inputs.Count) throw new ArgumentOutOfRangeException(nameof(inputIndex));
        var effect = new AudioGraphNode(nodeId, deviceId, version, [destination.Inputs[inputIndex]], parameters);
        var connections = destination.Inputs.ToArray(); connections[inputIndex] = nodeId;
        var edited = new AudioGraphDefinition(graph.Nodes.Select(n => n.Id == destinationNodeId
            ? new AudioGraphNode(n.Id, n.DeviceId, n.Version, connections, n.Parameters, n.Bypassed) : n).Append(effect), graph.OutputId);
        return BeginGraphEdit(document, before, owner, edited, binding, null, "Insert effect");
    }

    public static MixerEditRequest BeginSetBypass(ProjectDocument document, string nodeId, bool bypassed)
    {
        var (before, owner, binding, graph) = Selected(document);
        var node = graph.Nodes.SingleOrDefault(n => n.Id == nodeId) ?? throw new ArgumentException("Unknown device node");
        if (!DeviceCatalog.Get(node.DeviceId, node.Version).CanBypass) throw new ArgumentException("Device does not support bypass");
        var changed = new AudioGraphNode(node.Id, node.DeviceId, node.Version, node.Inputs, node.Parameters, bypassed);
        return BeginGraphEdit(document, before, owner,
            new(graph.Nodes.Select(n => n.Id == nodeId ? changed : n), graph.OutputId), binding, null, "Set effect bypass");
    }

    public static MixerEditRequest BeginRemoveEffect(ProjectDocument document, string nodeId)
    {
        var (before, owner, binding, graph) = Selected(document);
        var node = graph.Nodes.SingleOrDefault(n => n.Id == nodeId) ?? throw new ArgumentException("Unknown device node");
        if (node.Inputs.Count != 1) throw new ArgumentException("Only single-input effects can be removed by this gesture");
        string input = node.Inputs[0];
        var edited = new AudioGraphDefinition(graph.Nodes.Where(n => n.Id != nodeId).Select(n =>
            n.Inputs.Contains(nodeId) ? new AudioGraphNode(n.Id, n.DeviceId, n.Version,
                n.Inputs.Select(id => id == nodeId ? input : id), n.Parameters, n.Bypassed) : n),
            graph.OutputId == nodeId ? input : graph.OutputId);
        return BeginGraphEdit(document, before, owner, edited, binding, null, "Remove effect and automation", nodeId);
    }

    /// <summary>Reorder one contiguous serial chain. Intermediate fan-out is
    /// rejected because moving it would also change a parallel route.</summary>
    public static MixerEditRequest BeginReorderEffects(ProjectDocument document, IReadOnlyList<string> order)
    {
        var (before, owner, binding, graph) = Selected(document);
        if (order.Count is < 2 or > 1024 || order.Distinct(StringComparer.Ordinal).Count() != order.Count)
            throw new ArgumentException("Select at least two distinct serial effects");
        var selected = order.Select(id => graph.Nodes.SingleOrDefault(n => n.Id == id)
            ?? throw new ArgumentException("Unknown effect node")).ToDictionary(n => n.Id);
        if (selected.Values.Any(n => n.Inputs.Count != 1)) throw new ArgumentException("Only single-input effects can be reordered");
        var heads = selected.Values.Where(n => !selected.ContainsKey(n.Inputs[0])).ToArray();
        var usedInputs = selected.Values.Select(n => n.Inputs[0]).ToHashSet();
        var tails = selected.Values.Where(n => !usedInputs.Contains(n.Id)).ToArray();
        if (heads.Length != 1 || tails.Length != 1) throw new ArgumentException("Effects must form one contiguous chain");
        string headInput = heads[0].Inputs[0], tail = tails[0].Id;
        var visited = new HashSet<string>(); string cursor = tail;
        while (selected.TryGetValue(cursor, out var node) && visited.Add(cursor)) cursor = node.Inputs[0];
        if (visited.Count != selected.Count || cursor != headInput) throw new ArgumentException("Effects must form one contiguous chain");
        if (graph.Nodes.Where(n => !selected.ContainsKey(n.Id)).Any(n => n.Inputs.Any(id => selected.ContainsKey(id) && id != tail)) ||
            (selected.ContainsKey(graph.OutputId) && graph.OutputId != tail))
            throw new ArgumentException("Intermediate effect fan-out requires explicit graph routing");
        var positions = order.Select((id, index) => (id, index)).ToDictionary(p => p.id, p => p.index);
        string newTail = order[^1];
        var edited = new AudioGraphDefinition(graph.Nodes.Select(n =>
        {
            if (positions.TryGetValue(n.Id, out int index))
                return new AudioGraphNode(n.Id, n.DeviceId, n.Version, [index == 0 ? headInput : order[index - 1]], n.Parameters, n.Bypassed);
            return n.Inputs.Contains(tail) ? new AudioGraphNode(n.Id, n.DeviceId, n.Version,
                n.Inputs.Select(id => id == tail ? newTail : id), n.Parameters, n.Bypassed) : n;
        }), graph.OutputId == tail ? newTail : graph.OutputId);
        return BeginGraphEdit(document, before, owner, edited, binding, null, "Reorder effects");
    }

    private static (ProjectSnapshot, ProjectSource, Guid, AudioGraphDefinition) Selected(ProjectDocument document)
    {
        var before = document.Snapshot;
        var bindingId = before.Routing.GraphBinding ?? throw new InvalidOperationException("No selected graph");
        var owner = before.Sources.Values.SingleOrDefault(s => s.Bindings.Any(b => b.Id == bindingId && b.Available && b.Output.Role == GeneratedRole.Graph))
            ?? throw new InvalidOperationException("Selected graph is unavailable");
        var layer = owner.Bindings.Single(b => b.Id == bindingId).Output.LayerId;
        var graph = owner.Result.GraphLayers.Single(l => l.Id == layer).Graph;
        if (owner.Plugin is { } plugin)
            graph = plugin.ValidateEffect(owner.Result, plugin.Manifest.MinimumSampleRate, plugin.Manifest.MaximumBlockFrames).ApplyValues(owner.PluginValues);
        return (before, owner, bindingId, graph);
    }

    private static MixerEditRequest BeginGraphEdit(ProjectDocument document, ProjectSnapshot before, ProjectSource owner,
        AudioGraphDefinition graph, Guid bindingId, ProjectTrack? addedTrack, string description, string? removedNode = null)
    {
        var lines = FlowGraphExporter.Export(graph).TrimEnd().Split('\n');
        string code = "use \"@flowDaw\"\nproc generate (Dict<String, Double>: context)\n" +
            string.Join('\n', lines.Skip(1).SkipLast(1).Select(line => "    " + line)) +
            "\n    (dawResult \"mixer\" " + lines[^1] + ")\nend proc\n";
        var descriptor = owner.IsManagedGraph ? owner.Descriptor : new GeneratorDescriptor(1, Guid.NewGuid(), "generate");
        var ticket = document.BeginBuild(descriptor, code);
        return new(before, ticket, addedTrack, bindingId, description, removedNode);
    }

    public static PreparedMixerEdit Prepare(MixerEditRequest request, CancellationToken cancellation = default)
    {
        var ticket = request.Ticket;
        // Only exporter-produced construction code is evaluated here; arbitrary
        // user edits must use the isolated generator host instead.
        var result = FlowDawGenerator.Build(new(ticket.Descriptor, ticket.Revision, ticket.Code, ticket.Context, TimeSpan.FromSeconds(20)), cancellation);
        cancellation.ThrowIfCancellationRequested();
        if (result.Status != JobStatus.Succeeded || result.Value is null)
            throw new InvalidOperationException(result.Error ?? "Mixer graph preparation failed");
        return new(request, result.Value);
    }

    public static bool Commit(ProjectDocument document, PreparedMixerEdit prepared)
    {
        var request = prepared.Request;
        if (!ReferenceEquals(document.Snapshot, request.Before))
        { document.DiscardBuild(request.Ticket); return false; }
        try
        {
            return document.Accept(request.Ticket, prepared.Output, request.Description, p =>
            {
                var source = p.Sources[request.Ticket.Descriptor.SourceId];
                var graph = source.Bindings.Single(b => b.Available && b.Output == new OutputKey(GeneratedRole.Graph, "mixer")).Id;
                var automation = p.Automation.Where(l => !(l.GraphBinding == request.PreviousGraph && l.NodeId == request.RemovedNode)).Select(l => l.GraphBinding == request.PreviousGraph
                    ? new ProjectAutomationLane(l.Id, graph, l.NodeId, l.ParameterId, l.Points, l.Shape, l.TargetKind) : l);
                return new(p.Arrangement, p.Context, p.Sources.Values,
                    new(request.AddedTrack is { } track ? p.Routing.Tracks.Append(track) : p.Routing.Tracks, graph, p.Routing.Effects), p.Assets, automation, p.RenderSettings);
            }, managedGraph: true);
        }
        finally { document.DiscardBuild(request.Ticket); }
    }
}
