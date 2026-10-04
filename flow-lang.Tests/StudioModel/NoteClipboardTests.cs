using Flow.Music.Model;
using Flow.Music.Model.Editing;
using Flow.Studio.Model;
using Xunit;

namespace FlowLang.Tests.StudioModel;

public class NoteClipboardTests
{
    private static NoteEvent Note(double offset) => new(Guid.NewGuid(), "voice", offset, .5,
        new('A', 4, 0, null, 69, 432), .7, NoteArticulation.Legato, true, .1, 20, new(1, 2), new("source", 4, 2));

    [Fact]
    public void PastePreservesMusicalDetailsWithRelativeTimingAndIndependentIdentity()
    {
        var a = Note(1); var b = Note(2);
        var source = new SequenceSnapshot(Guid.NewGuid(), "source", 4, [a, b]);
        var clipboard = NoteClipboard.Copy(source, [b.Id, a.Id]);
        var first = clipboard.Paste(source, 0);
        var added = first.Notes.Where(n => n.Id != a.Id && n.Id != b.Id).ToArray();
        Assert.Equal(new[] { 0d, 1d }, added.Select(n => n.OffsetQuarters));
        Assert.Equal(a with { Id = added[0].Id, VoiceId = added[0].VoiceId, OffsetQuarters = 0 }, added[0]);
        Assert.Equal(added[0].VoiceId, added[1].VoiceId); Assert.NotEqual(a.VoiceId, added[0].VoiceId);
        var again = clipboard.Paste(first, 0);
        Assert.Equal(6, again.Notes.Select(n => n.Id).Distinct().Count());
        Assert.Throws<ArgumentException>(() => clipboard.Paste(source, 3));
        Assert.Throws<ArgumentException>(() => NoteClipboard.Copy(source, [a.Id, a.Id]));
        Assert.Throws<ArgumentException>(() => NoteClipboard.Copy(source, [Guid.NewGuid()]));
        Assert.Equal(new[] { a, b }, source.Notes);
    }

    [Fact]
    public void CutAndPasteAcrossSourcesIsAtomicAndRedoRestoresCapturedIdentities()
    {
        var tempo = new ProjectTempoMap([new(0, 120)]); var meter = new ProjectMeterMap([new(1, 4, 4)]);
        var track = Guid.NewGuid();
        var doc = new ProjectDocument(new(new(Guid.NewGuid(), tempo, meter), new(0, 1, tempo, meter),
            routing: new([new(track, "Notes")], null)));
        var a = Note(1); var source = Guid.NewGuid(); var target = Guid.NewGuid();
        ProjectNoteCommands.CreateFromNotes(doc, source, Guid.NewGuid(), track, 0, 4, [a]);
        ProjectNoteCommands.CreateBlank(doc, target, Guid.NewGuid(), track, 4, 4);
        var from = doc.Snapshot.Sources[source].Result.ScoreLayers[0].Composition.Placements[0].Section;
        var to = doc.Snapshot.Sources[target].Result.ScoreLayers[0].Composition.Placements[0].Section;
        var clipboard = NoteClipboard.Copy(from.Sequences[0], [a.Id]);
        var before = doc.Snapshot; int count = doc.History.UndoCount, calls = 0;
        SequenceEdit Cut() => new(source, from.Id, from.Sequences[0].Id, s => NoteEditing.Delete(s, [a.Id]));
        Assert.Throws<ArgumentException>(() => ProjectNoteCommands.EditSequences(doc, "Bad paste",
            [Cut(), new(target, to.Id, to.Sequences[0].Id, s => clipboard.Paste(s, 4))]));
        Assert.Same(before, doc.Snapshot); Assert.Equal(count, doc.History.UndoCount);
        ProjectNoteCommands.EditSequences(doc, "Cut and paste", [Cut(),
            new(target, to.Id, to.Sequences[0].Id, s => { calls++; return clipboard.Paste(s, 2); })]);
        var after = doc.Snapshot;
        Assert.Equal(count + 1, doc.History.UndoCount);
        Assert.Empty(after.Sources[source].Result.ScoreLayers[0].Composition.Placements[0].Section.Sequences[0].Notes);
        var pasted = Assert.Single(after.Sources[target].Result.ScoreLayers[0].Composition.Placements[0].Section.Sequences[0].Notes);
        Assert.NotEqual(a.Id, pasted.Id); Assert.Equal(2, pasted.OffsetQuarters); Assert.Equal(a.Pitch, pasted.Pitch);
        Assert.True(doc.History.Undo()); Assert.Same(before, doc.Snapshot);
        Assert.True(doc.History.Redo()); Assert.Same(after, doc.Snapshot); Assert.Equal(1, calls);
        var loaded = ProjectJson.Deserialize(ProjectJson.Serialize(after));
        Assert.Equal(pasted, Assert.Single(loaded.Sources[target].Result.ScoreLayers[0].Composition.Placements[0].Section.Sequences[0].Notes));
    }
}
