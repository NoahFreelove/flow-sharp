---
phase: 19-tuplets-arbitrary-fractional-durations
plan: 01
subsystem: parser-compiler
tags: [tuplets, ast, parser, compiler, fraction-propagation, music21]
requirements_completed: [TUP-01, TUP-02, TUP-03]
dependency_graph:
  requires:
    - flow-lang/TypeSystem/Fraction.cs (Phase 18 FRAC-01, commit 2092f32)
    - flow-lang/TypeSystem/SpecialTypes/NoteType.cs DurationFraction wiring (Phase 18 FRAC-02, commit ba8534a)
  provides:
    - flow-lang/Ast/Expressions/NoteStreamExpression.cs::TupletElement
    - flow-lang/Ast/Expressions/NoteStreamExpression.cs::NoteElement.TupletRatio (defaulted; populated by Plan 19-02)
    - flow-lang/Parsing/Parser.NoteStream.cs::ParseTupletChildren
    - flow-lang/Parsing/Parser.NoteStream.cs::MusicTwentyOneShorthand
    - flow-lang/Runtime/NoteStreamCompiler.cs::CompileTupletElement
    - flow-lang/Runtime/NoteStreamCompiler.cs::SuffixToQuarterFraction
  affects:
    - All future Phase 19 plans (19-02 per-note /X:Y, 19-03 bar-fit validator, 19-04 MIDI TPQN, 19-05 augment/diminish regression)
tech_stack:
  added: []
  patterns:
    - Recursive AST descent with accumulating Fraction outerScale
    - Phase 18 defaulted-parameter migration (zero-edit call-site compatibility for NoteElement.TupletRatio)
    - Token-level recursive parser helper (ParseTupletChildren mirrors main loop dispatch shape)
    - Quarter-note-unit Fraction storage (music21 convention)
key_files:
  created:
    - flow-lang.Tests/Unit/Phase19/TupletBracketTests.cs (8 Facts, 175 lines)
  modified:
    - flow-lang/Ast/Expressions/NoteStreamExpression.cs (NoteElement +1 field, +TupletElement record)
    - flow-lang/Parsing/Parser.NoteStream.cs (+MusicTwentyOneShorthand table, +LBrace dispatch arm, +ParseTupletChildren helper)
    - flow-lang/Runtime/NoteStreamCompiler.cs (+TupletElement switch case, +CompileTupletElement, +SuffixToQuarterFraction, +CompileNamedChordElement enum-overload bridge)
decisions:
  - "Music21 shorthand table M values for counts 2/4/8/10/11 follow RESEARCH §6 Code Examples §1: 2->3, 4->6, 8->6, 10->8, 11->8. SPEC LOCKS 3->2, 5->4, 6->4, 7->4, 9->8 (5 explicit + 5 discretion = 10 entries covering counts 2-11 inclusive)."
  - "nestedOuterScale formula corrected from plan-template draft (outerScale * 1/N) to (perChildSlot = bracketSpan * 1/N). The corrected form makes outerScale always represent ONE OUTER SLOT'S quarter-size from the recursive call's POV — verified against SPEC TUP-03 expected values [2/3, 2/9, 2/9, 2/9, 2/3] quarter-units."
  - "DurationFraction stored in QUARTER-NOTE units, not whole-note units (music21 convention; matches Phase 18 18-02 pin). SPEC's '1/12 whole' prose translates to 1/3 quarter for TUP-01 acceptance."
  - "ParseTupletChildren intentionally covers only TupletElement / NoteElement / RestElement / NamedChordElement child types — sufficient for SPEC TUP-01/02/03 acceptance. Roman numerals, ghost/grace, random-choice inside tuplets are not in scope; defensive default branch in CompileTupletElement handles any future expansion without silent drops."
  - "LBrace dispatch arm placed BEFORE LBracket arm in ParseNoteStream (line ordering: tuplet group then chord group). Avoids accidental shadowing — `[...]` chord-bracket and `{...}` tuplet-bracket are visually similar but tokenize distinctly."
  - "CompileNamedChordElement enum-overload bridge added (NoteValueType.Value → NoteValueType.Value? forwarder) so CompileTupletElement can pass a non-nullable enum without explicit cast at the call site. One-line forwarder; zero behavior change to the existing nullable-Value? overload."
metrics:
  duration: ~12 min
  completed_date: 2026-04-26
  tasks_completed: 3
  files_changed: 4
  facts_added: 8
  full_suite_pre: 306
  full_suite_post: 314
---

# Phase 19 Plan 01: Tuplet Bracket Foundation Summary

