namespace FlowLang.TypeSystem.SpecialTypes;

/// <summary>
/// Represents a musical bar (measure) containing a collection of notes.
/// A Bar is a container for organizing notes into musical phrases.
/// </summary>
public sealed class BarType : FlowType
{
    private BarType() { }

    public static BarType Instance { get; } = new();

    public override string Name => "Bar";

    public override int GetSpecificity() => 135;
}

/// <summary>
/// Bar mode: Simple (legacy string notes) or Musical (structured notes with durations).
/// </summary>
public enum BarMode
{
    Simple,
    Musical
}

/// <summary>
/// Runtime representation of a Bar containing notes.
/// </summary>
public class BarData
{
    /// <summary>
    /// The notes contained in this bar (legacy mode).
    /// Each note is stored as a string (e.g., "A4", "C3", "E5+").
    /// </summary>
    public List<string> Notes { get; }

    /// <summary>
    /// The musical notes contained in this bar (musical mode).
    /// </summary>
    public List<MusicalNoteData> MusicalNotes { get; }

    /// <summary>
    /// Optional: Time signature numerator (e.g., 4 in 4/4 time).
    /// </summary>
    public int? TimeSignatureNumerator { get; set; }

    /// <summary>
    /// Optional: Time signature denominator (e.g., 4 in 4/4 time).
    /// </summary>
    public int? TimeSignatureDenominator { get; set; }

    /// <summary>
    /// Time signature for musical mode.
    /// </summary>
    public TimeSignatureData? TimeSignature { get; set; }

    /// <summary>
    /// The mode of this bar (Simple or Musical).
    /// </summary>
    public BarMode Mode { get; set; }

    /// <summary>
    /// Whether this bar is a pickup (anacrusis) bar.
    /// Pickup bars use their actual note duration instead of the full time signature.
    /// </summary>
    public bool IsPickup { get; set; }

    public BarData()
    {
        Notes = new List<string>();
        MusicalNotes = new List<MusicalNoteData>();
        TimeSignatureNumerator = null;
        TimeSignatureDenominator = null;
        TimeSignature = null;
        Mode = BarMode.Simple;
    }

    public BarData(IEnumerable<string> notes)
    {
        Notes = new List<string>(notes);
        MusicalNotes = new List<MusicalNoteData>();
        TimeSignatureNumerator = null;
        TimeSignatureDenominator = null;
        TimeSignature = null;
        Mode = BarMode.Simple;
    }

    public BarData(IEnumerable<string> notes, int numerator, int denominator)
    {
        Notes = new List<string>(notes);
        MusicalNotes = new List<MusicalNoteData>();
        TimeSignatureNumerator = numerator;
        TimeSignatureDenominator = denominator;
        TimeSignature = null;
        Mode = BarMode.Simple;
    }

    public BarData(IEnumerable<MusicalNoteData> musicalNotes, TimeSignatureData timeSignature)
    {
        Notes = new List<string>();
        MusicalNotes = new List<MusicalNoteData>(musicalNotes);
        TimeSignature = timeSignature;
        TimeSignatureNumerator = timeSignature?.Numerator;
        TimeSignatureDenominator = timeSignature?.Denominator;
        Mode = BarMode.Musical;
    }

    /// <summary>
    /// Returns the number of notes in this bar.
    /// </summary>
    public int Count => Notes.Count;

    /// <summary>
    /// Adds a note to the bar.
    /// </summary>
    public void AddNote(string note)
    {
        Notes.Add(note);
    }

    /// <summary>
    /// Gets a note at the specified index.
    /// </summary>
    public string GetNote(int index)
    {
        if (index < 0 || index >= Notes.Count)
            throw new IndexOutOfRangeException($"Note index {index} out of range [0, {Notes.Count})");
        return Notes[index];
    }

