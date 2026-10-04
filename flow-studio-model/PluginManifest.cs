using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace Flow.Studio.Model;

public enum FlowPluginKind { Instrument, AudioEffect, NoteTransform, OfflineAudio }
public enum PluginStatePolicy { Reset }
public enum PluginParameterScale { Linear, Logarithmic, Enumeration }
public sealed record PluginDependency(string Id, string Version, string Sha256);

/// <summary>Detached discovery metadata. Parsing never evaluates Flow or resolves files.</summary>
public sealed class PluginManifest
{
    public const int CurrentApiVersion = 1;
    public string Id { get; }
    public string Name { get; }
    public string Author { get; }
    public string License { get; }
    public string Version { get; }
    public string SourceSha256 { get; }
    public string Builder { get; }
    public FlowPluginKind Kind { get; }
    public int AudioInputs { get; }
    public int AudioOutputs { get; }
    public bool NoteInput { get; }
    public bool NoteOutput { get; }
    public int MinimumSampleRate { get; }
    public int MaximumSampleRate { get; }
    public int MaximumBlockFrames { get; }
    public string StateSchema { get; }
    public PluginStatePolicy StatePolicy { get; }
    public IReadOnlyList<PluginParameter> Parameters { get; }
    public IReadOnlyList<PluginDependency> Dependencies { get; }
    private sealed record Wire(int ApiVersion, string Id, string Name, string Author, string License,
        string Version, string SourceSha256, string Builder, FlowPluginKind Kind, int AudioInputs,
        int AudioOutputs, bool NoteInput, bool NoteOutput, int MinimumSampleRate, int MaximumSampleRate,
        int MaximumBlockFrames, string StateSchema, PluginStatePolicy StatePolicy, PluginParameter[] Parameters, PluginDependency[] Dependencies);
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = false, RespectRequiredConstructorParameters = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 16, Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };
    private PluginManifest(Wire w)
    {
        if (w.ApiVersion != CurrentApiVersion) throw new ArgumentException("Unsupported plugin API version");
        CheckId(w.Id); CheckText(w.Name, 256); CheckText(w.Author, 256); CheckText(w.License, 1024);
        CheckText(w.Version, 128); CheckId(w.Builder);
        if (!(char.IsAsciiLetter(w.Builder[0]) || w.Builder[0] == '_') || w.Builder.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_'))
            throw new ArgumentException("Builder must be a Flow identifier"); CheckHash(w.SourceSha256); CheckText(w.StateSchema, 128);
        if (!Enum.IsDefined(w.StatePolicy) || !Enum.IsDefined(w.Kind) || w.AudioInputs is < 0 or > 64 || w.AudioOutputs is < 0 or > 1 ||
            w.MinimumSampleRate < 1 || w.MaximumSampleRate > 384000 || w.MinimumSampleRate > w.MaximumSampleRate ||
            w.MaximumBlockFrames is < 1 or > 65536) throw new ArgumentException("Invalid plugin ports or processing limits");
        // Audio ports are interleaved stereo buses; mono adaptation is explicit in a graph.
        bool validPorts = w.Kind switch
        {
            FlowPluginKind.AudioEffect => w.AudioInputs >= 1 && w.AudioOutputs == 1 && !w.NoteInput && !w.NoteOutput,
            FlowPluginKind.Instrument => w.AudioInputs == 0 && w.AudioOutputs == 1 && w.NoteInput && !w.NoteOutput,
            FlowPluginKind.NoteTransform => w.AudioInputs == 0 && w.AudioOutputs == 0 && w.NoteInput && w.NoteOutput,
            FlowPluginKind.OfflineAudio => w.AudioInputs >= 1 && w.AudioOutputs == 1 && !w.NoteInput && !w.NoteOutput,
            _ => false
        };
        if (!validPorts) throw new ArgumentException("Ports do not match plugin kind");
        if (w.Parameters is null || w.Parameters.Length > 256 || w.Parameters.Any(p => p is null) ||
            w.Parameters.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != w.Parameters.Length)
            throw new ArgumentException("Plugin parameters need unique stable IDs within the 256-parameter limit");
        foreach (var p in w.Parameters) p.Validate();
        if (w.Dependencies is null || w.Dependencies.Length > 256 || w.Dependencies.Any(d => d is null) ||
            w.Dependencies.Select(d => d.Id).Distinct(StringComparer.Ordinal).Count() != w.Dependencies.Length)
            throw new ArgumentException("Invalid dependency declarations");
        foreach (var d in w.Dependencies) { CheckId(d.Id); CheckText(d.Version, 128); CheckHash(d.Sha256); }
        Id = w.Id; Name = w.Name; Author = w.Author; License = w.License; Version = w.Version;
        SourceSha256 = w.SourceSha256; Builder = w.Builder; Kind = w.Kind;
        AudioInputs = w.AudioInputs; AudioOutputs = w.AudioOutputs; NoteInput = w.NoteInput; NoteOutput = w.NoteOutput;
        MinimumSampleRate = w.MinimumSampleRate; MaximumSampleRate = w.MaximumSampleRate; MaximumBlockFrames = w.MaximumBlockFrames;
        StateSchema = w.StateSchema; StatePolicy = w.StatePolicy; Parameters = Array.AsReadOnly(w.Parameters); Dependencies = Array.AsReadOnly(w.Dependencies);
    }
    public static PluginManifest Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (json.Length > 1024 * 1024) throw new ArgumentException("Plugin manifest exceeds 1 Mi characters");
        using var document = JsonDocument.Parse(json, new() { MaxDepth = 16 });
        RejectDuplicates(document.RootElement);
        return new(JsonSerializer.Deserialize<Wire>(json, Options) ?? throw new ArgumentException("Missing plugin manifest"));
    }
    public string Serialize() => JsonSerializer.Serialize(new Wire(CurrentApiVersion, Id, Name, Author, License,
        Version, SourceSha256, Builder, Kind, AudioInputs, AudioOutputs, NoteInput, NoteOutput,
        MinimumSampleRate, MaximumSampleRate, MaximumBlockFrames, StateSchema, StatePolicy, Parameters.ToArray(), Dependencies.ToArray()), Options);
    public void ValidateSource(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Length > 2 * 1024 * 1024) throw new ArgumentException("Plugin source exceeds 2 Mi characters");
        string actual = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
        if (!string.Equals(actual, SourceSha256, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Plugin source hash mismatch");
    }
    public void ValidateDependency(string id, string version, ReadOnlySpan<byte> content)
    {
        var dependency = Dependencies.SingleOrDefault(d => d.Id == id)
            ?? throw new ArgumentException("Undeclared plugin dependency");
        if (dependency.Version != version || !string.Equals(dependency.Sha256,
            Convert.ToHexStringLower(SHA256.HashData(content)), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Plugin dependency version/content differs from the pinned declaration");
    }
    public void ValidateProcessing(int sampleRate, int blockFrames)
    {
        if (sampleRate < MinimumSampleRate || sampleRate > MaximumSampleRate || blockFrames < 1 || blockFrames > MaximumBlockFrames)
            throw new ArgumentException("Host processing configuration is outside plugin limits");
    }
    internal static void RejectDuplicates(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in value.EnumerateObject())
            { if (!names.Add(p.Name)) throw new ArgumentException("Duplicate manifest field"); RejectDuplicates(p.Value); }
        }
        else if (value.ValueKind == JsonValueKind.Array) foreach (var item in value.EnumerateArray()) RejectDuplicates(item);
    }
    internal static void CheckId(string id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > 128 || id.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('_' or '-' or '.')))
            throw new ArgumentException("Invalid stable plugin ID");
    }
    internal static void CheckText(string text, int limit)
    { if (string.IsNullOrWhiteSpace(text) || text.Length > limit) throw new ArgumentException("Invalid plugin text field"); }
    private static void CheckHash(string hash)
    { if (hash is null || hash.Length != 64 || hash.Any(c => !char.IsAsciiHexDigit(c))) throw new ArgumentException("Expected SHA-256 content hash"); }
}

