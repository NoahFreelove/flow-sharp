using Flow.Music.Model;
using Flow.Music.Model.Editing;
using Xunit;
namespace FlowLang.Tests.StudioModel;
public class NoteEditingTests
{
    private static NoteEvent Note(double offset = 0) => new(Guid.NewGuid(), "voice", offset, 1,
        new('A', 4, 0, 12.5, 69, 443.2), Origin: new("source", 2, 3));
    [Fact]
    public void MoveResizeVelocityAndPitchPreserveIdentityAndSourceGeometry()
    {
        var note = Note() with { ExactDuration = new(1, 1) };
        var sequence = new SequenceSnapshot(Guid.NewGuid(), "notes", 4, [note], [new(Guid.NewGuid(), 0, 4, 4, 4, false)]);
        var moved = NoteEditing.Move(sequence, [note.Id], 1);
        Assert.Equal(0, sequence.Notes[0].OffsetQuarters); Assert.Equal(1, moved.Notes[0].OffsetQuarters);
        Assert.Same(note.Pitch, moved.Notes[0].Pitch); Assert.Same(note.Origin, moved.Notes[0].Origin);
        var resized = NoteEditing.Resize(moved, note.Id, 2); Assert.Null(resized.Notes[0].ExactDuration);
        var velocity = NoteEditing.SetVelocity(resized, [note.Id], .9);
        Assert.Equal(.9, velocity.Notes[0].Velocity); Assert.Equal(4, velocity.DurationQuarters); Assert.Equal(sequence.Bars, velocity.Bars);
        var pitch = note.Pitch! with { FrequencyHz = 450 };
        Assert.Same(pitch, NoteEditing.SetPitch(velocity, note.Id, pitch).Notes[0].Pitch);
        Assert.Null(NoteEditing.SetPitch(velocity, note.Id, null).Notes[0].Pitch);
    }
    [Fact]
    public void AddDeleteAndFailedMultiNoteEditAreAtomic()
    {
        var a = Note(); var b = Note(3); var sequence = new SequenceSnapshot(Guid.NewGuid(), "notes", 4, [a, b]);
        Assert.Throws<ArgumentException>(() => NoteEditing.Move(sequence, [a.Id, b.Id], 1));
        Assert.Equal(0, sequence.Notes[0].OffsetQuarters); Assert.Equal(3, sequence.Notes[1].OffsetQuarters);
        Assert.Throws<ArgumentException>(() => NoteEditing.Add(sequence, [a]));
        Assert.Throws<ArgumentException>(() => NoteEditing.SetVelocity(sequence, [a.Id], double.NaN));
        Assert.Throws<ArgumentException>(() => NoteEditing.Delete(sequence, [Guid.NewGuid()]));
        var added = Note(2); var result = NoteEditing.Add(sequence, [added]);
        Assert.Equal(new[] { a.Id, added.Id, b.Id }, result.Notes.Select(n => n.Id));
        Assert.Equal(new[] { a.Id, b.Id }, NoteEditing.Delete(result, [added.Id]).Notes.Select(n => n.Id));
    }
    [Fact]
    public void SharedRepeatedSectionEditsKeepPlacementsAndDurations()
    {
        var note = Note(); var sequence = new SequenceSnapshot(Guid.NewGuid(), "notes", 4, [note]);
        var section = new SectionSnapshot(Guid.NewGuid(), "part", new(), [sequence]);
        var score = new CompositionSnapshot(Guid.NewGuid(), [new(Guid.NewGuid(), section, 2), new(Guid.NewGuid(), section)]);
        var changed = ScoreEditing.EditSequence(score, section.Id, sequence.Id, s => NoteEditing.Move(s, [note.Id], 1));
        Assert.Same(changed.Placements[0].Section, changed.Placements[1].Section);
        Assert.Equal(score.Placements.Select(p => p.Id), changed.Placements.Select(p => p.Id));
        Assert.Equal(score.DurationSeconds, changed.DurationSeconds);
        Assert.Equal(1, changed.Placements[0].Section.Sequences[0].Notes[0].OffsetQuarters);
        Assert.Equal(0, score.Placements[0].Section.Sequences[0].Notes[0].OffsetQuarters);
    }

}
