namespace FlowLang.TypeSystem.SpecialTypes;

/// <summary>
/// Represents a musical note (A-G with alterations: ++, +, natural, -, --).
/// </summary>
public sealed class NoteType : FlowType
{
    private NoteType() { }

    public static NoteType Instance { get; } = new();

    public override string Name => "Note";

    public override int GetSpecificity() => 130;

    /// <summary>
    /// Parses a note string like "A+", "C--", "G" into a note value.
    /// </summary>
    public static (char note, int alteration) Parse(string noteStr)
    {
        if (string.IsNullOrEmpty(noteStr))
            throw new ArgumentException("Note string cannot be empty");

        char note = char.ToUpper(noteStr[0]);
        if (note < 'A' || note > 'G')
            throw new ArgumentException($"Invalid note: {note}. Must be A-G.");

        string alterationPart = noteStr.Length > 1 ? noteStr[1..] : "";

        int alteration = alterationPart switch
        {
            "++" => 2,
            "+" => 1,
            "" => 0,
            "-" => -1,
            "--" => -2,
            _ => throw new ArgumentException($"Invalid alteration: {alterationPart}")
        };

        return (note, alteration);
    }

    /// <summary>
    /// Formats a note value back to string representation.
    /// </summary>
    public static string Format(char note, int alteration)
    {
        string alterationStr = alteration switch
        {
            2 => "++",
            1 => "+",
            0 => "",
            -1 => "-",
            -2 => "--",
            _ => throw new ArgumentException($"Invalid alteration: {alteration}")
        };

        return $"{note}{alterationStr}";
    }
}
