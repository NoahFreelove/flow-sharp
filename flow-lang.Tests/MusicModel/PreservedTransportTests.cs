using Flow.Audio;
using Xunit;
namespace FlowLang.Tests.MusicModel;
public class PreservedTransportTests
{
    private sealed class Cursor(long length) : IPreparedAudioPlayback
    {
        public int SampleRate => 8000;
        public int MaxBlockFrames => 16;
        public long TotalFrames => length;
        public long PositionFrames { get; private set; }
        public void Reset() => PositionFrames = 0;
        public void Seek(long frame) => PositionFrames = frame;
        public int Read(Span<float> output)
        {
            output.Clear(); int count = (int)Math.Min(output.Length / 2, length - PositionFrames);
            for (int i = 0; i < count; i++) { output[i * 2] = output[i * 2 + 1] = PositionFrames++; }
            return count;
        }
    }
    [Fact]
    public void PlayingReplacementUsesBoundaryPositionAndConsumesEarlierCommands()
    {
        var queue = new QueuedSinePlayback(new Cursor(1000)); float[] block = new float[8];
        queue.TryPlay(); queue.Read(block); Assert.Equal(4, queue.PositionFrames);
        queue.TrySeek(100); queue.TrySetLoop(100, 110);
        Assert.True(queue.TryReplace(new Cursor(2000), PlaybackReplacementMode.PreserveTransport));
        Assert.Equal(4, queue.Read(block)); Assert.Equal(new float[] { 100,100,101,101,102,102,103,103 }, block);
        Assert.Equal(104, queue.PositionFrames); Assert.Equal(TransportState.Playing, queue.State);
        queue.Read(block); queue.Read(block); Assert.Equal(102, queue.PositionFrames);
        Assert.Equal(0, queue.DiscardedCommands);
        Assert.True(queue.TryTakeRetired(out var retired)); Assert.Equal(100, retired!.PositionFrames);
    }
    [Theory]
    [InlineData(120, 104, TransportState.Playing)] // Full loop survives.
    [InlineData(102, 100, TransportState.Playing)] // Loop end clamps, wraps twice.
    [InlineData(100, 100, TransportState.Stopped)] // Loop start no longer exists.
    [InlineData(0, 0, TransportState.Stopped)]
    public void ShorterProjectsClampCursorAndValidLoop(long length, long position, TransportState state)
    {
        var queue = new QueuedSinePlayback(new Cursor(1000));
        queue.TrySeek(100); queue.TrySetLoop(100, 110); queue.TryPlay();
        Assert.True(queue.TryReplace(new Cursor(length), PlaybackReplacementMode.PreserveTransport));
        queue.Read(new float[8]); Assert.Equal(position, queue.PositionFrames); Assert.Equal(state, queue.State);
    }
    [Fact]
    public void PausedReplacementRemainsPausedAndStopStillWins()
    {
        var queue = new QueuedSinePlayback(new Cursor(1000)); float[] block = new float[8];
        queue.TrySeek(25); queue.TryPlay(); queue.TryPause();
        queue.TryReplace(new Cursor(2000), PlaybackReplacementMode.PreserveTransport);
        Assert.Equal(0, queue.Read(block)); Assert.Equal(25, queue.PositionFrames); Assert.Equal(TransportState.Paused, queue.State);
        queue.TryTakeRetired(out _); queue.TryPlay(); queue.Read(block);
        queue.TryReplace(new Cursor(3000), PlaybackReplacementMode.PreserveTransport); queue.RequestStop();
        Assert.Equal(0, queue.Read(block)); Assert.True(queue.ReplacementPending);
        Assert.Equal(0, queue.Read(block)); Assert.False(queue.ReplacementPending);
        Assert.Equal(0, queue.PositionFrames); Assert.Equal(TransportState.Stopped, queue.State);
    }
    [Fact]
    public void PreservedInstallationAllocatesNothingOnAudioOwner()
    {
        var queue = new QueuedSinePlayback(new Cursor(10000)); float[] block = new float[8]; queue.TryPlay();
        for (int i = 0; i < 100; i++)
        { queue.TryReplace(new Cursor(10000), PlaybackReplacementMode.PreserveTransport); queue.Read(block); queue.TryTakeRetired(out _); }
        var candidate = new Cursor(10000); Assert.True(queue.TryReplace(candidate, PlaybackReplacementMode.PreserveTransport));
        long allocated = GC.GetAllocatedBytesForCurrentThread(); queue.Read(block);
        long delta = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Assert.Equal(0, delta); Assert.Equal(404, queue.PositionFrames);
    }
}
