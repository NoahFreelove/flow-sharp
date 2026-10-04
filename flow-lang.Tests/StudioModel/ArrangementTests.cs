using Flow.Studio.Model;
using Xunit;

namespace FlowLang.Tests.StudioModel;

public class ArrangementTests
{
    private static ScoreClip Clip(double anchor = 8, double offset = 2, double length = 8, double nudge = 25) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "lead", anchor, offset, length, new(nudge));
    private static ProjectTempoMap Tempo() => new([new(0, 120), new(10, 60), new(14, 180)]);
    private static ProjectMeterMap Meter() => new([new(1, 4, 4), new(3, 3, 4), new(5, 7, 8)]);
    private static ArrangementSnapshot Snapshot(ScoreClip clip) => new(Guid.NewGuid(), Tempo(), Meter(), [clip]);

    [Fact]
    public void TempoIsInvertibleAcrossChangesAndBeforeZero()
    {
        var tempo = Tempo();
        Assert.Equal(5, tempo.SecondsAt(10));
        Assert.Equal(9, tempo.SecondsAt(14));
        for (double q = -10; q < 40; q += 0.125)
            Assert.Equal(q, tempo.QuarterAt(tempo.SecondsAt(q)), 10);
    }

    [Fact]
    public void MeterAlignmentResolvesOneBasedBarsAndClearsNudge()
    {
        var clip = Clip();
        Assert.Equal(0, Meter().QuarterAtBar(1));
        Assert.Equal(8, Meter().QuarterAtBar(3));
        Assert.Equal(14, Meter().QuarterAtBar(5));
        Assert.Equal(17.5, Meter().QuarterAtBar(6));
        var aligned = ClipOperations.AlignToBar(clip, 6, Meter());
        Assert.Equal(17.5, aligned.AnchorQuarters);
        Assert.Equal(0, aligned.Nudge.Milliseconds);
        Assert.Equal(clip.SourceOffsetQuarters, aligned.SourceOffsetQuarters);
        Assert.Equal(clip.LengthQuarters, aligned.LengthQuarters);
        Assert.Equal(25, clip.Nudge.Milliseconds);
    }

    [Fact]
    public void ScoreSplitPreservesTimingAcrossTempoChangesAndNudges()
    {
        var clip = Clip(nudge: -250);
        var rightId = Guid.NewGuid();
        var (left, right) = ClipOperations.Split(clip, new(4), rightId);
        Assert.Equal(clip.Id, left.Id);
        Assert.Equal(rightId, right.Id);
        Assert.Equal(clip.SourceId, right.SourceId);
        Assert.Equal(clip.LayerId, right.LayerId);
        Assert.Equal(12, right.AnchorQuarters);
        Assert.Equal(6, right.SourceOffsetQuarters);
        Assert.Equal(clip.SecondsAtSourceQuarter(6, Tempo()), right.SecondsAtSourceQuarter(6, Tempo()), 12);
        Assert.Equal(left.SecondsAtSourceQuarter(6, Tempo()), right.SecondsAtSourceQuarter(6, Tempo()), 12);
        var ruler = ClipOperations.SplitAtProjectSeconds(clip, right.SecondsAtSourceQuarter(6, Tempo()), Tempo(), rightId);
        Assert.Equal(left, ruler.Left);
        Assert.Equal(right, ruler.Right);
    }

    [Fact]
    public void AudioSplitUsesFramesAndNonuniformTempoWithoutStretching()
    {
        var clip = new AudioClip(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 8, 24000, 240000, 48000, new(-25));
        var (left, right) = ClipOperations.SplitFrames(clip, 96000, Tempo(), Guid.NewGuid());
        Assert.Equal(11, right.AnchorQuarters); // q8=4s, +2s => q11, not q12
        Assert.Equal(120000, right.SourceOffsetFrames);
        Assert.Equal(144000, right.LengthFrames);
        Assert.Equal(left.StartSeconds(Tempo()) + 2, right.StartSeconds(Tempo()), 12);
        var ruler = ClipOperations.SplitAtProjectSeconds(clip, right.StartSeconds(Tempo()), Tempo(), right.Id);
        Assert.Equal(left, ruler.Left);
        Assert.Equal(right, ruler.Right);
        var moved = ClipOperations.MoveByQuarters(clip, 4);
        Assert.Equal(clip.LengthFrames, moved.LengthFrames);
        Assert.Equal(clip.SourceOffsetFrames, moved.SourceOffsetFrames);
    }

    [Fact]
    public void SplitAndNudgeRoundTripWithExactUndoAndRedo()
    {
        var original = Snapshot(Clip());
        var document = new ArrangementDocument(original);
        var rightId = Guid.NewGuid();
        int evaluations = 0;
        document.Edit("Split clip", s => { evaluations++; return s.SplitScore(s.ScoreClips[0].Id, new(4), rightId); });
        var split = document.Snapshot;
        Assert.Equal(2, split.ScoreClips.Count);
        Assert.True(document.History.IsDirty);
        Assert.True(document.History.Undo());
        Assert.Same(original, document.Snapshot);
        Assert.False(document.History.IsDirty);
        Assert.True(document.History.Redo());
        Assert.Same(split, document.Snapshot);
        Assert.Equal(1, evaluations);
        document.History.MarkSaved();
        Assert.False(document.History.IsDirty);
        document.History.Undo();
        Assert.True(document.History.IsDirty);
        document.History.Redo();
        Assert.False(document.History.IsDirty);
    }

    [Fact]
    public void ArrangementSerializationPreservesAllTimingAndIdentity()
    {
        var score = Clip();
        var audio = new AudioClip(Guid.NewGuid(), score.TrackId, Guid.NewGuid(), 2, 480, 48000, 48000, new(-12.5));
        var original = new ArrangementSnapshot(Guid.NewGuid(), Tempo(), Meter(), [score], [audio]);
        var loaded = ArrangementJson.Deserialize(ArrangementJson.Serialize(original));
        Assert.Equal(original.Id, loaded.Id);
        Assert.Equal(original.ScoreClips, loaded.ScoreClips);
        Assert.Equal(original.AudioClips, loaded.AudioClips);
        Assert.Equal(original.Tempo.Changes, loaded.Tempo.Changes);
        Assert.Equal(original.Meter.Changes, loaded.Meter.Changes);
        Assert.Equal(ArrangementJson.Serialize(original), ArrangementJson.Serialize(loaded));
    }

    [Fact]
    public void FailedEditLeavesDocumentAndHistoryUntouched()
    {
        var original = Snapshot(Clip());
        var document = new ArrangementDocument(original);
        Assert.Throws<ArgumentException>(() => document.Edit("Duplicate ID", s =>
            s.SplitScore(s.ScoreClips[0].Id, new(2), s.ScoreClips[0].Id)));
        Assert.Same(original, document.Snapshot);
        Assert.Equal(0, document.History.UndoCount);
        Assert.False(document.History.IsDirty);
    }

    [Fact]
    public void InvalidTimingsAndFutureSchemasAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TimeOffset(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => ClipOperations.Split(Clip(), default, Guid.NewGuid()));
        Assert.Throws<ArgumentOutOfRangeException>(() => ClipOperations.Split(Clip(), new(8), Guid.NewGuid()));
        Assert.Throws<ArgumentOutOfRangeException>(() => ClipOperations.MoveByQuarters(Clip(), -9));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProjectTempoMap([new(0, 0)]));
        Assert.Throws<ArgumentException>(() => new ProjectTempoMap([new(0, 120), new(0, 60)]));
        Assert.Throws<ArgumentException>(() => new ProjectMeterMap([new(1, 4, 3)]));
        string json = ArrangementJson.Serialize(Snapshot(Clip()));
        Assert.Throws<System.Text.Json.JsonException>(() => ArrangementJson.Deserialize(json.Replace("\"Version\": 2", "\"Version\": 3")));
        Assert.Throws<System.Text.Json.JsonException>(() => ArrangementJson.Deserialize(json.Replace("\"Version\": 2", "\"FutureData\": 0, \"Version\": 2")));
    }
    [Fact]
    public void SaveReopenAndFailedSavePreserveDocumentAndDirtyState()
    {
        string directory = Path.Combine(Path.GetTempPath(), "flow-arrangement-test-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            var document = new ArrangementDocument(Snapshot(Clip()));
            document.Edit("split", s => s.SplitScore(s.ScoreClips[0].Id, new(3), Guid.NewGuid()));
            string path = Path.Combine(directory, "arrangement.json");
            ArrangementFile.Save(path, document);
            Assert.False(document.History.IsDirty);
            var loaded = ArrangementFile.Load(path);
            Assert.False(loaded.History.IsDirty);
            Assert.Equal(ArrangementJson.Serialize(document.Snapshot), ArrangementJson.Serialize(loaded.Snapshot));
            string saved = File.ReadAllText(path);
            document.History.Undo();
            Assert.True(document.History.IsDirty);
            Assert.ThrowsAny<IOException>(() => ArrangementFile.Save(directory, document));
            Assert.True(document.History.IsDirty);
            Assert.Equal(saved, File.ReadAllText(path));
            Assert.Single(Directory.GetFiles(directory));
            ArrangementFile.Save(path, document);
            Assert.Single(ArrangementFile.Load(path).Snapshot.ScoreClips);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
