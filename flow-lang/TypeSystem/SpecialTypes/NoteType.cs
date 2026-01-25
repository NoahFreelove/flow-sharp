namespace FlowLang.TypeSystem.SpecialTypes;

/// <summary>
/// Represents a musical note with octave notation (e.g., A4, C3, E0-E10 range).
/// Default octave is 4 (middle octave).
/// </summary>
public sealed class NoteType : FlowType
{
    private NoteType() { }

    public static NoteType Instance { get; } = new();

    public override string Name => "Note";

    public override int GetSpecificity() => 130;

    /// <summary>
    /// Parses a note string like "A4", "C3", "G" (defaults to octave 4) into a note value.
    /// Valid range: E0 to E10.
    /// </summary>
    public static (char note, int octave, int alteration) Parse(string noteStr)
    {
        if (string.IsNullOrEmpty(noteStr))
            throw new ArgumentException("Note string cannot be empty");

        char note = char.ToUpper(noteStr[0]);
        if (note < 'A' || note > 'G')
            throw new ArgumentException($"Invalid note: {note}. Must be A-G.");

        // Parse remaining part (could be octave and/or alteration)
        string remaining = noteStr.Length > 1 ? noteStr[1..] : "";

        int octave = 4; // Default octave
        int alteration = 0;

        if (remaining.Length > 0)
        {
            // Check if it starts with a digit (octave)
            int octaveEndIndex = 0;
            while (octaveEndIndex < remaining.Length && char.IsDigit(remaining[octaveEndIndex]))
            {
                octaveEndIndex++;
            }

            if (octaveEndIndex > 0)
            {
                string octaveStr = remaining.Substring(0, octaveEndIndex);
                octave = int.Parse(octaveStr);
                remaining = remaining.Substring(octaveEndIndex);
            }

            // Parse alteration if any
            if (remaining.Length > 0)
            {
                alteration = remaining switch
                {
                    "++" => 2,
                    "+" => 1,
                    "-" => -1,
                    "--" => -2,
                    _ => throw new ArgumentException($"Invalid alteration: {remaining}")
                };
            }
        }

        // Validate range: E0 to E10
        if (!IsValidNoteRange(note, octave))
        {
            throw new ArgumentException($"Note {note}{octave} is out of valid range (E0 to E10)");
        }

        return (note, octave, alteration);
    }

    /// <summary>
    /// Checks if a note and octave combination is within the valid range (E0 to E10).
    /// </summary>
    private static bool IsValidNoteRange(char note, int octave)
    {
        // Calculate MIDI-like note number (C0 = 12, C4 = 60)
        int noteValue = GetNoteValue(note, octave);

        // E0 = 16, E10 = 136
        int minNote = GetNoteValue('E', 0);  // E0
        int maxNote = GetNoteValue('E', 10); // E10

        return noteValue >= minNote && noteValue <= maxNote;
    }

    /// <summary>
    /// Converts a note and octave to a MIDI-like note number for range validation.
    /// </summary>
    private static int GetNoteValue(char note, int octave)
    {
        int noteOffset = note switch
        {
            'C' => 0,
            'D' => 2,
            'E' => 4,
            'F' => 5,
            'G' => 7,
            'A' => 9,
            'B' => 11,
            _ => throw new ArgumentException($"Invalid note: {note}")
        };

        return (octave + 1) * 12 + noteOffset; // C0 = 12
    }

    /// <summary>
    /// Formats a note value back to string representation.
    /// </summary>
    public static string Format(char note, int octave, int alteration)
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

        return $"{note}{octave}{alterationStr}";
    }
}
