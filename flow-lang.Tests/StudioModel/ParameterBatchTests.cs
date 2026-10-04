using Flow.Audio.Graph;
using Xunit;
namespace FlowLang.Tests.StudioModel;
public class ParameterBatchTests
{
    private static AudioGraphDefinition Difference() => AudioGraphDefinition.Mix("difference", AudioGraphDefinition.Value("a", 0),
        AudioGraphDefinition.Multiply("negate", AudioGraphDefinition.Value("b", 0), AudioGraphDefinition.Value("minus", -1)));
    [Fact]
    public async Task ConcurrentAudioNeverObservesHalfOfAControlBatch()
    {
        var graph = new PreparedAudioGraph(Difference(), 8000, 16); int done = 0;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var producer = Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < 10000; i++)
                {
                    deadline.Token.ThrowIfCancellationRequested();
                    graph.SetLatestParameters([new("a", "value", i % 11), new("b", "value", i % 11)]);
                }
            }
            finally { Volatile.Write(ref done, 1); }
        });
        var consumer = Task.Run(() =>
        {
            var samples = new float[32];
            do
            {
                deadline.Token.ThrowIfCancellationRequested(); graph.Process([], samples);
                foreach (float value in samples) if (value != 0) throw new Exception("Partially applied batch");
            } while (Volatile.Read(ref done) == 0);
        });
        await Task.WhenAll(producer, consumer);
    }
    [Fact]
    public void InvalidBatchDoesNotPublishAndMergedRestorationsSurviveNextGesture()
    {
        var graph = new PreparedAudioGraph(Difference(), 8000, 128); var output = new float[256];
        graph.SetLatestParameters([new("a", "value", 1), new("b", "value", 1)]);
        Assert.Throws<ArgumentOutOfRangeException>(() => graph.SetLatestParameters([new("a", "value", 2), new("b", "value", double.NaN)]));
        Assert.Throws<ArgumentException>(() => graph.SetLatestParameters([new("a", "value", 2), new("a", "value", 3)]));
        graph.Process([], output); Assert.All(output, x => Assert.Equal(0, x));
        graph.SetLatestParameter("a", "value", 0); // Cancel first gesture.
        GraphParameterValue[] next = [new("b", "value", 0)]; graph.SetLatestParameters(next); next[0] = new("b", "value", 10);
        long before = GC.GetAllocatedBytesForCurrentThread(); graph.Process([], output);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated); Assert.All(output, x => Assert.Equal(0, x));
    }
}
