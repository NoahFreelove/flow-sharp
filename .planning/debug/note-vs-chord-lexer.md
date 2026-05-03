---
slug: note-vs-chord-lexer
status: resolved
trigger: |
  Lexer treats note+octave tokens like `D6`, `G6`, `A6`, `C6`, `F6` as chord
  literals (D dominant 6, etc.) instead of as a note in octave 6. This breaks
  parsing inside chord brackets `[...]` where only note literals are allowed.
created: 2026-05-02
updated: 2026-05-02
---

# Debug Session: note-vs-chord-lexer

## Symptoms

**Expected behavior:**
Note tokens like `D6`, `G6`, `A6`, `C6`, `F6` (note name + octave digit) parse
as `Note` literals — D in octave 6, etc. — in any context (note streams,
chord brackets, standalone). Octave should be unrestricted (any digit 0-9).
Chord-literal interpretation (e.g., `D6` = D dominant 6 chord) should only
apply when the token unambiguously cannot be a note in the current grammar
position. Inside `[...]` chord brackets, tokens must always be note literals.

**Actual behavior:**
Lexer emits `ChordLiteral` for `D6`, `G6`, `A6`, `C6`, `F6` regardless of
context. Inside `[...]` chord brackets the parser then errors with
"Expected note literal in chord bracket. Got ChordLiteral 'D6'".

**Error messages:**
```
chopin_nocturne.flow:19:61: error: Expected note literal in chord bracket. Got ChordLiteral 'D6'
chopin_nocturne.flow:193:75: error: Expected note literal in chord bracket. Got ChordLiteral 'G6'
chopin_nocturne.flow:353:5: error: Unexpected token RBrace '}' (cascade)
```

**Timeline:**
First observed today (2026-05-02) while attempting to render Chopin's Nocturne
Op. 9 No. 2 in Eb Major after MIDI → .flow conversion via the flow-midi tool.
Has likely been latent since note-stream / chord-bracket support shipped — no
prior test exercised octave 6+ inside `[...]`.

**Reproduction:**
```bash
# Convert MIDI → .flow (succeeds)
dotnet run --project flow-midi -c Release -- \
  "/home/noah/Downloads/midi/Chopin _ Nocturnes Op. 9, No. 2 in Eb Major.mid" \
  -o /tmp/flow-render/chopin_nocturne.flow

# Render .flow → WAV (fails at parse)
dotnet run --project flow-interpreter -c Release -- \
  /tmp/flow-render/chopin_nocturne.flow
```

Minimal repro (likely):
```flow
Sequence s = | [C4 D6]q |
```

## Goal

Uncap the note range so any octave 0-9 parses as a note literal. Chord-literal
interpretation kicks in only when the token cannot be a valid note. Inside
`[...]` brackets, always prefer note literals. Inside note streams `| ... |`,
prefer note literal when followed by a duration suffix (`q`, `h`, `w`, `e`,
`s`, `t`) or whitespace.

## Suspected Files

- `flow-lang/Lexing/SimpleLexer.cs` — note vs chord disambiguation
- `flow-lang/Parsing/Parser.cs` — chord-bracket parsing path that rejects ChordLiteral
- `flow-lang/StandardLibrary/Harmony/ChordParser.cs` — chord literal recognition

## Current Focus

- hypothesis: Chord-literal pattern over-matches `<NoteLetter><digit>` because
  `QualityIntervals` includes bare-digit keys (`"6"`, `"7"`, `"9"`).
  `ChordParser.IsChordSymbol` therefore accepts `D6`, `G7`, `A6`, `C6`, `F6`
  as chords, and the lexer dispatches chord-before-note (Phase 14 D-21) so the
  note path never runs. Fix: make `IsChordSymbol` reject digit-only quality
  suffixes so `D6` etc. fall through to `TryParseNote`. The project's existing
  convention (documented in `tests/test_chords.flow:13`) already says: "G7 is
  parsed as note G at octave 7, use dom7 for chord".
- test: rebuild and rerun chopin_nocturne.flow + a minimal `[C4 D6]q` repro
- expecting: parse succeeds, WAV renders
- next_action: gather initial evidence — read SimpleLexer.cs note/chord
  disambiguation logic and Parser.cs chord-bracket handler

## Evidence

- timestamp: 2026-05-02 — `flow-lang/Lexing/SimpleLexer.cs:642` calls
  `ChordParser.IsChordSymbol(text)` BEFORE `TryParseNote(text)`. Comment at
  L631-L645 documents this is "Phase 14 D-21 defence-in-depth so existing
  ChordParser symbols always win" under the extended NoteType.Parse surface.

