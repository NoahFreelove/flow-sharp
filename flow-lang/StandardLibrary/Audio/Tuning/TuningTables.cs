namespace FlowLang.StandardLibrary.Audio.Tuning;

/// <summary>
/// Static dictionary of chromatic ratio tables keyed by (TuningSystem, Mode).
/// Ships 14 entries: 7 JI modes + 7 Pythagorean modes per D-03.
/// EqualTemperament has no entry — consumers short-circuit on
/// <c>tuning.System == EqualTemperament</c> before calling <see cref="LookupRatio"/>
/// per RESEARCH Pitfall 6 (byte-identical 12-TET fast path).
///
/// Sources (canonical):
///   - 5-limit JI: Wikipedia "Five-limit tuning" asymmetric chromatic table
///     (https://en.wikipedia.org/wiki/Five-limit_tuning).
///   - 5-limit JI mode shifts: Mudcat "Just Intonation Music Scales" (Olson)
///     (https://mudcat.org/olson/JUSTINT.html).
///   - 3-limit Pythagorean: Wikipedia "Pythagorean tuning" chain-of-fifths from C
///     (https://en.wikipedia.org/wiki/Pythagorean_tuning).
///
/// Chromatic-fallback rule (Pitfall 3): non-diatonic spellings in mode tables use
/// the same 25/24 raised + 16/15 lowered semitone construction as JI Major; for
/// Pythagorean modes, chromatic tones stay on the chain-of-fifths.
/// </summary>
public static class TuningTables
{
    /// <summary>
    /// Closed dictionary of 14 chromatic ratio tables keyed by (TuningSystem, Mode).
    /// Built by the static constructor AFTER all <c>JustIonian</c>...<c>PythLocrian</c>
    /// field initializers have run — populating this dictionary directly at field-init
    /// time would race the per-table field initializers (C# initializes static
    /// readonly fields in textual declaration order; a forward reference would yield
    /// null entries).
    /// </summary>
    public static readonly IReadOnlyDictionary<(TuningSystem, Mode), ChromaticRatioTable> Tables;

    static TuningTables()
    {
        Tables = new Dictionary<(TuningSystem, Mode), ChromaticRatioTable>
        {
            [(TuningSystem.JustIntonation, Mode.Major)]      = JustIonian,
            [(TuningSystem.JustIntonation, Mode.Minor)]      = JustAeolian,
            [(TuningSystem.JustIntonation, Mode.Dorian)]     = JustDorian,
            [(TuningSystem.JustIntonation, Mode.Phrygian)]   = JustPhrygian,
            [(TuningSystem.JustIntonation, Mode.Lydian)]     = JustLydian,
            [(TuningSystem.JustIntonation, Mode.Mixolydian)] = JustMixolydian,
            [(TuningSystem.JustIntonation, Mode.Locrian)]    = JustLocrian,
            [(TuningSystem.Pythagorean,    Mode.Major)]      = PythIonian,
            [(TuningSystem.Pythagorean,    Mode.Minor)]      = PythAeolian,
            [(TuningSystem.Pythagorean,    Mode.Dorian)]     = PythDorian,
            [(TuningSystem.Pythagorean,    Mode.Phrygian)]   = PythPhrygian,
            [(TuningSystem.Pythagorean,    Mode.Lydian)]     = PythLydian,
            [(TuningSystem.Pythagorean,    Mode.Mixolydian)] = PythMixolydian,
            [(TuningSystem.Pythagorean,    Mode.Locrian)]    = PythLocrian,
        };
    }

    /// <summary>
    /// Looks up the (letter, alteration) ratio in the table for (system, mode).
    /// Throws <see cref="KeyNotFoundException"/> when (system, mode) is absent —
    /// the caller is responsible for the EqualTemperament short-circuit per Pitfall 6.
    /// </summary>
    public static double LookupRatio(TuningSystem system, Mode mode, char letter, int alteration)
    {
        if (!Tables.TryGetValue((system, mode), out var table))
            throw new KeyNotFoundException(
                $"TuningTables: no table for ({system}, {mode}). EqualTemperament should " +
                $"short-circuit before calling LookupRatio per Pitfall 6.");
        return table.Lookup(letter, alteration);
    }

