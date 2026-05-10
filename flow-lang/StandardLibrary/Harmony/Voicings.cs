using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Harmony;

/// <summary>
/// Phase 22 DX-11: chord-shape transforms — inversion (rotate the n lowest notes up an
/// octave each) and named voicings (drop2, drop3, open, close, spread).
///
/// Per Phase 22 CONTEXT D-07 (charitable interpretation): when a chord lacks enough notes
/// for the requested voicing (drop2/drop3 need >=4; spread/open/close need >=3) — or when
/// the inversion index is out of bounds (n &lt;= 0 || n &gt;= NoteNames.Length) — the function
/// returns the input chord unchanged. No error, no warning, no log spam. Composer can
/// keep iterating. See Phase 22 CONTEXT D-07.
///
/// Per Phase 22 CONTEXT D-08, every voicing helper documents this in its doc comment.
///
/// Note-name canonicalization (Pitfall 5): all octave manipulation goes through
/// NoteType.Parse + NoteType.Format so the "+" / "-" accidental form round-trips
/// exactly. Never concatenate "s" / "#" / "b" — that escapes the canonical form
/// the rest of the runtime expects.
/// </summary>
public static class Voicings
{
    /// <summary>
    /// Wired from <see cref="HarmonyFunctions.Register"/>. Adds:
    ///   inversion(Chord, Int) -> Chord
    ///   voicing(Chord, String) -> Chord
    /// to the runtime's S-expression dispatch table.
    /// </summary>
    public static void Register(InternalFunctionRegistry registry)
    {
        // inversion(Chord, Int) -> Chord
        var inversionSig = new FunctionSignature("inversion",
            [ChordType.Instance, IntType.Instance]);
        registry.Register("inversion", inversionSig, args =>
        {
            var chord = args[0].As<ChordData>();
            int n = args[1].As<int>();
            return Value.Chord(Inversion(chord, n));
        });

        // voicing(Chord, String) -> Chord
        var voicingSig = new FunctionSignature("voicing",
            [ChordType.Instance, StringType.Instance]);
        registry.Register("voicing", voicingSig, args =>
        {
            var chord = args[0].As<ChordData>();
            string name = args[1].As<string>();
            return Value.Chord(Voicing(chord, name));
        });
    }

    /// <summary>
    /// inversion(chord, n) — rotates the n lowest notes up an octave each. After n=1 on
    /// ["C4","E4","G4"] the result is ["E4","G4","C5"].
    ///
    /// Per Phase 22 CONTEXT D-07 (charitable interpretation): when n &gt;= NoteNames.Length
    /// or n &lt;= 0, returns the input chord unchanged. No error, no warning. See Phase 22
    /// CONTEXT D-07.
    /// </summary>
    public static ChordData Inversion(ChordData input, int n)
    {
        if (n <= 0 || n >= input.NoteNames.Length) return input;  // CONTEXT D-07
        var notes = input.NoteNames.ToList();
        for (int i = 0; i < n; i++)
        {
            string lowest = notes[0];
            notes.RemoveAt(0);
            notes.Add(RaiseOctave(lowest));
        }
        return new ChordData(input.Root, input.Quality, input.Octave, notes.ToArray());
    }

    /// <summary>
    /// voicing(chord, name) — applies a named voicing. Recognized names:
    ///   "drop2"  — lower the 2nd-from-top note an octave
    ///   "drop3"  — lower the 3rd-from-top note an octave
    ///   "open"   — raise the middle note an octave (wider spacing)
    ///   "close"  — collapse any note > 1 octave above the root back down
    ///   "spread" — raise the highest note an additional octave
    ///
    /// Per Phase 22 CONTEXT D-07 (charitable interpretation): unknown names return input
    /// unchanged; voicings whose minimum-note-count requirement isn't met return input
    /// unchanged. No error, no warning. See Phase 22 CONTEXT D-07.
    /// </summary>
    public static ChordData Voicing(ChordData input, string name) =>
        (name ?? "").ToLowerInvariant() switch
        {
            "drop2"  => Drop2(input),
            "drop3"  => Drop3(input),
            "open"   => Open(input),
            "close"  => Close(input),
            "spread" => Spread(input),
            _        => input,        // CONTEXT D-07 — unknown name returns unchanged
        };

    // === Voicing implementations ===

    /// <summary>
    /// drop2 — lowers the 2nd-from-top note by an octave. Common in jazz comping.
    /// Per Phase 22 CONTEXT D-07: returns input unchanged if NoteNames.Length &lt; 4.
    /// See Phase 22 CONTEXT D-07.
    /// </summary>
    private static ChordData Drop2(ChordData input)
    {
        if (input.NoteNames.Length < 4) return input;  // CONTEXT D-07
        var notes = input.NoteNames.ToList();
        int idx = notes.Count - 2;
        notes[idx] = LowerOctave(notes[idx]);
        notes.Sort(CompareByPitch);
        return new ChordData(input.Root, input.Quality, input.Octave, notes.ToArray());
    }

