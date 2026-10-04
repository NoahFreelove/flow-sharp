using Flow.Studio.Engine;
using Flow.Studio.Model;
using Xunit;
namespace FlowLang.Tests.StudioModel;
public class MidiRecordingTakeTests
{
    private static ProjectDocument Document()
    {
        var tempo = new ProjectTempoMap([new(0, 120), new(2, 60)]); var meter = new ProjectMeterMap([new(1, 4, 4)]);
        return new(new(new(Guid.NewGuid(), tempo, meter), new(0, 1, tempo, meter), routing: new([new(Guid.NewGuid(), "Recorded")], null)));
    }
    [Fact]
    public void TakeCrossingTempoChangeCommitsOneEditableActionAndRedoRetainsIdentity()
    {
        var doc = Document(); var before = doc.Snapshot;
        var take = new MidiRecordingTake(before, [new(2, 69, 127, 500, 2500)], 0, 3000, 0, 1000);
        var note = take.Notes.Single(); Assert.Equal(1, note.OffsetQuarters); Assert.Equal(2.5, note.DurationQuarters);
        Assert.Equal(440, note.Pitch!.FrequencyHz); Assert.Equal("midi-channel-3", note.VoiceId);
        Assert.True(take.Commit(doc, doc.Snapshot.Routing.Tracks.Single().Id));
        Assert.Equal(1, doc.History.UndoCount); Assert.True(doc.Snapshot.Sources[take.SourceId].IsEditable);
        var saved = ProjectJson.Serialize(doc.Snapshot); Assert.Equal(saved, ProjectJson.Serialize(ProjectJson.Deserialize(saved)));
        Assert.True(doc.History.Undo()); Assert.Same(before, doc.Snapshot);
        Assert.True(doc.History.Redo()); Assert.Equal(saved, ProjectJson.Serialize(doc.Snapshot));
        Assert.Throws<InvalidOperationException>(() => take.Commit(doc, doc.Snapshot.Routing.Tracks.Single().Id));
    }
    [Fact]
    public void LatencyTrimsBeforeZeroAndStaleProjectCannotSilentlyAccept()
    {
        var doc = Document(); var track = doc.Snapshot.Routing.Tracks.Single().Id;
        var take = new MidiRecordingTake(doc.Snapshot, [new(0, 60, 64, 0, 200), new(0, 61, 90, 10, 20)], 0, 1000, 0, 1000, 100);
        Assert.Single(take.Notes); Assert.Equal(0, take.Notes[0].OffsetQuarters); Assert.Equal(.2, take.Notes[0].DurationQuarters);
        ProjectNoteCommands.CreateBlank(doc, Guid.NewGuid(), Guid.NewGuid(), track, 0, 1);
        var changed = doc.Snapshot;
        Assert.Throws<InvalidOperationException>(() => take.Commit(doc, track)); Assert.Same(changed, doc.Snapshot);
    }
}
