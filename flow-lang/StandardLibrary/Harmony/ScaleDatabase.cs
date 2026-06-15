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
    /// Per-mode scale interval tables (semitone offsets from the tonic). Each church
    /// mode is a rotation of the major scale; sweep-0614 wires these into
    /// <see cref="ResolveRomanNumeral"/> + <see cref="GetScaleNotes"/> so a valid
    /// <c>key Ddorian { ... }</c> context no longer silently resolves every roman
    /// numeral to a rest. Major/Minor reuse the existing arrays.
    /// </summary>
    private static readonly Dictionary<FlowLang.StandardLibrary.Audio.Tuning.Mode, int[]> ModeIntervals = new()
    {
        { FlowLang.StandardLibrary.Audio.Tuning.Mode.Major,      MajorIntervals },
        { FlowLang.StandardLibrary.Audio.Tuning.Mode.Minor,      MinorIntervals },
        { FlowLang.StandardLibrary.Audio.Tuning.Mode.Dorian,     new[] { 0, 2, 3, 5, 7, 9, 10 } },
        { FlowLang.StandardLibrary.Audio.Tuning.Mode.Phrygian,   new[] { 0, 1, 3, 5, 7, 8, 10 } },
        { FlowLang.StandardLibrary.Audio.Tuning.Mode.Lydian,     new[] { 0, 2, 4, 6, 7, 9, 11 } },
        { FlowLang.StandardLibrary.Audio.Tuning.Mode.Mixolydian, new[] { 0, 2, 4, 5, 7, 9, 10 } },
        { FlowLang.StandardLibrary.Audio.Tuning.Mode.Locrian,    new[] { 0, 1, 3, 5, 6, 8, 10 } },
    };

    /// <summary>
    /// Per-mode diatonic chord-quality tables (degree I..VII). Used only as the
    /// fallback when the numeral carries no explicit quality extension AND the
    /// composer's case does not already pin the triad quality (the case override in
    /// <see cref="ResolveRomanNumeral"/> wins for maj/m; dim is taken from here when
    /// the numeral is lowercase). Standard diatonic triad qualities for each mode.
    /// </summary>
    private static readonly Dictionary<FlowLang.StandardLibrary.Audio.Tuning.Mode, string[]> ModeQualities = new()
    {
        { FlowLang.StandardLibrary.Audio.Tuning.Mode.Major,      MajorQualities },
        { FlowLang.StandardLibrary.Audio.Tuning.Mode.Minor,      MinorQualities },
        { FlowLang.StandardLibrary.Audio.Tuning.Mode.Dorian,     new[] { "m", "m", "maj", "maj", "m", "dim", "maj" } },
        { FlowLang.StandardLibrary.Audio.Tuning.Mode.Phrygian,   new[] { "m", "maj", "maj", "m", "dim", "maj", "m" } },
        { FlowLang.StandardLibrary.Audio.Tuning.Mode.Lydian,     new[] { "maj", "maj", "m", "dim", "maj", "m", "m" } },
        { FlowLang.StandardLibrary.Audio.Tuning.Mode.Mixolydian, new[] { "maj", "m", "dim", "maj", "m", "m", "maj" } },
        { FlowLang.StandardLibrary.Audio.Tuning.Mode.Locrian,    new[] { "dim", "maj", "m", "m", "maj", "maj", "m" } },
    };

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

        // sweep-0614: route through the mode-aware parser so church-mode keys
        // (Ddorian/Aphrygian/...) resolve instead of returning null → all-rests.
        if (!TryParseKeyWithMode(keyName, out string? rootNote, out var mode))
            return null;

        if (!NoteToSemitone.TryGetValue(rootNote!, out int keySemitone))
            return null;

        if (!RomanNumeralValues.TryGetValue(baseNumeral, out int degree))
            return null;

        var intervals = ModeIntervals.TryGetValue(mode, out var modeIv) ? modeIv : MajorIntervals;
        var defaultQualities = ModeQualities.TryGetValue(mode, out var modeQ) ? modeQ : MajorQualities;

        int chordRootSemitone = (keySemitone + intervals[degree]) % 12;

        string quality;
        if (extension != null)
        {
            // Explicit quality extension (V7, iv7, ...) always wins — untouched.
            quality = extension;
        }
        else
        {
            // sweep-0614: honor the composer's case as triad-quality intent
            // (uppercase = major triad, lowercase = minor triad — the standard
            // Roman-numeral-analysis convention). This fixes borrowed/chromatic
            // chords like minor `iv` in a major key (F-Ab-C, not F-A-C). The
            // diatonic-default `dim` is preserved ONLY for a lowercase numeral on
            // a degree whose diatonic triad is diminished (so `vii` in major stays
            // the diminished leading-tone triad), since case alone cannot express it.
            quality = char.IsUpper(baseNumeral[0])
                ? "maj"
                : (defaultQualities[degree] == "dim" ? "dim" : "m");
        }

        string chordRoot = ChromaticNotes[chordRootSemitone];
        string chordSymbol = chordRoot + quality;

        if (ChordParser.TryParse(chordSymbol, out var chordData))
            return chordData;

        return null;
    }

    /// <summary>
    /// Phase 23 D-04 / WARNING-8: canonical key parser entry returning a
    /// <see cref="FlowLang.StandardLibrary.Audio.Tuning.Mode"/> rather than the
    /// legacy bool isMajor. Recognizes all 7 mode suffixes (major, minor + the 5
    /// church modes: dorian, phrygian, lydian, mixolydian, locrian), with
    /// longer-suffix-first ordering to avoid false-suffix-match (e.g. "lydian" is a
    /// substring of "mixolydian").
    ///
    /// sweep-0614: <see cref="ResolveRomanNumeral"/> and <see cref="GetScaleNotes"/>
    /// now route through this mode-aware parser (the legacy bool-isMajor
    /// <c>TryParseKey</c> was removed — it ignored the 5 church-mode suffixes, so a
    /// valid <c>key Ddorian { }</c> context resolved every roman numeral to a rest).
    /// </summary>
    public static bool TryParseKeyWithMode(string keyName, out string? rootNote, out FlowLang.StandardLibrary.Audio.Tuning.Mode mode)
    {
        rootNote = null;
        mode = FlowLang.StandardLibrary.Audio.Tuning.Mode.Major;

        if (string.IsNullOrEmpty(keyName)) return false;
        string lower = keyName.ToLowerInvariant();

        // Wave 3 (Plan 23-03 Task 1): widened to recognize the 5 church-mode suffixes
        // alongside the Wave 2 major/minor branch. Longer-suffix-first ordering is mandatory
        // to avoid false-prefix-match — `lydian` is a substring of `mixolydian`, so
        // `mixolydian` MUST be tested before `lydian`. Similarly `phrygian` (8) and
        // `locrian` (7) come before `dorian` (6) and `lydian` (6) to keep the chain
        // monotonically descending in suffix length.
        int suffixLen;
        if      (lower.EndsWith("mixolydian")) { mode = FlowLang.StandardLibrary.Audio.Tuning.Mode.Mixolydian; suffixLen = 10; }
        else if (lower.EndsWith("phrygian"))   { mode = FlowLang.StandardLibrary.Audio.Tuning.Mode.Phrygian;   suffixLen = 8; }
        else if (lower.EndsWith("locrian"))    { mode = FlowLang.StandardLibrary.Audio.Tuning.Mode.Locrian;    suffixLen = 7; }
        else if (lower.EndsWith("dorian"))     { mode = FlowLang.StandardLibrary.Audio.Tuning.Mode.Dorian;     suffixLen = 6; }
        else if (lower.EndsWith("lydian"))     { mode = FlowLang.StandardLibrary.Audio.Tuning.Mode.Lydian;     suffixLen = 6; }
        else if (lower.EndsWith("major"))      { mode = FlowLang.StandardLibrary.Audio.Tuning.Mode.Major;      suffixLen = 5; }
        else if (lower.EndsWith("minor"))      { mode = FlowLang.StandardLibrary.Audio.Tuning.Mode.Minor;      suffixLen = 5; }
        else return false;

        rootNote = keyName[..^suffixLen];
        if (rootNote.Length == 0) { rootNote = null; return false; }
        // Phase 48 D-48-03 (invariant globalization): char.ToUpperInvariant +
        // string.ToLowerInvariant match the ASCII root-note alphabet
        // (A..G, a..g, plus '#'/'b' accidentals) regardless of host locale.
        // Avoids Turkish-I problem under <InvariantGlobalization>true</InvariantGlobalization>.
        rootNote = char.ToUpperInvariant(rootNote[0]) + rootNote[1..].ToLowerInvariant();
        if (!NoteToSemitone.ContainsKey(rootNote)) { rootNote = null; return false; }
        return true;
    }

    /// <summary>
    /// Returns the scale notes for a given key.
    /// </summary>
    public static string[]? GetScaleNotes(string keyName)
    {
        // sweep-0614: mode-aware so (scaleNotes "Ddorian") returns the 7 modal
        // pitches instead of [] (which silently dropped notes in modal contexts).
        if (!TryParseKeyWithMode(keyName, out string? rootNote, out var mode))
            return null;

        if (!NoteToSemitone.TryGetValue(rootNote!, out int keySemitone))
            return null;

        var intervals = ModeIntervals.TryGetValue(mode, out var modeIv) ? modeIv : MajorIntervals;
        var notes = new string[7];

        for (int i = 0; i < 7; i++)
        {
            int semitone = (keySemitone + intervals[i]) % 12;
            notes[i] = ChromaticNotes[semitone];
        }

        return notes;
    }
}
