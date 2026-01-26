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
/// Runtime representation of a Bar containing notes.
/// </summary>
public class BarData
{
    /// <summary>
    /// The notes contained in this bar.
    /// Each note is stored as a string (e.g., "A4", "C3", "E5+").
    /// </summary>
    public List<string> Notes { get; }

    /// <summary>
    /// Optional: Time signature numerator (e.g., 4 in 4/4 time).
    /// </summary>
    public int? TimeSignatureNumerator { get; set; }

    /// <summary>
    /// Optional: Time signature denominator (e.g., 4 in 4/4 time).
    /// </summary>
    public int? TimeSignatureDenominator { get; set; }

    public BarData()
    {
        Notes = new List<string>();
        TimeSignatureNumerator = null;
        TimeSignatureDenominator = null;
    }

    public BarData(IEnumerable<string> notes)
    {
        Notes = new List<string>(notes);
        TimeSignatureNumerator = null;
        TimeSignatureDenominator = null;
    }

    public BarData(IEnumerable<string> notes, int numerator, int denominator)
    {
        Notes = new List<string>(notes);
        TimeSignatureNumerator = numerator;
        TimeSignatureDenominator = denominator;
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
    /// Formats the bar as a string.
    /// </summary>
    public override string ToString()
    {
        var timeSignature = TimeSignatureNumerator.HasValue && TimeSignatureDenominator.HasValue
            ? $"{TimeSignatureNumerator}/{TimeSignatureDenominator} "
            : "";

        var notesList = Notes.Count > 0 ? string.Join(" ", Notes) : "(empty)";
        return $"Bar[{timeSignature}{notesList}]";
    }
}
