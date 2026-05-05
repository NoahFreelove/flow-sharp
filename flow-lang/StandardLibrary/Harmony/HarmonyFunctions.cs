using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Harmony;

/// <summary>
/// Built-in functions for chord and harmony operations.
/// </summary>
public static class HarmonyFunctions
{
    /// <summary>
    /// Phase 14 DX-06 (CONTEXT D-03/D-04/D-05/D-06): registers context-dependent harmony
    /// built-ins — currently just <c>enharmonic(Note) → Note</c>. Wired from
    /// <c>BuiltInFunctions.RegisterContextDependentFunctions</c>.
    ///
    /// Kept distinct from the existing parameterless <see cref="Register"/> method so the
    /// additive nature of this extension is visible at a glance.
    /// </summary>
    public static void RegisterContextDependent(InternalFunctionRegistry registry, FlowLang.Runtime.ExecutionContext context)
    {
        var enharmonicSig = new FunctionSignature("enharmonic", [NoteType.Instance]);
        registry.Register("enharmonic", enharmonicSig, args => Enharmonic(args, context));
    }

    /// <summary>
    /// Key-context-aware enharmonic respelling.
    /// - Naturals at edges (E/F/B/C) respell to multi-letter neighbor (DEFER-04 / Phase 20);
    ///   D/G/A naturals return unchanged (no enharmonic edge — they sit between two whole-step
    ///   letters, so there is no adjacent-letter spelling at the same pitch).
    /// - In-key (active <c>MusicalContext.Key</c>): if input pitch matches a scale tone by MIDI,
    ///   return the diatonic spelling whose key-affinity (flat key → flat letter, sharp key →
    ///   sharp letter) matches. Implementation is MIDI-based (not string-echo) to bypass Pitfall 3
    ///   — ScaleDatabase.GetScaleNotes returns sharp-spelled tones even for flat keys. The in-key
    ///   branch fires before the natural-edge switch so diatonic preservation wins (D-USER-B).
    /// - Chromatic-in-key or no-key: flip sharp ↔ flat (Db4 ↔ C#4, F#3 ↔ Gb3). Double-sharps
    ///   and double-flats may collapse to naturals (F##4 → G4) — documented non-involutive.
    /// </summary>
    private static Value Enharmonic(IReadOnlyList<Value> args, FlowLang.Runtime.ExecutionContext context)
    {
        // Note values are stored as the original string form (Value.cs:32). Parse to get the triple.
        string noteStr = args[0].As<string>();
        var (letter, octave, alteration) = NoteType.Parse(noteStr);

        int inputMidi = NoteType.ToMidiNote(letter, octave, alteration);
        var musicalCtx = context.GetMusicalContext();
        string? key = musicalCtx?.Key;

        // Phase 23 Plan 23-03 Task 2 / D-11: under non-12-TET tuning, enharmonic respelling is
        // destructive (~21 cent shift at enharmonic junctions) — emit a one-shot stderr warning
        // so composers know the silent regression is happening. Conversion still happens; warning
        // is purely advisory. Pitfall 5 #3 / AUDIT-VERIFIED. EqualTemperament + no-pragma silent.
        if (musicalCtx?.Tuning is TuningSystem activeTuning && activeTuning != TuningSystem.EqualTemperament)
        {
            RenderingDiagnostics.WarnOnce(
                "enharmonic-non-equal-temperament",
                "[enharmonic] called inside tuning != equalTemperament; conversion is destructive (≈ 21 cent shift)");
        }

        // D-04 / D-USER-B: in-key branch fires FIRST so diatonic spelling wins for both naturals
        // and accidentals. If the input pitch matches a scale tone, we return that scale spelling
        // rather than the no-key edge respelling. e.g. (key Fmajor) (enharmonic E4) → "E4" because
        // E is diatonic in F major; only chromatic-in-key inputs fall through to the no-key edge.
        if (key != null)
        {
            if (TryEnharmonicInKey(inputMidi, key, out Value? inKeyResult) && inKeyResult != null)
            {
                return inKeyResult;
            }
            // chromatic-not-in-scale → fall through to natural-edge / sharp-flat flip
        }

        // DEFER-04 (Phase 20 plan 20-02): naturals at letter-boundary edges (E/F/B/C) respell
        // to their multi-letter neighbor. D/G/A naturals remain unchanged (no enharmonic edge).
        // E ↔ Fb (same octave): E4 (MIDI 64) → Fb4 (F=65, alt=-1, MIDI 64)
        // F ↔ E# (same octave): F4 (MIDI 65) → E#4 (E=64, alt=+1, MIDI 65)
        // B ↔ Cb (octave +1):   B4 (MIDI 71) → Cb5 (C5=72, alt=-1, MIDI 71)
        // C ↔ B# (octave -1):   C4 (MIDI 60) → B#3 (B3=59, alt=+1, MIDI 60)
        if (alteration == 0)
        {
            return letter switch
            {
                'E' => Value.Note(NoteType.Format('F', octave,     -1)),
                'F' => Value.Note(NoteType.Format('E', octave,     +1)),
                'B' => Value.Note(NoteType.Format('C', octave + 1, -1)),
                'C' => Value.Note(NoteType.Format('B', octave - 1, +1)),
                _   => Value.Note(NoteType.Format(letter, octave, 0)),  // D/G/A unchanged
            };
        }

        // D-05: no-key flip. Sharp → letter up (alt = inputMidi - naturalMidi(up)).
        //                   Flat  → letter down (alt = inputMidi - naturalMidi(down)).
        var (flippedLetter, flippedOct, flippedAlt) = ComputeFlippedSpelling(letter, octave, alteration, inputMidi);
        return Value.Note(NoteType.Format(flippedLetter, flippedOct, flippedAlt));
    }

