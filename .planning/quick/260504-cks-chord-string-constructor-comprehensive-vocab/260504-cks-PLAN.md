---
quick_id: 260504-cks
type: execute
wave: 1
depends_on: []
files_modified:
  - flow-lang/StandardLibrary/Harmony/ChordParser.cs
  - flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs
  - tests/test_chord_runtime.flow
autonomous: true
requirements:
  - QUICK-260504-cks: Composers can build a `Chord` value at runtime from a string symbol (`(chord "Am7b5")`, `(chord "G7#9")`, `(chord "C/E")`) covering the full common-practice vocabulary — triads, sevenths, 9/11/13 extensions, sus, add, alterations, slash bass — without inventing a new literal-only quality every time.

must_haves:
  truths:
    - "(chord \"Cmaj\") returns a Chord whose notes are [C E G] (major triad)"
    - "(chord \"Cm7\") returns a Chord whose notes are [C Ef G Bf]"
    - "(chord \"Cmaj7\") returns a Chord whose notes are [C E G B]"
    - "(chord \"C9\") returns a Chord whose notes are [C E G Bf D] (dom9 = dom7 + 9)"
    - "(chord \"C11\") returns a Chord whose notes are [C E G Bf D F] (dom11 = dom9 + 11)"
    - "(chord \"C13\") returns a Chord whose notes are [C E G Bf D F A] (dom13 = dom11 + 13)"
    - "(chord \"Cmaj9\") returns a Chord whose notes are [C E G B D]"
    - "(chord \"Cmaj13\") returns a Chord whose notes are [C E G B D F A]"
    - "(chord \"Cm9\") returns a Chord whose notes are [C Ef G Bf D]"
    - "(chord \"C5\") returns a power-chord Chord whose notes are [C G] (no 3rd)"
    - "(chord \"C6\") returns a Chord whose notes are [C E G A]"
    - "(chord \"Cm6\") returns a Chord whose notes are [C Ef G A]"
    - "(chord \"Csus2\") returns [C D G]; (chord \"Csus4\") returns [C F G]"
    - "(chord \"C7sus4\") returns [C F G Bf]"
    - "(chord \"Cadd9\") returns [C E G D]; (chord \"Cadd11\") returns [C E G F]"
    - "(chord \"Cm7b5\") and (chord \"Cm7f5\") both return the same half-diminished chord [C Ef Gf Bf] (b/# and f/s alteration aliases)"
    - "(chord \"Cdim7\") returns [C Ef Gf A]; (chord \"Caug\") returns [C E Gs]"
    - "(chord \"C7b9\") returns [C E G Bf Df]; (chord \"C7#9\") returns [C E G Bf Ds]; (chord \"C7#11\") returns [C E G Bf Fs]"
    - "(chord \"Cmaj7#11\") returns [C E G B Fs]"
    - "(chord \"C/E\") and (chord \"C/G\") return the same notes as (chord \"C\") but with the bass note prepended to NoteNames as the first entry one octave below the chord root"
    - "(chord \"Bb7\") returns the same notes as (chord \"Bf7\") (string-form charitable accidental: b→f, #→s)"
    - "(chord \"unknown_garbage\") returns Void (charitable on hopeless input — no exception, no false chord)"
    - "Existing tests/test_chords.flow continues to pass byte-identical (existing literals Cmaj, Dm, Gdom7, Cmaj7, Am7, Bdim, Caug, Dsus2, Asus4, Csmaj, Bfm and the | Cmaj7 Am7 Dm | note stream all still parse via the same ChordParser.TryParse surface)"
    - "Existing | Cmaj7 Am7 Dm | NoteStreamCompiler chord-into-stream injection continues to work — ChordParser changes preserve all currently-recognized symbols"
  artifacts:
    - path: "flow-lang/StandardLibrary/Harmony/ChordParser.cs"
      provides: "Comprehensive chord-symbol parser covering 5/6/7/9/11/13/maj/m/dim/aug/sus/add families plus alterations and slash bass; charitable b↔f / #↔s normalization for runtime entry point"
      contains: "TryParseFlexible, QualityIntervals expansion"
    - path: "flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs"
      provides: "(chord String) -> Chord builtin"
      contains: "registry.Register(\"chord\", ..."
    - path: "tests/test_chord_runtime.flow"
      provides: "Flow-script regression covering the truths above"
      contains: "(chord"
  key_links:
    - from: "flow-lang/Lexing/SimpleLexer.cs:642"
      to: "flow-lang/StandardLibrary/Harmony/ChordParser.cs:IsChordSymbol"
      via: "lexer dispatches chord-before-note"
      pattern: "ChordParser.IsChordSymbol"
    - from: "flow-lang/Runtime/NoteStreamCompiler.cs:747"
      to: "flow-lang/StandardLibrary/Harmony/ChordParser.cs:TryParse"
      via: "named-chord element compilation in note streams"
      pattern: "ChordParser.TryParse"
    - from: "flow-lang/StandardLibrary/Harmony/ScaleDatabase.cs:145"
      to: "flow-lang/StandardLibrary/Harmony/ChordParser.cs:TryParse"
      via: "roman numeral resolution"
      pattern: "ChordParser.TryParse"