**One-liner:** Bracket-form tuplet syntax `{N:M ...}q` and music21 shorthand `{N ...}q` end-to-end across AST, parser, and NoteStreamCompiler — recursive Fraction outerScale propagation activates Phase 18's dormant DurationFraction field for the first time.

## Outcome

Composer can now write triplets, quintuplets, septuplets, and nested tuplets via braces:

```flow
| {3:2 C4 D4 E4}q |              // explicit ratio: 3 in time of 2
| {3 C4 D4 E4}q |                // shorthand: 3 -> 3:2 via music21 lookup
| {5 C4 D4 E4 F4 G4}q |          // shorthand: 5 -> 5:4
| {3:2 C4 {3:2 D4 E4 F4}q G4}h | // nested: 5 notes [2/3, 2/9, 2/9, 2/9, 2/3] quarter-units
```

Each leaf note emits a `MusicalNoteData` with `DurationFraction` set to its rational quarter-note duration. Sums validate against the outer suffix exactly via `Fraction +`/`*` arithmetic — no double drift.

## TupletElement Record Shape

10th `NoteStreamElement` subtype, sibling of the 9 existing record types:

```csharp
public record TupletElement(
    SourceLocation Location,
    int Numerator,                              // "3" in 3:2, or "3" in shorthand {3 ...}
    int Denominator,                            // "2" in 3:2 (or resolved from music21 lookup)
    IReadOnlyList<NoteStreamElement> Children,  // recursive — can contain TupletElement
    string DurationSuffix,                      // w/h/q/e/s/t — REQUIRED per CONTEXT D-04
    bool IsDotted                               // outer dot ({3:2 ...}q.)
) : NoteStreamElement(Location);
```

`NoteElement` extended with a defaulted optional parameter at the END of the ctor (Phase 18 18-02 defaulted-parameter migration precedent — zero existing call-site edits required, structurally guaranteed):

```csharp
(int Num, int Denom)? TupletRatio = null    // Plan 19-02 populates from /X:Y syntax
```

## Parser Dispatch (Parser.NoteStream.cs)

**Music21 shorthand table** (private static, 10 entries):

| Count | M | Locked? | Notes |
|-------|---|---------|-------|
| 2 | 3 | discretion | duplet (2 in time of 3) |
| 3 | 2 | LOCKED | triplet (SPEC TUP-02) |
| 4 | 6 | discretion | quadruplet |
| 5 | 4 | LOCKED | quintuplet (SPEC TUP-02) |
| 6 | 4 | LOCKED | sextuplet (SPEC TUP-02) |
| 7 | 4 | LOCKED | septuplet (SPEC TUP-02) |
| 8 | 6 | discretion | octuplet |
| 9 | 8 | LOCKED | nonuplet (SPEC TUP-02) |
| 10 | 8 | discretion | |
| 11 | 8 | discretion | |

Counts ≥ 12 raise parse error: `"Tuplet shorthand {N} only supports counts 2-11 (got {n}); use explicit {N:M} form"`.

**LBrace dispatch arm** added BEFORE the existing LBracket (chord) arm in `ParseNoteStream`'s main loop. Recurses via `ParseTupletChildren()` helper that mirrors the main-loop dispatch shape but terminates on `RBrace` instead of `Pipe|EOF`. Nested tuplets handled by the helper's own LBrace arm — fully recursive descent.

Tuplet bracket without explicit duration suffix raises: `"Tuplet bracket requires explicit duration suffix"` (per CONTEXT D-04 / SPEC D-USER-04). Best-effort recovery falls back to `q` so downstream compile path stays well-defined.

## Compiler Math (NoteStreamCompiler.cs)

```
bracketSpan      = suffixFrac × outerScale          // total quarter-units this bracket spans
perChildSlot     = bracketSpan × (1 / Numerator)    // per-leaf-child duration
nestedOuterScale = perChildSlot                     // ONE outer slot's quarter-size (passed to nested)
```

`SuffixToQuarterFraction(suffix, isDotted)` maps `w=4q, h=2q, q=1q, e=1/2q, s=1/4q, t=1/8q`, with `× 3/2` if dotted. Mirrors `DurationSuffixMap` vocabulary — same 6 keys.

**Worked TUP-03 verification** for `| {3:2 C4 {3:2 D4 E4 F4}q G4}h |`:

| Step | suffixFrac | outerScale | bracketSpan | perChildSlot | Per-note |
|------|-----------|-----------|-------------|--------------|----------|
| Outer call | h=2/1 | 1/1 | 2/1 | 2/3 | C4=2/3, G4=2/3 |
| Nested call | q=1/1 | 2/3 | 2/3 | 2/9 | D4=E4=F4=2/9 |

