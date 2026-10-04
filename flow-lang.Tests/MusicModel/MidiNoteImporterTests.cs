using System.Buffers.Binary;
using Flow.Music.IO;
using Xunit;

namespace FlowLang.Tests.MusicModel;

public class MidiNoteImporterTests
{
    private static byte[] File(params byte[] track)
    {
        var data = new byte[22 + track.Length];
        "MThd"u8.CopyTo(data); BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(4), 6);
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(10), 1); BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(12), 480);
        "MTrk"u8.CopyTo(data.AsSpan(14)); BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(18), (uint)track.Length);
        track.CopyTo(data, 22); return data;
    }
    [Fact]
    public void RunningStatusAndOverlappingSameKeyNotesPairFifoAndRetainChannelAndTempo()
    {
        var data = File(0, 0xff, 0x51, 3, 7, 0xa1, 0x20,
            0, 0xff, 0x58, 4, 4, 2, 24, 8,
            0, 0x92, 69, 100, 120, 69, 80,
            120, 0x82, 69, 0, 120, 69, 0, 120, 0xff, 0x2f, 0);
        var imported = MidiNoteImporter.Read(data, TestContext.Current.CancellationToken);
        var part = Assert.Single(imported.Parts); Assert.Equal(2, part.Channel); Assert.Equal(1, part.DurationQuarters);
        Assert.Equal(2, part.Notes.Count); Assert.Equal(0, part.Notes[0].OffsetQuarters); Assert.Equal(.25, part.Notes[1].OffsetQuarters);
        Assert.All(part.Notes, n => { Assert.Equal(.5, n.DurationQuarters); Assert.Equal(440, n.Pitch!.FrequencyHz); });
        Assert.Equal(100 / 127.0, part.Notes[0].Velocity); Assert.Equal(120, Assert.Single(imported.Tempo).Bpm);
        Assert.Equal(4, Assert.Single(imported.Meter).Denominator); Assert.Empty(imported.Diagnostics);
    }
    [Theory]
    [InlineData("ragtime_q_ee.mid")]
    [InlineData("two_voice_counterpoint.mid")]
    [InlineData("drum_loop.mid")]
    public void CommittedMidiFixturesProduceDetachedNotes(string name)
    {
        var path = Path.Combine(FlowLang.Tests.Characterization.Snapshot.RepoRoot, "flow-lang.Tests", "fixtures", "midi", name);
        var imported = MidiNoteImporter.Read(System.IO.File.ReadAllBytes(path), TestContext.Current.CancellationToken);
        Assert.NotEmpty(imported.Parts); Assert.True(imported.Parts.Sum(p => p.Notes.Count) > 0);
        Assert.All(imported.Parts.SelectMany(p => p.Notes), n => Assert.InRange(n.Pitch!.MidiKey, 0, 127));
    }
    [Fact]
    public void IgnoredControlsOrphanOffsAndUnfinishedNotesHaveExplicitDiagnostics()
    {
        var imported = MidiNoteImporter.Read(File(0, 0xb0, 64, 127, 0, 0x80, 60, 0,
            0, 0x90, 69, 100, 120, 0xff, 0x2f, 0));
        Assert.Equal(.25, Assert.Single(Assert.Single(imported.Parts).Notes).DurationQuarters);
        Assert.Equal(3, imported.Diagnostics.Count);
    }
    [Fact]
    public void TruncatedChunksInvalidRunningStatusAndUnsupportedTimingFailClearly()
    {
        var valid = File(0, 0x90, 60, 100, 120, 0x80, 60, 0, 0, 0xff, 0x2f, 0);
        for (int length = 0; length < valid.Length; length++)
            Assert.ThrowsAny<InvalidDataException>(() => MidiNoteImporter.Read(valid[..length]));
        Assert.Throws<InvalidDataException>(() => MidiNoteImporter.Read(File(0, 60, 100, 0, 0xff, 0x2f, 0)));
        Assert.Throws<InvalidDataException>(() => MidiNoteImporter.Read(File(0x80, 0x80, 0x80, 0x80, 0, 0xff, 0x2f, 0)));
        valid[12] = 0x80; Assert.Throws<NotSupportedException>(() => MidiNoteImporter.Read(valid));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => MidiNoteImporter.Read(valid, cancellation.Token));
    }
}