    /// <summary>
    /// In-key enharmonic lookup. Iterates scale tones at each octave position; when the input
    /// MIDI matches a scale tone's MIDI, synthesize a spelling whose accidental direction matches
    /// the key signature (flat key → flat accidental, sharp key → sharp accidental).
    /// </summary>
    private static bool TryEnharmonicInKey(int inputMidi, string key, out Value? result)
    {
        result = null;
        string[]? scaleTokens = ScaleDatabase.GetScaleNotes(key);
        if (scaleTokens == null)
            return false;

        bool preferFlat = KeyPrefersFlats(key);
        int targetSemitone = ((inputMidi % 12) + 12) % 12;

        foreach (var scaleToken in scaleTokens)
        {
            if (!TryGetSemitoneOfScaleSpelling(scaleToken, out int scaleSemitone))
                continue;
            if (scaleSemitone != targetSemitone)
                continue;

            // Match: synthesize a spelling that agrees with the key's accidental direction.
            var (spLetter, spAlt) = ResolveScaleSpellingWithKeyAffinity(scaleToken, preferFlat);

            // Calibrate octave so ToMidiNote(spLetter, oct, spAlt) == inputMidi.
            int oct = inputMidi / 12 - 1;
            while (NoteType.ToMidiNote(spLetter, oct, spAlt) > inputMidi) oct--;
            while (NoteType.ToMidiNote(spLetter, oct, spAlt) < inputMidi) oct++;

            if (NoteType.ToMidiNote(spLetter, oct, spAlt) != inputMidi)
                continue;  // spelling doesn't hit the target — skip and keep searching

            result = Value.Note(NoteType.Format(spLetter, oct, spAlt));
            return true;
        }

        return false;
    }

    /// <summary>
    /// A key is treated as "flat" if its root uses a <c>b</c> accidental (Db, Eb, Gb, Ab, Bb)
    /// OR it's F major / F minor (the one sharpless flat-family key). Otherwise it's treated
    /// as sharp-leaning (C, G, D, A, E, B, F#, C#, and their minors).
    /// </summary>
    private static bool KeyPrefersFlats(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;

        // Strip the "major"/"minor" suffix (case-insensitive) to isolate the root token.
        string lower = key.ToLowerInvariant();
        string root;
        if (lower.EndsWith("major"))
            root = key[..^5];
        else if (lower.EndsWith("minor"))
            root = key[..^5];
        else
            root = key;

        // "Db", "Eb", "Gb", "Ab", "Bb" → flat-family.
        if (root.Length >= 2 && (root[1] == 'b' || root[1] == 'f'))
            return true;

        // Bare F (F major or F minor) → flat-family (one flat in the signature).
        if (root.Length == 1 && char.ToUpperInvariant(root[0]) == 'F')
            return true;

        return false;
    }

