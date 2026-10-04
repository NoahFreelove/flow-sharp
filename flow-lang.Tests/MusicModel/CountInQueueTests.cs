using System.Diagnostics;
using Flow.Audio;
using Xunit;

namespace FlowLang.Tests.MusicModel;

public class CountInQueueTests
{
    private static PreparedPcmPlayback Lead(int frames = 21) => new(
        [new(Guid.NewGuid(), new PcmAsset(Enumerable.Repeat(.25f, frames * 2).ToArray(), 1000), 0, frames, 0, frames)], 1000, 16);
    [Fact]
    public void ClockPublishesCountInProgressAndTheIntraBlockRecordingBoundary()
    {
        var queue = new QueuedSinePlayback(new PreparedPcmPlayback([], 1000, 16));
        Assert.True(queue.TryBeginRecording(Lead(), out var token));
        var output = new float[32]; Assert.Equal(0, queue.Read(output));
        Assert.True(queue.TryReadClock(out var pending));
        Assert.Equal(token, pending.RecordingToken); Assert.False(pending.RecordingEnabled);
        Assert.Equal(5, pending.CountInRemainingFrames); Assert.Equal(0, pending.PositionFrames);
        Assert.Equal(0, pending.RecordingStartTimestamp);
        long before = Stopwatch.GetTimestamp(); Assert.Equal(11, queue.Read(output)); long after = Stopwatch.GetTimestamp();
        Assert.True(queue.TryReadClock(out var started));
        Assert.True(started.RecordingEnabled); Assert.Equal(0, started.CountInRemainingFrames);
        Assert.Equal(11, started.PositionFrames); Assert.Equal(0, started.RecordingStartFrame);
        long offset = Stopwatch.Frequency * 5 / 1000;
        Assert.InRange(started.RecordingStartTimestamp, before + offset, after + offset);
        Assert.All(output[..10], v => Assert.Equal(.25f, v)); Assert.All(output[10..], v => Assert.Equal(0, v));
    }

    [Fact]
    public void ReleaseSurvivesFullQueueAndRejectedLeadRemainsCallerOwned()
    {
        var queue = new QueuedSinePlayback(new PreparedPcmPlayback([], 1000, 16), capacity: 1);
        Assert.True(queue.TryBeginRecording(Lead(100), out var token));
        var rejected = Lead(); Assert.False(queue.TryBeginRecording(rejected, out _)); Assert.Equal(0, rejected.PositionFrames);
        var output = new float[32]; queue.Read(output);
        Assert.True(queue.TryPlay()); queue.RequestEndRecording(token);
        queue.Read(output); Assert.True(queue.TryReadClock(out var cancelled));
        Assert.False(cancelled.RecordingEnabled); Assert.Equal(0, cancelled.CountInRemainingFrames);
        Assert.Equal(TransportState.Stopped, cancelled.State); Assert.All(output, v => Assert.Equal(0, v));
        Assert.True(queue.TryBeginRecording(rejected, out var next));
        queue.RequestEndRecording(token); queue.Read(output);
        Assert.True(queue.TryReadClock(out var newer)); Assert.Equal(next, newer.RecordingToken);
        Assert.Equal(5, newer.CountInRemainingFrames); // Stale release cannot cancel the new lead-in.
        queue.RequestStop(); queue.Read(output);
        Assert.True(queue.TryReadClock(out var stopped)); Assert.Equal(0, stopped.CountInRemainingFrames);
        Assert.False(stopped.RecordingEnabled);
    }
}