public sealed class PluginParameter
{
    public string Id { get; }
    public string Name { get; }
    public string Unit { get; }
    public double Minimum { get; }
    public double Maximum { get; }
    public double Default { get; }
    public PluginParameterScale Scale { get; }
    public double SmoothingMilliseconds { get; }
    public bool RequiresRebuild { get; }
    public IReadOnlyList<string> Labels { get; }
    [JsonConstructor]
    public PluginParameter(string id, string name, string unit, double minimum, double maximum, double @default,
        PluginParameterScale scale, double smoothingMilliseconds, bool requiresRebuild, IReadOnlyList<string> labels)
    {
        Id = id; Name = name; Unit = unit; Minimum = minimum; Maximum = maximum; Default = @default;
        Scale = scale; SmoothingMilliseconds = smoothingMilliseconds; RequiresRebuild = requiresRebuild;
        Labels = Array.AsReadOnly((labels ?? throw new ArgumentException("Missing parameter labels")).Take(257).ToArray()); Validate();
    }
    /// <summary>API-v1 normalized mapping; persistence/automation store typed values.</summary>
    public double FromNormalized(double value)
    {
        if (!double.IsFinite(value) || value is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(value));
        if (value == 0) return Minimum;
        if (value == 1) return Maximum;
        return Scale switch
        {
            PluginParameterScale.Logarithmic => Math.Exp(Math.Log(Minimum) + value * (Math.Log(Maximum) - Math.Log(Minimum))),
            PluginParameterScale.Enumeration => Minimum + Math.Round(value * (Maximum - Minimum), MidpointRounding.AwayFromZero),
            _ => Minimum + value * (Maximum - Minimum)
        };
    }
    public double ToNormalized(double value)
    {
        if (!double.IsFinite(value) || value < Minimum || value > Maximum ||
            (Scale == PluginParameterScale.Enumeration && value != Math.Truncate(value)))
            throw new ArgumentOutOfRangeException(nameof(value));
        if (value == Minimum) return 0;
        if (value == Maximum) return 1;
        return Scale == PluginParameterScale.Logarithmic
            ? (Math.Log(value) - Math.Log(Minimum)) / (Math.Log(Maximum) - Math.Log(Minimum))
            : (value - Minimum) / (Maximum - Minimum);
    }
    internal void Validate()
    {
        PluginManifest.CheckId(Id); PluginManifest.CheckText(Name, 256);
        if (Unit is null || Unit.Length > 64 || !double.IsFinite(Minimum) || !double.IsFinite(Maximum) || Minimum >= Maximum ||
            !double.IsFinite(Maximum - Minimum) || !double.IsFinite(Default) || Default < Minimum || Default > Maximum || !Enum.IsDefined(Scale) ||
            !double.IsFinite(SmoothingMilliseconds) || SmoothingMilliseconds is < 0 or > 10000 || Labels.Count > 256)
            throw new ArgumentException("Invalid plugin parameter range");
        if (Scale == PluginParameterScale.Logarithmic && (Minimum <= 0 || Math.Log(Maximum) <= Math.Log(Minimum))) throw new ArgumentException("Log range must be positive and distinguishable");
        if (Scale == PluginParameterScale.Enumeration)
        {
            if (Math.Abs(Minimum) > 9007199254740991d || Math.Abs(Maximum) > 9007199254740991d || Minimum != Math.Truncate(Minimum) || Maximum != Minimum + Labels.Count - 1 || Default != Math.Truncate(Default)) throw new ArgumentException("Enum values index labels relative to the minimum");
            foreach (string label in Labels) PluginManifest.CheckText(label, 128);
        }
        else if (Labels.Count != 0) throw new ArgumentException("Labels require enum scale");
    }
}
