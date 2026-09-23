namespace FlowLang.Syntax;

/// <summary>
/// Lexical rules for chord symbols (<c>Cmaj7</c>, <c>Dm</c>, <c>Csmaj7</c>). Part of Flow's
/// one grammar: the lexer uses <see cref="IsChordSymbol"/> to decide what is a chord
/// literal. The music layer's <c>ChordParser</c> maps each quality to intervals; a test
/// keeps its table and <see cref="Qualities"/> identical.
/// </summary>
public static class ChordSyntax
{
    /// <summary>Every chord-quality suffix the grammar recognizes ("" is a bare major root).</summary>
    public static readonly IReadOnlySet<string> Qualities = new HashSet<string>(StringComparer.Ordinal)
    {
        "",
        "maj",
        "M",
        "m",
        "min",
        "mi",
        "dim",
        "aug",
        "5",
        "6",
        "m6",
        "min6",
        "69",
        "6/9",
        "m69",
        "m6/9",
        "7",
        "dom7",
        "maj7",
        "M7",
        "m7",
        "min7",
        "mi7",
        "dim7",
        "m7f5",
        "m7b5",
        "min7f5",
        "min7b5",
        "mMaj7",
        "mmaj7",
        "minMaj7",
        "minmaj7",
        "sus2",
        "sus4",
        "sus",
        "7sus4",
        "7sus",
        "7sus2",
        "9sus4",
        "9sus",
        "13sus4",
        "9",
        "maj9",
        "M9",
        "m9",
        "min9",
        "mMaj9",
        "mmaj9",
        "11",
        "maj11",
        "M11",
        "m11",
        "min11",
        "13",
        "maj13",
        "M13",
        "m13",
        "min13",
        "add2",
        "add4",
        "add6",
        "add9",
        "add11",
        "add13",
        "madd9",
        "madd11",
        "minadd9",
        "7f5",
        "7b5",
        "7s5",
        "7#5",
        "7f9",
        "7b9",
        "7s9",
        "7#9",
        "7s11",
        "7#11",
        "7f13",
        "7b13",
        "9f5",
        "9b5",
        "9s5",
        "9#5",
        "9s11",
        "9#11",
        "13f9",
        "13b9",
        "13s9",
        "13#9",
        "13s11",
        "13#11",
        "maj7f5",
        "maj7b5",
        "maj7s5",
        "maj7#5",
        "maj7s11",
        "maj7#11",
        "maj9s11",
        "maj9#11",
        "m7f9",
        "m7b9",
        "m9f5",
        "m9b5",
        "m11f5",
        "m11b5",
    };

    /// <summary>
    /// Checks whether a text token is a chord symbol (for lexer use).
    /// Must be at least 2 chars, start with A-G, have optional accidental (s/f),
    /// and remaining text must match a known quality.
    /// Note: The lexer calls TryParseNote first, so anything reaching this method
    /// has already failed note parsing (e.g., C4 is caught as a note before this runs).
    ///
    /// note-vs-chord-lexer fix (2026-05-02): on the no-accidental branch, reject
    /// quality suffixes that consist of digits only (e.g., "D6", "G7", "D9"). These
    /// shapes are ambiguous with note literals (D in octave 6, G in octave 7, etc.),
    /// and the project's documented convention (tests/test_chords.flow:13) already
    /// assigns them to notes ("G7 is parsed as note G at octave 7, use dom7 for chord").
    /// Falling through here lets the lexer's TryParseNote pick them up as NoteLiteral.
    /// The with-accidental branch (Cs6, Df7) is unchanged since "Cs6" cannot be a
    /// valid note (NoteType.Parse rejects 's' as a non-alteration character) and
    /// keeping it as a chord preserves the existing chord-symbol grammar surface.
    /// ChordParser.TryParse (called from ScaleDatabase.ResolveRomanNumeral with
    /// symbols like "D7" derived from V7 numerals) is unchanged — only the
    /// lexer-side recognizer narrows.
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
        if (qualityNoAcc.Length > 0
            && Qualities.Contains(qualityNoAcc)
            && !IsAllDigits(qualityNoAcc))
        {
            return true;
        }

        // Try with accidental (e.g., "Csmaj7" = Cs + maj7)
        if (text.Length >= 2 && (text[1] == 's' || text[1] == 'f'))
        {
            string qualityWithAcc = text[2..];
            if (qualityWithAcc.Length == 0)
            {
                // "Cs", "Df" — root with accidental, no quality = major chord
                return true;
            }
            // Reject bare-digit qualities here too — same project convention as the
            // no-accidental branch above: "Cs5" / "Df7" must stay as note literals
            // (C-sharp octave 5, D-flat octave 7), not power-chord / dom7 chords.
            // Without this gate, the expanded QualityIntervals dict (which now contains
            // "5", "6", "7", "9", "11", "13" entries to support runtime `(chord "C5")`)
            // would silently re-route every accidented note literal into a chord token.
            if (Qualities.Contains(qualityWithAcc) && !IsAllDigits(qualityWithAcc))
                return true;
        }