    // === JI Mode Tables (Source: Mudcat Olson + Wikipedia asymmetric 5-limit) ===

    /// <summary>JI Ionian (Major). Diatonic: 1, 9/8, 5/4, 4/3, 3/2, 5/3, 15/8.</summary>
    public static readonly ChromaticRatioTable JustIonian = ChromaticRatioTable.Build(
        naturals: new Dictionary<char, double> {
            ['C'] = 1.0,
            ['D'] = 9.0/8.0,
            ['E'] = 5.0/4.0,    // MICR-01 canary
            ['F'] = 4.0/3.0,
            ['G'] = 3.0/2.0,
            ['A'] = 5.0/3.0,
            ['B'] = 15.0/8.0,
        },
        sharps: new Dictionary<char, double> {
            ['C'] = 25.0/24.0,
            ['D'] = 75.0/64.0,   // distinct from Eb=6/5 per D-09
            ['F'] = 25.0/18.0,
            ['G'] = 25.0/16.0,
            ['A'] = 125.0/72.0,
        },
        flats: new Dictionary<char, double> {
            ['D'] = 16.0/15.0,
            ['E'] = 6.0/5.0,     // canonical minor third — distinct from D# per D-09
            ['G'] = 64.0/45.0,
            ['A'] = 8.0/5.0,
            ['B'] = 9.0/5.0,
        });

    /// <summary>JI Aeolian (Natural Minor). 1, 9/8, 6/5, 4/3, 3/2, 8/5, 9/5.</summary>
    public static readonly ChromaticRatioTable JustAeolian = ChromaticRatioTable.Build(
        naturals: new Dictionary<char, double> {
            ['C'] = 1.0, ['D'] = 9.0/8.0, ['E'] = 6.0/5.0, ['F'] = 4.0/3.0,
            ['G'] = 3.0/2.0, ['A'] = 8.0/5.0, ['B'] = 9.0/5.0,
        },
        sharps: new Dictionary<char, double> {
            ['C'] = 25.0/24.0, ['D'] = 75.0/64.0, ['F'] = 25.0/18.0,
            ['G'] = 25.0/16.0, ['A'] = 125.0/72.0,
        },
        flats: new Dictionary<char, double> {
            ['D'] = 16.0/15.0, ['E'] = 6.0/5.0, ['G'] = 64.0/45.0,
            ['A'] = 8.0/5.0, ['B'] = 9.0/5.0,
        });

    /// <summary>JI Dorian. 1, 9/8, 6/5, 4/3, 3/2, 5/3, 9/5 (3rd, 7th lowered from Ionian; 6th major).</summary>
    public static readonly ChromaticRatioTable JustDorian = ChromaticRatioTable.Build(
        naturals: new Dictionary<char, double> {
            ['C'] = 1.0, ['D'] = 9.0/8.0, ['E'] = 6.0/5.0, ['F'] = 4.0/3.0,
            ['G'] = 3.0/2.0, ['A'] = 5.0/3.0, ['B'] = 9.0/5.0,
        },
        sharps: new Dictionary<char, double> {
            ['C'] = 25.0/24.0, ['D'] = 75.0/64.0, ['F'] = 25.0/18.0,
            ['G'] = 25.0/16.0, ['A'] = 125.0/72.0,
        },
        flats: new Dictionary<char, double> {
            ['D'] = 16.0/15.0, ['E'] = 6.0/5.0, ['G'] = 64.0/45.0,
            ['A'] = 8.0/5.0, ['B'] = 9.0/5.0,
        });

