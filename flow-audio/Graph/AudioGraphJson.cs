using System.Text.Json;
using System.Text.Json.Serialization;

namespace Flow.Audio.Graph;

public static class AudioGraphJson
{
    private sealed record Node(string Id, string DeviceId, int Version, string[] Inputs, Dictionary<string, double> Parameters, bool Bypassed);
    private sealed record Data(int Version, string OutputId, Node[] Nodes);
    private static readonly JsonSerializerOptions Options = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectRequiredConstructorParameters = true };
    public static string Serialize(AudioGraphDefinition graph)
    {
        graph.GetProcessingOrder();
        return JsonSerializer.Serialize(new Data(1, graph.OutputId, graph.Nodes.Select(n =>
            new Node(n.Id, n.DeviceId, n.Version, n.Inputs.ToArray(), n.Parameters.ToDictionary(p => p.Key, p => p.Value), n.Bypassed)).ToArray()), Options);
    }
    public static AudioGraphDefinition Deserialize(string json)
    {
        if (json.Length > 2 * 1024 * 1024) throw new JsonException("Graph exceeds interchange budget");
        var data = JsonSerializer.Deserialize<Data>(json, Options) ?? throw new JsonException("Missing graph");
        if (data.Version != 1 || data.Nodes is null || data.Nodes.Length is < 1 or > 1024) throw new JsonException("Invalid graph schema/budget");
        var graph = new AudioGraphDefinition(data.Nodes.Select(n => new AudioGraphNode(n.Id, n.DeviceId, n.Version, n.Inputs, n.Parameters, n.Bypassed)), data.OutputId);
        graph.GetProcessingOrder();
        return graph;
    }
}
