using Flow.Audio;
using Flow.Music.Model;
using Xunit;

namespace FlowLang.Tests.MusicModel;

public class PreparedSineTransportTests
{
    private static CompositionSnapshot Score()
    {
        var first = new SectionSnapshot(Guid.NewGuid(), "first", new(Bpm: 120),
            [new(Guid.NewGuid(), "melody", 0.04,
                [new(Guid.NewGuid(), "voice", 0, 0.04, new('A', 4, 0, null, 69, 440))])]);
        var second = new SectionSnapshot(Guid.NewGuid(), "second", new(Bpm: 90),
            [new(Guid.NewGuid(), "melody", 0.03,
                [new(Guid.NewGuid(), "voice", 0, 0.03, new('C', 5, 0, null, 72, 523.25))])]);
        return new(Guid.NewGuid(), [new(Guid.NewGuid(), first, 2), new(Guid.NewGuid(), second)]);
    }

    private static PreparedSineTransport Transport(CompositionSnapshot? score = null) =>
        new(PreparedSinePlayback.Prepare(score ?? Score(), new(8000, 1024)));

    private static float[] Offline()
    {
        var samples = new List<float>();
        SineCompositionRenderer.Render(Score(), block => samples.AddRange(block.ToArray()), new(8000, 127));
        return samples.ToArray();
    }

    [Fact]
    public void PlayPauseResumeStopAndSeekPreserveSamplesAndState()
    {
        var transport = Transport();
        var expected = Offline();
        var block = Enumerable.Repeat(float.NaN, 74).ToArray();
        Assert.Equal(TransportState.Stopped, transport.State);
        transport.Pause();
        Assert.Equal(TransportState.Stopped, transport.State);
        Assert.Equal(0, transport.Read(block));
        Assert.All(block, sample => Assert.Equal(0, sample));
        transport.Play();
        transport.Play();
        Assert.Equal(37, transport.Read(block));
        Assert.Equal(expected[..74], block);
        transport.Pause();
        transport.Pause();
        Assert.Equal(0, transport.Read(block));
        Assert.Equal(37, transport.PositionFrames);
        Assert.All(block, sample => Assert.Equal(0, sample));
        transport.Seek(51);
        Assert.Equal(TransportState.Paused, transport.State);
        transport.Play();
        Assert.Equal(37, transport.Read(block));
        Assert.Equal(expected[102..176], block);
        transport.Stop();
        transport.Stop();
        Assert.Equal(0, transport.PositionFrames);
        Assert.Equal(TransportState.Stopped, transport.State);
        transport.Seek(19);
        Assert.Equal(TransportState.Stopped, transport.State);
        transport.Play();
        transport.Read(block);
        Assert.Equal(expected[38..112], block);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(32)]
    public void ExactAndPartialEofStopAtEndAndZeroRemainder(int blockFrames)
    {
        var transport = Transport();
        var expected = Offline();
        transport.Seek(transport.TotalFrames - 7);
        transport.Play();
        var block = Enumerable.Repeat(float.NaN, blockFrames * 2).ToArray();
        Assert.Equal(7, transport.Read(block));
        Assert.Equal(expected[^14..], block[..14]);
        Assert.All(block.Skip(14), sample => Assert.Equal(0, sample));
        Assert.Equal(transport.TotalFrames, transport.PositionFrames);
        Assert.Equal(TransportState.Stopped, transport.State);
        transport.Play();
        Assert.Equal(TransportState.Stopped, transport.State);
        Assert.Equal(0, transport.Read(block));
        Assert.All(block, sample => Assert.Equal(0, sample));
        transport.Stop();
        transport.Play();
        Assert.Equal(blockFrames, transport.Read(block));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(127)]
    [InlineData(1024)]
    public void LoopsMatchOfflineSlicesAcrossBlocksRepeatsAndTempos(int blockFrames)
    {
        var transport = Transport();
        var expected = Offline();
        // Range includes both repeat and tempo boundaries, plus a lead-in.
        long start = 13, end = transport.TotalFrames - 11, position = 0;
        transport.SetLoop(start, end);
        transport.Play();
        var block = new float[blockFrames * 2];
        for (int iteration = 0; iteration < 1500 / blockFrames + 3; iteration++)
        {
            Assert.Equal(blockFrames, transport.Read(block));
            for (int frame = 0; frame < blockFrames; frame++)
            {
                Assert.Equal(expected[(int)position * 2], block[frame * 2]);
                Assert.Equal(expected[(int)position * 2 + 1], block[frame * 2 + 1]);
                if (++position == end) position = start;
            }
            Assert.Equal(position, transport.PositionFrames);
            Assert.Equal(TransportState.Playing, transport.State);
        }
    }

    [Fact]
    public void OneFrameLoopWrapsAtExactBlockEndAndCanBeDisabled()
    {
        var transport = Transport();
        var expected = Offline();
        transport.Seek(transport.TotalFrames);
        transport.SetLoop(9, 10);
        Assert.Equal(transport.TotalFrames, transport.PositionFrames);
        transport.Play();
        var block = new float[128];
        Assert.Equal(64, transport.Read(block));
        for (int i = 0; i < 64; i++) Assert.Equal(expected[18..20], block[(i * 2)..(i * 2 + 2)]);
        Assert.Equal(9, transport.PositionFrames);
        transport.Pause();
        transport.SetLoop(21, 30);
        Assert.Equal(9, transport.PositionFrames);
        Assert.Equal(TransportState.Paused, transport.State);
        transport.Seek(30);
        transport.Play();
        Assert.Equal(9, transport.Read(block.AsSpan(0, 18)));
        Assert.Equal(expected[42..60], block[..18]);
        Assert.Equal(21, transport.PositionFrames);
        transport.ClearLoop();
        Assert.False(transport.LoopEnabled);
        Assert.Equal(TransportState.Playing, transport.State);
        transport.Read(block);
        Assert.Equal(expected[42..170], block);
        transport.SetLoop(9, 10);
        transport.Stop();
        Assert.True(transport.LoopEnabled);
        Assert.Equal(0, transport.PositionFrames);
    }

