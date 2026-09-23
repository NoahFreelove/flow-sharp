using FlowLang.Runtime;
using FlowLang.StandardLibrary.Harmony;
using FlowLang.Syntax;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.Music;

/// <summary>Music-aware match patterns: chord literals, roman numerals, articulations.</summary>
internal static class MusicPatterns
{
    /// <summary>
    /// Phase 35 Plan 35-06 (LANG-02) — matches a chord-literal pattern (e.g.,
    /// <c>Cmaj7</c>, <c>Dm7</c>) against a Chord scrutinee. The canonical
    /// equality per RESEARCH §Example 2 is Root + Quality match — different
    /// roots miss, different qualities miss, only structural equality on
    /// both fields produces a hit. Octave is intentionally ignored so a
    /// composer matching <c>Cmaj7</c> hits any Cmaj7 chord value regardless
    /// of the octave the scrutinee was rendered at.
    /// </summary>
    public static bool MatchChordQuality(string chordText, Value scrutinee)
    {
        if (scrutinee.Data is not ChordData scrutineeChord)
            return false;

        if (!ChordParser.TryParse(chordText, out var expected) || expected == null)
            return false;

        return string.Equals(scrutineeChord.Root, expected.Root, StringComparison.Ordinal)
            && string.Equals(scrutineeChord.Quality, expected.Quality, StringComparison.Ordinal);
    }

    /// <summary>
    /// Phase 35 Plan 35-06 (LANG-02) — matches a roman-numeral pattern (e.g.,
    /// <c>V7</c>, <c>I</c>, <c>vi</c>) against a Chord scrutinee. The numeral
    /// is resolved against the active key context (read from
    /// <see cref="MusicalContext.Key"/>) via
    /// <see cref="ScaleDatabase.ResolveRomanNumeral"/>, then compared to the
    /// scrutinee by Root + Quality (mirroring MatchChordQuality). When no
    /// key is active or the resolution fails, the match misses charitably
    /// rather than throwing — the composer is expected to scope the match
    /// inside a <c>key X { ... }</c> block, but a missing key is a
    /// composer-error condition, not a runtime crash.
    /// </summary>
    public static bool MatchRomanNumeral(
        string numeral,
        Value scrutinee,
        Runtime.ExecutionContext context)
    {
        if (scrutinee.Data is not ChordData scrutineeChord)
            return false;

        var musical = context.GetMusicalContext();
        if (musical.Key is null)
            return false;

        var resolved = ScaleDatabase.ResolveRomanNumeral(numeral, musical.Key);
        if (resolved is null)
            return false;

        return string.Equals(scrutineeChord.Root, resolved.Root, StringComparison.Ordinal)
            && string.Equals(scrutineeChord.Quality, resolved.Quality, StringComparison.Ordinal);
    }

    /// <summary>
    /// Phase 35 Plan 35-06 (LANG-02) — matches an articulation-symbol pattern
    /// (e.g., <c>#staccato</c>, <c>#legato</c>, <c>#accent</c>) against a
    /// MusicalNote scrutinee by comparing the symbol body (case-insensitive
    /// per Phase 28's lex-time normalization) to the note's
    /// <see cref="Articulation"/> enum value. Unknown symbol names produce
    /// a charitable miss rather than throwing.
    /// </summary>
    public static bool MatchArticulation(string symbolName, Value scrutinee)
    {
        if (scrutinee.Data is not MusicalNoteData musicalNote)
            return false;

        if (!Enum.TryParse<Articulation>(symbolName, ignoreCase: true, out var expected))
            return false;

        return musicalNote.Articulation == expected;
    }
}