    /// <summary>JI Phrygian. 1, 27/25, 6/5, 4/3, 3/2, 8/5, 9/5 (lowered 2, 3, 6, 7).</summary>
    public static readonly ChromaticRatioTable JustPhrygian = ChromaticRatioTable.Build(
        naturals: new Dictionary<char, double> {
            ['C'] = 1.0, ['D'] = 27.0/25.0, ['E'] = 6.0/5.0, ['F'] = 4.0/3.0,
            ['G'] = 3.0/2.0, ['A'] = 8.0/5.0, ['B'] = 9.0/5.0,
        },
        sharps: new Dictionary<char, double> {
            ['C'] = 25.0/24.0, ['D'] = 75.0/64.0, ['F'] = 25.0/18.0,
            ['G'] = 25.0/16.0, ['A'] = 125.0/72.0,
        },
        flats: new Dictionary<char, double> {
            ['D'] = 16.0/15.0, ['E'] = 6.0/5.0, ['G'] = 64.0/45.0,
            ['A'] = 8.0/5.0, ['B'] = 9.0/5.0,
        });

    /// <summary>JI Lydian. 1, 9/8, 5/4, 25/18, 3/2, 5/3, 15/8 (raised 4th).</summary>
    public static readonly ChromaticRatioTable JustLydian = ChromaticRatioTable.Build(
        naturals: new Dictionary<char, double> {
            ['C'] = 1.0, ['D'] = 9.0/8.0, ['E'] = 5.0/4.0, ['F'] = 25.0/18.0,
            ['G'] = 3.0/2.0, ['A'] = 5.0/3.0, ['B'] = 15.0/8.0,
        },
        sharps: new Dictionary<char, double> {
            ['C'] = 25.0/24.0, ['D'] = 75.0/64.0, ['F'] = 25.0/18.0,
            ['G'] = 25.0/16.0, ['A'] = 125.0/72.0,
        },
        flats: new Dictionary<char, double> {
            ['D'] = 16.0/15.0, ['E'] = 6.0/5.0, ['G'] = 64.0/45.0,
            ['A'] = 8.0/5.0, ['B'] = 9.0/5.0,
        });

    /// <summary>JI Mixolydian. 1, 9/8, 5/4, 4/3, 3/2, 5/3, 9/5 (lowered 7th).</summary>
    public static readonly ChromaticRatioTable JustMixolydian = ChromaticRatioTable.Build(
        naturals: new Dictionary<char, double> {
            ['C'] = 1.0, ['D'] = 9.0/8.0, ['E'] = 5.0/4.0, ['F'] = 4.0/3.0,
            ['G'] = 3.0/2.0, ['A'] = 5.0/3.0, ['B'] = 9.0/5.0,
        },
        sharps: new Dictionary<char, double> {
            ['C'] = 25.0/24.0, ['D'] = 75.0/64.0, ['F'] = 25.0/18.0,
            ['G'] = 25.0/16.0, ['A'] = 125.0/72.0,
        },
        flats: new Dictionary<char, double> {
            ['D'] = 16.0/15.0, ['E'] = 6.0/5.0, ['G'] = 64.0/45.0,
            ['A'] = 8.0/5.0, ['B'] = 9.0/5.0,
        });

    /// <summary>JI Locrian. 1, 27/25, 6/5, 4/3, 36/25, 8/5, 9/5 (diminished 5th).</summary>
    public static readonly ChromaticRatioTable JustLocrian = ChromaticRatioTable.Build(
        naturals: new Dictionary<char, double> {
            ['C'] = 1.0, ['D'] = 27.0/25.0, ['E'] = 6.0/5.0, ['F'] = 4.0/3.0,
            ['G'] = 36.0/25.0, ['A'] = 8.0/5.0, ['B'] = 9.0/5.0,
        },
        sharps: new Dictionary<char, double> {
            ['C'] = 25.0/24.0, ['D'] = 75.0/64.0, ['F'] = 25.0/18.0,
            ['G'] = 25.0/16.0, ['A'] = 125.0/72.0,
        },
        flats: new Dictionary<char, double> {
            ['D'] = 16.0/15.0, ['E'] = 6.0/5.0, ['G'] = 64.0/45.0,
            ['A'] = 8.0/5.0, ['B'] = 9.0/5.0,
        });

