using System.Text.Json;
using System.Text.Json.Serialization;
namespace Flow.Studio.Model;

public sealed record PluginDependencyContent(string Id, string Version, string Base64);
/// <summary>Immutable, hash-verified plugin source and dependency snapshot. Loading
/// performs no code execution or external dependency lookup.</summary>
public sealed class PluginPackage
{
    public const int MaxCharacters = 16 * 1024 * 1024;
    public PluginManifest Manifest { get; }
    public string Source { get; }
    public string OutputLayer { get; }
    public IReadOnlyList<PluginParameterTarget> Targets { get; }
    public IReadOnlyList<PluginDependencyContent> Dependencies { get; }
    private sealed record Data(int Version, string Manifest, string Source, string OutputLayer,
        PluginParameterTarget[] Targets, PluginDependencyContent[] Dependencies);
    private static readonly JsonSerializerOptions Options = new()
    { RespectRequiredConstructorParameters = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 16 };
    public PluginPackage(PluginManifest manifest, string source, string outputLayer,
        IEnumerable<PluginParameterTarget> targets, IEnumerable<PluginDependencyContent>? dependencies = null)
    {
        ArgumentNullException.ThrowIfNull(manifest); ArgumentNullException.ThrowIfNull(targets);
        manifest.ValidateSource(source); PluginManifest.CheckId(outputLayer);
        var bindings = targets.Take(4097).ToArray();
        if (bindings.Length > 4096 || bindings.Any(t => t is null) ||
            bindings.Select(t => (t.NodeId, t.DeviceParameterId)).Distinct().Count() != bindings.Length)
            throw new ArgumentException("Invalid plugin parameter targets");
        foreach (var t in bindings)
        {
            PluginManifest.CheckId(t.NodeId); PluginManifest.CheckId(t.DeviceParameterId);
            if (!manifest.Parameters.Any(p => p.Id == t.ParameterId)) throw new ArgumentException("Undeclared plugin parameter");
        }
        if (manifest.Kind is FlowPluginKind.NoteTransform or FlowPluginKind.OfflineAudio)
        {
            if (bindings.Length != 0) throw new ArgumentException("Offline processors consume public parameters through their context, not graph targets");
            if (manifest.Parameters.Any(p => !p.RequiresRebuild || p.SmoothingMilliseconds != 0))
                throw new ArgumentException("Processor parameters require reprocessing and cannot advertise live smoothing");
        }
        else if (manifest.Parameters.Any(p => !bindings.Any(t => t.ParameterId == p.Id))) throw new ArgumentException("Missing plugin parameter target");
        var contents = (dependencies ?? []).Take(257).ToArray();
        if (contents.Length != manifest.Dependencies.Count || contents.Any(d => d is null) ||
            contents.Select(d => d.Id).Distinct().Count() != contents.Length) throw new ArgumentException("Missing or duplicate dependency snapshot");
        long encoded = 0;
        foreach (var d in contents)
        {
            if (d.Base64 is null || (encoded += d.Base64.Length) > 12 * 1024 * 1024) throw new ArgumentException("Dependency snapshot budget exceeded");
            manifest.ValidateDependency(d.Id, d.Version, Convert.FromBase64String(d.Base64));
        }
        Manifest = manifest; Source = source; OutputLayer = outputLayer;
        Targets = Array.AsReadOnly(bindings); Dependencies = Array.AsReadOnly(contents);
        if (Serialize().Length > MaxCharacters) throw new ArgumentException("Plugin package exceeds interchange budget");
    }
    public string Serialize() => JsonSerializer.Serialize(new Data(1, Manifest.Serialize(), Source, OutputLayer, Targets.ToArray(), Dependencies.ToArray()), Options);
    public static PluginPackage Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (json.Length > MaxCharacters) throw new ArgumentException("Plugin package exceeds interchange budget");
        using var document = JsonDocument.Parse(json, new() { MaxDepth = 16 });
        PluginManifest.RejectDuplicates(document.RootElement);
        var data = JsonSerializer.Deserialize<Data>(json, Options) ?? throw new JsonException("Missing plugin package");
        if (data.Version != 1 || data.Dependencies is null) throw new JsonException("Unsupported/incomplete plugin package");
        return new(PluginManifest.Parse(data.Manifest), data.Source, data.OutputLayer, data.Targets, data.Dependencies);
    }
    public PluginInstrumentContract ValidateInstrument(GeneratedSourceOutput output, int sampleRate, int blockFrames)
    {
        if (output.InstrumentLayers.Count != 1 || output.InstrumentLayers[0].Id != OutputLayer || output.ScoreLayers.Count != 0 ||
            output.AudioLayers.Count != 0 || output.GraphLayers.Count != 0) throw new ArgumentException("Plugin build must return its declared instrument layer only");
        return new(Manifest, output.InstrumentLayers[0].Instrument, Targets, sampleRate, blockFrames);
    }
    public void ValidateOutput(GeneratedSourceOutput output, int sampleRate, int blockFrames)
    {
        if (Manifest.Kind == FlowPluginKind.Instrument) ValidateInstrument(output, sampleRate, blockFrames);
        else if (Manifest.Kind == FlowPluginKind.AudioEffect) ValidateEffect(output, sampleRate, blockFrames);
        else if (Manifest.Kind == FlowPluginKind.OfflineAudio)
        {
            Manifest.ValidateProcessing(sampleRate, blockFrames);
            if (output.AudioLayers.Count != 1 || output.AudioLayers[0].Id != OutputLayer || output.AudioLayers[0].Asset.SampleRate != sampleRate ||
                output.ScoreLayers.Count != 0 || output.GraphLayers.Count != 0 || output.InstrumentLayers.Count != 0)
                throw new ArgumentException("Offline audio processor must return its declared audio layer at the input sample rate");
        }
        else if (Manifest.Kind == FlowPluginKind.NoteTransform)
        {
            if (output.ScoreLayers.Count != 1 || output.ScoreLayers[0].Id != OutputLayer || output.AudioLayers.Count != 0 ||
                output.GraphLayers.Count != 0 || output.InstrumentLayers.Count != 0)
                throw new ArgumentException("Note processor must return its declared note layer only");
            var score = output.ScoreLayers[0].Composition;
            if (score.Placements.Count != 1 || score.Placements[0].RepeatCount != 1 || score.Placements[0].Section.Sequences.Count != 1)
                throw new ArgumentException("Note processor result requires one detached note window");
            var sequence = score.Placements[0].Section.Sequences[0];
            _ = new PluginNoteInput(sequence.DurationQuarters, sequence.Notes);
        }
        else throw new NotSupportedException("Unsupported plugin kind");
    }
    public PluginEffectContract ValidateEffect(GeneratedSourceOutput output, int sampleRate, int blockFrames)
    {
        if (output.GraphLayers.Count != 1 || output.GraphLayers[0].Id != OutputLayer || output.ScoreLayers.Count != 0 ||
            output.AudioLayers.Count != 0 || output.InstrumentLayers.Count != 0) throw new ArgumentException("Plugin build must return its declared effect layer only");
        return new(Manifest, output.GraphLayers[0].Graph, Targets, sampleRate, blockFrames);
    }
}
