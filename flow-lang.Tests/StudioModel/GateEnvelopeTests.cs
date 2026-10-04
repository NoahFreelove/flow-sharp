using Flow.Audio.Graph;
using FlowLang.Core;
using FlowLang.Hosting;
using Xunit;
namespace FlowLang.Tests.StudioModel;
[Collection("FlowScripts")]
public class GateEnvelopeTests
{
    private static AudioGraphDefinition Definition(double attack = 2, double decay = 2, double sustain = .5, double release = 2) =>
        AudioGraphDefinition.Input("gate").Then("env", "flow.adsr", new Dictionary<string, double>
        { ["attackMs"] = attack, ["decayMs"] = decay, ["sustain"] = sustain, ["releaseMs"] = release });
    private static float[] Stereo(params float[] mono) => mono.SelectMany(x => new[] { x, x }).ToArray();
    [Fact]
    public void StagesReachExactTargetsAndReleaseFromCurrentLevel()
    {
        var graph = new PreparedAudioGraph(Definition(), 1000, 32);
        var input = Stereo(1, 1, 1, 1, 1, 0, 0, 0); var output = new float[input.Length];
        graph.Process(input, output);
        Assert.Equal(Stereo(.5f, 1, .75f, .5f, .5f, .25f, 0, 0), output);
        Assert.Equal(2, graph.TailFrames);
        graph.Reset(); graph.Process(Stereo(1, 0, 1, 1), output.AsSpan(0, 8));
        Assert.Equal(Stereo(.5f, .25f, .625f, 1), output[..8]);
    }
    [Fact]
    public void ZeroStagesAndStereoEdgesAreIndependent()
    {
        var graph = new PreparedAudioGraph(Definition(0, 0, .25, 0), 1000, 32);
        float[] input = [1, 0, 1, 1, 0, 1, float.NaN, -1], output = new float[8];
        graph.Process(input, output);
        Assert.Equal(new float[] { .25f, 0, .25f, .25f, 0, .25f, 0, 0 }, output);
        Assert.Equal(0, graph.TailFrames);
    }
    [Fact]
    public void PartitionResetAndCallbackAllocationRemainStable()
    {
        var definition = Definition(7, 9, .4, 11);
        var graph = new PreparedAudioGraph(definition, 1000, 128);
        var whole = new PreparedAudioGraph(definition, 1000, 128);
        var input = Stereo(Enumerable.Range(0, 128).Select(i => i % 31 < 20 ? 1f : 0f).ToArray());
        var expected = new float[input.Length]; var actual = new float[input.Length];
        whole.Process(input, expected);
        graph.Process(input.AsSpan(0, 22), actual.AsSpan(0, 22));
        graph.Process(input.AsSpan(22), actual.AsSpan(22)); Assert.Equal(expected, actual);
        graph.Reset(100); graph.Process(input, actual); Assert.Equal(expected, actual);
        long before = GC.GetAllocatedBytesForCurrentThread(); graph.Process(input, actual);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
    [Fact]
    public void EnvelopePublicFlowAndExportReconstructSameGraph()
    {
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var definition = Definition();
        var result = engine.Evaluate(FlowGraphExporter.Export(definition));
        Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        Assert.Equal(AudioGraphJson.Serialize(definition), AudioGraphJson.Serialize(result.LastValue!.As<AudioGraphDefinition>()));
        var authored = engine.Evaluate("(dawAdsr \"env\" (dawInput \"gate\" 0) 2ms 2ms 0.5 2ms)");
        Assert.True(authored.Succeeded, string.Join("\n", authored.Errors));
        Assert.Equal(AudioGraphJson.Serialize(definition), AudioGraphJson.Serialize(authored.LastValue!.As<AudioGraphDefinition>()));
    }
}
