using Flow.Audio;
using Flow.Music.Model;
using Xunit;

namespace FlowLang.Tests.MusicModel;

public class PlaybackPublicationTests
{
    private static PreparedSinePlayback Source(double frequency = 440, double duration = 1,
        int rate = 8000, int block = 128)
    {
        var section = new SectionSnapshot(Guid.NewGuid(), "tone", new(Bpm: 120),
            [new(Guid.NewGuid(), "melody", duration,
                [new(Guid.NewGuid(), "voice", 0, duration, new('A', 4, 0, null, 69, frequency))])]);
        return PreparedSinePlayback.Prepare(new(Guid.NewGuid(), [new(Guid.NewGuid(), section)]), new(rate, block));
    }

    [Fact]
    public void ReplacementInstallsStoppedAtBoundaryAndDiscardsOldScoreCommands()
    {
        var original = Source();
        var queued = new QueuedSinePlayback(original, 4);
        var block = new float[64];
        queued.TryPlay();
        queued.Read(block);
        Assert.Equal(32, queued.PositionFrames);
        queued.TrySeek(3000);
        queued.TrySetLoop(2000, 3000);
        queued.TryPlay();
        var next = Source(660, 0.01);
        Assert.True(queued.TryReplace(next));
        Assert.True(queued.ReplacementPending);
        Assert.Equal(1, queued.Generation);
        Assert.Equal(original.TotalFrames, queued.TotalFrames);
        Assert.False(queued.TrySeek(long.MaxValue)); // Pending rejects before score validation.
        Assert.False(queued.TrySetLoop(0, long.MaxValue));
        Assert.False(queued.TryPlay());
        Assert.Equal(0, queued.Read(block));
        Assert.All(block, sample => Assert.Equal(0, sample));
        Assert.False(queued.ReplacementPending);
        Assert.Equal(2, queued.Generation);
        Assert.Equal(next.TotalFrames, queued.TotalFrames);
        Assert.Equal(0, queued.PositionFrames);
        Assert.Equal(TransportState.Stopped, queued.State);
        Assert.Equal(3, queued.DiscardedCommands);
        Assert.True(queued.TryTakeRetired(out var retired));
        Assert.Equal(32, retired!.PositionFrames); // Old pending seeks never applied.
        Assert.False(queued.TryTakeRetired(out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => queued.TrySeek(3000));
        Assert.True(queued.TryPlay());
        var reference = Source(660, 0.01);
        var expected = new float[64];
        Assert.Equal(reference.Read(expected), queued.Read(block));
        Assert.Equal(expected, block);
        Assert.Equal(reference.Read(expected), queued.Read(block)); // No old loop survives.
        Assert.Equal(expected, block);
        Assert.Equal(TransportState.Stopped, queued.State);
    }

    [Fact]
    public void PendingAndRetiredSlotsApplyBackpressureWithoutTakingCandidateOwnership()
    {
        var queued = new QueuedSinePlayback(Source());
        var candidate = Source(880);
        candidate.Seek(11);
        Assert.True(queued.TryReplace(Source(660)));
        Assert.False(queued.TryReplace(candidate));
        Assert.Equal(11, candidate.PositionFrames);
        Assert.False(queued.TryTakeRetired(out _));
        queued.Read(new float[2]);
        Assert.False(queued.TryReplace(candidate));
        Assert.Equal(11, candidate.PositionFrames);
        Assert.True(queued.TryTakeRetired(out var retired));
        Assert.NotNull(retired);
        Assert.True(queued.TryReplace(candidate));
        Assert.Equal(0, candidate.PositionFrames);
        queued.Read(new float[2]);
        Assert.Equal(3, queued.Generation);
    }

    [Fact]
    public void StopWinsBoundaryAndPendingReplacementCannotRestartPlayback()
    {
        var queued = new QueuedSinePlayback(Source(), 1);
        var block = new float[64];
        queued.TryPlay();
        queued.Read(block);
        queued.TryPlay();
        Assert.True(queued.TryReplace(Source(660)));
        queued.RequestStop();
        Assert.Equal(0, queued.Read(block));
        Assert.Equal(1, queued.Generation);
        Assert.True(queued.ReplacementPending);
        Assert.False(queued.StopPending);
        Assert.False(queued.TryPlay());
        Assert.Equal(0, queued.Read(block));
        Assert.Equal(2, queued.Generation);
        Assert.Equal(TransportState.Stopped, queued.State);
        Assert.Equal(0, queued.PositionFrames);
        Assert.All(block, sample => Assert.Equal(0, sample));
        queued.TryTakeRetired(out _);
        queued.RequestStop();
        var candidate = Source(880);
        candidate.Seek(3);
        Assert.False(queued.TryReplace(candidate));
        Assert.Equal(3, candidate.PositionFrames);
    }

    [Fact]
    public void InvalidAndEmptyReadsDoNotPublishOrRetire()
    {
        var queued = new QueuedSinePlayback(Source());
        Assert.True(queued.TryReplace(Source(660)));
        var odd = new float[] { 42, 42, 42 };
        Assert.Throws<ArgumentException>(() => queued.Read(odd));
        Assert.Throws<ArgumentException>(() => queued.Read(new float[258]));
        Assert.Equal(0, queued.Read(Span<float>.Empty));
        Assert.All(odd, sample => Assert.Equal(42, sample));
        Assert.Equal(1, queued.Generation);
        Assert.True(queued.ReplacementPending);
        Assert.False(queued.TryTakeRetired(out _));
        queued.Read(new float[2]);
        Assert.Equal(2, queued.Generation);
    }

    [Fact]
    public void InvalidReplacementLeavesCurrentPlaybackAndCandidateUntouched()
    {
        var original = Source();
        var queued = new QueuedSinePlayback(original);
        queued.TryPlay();
        queued.Read(new float[64]);
        Assert.Throws<ArgumentException>(() => queued.TryReplace(original));
        Assert.Throws<ArgumentNullException>(() => queued.TryReplace(null!));
        foreach (var candidate in new[] { Source(rate: 16000), Source(block: 64) })
        {
            candidate.Seek(13);
            Assert.Throws<ArgumentException>(() => queued.TryReplace(candidate));
            Assert.Equal(13, candidate.PositionFrames);
        }
        Assert.False(queued.ReplacementPending);
        Assert.Equal(1, queued.Generation);
        Assert.Equal(TransportState.Playing, queued.State);
        Assert.Equal(32, queued.PositionFrames);
        Assert.Equal(32, queued.Read(new float[64]));
    }

    [Fact]
    public void EmptyScoreCanReplaceAndBeReplaced()
    {
        var queued = new QueuedSinePlayback(Source());
        Assert.True(queued.TryReplace(Source(duration: 0)));
        queued.Read(new float[2]);
        Assert.Equal(0, queued.TotalFrames);
        queued.TryPlay();
        Assert.Equal(0, queued.Read(new float[2]));
        Assert.Equal(TransportState.Stopped, queued.State);
        queued.TryTakeRetired(out _);
        Assert.True(queued.TryReplace(Source()));
        queued.Read(new float[2]);
        queued.TryPlay();
        Assert.Equal(1, queued.Read(new float[2]));
    }

    [Fact]
    public async Task ConcurrentPublicationRetiresEachGenerationExactlyOnce()
    {
        const int replacements = 2000;
        var queued = new QueuedSinePlayback(Source(), 4);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        int finished = 0;
        var producer = Task.Run(() =>
        {
            for (int i = 0; i < replacements; i++)
            {
                var candidate = Source(i % 2 == 0 ? 660 : 880, i % 2 == 0 ? 1 : 0.5);
                Assert.True(queued.TryReplace(candidate));
                while (queued.ReplacementPending)
                {
                    deadline.Token.ThrowIfCancellationRequested();
                    Thread.Yield();
                }
                Assert.Equal(i + 2, queued.Generation);
                Assert.Equal(candidate.TotalFrames, queued.TotalFrames);
                Assert.True(queued.TryTakeRetired(out var retired));
                Assert.False(queued.TryTakeRetired(out _));
                // Reclaimed state belongs to this thread; the callback must no
                // longer mutate it even while the next graph is being played.
                retired!.Seek(7);
                queued.TryPlay();
                Thread.Yield();
                Assert.Equal(7, retired.PositionFrames);
            }
            Volatile.Write(ref finished, 1);
        });
        var consumer = Task.Run(() =>
        {
            var block = new float[64];
            while (Volatile.Read(ref finished) == 0)
            {
                deadline.Token.ThrowIfCancellationRequested();
                queued.Read(block);
            }
        });
        try { await Task.WhenAll(producer, consumer); }
        finally { deadline.Cancel(); }
        Assert.Equal(replacements + 1, queued.Generation);
        Assert.False(queued.ReplacementPending);
        Assert.False(queued.TryTakeRetired(out _));
    }

    [Fact]
    public void InstallationAndRetirementHandoffAllocateNothingOnAudioThread()
    {
        var queued = new QueuedSinePlayback(Source());
        var block = new float[64];
        for (int i = 0; i < 100; i++)
        {
            Assert.True(queued.TryReplace(Source(660)));
            queued.Read(block);
            queued.TryTakeRetired(out _);
        }
        long allocated = 0;
        for (int i = 0; i < 200; i++)
        {
            Assert.True(queued.TryReplace(Source(880)));
            long before = GC.GetAllocatedBytesForCurrentThread();
            queued.Read(block);
            allocated += GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.True(queued.TryTakeRetired(out _));
        }
        Assert.Equal(0, allocated);
    }
}
