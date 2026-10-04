using Flow.Music.Model;
using Flow.Music.Model.Editing;
using Flow.Studio.Model;
using Xunit;
namespace FlowLang.Tests.StudioModel;
public class EditableSourceTests
{
    [Fact]
    public void ConversionEditsAndReopenKeepOwnershipProvenanceAndClipWindows()
    {
        var tempo = new ProjectTempoMap([new(0, 120)]); var meter = new ProjectMeterMap([new(1, 4, 4)]);
        var doc = new ProjectDocument(new(new(Guid.NewGuid(), tempo, meter), new(0, 1, tempo, meter)));
        var note = new NoteEvent(Guid.NewGuid(), "voice", 0, 1, new('A', 4, 0, null, 69, 443));
        var sequence = new SequenceSnapshot(Guid.NewGuid(), "notes", 4, [note]);
        var section = new SectionSnapshot(Guid.NewGuid(), "section", new(), [sequence]);
        var score = new CompositionSnapshot(Guid.NewGuid(), [new(Guid.NewGuid(), section)]);
        var ticket = doc.BeginBuild(new(1, Guid.NewGuid(), "generate"), "original code");
        Assert.True(doc.Accept(ticket, new(ticket.Descriptor.SourceId, ticket.Revision, ticket.Context, [new("main", score)])));
        var clip = new ScoreClip(Guid.NewGuid(), Guid.NewGuid(), ticket.Descriptor.SourceId, "main", 8, .5, 2, new(12));
        var linked = new ScoreClip(Guid.NewGuid(), clip.TrackId, clip.SourceId, "main", 16, 0, 4);
        doc.Edit("Place", p => new(new(p.Arrangement.Id, tempo, meter, [clip, linked]), p.Context, p.Sources.Values));
        Assert.Throws<InvalidOperationException>(() => ProjectNoteCommands.EditSequence(doc, clip.SourceId, section.Id, sequence.Id, "Move",
            s => NoteEditing.Move(s, [note.Id], 1)));
        var newSource = Guid.NewGuid(); ProjectNoteCommands.MakeEditable(doc, clip.Id, newSource);
        var detached = doc.Snapshot.Arrangement.ScoreClips[0];
        Assert.Equal(clip.AnchorQuarters, detached.AnchorQuarters); Assert.Equal(clip.SourceOffsetQuarters, detached.SourceOffsetQuarters);
        Assert.Equal(clip.LengthQuarters, detached.LengthQuarters); Assert.Equal(clip.Nudge, detached.Nudge);
        Assert.Equal(clip.SourceId, doc.Snapshot.Arrangement.ScoreClips[1].SourceId);
        ProjectNoteCommands.EditSequence(doc, newSource, section.Id, sequence.Id, "Move note", s => NoteEditing.Move(s, [note.Id], 1));
        var edited = doc.Snapshot.Sources[newSource]; Assert.True(edited.IsEditable);
        Assert.Equal(clip.SourceId, edited.EditableOrigin!.SourceId);
        Assert.Equal(1, edited.Result.ScoreLayers[0].Composition.Placements[0].Section.Sequences[0].Notes[0].OffsetQuarters);
        Assert.Equal(0, doc.Snapshot.Sources[clip.SourceId].Result.ScoreLayers[0].Composition.Placements[0].Section.Sequences[0].Notes[0].OffsetQuarters);
        var restored = ProjectJson.Deserialize(ProjectJson.Serialize(doc.Snapshot));
        Assert.Equal(edited.EditableOrigin, restored.Sources[newSource].EditableOrigin);
        Assert.True(restored.Sources[newSource].IsEditable);
        Assert.Throws<InvalidOperationException>(() => new ProjectDocument(restored).BeginBuild(edited.Descriptor, "overwrite"));
        Assert.True(doc.History.Undo()); Assert.True(doc.History.Undo());
        Assert.False(doc.Snapshot.Sources.ContainsKey(newSource)); Assert.Same(clip, doc.Snapshot.Arrangement.ScoreClips[0]);
        Assert.True(doc.History.Redo()); Assert.True(doc.History.Redo()); Assert.Same(edited, doc.Snapshot.Sources[newSource]);
    }
}
