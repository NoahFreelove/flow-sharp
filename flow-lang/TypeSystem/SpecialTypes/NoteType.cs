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
        int octave = 4; // Default octave (no digits)
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

    public static int ToMidiNote(char note, int octave, int alteration)
    {
        return GetNoteValue(note, octave) + alteration;
    }

    public static (char note, int octave, int alteration) FromMidiNote(int midiNote)
    {
        if (midiNote < 12 || midiNote > 127) throw new ArgumentOutOfRangeException(nameof(midiNote));
        int octave = (midiNote / 12) - 1;
        int noteNum = midiNote % 12;

        return noteNum switch
        {
            0 => ('C', octave, 0),
            1 => ('C', octave, 1),
            2 => ('D', octave, 0),
            3 => ('D', octave, 1),
            4 => ('E', octave, 0),
            5 => ('F', octave, 0),
            6 => ('F', octave, 1),
            7 => ('G', octave, 0),
            8 => ('G', octave, 1),
            9 => ('A', octave, 0),
            10 => ('A', octave, 1),
            11 => ('B', octave, 0),
            _ => throw new InvalidOperationException()
        };
    }

    /// <summary>
    /// Formats a note value back to string representation.
    ///
    /// Phase 14 DX-06 (CONTEXT D-08): run-based emission for any int alteration.
    /// Canonical shape: <c>{letter}{octave}{'+' * n | '-' * |n|}</c>. Round-trip invariant
    /// <c>Parse(Format(x)) == x</c> holds for every integer alteration (bounded only by the
    /// post-alteration MIDI range check in Parse).
    /// </summary>
    public static string Format(char note, int octave, int alteration)
    {
        string altStr;
        if (alteration == 0)
        {
            altStr = "";
        }
        else if (alteration > 0)
        {
            altStr = new string('+', alteration);
        }
        else
        {
            altStr = new string('-', -alteration);
        }

        return $"{note}{octave}{altStr}";
    }
}

/// <summary>
/// Articulation affects how a note's envelope is shaped.
/// </summary>
public enum Articulation
{
    Normal,     // Default envelope
    Staccato,   // Short, detached (~50% duration)
    Tenuto,     // Full sustain, held to full value
    Marcato,    // Accented + slightly shortened
    Accent,     // Velocity bump, normal duration
    Sforzando   // Sudden loud spike, then return to previous dynamic
}

/// <summary>
/// Represents a musical note with pitch, duration, and rest information for classical composition.
/// </summary>
public class MusicalNoteData
{
    public char NoteName { get; }
    public int Octave { get; }
    public int Alteration { get; }
    public int? DurationValue { get; }
    public bool IsRest { get; }
    public double? CentOffset { get; }
    public bool IsTied { get; }
    public bool IsDotted { get; }
    public double Velocity { get; }
    public Articulation Articulation { get; }

    /// <summary>
    /// Optional source location for timeline mapping (editor live highlighting).
    /// </summary>
    public FlowLang.Core.SourceLocation? SourceLocation { get; }

    /// <summary>
    /// Length of the original source token (e.g., "C4" = 2, "C4q." = 4) for precise highlighting.
    /// </summary>
    public int SourceLength { get; }

    public MusicalNoteData(char noteName, int octave, int alteration, int? durationValue, bool isRest, double? centOffset = null, bool isTied = false, double velocity = 0.63, Articulation articulation = Articulation.Normal, bool isDotted = false, FlowLang.Core.SourceLocation? sourceLocation = null, int sourceLength = 0)
    {
        NoteName = noteName;
        Octave = octave;
        Alteration = alteration;
        DurationValue = durationValue;
        IsRest = isRest;
        CentOffset = centOffset;
        IsTied = isTied;
        IsDotted = isDotted;
        Velocity = Math.Clamp(velocity, 0.0, 1.0);
        Articulation = articulation;
        SourceLocation = sourceLocation;
        SourceLength = sourceLength;
    }

    /// <summary>
    /// Calculates the duration of this note in beats based on the time signature denominator.
    /// </summary>
    public double GetBeats(int timeSigDenominator)
    {
        if (!DurationValue.HasValue)
            return 1.0; // Default to 1 beat if no duration specified

        double fraction = NoteValueType.ToFraction((NoteValueType.Value)DurationValue.Value);
        if (IsDotted) fraction *= 1.5;
        return fraction * timeSigDenominator;
    }

    public override string ToString()
    {
        if (IsRest)
        {
            string durationName = DurationValue.HasValue
                ? NoteValueType.Format((NoteValueType.Value)DurationValue.Value)
                : "quarter";
            return $"{durationName}Rest";
        }

        string noteStr = NoteType.Format(NoteName, Octave, Alteration);
        string durationName2 = DurationValue.HasValue
            ? NoteValueType.Format((NoteValueType.Value)DurationValue.Value)
            : "quarter";
        return $"{durationName2}({noteStr})";
    }
}
