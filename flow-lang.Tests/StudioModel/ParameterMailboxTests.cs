using Flow.Audio.Graph;
using Xunit;
namespace FlowLang.Tests.StudioModel;
public class ParameterMailboxTests
{
    [Fact]
    public void LatestValuesCoalescePerParameterOverrideFullQueueAndAllocateNothingInAudio()
    {
        var definition = AudioGraphDefinition.Input("input").Then("a", "flow.gain").Then("b", "flow.gain");
        var graph = new PreparedAudioGraph(definition, 8000, 128, parameterCapacity: 1);
        float[] input = Enumerable.Repeat(1f, 256).ToArray(), output = new float[256];
        graph.Process(input, output); // Warm callback path.
        Assert.True(graph.TrySetParameter("a", "gain", 0)); Assert.False(graph.TrySetParameter("a", "gain", 1));
        for (int i = 0; i < 10000; i++) graph.SetLatestParameter("a", "gain", i % 16);
        graph.SetLatestParameter("a", "gain", .25); graph.SetLatestParameter("b", "gain", .5);
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        graph.Process(input, output); graph.Process(input, output);
        long delta = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Assert.Equal(0, delta); Assert.All(output, x => Assert.Equal(.125f, x));
        graph.SetLatestParameter("a", "gain", 1); graph.SetLatestParameter("b", "gain", 1);
        graph.Process(input, output); graph.Process(input, output); Assert.All(output, x => Assert.Equal(1, x));
    }
}