    /// <summary>
    /// Computes the chromatic semitone (0..11) of a scale-token spelling produced by
    /// <see cref="ScaleDatabase.GetScaleNotes"/>. Accepts the 's'/'#'/'+' sharp convention and
    /// 'f'/'b'/'-' flat convention.
    /// </summary>
    private static bool TryGetSemitoneOfScaleSpelling(string token, out int semitone)
    {
        semitone = 0;
        if (string.IsNullOrEmpty(token))
            return false;

        char letter = char.ToUpper(token[0]);
        int baseSemitone = letter switch
        {
            'C' => 0, 'D' => 2, 'E' => 4, 'F' => 5, 'G' => 7, 'A' => 9, 'B' => 11,
            _ => -1
        };
        if (baseSemitone < 0)
            return false;

        int delta = 0;
        for (int i = 1; i < token.Length; i++)
        {
            switch (token[i])
            {
                case 's':
                case '#':
                case '+':
                    delta++;
                    break;
                case 'f':
                case 'b':
                case '-':
                    delta--;
                    break;
                default:
                    return false;
            }
        }

        semitone = ((baseSemitone + delta) % 12 + 12) % 12;
        return true;
    }

    /// <summary>
    /// Converts a scale-token spelling into a <c>(letter, alteration)</c> pair, rewriting
    /// sharp-spelled tokens to flat spellings when the active key prefers flats (mitigates
    /// Pitfall 3: ScaleDatabase returns sharp-spelled tokens even for flat keys).
    /// </summary>
    private static (char letter, int alt) ResolveScaleSpellingWithKeyAffinity(string token, bool preferFlat)
    {
        char letter = char.ToUpper(token[0]);
        int alt = 0;
        for (int i = 1; i < token.Length; i++)
        {
            switch (token[i])
            {
                case 's':
                case '#':
                case '+':
                    alt++;
                    break;
                case 'f':
                case 'b':
                case '-':
                    alt--;
                    break;
            }
        }

        // Flat-key rewrite: a sharp-spelled token (e.g., "Cs" = C# = MIDI semitone 1) in a
        // flat-prefering key (Db major) gets rewritten to the flat spelling ("Db"). Use MIDI
        // equivalence: Cs (C=0, +1 = 1) → Db (D=2, -1 = 1). Same MIDI, flat-family letter.
        if (preferFlat && alt > 0)
        {
            char upLetter = LetterUp(letter);
            int upNaturalSemitone = NaturalSemitoneOf(upLetter);
            int currentSemitone = NaturalSemitoneOf(letter) + alt;
            // Normalize over the octave boundary (B → C wraps).
            int semitoneDiff = upNaturalSemitone - NaturalSemitoneOf(letter);
            if (semitoneDiff < 0) semitoneDiff += 12;
            int newAlt = alt - semitoneDiff;
            return (upLetter, newAlt);
        }

        return (letter, alt);
    }

    /// <summary>
    /// Computes the sharp↔flat flipped spelling for a non-natural input. Sharps become the
    /// upper neighbor (C# → Db: letter up, alt recomputed from MIDI). Flats become the lower
    /// neighbor (Db → C#: letter down, alt recomputed from MIDI). Double-accidentals can
    /// collapse to naturals (F## = MIDI G → ('G', oct, 0)).
    /// </summary>
    private static (char letter, int oct, int alt) ComputeFlippedSpelling(char letter, int octave, int alteration, int inputMidi)
    {
        if (alteration > 0)
        {
            // sharp → letter UP. If letter is 'B', upper neighbor 'C' is in the next octave.
            char up = LetterUp(letter);
            int upOct = (letter == 'B') ? octave + 1 : octave;
            int upNaturalMidi = NoteType.GetNoteValue(up, upOct);
            return (up, upOct, inputMidi - upNaturalMidi);
        }
        else
        {
            // flat → letter DOWN. If letter is 'C', lower neighbor 'B' is in the previous octave.
            char down = LetterDown(letter);
            int downOct = (letter == 'C') ? octave - 1 : octave;
            int downNaturalMidi = NoteType.GetNoteValue(down, downOct);
            return (down, downOct, inputMidi - downNaturalMidi);
        }
    }

