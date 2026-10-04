using Flow.Audio.Graph;
using Flow.Audio;
using Xunit;
namespace FlowLang.Tests.StudioModel;
public class GraphAutomationTests
{
    private static AudioGraphDefinition Graph() => AudioGraphDefinition.Input("input").Then("gain", "flow.gain",
        new Dictionary<string, double> { ["gain"] = .25 });
    private static GraphAutomationLane Lane() => new("gain", "gain", [new(2, 0), new(6, 1)]);
    [Fact]
    public void LinearAutomationIsSampleAccurateAndIndependentOfBlockSize()
    {
        var graph = new PreparedAudioGraph(Graph(), 8000, 16, automation: [Lane()]);
        var split = new PreparedAudioGraph(Graph(), 8000, 16, automation: [Lane()]);
        var input = Enumerable.Repeat(1f, 16).ToArray(); var actual = new float[16]; var pieces = new float[16];
        graph.Process(input, actual);
        split.Process(input.AsSpan(0, 6), pieces.AsSpan(0, 6)); split.Process(input.AsSpan(6), pieces.AsSpan(6));
        Assert.Equal(pieces, actual);
        Assert.Equal(new float[] { .25f, .25f, 0, .25f, .5f, .75f, 1, 1 }, actual.Where((_, i) => i % 2 == 0));
        graph.Reset(4); graph.Process(input.AsSpan(0, 2), actual.AsSpan(0, 2)); Assert.Equal(.5f, actual[0]);
        graph.Reset(0); graph.Process(input.AsSpan(0, 2), actual.AsSpan(0, 2)); Assert.Equal(.25f, actual[0]);
        Assert.Throws<InvalidOperationException>(() => graph.TrySetParameter("gain", "gain", .5));
    }
    [Fact]
    public void StepCurvesHoldAndInvalidOrUnboundedLanesAreRejected()
    {
        var lane = new GraphAutomationLane("gain", "gain", [new(3, .5), new(7, 1)], AutomationShape.Step);
        Assert.Equal(.25, lane.ValueAt(2, .25)); Assert.Equal(.5, lane.ValueAt(6, .25)); Assert.Equal(1, lane.ValueAt(7, .25));
        Assert.Throws<ArgumentException>(() => new GraphAutomationLane("gain", "gain", [new(1, 0), new(1, 1)]));
        Assert.Throws<ArgumentException>(() => new PreparedAudioGraph(Graph(), automation: [lane, lane]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PreparedAudioGraph(Graph(), automation:
            [new GraphAutomationLane("gain", "gain", [new(0, 17)])]));
        Assert.Throws<ArgumentException>(() => new PreparedAudioGraph(Graph(), automation:
            [new GraphAutomationLane("input", "bus", [new(0, 1)])]));
    }
    [Fact]
    public void ProcessingAndSeekOfAutomationAllocateNothing()
    {
        var graph = new PreparedAudioGraph(Graph(), automation: [Lane()]);
        var input = new float[512]; var output = new float[512];
        for (int i = 0; i < 100; i++) { graph.Reset(3); graph.Process(input, output); }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) { graph.Reset(3); graph.Process(input, output); }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
    [Fact]
    public void PlaybackSeekUsesAbsoluteAutomationPosition()
    {
        var asset = new PcmAsset(Enumerable.Repeat(1f, 16).ToArray(), 8000);
        var source = new PreparedPcmPlayback([new(Guid.NewGuid(), asset, 0, 8, 0, 8)], 8000, 16);
        var playback = new PreparedGraphPlayback(new PreparedAudioGraph(Graph(), 8000, 16, automation: [Lane()]), [source]);
        playback.Seek(4); var output = new float[2]; playback.Read(output); Assert.Equal(.5f, output[0]);
        playback.Reset(); playback.Read(output); Assert.Equal(.25f, output[0]);
    }

}
