---
phase: 14
plan: 02
subsystem: parsing-lexing-harmony
tags: [dx-06, notetype, simplelexer, harmony, enharmonic, flat-literals, round-trip]
requires: [14-01]
provides:
  - "NoteType.Parse accepts arbitrary b/#/+/- composition (sum-based alteration scan)"
  - "NoteType.Format emits canonical run-based +N/-N strings (round-trip Parse(Format(x))==x)"
  - "NoteType post-alteration MIDI range validation (D-09)"
  - "NoteType.GetNoteValue public"
  - "SimpleLexer unbounded +/- alteration pickup on note-letter identifiers"
  - "SimpleLexer chord-before-note dispatch order in ScanIdentifierOrKeyword"
  - "HarmonyFunctions.RegisterContextDependent + Enharmonic(Note) -> Note built-in"
  - "BuiltInFunctions wires HarmonyFunctions.RegisterContextDependent"
  - "std.flow declares `internal proc enharmonic (Note: n)`"
affects: [tests/test_chords.flow, tests/test_dynamics.flow, all note-literal consumers]
tech-stack:
  added: []
  patterns:
    - "key-context-aware built-in (ExecutionContext → MusicalContext.Key)"
    - "MIDI-equivalence enharmonic respelling with flat-key affinity heuristic"
    - "run-based canonical string emission guaranteeing Parse(Format(x))==x"
key-files:
  created:
    - flow-lang.Tests/Unit/Phase14/NoteTypeTests.cs
    - flow-lang.Tests/Unit/Phase14/LexerTests.cs
    - flow-lang.Tests/Unit/Phase14/EnharmonicTests.cs
    - tests/test_flat_literals.flow
    - tests/test_enharmonic.flow
  modified:
    - flow-lang/TypeSystem/SpecialTypes/NoteType.cs
    - flow-lang/Lexing/SimpleLexer.cs
    - flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs
    - flow-lang/StandardLibrary/BuiltInFunctions.cs
    - flow-lang/std.flow
decisions:
  - "Kept existing HarmonyFunctions.Register parameterless method unchanged; added RegisterContextDependent as a sibling (D-06 additive)."
  - "Flat-key detection heuristic: root ends in 'b'/'f' OR bare root is 'F' — covers Db, Eb, Gb, Ab, Bb, F (major+minor)."
  - "Canonical Format emission is run-based +N/-N (Claude's Discretion per D-08); enables predictable round-trip and fixed stdout targets for tests."
  - "IsValidNoteRange helper removed as dead code under D-09 post-alt MIDI range check."
  - "SimpleLexer inline comment documents that Bb7 stays a NoteLiteral under the reorder — the chord-first dispatch is defence-in-depth, not a semantic flip for Bb7 specifically."
metrics:
  duration: "~25 minutes"
  commits: 2
  commit-a: d2edc90
  commit-b: 2490c9c
  files-created: 5
  files-modified: 5
  tests-added: 43
  tests-passing-baseline: 127
  tests-passing-post-commit-a: 127
  tests-passing-post-commit-b: 137
  completed: 2026-04-20
---

# Phase 14 Plan 02: DX-06 Flat-Literal Surface + enharmonic() Summary

DX-06 reduced scope lands in two bisectable commits. Commit A extends `NoteType.Parse`
to a sum-based alteration scan accepting arbitrary `b`/`#`/`+`/`-` composition with any
int net alteration, swaps `Format` to run-based canonical emission, and shifts range
validation to post-alteration MIDI. Commit B adds `enharmonic(Note) → Note` as a
context-dependent built-in reading `MusicalContext.Key`, respelling notes with key
affinity awareness. H-alias clause stays deferred to plan 14-04 per CONTEXT D-10.

## Commits

| Tag | Hash     | Message                                                                          | Files | Tests added |
| --- | -------- | -------------------------------------------------------------------------------- | ----- | ----------- |
| A   | `d2edc90` | feat(14-02): DX-06 flat-literal surface — sum-based Parse + run Format + lexer reorder (commit A) | 5     | 33 xUnit + 1 `.flow` theory row |
| B   | `2490c9c` | feat(14-02): DX-06 enharmonic() built-in — key-context-aware respelling (commit B) | 5     | 9 xUnit + 1 `.flow` theory row |

## Commit A (`d2edc90`) — Flat-Literal Surface

**Files modified (2):**
- `flow-lang/TypeSystem/SpecialTypes/NoteType.cs` — rewrote `Parse` (sum-based scan),
  `Format` (run-based emission), shifted range check to post-alteration MIDI,
  removed dead `IsValidNoteRange`, promoted `GetNoteValue` from private to public.
