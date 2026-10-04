using Flow.Music.Model;
using Flow.Music.Model.Editing;
using Flow.Studio.Model;
using Xunit;
namespace FlowLang.Tests.StudioModel;
public class ProjectClipCommandTests
{
    [Fact]
    public void TrimAndRepeatAreCapturedAtomicEditsWithLinkedSources()
    {
        var track = Guid.NewGuid(); var doc = Create(track); var id = Guid.NewGuid();
        ProjectNoteCommands.CreateBlank(doc, Guid.NewGuid(), id, track, 4, 8);
        var original = doc.Snapshot.Arrangement.ScoreClips.Single();
        ProjectClipCommands.TrimScore(doc, id, 2, 3);
        var trimmed = doc.Snapshot.Arrangement.ScoreClips.Single();
        Assert.Equal(original.SecondsAtSourceQuarter(3, doc.Snapshot.Arrangement.Tempo), trimmed.SecondsAtSourceQuarter(3, doc.Snapshot.Arrangement.Tempo));
        var before = doc.Snapshot; int count = doc.History.UndoCount;
        ProjectClipCommands.TrimScore(doc, id, 0, 3);
        Assert.Same(before, doc.Snapshot); Assert.Equal(count, doc.History.UndoCount);
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
        ProjectClipCommands.Repeat(doc, id, ids);
        var repeated = doc.Snapshot;
        Assert.Equal(new double[] { 6, 9, 12 }, repeated.Arrangement.ScoreClips.Select(c => c.AnchorQuarters));
        Assert.All(repeated.Arrangement.ScoreClips, c => Assert.Equal(trimmed.SourceId, c.SourceId));
        Assert.Equal(count + 1, doc.History.UndoCount);
        ids[0] = Guid.NewGuid();
        Assert.True(doc.History.Undo()); Assert.Same(before, doc.Snapshot);
        Assert.True(doc.History.Redo()); Assert.Same(repeated, doc.Snapshot);
        Assert.Throws<ArgumentException>(() => ProjectClipCommands.Repeat(doc, id, [repeated.Arrangement.ScoreClips[1].Id]));
        Assert.Throws<ArgumentOutOfRangeException>(() => ProjectClipCommands.TrimScore(doc, id, -3, 1));
        Assert.Same(repeated, doc.Snapshot);
        ProjectClipCommands.TrimScore(doc, id, -2, 8);
        Assert.Equal(original, doc.Snapshot.Arrangement.ScoreClips[0]);
    }
    [Fact]
    public void AudioEdgesAndRepeatsRetainSampleTimingAcrossTempoChanges()
    {
        var tempo = new ProjectTempoMap([new(0, 120), new(4, 60), new(8, 180)]);
        var clip = new AudioClip(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 3, 8000, 16000, 8000, new(17));
        var trimmed = ClipOperations.TrimFrames(clip, 8000, 8000, tempo);
        Assert.Equal(4.5, trimmed.AnchorQuarters);
        Assert.Equal(16000, trimmed.SourceOffsetFrames);
        Assert.Equal(clip.StartSeconds(tempo) + 1, trimmed.StartSeconds(tempo), 12);
        Assert.Equal(clip, ClipOperations.TrimFrames(trimmed, -8000, 16000, tempo));
        Assert.Equal(clip.AnchorQuarters, ClipOperations.TrimFrames(clip, 0, 32000, tempo).AnchorQuarters);
        var copies = ClipOperations.Repeat(clip, Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()), tempo);
        for (int i = 0; i < copies.Count; i++)
        {
            Assert.Equal(clip.StartSeconds(tempo) + (i + 1) * 2, copies[i].StartSeconds(tempo), 12);
            Assert.Equal(clip.SourceId, copies[i].SourceId); Assert.Equal(clip.Nudge, copies[i].Nudge);
            Assert.Equal(clip.SourceOffsetFrames, copies[i].SourceOffsetFrames);
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => ClipOperations.TrimFrames(clip, -8001, 1, tempo));
        Assert.Throws<OverflowException>(() => ClipOperations.TrimFrames(clip, long.MaxValue, 1, tempo));
        Assert.Throws<ArgumentException>(() => ClipOperations.Repeat(clip, [clip.Id], tempo));
    }
    private static ProjectDocument Create(Guid track)
    {
        var tempo = new ProjectTempoMap([new(0, 120)]); var meter = new ProjectMeterMap([new(1, 4, 4)]);
        return new(new(new(Guid.NewGuid(), tempo, meter), new(0, 1, tempo, meter), routing: new([new(track, "Track")], null)));
    }
    [Fact]
    public void BlankClipCanBeEditedDuplicatedSplitDeletedAndReopened()
    {
        var track = Guid.NewGuid(); var doc = Create(track); var source = Guid.NewGuid(); var clip = Guid.NewGuid();
        ProjectNoteCommands.CreateBlank(doc, source, clip, track, 0, 4);
        Assert.Null(doc.Snapshot.Sources[source].EditableOrigin!.SourceId);
        var section = doc.Snapshot.Sources[source].Result.ScoreLayers.Single().Composition.Placements.Single().Section;
        var sequence = section.Sequences.Single();
        ProjectNoteCommands.EditSequence(doc, source, section.Id, sequence.Id, "Draw note", s => NoteEditing.Add(s,
            [new(Guid.NewGuid(), "voice", 0, 1, new('A', 4, 0, null, 69, 440))]));
        var copy = Guid.NewGuid(); ProjectClipCommands.Duplicate(doc, new Dictionary<Guid, Guid> { [clip] = copy }, 4);
        Assert.All(doc.Snapshot.Arrangement.ScoreClips, c => Assert.Equal(source, c.SourceId));
        int before = doc.History.UndoCount; ProjectClipCommands.Move(doc, [clip, copy], 2);
        Assert.Equal(before + 1, doc.History.UndoCount); Assert.Equal(new double[] { 2, 6 }, doc.Snapshot.Arrangement.ScoreClips.Select(c => c.AnchorQuarters));
        Assert.True(doc.History.Undo()); Assert.Equal(new double[] { 0, 4 }, doc.Snapshot.Arrangement.ScoreClips.Select(c => c.AnchorQuarters));
        Assert.True(doc.History.Redo());
        var right = Guid.NewGuid(); ProjectClipCommands.SplitScore(doc, copy, new(1), right);
        Assert.Equal(1, doc.Snapshot.Arrangement.ScoreClips.Single(c => c.Id == right).SourceOffsetQuarters);
        ProjectClipCommands.Delete(doc, [copy, right]); Assert.Single(doc.Snapshot.Arrangement.ScoreClips);
        Assert.True(doc.History.Undo()); Assert.Equal(3, doc.Snapshot.Arrangement.ScoreClips.Count);
        var restored = ProjectJson.Deserialize(ProjectJson.Serialize(doc.Snapshot));
        Assert.True(restored.Sources[source].IsEditable); Assert.Null(restored.Sources[source].EditableOrigin!.SourceId);
        Assert.Single(restored.Sources[source].Result.ScoreLayers[0].Composition.Placements[0].Section.Sequences[0].Notes);
    }
    [Fact]
    public void InvalidMultiClipMoveAndDuplicateAreAtomic()
    {
        var track = Guid.NewGuid(); var doc = Create(track); var clip = Guid.NewGuid();
        ProjectNoteCommands.CreateBlank(doc, Guid.NewGuid(), clip, track, 0, 4);
        var before = doc.Snapshot; int count = doc.History.UndoCount;
        Assert.Throws<ArgumentOutOfRangeException>(() => ProjectClipCommands.Move(doc, [clip], -1));
        Assert.Throws<ArgumentException>(() => ProjectClipCommands.Duplicate(doc, new Dictionary<Guid, Guid> { [clip] = clip }, 4));
        Assert.Throws<ArgumentException>(() => ProjectClipCommands.Delete(doc, [Guid.NewGuid()]));
        Assert.Same(before, doc.Snapshot); Assert.Equal(count, doc.History.UndoCount);
    }
    [Fact]
    public void AudioSplitAndMixedSelectionPreserveFramesAndNudge()
    {
        var track = Guid.NewGuid(); var doc = Create(track); var score = Guid.NewGuid();
        ProjectNoteCommands.CreateBlank(doc, Guid.NewGuid(), score, track, 0, 4);
        var audio = new AudioClip(Guid.NewGuid(), track, Guid.NewGuid(), 0, 7, 16000, 8000, new(12));
        doc.Edit("Place audio", p => new(new(p.Arrangement.Id, p.Arrangement.Tempo, p.Arrangement.Meter, p.Arrangement.ScoreClips, [audio]),
            p.Context, p.Sources.Values, p.Routing, p.Assets, p.Automation));
        ProjectClipCommands.Move(doc, [score, audio.Id], 4);
        var right = Guid.NewGuid(); ProjectClipCommands.SplitAudio(doc, audio.Id, 8000, right);
        var split = doc.Snapshot.Arrangement.AudioClips.Single(c => c.Id == right);
        Assert.Equal(8007, split.SourceOffsetFrames); Assert.Equal(8000, split.LengthFrames); Assert.Equal(audio.Nudge, split.Nudge);
        Assert.Equal(6, split.AnchorQuarters);
        Assert.True(doc.History.Undo()); Assert.Single(doc.Snapshot.Arrangement.AudioClips);
    }
}