    [Fact]
    public void LoopAtScoreEndUsesLongFramePositionsAndSurvivesPause()
    {
        var section = Score().Placements[0].Section;
        var score = new CompositionSnapshot(Guid.NewGuid(), [new(Guid.NewGuid(), section, int.MaxValue)]);
        var transport = Transport(score);
        Assert.True(transport.TotalFrames > int.MaxValue);
        long start = transport.TotalFrames - 3;
        var reference = PreparedSinePlayback.Prepare(score, new(8000, 1024));
        reference.Seek(start);
        var expected = new float[6];
        reference.Read(expected);
        transport.SetLoop(start, transport.TotalFrames);
        transport.Seek(start);
        transport.Play();
        var block = new float[6];
        Assert.Equal(3, transport.Read(block));
        Assert.Equal(expected, block);
        Assert.Equal(start, transport.PositionFrames);
        Assert.Equal(TransportState.Playing, transport.State);
        transport.Pause();
        Assert.Equal(0, transport.Read(block));
        Assert.Equal(start, transport.PositionFrames);
        transport.Play();
        Assert.Equal(3, transport.Read(block));
        Assert.Equal(expected, block);
        transport.ClearLoop();
        Assert.Equal(3, transport.Read(block));
        Assert.Equal(TransportState.Stopped, transport.State);
        Assert.Equal(transport.TotalFrames, transport.PositionFrames);
    }

    [Fact]
    public void EmptyReadsDoNotResolveSeekToEofButNextCallbackDoes()
    {
        var transport = Transport();
        transport.Play();
        transport.Seek(transport.TotalFrames);
        Assert.Equal(0, transport.Read(Span<float>.Empty));
        Assert.Equal(TransportState.Playing, transport.State);
        Assert.Equal(0, transport.Read(new float[2]));
        Assert.Equal(TransportState.Stopped, transport.State);
    }

    [Theory]
    [InlineData(TransportState.Stopped)]
    [InlineData(TransportState.Paused)]
    [InlineData(TransportState.Playing)]
    public void InvalidCommandsLeaveBufferCursorStateAndLoopUnchanged(TransportState state)
    {
        var transport = Transport();
        transport.SetLoop(11, 20);
        transport.Seek(17);
        if (state != TransportState.Stopped) transport.Play();
        if (state == TransportState.Paused) transport.Pause();
        var odd = new float[] { 42, 42, 42 };
        var large = Enumerable.Repeat(42f, 2050).ToArray();
        Assert.Throws<ArgumentException>(() => transport.Read(odd));
        Assert.Throws<ArgumentException>(() => transport.Read(large));
        Assert.Throws<ArgumentOutOfRangeException>(() => transport.Seek(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => transport.Seek(transport.TotalFrames + 1));
        foreach (var (start, end) in new (long, long)[] { (-1, 20), (20, 20), (21, 20),
                     (0, transport.TotalFrames + 1), (transport.TotalFrames, transport.TotalFrames) })
            Assert.Throws<ArgumentOutOfRangeException>(() => transport.SetLoop(start, end));
        Assert.Equal(17, transport.PositionFrames);
        Assert.Equal(state, transport.State);
        Assert.True(transport.LoopEnabled);
        Assert.Equal(11, transport.LoopStartFrames);
        Assert.Equal(20, transport.LoopEndFrames);
        Assert.All(odd.Concat(large), value => Assert.Equal(42, value));
    }

    [Fact]
    public void EmptyScoreCannotPlayOrLoopAndConstructionResetsOwnedCursor()
    {
        var source = PreparedSinePlayback.Prepare(Score());
        source.Seek(17);
        var transport = new PreparedSineTransport(source);
        Assert.Equal(0, transport.PositionFrames);
        Assert.Equal(TransportState.Stopped, transport.State);
        Assert.Throws<ArgumentNullException>(() => new PreparedSineTransport(null!));
        var empty = Transport(new(Guid.NewGuid(), []));
        empty.Play();
        Assert.Equal(TransportState.Stopped, empty.State);
        var block = new float[] { 1, 2 };
        Assert.Equal(0, empty.Read(block));
        Assert.Equal(new float[2], block);
        Assert.Throws<ArgumentOutOfRangeException>(() => empty.SetLoop(0, 1));
    }

    [Fact]
    public void TransportOperationsAllocateNothingAfterWarmup()
    {
        var transport = Transport();
        var block = new float[254];
        void Cycle()
        {
            transport.Stop();
            transport.SetLoop(11, 12);
            transport.Play();
            transport.Read(block);
            transport.Pause();
            transport.Read(block);
            transport.Seek(transport.TotalFrames);
            transport.Play();
            transport.Read(block);
            transport.ClearLoop();
            transport.Seek(transport.TotalFrames - 1);
            transport.Read(block);
            transport.Read(block);
        }
        for (int i = 0; i < 100; i++) Cycle();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 200; i++) Cycle();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
    }
}
