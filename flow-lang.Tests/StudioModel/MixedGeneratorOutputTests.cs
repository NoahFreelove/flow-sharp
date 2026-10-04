using System.Text.Json.Nodes;
using Flow.Audio;
using Flow.Audio.Graph;
using Flow.Studio.Model;
using FlowLang.Hosting;
using Xunit;

namespace FlowLang.Tests.StudioModel;

[Collection("FlowScripts")]
public class MixedGeneratorOutputTests
{
    private const string Source = """
        use "@flowDaw"
        use "@audio"
        proc generate (Dict<String, Double>: context)
            section phrase { Sequence notes = | A4q C5q | }
            Song score = [phrase]
            AudioGraph effect = (dawGain "gain" (dawInput "input" 0) 0.5)
            Buffer tone = (createSineTone 0.01 440.0 0.25)
            DawAudio audio = (dawAudio (dawProcess effect tone))
            DawInstrument instrument = (dawSine 32 3ms 40ms)
            DawResult a = (dawCombine (dawResult "main" score) (dawResult "main" audio))
            DawResult b = (dawCombine (dawResult "main" effect) (dawResult "main" instrument))
            (dawCombine a b)
        end proc
        """;
    private static GeneratorBuildRequest Request() => new(new(1, Guid.NewGuid(), "generate"), 3, Source,
        new(8, 123, new([new(0, 120)]), new([new(1, 4, 4)])), TimeSpan.FromSeconds(20));

#if !FLOW_WEB
    [Fact]
    public async Task AllFourRolesSurviveActualWorkerWithIdenticalContent()
    {
        var request = Request();
        var direct = FlowDawGenerator.Build(request, TestContext.Current.CancellationToken);
        Assert.True(direct.Status == JobStatus.Succeeded, direct.Error);
        await using var worker = ProcessGeneratorWorker.ForInterpreter(Path.Combine(AppContext.BaseDirectory, "flow-interpreter.dll"));
        var isolated = await worker.BuildAsync(request, TestContext.Current.CancellationToken);
        Assert.True(isolated.Status == JobStatus.Succeeded, isolated.Error);
        var output = isolated.Value!;
        Assert.Single(output.ScoreLayers); Assert.Single(output.AudioLayers); Assert.Single(output.GraphLayers); Assert.Single(output.InstrumentLayers);
        Assert.Equal(GeneratedContentJson.Serialize(direct.Value!), GeneratedContentJson.Serialize(output));
        Assert.Equal(32, output.InstrumentLayers[0].Instrument.VoiceLimit);
        var pcm = new float[output.AudioLayers[0].Asset.Frames * 2]; output.AudioLayers[0].Asset.CopyTo(pcm);
        Assert.Contains(pcm, sample => sample != 0);
        Assert.Equal(0.5, output.GraphLayers[0].Graph.Nodes.Single(n => n.Id == "gain").Parameters["gain"]);
        Assert.Null(worker.ProcessId);
    }

#endif
    [Fact]
    public async Task DuplicateResultDoesNotReplaceLastGood()
    {
        var request = Request();
        using var host = new FlowDawGeneratorHost(request.Descriptor.SourceId);
        var good = await host.Submit(request);
        Assert.True(good.Status == JobStatus.Succeeded, good.Error);
        var duplicate = await host.Submit(request with { SourceRevision = 4, Source = Source.Replace("(dawCombine a b)", "(dawCombine a a)") });
        Assert.Equal(JobStatus.Failed, duplicate.Status);
        Assert.Same(good.Value, host.LastGood);
    }

    [Fact]
    public void AudioOnlyAndGraphOnlyResultsAreValid()
    {
        var request = Request();
        var audio = FlowDawGenerator.Build(request with { Source = Source.Replace("(dawCombine a b)", "(dawResult \"audio\" audio)") }, TestContext.Current.CancellationToken);
        Assert.True(audio.Status == JobStatus.Succeeded, audio.Error);
        Assert.Empty(audio.Value!.ScoreLayers); Assert.Single(audio.Value.AudioLayers);
        var graph = FlowDawGenerator.Build(request with { Source = Source.Replace("(dawCombine a b)", "(dawResult \"effect\" effect)") }, TestContext.Current.CancellationToken);
        Assert.True(graph.Status == JobStatus.Succeeded, graph.Error);
        Assert.Empty(graph.Value!.ScoreLayers); Assert.Single(graph.Value.GraphLayers);
    }

    [Fact]
    public void ContentCodecRejectsBadSchemaInstrumentAndPcm()
    {
        var request = Request();
        var built = FlowDawGenerator.Build(request, TestContext.Current.CancellationToken);
        Assert.True(built.Status == JobStatus.Succeeded, built.Error);
        string json = GeneratedContentJson.Serialize(built.Value!);
        var data = JsonNode.Parse(json)!;
        data["Instruments"]![0]!["Version"] = 99;
        Assert.Throws<System.Text.Json.JsonException>(() => GeneratedContentJson.Deserialize(data.ToJsonString(), request.Descriptor.SourceId, 3, request.Context));
        data = JsonNode.Parse(json)!;
        data["Audio"]![0]!["Pcm"] = Convert.ToBase64String(new byte[3]);
        Assert.Throws<System.Text.Json.JsonException>(() => GeneratedContentJson.Deserialize(data.ToJsonString(), request.Descriptor.SourceId, 3, request.Context));
        data = JsonNode.Parse(json)!;
        data["Version"] = 999;
        Assert.Throws<System.Text.Json.JsonException>(() => GeneratedContentJson.Deserialize(data.ToJsonString(), request.Descriptor.SourceId, 3, request.Context));
    }
}