        return false;
    }

    private static bool IsAllDigits(string s)
    {
        if (s.Length == 0) return false;
        foreach (char c in s)
        {
            if (c < '0' || c > '9') return false;
        }
        return true;
    }

    /// <summary>
    /// The note-stream duration-suffix letters recognized by
    /// Parser.NoteStream.cs <c>TryParseDurationSuffix</c>
    /// (w=whole, h=half, q=quarter, e=eighth, s=sixteenth, t=thirty-second,
    /// x=64th, y=128th). Single-letter forms only — a chord+duration fusion
    /// token carries exactly one trailing duration letter (the optional dot
    /// <c>.</c> and tie <c>~</c> lex as their own tokens, so they are NOT part
    /// of the identifier and never reach this method).
    /// </summary>
    private static bool IsDurationSuffixLetter(char ch) =>
        ch is 'w' or 'h' or 'q' or 'e' or 's' or 't' or 'x' or 'y';

    /// <summary>
    /// chord-duration-fusion (feature-addition 0615 #5): tries to read
    /// <paramref name="text"/> as a CHORD NAME immediately followed by a single
    /// trailing duration letter — e.g. <c>Cmaj7q</c>, <c>Dm7e</c>,
    /// <c>F#dim7h</c>, <c>Bb7w</c> — mirroring the existing note+duration fusion
    /// (<c>C4q</c>). The lexer calls this AFTER whole-token chord/note checks
    /// fail and BEFORE the note+duration split, so a recognized chord+duration
    /// wins over the exotic "<c>Bb7</c> = B-flat octave-7 note" reading per the
    /// CLAUDE.md "Chord literals" doc (which lists <c>Bb7</c> as a chord).
    ///
    /// <para>On success, <paramref name="chordCore"/> is the canonical chord
    /// symbol (root accidentals normalized <c>b</c>/<c>#</c> → <c>f</c>/<c>s</c>
    /// so the parser/ChordParser.TryParse path consumes it unchanged) and
    /// <paramref name="durationLetter"/> is the single suffix letter.</para>
    ///
    /// <para>Conservatism (so plain notes are NEVER stolen):</para>
    /// <list type="bullet">
    ///   <item>The chord part must carry a NON-EMPTY known quality — a bare root
    ///   (<c>Bbq</c> → <c>Bb</c>, empty quality) falls through to the plain-note
    ///   path (B-flat quarter), not a B-flat major chord.</item>
    ///   <item>A BARE-DIGIT quality (<c>5</c>/<c>6</c>/<c>7</c>/<c>9</c>/<c>11</c>/
    ///   <c>13</c>) is ALWAYS rejected — it is structurally indistinguishable from
    ///   a note octave (<c>F#5</c> = F-sharp octave 5, <c>G7</c> = G octave 7,
    ///   <c>Cs5</c>/<c>Df7</c> — the documented tests/test_chords.flow +
    ///   <see cref="IsChordSymbol"/> convention). So <c>F#5e</c>/<c>G7q</c>/
    ///   <c>Bb7w</c> stay NOTES; only QUALITIES CONTAINING A LETTER
    ///   (<c>maj7</c>/<c>m7</c>/<c>dim7</c>/<c>sus4</c>/<c>add9</c>/<c>m</c>/…) fuse.</item>
    /// </list>
    /// </summary>
    public static bool TryMatchChordWithDuration(string text, out string chordCore, out char durationLetter)
    {
        chordCore = string.Empty;
        durationLetter = '\0';

        // Need at least root(1) + quality(1) + duration(1) = 3 chars.
        if (text.Length < 3)
            return false;

        char last = text[^1];
        if (!IsDurationSuffixLetter(last))
            return false;

        string core = text[..^1];
        if (core.Length < 2)
            return false;

        char first = core[0];
        if (first < 'A' || first > 'G')
            return false;

        // Normalize a leading b/# root accidental to f/s so both common-practice
        // (Bb7, F#dim7) and Flow-native (Bf7, Fsdim7) spellings resolve. Only the
        // root accidental at position 1 is normalized — quality alterations are
        // already dual-listed (b5/f5 etc.) in QualityIntervals.
        bool hasAccidental = false;
        string norm = core;
        if (core.Length >= 2 && (core[1] == 'b' || core[1] == '#'))
        {
            norm = core[0] + (core[1] == 'b' ? "f" : "s") + core[2..];
            hasAccidental = true;
        }
        else if (core.Length >= 2 && (core[1] == 's' || core[1] == 'f'))
        {
            hasAccidental = true;
        }

        // Split root (letter + optional s/f accidental) from quality.
        int rootLen = hasAccidental ? 2 : 1;
        if (norm.Length <= rootLen)
            return false; // empty quality (e.g. "Bb" → bare flat note, not a chord)
        string quality = norm[rootLen..];

        if (!Qualities.Contains(quality))
            return false;

        // Bare-digit quality (5/6/7/9/11/13) is ALWAYS rejected: it collides with a
        // note octave (F#5 = F-sharp octave 5, G7 = note G octave 7, Cs5/Df7). This
        // mirrors the IsChordSymbol IsAllDigits gate. Only letter-bearing qualities
        // (maj7/m7/dim7/sus4/add9/m/…) are unambiguous enough to fuse.
        if (IsAllDigits(quality))
            return false;

        chordCore = norm;
        durationLetter = last;
        return true;
    }
}
