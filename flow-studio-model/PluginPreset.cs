using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Flow.Studio.Model;

/// <summary>Portable public values pinned to the exact packaged device definition.
/// Presets contain no executable code or running DSP state. Migration between
/// package revisions is explicit, never an implicit parameter-name match.</summary>
public sealed class PluginPreset
{
    public string Name { get; }
    public string PluginId { get; }
    public string PackageSha256 { get; }
    public IReadOnlyDictionary<string, double> Values { get; }
    private sealed record Wire(int Version, string Name, string PluginId, string PackageSha256,
        Dictionary<string, double> Values);
    private static readonly JsonSerializerOptions Options = new()
    {
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 8
    };
    private PluginPreset(Wire wire)
    {
        if (wire.Version != 1) throw new ArgumentException("Unsupported preset version");
        PluginManifest.CheckText(wire.Name, 256); PluginManifest.CheckId(wire.PluginId);
        if (wire.PackageSha256 is null || wire.PackageSha256.Length != 64 || wire.PackageSha256.Any(c => !char.IsAsciiHexDigit(c)))
            throw new ArgumentException("Invalid preset package hash");
        if (wire.Values is null || wire.Values.Count > 256) throw new ArgumentException("Invalid preset values");
        foreach (var pair in wire.Values)
        {
            PluginManifest.CheckId(pair.Key);
            if (!double.IsFinite(pair.Value)) throw new ArgumentException("Preset values must be finite");
        }
        Name = wire.Name; PluginId = wire.PluginId; PackageSha256 = wire.PackageSha256.ToLowerInvariant();
        Values = new ReadOnlyDictionary<string, double>(new Dictionary<string, double>(wire.Values, StringComparer.Ordinal));
    }
    public static PluginPreset Capture(string name, ProjectSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var package = source.Plugin ?? throw new InvalidOperationException("Source is not a declared plugin");
        return new(new(1, name, package.Manifest.Id, Fingerprint(package),
            package.Manifest.Parameters.ToDictionary(p => p.Id, p => source.PluginValues.GetValueOrDefault(p.Id, p.Default), StringComparer.Ordinal)));
    }
    public void ValidateFor(PluginPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        if (PluginId != package.Manifest.Id || PackageSha256 != Fingerprint(package))
            throw new ArgumentException("Preset belongs to a different package revision; explicit migration is required");
        if (Values.Count != package.Manifest.Parameters.Count) throw new ArgumentException("Preset must supply every public parameter");
        foreach (var parameter in package.Manifest.Parameters)
        {
            if (!Values.TryGetValue(parameter.Id, out var value)) throw new ArgumentException("Missing preset parameter");
            _ = parameter.ToNormalized(value);
        }
    }
    public string Serialize() => JsonSerializer.Serialize(new Wire(1, Name, PluginId, PackageSha256,
        Values.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal)), Options);
    public static PluginPreset Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (json.Length > 128 * 1024) throw new ArgumentException("Preset exceeds 128 Ki characters");
        using var document = JsonDocument.Parse(json, new() { MaxDepth = 8 });
        PluginManifest.RejectDuplicates(document.RootElement);
        return new(JsonSerializer.Deserialize<Wire>(json, Options) ?? throw new ArgumentException("Missing preset"));
    }
    private static string Fingerprint(PluginPackage package)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(package.Serialize())));
}
