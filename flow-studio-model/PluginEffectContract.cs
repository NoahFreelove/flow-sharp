using Flow.Audio.Graph;
namespace Flow.Studio.Model;

public sealed record PluginParameterTarget(string ParameterId, string NodeId, string DeviceParameterId);

/// <summary>Worker-side validation of an effect build against discovery metadata.
/// Bindings use stable public IDs, not display labels or parameter positions.
/// This initial mapping is direct in typed units; nonlinear signal handles and
/// user-defined DSP primitives are a separate graph authoring layer.</summary>
public sealed class PluginEffectContract
{
    public PluginManifest Manifest { get; }
    public AudioGraphDefinition Graph { get; }
    public IReadOnlyList<PluginParameterTarget> Targets { get; }
    public PluginEffectContract(PluginManifest manifest, AudioGraphDefinition graph,
        IEnumerable<PluginParameterTarget> targets, int sampleRate, int blockFrames)
        : this(manifest, graph, targets, sampleRate, blockFrames, false) { }
    internal PluginEffectContract(PluginManifest manifest, AudioGraphDefinition graph,
        IEnumerable<PluginParameterTarget> targets, int sampleRate, int blockFrames, bool instrument)
    {
        ArgumentNullException.ThrowIfNull(manifest); ArgumentNullException.ThrowIfNull(graph); ArgumentNullException.ThrowIfNull(targets);
        manifest.ValidateProcessing(sampleRate, blockFrames);
        if (manifest.Kind != (instrument ? FlowPluginKind.Instrument : FlowPluginKind.AudioEffect)) throw new ArgumentException("Expected an audio effect manifest");
        var nodes = graph.GetProcessingOrder().ToDictionary(n => n.Id, StringComparer.Ordinal);
        var buses = nodes.Values.Where(n => n.DeviceId == "flow.input").Select(n => (int)n.Parameters["bus"]).Distinct().Order().ToArray();
        if (instrument ? buses.Any(bus => bus > 2) : !buses.SequenceEqual(Enumerable.Range(0, manifest.AudioInputs))) throw new ArgumentException("Built input ports differ from manifest");
        var bindings = targets.Take(4097).ToArray();
        if (bindings.Length > 4096 || bindings.Any(t => t is null) ||
            bindings.Select(t => (t.NodeId, t.DeviceParameterId)).Distinct().Count() != bindings.Length)
            throw new ArgumentException("Invalid or ambiguous plugin parameter targets");
        var parameters = manifest.Parameters.ToDictionary(p => p.Id, StringComparer.Ordinal);
        foreach (var binding in bindings)
        {
            if (!parameters.TryGetValue(binding.ParameterId, out var parameter) || !nodes.TryGetValue(binding.NodeId, out var node))
                throw new ArgumentException("Unknown plugin parameter or target node");
            var device = DeviceCatalog.Get(node.DeviceId, node.Version);
            var target = device.Parameters.SingleOrDefault(p => p.Id == binding.DeviceParameterId)
                ?? throw new ArgumentException("Unknown target parameter");
            if (node.DeviceId == "flow.sample" && target.Id == "asset") throw new ArgumentException("Sample resource slots cannot be exposed as numeric controls");
            if (node.DeviceId == "flow.input") throw new ArgumentException("Ports cannot be exposed as plugin parameters");
            if ((node.DeviceId != "flow.value" && parameter.Unit != target.Unit) || parameter.Minimum < target.Minimum || parameter.Maximum > target.Maximum ||
                parameter.Default != node.Parameters[target.Id] || (!parameter.RequiresRebuild && !target.Automatable) ||
                (!parameter.RequiresRebuild && parameter.SmoothingMilliseconds != target.SmoothingMilliseconds))
                throw new ArgumentException("Built parameter behavior differs from manifest");
            if (node.DeviceId == "flow.delay" && target.Id == "repeats" && parameter.Scale != PluginParameterScale.Enumeration)
                throw new ArgumentException("Integer graph parameters need a discrete authoring contract");
        }
        if (parameters.Keys.Any(id => !bindings.Any(t => t.ParameterId == id))) throw new ArgumentException("Unbound declared parameter");
        Manifest = manifest; Graph = graph; Targets = Array.AsReadOnly(bindings);
    }
    /// <summary>Prepare all public-value targets atomically on the worker. The
    /// accepted source graph remains the immutable default build for validation.</summary>
    public AudioGraphDefinition ApplyValues(IReadOnlyDictionary<string, double> values)
    {
        foreach (var pair in values)
        {
            var parameter = Manifest.Parameters.SingleOrDefault(p => p.Id == pair.Key)
                ?? throw new ArgumentException("Unknown public parameter");
            _ = parameter.ToNormalized(pair.Value);
        }
        var changes = Targets.Where(t => values.ContainsKey(t.ParameterId)).GroupBy(t => t.NodeId)
            .ToDictionary(g => g.Key, g => g.ToArray());
        return new(Graph.Nodes.Select(n =>
        {
            if (!changes.TryGetValue(n.Id, out var targets)) return n;
            var parameters = new Dictionary<string, double>(n.Parameters);
            foreach (var target in targets) parameters[target.DeviceParameterId] = values[target.ParameterId];
            return new AudioGraphNode(n.Id, n.DeviceId, n.Version, n.Inputs, parameters, n.Bypassed);
        }), Graph.OutputId);
    }

}
