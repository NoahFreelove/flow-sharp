using Flow.Audio;
using Xunit;

namespace FlowLang.Tests.MusicModel;

public class RecordingTransportTests
{
    [Fact]
    public void RecordingContinuesThroughEofWithoutChangingSourceLengthOrSamples()
    {
        var asset = new PcmAsset(Enumerable.Repeat(.5f, 10).ToArray(), 1000);
        var source = new PreparedPcmPlayback([new(Guid.NewGuid(), asset, 0, 5, 0, 5)], 1000, 16);
        var transport = new PreparedSineTransport(source);
        transport.BeginRecording();
        var block = new float[32];
        Assert.Equal(16, transport.Read(block));
        Assert.All(block.Take(10), x => Assert.Equal(.5f, x));
        Assert.All(block.Skip(10), x => Assert.Equal(0, x));
        Assert.Equal(16, transport.PositionFrames); Assert.Equal(5, transport.TotalFrames);
        Assert.Equal(TransportState.Playing, transport.State);
        Assert.Equal(16, transport.Read(block)); Assert.Equal(32, transport.PositionFrames);
        Assert.All(block, x => Assert.Equal(0, x));
        transport.EndRecording();
        Assert.Equal(5, transport.PositionFrames); Assert.Equal(TransportState.Stopped, transport.State);
        transport.Seek(0); transport.Play();
        Assert.Equal(5, transport.Read(block)); Assert.Equal(TransportState.Stopped, transport.State);
    }

    [Fact]
    public void EmptyRecordingClockAllocatesNothingAndStopClearsExtension()
    {
        var queue = new QueuedSinePlayback(new PreparedPcmPlayback([], 1000, 16));
        var block = new float[32];
        Assert.True(queue.TryBeginRecording(out long token)); queue.Read(block);
        Assert.True(queue.TryReadClock(out var clock));
        Assert.Equal(token, clock.RecordingToken); Assert.True(clock.RecordingEnabled);
        Assert.Equal(0, clock.RecordingStartFrame); Assert.True(clock.RecordingStartTimestamp <= clock.Timestamp);
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) queue.Read(block);
        Assert.Equal(allocated, GC.GetAllocatedBytesForCurrentThread());
        Assert.Equal(1616, queue.PositionFrames); Assert.Equal(0, queue.TotalFrames);
        queue.RequestStop(); queue.Read(block); queue.TryReadClock(out clock);
        Assert.False(clock.RecordingEnabled); Assert.Equal(0, clock.PositionFrames);
        Assert.Equal(TransportState.Stopped, clock.State);
    }

    [Fact]
    public void CommittingLongerSourceAfterReleaseDoesNotAutoplayTheNewTake()
    {
        var queue = new QueuedSinePlayback(new PreparedPcmPlayback([], 1000, 16));
        var block = new float[32];
        Assert.True(queue.TryBeginRecording(out long token)); queue.Read(block);
        queue.RequestEndRecording(token);
        Assert.True(queue.TryReplace(new PreparedPcmPlayback([], 1000, 16, 500), PlaybackReplacementMode.PreserveTransport));
        queue.Read(block);
        Assert.Equal(500, queue.TotalFrames); Assert.Equal(0, queue.PositionFrames);
        Assert.Equal(TransportState.Stopped, queue.State);
        queue.TryReadClock(out var clock); Assert.False(clock.RecordingEnabled);
    }

    [Fact]
    public void ReleaseSurvivesFullQueueAndStaleTokenCannotEndNewRecording()
    {
        var queue = new QueuedSinePlayback(new PreparedPcmPlayback([], 1000, 16), 1);
        var block = new float[32];
        Assert.True(queue.TryBeginRecording(out long first)); queue.Read(block);
        Assert.True(queue.TryPlay()); // Fill the ordinary FIFO.
        queue.RequestEndRecording(first); queue.Read(block);
        queue.TryReadClock(out var clock); Assert.False(clock.RecordingEnabled);
        Assert.True(queue.TryBeginRecording(out long second));
        queue.RequestEndRecording(first); // Late release of old ownership.
        queue.Read(block); queue.TryReadClock(out clock);
        Assert.True(clock.RecordingEnabled); Assert.Equal(second, clock.RecordingToken);
        Assert.True(queue.TryReplace(new PreparedPcmPlayback([], 1000, 16), PlaybackReplacementMode.PreserveTransport));
        queue.Read(block); queue.TryReadClock(out clock);
        Assert.False(clock.RecordingEnabled); Assert.Equal(TransportState.Stopped, clock.State);
    }
}