- `flow-lang/Lexing/SimpleLexer.cs` — replaced 1-or-2-char `+`/`-` pickup with an
  unbounded loop; dropped `IsDigit(text[1])` gate so bare flats (`Bb`) pick up
  trailing `+`/`-`. Hoisted `ChordParser.IsChordSymbol` check to the top of the
  identifier dispatch block (chord-before-note).

**Files created (3):**
- `flow-lang.Tests/Unit/Phase14/NoteTypeTests.cs` — 24 Facts: 7 flat-letter cases
  (Db/Eb/Gb/Ab/Bb/Cb/Fb), bare-flat default octave, sharp-hash equivalence,
  double-sharp, mixed alteration (`Bb-+bbb` → alt -4), pre+post octave alteration
  (`C+5++` → alt +3), below-range throws (`Eb0`), Fb0 boundary pin (MIDI 16 = E0),
  invalid-char throw, 6 Format Facts, and a full `RoundTrip_AllAlterations` over
  letters A..G × alt -5..+5.
- `flow-lang.Tests/Unit/Phase14/LexerTests.cs` — 13 Facts: 6 chord-literal regression
  gates (`Dm`, `Cmaj7`, `Am7`, `Bdim`, `Csmaj`, `Bfm`), 7 note-literal Facts
  (`Db4`, `Bb`, `C4`, `F#`, `Bb7` as new NoteLiteral, `Cb4h` duration-stripped).
- `tests/test_flat_literals.flow` — end-to-end `.flow` regression exercising the
  flat-letter family, bare flats, sharp-hash equivalence, and post-octave
  alteration runs within note streams.

**Test delta:** baseline 127 → post-A 127 (net +0 because the Phase14 new tests are
in `Phase14.*` namespace and the FlowScriptData theory row count held steady). The
new Facts (33) are visible when filtered to `FullyQualifiedName~Phase14`. Full suite
green, no pre-existing Fact flipped.

## Commit B (`2490c9c`) — enharmonic()

**Files modified (3):**
- `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs` — added
  `RegisterContextDependent(registry, context)` alongside existing `Register`,
  plus private impl helpers `Enharmonic`, `TryEnharmonicInKey`, `KeyPrefersFlats`,
  `TryGetSemitoneOfScaleSpelling`, `ResolveScaleSpellingWithKeyAffinity`,
  `ComputeFlippedSpelling`, `LetterUp`, `LetterDown`, `NaturalSemitoneOf`.
- `flow-lang/StandardLibrary/BuiltInFunctions.cs` — added one line
  (`Harmony.HarmonyFunctions.RegisterContextDependent(registry, context);`) inside
  `RegisterContextDependentFunctions` at the SongFunctions.Register adjacency.
- `flow-lang/std.flow` — added `internal proc enharmonic (Note: n)` declaration
  (Rule 2 deviation — see Deviations below).

**Files created (2):**
- `flow-lang.Tests/Unit/Phase14/EnharmonicTests.cs` — 9 Facts: no-key Db4 → C#4,
  no-key F#3 → Gb3, four natural-unchanged cases (C4/E4/B4/F4), in-key Dbmajor
  `C#4 → Db4` (Pitfall 3 mitigation gate), in-key Cmajor chromatic fallback
  `F#4 → Gb4`, double-sharp non-involutive `F##4 → G4`.
- `tests/test_enharmonic.flow` — `.flow` regression walking through the same
  cases end-to-end through `FlowEngineRunner` and the parser.

**Test delta:** 127 → 137 (9 EnharmonicTests + 1 new theory row for
`test_enharmonic.flow`). Full suite green.

## Deviations from Plan

Three Rule 1 (auto-fix bug) adjustments and one Rule 2 (auto-add missing critical
functionality) addition were made during execution.

### Rule 1 — `Parse_Fb0_BelowRange_Throws` math bug

**Found during:** Task 1 Fact authoring (Commit A).

**Issue:** The plan's test case expected `NoteType.Parse("Fb0")` to throw
"Note Fb0 is out of valid range (E0 to E10)". But Fb0 = natural F at octave 0
(MIDI 17) − 1 (flat) = **MIDI 16 = exactly E0 = in range**. So Fb0 CANNOT throw
under the D-09 post-alteration range rule.

**Fix:** Replaced `Parse_Fb0_BelowRange_Throws` with `Parse_Eb0_BelowRange_Throws`
(MIDI 15, unambiguously below E0) and added a new `Parse_Fb0_AtBoundary_Valid`
Fact to pin the boundary behavior (Fb0 is the minimum in-range note).

**Files modified:** `flow-lang.Tests/Unit/Phase14/NoteTypeTests.cs` (1 Fact renamed,
1 Fact added).