    /// <summary>
    /// Returns the actual total beats of note content in this bar.
    /// For pickup bars, this is the sum of note durations rather than the time signature numerator.
    /// </summary>
    public double GetActualBeats()
    {
        if (Mode != BarMode.Musical || TimeSignature == null)
            return TimeSignature?.Numerator ?? 4;

        // Chord-tones share the leading tone's slot — they must not
        // contribute again to the bar's actual beat count. See
        // MusicalNoteData.IsChordTone.
        return MusicalNotes
            .Where(n => !n.IsChordTone)
            .Sum(n => n.GetBeats(TimeSignature.Denominator));
    }

    /// <summary>
    /// Validates that the total duration of notes fits within the time signature.
    /// </summary>
    public bool ValidateDuration()
    {
        if (Mode != BarMode.Musical || TimeSignature == null)
            return true;

        // Chord-tones share the leading tone's slot — see GetActualBeats above.
        double totalBeats = MusicalNotes
            .Where(n => !n.IsChordTone)
            .Sum(n => n.GetBeats(TimeSignature.Denominator));
        return totalBeats <= TimeSignature.Numerator;
    }

    /// <summary>
    /// Converts the bar to a timeline with each note's offset in beats.
    ///
    /// Phase 22 DX-13 quantize: <see cref="MusicalNoteData.OnsetOffset"/> is added to the
    /// emitted onset position only. It does NOT advance currentBeat — that would shift
    /// every subsequent note as well, which is not what quantize means. The default
    /// OnsetOffset (0.0) makes this addition a no-op for all pre-Phase-22 callers, so the
    /// byte-identical regression gate stays GREEN.
    /// </summary>
    public List<(MusicalNoteData note, double offsetBeats)> ToTimeline()
    {
        var result = new List<(MusicalNoteData, double)>();
        double currentBeat = 0;
        // Lead-onset of the most recent chord group. Chord-tones (IsChordTone=true)
        // share this onset and do NOT advance currentBeat — fixing the long-standing
        // chord-arpeggiation bug where [C E G]q rendered as a sequential C-E-G
        // arpeggio instead of a simultaneous strike. See MusicalNoteData.IsChordTone.
        double lastLeadOnset = 0;

        foreach (var note in MusicalNotes)
        {
            if (note.IsChordTone)
            {
                // Stack on the leading tone's onset. Do NOT advance currentBeat —
                // the leading tone already did. OnsetOffset still applies per-note
                // so quantize() can dither chord-tones independently if desired.
                result.Add((note, lastLeadOnset + note.OnsetOffset));
                continue;
            }

            // Leading tone (or plain non-chord note): emit at currentBeat (+ DX-13 offset),
            // record this onset as the chord-group lead, then advance the cursor.
            double onset = currentBeat + note.OnsetOffset;
            result.Add((note, onset));
            lastLeadOnset = currentBeat;  // raw lead position WITHOUT OnsetOffset, so
                                          // a chord-tone's own OnsetOffset stacks cleanly
            if (TimeSignature != null)
            {
                currentBeat += note.GetBeats(TimeSignature.Denominator);
            }
            else
            {
                currentBeat += 1.0; // Default to 1 beat if no time signature
            }
        }

        return result;
    }

    /// <summary>
    /// Formats the bar as a string.
    /// </summary>
    public override string ToString()
    {
        if (Mode == BarMode.Musical)
        {
            var timeSignature = TimeSignature != null
                ? $"{TimeSignature} "
                : "";

            var notesList = MusicalNotes.Count > 0
                ? string.Join(" ", MusicalNotes.Select(n => n.ToString()))
                : "(empty)";
            return $"Bar[{timeSignature}{notesList}]";
        }
        else
        {
            var timeSignature = TimeSignatureNumerator.HasValue && TimeSignatureDenominator.HasValue
                ? $"{TimeSignatureNumerator}/{TimeSignatureDenominator} "
                : "";

            var notesList = Notes.Count > 0 ? string.Join(" ", Notes) : "(empty)";
            return $"Bar[{timeSignature}{notesList}]";
        }
    }
}
