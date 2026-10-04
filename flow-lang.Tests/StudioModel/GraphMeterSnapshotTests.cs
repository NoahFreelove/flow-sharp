using Flow.Audio.Graph;
using Xunit;
namespace FlowLang.Tests.StudioModel;
public class GraphMeterSnapshotTests
{
    private static PreparedAudioGraph Create() => new(AudioGraphDefinition.Input("input").Then("gain", "flow.gain"), 8000, 16);
    [Fact]
    public void PublishedMetersIdentifyCompletedBlockAndResetWithoutAllocations()
    {
        var graph = Create(); var meters = new StereoMeter[graph.MeterNodeIds.Count];
        var input = Enumerable.Repeat(.5f, 32).ToArray(); var output = new float[32];
        graph.Process(input, output);
        Assert.True(graph.TryReadMeters(meters, out long frame)); Assert.Equal(16, frame);
        Assert.Equal(new[] { "input", "gain" }, graph.MeterNodeIds);
        Assert.All(meters, m => Assert.Equal(new StereoMeter(.5f, .5f, .5, .5), m));
        graph.Reset(7); Assert.True(graph.TryReadMeters(meters, out frame)); Assert.Equal(7, frame);
        Assert.All(meters, m => Assert.Equal(default, m));
        for (int i = 0; i < 100; i++) { graph.Process(input, output); graph.TryReadMeters(meters, out _); }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) { graph.Process(input, output); graph.TryReadMeters(meters, out _); }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
    [Fact]
    public async Task ConcurrentReaderNeverAcceptsMixedBlocks()
    {
        var graph = Create(); var meters = new StereoMeter[2];
        using var start = new ManualResetEventSlim();
        var writer = Task.Run(() =>
        {
            var input = new float[32]; var output = new float[32]; start.Wait();
            for (int block = 1; block <= 20000; block++)
            {
                Array.Fill(input, (float)(block % 8) / 8); graph.Process(input, output);
            }
        }, TestContext.Current.CancellationToken);
        start.Set(); int accepted = 0;
        while (!writer.IsCompleted)
        {
            if (!graph.TryReadMeters(meters, out long frame)) continue;
            float expected = (float)((frame / 16) % 8) / 8;
            Assert.All(meters, m => Assert.Equal(new StereoMeter(expected, expected, expected, expected), m));
            accepted++;
        }
        await writer;
        Assert.True(graph.TryReadMeters(meters, out long finalFrame)); Assert.Equal(320000, finalFrame);
        Assert.All(meters, m => Assert.Equal(default, m));
        Assert.True(accepted > 0);
    }
}