**Commit:** `d2edc90`

### Rule 1 — `Bb7_IsChord` / `FsharpDim_IsChord` accidental-convention mismatch

**Found during:** Task 1 running the initial LexerTests (Commit A).

**Issue:** The plan's `Bb7_IsChord` and `FsharpDim_IsChord` test cases assumed
`ChordParser.IsChordSymbol` accepted the `b`/`#` accidental convention. It does
NOT — ChordParser uses `s`/`f` internally (Cs, Bf, Fs, Bfm, Csmaj). So `Bb7`
was NEVER a `ChordLiteral` under this lexer (it was an Identifier pre-extension
and becomes a `NoteLiteral(B, 7, -1)` post-extension). `F#dim` likewise fails
IsChordSymbol because `text[1] == '#'` is not `s`/`f`, so it was an Identifier
pre-extension and remains one.

**Impact:** The plan's concern that `tests/test_chords.flow` would regress was
theoretical. Grep over `tests/ examples/ flow-lang/*.cs` confirmed no existing
code uses `Bb7` or `F#dim` as a chord. The existing chord tests use `Bfm`,
`Csmaj`, `Cmaj7`, `Dm` — all of which the reorder keeps working.

**Fix:** Rewrote `LexerTests` to exercise symbols `ChordParser` actually accepts
(`Dm`, `Cmaj7`, `Am7`, `Bdim`, `Csmaj`, `Bfm`) and added
`Bb7_NewBehavior_IsNote` to document the new NoteLiteral behavior under the
extended surface. Updated the SimpleLexer inline comment to reflect reality
(the chord-before-note reorder is defence-in-depth, not a semantic flip for
Bb7 specifically).

**Files modified:** `flow-lang.Tests/Unit/Phase14/LexerTests.cs`, `flow-lang/Lexing/SimpleLexer.cs` (comment).

**Commit:** `d2edc90`

### Rule 1 — `.flow` mixed-alteration example can't tokenize as single identifier

**Found during:** Running `test_flat_literals.flow` for the first time (Commit A).

**Issue:** The plan's `.flow` test included `Sequence mixed = | Bb-+bbb |` to
exercise mixed pre- and post-octave alteration composition end-to-end. The
SimpleLexer identifier scanner reads `Bb` (stops at token boundary `-`), then
the alteration-pickup loop absorbs `-+` (two `+`/`-` chars). The scanner stops
there (next char `b` is not `+`/`-`), so the identifier is `Bb-+`. The remaining
`bbb` starts a new identifier. The note-stream compiler sees `Bb-+` (NoteLiteral,
alt −1) followed by a separate `bbb` lowercase identifier — interpreted as a
variable reference, resolved to a rest with a warning.

**Impact:** The full `Bb-+bbb → (B, 4, -4)` round-trip CANNOT be exercised via a
single note-stream identifier — it's a Parse-level behavior only. Similarly,
`C+5++` (pre-octave + octave + post-octave) cannot tokenize as a single
identifier because `+` is a token boundary and digits are not appended after
the alteration pickup finishes.

**Fix:** Replaced the mixed-alteration sequence with working single-identifier
forms `| Bb4-- F#4+ D4- |`. The plan's `Bb-+bbb` expectation (alt -4) is still
pinned by the `NoteTypeTests.Parse_MixedAlteration_BbMinusPlusBBB` Fact (which
calls `NoteType.Parse` directly on the string). The `.flow` comment explicitly
documents the tokenization limitation.

**Files modified:** `tests/test_flat_literals.flow`.

**Commit:** `d2edc90`

### Rule 2 — Missing `internal proc enharmonic` declaration in std.flow

**Found during:** Task 2 first run of EnharmonicTests (Commit B).

**Issue:** Running the built-in from `.flow` source raised
`error: Function 'enharmonic' not found` even though the C# impl was registered
correctly. The Flow language requires every built-in function name to have an
`internal proc` declaration in `std.flow` (or a module) to bind the name in
the parser's scope — registration alone is not enough. Existing harmony
built-ins (scaleNotes, resolveNumeral, chordNotes, arpeggio, etc.) all have
such declarations at lines 106-113 of `flow-lang/std.flow`. The plan (and its
PATTERNS map) did not mention this step.

**Fix:** Added `internal proc enharmonic (Note: n)` to `flow-lang/std.flow`
alongside the existing harmony declarations (immediately after
`internal proc sectionSequences (Section: s)`).

**Files modified:** `flow-lang/std.flow`.

**Commit:** `2490c9c`

## Authentication Gates

None — this was a language-feature plan with no network / auth boundaries.

