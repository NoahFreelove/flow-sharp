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
    /// Validates that the total duration of notes fits within the time signature.
    /// </summary>
    public bool ValidateDuration()
    {
        if (Mode != BarMode.Musical || TimeSignature == null)
            return true;

        double totalBeats = MusicalNotes.Sum(n => n.GetBeats(TimeSignature.Denominator));
        return totalBeats <= TimeSignature.Numerator;
    }

    /// <summary>
    /// Converts the bar to a timeline with each note's offset in beats.
    /// </summary>
    public List<(MusicalNoteData note, double offsetBeats)> ToTimeline()
    {
        var result = new List<(MusicalNoteData, double)>();
        double currentBeat = 0;

        foreach (var note in MusicalNotes)
        {
            result.Add((note, currentBeat));
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
