using Flow.Audio;
using Flow.Music.Model;
using Xunit;

namespace FlowLang.Tests.MusicModel;

public class PreparedSinePlaybackTests
{
    private static SectionSnapshot Section(double bpm = 137, double duration = 0.07) =>
        new(Guid.NewGuid(), "phrase", new(Bpm: bpm, Pan: -0.3, VoicePoolSize: 1),
            [new(Guid.NewGuid(), "melody", duration,
                [new(Guid.NewGuid(), "voice", -0.01, 0.1, new('A', 4, 0, null, 69, 440)),
                 new(Guid.NewGuid(), "voice", 0.02, 0.08, new('C', 5, 0, null, 72, 523.25))])]);

    private static CompositionSnapshot Score()
    {
        var phrase = Section();
        return new(Guid.NewGuid(), [new(Guid.NewGuid(), phrase, 3),
            new(Guid.NewGuid(), Section(83), 0), new(Guid.NewGuid(), Section(duration: 0)),
            new(Guid.NewGuid(), Section(83), 2), new(Guid.NewGuid(), phrase)]);
    }

    private static float[] Offline(CompositionSnapshot score, int rate = 8000)
    {
        var result = new List<float>();
        SineCompositionRenderer.Render(score, block => result.AddRange(block.ToArray()), new(rate, 127));
        return result.ToArray();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(127)]
    [InlineData(1024)]
    public void PullMatchesOfflineAcrossTemposRepeatsAndPartialBlocks(int size)
    {
        var score = Score();
        var expected = Offline(score);
        var playback = PreparedSinePlayback.Prepare(score, new(8000, size));
        Assert.Equal(expected.Length / 2, playback.TotalFrames);
        var actual = new List<float>();
        var block = new float[size * 2];
        while (playback.PositionFrames < playback.TotalFrames)
        {
            Array.Fill(block, float.NaN);
            int count = playback.Read(block);
            actual.AddRange(block.AsSpan(0, count * 2).ToArray());
            Assert.All(block.Skip(count * 2), value => Assert.Equal(0, value));
        }
        Assert.Equal(expected, actual);
        Assert.Equal(0, playback.Read(block));
        Assert.All(block, value => Assert.Equal(0, value));
    }

    [Fact]
    public void SeekRestoresExactSamplesAndInstancesHaveIndependentCursors()
    {
        var score = Score();
        var expected = Offline(score);
        var first = PreparedSinePlayback.Prepare(score, new(8000, 257));
        var second = PreparedSinePlayback.Prepare(score, new(8000, 257));
        var block = new float[514];
        foreach (long position in new long[] { 0, 37, 201, 503, first.TotalFrames - 1, first.TotalFrames, 17 })
        {
            first.Seek(position);
            int count = first.Read(block);
            Assert.Equal(expected.AsSpan((int)position * 2, count * 2).ToArray(), block[..(count * 2)]);
            Assert.Equal(position + count, first.PositionFrames);
            Assert.Equal(0, second.PositionFrames);
        }
        first.Reset();
        Assert.Equal(0, first.PositionFrames);
    }

    [Fact]
    public void InvalidReadsAndSeeksDoNotChangeOutputOrPosition()
    {
        var playback = PreparedSinePlayback.Prepare(Score(), new(8000, 16));
        playback.Seek(5);
        var odd = Enumerable.Repeat(42f, 3).ToArray();
        var large = Enumerable.Repeat(42f, 34).ToArray();
        Assert.Throws<ArgumentException>(() => playback.Read(odd));
        Assert.Throws<ArgumentException>(() => playback.Read(large));
        Assert.Throws<ArgumentOutOfRangeException>(() => playback.Seek(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => playback.Seek(playback.TotalFrames + 1));
        Assert.All(odd.Concat(large), value => Assert.Equal(42, value));
        Assert.Equal(0, playback.Read(Span<float>.Empty));
        Assert.Equal(5, playback.PositionFrames);
    }

    [Fact]
    public void PreparationEnforcesBudgetsCancellationAndUnsupportedSettings()
    {
        var section = Section();
        var shared = new CompositionSnapshot(Guid.NewGuid(),
            [new(Guid.NewGuid(), section, int.MaxValue), new(Guid.NewGuid(), section)]);
        var playback = PreparedSinePlayback.Prepare(shared, new(8000), maxPreparedNotes: 2);
        Assert.True(playback.TotalFrames > int.MaxValue);
        playback.Seek(playback.TotalFrames - 5);
        Assert.Equal(5, playback.Read(new float[20]));
        Assert.Throws<InvalidOperationException>(() => PreparedSinePlayback.Prepare(shared, maxPreparedNotes: 1));
        Assert.Throws<InvalidOperationException>(() => PreparedSinePlayback.Prepare(shared, maxPlacements: 1));
        var distinct = new CompositionSnapshot(Guid.NewGuid(),
            [new(Guid.NewGuid(), section), new(Guid.NewGuid(), Section())]);
        Assert.Throws<InvalidOperationException>(() => PreparedSinePlayback.Prepare(distinct, maxPreparedNotes: 3));
        Assert.Throws<OperationCanceledException>(() => PreparedSinePlayback.Prepare(shared,
            cancellation: new CancellationToken(true)));
        var wet = new CompositionSnapshot(Guid.NewGuid(), [new(Guid.NewGuid(),
            new(Guid.NewGuid(), "wet", new(ReverbSeconds: 1), []))]);
        Assert.Throws<NotSupportedException>(() => PreparedSinePlayback.Prepare(wet));
    }

    [Fact]
    public void EmptyScoreIsSilenceAndStillHonorsCancellation()
    {
        var score = new CompositionSnapshot(Guid.NewGuid(), []);
        var playback = PreparedSinePlayback.Prepare(score);
        var output = new float[] { 1, 2, 3, 4 };
        Assert.Equal(0, playback.Read(output));
        Assert.Equal(new float[4], output);
        playback.Seek(0);
        playback.Reset();
        Assert.Equal(0, playback.TotalFrames);
        Assert.Throws<OperationCanceledException>(() => PreparedSinePlayback.Prepare(score,
            cancellation: new CancellationToken(true)));
    }

    [Fact]
    public void ReadsSeeksResetsAndBoundaryCrossingsAllocateNothingAfterPreparation()
    {
        var playback = PreparedSinePlayback.Prepare(Score(), new(8000, 1024));
        var block = new float[2048];
        for (int i = 0; i < 100; i++) { playback.Reset(); playback.Read(block); }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 200; i++)
        {
            playback.Reset();
            while (playback.PositionFrames < playback.TotalFrames) playback.Read(block);
            playback.Seek(39);
            playback.Read(block);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
    }
}
