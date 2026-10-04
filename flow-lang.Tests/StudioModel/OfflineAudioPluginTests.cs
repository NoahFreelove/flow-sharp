using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Flow.Audio;
using Flow.Studio.Model;
using FlowLang.Hosting;
using Xunit;

namespace FlowLang.Tests.StudioModel;

[Collection("FlowScripts")]
public class OfflineAudioPluginTests
{
    private const string Code = """
        use "@flowDaw"
        proc process (Dict<String, Double>: context, Buffers: inputs)
            AudioGraph signal = (dawMix "sum" (dawInput "left" 0) (dawInput "right" 1))
            AudioGraph level = (dawGain "level" signal (get context "parameter:gain"))
            (dawResult "processed" (dawAudio (dawProcess level inputs)))
        end proc
        """;
    private static PluginPackage Package(string code = Code, string layer = "processed")
    {
        var manifest = PluginManifest.Parse(JsonSerializer.Serialize(new
        {
            ApiVersion = 1, Id = "example.offline", Name = "Offline mix", Author = "Local", License = "Private", Version = "1",
            SourceSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(code))), Builder = "process",
            Kind = "OfflineAudio", AudioInputs = 2, AudioOutputs = 1, NoteInput = false, NoteOutput = false,
            MinimumSampleRate = 8000, MaximumSampleRate = 96000, MaximumBlockFrames = 256,
            StateSchema = "1", StatePolicy = "Reset",
            Parameters = new[] { new { Id = "gain", Name = "Gain", Unit = "linear", Minimum = 0, Maximum = 2,
                Default = .5, Scale = "Linear", SmoothingMilliseconds = 0, RequiresRebuild = true, Labels = Array.Empty<string>() } },
            Dependencies = Array.Empty<PluginDependency>()
        }));
        return new(manifest, code, layer, []);
    }
    private static GeneratorBuildRequest Request(PluginPackage? package = null, double? gain = null)
    {
        package ??= Package();
        return new(new(1, Guid.NewGuid(), "process"), 3, package.Source,
            new(7, 1, new([new(0, 120)]), new([new(1, 4, 4)]), gain.HasValue ? new Dictionary<string, double> { ["gain"] = gain.Value } : null),
            TimeSpan.FromSeconds(10), package, new([
                new PcmAsset(Enumerable.Repeat(.2f, 64).ToArray(), 8000), new PcmAsset(Enumerable.Repeat(.4f, 64).ToArray(), 8000)]));
    }
    private static float[] Samples(PcmAsset asset)
    { var samples = new float[asset.Frames * 2]; asset.CopyTo(samples); return samples; }
    [Fact]
    public void PackagedExampleUsesThePublicOfflineInputContract()
    {
        string root = Path.Combine(FlowLang.Tests.Characterization.Snapshot.RepoRoot, "examples", "plugins");
        var package = PluginPackage.Deserialize(File.ReadAllText(Path.Combine(root, "offline-bus-mix.flowplugin")));
        Assert.Equal(File.ReadAllText(Path.Combine(root, "offline-bus-mix.flow")), package.Source);
        var result = FlowDawGenerator.Build(Request(package, .25), TestContext.Current.CancellationToken);
        Assert.True(result.Status == JobStatus.Succeeded, result.Error);
        Assert.All(Samples(result.Value!.AudioLayers.Single().Asset), sample => Assert.Equal(.15f, sample, 6));
    }
    [Theory]
    [InlineData(null, .3f)]
    [InlineData(.25, .15f)]
    public void ProcessorConsumesDetachedBusesAndTypedDefaultOrOverride(double? gain, float expected)
    {
        var request = Request(gain: gain);
        var result = FlowDawGenerator.Build(request, TestContext.Current.CancellationToken);
        Assert.True(result.Status == JobStatus.Succeeded, result.Error);
        Assert.All(Samples(result.Value!.AudioLayers.Single().Asset), sample => Assert.Equal(expected, sample, 6));
        Assert.All(Samples(request.AudioInput!.Buses[0]), sample => Assert.Equal(.2f, sample));
        Assert.Equal(GeneratedContentJson.Serialize(result.Value), GeneratedContentJson.Serialize(
            GeneratedContentJson.Deserialize(GeneratedContentJson.Serialize(result.Value), result.Value.SourceId, 3, request.Context)));
        Assert.Equal(request.AudioInput.Serialize(), PluginAudioInput.Deserialize(request.AudioInput.Serialize()).Serialize());
    }
    [Fact]
    public void MissingMismatchedOrInvalidInputAndWrongResultFailWithoutOutput()
    {
        var request = Request();
        foreach (var invalid in new[]
        {
            request with { AudioInput = null },
            Request(gain: 3),
            request with { AudioInput = new([request.AudioInput!.Buses[0]]) },
            Request(Package(layer: "different")),
            Request(Package(Code.Replace("(dawAudio (dawProcess level inputs))", "level"))),
            request with { Plugin = null }
        })
        {
            var result = FlowDawGenerator.Build(invalid, TestContext.Current.CancellationToken);
            Assert.Equal(JobStatus.Failed, result.Status); Assert.Null(result.Value);
        }
        Assert.Throws<ArgumentException>(() => new PluginAudioInput([new(new float[4], 8000), new(new float[6], 8000)]));
        Assert.Throws<ArgumentException>(() => new PluginAudioInput([new(new float[4], 8000), new(new float[4], 16000)]));
        Assert.Throws<ArgumentException>(() => new PluginAudioInput([]));
    }
    [Fact]
    public void CapturedProcessorOutputCannotPretendToChangeAudioByEditingLiveValues()
    {
        var request = Request(gain: .25); var doc = new ProjectDocument(new(new(Guid.NewGuid(), request.Context.Tempo, request.Context.Meter), request.Context));
        var ticket = doc.BeginBuild(request.Descriptor, request.Source);
        var built = FlowDawGenerator.Build(request with { SourceRevision = ticket.Revision }, TestContext.Current.CancellationToken);
        Assert.True(built.Status == JobStatus.Succeeded, built.Error);
        Assert.True(doc.Accept(ticket, built.Value!, "Processed audio", p => p, plugin: request.Plugin));
        var before = doc.Snapshot;
        Assert.Throws<InvalidOperationException>(() => ProjectPluginCommands.SetParameter(doc, request.Descriptor.SourceId, "gain", .25));
        var preset = PluginPreset.Capture("Default", before.Sources[request.Descriptor.SourceId]);
        Assert.Equal(.25, preset.Values["gain"]);
        Assert.Equal(.25, ProjectJson.Deserialize(ProjectJson.Serialize(before)).Sources[request.Descriptor.SourceId].PluginValues["gain"]);
        Assert.Throws<InvalidOperationException>(() => ProjectPluginCommands.ApplyPreset(doc, request.Descriptor.SourceId, preset));
        Assert.Same(before, doc.Snapshot);
    }
#if !FLOW_WEB
    [Fact]
    public async Task IsolatedWorkerCarriesExactAudioInputAndReturnsValidatedProcessorOutput()
    {
        var request = Request(gain: .25);
        await using var worker = ProcessGeneratorWorker.ForInterpreter(Path.Combine(AppContext.BaseDirectory, "flow-interpreter.dll"));
        var result = await worker.BuildAsync(request, TestContext.Current.CancellationToken);
        Assert.True(result.Status == JobStatus.Succeeded, result.Error);
        Assert.All(Samples(result.Value!.AudioLayers.Single().Asset), sample => Assert.Equal(.15f, sample, 6));
        Assert.Same(request.Context, result.Value.Context); Assert.Null(worker.ProcessId);
        var failed = await worker.BuildAsync(request with { AudioInput = null }, TestContext.Current.CancellationToken);
        Assert.Equal(JobStatus.Failed, failed.Status); Assert.Null(failed.Value); Assert.Null(worker.ProcessId);
    }
#endif
}