    /// <summary>
    /// drop3 — lowers the 3rd-from-top note by an octave.
    /// Per Phase 22 CONTEXT D-07: returns input unchanged if NoteNames.Length &lt; 4.
    /// See Phase 22 CONTEXT D-07.
    /// </summary>
    private static ChordData Drop3(ChordData input)
    {
        if (input.NoteNames.Length < 4) return input;  // CONTEXT D-07
        var notes = input.NoteNames.ToList();
        int idx = notes.Count - 3;
        notes[idx] = LowerOctave(notes[idx]);
        notes.Sort(CompareByPitch);
        return new ChordData(input.Root, input.Quality, input.Octave, notes.ToArray());
    }

    /// <summary>
    /// open — opens spacing by raising the middle note (index 1) an octave. On a triad
    /// this turns ["C4","E4","G4"] into ["C4","G4","E5"].
    /// Per Phase 22 CONTEXT D-07: returns input unchanged if NoteNames.Length &lt; 3.
    /// See Phase 22 CONTEXT D-07.
    /// </summary>
    private static ChordData Open(ChordData input)
    {
        if (input.NoteNames.Length < 3) return input;  // CONTEXT D-07
        var notes = input.NoteNames.ToList();
        notes[1] = RaiseOctave(notes[1]);
        notes.Sort(CompareByPitch);
        return new ChordData(input.Root, input.Quality, input.Octave, notes.ToArray());
    }

    /// <summary>
    /// close — collapses spacing so every note sits within one octave of the root. Walks
    /// each non-root note: while its pitch is more than 12 semitones above the root, drop
    /// it an octave. Idempotent on already-close chords.
    /// Per Phase 22 CONTEXT D-07: returns input unchanged if NoteNames.Length &lt; 3.
    /// See Phase 22 CONTEXT D-07.
    /// </summary>
    private static ChordData Close(ChordData input)
    {
        if (input.NoteNames.Length < 3) return input;  // CONTEXT D-07
        var notes = input.NoteNames.ToList();
        var (rootLetter, rootOct, rootAlt) = NoteType.Parse(notes[0]);
        int rootMidi = NoteType.ToMidiNote(rootLetter, rootOct, rootAlt);
        for (int i = 1; i < notes.Count; i++)
        {
            var (letter, oct, alt) = NoteType.Parse(notes[i]);
            int midi = NoteType.ToMidiNote(letter, oct, alt);
            while (midi - rootMidi > 12)
            {
                oct -= 1;
                midi -= 12;
            }
            notes[i] = NoteType.Format(letter, oct, alt);
        }
        notes.Sort(CompareByPitch);
        return new ChordData(input.Root, input.Quality, input.Octave, notes.ToArray());
    }

    /// <summary>
    /// spread — widens spacing by raising the highest note an additional octave.
    /// Per Phase 22 CONTEXT D-07: returns input unchanged if NoteNames.Length &lt; 3.
    /// See Phase 22 CONTEXT D-07.
    /// </summary>
    private static ChordData Spread(ChordData input)
    {
        if (input.NoteNames.Length < 3) return input;  // CONTEXT D-07
        var notes = input.NoteNames.ToList();
        int top = notes.Count - 1;
        notes[top] = RaiseOctave(notes[top]);
        notes.Sort(CompareByPitch);
        return new ChordData(input.Root, input.Quality, input.Octave, notes.ToArray());
    }

    // === Helpers — canonical NoteType.Parse + NoteType.Format round-trip (Pitfall 5) ===

    private static string RaiseOctave(string noteName)
    {
        var (letter, oct, alt) = NoteType.Parse(noteName);
        return NoteType.Format(letter, oct + 1, alt);
    }

    private static string LowerOctave(string noteName)
    {
        var (letter, oct, alt) = NoteType.Parse(noteName);
        return NoteType.Format(letter, oct - 1, alt);
    }

    /// <summary>
    /// Compares two note-name strings by absolute MIDI pitch. Used to re-sort chord
    /// note lists after a drop/raise so the result reads low-to-high.
    /// </summary>
    private static int CompareByPitch(string a, string b)
    {
        var (la, oa, aa) = NoteType.Parse(a);
        var (lb, ob, ab) = NoteType.Parse(b);
        int ma = NoteType.ToMidiNote(la, oa, aa);
        int mb = NoteType.ToMidiNote(lb, ob, ab);
        return ma.CompareTo(mb);
    }
}