    // === Pythagorean Mode Tables (Source: Wikipedia Pythagorean_tuning chain-of-fifths) ===
    // Diatonic ratios are mode-specific; chromatic alterations stay on chain-of-fifths.
    // Wolf fifth lands at G#-Eb when C is tonic — documented but ratios are exact.

    /// <summary>Pythagorean Ionian. 1, 9/8, 81/64, 4/3, 3/2, 27/16, 243/128.</summary>
    public static readonly ChromaticRatioTable PythIonian = ChromaticRatioTable.Build(
        naturals: new Dictionary<char, double> {
            ['C'] = 1.0,
            ['D'] = 9.0/8.0,
            ['E'] = 81.0/64.0,         // MICR-01 Pythagorean canary
            ['F'] = 4.0/3.0,
            ['G'] = 3.0/2.0,
            ['A'] = 27.0/16.0,
            ['B'] = 243.0/128.0,
        },
        sharps: new Dictionary<char, double> {
            ['C'] = 2187.0/2048.0,
            ['D'] = 19683.0/16384.0,
            ['F'] = 729.0/512.0,
            ['G'] = 6561.0/4096.0,
            ['A'] = 59049.0/32768.0,
        },
        flats: new Dictionary<char, double> {
            ['D'] = 256.0/243.0,
            ['E'] = 32.0/27.0,         // distinct from D# = 19683/16384 per D-09
            ['G'] = 1024.0/729.0,
            ['A'] = 128.0/81.0,
            ['B'] = 16.0/9.0,
        });

    /// <summary>Pythagorean Aeolian (natural minor). 1, 9/8, 32/27, 4/3, 3/2, 128/81, 16/9.</summary>
    public static readonly ChromaticRatioTable PythAeolian = ChromaticRatioTable.Build(
        naturals: new Dictionary<char, double> {
            ['C'] = 1.0, ['D'] = 9.0/8.0, ['E'] = 32.0/27.0, ['F'] = 4.0/3.0,
            ['G'] = 3.0/2.0, ['A'] = 128.0/81.0, ['B'] = 16.0/9.0,
        },
        sharps: new Dictionary<char, double> {
            ['C'] = 2187.0/2048.0, ['D'] = 19683.0/16384.0, ['F'] = 729.0/512.0,
            ['G'] = 6561.0/4096.0, ['A'] = 59049.0/32768.0,
        },
        flats: new Dictionary<char, double> {
            ['D'] = 256.0/243.0, ['E'] = 32.0/27.0, ['G'] = 1024.0/729.0,
            ['A'] = 128.0/81.0, ['B'] = 16.0/9.0,
        });

    /// <summary>Pythagorean Dorian. 1, 9/8, 32/27, 4/3, 3/2, 27/16, 16/9.</summary>
    public static readonly ChromaticRatioTable PythDorian = ChromaticRatioTable.Build(
        naturals: new Dictionary<char, double> {
            ['C'] = 1.0, ['D'] = 9.0/8.0, ['E'] = 32.0/27.0, ['F'] = 4.0/3.0,
            ['G'] = 3.0/2.0, ['A'] = 27.0/16.0, ['B'] = 16.0/9.0,
        },
        sharps: new Dictionary<char, double> {
            ['C'] = 2187.0/2048.0, ['D'] = 19683.0/16384.0, ['F'] = 729.0/512.0,
            ['G'] = 6561.0/4096.0, ['A'] = 59049.0/32768.0,
        },
        flats: new Dictionary<char, double> {
            ['D'] = 256.0/243.0, ['E'] = 32.0/27.0, ['G'] = 1024.0/729.0,
            ['A'] = 128.0/81.0, ['B'] = 16.0/9.0,
        });

