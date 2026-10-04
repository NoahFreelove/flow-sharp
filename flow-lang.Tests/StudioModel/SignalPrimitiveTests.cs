using Flow.Audio.Graph;
using FlowLang.Core;
using FlowLang.Hosting;
using Xunit;
namespace FlowLang.Tests.StudioModel;
[Collection("FlowScripts")]
public class SignalPrimitiveTests
{
    private static AudioGraphDefinition Shape() => AudioGraphDefinition.Multiply("output",
        AudioGraphDefinition.Mix("biasAdd", AudioGraphDefinition.Multiply("driven", AudioGraphDefinition.Input("input"),
            AudioGraphDefinition.Value("drive", 2)), AudioGraphDefinition.Value("bias", .1)).Then("shape", "flow.tanh"),
        AudioGraphDefinition.Value("level", .25));
    [Fact]
    public void ComposedShapeMatchesSampleFormulaAndLiveParameterUpdatesWithoutAllocation()
    {
        var graph = new PreparedAudioGraph(Shape(), 8000, 128);
        float[] input = Enumerable.Range(0, 256).Select(i => (i % 17 - 8) / 8f).ToArray(), output = new float[256];
        graph.Process(input, output);
        for (int i = 0; i < output.Length; i++) Assert.Equal((float)Math.Tanh(input[i] * 2 + .1f) * .25f, output[i], 6);
        graph.SetLatestParameter("drive", "value", 4);
        graph.Process(input, output); // Complete 5 ms smoothing.
        long before = GC.GetAllocatedBytesForCurrentThread(); graph.Process(input, output);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before; Assert.Equal(0, allocated);
        for (int i = 0; i < output.Length; i++) Assert.Equal((float)Math.Tanh(input[i] * 4 + .1f) * .25f, output[i], 6);
    }
    [Fact]
    public void GraphJsonAndExecutableFlowKeepComposedConnectionsAndOutput()
    {
        var definition = Shape(); var json = AudioGraphJson.Serialize(definition);
        var restored = AudioGraphJson.Deserialize(json);
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var result = engine.Evaluate(FlowGraphExporter.Export(definition));
        Assert.True(result.Succeeded, string.Join("\n", result.Errors));
        var fromFlow = result.LastValue!.As<AudioGraphDefinition>();
        Assert.Equal(json, AudioGraphJson.Serialize(fromFlow));
        float[] input = [.1f, -.2f, .3f, -.4f], a = new float[4], b = new float[4];
        new PreparedAudioGraph(restored).Process(input, a); new PreparedAudioGraph(fromFlow).Process(input, b); Assert.Equal(a, b);
    }
    [Fact]
    public void ValueAutomationRunsAtAbsoluteFramesAndMultiplicationRemainsFinite()
    {
        var definition = AudioGraphDefinition.Multiply("out", AudioGraphDefinition.Input("input"), AudioGraphDefinition.Value("level", 1));
        var graph = new PreparedAudioGraph(definition, 8000, 16, automation:
            [new("level", "value", [new(0, 0), new(2, 1)], AutomationShape.Step)]);
        var output = new float[8]; graph.Process(Enumerable.Repeat(1f, 8).ToArray(), output);
        Assert.Equal(new float[] {0,0,0,0,1,1,1,1}, output);
        Assert.Throws<InvalidOperationException>(() => graph.SetLatestParameter("level", "value", 2));
        var large = new PreparedAudioGraph(AudioGraphDefinition.Multiply("product", AudioGraphDefinition.Input("input"), AudioGraphDefinition.Value("value", 1000000)));
        large.Process(new float[] {float.MaxValue, -float.MaxValue}, output.AsSpan(0, 2));
        Assert.Equal(float.MaxValue, output[0]); Assert.Equal(-float.MaxValue, output[1]);
    }
}
