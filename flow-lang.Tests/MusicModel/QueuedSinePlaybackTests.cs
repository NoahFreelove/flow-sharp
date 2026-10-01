using Flow.Audio;
using Flow.Music.Model;
using Xunit;

namespace FlowLang.Tests.MusicModel;

public class QueuedSinePlaybackTests
{
    private static PreparedSinePlayback Source()
    {
        var section = new SectionSnapshot(Guid.NewGuid(), "tone", new(Bpm: 120),
            [new(Guid.NewGuid(), "melody", 1,
                [new(Guid.NewGuid(), "voice", 0, 1, new('A', 4, 0, null, 69, 440))])]);
        return PreparedSinePlayback.Prepare(new(Guid.NewGuid(), [new(Guid.NewGuid(), section, 1000)]), new(8000, 128));
    }

    [Fact]
    public void CommandsApplyInOrderAtBlockBoundariesAndMatchDirectPlayback()
    {
        var queued = new QueuedSinePlayback(Source(), 4);
        var direct = new PreparedSineTransport(Source());
        var actual = new float[128];
        var expected = new float[128];
        Assert.True(queued.TrySeek(19));
        Assert.True(queued.TrySetLoop(20, 31));
        Assert.True(queued.TryPlay());
        Assert.Equal(0, queued.PositionFrames);
        Assert.Equal(TransportState.Stopped, queued.State);
        direct.Seek(19);
        direct.SetLoop(20, 31);
        direct.Play();
        Assert.Equal(direct.Read(expected), queued.Read(actual));
        Assert.Equal(expected, actual);
        Assert.Equal(direct.PositionFrames, queued.PositionFrames);
        Assert.True(queued.TryPause());
        Assert.Equal(0, queued.Read(actual));
        Assert.Equal(TransportState.Paused, queued.State);
        Assert.All(actual, value => Assert.Equal(0, value));
        Assert.True(queued.TryClearLoop());
        Assert.True(queued.TrySeek(45));
        Assert.True(queued.TryPlay());
        direct.ClearLoop();
        direct.Seek(45);
        Assert.Equal(direct.Read(expected), queued.Read(actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void FullQueueRejectsWithoutOverwritingAndWrapsRepeatedly(int capacity)
    {
        var queued = new QueuedSinePlayback(Source(), capacity);
        var block = new float[2];
        for (int iteration = 0; iteration < 100; iteration++)
        {
            for (int i = 0; i < capacity; i++) Assert.True(queued.TrySeek(iteration * capacity + i));
            Assert.False(queued.TryPlay());
            Assert.Equal(0, queued.Read(block));
            Assert.Equal(iteration * capacity + capacity - 1, queued.PositionFrames);
            Assert.Equal(TransportState.Stopped, queued.State);
        }
        Assert.Equal(100, queued.RejectedCommands);
        Assert.Equal(0, queued.DiscardedCommands);
    }

    [Fact]
    public void StopSurvivesSaturationDiscardsStalePlayAndRequiresAcknowledgment()
    {
        var queued = new QueuedSinePlayback(Source(), 2);
        var block = new float[64];
        Assert.True(queued.TryPlay());
        Assert.Equal(32, queued.Read(block));
        Assert.True(queued.TrySeek(99));
        Assert.True(queued.TryPlay());
        queued.RequestStop();
        queued.RequestStop();
        Assert.True(queued.StopPending);
        Assert.False(queued.TryPlay());
        Assert.False(queued.TryPause());
        Assert.Equal(0, queued.Read(block));
        Assert.All(block, sample => Assert.Equal(0, sample));
        Assert.False(queued.StopPending);
        Assert.Equal(0, queued.PositionFrames);
        Assert.Equal(TransportState.Stopped, queued.State);
        Assert.Equal(2, queued.DiscardedCommands);
        Assert.Equal(2, queued.RejectedCommands);
        Assert.True(queued.TryPlay());
        Assert.Equal(32, queued.Read(block));
        Assert.Equal(32, queued.PositionFrames);
        queued.RequestStop();
        queued.Read(block);
        Assert.Equal(2, queued.DiscardedCommands);
    }

    [Fact]
    public void InvalidAndEmptyBuffersDoNotDrainOrAcknowledgeStop()
    {
        var queued = new QueuedSinePlayback(Source(), 1);
        Assert.True(queued.TryPlay());
        var odd = new float[] { 42, 42, 42 };
        var large = Enumerable.Repeat(42f, 258).ToArray();
        Assert.Throws<ArgumentException>(() => queued.Read(odd));
        Assert.Throws<ArgumentException>(() => queued.Read(large));
        Assert.Equal(0, queued.Read(Span<float>.Empty));
        Assert.False(queued.TryPause());
        queued.RequestStop();
        Assert.Throws<ArgumentException>(() => queued.Read(odd));
        queued.Read(Span<float>.Empty);
        Assert.True(queued.StopPending);
        Assert.Equal(TransportState.Stopped, queued.State);
        Assert.All(odd.Concat(large), value => Assert.Equal(42, value));
        queued.Read(new float[2]);
        Assert.False(queued.StopPending);
        Assert.Equal(1, queued.DiscardedCommands);
    }

    [Fact]
    public void InvalidProducerArgumentsAreRejectedBeforePublication()
    {
        var source = Source();
        source.Seek(99);
        Assert.Throws<ArgumentOutOfRangeException>(() => new QueuedSinePlayback(source, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new QueuedSinePlayback(source, 65_537));
        Assert.Equal(99, source.PositionFrames);
        Assert.Throws<ArgumentNullException>(() => new QueuedSinePlayback(null!));
        var queued = new QueuedSinePlayback(source, 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => queued.TrySeek(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => queued.TrySeek(queued.TotalFrames + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => queued.TrySetLoop(-1, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => queued.TrySetLoop(2, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => queued.TrySetLoop(0, queued.TotalFrames + 1));
        Assert.True(queued.TrySeek(queued.TotalFrames));
        queued.Read(new float[2]);
        Assert.Equal(queued.TotalFrames, queued.PositionFrames);
        Assert.Equal(0, queued.RejectedCommands);
    }

    [Fact]
    public async Task ConcurrentProducerAndConsumerPreserveOrderThroughWrapAndSaturation()
    {
        var queued = new QueuedSinePlayback(Source(), 7);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        const int commands = 100_000;
        var producer = Task.Run(() =>
        {
            for (int i = 1; i <= commands; i++)
                while (!queued.TrySeek(i))
                {
                    deadline.Token.ThrowIfCancellationRequested();
                    Thread.Yield();
                }
        });
        var consumer = Task.Run(() =>
        {
            var block = new float[2];
            long previous = 0;
            while (previous != commands)
            {
                deadline.Token.ThrowIfCancellationRequested();
                queued.Read(block);
                long current = queued.PositionFrames;
                Assert.InRange(current, previous, commands);
                previous = current;
            }
        });
        try { await Task.WhenAll(producer, consumer); }
        finally { deadline.Cancel(); }
        Assert.Equal(commands, queued.PositionFrames);
        Assert.Equal(TransportState.Stopped, queued.State);
    }

    [Fact]
    public async Task ConcurrentStopAcknowledgmentPreventsStaleRestart()
    {
        var queued = new QueuedSinePlayback(Source(), 3);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        int finished = 0;
        var producer = Task.Run(() =>
        {
            for (int i = 0; i < 5000; i++)
            {
                queued.TryPlay();
                queued.TrySeek(33);
                queued.TryPlay();
                queued.RequestStop();
                while (queued.StopPending)
                {
                    deadline.Token.ThrowIfCancellationRequested();
                    Thread.Yield();
                }
            }
            Volatile.Write(ref finished, 1);
        });
        var consumer = Task.Run(() =>
        {
            var block = new float[2];
            while (Volatile.Read(ref finished) == 0)
            {
                deadline.Token.ThrowIfCancellationRequested();
                queued.Read(block);
            }
            queued.Read(block);
            Assert.Equal(TransportState.Stopped, queued.State);
            Assert.Equal(0, queued.PositionFrames);
            Assert.All(block, sample => Assert.Equal(0, sample));
        });
        try { await Task.WhenAll(producer, consumer); }
        finally { deadline.Cancel(); }
    }

    [Fact]
    public void CommandsOverflowStopAndReadsAllocateNothingAfterConstruction()
    {
        var queued = new QueuedSinePlayback(Source(), 4);
        var block = new float[128];
        void Cycle()
        {
            queued.TrySeek(9);
            queued.TrySetLoop(10, 30);
            queued.TryPlay();
            queued.TryClearLoop();
            queued.TryPause(); // Deliberate overflow.
            queued.Read(block);
            queued.TryPause();
            queued.Read(block);
            queued.TryPlay();
            queued.RequestStop();
            queued.TryPlay(); // Rejected until stop acknowledgment.
            queued.Read(block);
        }
        for (int i = 0; i < 100; i++) Cycle();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 200; i++) Cycle();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
    }
}
