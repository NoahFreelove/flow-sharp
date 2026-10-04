namespace Flow.Music.Model.Editing;

public static class ScoreEditing
{
    /// <summary>Edit one source sequence, affecting all repeated/shared occurrences.
    /// Source duration is fixed so following placements cannot silently shift.</summary>
    public static CompositionSnapshot EditSequence(CompositionSnapshot composition, Guid sectionId, Guid sequenceId,
        Func<SequenceSnapshot, SequenceSnapshot> edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        var matches = composition.Placements.Select(p => p.Section).Where(s => s.Id == sectionId)
            .Distinct<SectionSnapshot>(ReferenceEqualityComparer.Instance).ToArray();
        if (matches.Length != 1) throw new ArgumentException("Missing or ambiguous section identity");
        var section = matches[0];
        var sequences = section.Sequences.Where(s => s.Id == sequenceId).ToArray();
        if (sequences.Length != 1) throw new ArgumentException("Missing or ambiguous sequence identity");
        var before = sequences[0]; var after = edit(before) ?? throw new ArgumentException("Missing edited sequence");
        if (after.Id != before.Id || after.DurationQuarters != before.DurationQuarters)
            throw new ArgumentException("Note edit cannot change sequence identity or duration");
        var replacement = new SectionSnapshot(section.Id, section.Name, section.Settings,
            section.Sequences.Select(s => ReferenceEquals(s, before) ? after : s), section.Origin);
        return new(composition.Id, composition.Placements.Select(p => ReferenceEquals(p.Section, section) ? p with { Section = replacement } : p));
    }
}
