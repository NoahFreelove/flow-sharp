namespace Flow.Music.Model.Editing;

/// <summary>Atomic immutable note edits for manually owned sequences. Sequence/bar
/// duration stays fixed: edits outside the source window require a separate resize.
/// Existing notes retain identity, tuning and provenance unless explicitly replaced.</summary>
public static class NoteEditing
{
    public static SequenceSnapshot Add(SequenceSnapshot sequence, IEnumerable<NoteEvent> notes)
    {
        var added = notes.Take(100001).ToArray();
        if (sequence.Notes.Count + (long)added.Length > 100000) throw new ArgumentException("Editable note budget exceeded");
        var ids = sequence.Notes.Select(n => n.Id).ToHashSet();
        if (ids.Count != sequence.Notes.Count) throw new ArgumentException("Ambiguous existing note identity");
        foreach (var note in added)
        {
            Validate(note, sequence.DurationQuarters);
            if (!ids.Add(note.Id)) throw new ArgumentException("Duplicate note identity");
        }
        return Build(sequence, sequence.Notes.Concat(added));
    }
    public static SequenceSnapshot Delete(SequenceSnapshot sequence, IEnumerable<Guid> ids)
    {
        var selected = Select(sequence, ids);
        return Build(sequence, sequence.Notes.Where(n => !selected.Contains(n.Id)));
    }
    public static SequenceSnapshot Move(SequenceSnapshot sequence, IEnumerable<Guid> ids, double deltaQuarters)
    {
        if (!double.IsFinite(deltaQuarters)) throw new ArgumentOutOfRangeException(nameof(deltaQuarters));
        return Change(sequence, ids, n => n with { OffsetQuarters = n.OffsetQuarters + deltaQuarters });
    }
    public static SequenceSnapshot Resize(SequenceSnapshot sequence, Guid id, double durationQuarters) =>
        Change(sequence, [id], n => n with { DurationQuarters = durationQuarters, ExactDuration = null });
    public static SequenceSnapshot SetVelocity(SequenceSnapshot sequence, IEnumerable<Guid> ids, double velocity) =>
        Change(sequence, ids, n => n with { Velocity = velocity });
    public static SequenceSnapshot SetPitch(SequenceSnapshot sequence, Guid id, NotePitch? pitch) =>
        Change(sequence, [id], n => n with { Pitch = pitch });

    private static HashSet<Guid> Select(SequenceSnapshot sequence, IEnumerable<Guid> ids)
    {
        var requested = ids.Take(100001).ToArray();
        var selected = requested.ToHashSet();
        if (requested.Length > 100000 || selected.Contains(Guid.Empty)) throw new ArgumentException("Invalid note selection");
        var existing = sequence.Notes.Select(n => n.Id).ToHashSet();
        if (existing.Count != sequence.Notes.Count || !selected.IsSubsetOf(existing)) throw new ArgumentException("Missing or ambiguous note identity");
        return selected;
    }
    private static SequenceSnapshot Change(SequenceSnapshot sequence, IEnumerable<Guid> ids, Func<NoteEvent, NoteEvent> edit)
    {
        var selected = Select(sequence, ids);
        var notes = sequence.Notes.Select(n => selected.Contains(n.Id) ? edit(n) : n).ToArray();
        foreach (var note in notes.Where(n => selected.Contains(n.Id))) Validate(note, sequence.DurationQuarters);
        return Build(sequence, notes);
    }
    private static SequenceSnapshot Build(SequenceSnapshot sequence, IEnumerable<NoteEvent> notes) =>
        new(sequence.Id, sequence.Name, sequence.DurationQuarters, notes.OrderBy(n => n.OffsetQuarters), sequence.Bars);
    private static void Validate(NoteEvent note, double duration)
    {
        if (note is null || note.Id == Guid.Empty || string.IsNullOrWhiteSpace(note.VoiceId) ||
            !double.IsFinite(note.OffsetQuarters) || note.OffsetQuarters < 0 || !double.IsFinite(note.DurationQuarters) || note.DurationQuarters <= 0 ||
            note.OffsetQuarters + note.DurationQuarters > duration || !double.IsFinite(note.Velocity) || note.Velocity is < 0 or > 1 ||
            !Enum.IsDefined(note.Articulation) || !double.IsFinite(note.DurationOverlap) || note.DurationOverlap < 0 ||
            !double.IsFinite(note.PortamentoMs) || note.PortamentoMs < 0)
            throw new ArgumentException("Invalid editable note or note outside sequence duration");
        if (note.Pitch is { } pitch && (!double.IsFinite(pitch.FrequencyHz) || pitch.FrequencyHz <= 0 ||
            "ABCDEFG".IndexOf(pitch.Letter) < 0 || pitch.CentOffset is { } cents && !double.IsFinite(cents)))
            throw new ArgumentException("Invalid resolved note pitch");
    }
}
