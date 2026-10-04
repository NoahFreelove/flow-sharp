using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Flow.Audio.Graph;
using Flow.Studio.Model;
using Xunit;
namespace FlowLang.Tests.StudioModel;
public class PluginManifestTests
{
    private const string Source = "this source must never execute while discovering plugins";
    private static JsonObject Json() => JsonNode.Parse(JsonSerializer.Serialize(new
    {
        ApiVersion = 1, Id = "example.level", Name = "Level", Author = "Local author", License = "Private",
        Version = "1.0.0", SourceSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Source))),
        Builder = "build", Kind = "AudioEffect", AudioInputs = 1, AudioOutputs = 1, NoteInput = false, NoteOutput = false,
        MinimumSampleRate = 8000, MaximumSampleRate = 96000, MaximumBlockFrames = 512, StateSchema = "reset-v1", StatePolicy = "Reset",
        Parameters = new[] { new { Id = "level", Name = "Level", Unit = "linear", Minimum = 0, Maximum = 2, Default = 1,
            Scale = "Linear", SmoothingMilliseconds = 5, RequiresRebuild = false, Labels = Array.Empty<string>() } },
        Dependencies = new[] { new { Id = "library.helpers", Version = "1.0.0", Sha256 = new string('a', 64) } }
    }))!.AsObject();
    private static PluginManifest Parse(JsonObject json) => PluginManifest.Parse(json.ToJsonString());
    [Fact]
    public void DiscoveryRoundTripsAndVerifiesExactSourceWithoutExecutingIt()
    {
        var manifest = Parse(Json()); manifest.ValidateSource(Source);
        Assert.Equal(manifest.Serialize(), PluginManifest.Parse(manifest.Serialize()).Serialize());
        Assert.Equal("level", Assert.Single(manifest.Parameters).Id);
        Assert.Throws<ArgumentException>(() => manifest.ValidateSource(Source + "\n"));
        Assert.Throws<ArgumentException>(() => manifest.ValidateProcessing(192000, 128));
        Assert.Throws<ArgumentException>(() => manifest.ValidateProcessing(48000, 1024));
        var labels = new List<string> { "Off", "On" };
        var parameter = new PluginParameter("mode", "Mode", "", 0, 1, 0, PluginParameterScale.Enumeration, 0, true, labels);
        labels[0] = "Mutated"; Assert.Equal("Off", parameter.Labels[0]);
    }
    [Fact]
    public void MissingUnknownDuplicateAndInvalidDeclarationsFailClosed()
    {
        var json = Json(); json.Remove("NoteInput"); Assert.Throws<JsonException>(() => Parse(json));
        json = Json(); json["Unknown"] = true; Assert.Throws<JsonException>(() => Parse(json));
        Assert.Throws<ArgumentException>(() => PluginManifest.Parse(Json().ToJsonString().Replace("\"ApiVersion\":1", "\"ApiVersion\":1,\"ApiVersion\":1")));
        foreach (var field in new[] { "Id", "SourceSha256", "Builder" })
        { json = Json(); json[field] = "invalid text"; Assert.Throws<ArgumentException>(() => Parse(json)); }
        json = Json(); json["ApiVersion"] = 99; Assert.Throws<ArgumentException>(() => Parse(json));
        json = Json(); json["Kind"] = "Instrument"; Assert.Throws<ArgumentException>(() => Parse(json));
        json = Json(); json["Parameters"]!.AsArray().Add(json["Parameters"]![0]!.DeepClone()); Assert.Throws<ArgumentException>(() => Parse(json));
        json = Json(); json["Parameters"]![0]!["Scale"] = "Logarithmic"; Assert.Throws<ArgumentException>(() => Parse(json));
    }
    [Fact]
    public void EffectBuildValidatesPortsStableBindingsAndDeclaredBehavior()
    {
        var manifest = Parse(Json());
        var graph = AudioGraphDefinition.Input("input").Then("gain", "flow.gain");
        PluginParameterTarget[] targets = [new("level", "gain", "gain")];
        var contract = new PluginEffectContract(manifest, graph, targets, 48000, 256);
        targets[0] = new("wrong", "gain", "gain"); Assert.Equal("level", contract.Targets[0].ParameterId);
        var renamed = Json(); renamed["Parameters"]![0]!["Name"] = "Renamed knob";
        _ = new PluginEffectContract(Parse(renamed), graph, contract.Targets, 48000, 256);
        Assert.Throws<ArgumentException>(() => new PluginEffectContract(manifest, graph, [], 48000, 256));
        Assert.Throws<ArgumentException>(() => new PluginEffectContract(manifest, graph, targets, 48000, 256));
        Assert.Throws<ArgumentException>(() => new PluginEffectContract(manifest, AudioGraphDefinition.Input("input", 1).Then("gain", "flow.gain"), contract.Targets, 48000, 256));
        var different = Json(); different["Parameters"]![0]!["SmoothingMilliseconds"] = 0;
        Assert.Throws<ArgumentException>(() => new PluginEffectContract(Parse(different), graph, contract.Targets, 48000, 256));
        Assert.Throws<ArgumentException>(() => new PluginEffectContract(manifest, graph.Then("other", "flow.gain"), [new("level", "other", "missing")], 48000, 256));
    }
    [Fact]
    public void ParameterMappingAndDependencyPinsHaveExplicitStableSemantics()
    {
        var frequency = new PluginParameter("frequency", "Frequency", "Hz", 20, 20000, 440, PluginParameterScale.Logarithmic, 5, false, []);
        Assert.Throws<ArgumentException>(() => new PluginParameter("bad", "Bad", "", 1e100, Math.BitIncrement(1e100), 1e100, PluginParameterScale.Logarithmic, 0, false, []));
        Assert.Equal(20, frequency.FromNormalized(0)); Assert.Equal(20000, frequency.FromNormalized(1));
        Assert.Equal(.5, frequency.ToNormalized(frequency.FromNormalized(.5)), 12);
        var count = new PluginParameter("count", "Repeats", "count", 1, 3, 2, PluginParameterScale.Enumeration, 0, true, ["One", "Two", "Three"]);
        Assert.Equal(2, count.FromNormalized(.5)); Assert.Equal(.5, count.ToNormalized(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => count.ToNormalized(1.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => frequency.FromNormalized(double.NaN));
        var json = Json(); byte[] content = Encoding.UTF8.GetBytes("pinned helper");
        json["Dependencies"]![0]!["Sha256"] = Convert.ToHexStringLower(SHA256.HashData(content));
        var manifest = Parse(json); manifest.ValidateDependency("library.helpers", "1.0.0", content);
        Assert.Throws<ArgumentException>(() => manifest.ValidateDependency("library.helpers", "2.0.0", content));
        Assert.Throws<ArgumentException>(() => manifest.ValidateDependency("library.helpers", "1.0.0", [0]));
        Assert.Throws<ArgumentException>(() => manifest.ValidateDependency("undeclared", "1.0.0", content));
    }

}
