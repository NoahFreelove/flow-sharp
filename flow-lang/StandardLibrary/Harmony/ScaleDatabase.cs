using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Harmony;

/// <summary>
/// Provides scale/key definitions and roman numeral resolution.
/// </summary>
public static class ScaleDatabase
{
    /// <summary>
    /// Semitone intervals for major scale: W-W-H-W-W-W-H
    /// </summary>
    private static readonly int[] MajorIntervals = { 0, 2, 4, 5, 7, 9, 11 };

    /// <summary>
    /// Semitone intervals for natural minor scale: W-H-W-W-H-W-W
    /// </summary>
    private static readonly int[] MinorIntervals = { 0, 2, 3, 5, 7, 8, 10 };

    /// <summary>
    /// Chord qualities for each degree of a major scale (I-VII).
    /// </summary>
    private static readonly string[] MajorQualities = { "maj", "m", "m", "maj", "maj", "m", "dim" };

    /// <summary>
    /// Chord qualities for each degree of a natural minor scale (i-vii).
    /// </summary>
    private static readonly string[] MinorQualities = { "m", "dim", "maj", "m", "m", "maj", "maj" };

    /// <summary>
    /// Map note names to semitone offsets from C.
    /// </summary>
    private static readonly Dictionary<string, int> NoteToSemitone = new(StringComparer.OrdinalIgnoreCase)
    {
        { "C", 0 }, { "Csharp", 1 }, { "Db", 1 },
        { "D", 2 }, { "Dsharp", 3 }, { "Eb", 3 },
        { "E", 4 },
        { "F", 5 }, { "Fsharp", 6 }, { "Gb", 6 },
        { "G", 7 }, { "Gsharp", 8 }, { "Ab", 8 },
        { "A", 9 }, { "Asharp", 10 }, { "Bb", 10 },
        { "B", 11 },
    };

    /// <summary>
    /// Chromatic note names for interval expansion.
    /// </summary>
    private static readonly string[] ChromaticNotes =
        { "C", "Cs", "D", "Ds", "E", "F", "Fs", "G", "Gs", "A", "As", "B" };

    /// <summary>
    /// Roman numeral base values.
    /// </summary>
    private static readonly Dictionary<string, int> RomanNumeralValues = new(StringComparer.Ordinal)
    {
        { "I", 0 }, { "II", 1 }, { "III", 2 }, { "IV", 3 }, { "V", 4 }, { "VI", 5 }, { "VII", 6 },
        { "i", 0 }, { "ii", 1 }, { "iii", 2 }, { "iv", 3 }, { "v", 4 }, { "vi", 5 }, { "vii", 6 },
    };

    /// <summary>
    /// Checks if text looks like a roman numeral chord reference.
    /// </summary>
    public static bool IsRomanNumeral(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        var (baseNumeral, _) = SplitRomanNumeral(text);
        return baseNumeral != null;
    }

    /// <summary>
    /// Splits a roman numeral string into the base numeral and optional quality extension.
    /// </summary>
    private static (string? baseNumeral, string? extension) SplitRomanNumeral(string text)
    {
        // Try longest roman numeral first to avoid partial matches
        string[] upperNumerals = { "VII", "III", "VI", "IV", "II", "V", "I" };
        string[] lowerNumerals = { "vii", "iii", "vi", "iv", "ii", "v", "i" };

        foreach (var rn in upperNumerals)
        {
            if (text.StartsWith(rn, StringComparison.Ordinal))
            {
                string ext = text[rn.Length..];
                if (ext.Length == 0 || IsQualityExtension(ext))
                    return (rn, ext.Length == 0 ? null : ext);
            }
        }

        foreach (var rn in lowerNumerals)
        {
            if (text.StartsWith(rn, StringComparison.Ordinal))
            {
                string ext = text[rn.Length..];
                if (ext.Length == 0 || IsQualityExtension(ext))
                    return (rn, ext.Length == 0 ? null : ext);
            }
        }

        return (null, null);
    }

    private static bool IsQualityExtension(string ext)
    {
        return ext is "7" or "maj7" or "min7" or "m7" or "dim7" or "sus2" or "sus4"
            or "9" or "6" or "m6" or "add9" or "aug" or "dim";
    }

    /// <summary>
    /// Resolves a roman numeral in a key context to a ChordData.
    /// </summary>
    public static ChordData? ResolveRomanNumeral(string numeral, string keyName)
    {
        var (baseNumeral, extension) = SplitRomanNumeral(numeral);
        if (baseNumeral == null)
            return null;

        if (!TryParseKey(keyName, out string? rootNote, out bool isMajor))
            return null;

        if (!NoteToSemitone.TryGetValue(rootNote!, out int keySemitone))
            return null;

        if (!RomanNumeralValues.TryGetValue(baseNumeral, out int degree))
            return null;

        var intervals = isMajor ? MajorIntervals : MinorIntervals;
        var defaultQualities = isMajor ? MajorQualities : MinorQualities;

        int chordRootSemitone = (keySemitone + intervals[degree]) % 12;

        string quality;
        if (extension != null)
        {
            quality = extension;
        }
        else
        {
            quality = defaultQualities[degree];
        }

        string chordRoot = ChromaticNotes[chordRootSemitone];
        string chordSymbol = chordRoot + quality;

        if (ChordParser.TryParse(chordSymbol, out var chordData))
            return chordData;

        return null;
    }

    /// <summary>
    /// Parses a key name like "Cmajor", "Aminor", "Fsharpmajor" into root note and mode.
    /// </summary>
    private static bool TryParseKey(string keyName, out string? rootNote, out bool isMajor)
    {
        rootNote = null;
        isMajor = true;

        if (string.IsNullOrEmpty(keyName))
            return false;

        string lower = keyName.ToLowerInvariant();

        if (lower.EndsWith("major"))
        {
            isMajor = true;
            rootNote = keyName[..^5];
        }
        else if (lower.EndsWith("minor"))
        {
            isMajor = false;
            rootNote = keyName[..^5];
        }
        else
        {
            return false;
        }

        if (rootNote.Length == 0)
            return false;

        rootNote = char.ToUpper(rootNote[0]) + rootNote[1..].ToLower();

        if (!NoteToSemitone.ContainsKey(rootNote))
        {
            rootNote = null;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Returns the scale notes for a given key.
    /// </summary>
    public static string[]? GetScaleNotes(string keyName)
    {
        if (!TryParseKey(keyName, out string? rootNote, out bool isMajor))
            return null;

        if (!NoteToSemitone.TryGetValue(rootNote!, out int keySemitone))
            return null;

        var intervals = isMajor ? MajorIntervals : MinorIntervals;
        var notes = new string[7];

        for (int i = 0; i < 7; i++)
        {
            int semitone = (keySemitone + intervals[i]) % 12;
            notes[i] = ChromaticNotes[semitone];
        }

        return notes;
    }
}
