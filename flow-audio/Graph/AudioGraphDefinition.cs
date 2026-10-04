using System.Collections.ObjectModel;

namespace Flow.Audio.Graph;

public sealed class AudioGraphNode
{
    public string Id { get; }
    public string DeviceId { get; }
    public int Version { get; }
    public IReadOnlyList<string> Inputs { get; }
    public IReadOnlyDictionary<string, double> Parameters { get; }
    public bool Bypassed { get; }
    public AudioGraphNode(string id, string deviceId, int version, IEnumerable<string> inputs,
        IReadOnlyDictionary<string, double>? parameters = null, bool bypassed = false)
    {
        CheckId(id);
        var device = DeviceCatalog.Get(deviceId, version);
        var connections = inputs.ToArray();
        if (connections.Length < device.MinimumInputs || connections.Length > device.MaximumInputs)
            throw new ArgumentException($"Invalid input count for {deviceId}", nameof(inputs));
        foreach (string input in connections) CheckId(input);
        var values = device.Parameters.ToDictionary(p => p.Id, p => p.Default, StringComparer.Ordinal);
        foreach (var pair in parameters ?? new Dictionary<string, double>())
        {
            var parameter = device.Parameters.FirstOrDefault(p => p.Id == pair.Key)
                ?? throw new ArgumentException($"Unknown parameter {pair.Key}");
            parameter.Validate(pair.Value);
            values[pair.Key] = pair.Value;
        }
        if (deviceId == "flow.sample" && values["asset"] != Math.Truncate(values["asset"]))
            throw new ArgumentException("Sample asset index must be an integer");
        if (deviceId == "flow.input" && values["bus"] != Math.Truncate(values["bus"]))
            throw new ArgumentException("Input bus must be an integer");
        if (deviceId == "flow.delay" && values["repeats"] != Math.Truncate(values["repeats"]))
            throw new ArgumentException("Delay repeats must be an integer");
        if (bypassed && !device.CanBypass) throw new ArgumentException("Device does not support bypass");
        Id = id; DeviceId = deviceId; Version = version; Bypassed = bypassed;
        Inputs = Array.AsReadOnly(connections);
        Parameters = new ReadOnlyDictionary<string, double>(values);
    }
    internal static void CheckId(string id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > 128 || id.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('_' or '-' or '.')))
            throw new ArgumentException("Graph IDs must contain 1–128 ASCII letters, digits, underscores, hyphens or dots");
    }
}

/// <summary>Immutable explicit connections; input order is the sum order. Preparation
/// validates missing references/cycles; authoring itself never opens a device.</summary>
public sealed class AudioGraphDefinition
{
    public IReadOnlyList<AudioGraphNode> Nodes { get; }
    public string OutputId { get; }
    public AudioGraphDefinition(IEnumerable<AudioGraphNode> nodes, string outputId)
    {
        var array = nodes.ToArray();
        if (array.Length is < 1 or > 1024 || array.Any(n => n is null) || array.Select(n => n.Id).Distinct().Count() != array.Length)
            throw new ArgumentException("Graph needs 1–1024 nodes with unique IDs");
        AudioGraphNode.CheckId(outputId);
        if (!array.Any(n => n.Id == outputId)) throw new ArgumentException("Graph output is missing");
        Nodes = Array.AsReadOnly(array);
        OutputId = outputId;
    }

    public static AudioGraphDefinition Input(string id, int bus = 0) =>
        new([new(id, "flow.input", 1, [], new Dictionary<string, double> { ["bus"] = bus })], id);
    public static AudioGraphDefinition Value(string id, double value) =>
        new([new(id, "flow.value", 1, [], new Dictionary<string, double> { ["value"] = value })], id);
    public static AudioGraphDefinition Multiply(string id, AudioGraphDefinition left, AudioGraphDefinition right) =>
        Combine(id, "flow.multiply", [left, right]);
    public static AudioGraphDefinition LowPass(string id, AudioGraphDefinition input, AudioGraphDefinition cutoff) =>
        Combine(id, "flow.lowPass", [input, cutoff]);
    public static AudioGraphDefinition Sample(string id, AudioGraphDefinition frequency, AudioGraphDefinition gate, int asset, double rootHz = 440) =>
        Combine(id, "flow.sample", [frequency, gate], new Dictionary<string, double> { ["asset"] = asset, ["rootHz"] = rootHz });
    public AudioGraphDefinition Then(string id, string deviceId, IReadOnlyDictionary<string, double>? parameters = null,
        int version = 1, bool bypassed = false) =>
        new(Nodes.Append(new AudioGraphNode(id, deviceId, version, [OutputId], parameters, bypassed)), id);
    public static AudioGraphDefinition Mix(string id, AudioGraphDefinition left, AudioGraphDefinition right)
        => Sum(id, [left, right]);
    public static AudioGraphDefinition Sum(string id, IEnumerable<AudioGraphDefinition> inputs)
        => Combine(id, "flow.sum", inputs);
    private static AudioGraphDefinition Combine(string id, string device, IEnumerable<AudioGraphDefinition> inputs, IReadOnlyDictionary<string, double>? parameters = null)
    {
        var sources = inputs.Take(65).ToArray();
        if (sources.Length is < 2 or > 64) throw new ArgumentException("A combination needs 2–64 inputs");
        var merged = new Dictionary<string, AudioGraphNode>();
        foreach (var node in sources.SelectMany(s => s.Nodes))
        {
            if (merged.TryGetValue(node.Id, out var existing))
            {
                if (existing.DeviceId != node.DeviceId || existing.Version != node.Version || existing.Bypassed != node.Bypassed ||
                    !existing.Inputs.SequenceEqual(node.Inputs) || existing.Parameters.Count != node.Parameters.Count ||
                    existing.Parameters.Any(p => !node.Parameters.TryGetValue(p.Key, out double value) || value != p.Value))
                    throw new ArgumentException($"Conflicting shared node {node.Id}");
            }
            else merged.Add(node.Id, node);
        }
        return new(merged.Values.Append(new AudioGraphNode(id, device, 1, sources.Select(s => s.OutputId), parameters)), id);
    }
    public IReadOnlyList<AudioGraphNode> GetProcessingOrder()
    {
        var byId = Nodes.ToDictionary(n => n.Id);
        var visited = new Dictionary<string, int>();
        var sorted = new List<AudioGraphNode>();
        void Visit(string id)
        {
            if (!byId.TryGetValue(id, out var node)) throw new ArgumentException($"Missing graph node {id}");
            if (visited.TryGetValue(id, out int mark))
            {
                if (mark == 1) throw new ArgumentException("Audio graph contains a cycle");
                return;
            }
            visited[id] = 1;
            foreach (string input in node.Inputs) Visit(input);
            visited[id] = 2;
            sorted.Add(node);
        }
        Visit(OutputId);
        if (sorted.Count != Nodes.Count) throw new ArgumentException("Graph contains disconnected nodes");
        return sorted.AsReadOnly();
    }
}