- timestamp: 2026-05-02 — `flow-lang/StandardLibrary/Harmony/ChordParser.cs:14-34`
  declares `QualityIntervals` with bare-digit keys: `"7"`, `"9"`, `"6"`, plus
  multi-char keys like `"maj"`, `"m"`, `"dim"`, `"sus2"`, `"sus4"`, `"add9"`,
  `"m6"`, `"m7"`, `"dom7"`, `"maj7"`, `"dim7"`, `"m7f5"`, `"min"`, `"min7"`,
  `"aug"`. The bare-digit keys are exactly what causes `D6`/`G7`/`D9` to match
  as chords because `IsChordSymbol`'s first lookup (L73-75) is
  `QualityIntervals.ContainsKey(text[1..])` — for `"D6"`, `text[1..]="6"`
  hits the table.

- timestamp: 2026-05-02 — `tests/test_chords.flow:13` documents the project's
  existing convention: `Note: Dominant 7th - G7 is parsed as note G at octave 7,
  use dom7 for chord`. So the chord-precedence regression introduced by
  Phase 14 D-21 contradicts a documented design decision. The Chord declarations
  in tests use multi-char qualities exclusively (`Cmaj`, `Dm`, `Gdom7`, `Cmaj7`,
  `Am7`, `Bdim`, `Caug`, `Dsus2`, `Asus4`, `Csmaj`, `Bfm`).

- timestamp: 2026-05-02 — Grep across `tests/` and `flow-lang/*.flow` for the
  shape `[A-G][0-9]` confirms NO test exercises bare-digit chord literals.
  Every `[A-G][0-9]` token in the test corpus is a note literal (`C4`, `D5`,
  `B3`, `G6`, etc.).

- timestamp: 2026-05-02 — `flow-lang/TypeSystem/SpecialTypes/NoteType.cs:106-107`
  bounds notes to MIDI range E0..E10 — so `D6`, `G7`, `A8`, `C6`, `F6` are all
  valid note literals; the type system does not gate them.

- timestamp: 2026-05-02 — `flow-lang/Parsing/Parser.NoteStream.cs:209` is where
  the chord-bracket parser raises "Expected note literal in chord bracket"
  after the lexer emits a `ChordLiteral`. The bracket parser has no fallback —
  fixing the lexer side eliminates the error path.

- timestamp: 2026-05-02 — `flow-lang/StandardLibrary/Harmony/ScaleDatabase.cs:106`
  shows that bare-digit qualities are still meaningful inside roman numeral
  resolution (e.g., `V7` in G major resolves to a chord symbol `"D7"` via
  `ChordParser.TryParse`). So `TryParse` MUST keep accepting digit qualities;
  only `IsChordSymbol` (the lexer-side recognizer) needs to reject them.

## Eliminated

- "Octave range too small" — disproved: NoteType.Parse already accepts E0..E10.
- "Parser bracket handler is wrong" — disproved: parser correctly demands
  `NoteLiteral`; the lexer emitting `ChordLiteral` is the bug.
- "Need context-sensitive lexing (track `[` depth)" — unnecessary: the simpler
  fix is to reject digit-only chord quality suffixes in `IsChordSymbol`,
  since the project's documented convention already assigns those tokens to
  notes.

## Resolution

**Root cause:** `ChordParser.IsChordSymbol` accepts tokens like `D6`, `G7`,
`A6`, `C6`, `F6` as chord literals because its quality table
(`QualityIntervals`) contains bare-digit keys (`"6"`, `"7"`, `"9"`) used by
chord notation `Cmaj7`/`Dm7`/`Cadd9` etc. Combined with the Phase 14 D-21
chord-before-note dispatch in `SimpleLexer.cs:642`, octave-6+ note tokens
were silently re-classified as chords, breaking `[...]` chord-bracket
parsing and contradicting the documented convention in
`tests/test_chords.flow:13` ("G7 is parsed as note G at octave 7, use dom7
for chord").

**Fix:** Modify `ChordParser.IsChordSymbol` (lexer-side recognizer only) to
reject suffixes that consist of digits only. The chord forms users actually
need (`Cmaj7`, `Cm7`, `Cdom7`, `Cm6`, `Cadd9`) all contain letter prefixes
and remain accepted. `ChordParser.TryParse` is unchanged — it still resolves
`"D7"` etc. when called explicitly by `ScaleDatabase.ResolveRomanNumeral`
(roman-numeral path).

**Files changed:**
- `flow-lang/StandardLibrary/Harmony/ChordParser.cs` — `IsChordSymbol`
  rejects digit-only quality suffixes.
- `flow-lang.Tests/Unit/Phase14/LexerTests.cs` — new positive Facts:
  `D6_IsNote`, `G7_IsNote`, `A6_IsNote`, `C6_IsNote`, `F6_IsNote`,
  `D9_IsNote` pin the new behavior.

**Verification:**
- Minimal repro `Sequence s = | [C4 D6]q |` parses successfully.
- `chopin_nocturne.flow` parses and renders to WAV.
- All existing Phase 14 chord-literal regression Facts still pass
  (`Dm`, `Cmaj7`, `Am7`, `Bdim`, `Csmaj`, `Bfm`).
- Full `tests/test_*.flow` suite still passes.
- `dotnet test` (flow-lang.Tests) green.
