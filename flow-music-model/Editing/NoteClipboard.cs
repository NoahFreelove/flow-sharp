namespace Flow.Music.Model.Editing;

/// <summary>Detached musical selection with offsets relative to its first note.
/// Paste preserves resolved pitches/articulation/provenance, assigning fresh note
/// and voice identities so the copy cannot tie into unrelated target notes.</summary>
public sealed class NoteClipboard
{
    public IReadOnlyList<NoteEvent> Notes { get; }
    private NoteClipboard(NoteEvent[] notes) => Notes = Array.AsReadOnly(notes);

    public static NoteClipboard Copy(SequenceSnapshot source, IEnumerable<Guid> ids)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(ids);
        var requested = ids.Take(100001).ToArray();
        var selected = requested.ToHashSet();
        if (requested.Length is < 1 or > 100000 || selected.Count != requested.Length || selected.Contains(Guid.Empty))
            throw new ArgumentException("Invalid clipboard selection");
        if (source.Notes.Select(n => n.Id).Distinct().Count() != source.Notes.Count)
            throw new ArgumentException("Ambiguous source note identity");
        var notes = source.Notes.Where(n => selected.Contains(n.Id)).OrderBy(n => n.OffsetQuarters).ToArray();
        if (notes.Length != selected.Count) throw new ArgumentException("Unknown clipboard note");
        double origin = notes[0].OffsetQuarters;
        return new(notes.Select(n => n with { OffsetQuarters = n.OffsetQuarters - origin }).ToArray());
    }

    public SequenceSnapshot Paste(SequenceSnapshot target, double atQuarters)
    {
        if (!double.IsFinite(atQuarters) || atQuarters < 0) throw new ArgumentOutOfRangeException(nameof(atQuarters));
        var voices = Notes.Select(n => n.VoiceId).Distinct().ToDictionary(v => v, _ => "paste-" + Guid.NewGuid());
        return NoteEditing.Add(target, Notes.Select(n => n with
        {
            Id = Guid.NewGuid(), VoiceId = voices[n.VoiceId], OffsetQuarters = atQuarters + n.OffsetQuarters
        }));
    }
}
