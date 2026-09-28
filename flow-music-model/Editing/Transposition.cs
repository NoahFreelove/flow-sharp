namespace Flow.Music.Model.Editing;

/// <summary>A transposed spelling; <see cref="Clamped"/> reports range correction.</summary>
public readonly record struct TransposedPitch(char Letter, int Octave, int Alteration, int MidiKey,
    int RequestedMidiKey, bool Clamped);

/// <summary>
/// The one transpose implementation shared by Flow's <c>transpose</c> builtin and
/// editing hosts. Pitches move by MIDI key, respell with sharps and clamp to Flow's
/// range (E0–E10); spelling is not preserved across enharmonics.
/// </summary>
public static class Transposition
{
    public const int LowestKey = 16;   // E0
    public const int HighestKey = 136; // E10

    private static readonly (char Letter, int Alteration)[] SharpSpelling =
        [('C', 0), ('C', 1), ('D', 0), ('D', 1), ('E', 0), ('F', 0), ('F', 1), ('G', 0), ('G', 1), ('A', 0), ('A', 1), ('B', 0)];

    public static TransposedPitch Transpose(char letter, int octave, int alteration, int semitones)
    {
        int natural = char.ToUpperInvariant(letter) switch
        {
            'C' => 0, 'D' => 2, 'E' => 4, 'F' => 5, 'G' => 7, 'A' => 9, 'B' => 11,
            _ => throw new ArgumentException($"Invalid note name: {letter}", nameof(letter)),
        };
        long requested = (octave + 1L) * 12 + natural + alteration + semitones;
        int midi = (int)Math.Clamp(requested, LowestKey, HighestKey);
        var (name, accidental) = SharpSpelling[midi % 12];
        int reported = (int)Math.Clamp(requested, int.MinValue, int.MaxValue);
        return new(name, midi / 12 - 1, accidental, midi, reported, midi != requested);
    }

    /// <summary>Whole semitones and the same-sign cent remainder (truncates toward zero).</summary>
    public static (int Semitones, double Cents) SplitCents(double cents)
    {
        if (!double.IsFinite(cents)) throw new ArgumentOutOfRangeException(nameof(cents));
        double whole = Math.Truncate(cents / 100.0);
        if (whole is > int.MaxValue or < int.MinValue) throw new ArgumentOutOfRangeException(nameof(cents));
        return ((int)whole, cents - whole * 100.0);
    }

    /// <summary>
    /// Returns a copy with every pitched event transposed. A nonzero <paramref name="cents"/>
    /// adds to each note's cent offset. Frequency is rescaled by the equal-tempered interval
    /// actually applied (exact for 12-TET); pass <paramref name="frequency"/> to resolve
    /// another tuning from the new pitch. Rests, identity and timing are unchanged.
    /// </summary>
    public static SequenceSnapshot Apply(SequenceSnapshot sequence, int semitones, double cents = 0,
        Func<NotePitch, double>? frequency = null, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        if (!double.IsFinite(cents)) throw new ArgumentOutOfRangeException(nameof(cents));
        var notes = new NoteEvent[sequence.Notes.Count];
        for (int i = 0; i < notes.Length; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            var note = sequence.Notes[i];
            if (note.Pitch is not { } pitch) { notes[i] = note; continue; }
            var moved = Transpose(pitch.Letter, pitch.Octave, pitch.Alteration, semitones);
            double interval = moved.MidiKey - pitch.MidiKey + cents / 100.0;
            var result = new NotePitch(moved.Letter, moved.Octave, moved.Alteration,
                cents == 0 ? pitch.CentOffset : (pitch.CentOffset ?? 0) + cents, moved.MidiKey,
                pitch.FrequencyHz * Math.Pow(2, interval / 12.0));
            if (frequency is not null) result = result with { FrequencyHz = frequency(result) };
            notes[i] = note with { Pitch = result };
        }
        return new(sequence.Id, sequence.Name, sequence.DurationQuarters, notes, sequence.Bars);
    }
}