    private static char LetterUp(char letter) => letter switch
    {
        'C' => 'D', 'D' => 'E', 'E' => 'F', 'F' => 'G',
        'G' => 'A', 'A' => 'B', 'B' => 'C',
        _ => letter
    };

    private static char LetterDown(char letter) => letter switch
    {
        'C' => 'B', 'D' => 'C', 'E' => 'D', 'F' => 'E',
        'G' => 'F', 'A' => 'G', 'B' => 'A',
        _ => letter
    };

    private static int NaturalSemitoneOf(char letter) => letter switch
    {
        'C' => 0, 'D' => 2, 'E' => 4, 'F' => 5, 'G' => 7, 'A' => 9, 'B' => 11,
        _ => 0
    };

    /// <summary>
    /// DX-10 direction reordering helper for the 4-arg <c>arpeggio</c> overload. Per Phase 22
    /// CONTEXT and RESEARCH Pitfall 7, <c>"random"</c> falls back to <c>"up"</c> in v1.3
    /// (seeded random arpeggio deferred to v1.4 to preserve byte-identical determinism).
    /// Unknown direction strings also fall through to <c>"up"</c> per the project's charitable
    /// interpretation memory — no error path on unknown input. <c>"chord-tone"</c> /
    /// <c>"scale-tone"</c> patterns are accepted at the outer signature but the v1.3
    /// implementation routes them to linear ordering.
    /// </summary>
    private static List<string> ApplyDirection(List<string> notes, string direction)
    {
        return direction.ToLowerInvariant() switch
        {
            "down"   => notes.AsEnumerable().Reverse().ToList(),
            "updown" => notes.Concat(notes.AsEnumerable().Reverse().Skip(1)).ToList(),
            "downup" => notes.AsEnumerable().Reverse().Concat(notes.Skip(1)).ToList(),
            // "random" deferred to v1.4 — falls back to "up" per RESEARCH Pitfall 7
            _        => notes,
        };
    }

