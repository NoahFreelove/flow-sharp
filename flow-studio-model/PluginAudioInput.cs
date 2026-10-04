using Flow.Audio;

namespace Flow.Studio.Model;

/// <summary>Detached offline processing input. Buses share a stereo format and
/// frame window; construction never resolves project references or reads files.</summary>
public sealed class PluginAudioInput
{
    public IReadOnlyList<PcmAsset> Buses { get; }
    public int SampleRate => Buses[0].SampleRate;
    public int Frames => Buses[0].Frames;
    public PluginAudioInput(IEnumerable<PcmAsset> buses)
    {
        ArgumentNullException.ThrowIfNull(buses);
        var copy = buses.Take(33).ToArray();
        if (copy.Length is < 1 or > 32 || copy.Any(b => b is null) || copy.Sum(b => b.Bytes) > 16L * 1024 * 1024 ||
            copy.Any(b => b.SampleRate != copy[0].SampleRate || b.Frames != copy[0].Frames))
            throw new ArgumentException("Offline inputs require 1–32 matching stereo buses within 16 MiB");
        Buses = Array.AsReadOnly(copy);
    }
    // Reuse exact bounded PCM interchange; these envelope identities are not project identities.
    private static readonly Guid EnvelopeId = new("00000000-0000-0000-0000-000000000001");
    private static GenerationContext EnvelopeContext() => new(0, 0, new([new(0, 120)]), new([new(1, 4, 4)]));
    public string Serialize() => GeneratedContentJson.Serialize(new(EnvelopeId, 0, EnvelopeContext(), [],
        Buses.Select((bus, index) => new GeneratedAudioLayer(index.ToString(System.Globalization.CultureInfo.InvariantCulture), bus))));
    public static PluginAudioInput Deserialize(string json)
    {
        var output = GeneratedContentJson.Deserialize(json, EnvelopeId, 0, EnvelopeContext());
        if (output.ScoreLayers.Count != 0 || output.GraphLayers.Count != 0 || output.InstrumentLayers.Count != 0 ||
            output.AudioLayers.Where((layer, index) => layer.Id != index.ToString(System.Globalization.CultureInfo.InvariantCulture)).Any())
            throw new ArgumentException("Invalid offline audio input envelope");
        return new(output.AudioLayers.Select(l => l.Asset));
    }
}
