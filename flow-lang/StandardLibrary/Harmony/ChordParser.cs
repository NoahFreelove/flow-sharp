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
    /// Comprehensive vocabulary covering triads, 6th/7th/9th/11th/13th families,
    /// suspensions, adds, alterations, and power chord. Both `f`/`s` (Flow's
    /// identifier-safe accidentals) and `b`/`#` (common-practice notation) are
    /// listed as aliases for alteration positions — the lexer absorbs both into
    /// chord identifier tokens, so `Cm7b5` and `Cm7f5` resolve identically. Bare
    /// digit qualities (`5`, `6`, `7`, `9`, `11`, `13`) live in the dict but are
    /// gated out at the lexer side by <see cref="IsAllDigits"/> so `C5`/`G7`/`D9`
    /// continue to lex as note literals (project convention from
    /// tests/test_chords.flow:13). The runtime <see cref="TryParseFlexible"/>
    /// entry point bypasses that gate, so `(chord "C5")` is the power chord.
    /// </summary>
    private static readonly Dictionary<string, int[]> QualityIntervals = new()
    {
        // --- Triads ---
        { "", new[] { 0, 4, 7 } },               // bare root = major (used by TryParseFlexible)
        { "maj", new[] { 0, 4, 7 } },
        { "M", new[] { 0, 4, 7 } },
        { "m", new[] { 0, 3, 7 } },
        { "min", new[] { 0, 3, 7 } },
        { "mi", new[] { 0, 3, 7 } },
        { "dim", new[] { 0, 3, 6 } },
        { "aug", new[] { 0, 4, 8 } },

        // --- Power chord (root + 5th, no 3rd) ---
        { "5", new[] { 0, 7 } },

        // --- Sixths ---
        { "6", new[] { 0, 4, 7, 9 } },
        { "m6", new[] { 0, 3, 7, 9 } },
        { "min6", new[] { 0, 3, 7, 9 } },
        { "69", new[] { 0, 4, 7, 9, 14 } },
        { "6/9", new[] { 0, 4, 7, 9, 14 } },
        { "m69", new[] { 0, 3, 7, 9, 14 } },
        { "m6/9", new[] { 0, 3, 7, 9, 14 } },

        // --- Sevenths ---
        { "7", new[] { 0, 4, 7, 10 } },
        { "dom7", new[] { 0, 4, 7, 10 } },
        { "maj7", new[] { 0, 4, 7, 11 } },
        { "M7", new[] { 0, 4, 7, 11 } },
        { "m7", new[] { 0, 3, 7, 10 } },
        { "min7", new[] { 0, 3, 7, 10 } },
        { "mi7", new[] { 0, 3, 7, 10 } },
        { "dim7", new[] { 0, 3, 6, 9 } },
        { "m7f5", new[] { 0, 3, 6, 10 } },         // half-diminished (Flow accidental)
        { "m7b5", new[] { 0, 3, 6, 10 } },         // half-diminished (common notation)
        { "min7f5", new[] { 0, 3, 6, 10 } },
        { "min7b5", new[] { 0, 3, 6, 10 } },
        { "mMaj7", new[] { 0, 3, 7, 11 } },        // minor-major 7
        { "mmaj7", new[] { 0, 3, 7, 11 } },
        { "minMaj7", new[] { 0, 3, 7, 11 } },
        { "minmaj7", new[] { 0, 3, 7, 11 } },

        // --- Sus + 7 ---
        { "sus2", new[] { 0, 2, 7 } },
        { "sus4", new[] { 0, 5, 7 } },
        { "sus", new[] { 0, 5, 7 } },              // bare sus = sus4
        { "7sus4", new[] { 0, 5, 7, 10 } },
        { "7sus", new[] { 0, 5, 7, 10 } },
        { "7sus2", new[] { 0, 2, 7, 10 } },
        { "9sus4", new[] { 0, 5, 7, 10, 14 } },
        { "9sus", new[] { 0, 5, 7, 10, 14 } },
        { "13sus4", new[] { 0, 5, 7, 10, 14, 21 } },

        // --- Ninths (always include the 7) ---
        { "9", new[] { 0, 4, 7, 10, 14 } },
        { "maj9", new[] { 0, 4, 7, 11, 14 } },
        { "M9", new[] { 0, 4, 7, 11, 14 } },
        { "m9", new[] { 0, 3, 7, 10, 14 } },
        { "min9", new[] { 0, 3, 7, 10, 14 } },
        { "mMaj9", new[] { 0, 3, 7, 11, 14 } },
        { "mmaj9", new[] { 0, 3, 7, 11, 14 } },

        // --- Elevenths (always include 7 + 9) ---
        { "11", new[] { 0, 4, 7, 10, 14, 17 } },
        { "maj11", new[] { 0, 4, 7, 11, 14, 17 } },
        { "M11", new[] { 0, 4, 7, 11, 14, 17 } },
        { "m11", new[] { 0, 3, 7, 10, 14, 17 } },
        { "min11", new[] { 0, 3, 7, 10, 14, 17 } },

        // --- Thirteenths (always include 7 + 9 + 11) ---
        { "13", new[] { 0, 4, 7, 10, 14, 17, 21 } },
        { "maj13", new[] { 0, 4, 7, 11, 14, 17, 21 } },
        { "M13", new[] { 0, 4, 7, 11, 14, 17, 21 } },
        { "m13", new[] { 0, 3, 7, 10, 14, 17, 21 } },
        { "min13", new[] { 0, 3, 7, 10, 14, 17, 21 } },

        // --- Adds (triad + degree, no 7th) ---
        { "add2", new[] { 0, 2, 4, 7 } },
        { "add4", new[] { 0, 4, 5, 7 } },
        { "add6", new[] { 0, 4, 7, 9 } },
        { "add9", new[] { 0, 4, 7, 14 } },
        { "add11", new[] { 0, 4, 7, 17 } },
        { "add13", new[] { 0, 4, 7, 21 } },
        { "madd9", new[] { 0, 3, 7, 14 } },
        { "madd11", new[] { 0, 3, 7, 17 } },
        { "minadd9", new[] { 0, 3, 7, 14 } },

        // --- Altered sevenths (b5/#5/b9/#9/#11/b13) — both Flow and common notation ---
        { "7f5", new[] { 0, 4, 6, 10 } },
        { "7b5", new[] { 0, 4, 6, 10 } },
        { "7s5", new[] { 0, 4, 8, 10 } },
        { "7#5", new[] { 0, 4, 8, 10 } },
        { "7f9", new[] { 0, 4, 7, 10, 13 } },
        { "7b9", new[] { 0, 4, 7, 10, 13 } },
        { "7s9", new[] { 0, 4, 7, 10, 15 } },
        { "7#9", new[] { 0, 4, 7, 10, 15 } },
        { "7s11", new[] { 0, 4, 7, 10, 14, 18 } },
        { "7#11", new[] { 0, 4, 7, 10, 14, 18 } },
        { "7f13", new[] { 0, 4, 7, 10, 14, 17, 20 } },
        { "7b13", new[] { 0, 4, 7, 10, 14, 17, 20 } },

        // --- Altered ninths ---
        { "9f5", new[] { 0, 4, 6, 10, 14 } },
        { "9b5", new[] { 0, 4, 6, 10, 14 } },
        { "9s5", new[] { 0, 4, 8, 10, 14 } },
        { "9#5", new[] { 0, 4, 8, 10, 14 } },
        { "9s11", new[] { 0, 4, 7, 10, 14, 18 } },
        { "9#11", new[] { 0, 4, 7, 10, 14, 18 } },

        // --- Altered thirteenths ---
        { "13f9", new[] { 0, 4, 7, 10, 13, 17, 21 } },
        { "13b9", new[] { 0, 4, 7, 10, 13, 17, 21 } },
        { "13s9", new[] { 0, 4, 7, 10, 15, 17, 21 } },
        { "13#9", new[] { 0, 4, 7, 10, 15, 17, 21 } },
        { "13s11", new[] { 0, 4, 7, 10, 14, 18, 21 } },
        { "13#11", new[] { 0, 4, 7, 10, 14, 18, 21 } },

        // --- Altered maj7 / maj9 ---
        { "maj7f5", new[] { 0, 4, 6, 11 } },
        { "maj7b5", new[] { 0, 4, 6, 11 } },
        { "maj7s5", new[] { 0, 4, 8, 11 } },
        { "maj7#5", new[] { 0, 4, 8, 11 } },
        { "maj7s11", new[] { 0, 4, 7, 11, 18 } },
        { "maj7#11", new[] { 0, 4, 7, 11, 18 } },
        { "maj9s11", new[] { 0, 4, 7, 11, 14, 18 } },
        { "maj9#11", new[] { 0, 4, 7, 11, 14, 18 } },

        // --- Altered minor sevenths/ninths ---
        { "m7f9", new[] { 0, 3, 7, 10, 13 } },
        { "m7b9", new[] { 0, 3, 7, 10, 13 } },
        { "m9f5", new[] { 0, 3, 6, 10, 14 } },
        { "m9b5", new[] { 0, 3, 6, 10, 14 } },
        { "m11f5", new[] { 0, 3, 6, 10, 14, 17 } },
        { "m11b5", new[] { 0, 3, 6, 10, 14, 17 } },
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
            && QualityIntervals.ContainsKey(qualityNoAcc)
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
            if (QualityIntervals.ContainsKey(qualityWithAcc) && !IsAllDigits(qualityWithAcc))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns true iff <paramref name="s"/> is non-empty and every char is a digit 0-9.
    /// Used by IsChordSymbol to reject ambiguous bare-digit quality suffixes that
    /// collide with note octaves (e.g., "6" in "D6", "7" in "G7").
    /// </summary>
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

    /// <summary>
    /// Runtime entry point for the <c>(chord String)</c> builtin. Charitable
    /// over the strict lexer-form <see cref="TryParse"/>: accepts both Flow's
    /// identifier-safe accidentals (<c>s</c>/<c>f</c>) and common-practice
    /// notation (<c>#</c>/<c>b</c>) for the ROOT (the alterations are already
    /// dual-listed in <see cref="QualityIntervals"/>), supports slash-bass
    /// suffixes like <c>C/G</c>, and tolerates bare-digit qualities like
    /// <c>C5</c> / <c>G7</c> / <c>D9</c> / <c>C13</c> that the lexer
    /// intentionally routes to note literals (the runtime caller is opting
    /// in to chord interpretation by name).
    ///
    /// Slash bass: when present, the bass note is prepended to the produced
    /// <see cref="ChordData.NoteNames"/> one octave below the chord root.
    /// </summary>
    public static bool TryParseFlexible(string text, out ChordData? chord)
    {
        chord = null;
        if (string.IsNullOrEmpty(text)) return false;

        // 1. Strip slash bass at end (e.g., "C/G", "Am/E"). The slash only counts as
        //    a bass-note delimiter when the suffix STARTS WITH A-G — otherwise the slash
        //    belongs to the quality itself (e.g., "C6/9" is the major-6/9 chord, not a
        //    "C6 over 9" slash chord). This guard is what keeps the dictionary entries
        //    for "6/9" and "m6/9" from being silently shadowed by the slash parser.
        string? bassToken = null;
        int slashIdx = text.IndexOf('/');
        if (slashIdx > 0 && slashIdx < text.Length - 1)
        {
            char afterSlash = text[slashIdx + 1];
            if (afterSlash >= 'A' && afterSlash <= 'G')
            {
                bassToken = text[(slashIdx + 1)..];
                text = text[..slashIdx];
            }
        }

        // 2. Bare letter root = major triad (e.g., "(chord \"C\")" → C major).
        //    Strict TryParse rejects length-1 input; handle it here.
        if (text.Length == 1 && text[0] >= 'A' && text[0] <= 'G')
        {
            text = text + "maj";
        }

        // 3. Normalize the ROOT accidental (b/# → f/s) so common-practice
        //    spellings round-trip. Alterations inside the quality already
        //    have dual-form entries in QualityIntervals, so they need no
        //    special handling here.
        string normalized = NormalizeRootAccidental(text);

        if (!TryParse(normalized, out chord) || chord == null)
            return false;

        // 4. Apply slash-bass: prepend the bass note one octave below root.
        if (bassToken != null)
        {
            string normalizedBass = NormalizeRootAccidental(bassToken);
            // Bass token can be a 1- or 2-char root (e.g., "G", "Bf", "F#" → "Fs").
            string bassRoot;
            if (normalizedBass.Length == 1 && normalizedBass[0] >= 'A' && normalizedBass[0] <= 'G')
                bassRoot = normalizedBass;
            else if (normalizedBass.Length == 2 && (normalizedBass[1] == 's' || normalizedBass[1] == 'f'))
                bassRoot = normalizedBass;
            else
                return true; // unparseable bass — keep the chord, drop the slash silently (charitable)

            if (!NoteToSemitone.TryGetValue(bassRoot, out int bassSemitone))
                return true;

            int bassOctave = chord.Octave - 1;
            string bassDisplay = FormatBassNote(bassSemitone, bassOctave);

            var withBass = new string[chord.NoteNames.Length + 1];
            withBass[0] = bassDisplay;
            Array.Copy(chord.NoteNames, 0, withBass, 1, chord.NoteNames.Length);
            chord = new ChordData(chord.Root, chord.Quality, chord.Octave, withBass);
        }

        return true;
    }

    /// <summary>
    /// Replaces a leading <c>b</c>/<c>#</c> root accidental with the canonical
    /// <c>f</c>/<c>s</c>. Only touches position 1 (immediately after the root
    /// letter) — alterations later in the symbol are dual-listed in the
    /// dictionary and need no normalization.
    /// </summary>
    private static string NormalizeRootAccidental(string text)
    {
        if (text.Length < 2) return text;
        char acc = text[1];
        if (acc == 'b') return text[0] + "f" + text[2..];
        if (acc == '#') return text[0] + "s" + text[2..];
        return text;
    }

    /// <summary>
    /// Formats a bass note for ChordData.NoteNames using the same display
    /// convention as <see cref="ExpandIntervals"/>: natural notes as
    /// <c>"X{octave}"</c>, sharp notes as <c>"X{octave}+"</c>.
    /// </summary>
    private static string FormatBassNote(int rootSemitone, int octave)
    {
        int idx = rootSemitone % 12;
        if (idx < 0) idx += 12;
        string name = ChromaticNotes[idx];
        return name.Length == 1 ? $"{name}{octave}" : $"{name[0]}{octave}+";
    }
}
