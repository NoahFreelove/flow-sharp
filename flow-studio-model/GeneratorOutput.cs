using System.Collections.ObjectModel;
using Flow.Music.Model;
using Flow.Audio;
using Flow.Audio.Graph;

namespace Flow.Studio.Model;

/// <summary>V1 generator entry manifest. Builds can return score, audio, graph and instrument layers.</summary>
public sealed record GeneratorDescriptor
{
    public int ApiVersion { get; }
    public Guid SourceId { get; }
    public string EntryPoint { get; }
    public GeneratorDescriptor(int apiVersion, Guid sourceId, string entryPoint)
    {
        if (apiVersion != 1) throw new ArgumentOutOfRangeException(nameof(apiVersion));
        SourceId = Validate.Id(sourceId, nameof(sourceId));
        if (string.IsNullOrEmpty(entryPoint) || !(char.IsAsciiLetter(entryPoint[0]) || entryPoint[0] == '_') ||
            entryPoint.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_'))
            throw new ArgumentException("Entry point must be an unqualified Flow identifier", nameof(entryPoint));
        ApiVersion = apiVersion;
        EntryPoint = entryPoint;
    }
}

/// <summary>Detached context for one build. Revision changes whenever project timing,
/// tuning, bindings or parameter context changes. V1 supplies numeric parameters,
/// initial tempo/meter and an explicit seed; tuned notes retain their resolved Hz.</summary>
public sealed class GenerationContext
{
    public long Revision { get; }
    public int Seed { get; }
    public ProjectTempoMap Tempo { get; }
    public ProjectMeterMap Meter { get; }
    public IReadOnlyDictionary<string, double> Parameters { get; }
    public ProjectTuning Tuning { get; }
    public GenerationContext(long revision, int seed, ProjectTempoMap tempo, ProjectMeterMap meter,
        IReadOnlyDictionary<string, double>? parameters = null, ProjectTuning? tuning = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(revision);
        ArgumentNullException.ThrowIfNull(tempo);
        ArgumentNullException.ThrowIfNull(meter);
        var copy = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var pair in parameters ?? new Dictionary<string, double>())
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pair.Key);
            copy.Add(pair.Key, Validate.Finite(pair.Value, nameof(parameters)));
        }
        if (copy.Count > 1024) throw new ArgumentException("Too many generator parameters", nameof(parameters));
        Revision = revision;
        Seed = seed;
        Tempo = tempo;
        Meter = meter;
        Parameters = new ReadOnlyDictionary<string, double>(copy);
        Tuning = tuning ?? new();
    }
}

public sealed record GeneratedAudioLayer(string Id, PcmAsset Asset);
public sealed record GeneratedGraphLayer(string Id, AudioGraphDefinition Graph);
public sealed record GeneratedInstrumentLayer(string Id, SineVoiceSettings Instrument);

public sealed record GeneratedScoreLayer(string Id, CompositionSnapshot Composition);

/// <summary>Immutable detached build output; no interpreter Values, scopes or closures.</summary>
public sealed class GeneratedSourceOutput
{
    public Guid SourceId { get; }
    public long SourceRevision { get; }
    public GenerationContext Context { get; }
    public IReadOnlyList<GeneratedScoreLayer> ScoreLayers { get; }
    public IReadOnlyList<GeneratedAudioLayer> AudioLayers { get; }
    public IReadOnlyList<GeneratedGraphLayer> GraphLayers { get; }
    public IReadOnlyList<GeneratedInstrumentLayer> InstrumentLayers { get; }
    public GeneratedSourceOutput(Guid sourceId, long sourceRevision, GenerationContext context,
        IEnumerable<GeneratedScoreLayer> scoreLayers, IEnumerable<GeneratedAudioLayer>? audioLayers = null,
        IEnumerable<GeneratedGraphLayer>? graphLayers = null, IEnumerable<GeneratedInstrumentLayer>? instrumentLayers = null)
    {
        SourceId = Validate.Id(sourceId, nameof(sourceId));
        ArgumentOutOfRangeException.ThrowIfNegative(sourceRevision);
        ArgumentNullException.ThrowIfNull(context);
        var layers = scoreLayers.ToArray();
        var audio = (audioLayers ?? []).ToArray();
        var graphs = (graphLayers ?? []).ToArray();
        var instruments = (instrumentLayers ?? []).ToArray();
        if (layers.Length + audio.Length + graphs.Length + instruments.Length is < 1 or > 32) throw new ArgumentException("A generator must return 1–32 layers", nameof(scoreLayers));
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var layer in layers)
        {
            ArgumentNullException.ThrowIfNull(layer);
            ArgumentException.ThrowIfNullOrWhiteSpace(layer.Id);
            ArgumentNullException.ThrowIfNull(layer.Composition);
            if (!ids.Add(layer.Id)) throw new ArgumentException("Duplicate layer identity", nameof(scoreLayers));
        }
        ids.Clear();
        long bytes = 0;
        foreach (var layer in audio)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(layer.Id);
            ArgumentNullException.ThrowIfNull(layer.Asset);
            if (!ids.Add(layer.Id)) throw new ArgumentException("Duplicate audio layer identity");
            bytes += layer.Asset.Bytes;
            if (bytes > 16 * 1024 * 1024) throw new ArgumentException("Generated inline audio exceeds 16 MiB budget");
        }
        ids.Clear();
        foreach (var layer in graphs)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(layer.Id);
            layer.Graph.GetProcessingOrder();
            if (!ids.Add(layer.Id)) throw new ArgumentException("Duplicate graph layer identity");
        }
        ids.Clear();
        foreach (var layer in instruments)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(layer.Id);
            layer.Instrument.Validate();
            bytes += (layer.Instrument.Sample?.Bytes ?? 0) + (layer.Instrument.GraphSamples?.Bytes ?? 0);
            if (bytes > 16 * 1024 * 1024) throw new ArgumentException("Generated inline audio/sampler data exceeds 16 MiB budget");
            if (!ids.Add(layer.Id)) throw new ArgumentException("Duplicate instrument layer identity");
        }
        AudioLayers = Array.AsReadOnly(audio);
        GraphLayers = Array.AsReadOnly(graphs);
        InstrumentLayers = Array.AsReadOnly(instruments);
        SourceRevision = sourceRevision;
        Context = context;
        ScoreLayers = Array.AsReadOnly(layers);
    }
}
