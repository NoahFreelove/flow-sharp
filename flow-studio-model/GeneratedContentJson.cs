using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;
using Flow.Audio;
using Flow.Audio.Graph;

namespace Flow.Studio.Model;

/// <summary>Detached generated contents for worker/project interchange. PCM is
/// little-endian IEEE float base64; inline audio is bounded to 16 MiB per result.</summary>
public static class GeneratedContentJson
{
    private sealed record Score(string Id, string Data);
    private sealed record Audio(string Id, int SampleRate, string Pcm);
    private sealed record Graph(string Id, string Data);
    private sealed record SampleAsset(int Slot, int SampleRate, string Pcm);
    private sealed record Instrument(string Id, string Device, int Version, int Voices, double Attack, double Release, int? SampleRate = null, string? Pcm = null, double RootHz = 440, string? VoiceGraph = null, SampleAsset[]? Samples = null);
    private sealed record Data(int Version, Score[] Scores, Audio[] Audio, Graph[] Graphs, Instrument[] Instruments);
    private static readonly JsonSerializerOptions Options = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectRequiredConstructorParameters = true, MaxDepth = 32 };
    public static string Serialize(GeneratedSourceOutput output)
    {
        var data = new Data(4, output.ScoreLayers.Select(l => new Score(l.Id, CompositionJson.Serialize(l.Composition))).ToArray(),
            output.AudioLayers.Select(l => new Audio(l.Id, l.Asset.SampleRate, Encode(l.Asset))).ToArray(),
            output.GraphLayers.Select(l => new Graph(l.Id, AudioGraphJson.Serialize(l.Graph))).ToArray(),
            output.InstrumentLayers.Select(l => new Instrument(l.Id, l.Instrument.VoiceGraph is not null ? "flow.graphInstrument" : l.Instrument.Sample is null ? "flow.sine" : "flow.sampler", 1, l.Instrument.VoiceLimit,
                l.Instrument.AttackMilliseconds, l.Instrument.ReleaseMilliseconds, l.Instrument.Sample?.SampleRate,
                l.Instrument.Sample is null ? null : Encode(l.Instrument.Sample), l.Instrument.RootFrequencyHz, l.Instrument.VoiceGraph is null ? null : AudioGraphJson.Serialize(l.Instrument.VoiceGraph), l.Instrument.GraphSamples?.Assets.OrderBy(p => p.Key).Select(p => new SampleAsset(p.Key, p.Value.SampleRate, Encode(p.Value))).ToArray())).ToArray());
        string json = JsonSerializer.Serialize(data, Options);
        if (json.Length > 36 * 1024 * 1024) throw new InvalidDataException("Generated content exceeds interchange budget");
        return json;
    }
    public static GeneratedSourceOutput Deserialize(string json, Guid sourceId, long revision, GenerationContext context)
    {
        if (json.Length > 36 * 1024 * 1024) throw new JsonException("Generated content exceeds interchange budget");
        var data = JsonSerializer.Deserialize<Data>(json, Options) ?? throw new JsonException("Missing generated content");
        if (data.Version is not (1 or 2 or 3 or 4) || data.Scores is null || data.Audio is null || data.Graphs is null || data.Instruments is null ||
            data.Scores.Length + data.Audio.Length + data.Graphs.Length + data.Instruments.Length is < 1 or > 32)
            throw new JsonException("Invalid generated content schema");
        long bytes = 0;
        var audio = new List<GeneratedAudioLayer>();
        foreach (var layer in data.Audio)
        {
            bytes += (long)layer.Pcm.Length / 4 * 3;
            if (bytes > 16 * 1024 * 1024 + 64) throw new JsonException("Inline audio budget exceeded");
            var raw = Convert.FromBase64String(layer.Pcm);
            if (raw.Length % 8 != 0) throw new JsonException("Audio must contain stereo float frames");
            var samples = new float[raw.Length / 4];
            for (int i = 0; i < samples.Length; i++) samples[i] = BinaryPrimitives.ReadSingleLittleEndian(raw.AsSpan(i * 4, 4));
            audio.Add(new(layer.Id, new(samples, layer.SampleRate, 16 * 1024 * 1024)));
        }
        return new(sourceId, revision, context,
            data.Scores.Select(l => new GeneratedScoreLayer(l.Id, CompositionJson.Deserialize(l.Data))), audio,
            data.Graphs.Select(l => new GeneratedGraphLayer(l.Id, AudioGraphJson.Deserialize(l.Data))),
            data.Instruments.Select(l =>
            {
                if (l.Version != 1 || l.Device is not ("flow.sine" or "flow.sampler" or "flow.graphInstrument")) throw new JsonException("Unknown instrument/version");
                if (l.Device == "flow.graphInstrument" && (data.Version < 3 || l.VoiceGraph is null)) throw new JsonException("Missing or unsupported voice graph");
                if (l.Device != "flow.graphInstrument" && l.VoiceGraph is not null) throw new JsonException("Unexpected voice graph");
                if (l.Samples is not null && (data.Version < 4 || l.Device != "flow.graphInstrument" || l.Samples.Length is < 1 or > 256))
                    throw new JsonException("Unsupported graph sample bindings");
                GraphSampleSet? graphSamples = null;
                if (l.Samples is not null)
                {
                    var assets = new List<KeyValuePair<int, PcmAsset>>();
                    foreach (var asset in l.Samples)
                    {
                        bytes += (long)asset.Pcm.Length / 4 * 3;
                        if (bytes > 16 * 1024 * 1024 + 1024) throw new JsonException("Inline sample budget exceeded");
                        var raw = Convert.FromBase64String(asset.Pcm);
                        if (raw.Length % 8 != 0) throw new JsonException("Graph samples require stereo float frames");
                        var pcm = new float[raw.Length / 4];
                        for (int i = 0; i < pcm.Length; i++) pcm[i] = BinaryPrimitives.ReadSingleLittleEndian(raw.AsSpan(i * 4, 4));
                        assets.Add(new(asset.Slot, new PcmAsset(pcm, asset.SampleRate, 16 * 1024 * 1024)));
                    }
                    graphSamples = new(assets);
                }
                PcmAsset? sample = null;
                if (l.Device == "flow.sampler")
                {
                    if (l.Pcm is null || l.SampleRate is null) throw new JsonException("Missing sampler asset");
                    bytes += (long)l.Pcm.Length / 4 * 3;
                    if (bytes > 16 * 1024 * 1024 + 128) throw new JsonException("Inline sampler budget exceeded");
                    var raw = Convert.FromBase64String(l.Pcm);
                    if (raw.Length % 8 != 0) throw new JsonException("Sampler requires stereo float frames");
                    var samples = new float[raw.Length / 4];
                    for (int i = 0; i < samples.Length; i++) samples[i] = BinaryPrimitives.ReadSingleLittleEndian(raw.AsSpan(i * 4, 4));
                    sample = new(samples, l.SampleRate.Value, 16 * 1024 * 1024);
                }
                else if (l.Pcm is not null || l.SampleRate is not null) throw new JsonException("Sine cannot contain a sampler asset");
                return new GeneratedInstrumentLayer(l.Id, new(l.Voices, l.Attack, l.Release, sample, l.RootHz, l.VoiceGraph is null ? null : AudioGraphJson.Deserialize(l.VoiceGraph), graphSamples));
            }));
    }
    private static string Encode(PcmAsset asset)
    {
        var samples = new float[asset.Frames * 2]; asset.CopyTo(samples);
        var bytes = new byte[samples.Length * 4];
        for (int i = 0; i < samples.Length; i++) BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i * 4, 4), samples[i]);
        return Convert.ToBase64String(bytes);
    }
}
