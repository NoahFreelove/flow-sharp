using System.Collections.ObjectModel;
using Flow.Audio;

namespace Flow.Studio.Model;

/// <summary>Captured host-owned PCM addressed by project identity, not file path.</summary>
public sealed class GeneratorAssets
{
    public const long MaxBytes = 8 * 1024 * 1024;
    public IReadOnlyDictionary<Guid, PcmAsset> Samples { get; }
    public GeneratorAssets(IEnumerable<KeyValuePair<Guid, PcmAsset>> samples)
    {
        var copy = samples.Take(33).ToArray();
        if (copy.Length > 32 || copy.Any(p => p.Key == Guid.Empty || p.Value is null) || copy.Sum(p => p.Value.Bytes) > MaxBytes)
            throw new ArgumentException("Generator assets require at most 32 identified samples within 8 MiB");
        Samples = new ReadOnlyDictionary<Guid, PcmAsset>(copy.ToDictionary(p => p.Key, p => p.Value));
    }
    private static readonly Guid EnvelopeId = new("00000000-0000-0000-0000-000000000002");
    private static GenerationContext Context() => new(0, 0, new([new(0, 120)]), new([new(1, 4, 4)]));
    public string Serialize() => GeneratedContentJson.Serialize(new(EnvelopeId, 0, Context(), [],
        Samples.OrderBy(p => p.Key).Select(p => new GeneratedAudioLayer(p.Key.ToString(), p.Value))));
    public static GeneratorAssets Deserialize(string json)
    {
        var data = GeneratedContentJson.Deserialize(json, EnvelopeId, 0, Context());
        if (data.ScoreLayers.Count != 0 || data.GraphLayers.Count != 0 || data.InstrumentLayers.Count != 0)
            throw new ArgumentException("Invalid generator asset envelope");
        return new(data.AudioLayers.Select(l => new KeyValuePair<Guid, PcmAsset>(Guid.Parse(l.Id), l.Asset)));
    }
}
