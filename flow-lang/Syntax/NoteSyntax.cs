namespace FlowLang.Syntax;

/// <summary>
/// Lexical rules for note names (<c>C4</c>, <c>F#5</c>, <c>Bb-</c>). Part of Flow's one
/// grammar: the lexer uses these rules to decide what is a note literal, so they live in
/// the language layer; the music layer's <c>NoteType</c> delegates here.
/// </summary>
public static class NoteSyntax
{
    /// <summary>
    /// Parses a note string like "A4", "C3", "G" (defaults to octave 4) into a note value.
    ///
    /// Phase 14 DX-06 (CONTEXT D-07/D-08/D-09): accepts arbitrary composition of
    /// <c>b</c>/<c>#</c>/<c>+</c>/<c>-</c> on either side of octave digits. Net alteration =
    /// (count of sharps <c>#</c>+<c>+</c>) − (count of flats <c>b</c>+<c>-</c>). Alteration
    /// is any int (not bounded to ±2). Range validation uses post-alteration MIDI value.
    ///
    /// Examples:
    ///   <c>"Db4"</c>   → (D, 4, -1)
    ///   <c>"Bb"</c>    → (B, 4, -1) (default octave 4)
    ///   <c>"C#5"</c>   → (C, 5, +1)
    ///   <c>"F##4"</c>  → (F, 4, +2)
    ///   <c>"Bb-+bbb"</c> → (B, 4, -4)
    ///   <c>"Cb4"</c>   → (C, 4, -1) MIDI 59 = B3, in range
    ///   <c>"Cb0"</c>   → throws ArgumentException (post-alt MIDI 11, below E0=16)
    /// </summary>
    public static (char note, int octave, int alteration) Parse(string noteStr)
        => Parse(noteStr, 4);

    public static (char note, int octave, int alteration) Parse(string noteStr, int defaultOctave)
    {
        if (string.IsNullOrEmpty(noteStr))
            throw new ArgumentException("Note string cannot be empty");

        char note = char.ToUpper(noteStr[0]);
        if (note < 'A' || note > 'G')
            throw new ArgumentException($"Invalid note: {note}. Must be A-G.");

        // Sum-based scan across the remaining chars (D-07). Three phases:
        //   1. Pre-octave alteration chars (b/#/+/-)
        //   2. Octave digits (contiguous)
        //   3. Post-octave alteration chars (b/#/+/-)
        int sharpCount = 0;
        int flatCount = 0;
        int octave = defaultOctave; // Default octave when no digits present
        int i = 1;

        // Phase 1: pre-octave alterations
        while (i < noteStr.Length && !char.IsDigit(noteStr[i]))
        {
            switch (noteStr[i])
            {
                case '+':
                case '#':
                    sharpCount++;
                    break;
                case '-':
                case 'b':
                    flatCount++;
                    break;
                default:
                    throw new ArgumentException($"Invalid note character '{noteStr[i]}' in {noteStr}");
            }
            i++;
        }

        // Phase 2: octave digits
        int octStart = i;
        while (i < noteStr.Length && char.IsDigit(noteStr[i]))
        {
            i++;
        }
        if (i > octStart)
        {
            octave = int.Parse(noteStr[octStart..i]);
        }

        // Phase 3: post-octave alterations
        while (i < noteStr.Length)
        {
            switch (noteStr[i])
            {
                case '+':
                case '#':
                    sharpCount++;
                    break;
                case '-':
                case 'b':
                    flatCount++;
                    break;
                default:
                    throw new ArgumentException($"Invalid note character '{noteStr[i]}' in {noteStr}");
            }
            i++;
        }

        int alteration = sharpCount - flatCount;

        // Post-alteration MIDI range check (D-09): replaces letter+octave-only IsValidNoteRange.
        // Cb4 (MIDI 59 = B3) is in range; Cb0 (MIDI 11) is below E0 (MIDI 16) and throws.
        int midi = GetNoteValue(note, octave) + alteration;
        int minMidi = GetNoteValue('E', 0);
        int maxMidi = GetNoteValue('E', 10);
        if (midi < minMidi || midi > maxMidi)
        {
            throw new ArgumentException($"Note {noteStr} is out of valid range (E0 to E10)");
        }

        return (note, octave, alteration);
    }

    /// <summary>
    /// Converts a note and octave to a MIDI-like note number for range validation.
    /// Public so tests and helpers outside NoteType can compute ranges without duplicating
    /// the chromatic mapping.
    /// </summary>
    public static int GetNoteValue(char note, int octave)
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
}
