using Flow.Audio;
using Flow.Audio.Graph;
using Flow.Music.Model;
#if !FLOW_WEB
using Flow.Platform.Linux;
#endif
using Xunit;

namespace FlowLang.Tests.StudioModel;

public class GraphPlaybackTests
{
    private static PreparedSinePlayback Source(double frequency, double duration)
    {
        var section = new SectionSnapshot(Guid.NewGuid(), "tone", new(120),
            [new(Guid.NewGuid(), "melody", duration, [new(Guid.NewGuid(), "voice", 0, duration, new('A', 4, 0, null, 69, frequency))])]);
        return PreparedSinePlayback.Prepare(new(Guid.NewGuid(), [new(Guid.NewGuid(), section)]), new(8000, 128));
    }
    private static PreparedGraphPlayback Playback()
    {
        var graph = AudioGraphDefinition.Mix("group", AudioGraphDefinition.Input("a", 0), AudioGraphDefinition.Input("b", 1))
            .Then("master", "flow.gain", new Dictionary<string, double> { ["gain"] = 0.3 });
        return new(new(graph, 8000, 128), [Source(440, 0.07), Source(660, 0.03)]);
    }

#if !FLOW_WEB
    [Fact]
    public void LiveCallbackAndOfflineReadUseTheSameGraphSamplesThroughEof()
    {
        var offline = Playback();
        var liveSource = Playback();
        var queue = new QueuedSinePlayback(liveSource);
        var callback = new CallbackRenderProbe(queue, 32, mute: false);
        Assert.True(queue.TryPlay());
        var expected = new float[256]; var actual = new float[256];
        int blocks = (int)((offline.TotalFrames + 127) / 128) + 1;
        for (int i = 0; i < blocks; i++)
        {
            offline.Read(expected);
            callback.Process(actual);
            Assert.Equal(expected, actual);
        }
        Assert.Equal(TransportState.Stopped, queue.State);
        Assert.Equal(offline.TotalFrames, queue.PositionFrames);
        Assert.All(actual, value => Assert.Equal(0, value));
    }

#endif
    [Fact]
    public void SeekLoopPauseAndPublicationWorkWithGraphSources()
    {
        var queue = new QueuedSinePlayback(Playback());
        var output = new float[256];
        Assert.True(queue.TrySetLoop(32, 80));
        Assert.True(queue.TrySeek(32));
        Assert.True(queue.TryPlay());
        queue.Read(output);
        Assert.Equal(64, queue.PositionFrames);
        Assert.True(queue.TryPause());
        queue.Read(output);
        Assert.Equal(64, queue.PositionFrames);
        Assert.All(output, value => Assert.Equal(0, value));
        Assert.True(queue.TryReplace(Playback()));
        queue.Read(output);
        Assert.Equal(TransportState.Stopped, queue.State);
        Assert.Equal(0, queue.PositionFrames);
        Assert.True(queue.TryTakeRetired(out _));
    }

    [Fact]
    public void GraphPlaybackReadAndSeekAllocateNothingAfterPreparation()
    {
        var playback = Playback();
        var output = new float[256];
        for (int i = 0; i < 20; i++) { playback.Read(output); playback.Seek(0); }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 200; i++) { playback.Read(output); playback.Seek(0); }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