---

<objective>
Add `(chord <String>) -> Chord` runtime constructor + comprehensively expand the parser's chord-quality vocabulary. Today chord values can only be created via bare-token literals at parse time (`Chord c = Cmaj7`); there is no way to build one from a computed/dynamic string. The same parser also has a small, hand-curated quality dictionary (~18 entries) that misses 9/11/13 extensions, alterations like `7b5`/`7#9`/`maj7#11`, slash bass `C/E`, the power-chord `5`, sus extensions like `7sus4`, etc.

Purpose (ergonomics): composers shouldn't have to memorize "is this quality registered?" or work around it — every common-practice chord symbol should round-trip. The note-stream chord-injection feature already exists (`| Cmaj7 Am7 Dm |`); broadening the vocabulary means broader expressive surface for free everywhere ChordParser is consumed (literals, `(chord)`, note streams, roman numeral resolution).

Output: One enriched parser, one new builtin, one new flow-script regression. All callers of `ChordParser.TryParse` get the expanded vocabulary automatically (NoteStreamCompiler, ScaleDatabase, lexer chord-symbol recognition).
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@./CLAUDE.md
@.planning/STATE.md

<!-- Existing chord parser (read carefully before editing): -->
<!-- flow-lang/StandardLibrary/Harmony/ChordParser.cs -->
<!-- - QualityIntervals dict at line 14 — 18 entries, hand-curated -->
<!-- - IsChordSymbol at line 76 — used by lexer for chord-before-note dispatch -->
<!-- - TryParse at line 128 — used by NoteStreamCompiler.CompileNamedChordElement, ScaleDatabase.ResolveRomanNumeral, and ChordLiteralExpression evaluation -->
<!-- - ExpandIntervals at line 185 — converts (rootSemitone, intervals[], baseOctave) → display NoteNames[] -->
<!-- - WithOctave at line 217 — chord octave override; uses QualityIntervals dict -->

<!-- Lexer interaction (DO NOT BREAK): -->
<!-- flow-lang/Lexing/SimpleLexer.cs:642 — lexer calls IsChordSymbol before TryParseNote, -->
<!-- so any new dictionary entry that collides with a NoteLiteral shape would silently -->
<!-- reroute existing notes to chords. Rules to preserve: -->
<!--   - Bare-digit qualities ("D6", "G7", "D9", "C5") MUST stay as notes — composer convention -->
<!--   - Token-form root accidental MUST remain `s` / `f` only — `Bb` / `Bb7` MUST stay as notes -->
<!--   - Alteration positions inside qualities ("Cm7b5", "C7#9") may absorb b/# because the -->
<!--     lexer already glues those characters into the identifier and there is no note ambiguity -->
<!--     once we are mid-quality (the IsAllDigits gate at line 117 still keeps bare-digit -->
<!--     qualities OUT of the chord branch) -->

<!-- Charitable runtime entry point: -->
<!-- - Add ChordParser.TryParseFlexible(string) for runtime callers — accepts both the existing -->
<!--   `s`/`f` and the common-practice `#`/`b` accidentals/alterations everywhere; tries the -->
<!--   dictionary first, then falls back to compositional decomposition (root → quality → -->
<!--   sevenths/9/11/13 → alterations → slash bass) -->
<!-- - Existing TryParse stays mostly unchanged so token-form behavior is preserved; -->
<!--   we only extend QualityIntervals with new entries that do not collide with note-literal -->
<!--   shapes (none of "9", "11", "13", "maj9", "maj11", "maj13", "m9", "m11", "m13", "5", -->
<!--   "6/9", "7sus4", "9sus4", "add9", "add11", "add13", "add2", "add4", "7f5", "7s5", -->
<!--   "7f9", "7s9", "7s11", "7f13", "9f5", "9s5", "13s11", "13f9", "13s9", "maj7s11", -->
<!--   "maj7f5", "maj9s11", "m7f9", "m9f5", "mMaj7" collide with note shapes once IsAllDigits -->
<!--   filters bare numerics) -->

<!-- (chord) builtin signature: -->
<!-- - new FunctionSignature("chord", [StringType.Instance]) -->
<!-- - Calls ChordParser.TryParseFlexible. On success → Value.Chord(...). On failure → -->
<!--   Value.Void() (matches the existing resolveNumeral charitable pattern at -->
<!--   HarmonyFunctions.cs:457-465). -->

<!-- Test format reference: tests/test_chords.flow at the project root is the canonical -->
<!-- chord regression. The new tests/test_chord_runtime.flow follows the same shape: -->
<!-- `use "std.flow"`, `(print (str ...))` for visual confirmation, no xUnit. -->
</context>
