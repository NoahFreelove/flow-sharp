using System.Collections.ObjectModel;
namespace Flow.Audio.Graph;

/// <summary>Immutable slot bindings. PCM is shared by voices; slot tables are copied.</summary>
public sealed class GraphSampleSet
{
    public IReadOnlyDictionary<int, PcmAsset> Assets { get; }
    public long Bytes { get; }
    public GraphSampleSet(IEnumerable<KeyValuePair<int, PcmAsset>> assets)
    {
        var pairs = assets.Take(257).ToArray();
        if (pairs.Length is < 1 or > 256 || pairs.Any(p => p.Key is < 0 or > 255 || p.Value is null || p.Value.Frames == 0))
            throw new ArgumentException("Invalid graph sample bindings");
        var copy = pairs.ToDictionary(p => p.Key, p => p.Value);
        // Count serialized slot payloads, including aliases, to bound interchange.
        Bytes = copy.Values.Sum(a => a.Bytes);
        if (Bytes > 16 * 1024 * 1024) throw new ArgumentException("Graph samples exceed 16 MiB");
        Assets = new ReadOnlyDictionary<int, PcmAsset>(copy);
    }
}