Sum = 2·(2/3) + 3·(2/9) = 12/9 + 6/9 = 18/9 = **2 quarters = one half** ✓ matches outer `h` suffix.

In whole-note units (SPEC's prose form): 2/3 quarter = 1/6 whole, 2/9 quarter = 1/18 whole — exactly the SPEC TUP-03 acceptance pin.

## Facts Shipped (8)

All in `flow-lang.Tests/Unit/Phase19/TupletBracketTests.cs`:

| # | Test | Pinned |
|---|------|--------|
| 1 | `TripletQuarterGroup_ProducesThreeOneTwelfthNotes` | TUP-01: `{3:2 C4 D4 E4}q` → 3× Fraction(1,3) quarter-units (= 1/12 whole) |
| 2 | `ShorthandThree_EquivalentToThreeTwo` | TUP-02: `{3 ...}q` byte-identical DurationFraction sequence to `{3:2 ...}q` |
| 3 | `ShorthandFive_LookupTableLocked` | TUP-02 LOCKED: `{5 ...}q` → 5× Fraction(1,5) (5:4 mapping) |
| 4 | `ShorthandSeven_LookupTableLocked` | TUP-02 LOCKED: `{7 ...}q` → 7× Fraction(1,7) (7:4 mapping) |
| 5 | `ShorthandTwelve_RaisesParseError` | TUP-02: `{12 ...}q` → parse error containing "counts 2-11" |
| 6 | `TupletWithoutDurationSuffix_RaisesParseError` | CONTEXT D-04: `{3:2 C4 D4 E4}` → parse error "Tuplet bracket requires explicit duration suffix" |
| 7 | `NestedTriplet_OuterAndInnerComposeViaScaleAccumulation` | TUP-03: 5 notes pinned [2/3, 2/9, 2/9, 2/9, 2/3] quarter-units; sum = 2.0 quarter beats |
| 8 | `NoteStreamCompiler_NonTupletPath_DurationFractionStaysNull` | Phase 18 dormancy regression gate — `\| C4q D4q \|` produces null DurationFraction |

## Phase 18 Byte-Identical Regression Gate

**HELD.** `dotnet test --filter "FullyQualifiedName~Phase18"` reports **19/19 passed** post-commit. ByteIdenticalTutorialTests + ByteIdenticalShowcaseTests + MusicalNoteDataTests + FractionTests all green. The new compiler dispatch does NOT leak DurationFraction into non-tuplet output paths — Fact #8 above is the structural pin guaranteeing this.

Full suite: **314/314 passed** = 306 pre-Phase-19 baseline + 8 new Phase19 Facts.

## Pre-landing Collision Grep Transcript

Recorded in `19-01-PLAN.md` body before commit. Summary:

- Grep #1 (`{` inside note streams): **empty** ✓ — zero conflicts
- Grep #2 (`/N` shadows): hits inside string literals + block-comment "Note:" prose only — informational, does not block
- Grep #3 (LBrace/RBrace tokens): confirmed at `flow-lang/Lexing/SimpleLexer.cs:111-112`
- Grep #4 (Phase 18 commits): both `2092f32` (FRAC-01) and `ba8534a` (FRAC-02) present in HEAD ancestor history ✓

## Deviations from Plan

**Auto-fixed Issues**

**1. [Rule 3 - Blocking] Test harness `Parser.ParseProgram()` does not exist**
- **Found during:** Task 3 build
- **Issue:** Plan-template Fact harness called `parser.ParseProgram()`; actual Parser API method is `parser.Parse()` returning `Program`.
- **Fix:** Renamed both call sites in `TupletBracketTests.cs` (`CompileNoteStream` + `TryCompileNoteStream`).
- **Files modified:** `flow-lang.Tests/Unit/Phase19/TupletBracketTests.cs` (2 sites, replace_all)
- **Commit:** `a7f94ef` (bundled into atomic commit)

**2. [Rule 3 - Blocking] CompileTupletElement called CompileNamedChordElement with NoteValueType.Value (non-nullable), but the existing helper takes NoteValueType.Value? (nullable)**
- **Found during:** Task 3 (anticipated during Step 1 Edit C authorship)
- **Issue:** Plan template called `CompileNamedChordElement(namedChord, NoteValueType.Value.QUARTER)` but the existing signature is `(NamedChordElement, NoteValueType.Value?)`. Without an overload, the literal `NoteValueType.Value.QUARTER` would still implicitly convert via nullable widening, but adding a forwarder makes the intent explicit and avoids future ambiguity.
- **Fix:** Added one-line non-nullable forwarder: `private List<MusicalNoteData> CompileNamedChordElement(NamedChordElement namedChord, NoteValueType.Value defaultValue) => CompileNamedChordElement(namedChord, (NoteValueType.Value?)defaultValue);`
- **Files modified:** `flow-lang/Runtime/NoteStreamCompiler.cs` (1 method, ~3 lines)
- **Commit:** `a7f94ef`
- **Severity:** Minor — preserves existing dispatch path; bridges the typed enum-arg call site without altering behavior.

**3. [Rule 1 - Bug] Plan-template `nestedOuterScale = outerScale × (1 / Numerator)` math wrong for nested tuplets**
- **Found during:** Plan-template review during Step 1 authorship
- **Issue:** Plan template originally specified `Fraction nestedOuterScale = outerScale * new Fraction(1, tuplet.Numerator);` — but for `| {3:2 C4 {3:2 D4 E4 F4}q G4}h |` this yields nested perChildSlot = (1/3 * 1/1) * 1/3 = 1/9 quarter, NOT the SPEC-pinned 2/9 quarter (1/18 whole).
- **Fix:** Plan template self-corrected during the math walk-through: replace with `Fraction nestedOuterScale = perChildSlot;` (= bracketSpan / Numerator). With this, nested call sees outerScale = 2/3 quarter (the outer slot's actual quarter-size), so nested bracketSpan = 1/1 * 2/3 = 2/3, perChildSlot = 2/3 * 1/3 = 2/9 ✓.
- **Files modified:** `flow-lang/Runtime/NoteStreamCompiler.cs` (CompileTupletElement, 2 lines)
- **Commit:** `a7f94ef`
- **Severity:** Significant — caught at plan-authorship time, never landed broken. Test 7 (`NestedTriplet_OuterAndInnerComposeViaScaleAccumulation`) is the regression gate.

No Rule 4 architectural decisions. No auth gates. No bugs surfacing in unrelated files (out-of-scope detection clean).

## Atomic Commit

**`a7f94ef`** — `feat(19-01): TUP-01/02/03 tuplet bracket {N:M ...}q + AST + compiler`

Files: 4 (3 modified + 1 created)
Insertions: 511 lines
Deletions: 2 lines
No accidental file deletions (post-commit `git diff --diff-filter=D` empty).

## Phase 19 Forward-Readiness

This plan unlocks the entire Phase 19 wave structure (per CONTEXT D-08):

- **Plan 19-02 (TUP-04 + TUP-08):** `NoteElement.TupletRatio` field already present (defaulted-null), needs only the lexer/parser hookup for `C4/N` and `C4/X:Y[suffix]` syntax. Compiler already knows how to read DurationFraction from any MusicalNoteData.
- **Plan 19-03 (TUP-05 bar-fit validator):** Walks `TupletElement` tree summing `DurationFraction` values; Fraction +/× arithmetic from Phase 18 supports this directly.
- **Plan 19-04 (TUP-06 MIDI TPQN):** Walks `TupletElement` tree collecting `Denominator` values into `union(tuplet_denominators)` for LCM math; the AST shape exposes `Denominator` directly.
- **Plan 19-05 (TUP-07 augment/diminish regression):** Doubles/halves `DurationFraction` via Fraction `*` operator; relies on Phase 18 + this plan's compiler dispatch shipping rational durations end-to-end.

## Self-Check: PASSED

- ✓ `flow-lang/Ast/Expressions/NoteStreamExpression.cs` exists, has `TupletElement` record (1 hit) and `NoteElement.TupletRatio` field (1 hit)
- ✓ `flow-lang/Parsing/Parser.NoteStream.cs` exists, has `MusicTwentyOneShorthand` (3 refs), 5 LOCKED entries (3,5,6,7,9 each 1 hit), `ParseTupletChildren` method (1 hit)
- ✓ `flow-lang/Runtime/NoteStreamCompiler.cs` exists, has `case TupletElement tuplet` (1 hit), `CompileTupletElement` (1 hit), `SuffixToQuarterFraction` (1 hit), `using FlowLang.TypeSystem;` (1 hit)
- ✓ `flow-lang.Tests/Unit/Phase19/TupletBracketTests.cs` exists, namespace correct (1 hit), 8 [Fact] attributes (8 hits)
- ✓ Commit `a7f94ef` exists in HEAD: `git log --oneline -1` returns `a7f94ef feat(19-01): TUP-01/02/03 ...` ✓
- ✓ Build clean: 0 errors
- ✓ Phase19.TupletBracketTests: 8/8 passed
- ✓ Phase18: 19/19 passed (byte-identical contract held)
- ✓ Full suite: 314/314 passed (= 306 + 8)

---

*Phase: 19-tuplets-arbitrary-fractional-durations*
*Plan: 19-01*
*Atomic commit: a7f94ef*
*Completed: 2026-04-26*
