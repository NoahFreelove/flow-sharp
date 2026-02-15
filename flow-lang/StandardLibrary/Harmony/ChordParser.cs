using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Harmony;

/// <summary>
/// Parses chord symbols like "Cmaj7", "Dm", "Gsus4" into ChordData.
/// Accidentals use 's' for sharp and 'f' for flat (e.g., "Csmaj7" = C# major 7).
/// </summary>
public static class ChordParser
{
    /// <summary>
    /// Interval sets for each chord quality, in semitones from root.
    /// </summary>
    private static readonly Dictionary<string, int[]> QualityIntervals = new()
    {
        { "maj", new[] { 0, 4, 7 } },
        { "m", new[] { 0, 3, 7 } },
        { "min", new[] { 0, 3, 7 } },
        { "dim", new[] { 0, 3, 6 } },
        { "aug", new[] { 0, 4, 8 } },
        { "7", new[] { 0, 4, 7, 10 } },
        { "dom7", new[] { 0, 4, 7, 10 } },
        { "maj7", new[] { 0, 4, 7, 11 } },
        { "m7", new[] { 0, 3, 7, 10 } },
        { "min7", new[] { 0, 3, 7, 10 } },
        { "dim7", new[] { 0, 3, 6, 9 } },
        { "m7f5", new[] { 0, 3, 6, 10 } },
        { "sus2", new[] { 0, 2, 7 } },
        { "sus4", new[] { 0, 5, 7 } },
        { "add9", new[] { 0, 4, 7, 14 } },
        { "9", new[] { 0, 4, 7, 10, 14 } },
        { "6", new[] { 0, 4, 7, 9 } },
        { "m6", new[] { 0, 3, 7, 9 } },
    };

    /// <summary>
    /// Note names in chromatic order for interval calculation.
    /// </summary>
    private static readonly string[] ChromaticNotes =
        { "C", "Cs", "D", "Ds", "E", "F", "Fs", "G", "Gs", "A", "As", "B" };

    /// <summary>
    /// Map from note name (with accidental) to semitone offset from C.
    /// </summary>
    private static readonly Dictionary<string, int> NoteToSemitone = new()
    {
        { "C", 0 }, { "Cs", 1 }, { "Df", 1 },
        { "D", 2 }, { "Ds", 3 }, { "Ef", 3 },
        { "E", 4 }, { "Ff", 4 },
        { "F", 5 }, { "Es", 5 }, { "Fs", 6 }, { "Gf", 6 },
        { "G", 7 }, { "Gs", 8 }, { "Af", 8 },
        { "A", 9 }, { "As", 10 }, { "Bf", 10 },
        { "B", 11 }, { "Cf", 11 },
    };

    /// <summary>
    /// Checks whether a text token is a chord symbol (for lexer use).
    /// Must be at least 2 chars, start with A-G, have optional accidental (s/f),
    /// and remaining text must match a known quality.
    /// Note: The lexer calls TryParseNote first, so anything reaching this method
    /// has already failed note parsing (e.g., C4 is caught as a note before this runs).
    /// </summary>
    public static bool IsChordSymbol(string text)
    {
        if (text.Length < 2)
            return false;

        char first = text[0];
        if (first < 'A' || first > 'G')
            return false;

        // Try without accidental first (e.g., "Dsus2" = D + sus2, not Ds + us2)
        string qualityNoAcc = text[1..];
        if (qualityNoAcc.Length > 0 && QualityIntervals.ContainsKey(qualityNoAcc))
            return true;

        // Try with accidental (e.g., "Csmaj7" = Cs + maj7)
        if (text.Length >= 2 && (text[1] == 's' || text[1] == 'f'))
        {
            string qualityWithAcc = text[2..];
            if (qualityWithAcc.Length == 0)
            {
                // "Cs", "Df" — root with accidental, no quality = major chord
                return true;
            }
            if (QualityIntervals.ContainsKey(qualityWithAcc))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Tries to parse a chord symbol string into a ChordData.
    /// </summary>
    public static bool TryParse(string text, out ChordData? chord)
    {
        chord = null;

        if (text.Length < 2)
            return false;

        char first = text[0];
        if (first < 'A' || first > 'G')
            return false;

        // Try two interpretations:
        // 1. No accidental: "Dsus2" → root="D", quality="sus2"
        // 2. With accidental: "Csmaj7" → root="Cs", quality="maj7"
        string root;
        string quality;

        // Try without accidental first
        string qualityNoAcc = text[1..];
        if (qualityNoAcc.Length > 0 && QualityIntervals.ContainsKey(qualityNoAcc))
        {
            root = first.ToString();
            quality = qualityNoAcc;
        }
        else if (text.Length >= 2 && (text[1] == 's' || text[1] == 'f'))
        {
            // Try with accidental
            root = text[..2];
            quality = text[2..];
            if (quality.Length == 0)
                quality = "maj"; // bare accidental = major chord
            else if (!QualityIntervals.ContainsKey(quality))
                return false;
        }
        else
        {
            return false;
        }

        // Look up intervals
        if (!QualityIntervals.TryGetValue(quality, out var intervals))
            return false;

        if (!NoteToSemitone.TryGetValue(root, out int rootSemitone))
            return false;

        // Expand to note names at default octave 4
        int octave = 4;
        var noteNames = ExpandIntervals(rootSemitone, intervals, octave);

        chord = new ChordData(root, quality, octave, noteNames);
        return true;
    }

    /// <summary>
    /// Expands interval set from a root semitone at a given octave to note name strings.
    /// </summary>
    private static string[] ExpandIntervals(int rootSemitone, int[] intervals, int baseOctave)
    {
        var notes = new string[intervals.Length];
        for (int i = 0; i < intervals.Length; i++)
        {
            int absoluteSemitone = rootSemitone + intervals[i];
            int octaveOffset = absoluteSemitone / 12;
            int noteIndex = absoluteSemitone % 12;
            if (noteIndex < 0)
            {
                noteIndex += 12;
                octaveOffset--;
            }

            string noteName = ChromaticNotes[noteIndex];
            int noteOctave = baseOctave + octaveOffset;

            // Convert internal name (e.g., "Cs") to display format (e.g., "C")
            // For display, use the standard letter. Sharp notes get displayed with 's'.
            // Map back to standard note letter for NoteType compatibility
            string displayNote = noteName.Length == 1
                ? $"{noteName}{noteOctave}"
                : $"{noteName[0]}{noteOctave}+"; // sharp = +

            notes[i] = displayNote;
        }
        return notes;
    }

    /// <summary>
    /// Creates a ChordData with a specific octave override.
    /// </summary>
    public static ChordData? WithOctave(ChordData original, int newOctave)
    {
        if (!NoteToSemitone.TryGetValue(original.Root, out int rootSemitone))
            return null;

        if (!QualityIntervals.TryGetValue(
            original.Quality.Length == 0 ? "maj" : original.Quality, out var intervals))
            return null;

        var noteNames = ExpandIntervals(rootSemitone, intervals, newOctave);
        return new ChordData(original.Root, original.Quality, newOctave, noteNames);
    }
}
