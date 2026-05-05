using FlowLang.StandardLibrary.Audio.Tuning;

namespace FlowLsp.Diagnostics;

/// <summary>
/// Phase 24 Plan 24-02 (D-04 + D-05): closed-set diatonic-spelling lookup
/// for the 17 root spellings × 7 church modes = 119 entries that
/// <c>ScaleDatabase.TryParseKeyWithMode</c> accepts. Spelling-aware (D-01):
/// Cmajor's set is <c>{C, D, E, F, G, A, B}</c> — does NOT include E# or Gb
/// even though pitch-class 5 (F) IS diatonic. The analyzer uses
/// letter+accidental membership, not pitch-class.
///
/// Hardcoded literal data per Pattern 2 in 24-RESEARCH.md — beats a
/// circle-of-fifths algorithm at this size. Mirrors the TuningTables.cs:60-188
/// hardcoded-table precedent.
///
/// "Private to flow-lsp" per D-04 (zero flow-lang touch beyond the one
/// PragmaRegistry line). Class is <c>public static</c> because flow-lsp.csproj
/// has no InternalsVisibleTo for flow-lang.Tests.
/// </summary>
public static class DiatonicSpellings
{
    private static readonly Dictionary<(string Root, Mode Mode), string[]> Map = new()
    {
        // ── C ──
        // C major: C D E F G A B
        [("C",      Mode.Major)]      = new[] { "C", "D", "E",  "F",  "G",  "A",  "B"  },
        // C minor (natural): C D Eb F G Ab Bb
        [("C",      Mode.Minor)]      = new[] { "C", "D", "Eb", "F",  "G",  "Ab", "Bb" },
        [("C",      Mode.Dorian)]     = new[] { "C", "D", "Eb", "F",  "G",  "A",  "Bb" },
        [("C",      Mode.Phrygian)]   = new[] { "C", "Db","Eb", "F",  "G",  "Ab", "Bb" },
        [("C",      Mode.Lydian)]     = new[] { "C", "D", "E",  "F#", "G",  "A",  "B"  },
        [("C",      Mode.Mixolydian)] = new[] { "C", "D", "E",  "F",  "G",  "A",  "Bb" },
        [("C",      Mode.Locrian)]    = new[] { "C", "Db","Eb", "F",  "Gb", "Ab", "Bb" },

        // ── Csharp ──
        // C# major: C# D# E# F# G# A# B#
        [("Csharp", Mode.Major)]      = new[] { "C#", "D#", "E#", "F#", "G#", "A#", "B#" },
        [("Csharp", Mode.Minor)]      = new[] { "C#", "D#", "E",  "F#", "G#", "A",  "B"  },
        [("Csharp", Mode.Dorian)]     = new[] { "C#", "D#", "E",  "F#", "G#", "A#", "B"  },
        [("Csharp", Mode.Phrygian)]   = new[] { "C#", "D",  "E",  "F#", "G#", "A",  "B"  },
        [("Csharp", Mode.Lydian)]     = new[] { "C#", "D#", "E#", "F##","G#", "A#", "B#" },
        [("Csharp", Mode.Mixolydian)] = new[] { "C#", "D#", "E#", "F#", "G#", "A#", "B"  },
        [("Csharp", Mode.Locrian)]    = new[] { "C#", "D",  "E",  "F#", "G",  "A",  "B"  },

        // ── Db ──
        [("Db",     Mode.Major)]      = new[] { "Db", "Eb", "F",  "Gb", "Ab", "Bb", "C"  },
        [("Db",     Mode.Minor)]      = new[] { "Db", "Eb", "Fb", "Gb", "Ab", "Bbb","Cb" },
        [("Db",     Mode.Dorian)]     = new[] { "Db", "Eb", "Fb", "Gb", "Ab", "Bb", "Cb" },
        [("Db",     Mode.Phrygian)]   = new[] { "Db", "Ebb","Fb", "Gb", "Ab", "Bbb","Cb" },
        [("Db",     Mode.Lydian)]     = new[] { "Db", "Eb", "F",  "G",  "Ab", "Bb", "C"  },
        [("Db",     Mode.Mixolydian)] = new[] { "Db", "Eb", "F",  "Gb", "Ab", "Bb", "Cb" },
        [("Db",     Mode.Locrian)]    = new[] { "Db", "Ebb","Fb", "Gb", "Abb","Bbb","Cb" },

        // ── D ──
        [("D",      Mode.Major)]      = new[] { "D", "E", "F#", "G",  "A",  "B",  "C#" },
        [("D",      Mode.Minor)]      = new[] { "D", "E", "F",  "G",  "A",  "Bb", "C"  },
        [("D",      Mode.Dorian)]     = new[] { "D", "E", "F",  "G",  "A",  "B",  "C"  },
        [("D",      Mode.Phrygian)]   = new[] { "D", "Eb","F",  "G",  "A",  "Bb", "C"  },
        [("D",      Mode.Lydian)]     = new[] { "D", "E", "F#", "G#", "A",  "B",  "C#" },
        [("D",      Mode.Mixolydian)] = new[] { "D", "E", "F#", "G",  "A",  "B",  "C"  },
        [("D",      Mode.Locrian)]    = new[] { "D", "Eb","F",  "G",  "Ab", "Bb", "C"  },

        // ── Dsharp ──
        [("Dsharp", Mode.Major)]      = new[] { "D#", "E#", "F##","G#", "A#", "B#", "C##" },
        [("Dsharp", Mode.Minor)]      = new[] { "D#", "E#", "F#", "G#", "A#", "B",  "C#"  },
        [("Dsharp", Mode.Dorian)]     = new[] { "D#", "E#", "F#", "G#", "A#", "B#", "C#"  },
        [("Dsharp", Mode.Phrygian)]   = new[] { "D#", "E",  "F#", "G#", "A#", "B",  "C#"  },
        [("Dsharp", Mode.Lydian)]     = new[] { "D#", "E#", "F##","G##","A#", "B#", "C##" },
        [("Dsharp", Mode.Mixolydian)] = new[] { "D#", "E#", "F##","G#", "A#", "B#", "C#"  },
        [("Dsharp", Mode.Locrian)]    = new[] { "D#", "E",  "F#", "G#", "A",  "B",  "C#"  },

        // ── Eb ──
        [("Eb",     Mode.Major)]      = new[] { "Eb", "F",  "G",  "Ab", "Bb", "C",  "D"  },
        [("Eb",     Mode.Minor)]      = new[] { "Eb", "F",  "Gb", "Ab", "Bb", "Cb", "Db" },
        [("Eb",     Mode.Dorian)]     = new[] { "Eb", "F",  "Gb", "Ab", "Bb", "C",  "Db" },
        [("Eb",     Mode.Phrygian)]   = new[] { "Eb", "Fb", "Gb", "Ab", "Bb", "Cb", "Db" },
        [("Eb",     Mode.Lydian)]     = new[] { "Eb", "F",  "G",  "A",  "Bb", "C",  "D"  },
        [("Eb",     Mode.Mixolydian)] = new[] { "Eb", "F",  "G",  "Ab", "Bb", "C",  "Db" },
        [("Eb",     Mode.Locrian)]    = new[] { "Eb", "Fb", "Gb", "Ab", "Bbb","Cb", "Db" },

        // ── E ──
        [("E",      Mode.Major)]      = new[] { "E", "F#", "G#", "A",  "B",  "C#", "D#" },
        [("E",      Mode.Minor)]      = new[] { "E", "F#", "G",  "A",  "B",  "C",  "D"  },
        [("E",      Mode.Dorian)]     = new[] { "E", "F#", "G",  "A",  "B",  "C#", "D"  },
        [("E",      Mode.Phrygian)]   = new[] { "E", "F",  "G",  "A",  "B",  "C",  "D"  },
        [("E",      Mode.Lydian)]     = new[] { "E", "F#", "G#", "A#", "B",  "C#", "D#" },
        [("E",      Mode.Mixolydian)] = new[] { "E", "F#", "G#", "A",  "B",  "C#", "D"  },
        [("E",      Mode.Locrian)]    = new[] { "E", "F",  "G",  "A",  "Bb", "C",  "D"  },

        // ── F ──
        [("F",      Mode.Major)]      = new[] { "F", "G",  "A",  "Bb", "C",  "D",  "E"  },
        [("F",      Mode.Minor)]      = new[] { "F", "G",  "Ab", "Bb", "C",  "Db", "Eb" },
        [("F",      Mode.Dorian)]     = new[] { "F", "G",  "Ab", "Bb", "C",  "D",  "Eb" },
        [("F",      Mode.Phrygian)]   = new[] { "F", "Gb", "Ab", "Bb", "C",  "Db", "Eb" },
        [("F",      Mode.Lydian)]     = new[] { "F", "G",  "A",  "B",  "C",  "D",  "E"  },
        [("F",      Mode.Mixolydian)] = new[] { "F", "G",  "A",  "Bb", "C",  "D",  "Eb" },
        [("F",      Mode.Locrian)]    = new[] { "F", "Gb", "Ab", "Bb", "Cb", "Db", "Eb" },

        // ── Fsharp ──
        [("Fsharp", Mode.Major)]      = new[] { "F#", "G#", "A#", "B",  "C#", "D#", "E#" },
        [("Fsharp", Mode.Minor)]      = new[] { "F#", "G#", "A",  "B",  "C#", "D",  "E"  },
        [("Fsharp", Mode.Dorian)]     = new[] { "F#", "G#", "A",  "B",  "C#", "D#", "E"  },
        [("Fsharp", Mode.Phrygian)]   = new[] { "F#", "G",  "A",  "B",  "C#", "D",  "E"  },
        [("Fsharp", Mode.Lydian)]     = new[] { "F#", "G#", "A#", "B#", "C#", "D#", "E#" },
        [("Fsharp", Mode.Mixolydian)] = new[] { "F#", "G#", "A#", "B",  "C#", "D#", "E"  },
        [("Fsharp", Mode.Locrian)]    = new[] { "F#", "G",  "A",  "B",  "C",  "D",  "E"  },

        // ── Gb ──
        [("Gb",     Mode.Major)]      = new[] { "Gb", "Ab", "Bb", "Cb", "Db", "Eb", "F"  },
        [("Gb",     Mode.Minor)]      = new[] { "Gb", "Ab", "Bbb","Cb", "Db", "Ebb","Fb" },
        [("Gb",     Mode.Dorian)]     = new[] { "Gb", "Ab", "Bbb","Cb", "Db", "Eb", "Fb" },
        [("Gb",     Mode.Phrygian)]   = new[] { "Gb", "Abb","Bbb","Cb", "Db", "Ebb","Fb" },
        [("Gb",     Mode.Lydian)]     = new[] { "Gb", "Ab", "Bb", "C",  "Db", "Eb", "F"  },
        [("Gb",     Mode.Mixolydian)] = new[] { "Gb", "Ab", "Bb", "Cb", "Db", "Eb", "Fb" },
        [("Gb",     Mode.Locrian)]    = new[] { "Gb", "Abb","Bbb","Cb", "Dbb","Ebb","Fb" },

        // ── G ──
        [("G",      Mode.Major)]      = new[] { "G", "A",  "B",  "C",  "D",  "E",  "F#" },
        [("G",      Mode.Minor)]      = new[] { "G", "A",  "Bb", "C",  "D",  "Eb", "F"  },
        [("G",      Mode.Dorian)]     = new[] { "G", "A",  "Bb", "C",  "D",  "E",  "F"  },
        [("G",      Mode.Phrygian)]   = new[] { "G", "Ab", "Bb", "C",  "D",  "Eb", "F"  },
        [("G",      Mode.Lydian)]     = new[] { "G", "A",  "B",  "C#", "D",  "E",  "F#" },
        [("G",      Mode.Mixolydian)] = new[] { "G", "A",  "B",  "C",  "D",  "E",  "F"  },
        [("G",      Mode.Locrian)]    = new[] { "G", "Ab", "Bb", "C",  "Db", "Eb", "F"  },

        // ── Gsharp ──
        [("Gsharp", Mode.Major)]      = new[] { "G#", "A#", "B#", "C#", "D#", "E#", "F##" },
        [("Gsharp", Mode.Minor)]      = new[] { "G#", "A#", "B",  "C#", "D#", "E",  "F#"  },
        [("Gsharp", Mode.Dorian)]     = new[] { "G#", "A#", "B",  "C#", "D#", "E#", "F#"  },
        [("Gsharp", Mode.Phrygian)]   = new[] { "G#", "A",  "B",  "C#", "D#", "E",  "F#"  },
        [("Gsharp", Mode.Lydian)]     = new[] { "G#", "A#", "B#", "C##","D#", "E#", "F##" },
        [("Gsharp", Mode.Mixolydian)] = new[] { "G#", "A#", "B#", "C#", "D#", "E#", "F#"  },
        [("Gsharp", Mode.Locrian)]    = new[] { "G#", "A",  "B",  "C#", "D",  "E",  "F#"  },

        // ── Ab ──
        [("Ab",     Mode.Major)]      = new[] { "Ab", "Bb", "C",  "Db", "Eb", "F",  "G"  },
        [("Ab",     Mode.Minor)]      = new[] { "Ab", "Bb", "Cb", "Db", "Eb", "Fb", "Gb" },
        [("Ab",     Mode.Dorian)]     = new[] { "Ab", "Bb", "Cb", "Db", "Eb", "F",  "Gb" },
        [("Ab",     Mode.Phrygian)]   = new[] { "Ab", "Bbb","Cb", "Db", "Eb", "Fb", "Gb" },
        [("Ab",     Mode.Lydian)]     = new[] { "Ab", "Bb", "C",  "D",  "Eb", "F",  "G"  },
        [("Ab",     Mode.Mixolydian)] = new[] { "Ab", "Bb", "C",  "Db", "Eb", "F",  "Gb" },
        [("Ab",     Mode.Locrian)]    = new[] { "Ab", "Bbb","Cb", "Db", "Ebb","Fb", "Gb" },

        // ── A ──
        [("A",      Mode.Major)]      = new[] { "A", "B",  "C#", "D",  "E",  "F#", "G#" },
        [("A",      Mode.Minor)]      = new[] { "A", "B",  "C",  "D",  "E",  "F",  "G"  },
        [("A",      Mode.Dorian)]     = new[] { "A", "B",  "C",  "D",  "E",  "F#", "G"  },
        [("A",      Mode.Phrygian)]   = new[] { "A", "Bb", "C",  "D",  "E",  "F",  "G"  },
        [("A",      Mode.Lydian)]     = new[] { "A", "B",  "C#", "D#", "E",  "F#", "G#" },
        [("A",      Mode.Mixolydian)] = new[] { "A", "B",  "C#", "D",  "E",  "F#", "G"  },
        [("A",      Mode.Locrian)]    = new[] { "A", "Bb", "C",  "D",  "Eb", "F",  "G"  },

        // ── Asharp ──
        [("Asharp", Mode.Major)]      = new[] { "A#", "B#", "C##","D#", "E#", "F##","G##" },
        [("Asharp", Mode.Minor)]      = new[] { "A#", "B#", "C#", "D#", "E#", "F#", "G#"  },
        [("Asharp", Mode.Dorian)]     = new[] { "A#", "B#", "C#", "D#", "E#", "F##","G#"  },
        [("Asharp", Mode.Phrygian)]   = new[] { "A#", "B",  "C#", "D#", "E#", "F#", "G#"  },
        [("Asharp", Mode.Lydian)]     = new[] { "A#", "B#", "C##","D##","E#", "F##","G##" },
        [("Asharp", Mode.Mixolydian)] = new[] { "A#", "B#", "C##","D#", "E#", "F##","G#"  },
        [("Asharp", Mode.Locrian)]    = new[] { "A#", "B",  "C#", "D#", "E",  "F#", "G#"  },

        // ── Bb ──
        [("Bb",     Mode.Major)]      = new[] { "Bb", "C",  "D",  "Eb", "F",  "G",  "A"  },
        [("Bb",     Mode.Minor)]      = new[] { "Bb", "C",  "Db", "Eb", "F",  "Gb", "Ab" },
        [("Bb",     Mode.Dorian)]     = new[] { "Bb", "C",  "Db", "Eb", "F",  "G",  "Ab" },
        [("Bb",     Mode.Phrygian)]   = new[] { "Bb", "Cb", "Db", "Eb", "F",  "Gb", "Ab" },
        [("Bb",     Mode.Lydian)]     = new[] { "Bb", "C",  "D",  "E",  "F",  "G",  "A"  },
        [("Bb",     Mode.Mixolydian)] = new[] { "Bb", "C",  "D",  "Eb", "F",  "G",  "Ab" },
        [("Bb",     Mode.Locrian)]    = new[] { "Bb", "Cb", "Db", "Eb", "Fb", "Gb", "Ab" },

        // ── B ──
        [("B",      Mode.Major)]      = new[] { "B", "C#", "D#", "E",  "F#", "G#", "A#" },
        [("B",      Mode.Minor)]      = new[] { "B", "C#", "D",  "E",  "F#", "G",  "A"  },
        [("B",      Mode.Dorian)]     = new[] { "B", "C#", "D",  "E",  "F#", "G#", "A"  },
        [("B",      Mode.Phrygian)]   = new[] { "B", "C",  "D",  "E",  "F#", "G",  "A"  },
        [("B",      Mode.Lydian)]     = new[] { "B", "C#", "D#", "E#", "F#", "G#", "A#" },
        [("B",      Mode.Mixolydian)] = new[] { "B", "C#", "D#", "E",  "F#", "G#", "A"  },
        [("B",      Mode.Locrian)]    = new[] { "B", "C",  "D",  "E",  "F",  "G",  "A"  },
    };

    /// <summary>
    /// D-04 + D-05: returns the 7 letter+accidental strings that are diatonic
    /// in the given (root, mode). Spelling-aware (D-01): in C major,
    /// <c>"E#"</c> is NOT in the set even though pitch-class 5 (= F natural) IS.
    /// Returns <c>null</c> when the (root, mode) pair is not in the closed
    /// 119-entry set — analyzer treats null as "silent fail-open" per D-22.
    /// </summary>
    public static IReadOnlySet<string>? GetDiatonicSpellings(string root, Mode mode) =>
        Map.TryGetValue((root, mode), out var arr)
            ? new HashSet<string>(arr, StringComparer.Ordinal)
            : null;

    /// <summary>
    /// Total entries in the closed-set map. Pinned by
    /// <c>DiatonicSpellingsFacts.Map_HasExactly119Entries</c> at 119.
    /// </summary>
    public static int EntryCount => Map.Count;
}