    public static void Register(InternalFunctionRegistry registry)
    {
        // DX-11 (Phase 22 plan 22-03): inversion(Chord, Int) + voicing(Chord, String).
        // Registered first so the chord-shape transforms are visible to subsequent
        // chord-using harmony helpers in the same registration pass. Charitable D-07
        // (incomplete chord -> input unchanged) lives inside the Voicings static class.
        Voicings.Register(registry);

        // chord(String) -> Chord
        // QUICK-260504-cks: runtime constructor from a chord-symbol string. Wraps
        // ChordParser.TryParseFlexible — accepts both Flow's `s`/`f` accidentals and
        // common-practice `#`/`b`, supports slash bass (`C/G`), bare-digit qualities
        // (`C5`/`G7`/`D9`/`C13`), and the full QualityIntervals vocabulary
        // (triads, 6/7/9/11/13 family, sus, add, alterations). Charitable on
        // unparseable input — returns Void instead of throwing, matching
        // resolveNumeral's pattern below.
        var chordFromStringSignature = new FunctionSignature("chord", [StringType.Instance]);
        registry.Register("chord", chordFromStringSignature, args =>
        {
            var symbol = args[0].As<string>();
            if (ChordParser.TryParseFlexible(symbol, out var chordData) && chordData != null)
                return Value.Chord(chordData);
            return Value.Void();
        });

        // chord(Note) -> Chord
        // QUICK-260504-cks: Flow's literal evaluator (`TryParseSpecialLiteral`) auto-coerces
        // any quoted string that *parses* as a Note into a `Note` value at evaluation time
        // (e.g. `"C"`, `"C5"`, `"G7"`, `"Bb"` all become Notes, not Strings). Without this
        // overload, the most natural composer spelling — `(chord "C7")` — would die with
        // "No matching overload for function 'chord' with argument types (Note)". This
        // overload re-routes the Note's stored text back through `TryParseFlexible` so the
        // string-form vocabulary (power chords, dom7, slash bass embedded in note-shaped
        // tokens) reaches the same parser as the explicit String overload.
        var chordFromNoteSignature = new FunctionSignature("chord", [NoteType.Instance]);
        registry.Register("chord", chordFromNoteSignature, args =>
        {
            var noteText = args[0].As<string>();
            if (ChordParser.TryParseFlexible(noteText, out var chordData) && chordData != null)
                return Value.Chord(chordData);
            // Fallback: bare root letter (e.g. Note "C4" → C major triad on the root letter).
            // This keeps `(chord <Note variable>)` charitable when the note text isn't itself
            // a recognized chord symbol — pull the leading A-G as the root and build a major.
            if (!string.IsNullOrEmpty(noteText) && noteText[0] >= 'A' && noteText[0] <= 'G')
            {
                if (ChordParser.TryParseFlexible(noteText[0] + "maj", out var fallback) && fallback != null)
                    return Value.Chord(fallback);
            }
            return Value.Void();
        });

        // str(Chord) -> String
        var strChordSignature = new FunctionSignature("str", [ChordType.Instance]);
        registry.Register("str", strChordSignature, args =>
        {
            var chord = args[0].As<ChordData>();
            return Value.String(chord.ToString());
        });

        // chordNotes(Chord) -> Strings
        var chordNotesSignature = new FunctionSignature("chordNotes", [ChordType.Instance]);
        registry.Register("chordNotes", chordNotesSignature, args =>
        {
            var chord = args[0].As<ChordData>();
            var notes = chord.NoteNames.Select(n => Value.String(n)).ToArray();
            return Value.Array(notes, StringType.Instance);
        });

        // chordRoot(Chord) -> String
        var chordRootSignature = new FunctionSignature("chordRoot", [ChordType.Instance]);
        registry.Register("chordRoot", chordRootSignature, args =>
        {
            var chord = args[0].As<ChordData>();
            return Value.String(chord.Root);
        });

        // chordQuality(Chord) -> String
        var chordQualitySignature = new FunctionSignature("chordQuality", [ChordType.Instance]);
        registry.Register("chordQuality", chordQualitySignature, args =>
        {
            var chord = args[0].As<ChordData>();
            return Value.String(chord.Quality);
        });

        // arpeggio(Chord, String) -> Sequence (up, down, updown)
        var arpeggioSignature = new FunctionSignature("arpeggio", [ChordType.Instance, StringType.Instance]);
        registry.Register("arpeggio", arpeggioSignature, args =>
        {
            var chord = args[0].As<ChordData>();
            var direction = args[1].As<string>();

            var noteNames = chord.NoteNames.ToList();

            switch (direction.ToLower())
            {
                case "down":
                    noteNames.Reverse();
                    break;
                case "updown":
                    var down = new List<string>(noteNames);
                    down.Reverse();
                    if (down.Count > 1) down = down.Skip(1).ToList();
                    noteNames.AddRange(down);
                    break;
                // "up" is default order
            }

            // Build a sequence with one bar containing the arpeggiated notes
            var musicalNotes = new List<MusicalNoteData>();
            foreach (var noteName in noteNames)
            {
                var (name, octave, alteration) = NoteType.Parse(noteName);
                musicalNotes.Add(new MusicalNoteData(name, octave, alteration,
                    (int)NoteValueType.Value.EIGHTH, isRest: false));
            }

            var timeSig = new TimeSignatureData(4, 4);
            var bar = new BarData(musicalNotes, timeSig);
            var sequence = new SequenceData();
            sequence.AddBar(bar);

            return Value.Sequence(sequence);
        });

        // DX-10: 4-arg arpeggio(Chord, NoteValue, direction, pattern) -> Sequence
        // Phase 22 plan 22-01. Existing 2-arg overload above stays byte-identical.
        // - rate: NoteValue (int-backed enum) drives MusicalNoteData.DurationValue
        // - direction: "up" | "down" | "updown" | "downup" | "random" (random falls back to "up"
        //   in v1.3 per RESEARCH Pitfall 7 / charitable-interpretation memory; seeded random
        //   arpeggio deferred to v1.4 to preserve byte-identical determinism)
        // - pattern: "linear" | "chord-tone" | "scale-tone" (chord-tone / scale-tone route to
        //   linear in v1.3 per RESEARCH §Future Requirements / Assumption A8)
        var arpeggioFullSig = new FunctionSignature("arpeggio",
            [ChordType.Instance, NoteValueType.Instance, StringType.Instance, StringType.Instance]);
        registry.Register("arpeggio", arpeggioFullSig, args =>
        {
            var chord = args[0].As<ChordData>();
            int rateEnum = args[1].As<int>();   // NoteValue is int-backed
            var direction = args[2].As<string>();
            var pattern = args[3].As<string>(); // accepted for future expansion; v1.3 unused
            _ = pattern;

            var noteNames = ApplyDirection(chord.NoteNames.ToList(), direction);

            var musicalNotes = new List<MusicalNoteData>();
            foreach (var noteName in noteNames)
            {
                var (name, octave, alteration) = NoteType.Parse(noteName);
                musicalNotes.Add(new MusicalNoteData(name, octave, alteration,
                    rateEnum, isRest: false));
            }

            var timeSigFull = new TimeSignatureData(4, 4);
            var barFull = new BarData(musicalNotes, timeSigFull);
            var sequenceFull = new SequenceData();
            sequenceFull.AddBar(barFull);

            return Value.Sequence(sequenceFull);
        });

        // scaleNotes(String) -> Strings
        var scaleNotesSignature = new FunctionSignature("scaleNotes", [StringType.Instance]);
        registry.Register("scaleNotes", scaleNotesSignature, args =>
        {
            var keyName = args[0].As<string>();
            var notes = ScaleDatabase.GetScaleNotes(keyName);
            if (notes == null)
                return Value.Array(Array.Empty<Value>(), StringType.Instance);
            return Value.Array(notes.Select(n => Value.String(n)).ToArray(), StringType.Instance);
        });

        // resolveNumeral(String, String) -> Chord
        var resolveNumeralSignature = new FunctionSignature("resolveNumeral",
            [StringType.Instance, StringType.Instance]);
        registry.Register("resolveNumeral", resolveNumeralSignature, args =>
        {
            var numeral = args[0].As<string>();
            var keyName = args[1].As<string>();
            var chordData = ScaleDatabase.ResolveRomanNumeral(numeral, keyName);
            if (chordData == null)
                return Value.Void();
            return Value.Chord(chordData);
        });

        // str(Section) -> String
        var strSectionSignature = new FunctionSignature("str", [SectionType.Instance]);
        registry.Register("str", strSectionSignature, args =>
        {
            var section = args[0].As<SectionData>();
            return Value.String(section.ToString());
        });

        // str(Song) -> String
        var strSongSignature = new FunctionSignature("str", [SongType.Instance]);
        registry.Register("str", strSongSignature, args =>
        {
            var song = args[0].As<SongData>();
            return Value.String(song.ToString());
        });

        // getSections(Song) -> Strings
        var getSectionsSignature = new FunctionSignature("getSections", [SongType.Instance]);
        registry.Register("getSections", getSectionsSignature, args =>
        {
            var song = args[0].As<SongData>();
            var names = song.Sections.Select(s => Value.String(s.Name)).ToArray();
            return Value.Array(names, StringType.Instance);
        });

        // sectionSequences(Section) -> Strings (returns names of sequences in section)
        var sectionSequencesSignature = new FunctionSignature("sectionSequences", [SectionType.Instance]);
        registry.Register("sectionSequences", sectionSequencesSignature, args =>
        {
            var section = args[0].As<SectionData>();
            var names = section.Sequences.Keys.Select(k => Value.String(k)).ToArray();
            return Value.Array(names, StringType.Instance);
        });
    }
}
