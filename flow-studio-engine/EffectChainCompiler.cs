using System.Collections.ObjectModel;
using Flow.Audio.Graph;
using Flow.Studio.Model;

namespace Flow.Studio.Engine;

/// <summary>One accepted, single stereo-input effect instance on the preparation
/// worker. Saved public values are applied before graph composition.</summary>
public sealed record BoundEffect(Guid Binding, AudioGraphDefinition Graph, bool Bypassed = false)
{
    public static BoundEffect FromSource(ProjectSource source, int sampleRate, int blockFrames, bool bypassed = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        var plugin = source.Plugin ?? throw new ArgumentException("Expected a packaged effect");
        if (plugin.Manifest.Kind != FlowPluginKind.AudioEffect || plugin.Manifest.AudioInputs != 1)
            throw new ArgumentException("Serial inserts require a single stereo-input audio effect");
        var binding = source.Bindings.Single(b => b.Available && b.Output.Role == GeneratedRole.Graph && b.Output.LayerId == plugin.OutputLayer);
        return new(binding.Id, plugin.ValidateEffect(source.Result, sampleRate, blockFrames).ApplyValues(source.PluginValues), bypassed);
    }
}

/// <summary>Detached composed graph and source-node addressing. Bypassed devices
/// have no active node mapping; their definitions remain owned by the caller.</summary>
public sealed class ComposedEffectGraph
{
    public AudioGraphDefinition Graph { get; }
    public IReadOnlyDictionary<(Guid Binding, string Node), string> Nodes { get; }
    internal ComposedEffectGraph(AudioGraphDefinition graph, Dictionary<(Guid, string), string> nodes)
    { Graph = graph; Nodes = new ReadOnlyDictionary<(Guid, string), string>(nodes); }
    public GraphAutomationLane MapAutomation(Guid binding, GraphAutomationLane lane)
    {
        ArgumentNullException.ThrowIfNull(lane);
        if (!Nodes.TryGetValue((binding, lane.NodeId), out string? target))
            throw new ArgumentException("Effect target is not active in this composition");
        return new(target, lane.ParameterId, lane.Points, lane.Shape);
    }
}

/// <summary>Worker-only serial insert lowering into the ordinary Flow graph.
/// Track chains process raw buses once before all mixer branches; the master chain
/// processes the mixer output. No alternate callback or DSP implementation exists.</summary>
public static class EffectChainCompiler
{
    public static ComposedEffectGraph Compose(AudioGraphDefinition mixer,
        IReadOnlyDictionary<int, IReadOnlyList<BoundEffect>> trackEffects,
        IReadOnlyList<BoundEffect> masterEffects, CancellationToken cancellation = default, IReadOnlySet<int>? silentBuses = null)
    {
        ArgumentNullException.ThrowIfNull(mixer); ArgumentNullException.ThrowIfNull(trackEffects);
        ArgumentNullException.ThrowIfNull(masterEffects);
        cancellation.ThrowIfCancellationRequested();
        mixer.GetProcessingOrder();
        var buses = mixer.Nodes.Where(n => n.DeviceId == "flow.input").Select(n => (int)n.Parameters["bus"]).ToHashSet();
        if (silentBuses is not null && silentBuses.Any(b => !buses.Contains(b)))
            throw new ArgumentException("Mute gates require existing mixer input buses");
        if (trackEffects.Count > 64 || trackEffects.Any(p => !buses.Contains(p.Key) || p.Value is null))
            throw new ArgumentException("Track inserts require existing mixer input buses");
        var all = trackEffects.OrderBy(p => p.Key).SelectMany(p => p.Value).Concat(masterEffects).Take(257).ToArray();
        if (all.Length > 256 || all.Any(e => e is null || e.Binding == Guid.Empty || e.Graph is null) ||
            all.Select(e => e.Binding).Distinct().Count() != all.Length)
            throw new ArgumentException("Use at most 256 distinct effect instances; duplicate a device before inserting it twice");
        foreach (var effect in all)
        {
            cancellation.ThrowIfCancellationRequested(); effect.Graph.GetProcessingOrder();
            var inputs = effect.Graph.Nodes.Where(n => n.DeviceId == "flow.input").ToArray();
            if (inputs.Length == 0 || inputs.Any(n => n.Parameters["bus"] != 0))
                throw new ArgumentException("Serial effects require only stereo input bus zero");
        }
        var nodes = new List<AudioGraphNode>();
        var used = mixer.Nodes.Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
        var mapping = new Dictionary<(Guid, string), string>();
        int serial = 0;
        string Allocate()
        {
            string id;
            do { id = "effectNode" + serial++; } while (!used.Add(id));
            return id;
        }
        void Add(AudioGraphNode node)
        {
            if (nodes.Count >= 1024) throw new ArgumentException("Composed effects exceed the 1024-node graph budget");
            nodes.Add(node);
        }
        string Chain(string input, IReadOnlyList<BoundEffect> chain)
        {
            foreach (var effect in chain)
            {
                cancellation.ThrowIfCancellationRequested();
                if (effect.Bypassed) continue;
                var local = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var node in effect.Graph.Nodes.OrderBy(n => n.Id, StringComparer.Ordinal))
                    local.Add(node.Id, node.DeviceId == "flow.input" ? input : Allocate());
                foreach (var node in effect.Graph.Nodes.Where(n => n.DeviceId != "flow.input"))
                {
                    mapping.Add((effect.Binding, node.Id), local[node.Id]);
                    Add(new(local[node.Id], node.DeviceId, node.Version, node.Inputs.Select(i => local[i]), node.Parameters, node.Bypassed));
                }
                input = local[effect.Graph.OutputId];
            }
            return input;
        }
        var replacedBuses = new Dictionary<int, string>();
        foreach (int bus in trackEffects.Where(p => p.Value.Any(e => !e.Bypassed)).Select(p => p.Key)
            .Concat(silentBuses ?? (IEnumerable<int>)[]).Distinct().Order())
        {
            string raw = Allocate(); Add(new(raw, "flow.input", 1, [], new Dictionary<string, double> { ["bus"] = bus }));
            string processed = trackEffects.TryGetValue(bus, out var chain) ? Chain(raw, chain) : raw;
            if (silentBuses?.Contains(bus) == true)
            {
                string gate = Allocate();
                Add(new(gate, "flow.gain", 1, [processed], new Dictionary<string, double> { ["gain"] = 0 }));
                processed = gate;
            }
            replacedBuses.Add(bus, processed);
        }
        foreach (var node in mixer.Nodes)
        {
            if (node.DeviceId == "flow.input" && replacedBuses.TryGetValue((int)node.Parameters["bus"], out var processed))
                Add(new(node.Id, "flow.gain", 1, [processed]));
            else Add(node);
        }
        string output = Chain(mixer.OutputId, masterEffects);
        var graph = new AudioGraphDefinition(nodes, output); graph.GetProcessingOrder();
        return new(graph, mapping);
    }
}
