namespace FlowLang.StandardLibrary.Notation;

/// <summary>
/// Phase 39 Plan 39-02 — LilyPond pitch + duration mapping helpers per
/// Pitfall 2 (Dutch convention). LilyPond accepts pitches as
/// <c>{letter}{accidental}{octave-marker}</c> where:
///
/// <list type="bullet">
///   <item><c>letter</c>: lowercase <c>a</c>..<c>g</c></item>
///   <item><c>accidental</c>: <c>is</c> (sharp), <c>es</c> (flat),
///     <c>isis</c> (double-sharp), <c>eses</c> (double-flat), empty
///     (natural)</item>
///   <item><c>octave-marker</c>: <c>'</c> = up an octave from C3 anchor,
///     <c>,</c> = down. <c>c</c> = C3, <c>c'</c> = C4, <c>c,</c> = C2,
///     <c>c''</c> = C5, etc.</item>
/// </list>
///
/// Durations map LilyPond's reciprocal convention: <c>1</c> = whole,
/// <c>2</c> = half, <c>4</c> = quarter, <c>8</c> = eighth, <c>16</c> =
/// sixteenth, <c>32</c> = thirty-second. Dotted notes append <c>.</c>.
/// </summary>
public static class LilyPondPitch
{
    /// <summary>
    /// Map Flow (NoteName, Alteration, Octave) → LilyPond pitch string.
    /// Charitable for out-of-range inputs (returns empty string for
    /// unrecognized letters, ignores alterations outside ±2 per
    /// D-v1.5-05).
    /// </summary>
    public static string ToLilyPondPitch(char noteName, int alteration, int octave)
    {
        char upper = char.ToUpperInvariant(noteName);
        // Defensive: only A..G accepted (charitable for anything else)
        if (upper < 'A' || upper > 'G') return string.Empty;
        string letter = char.ToLowerInvariant(upper).ToString();
        string accidental = alteration switch
        {
            +2 => "isis",
            +1 => "is",
            0  => string.Empty,
            -1 => "es",
            -2 => "eses",
            _  => string.Empty,  // charitable per D-v1.5-05
        };
        int relativeToC3 = octave - 3;
        string octaveMarker = relativeToC3 switch
        {
            > 0 => new string('\'', relativeToC3),
            < 0 => new string(',', -relativeToC3),
            _   => string.Empty,
        };
        return $"{letter}{accidental}{octaveMarker}";
    }

    /// <summary>
    /// Map Flow's NoteValue int (0=WHOLE..5=THIRTYSECOND) + dotted flag →
    /// LilyPond duration suffix (e.g. "4", "4.", "8"). Null duration falls
    /// back to "4" (quarter; charitable per D-v1.5-05).
    /// </summary>
    public static string ToLilyPondDuration(int? durationValue, bool isDotted)
    {
        string baseDur = durationValue switch
        {
            0 => "1",
            1 => "2",
            2 => "4",
            3 => "8",
            4 => "16",
            5 => "32",
            6 => "64",
            7 => "128",
            _ => "4",  // null or out-of-range → quarter default
        };
        return isDotted ? baseDur + "." : baseDur;
    }
}