    /// <summary>Pythagorean Phrygian. 1, 256/243, 32/27, 4/3, 3/2, 128/81, 16/9.</summary>
    public static readonly ChromaticRatioTable PythPhrygian = ChromaticRatioTable.Build(
        naturals: new Dictionary<char, double> {
            ['C'] = 1.0, ['D'] = 256.0/243.0, ['E'] = 32.0/27.0, ['F'] = 4.0/3.0,
            ['G'] = 3.0/2.0, ['A'] = 128.0/81.0, ['B'] = 16.0/9.0,
        },
        sharps: new Dictionary<char, double> {
            ['C'] = 2187.0/2048.0, ['D'] = 19683.0/16384.0, ['F'] = 729.0/512.0,
            ['G'] = 6561.0/4096.0, ['A'] = 59049.0/32768.0,
        },
        flats: new Dictionary<char, double> {
            ['D'] = 256.0/243.0, ['E'] = 32.0/27.0, ['G'] = 1024.0/729.0,
            ['A'] = 128.0/81.0, ['B'] = 16.0/9.0,
        });

    /// <summary>Pythagorean Lydian. 1, 9/8, 81/64, 729/512, 3/2, 27/16, 243/128 (raised 4th).</summary>
    public static readonly ChromaticRatioTable PythLydian = ChromaticRatioTable.Build(
        naturals: new Dictionary<char, double> {
            ['C'] = 1.0, ['D'] = 9.0/8.0, ['E'] = 81.0/64.0, ['F'] = 729.0/512.0,
            ['G'] = 3.0/2.0, ['A'] = 27.0/16.0, ['B'] = 243.0/128.0,
        },
        sharps: new Dictionary<char, double> {
            ['C'] = 2187.0/2048.0, ['D'] = 19683.0/16384.0, ['F'] = 729.0/512.0,
            ['G'] = 6561.0/4096.0, ['A'] = 59049.0/32768.0,
        },
        flats: new Dictionary<char, double> {
            ['D'] = 256.0/243.0, ['E'] = 32.0/27.0, ['G'] = 1024.0/729.0,
            ['A'] = 128.0/81.0, ['B'] = 16.0/9.0,
        });

    /// <summary>Pythagorean Mixolydian. 1, 9/8, 81/64, 4/3, 3/2, 27/16, 16/9 (lowered 7th).</summary>
    public static readonly ChromaticRatioTable PythMixolydian = ChromaticRatioTable.Build(
        naturals: new Dictionary<char, double> {
            ['C'] = 1.0, ['D'] = 9.0/8.0, ['E'] = 81.0/64.0, ['F'] = 4.0/3.0,
            ['G'] = 3.0/2.0, ['A'] = 27.0/16.0, ['B'] = 16.0/9.0,
        },
        sharps: new Dictionary<char, double> {
            ['C'] = 2187.0/2048.0, ['D'] = 19683.0/16384.0, ['F'] = 729.0/512.0,
            ['G'] = 6561.0/4096.0, ['A'] = 59049.0/32768.0,
        },
        flats: new Dictionary<char, double> {
            ['D'] = 256.0/243.0, ['E'] = 32.0/27.0, ['G'] = 1024.0/729.0,
            ['A'] = 128.0/81.0, ['B'] = 16.0/9.0,
        });

    /// <summary>Pythagorean Locrian. 1, 256/243, 32/27, 4/3, 1024/729, 128/81, 16/9.</summary>
    public static readonly ChromaticRatioTable PythLocrian = ChromaticRatioTable.Build(
        naturals: new Dictionary<char, double> {
            ['C'] = 1.0, ['D'] = 256.0/243.0, ['E'] = 32.0/27.0, ['F'] = 4.0/3.0,
            ['G'] = 1024.0/729.0, ['A'] = 128.0/81.0, ['B'] = 16.0/9.0,
        },
        sharps: new Dictionary<char, double> {
            ['C'] = 2187.0/2048.0, ['D'] = 19683.0/16384.0, ['F'] = 729.0/512.0,
            ['G'] = 6561.0/4096.0, ['A'] = 59049.0/32768.0,
        },
        flats: new Dictionary<char, double> {
            ['D'] = 256.0/243.0, ['E'] = 32.0/27.0, ['G'] = 1024.0/729.0,
            ['A'] = 128.0/81.0, ['B'] = 16.0/9.0,
        });
}
