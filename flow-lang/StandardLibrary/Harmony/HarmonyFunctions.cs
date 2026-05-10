using FlowLang.Runtime;
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
    /// - Naturals (alteration == 0) always return unchanged (D-05: no E↔Fb, F↔E#, B↔Cb, C↔B# edges).
    /// - In-key (active <c>MusicalContext.Key</c>): if input pitch matches a scale tone by MIDI,
    ///   return the diatonic spelling whose key-affinity (flat key → flat letter, sharp key →
    ///   sharp letter) matches. Implementation is MIDI-based (not string-echo) to bypass Pitfall 3
    ///   — ScaleDatabase.GetScaleNotes returns sharp-spelled tones even for flat keys.
    /// - Chromatic-in-key or no-key: flip sharp ↔ flat (Db4 ↔ C#4, F#3 ↔ Gb3). Double-sharps
    ///   and double-flats may collapse to naturals (F##4 → G4) — documented non-involutive.
    /// </summary>
    private static Value Enharmonic(IReadOnlyList<Value> args, FlowLang.Runtime.ExecutionContext context)
    {
        // Note values are stored as the original string form (Value.cs:32). Parse to get the triple.
        string noteStr = args[0].As<string>();
        var (letter, octave, alteration) = NoteType.Parse(noteStr);

        // D-05: naturals return unchanged, full stop — no edge respelling.
        if (alteration == 0)
        {
            return Value.Note(NoteType.Format(letter, octave, 0));
        }

        int inputMidi = NoteType.ToMidiNote(letter, octave, alteration);
        var musicalCtx = context.GetMusicalContext();
        string? key = musicalCtx?.Key;

        // D-04: in-key branch. Try to find a diatonic spelling that matches the input MIDI.
        if (key != null)
        {
            if (TryEnharmonicInKey(inputMidi, key, out Value? inKeyResult) && inKeyResult != null)
            {
                return inKeyResult;
            }
            // chromatic-not-in-scale → fall through to no-key flip
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

    public static void Register(InternalFunctionRegistry registry)
    {
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
