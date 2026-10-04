using Flow.Audio.Graph;
using Flow.Studio.Engine;
using FlowLang.Core;
using FlowLang.Hosting;
using Xunit;

namespace FlowLang.Tests.StudioModel;

[Collection("FlowScripts")]
public class EffectChainCompilerTests
{
    private static BoundEffect Gain(double gain) => new(Guid.NewGuid(), AudioGraphDefinition.Input("input")
        .Then("gain", "flow.gain", new Dictionary<string, double> { ["gain"] = gain }));
    private static BoundEffect Saturate() => new(Guid.NewGuid(), AudioGraphDefinition.Input("input").Then("sat", "flow.tanh"));
    private static float[] Render(AudioGraphDefinition graph, float[] inputs, GraphAutomationLane[]? automation = null)
    {
        var prepared = new PreparedAudioGraph(graph, 1000, 16, automation: automation);
        var output = new float[32]; prepared.Process(inputs, output); return output;
    }
    [Fact]
    public void OrderedMasterEffectsUseSharedGraphAndExportAsExecutableFlow()
    {
        var gain = Gain(.5); var saturate = Saturate(); var mixer = AudioGraphDefinition.Input("input");
        var first = EffectChainCompiler.Compose(mixer, new Dictionary<int, IReadOnlyList<BoundEffect>>(), [gain, saturate]);
        var second = EffectChainCompiler.Compose(mixer, new Dictionary<int, IReadOnlyList<BoundEffect>>(), [saturate, gain]);
        var input = Enumerable.Repeat(.8f, 32).ToArray();
        Assert.All(Render(first.Graph, input), x => Assert.Equal((float)Math.Tanh(.4), x, 6));
        Assert.All(Render(second.Graph, input), x => Assert.Equal((float)Math.Tanh(.8) * .5f, x, 6));
        using var engine = new FlowEngine(new EngineOptions { Output = TextWriter.Null, Diagnostics = TextWriter.Null });
        var exported = engine.Evaluate(FlowGraphExporter.Export(first.Graph));
        Assert.True(exported.Succeeded, string.Join("\n", exported.Errors));
        Assert.Equal(Render(first.Graph, input), Render(exported.LastValue!.As<AudioGraphDefinition>(), input));
    }
    [Fact]
    public void TrackChainIsSharedAcrossMixerBranchesAndMasterRunsAfterTheMix()
    {
        var mixer = AudioGraphDefinition.Sum("mix", [AudioGraphDefinition.Input("left", 0),
            AudioGraphDefinition.Input("again", 0), AudioGraphDefinition.Input("other", 1)]);
        var track = Saturate(); var master = Gain(.5);
        var composed = EffectChainCompiler.Compose(mixer, new Dictionary<int, IReadOnlyList<BoundEffect>> { [0] = new[] { track } }, [master]);
        Assert.Single(composed.Graph.Nodes.Where(n => n.DeviceId == "flow.tanh"));
        Assert.Equal(2, composed.Graph.Nodes.Count(n => n.DeviceId == "flow.input"));
        var input = Enumerable.Repeat(.8f, 32).Concat(Enumerable.Repeat(.2f, 32)).ToArray();
        Assert.All(Render(composed.Graph, input), x => Assert.Equal((float)((Math.Tanh(.8) * 2 + .2) * .5), x, 6));
    }
    [Fact]
    public void CollidingLocalNodeNamesRetainIndependentAutomationAndLiveControls()
    {
        var first = Gain(1); var second = Gain(1);
        var composed = EffectChainCompiler.Compose(AudioGraphDefinition.Input("effectNode0"),
            new Dictionary<int, IReadOnlyList<BoundEffect>>(), [first, second]);
        Assert.NotEqual(composed.Nodes[(first.Binding, "gain")], composed.Nodes[(second.Binding, "gain")]);
        var lane = composed.MapAutomation(first.Binding, new("gain", "gain", [new(0, .25)]));
        var prepared = new PreparedAudioGraph(composed.Graph, 1000, 16, automation: [lane]);
        var input = Enumerable.Repeat(.8f, 32).ToArray(); var output = new float[32];
        prepared.SetLatestParameter(composed.Nodes[(second.Binding, "gain")], "gain", .5);
        prepared.Process(input, output); prepared.Process(input, output);
        Assert.All(output, x => Assert.Equal(.1f, x, 6));
        Assert.Throws<InvalidOperationException>(() => prepared.SetLatestParameter(composed.Nodes[(first.Binding, "gain")], "gain", .5));
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) prepared.Process(input, output);
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }
    [Fact]
    public void BypassOmitsProcessorAndTailWithoutMutatingAcceptedGraphs()
    {
        var delay = new BoundEffect(Guid.NewGuid(), AudioGraphDefinition.Input("input").Then("delay", "flow.delay",
            new Dictionary<string, double> { ["timeMs"] = 10, ["repeats"] = 2, ["feedback"] = .5, ["wet"] = 1 }));
        var mixer = AudioGraphDefinition.Input("input");
        var active = EffectChainCompiler.Compose(mixer, new Dictionary<int, IReadOnlyList<BoundEffect>>(), [delay]);
        var bypass = EffectChainCompiler.Compose(mixer, new Dictionary<int, IReadOnlyList<BoundEffect>>(), [delay with { Bypassed = true }]);
        Assert.True(new PreparedAudioGraph(active.Graph, 1000, 16).TailFrames > 0);
        Assert.Equal(0, new PreparedAudioGraph(bypass.Graph, 1000, 16).TailFrames);
        Assert.Empty(bypass.Nodes); Assert.Equal(2, delay.Graph.Nodes.Count);
        Assert.Throws<ArgumentException>(() => bypass.MapAutomation(delay.Binding, new("delay", "wet", [new(0, .5)])));
        var input = Enumerable.Repeat(.3f, 32).ToArray(); Assert.Equal(input, Render(bypass.Graph, input));
    }
    [Fact]
    public void InvalidPortsDuplicateInstancesAndOversizedCompositionFailBeforePreparation()
    {
        var mixer = AudioGraphDefinition.Input("input"); var gain = Gain(1);
        Assert.Throws<ArgumentException>(() => EffectChainCompiler.Compose(mixer,
            new Dictionary<int, IReadOnlyList<BoundEffect>> { [1] = new[] { gain } }, []));
        Assert.Throws<ArgumentException>(() => EffectChainCompiler.Compose(mixer,
            new Dictionary<int, IReadOnlyList<BoundEffect>>(), [gain, gain]));
        Assert.Throws<ArgumentException>(() => EffectChainCompiler.Compose(mixer,
            new Dictionary<int, IReadOnlyList<BoundEffect>>(), [new(Guid.NewGuid(), AudioGraphDefinition.Input("wrong", 1))]));
        var large = AudioGraphDefinition.Input("in");
        for (int i = 0; i < 600; i++) large = large.Then("gain" + i, "flow.gain");
        Assert.Throws<ArgumentException>(() => EffectChainCompiler.Compose(mixer,
            new Dictionary<int, IReadOnlyList<BoundEffect>>(), [new(Guid.NewGuid(), large), new(Guid.NewGuid(), large)]));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => EffectChainCompiler.Compose(mixer,
            new Dictionary<int, IReadOnlyList<BoundEffect>>(), [gain], cancelled.Token));
    }
}