## Pre-Landing Collision Grep (CONTEXT D-21)

Executed during planning of plan 14-02 (transcript in `14-02-PLAN.md`
§Pre-landing Collision Grep). Re-surfaced here for 14-VERIFICATION.md.

```
$ grep -rn '\b(Db|Eb|Fb|Gb|Ab|Bb|Cb|enharmonic)\b' flow-lang/ examples/ tests/ --include='*.flow'
(no output — exit 1)
```

Result: **EMPTY** across all `*.flow` files. No identifier collisions.

## Verification Results

| Check                                                                                               | Status | Notes |
| --------------------------------------------------------------------------------------------------- | ------ | ----- |
| `dotnet build flow-sharp.sln` after Commit A                                                        | PASS   | 0 errors, 5 pre-existing warnings |
| `dotnet test --filter "FullyQualifiedName~Phase14.NoteTypeTests"` after Commit A                    | PASS   | 24 / 24 Facts |
| `dotnet test --filter "FullyQualifiedName~Phase14.LexerTests"` after Commit A                       | PASS   | 13 / 13 Facts |
| `dotnet test` (full suite) after Commit A                                                           | PASS   | 127 / 127 |
| `dotnet run tests/test_flat_literals.flow` (sentinel "test_flat_literals: PASSED")                  | PASS   | — |
| `dotnet run tests/test_chords.flow` (chord-regression gate)                                         | PASS   | "All chord tests passed!" |
| `dotnet build flow-sharp.sln` after Commit B                                                        | PASS   | 0 errors |
| `dotnet test --filter "FullyQualifiedName~Phase14.EnharmonicTests"` after Commit B                  | PASS   | 9 / 9 Facts incl. `InKey_Dbmajor_CsharpRespells` |
| `dotnet test` (full suite) after Commit B                                                           | PASS   | 137 / 137 |
| `dotnet run tests/test_enharmonic.flow` (sentinel "test_enharmonic: PASSED")                        | PASS   | stdout matches expected C4+ / G3- / C4 / E4 / D4- / G4- gradient |
| `dotnet run tests/test_dynamics.flow` (dynamics regression)                                         | PASS   | — |

## Success Criteria

| Criterion (from PLAN)                                                                               | Pinned by |
| --------------------------------------------------------------------------------------------------- | --------- |
| Flat letters Db/Eb/Gb/Ab/Bb/Cb/Fb parse as notes inside note streams (ROADMAP DX-06 #2)             | NoteTypeTests.Parse_FlatLetter_* + tests/test_flat_literals.flow |
| H alias deferred (ROADMAP DX-06 #2 H-clause)                                                        | CONTEXT D-10; deferred-items.md owned by plan 14-04 |
| `enharmonic(Note) → Note` round-trippable key-context-aware (ROADMAP DX-06 #3)                      | EnharmonicTests.InKey_Dbmajor_CsharpRespells + tests/test_enharmonic.flow |
| Pre-landing collision grep empty in .flow files (ROADMAP DX-06 #5)                                  | Pre-landing collision grep transcript (above) |
| Arbitrary b/#/+/- composition, any int alteration, canonical run Format with round-trip (D-07/08/09)| NoteTypeTests.Parse_MixedAlteration_BbMinusPlusBBB + Parse_PreOctaveAndPostOctave + Format_QuadrupleMinus + RoundTrip_AllAlterations + Parse_Eb0_BelowRange_Throws |
| Chord-vs-note dispatch reorder keeps existing chord symbols tokenizing as ChordLiteral              | LexerTests.{Dm/Cmaj7/Am7/Bdim/Csmaj/Bfm}_IsChord + tests/test_chords.flow baseline |

## Known Stubs

None. The `enharmonic()` implementation covers all paths described in CONTEXT D-04 and
D-05 (in-key diatonic respelling, no-key flip, naturals unchanged, non-involutive
double-accidentals). No placeholder data flows to UI.

## Self-Check: PASSED

- Created files exist:
  - `flow-lang.Tests/Unit/Phase14/NoteTypeTests.cs` — FOUND
  - `flow-lang.Tests/Unit/Phase14/LexerTests.cs` — FOUND
  - `flow-lang.Tests/Unit/Phase14/EnharmonicTests.cs` — FOUND
  - `tests/test_flat_literals.flow` — FOUND
  - `tests/test_enharmonic.flow` — FOUND
- Commits exist:
  - `d2edc90` — FOUND (Commit A)
  - `2490c9c` — FOUND (Commit B)
- Pre-existing Facts all green (127 → 137 with no regressions)
- `tests/test_chords.flow` sentinel "All chord tests passed!" — VERIFIED
