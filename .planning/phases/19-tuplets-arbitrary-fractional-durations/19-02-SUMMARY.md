---
phase: 19-tuplets-arbitrary-fractional-durations
plan: 02
subsystem: parser-compiler
tags: [tuplets, fractional-duration, per-note-shorthand, lexer-parser-disambiguation, fraction-arithmetic]
requirements_completed: [TUP-04, TUP-08]
dependency_graph:
  requires:
    - flow-lang/Ast/Expressions/NoteStreamExpression.cs::NoteElement.TupletRatio (Plan 19-01 a7f94ef)
    - flow-lang/Runtime/NoteStreamCompiler.cs::SuffixToQuarterFraction (Plan 19-01 a7f94ef)
    - flow-lang/TypeSystem/Fraction.cs (Phase 18 FRAC-01, commit 2092f32)
    - flow-lang/TypeSystem/SpecialTypes/NoteType.cs DurationFraction wiring (Phase 18 FRAC-02, commit ba8534a)
  provides:
    - flow-lang/Parsing/Parser.NoteStream.cs main NoteLiteral arm — Slash IntLiteral peek + (N,1) sentinel / (X,Y) populate
    - flow-lang/Parsing/Parser.NoteStream.cs ParseTupletChildren NoteLiteral arm — same dispatch (per-note `/X:Y` legal inside tuplet brackets)
    - flow-lang/Runtime/NoteStreamCompiler.cs::CompileNoteElement TupletRatio branch — Fraction(4,X) for TUP-04, suffix × Fraction(1,X) for TUP-08
  affects:
    - Plan 19-03 (TUP-05 bar-fit validator) — both bracket-form AND per-note paths now produce non-null DurationFraction; validator can sum uniformly
    - Plan 19-04 (TUP-06 MIDI TPQN) — per-note `/X:Y` ratios feed `union(tuplet_denominators)` alongside bracket-form ratios
    - Plan 19-05 (TUP-07 augment/diminish regression) — Fraction `*` operator from Phase 18 doubles/halves rational durations end-to-end
tech_stack:
  added: []
  patterns:
    - Parser-only lexer-vs-parser disambiguation (RESEARCH Pattern 3 + Pitfall lexer-vs-parser)
    - Sentinel encoding (N,1) for TUP-04 to share single AST field with TUP-08 (X,Y)
    - Compile-time Fraction arithmetic in quarter-note units (music21 convention)
    - Structural disambiguation between random-choice colon and per-note tuplet colon (T-19-05 mitigation)
    - Defaulted-parameter NoteElement constructor compat (Plan 19-01 added `tupletRatio = null` at end)
key_files:
  created:
    - flow-lang.Tests/Unit/Phase19/FractionalDurationTests.cs (9 Facts, 188 lines)
  modified:
    - flow-lang/Parsing/Parser.NoteStream.cs (+106 lines: main NoteLiteral arm + ParseTupletChildren NoteLiteral arm; -7 lines old form)
    - flow-lang/Runtime/NoteStreamCompiler.cs (+22 lines: CompileNoteElement TupletRatio branch + xmldoc; +1 line `durationFraction:` ctor arg)
decisions:
  - "TUP-04 (`C4/N`) encoded as TupletRatio=(N,1) internal sentinel — Y=1 is musically meaningless for a real tuplet (X notes in time of 1 = X-fold acceleration), so the (N,1) shape collides with no real tuplet ratio. User-facing `C4/3:1` is mathematically equivalent to `C4/3` and is treated identically (acceptable — SPEC does not specify behavior for Y=1 user input)."
  - "TUP-08 default suffix when absent is `q` (quarter) per SPEC TUP-08 'default level: quarter'. `note.DurationSuffix ?? \"q\"` in CompileNoteElement TUP-08 branch; matches the bracket-form behavior where omitted-suffix raises a parse error but per-note form silently defaults."
  - "ParseTupletChildren NoteLiteral arm gets the SAME `/N` + `/X:Y` dispatch as the main loop NoteLiteral arm — per-note `C4/X:Y` syntax is therefore legal inside tuplet brackets (e.g. `{3:2 C4/2 D4 E4}q` is well-defined, though musically unusual). Compiler routes via the same CompileNoteElement TupletRatio branch."
  - "Quarter-note units stored throughout (music21 + Phase 18 18-02 convention). SPEC's prose `1/12 whole` → 1/3 quarter; `1/10 whole` → 2/5 quarter (TUP-08 half-suffix acceptance)."
  - "Random-choice colon-collision regression Fact pinned via AST inspection (RandomChoiceElement vs NoteElement.TupletRatio shapes don't overlap). Disambiguation is structural: `(?` outer-paren gates random choice BEFORE Slash arm fires; `/X:Y` happens AFTER Slash. T-19-05 mitigation."
  - "Parse-error recovery uses best-effort substitution (n=1, y=1) so downstream compile path stays well-defined even when error-reporter has accumulated diagnostics. Matches Plan 19-01's `durSuffix = q` recovery for missing tuplet-bracket suffix."
metrics:
  duration: ~10 min
  completed_date: 2026-04-26
  tasks_completed: 2
  files_changed: 3
  facts_added: 9
  full_suite_pre: 314
  full_suite_post: 323
---

# Phase 19 Plan 02: Per-Note Fractional Duration + Tuplet Ratio Shorthand Summary

**One-liner:** `C4/N` arbitrary fractional-duration syntax (TUP-04) and `C4/X:Y[suffix]` per-note tuplet shorthand (TUP-08) inside note streams — parser-only extension reuses existing Slash/Colon/IntLiteral tokens, encoded as `NoteElement.TupletRatio` populated by Plan 19-01's defaulted field, dispatched at compile-time via a 17-line branch on `if (y == 1)` sentinel.

## Outcome

Composers can now write fractional durations and per-note tuplet ratios:

```flow
| C4/12 D4/12 E4/12 |          // TUP-04: 3 notes each 1/12 whole = 1/3 quarter
| C4/1 |                       // TUP-04: one whole note (= C4w equivalent)
| C4/3:2 D4/3:2 E4/3:2 |       // TUP-08: ≡ | {3:2 C4 D4 E4}q | (each 1/3 quarter)
| C4/5:4h |                    // TUP-08: 1/10 whole = 2/5 quarter (half × 1/5)
| C4/3:2q D4/5:4q E4/3:2q |    // TUP-08: mixed adjacent ratios legal (per CONTEXT D-02)
```

Parse errors fire for invalid forms:
- `| C4/0 |` → `"Duration denominator must be ≥ 1; got 0"`
- `| C4/0:2 |` → `"Tuplet ratio numerator X must be ≥ 1; got 0"`

Both bracket-form (`{N:M ...}q`) AND per-note (`/N`, `/X:Y[suffix]`) now emit `MusicalNoteData` with non-null `DurationFraction`. Plan 19-03 bar-fit validator and Plan 19-04 MIDI TPQN auto-elevation can walk both paths uniformly.

## Parser Extension Shape

**Two NoteLiteral arms updated** (main `ParseNoteStream` loop + `ParseTupletChildren` helper) — both reuse the same dispatch:

```csharp
if (Match(TokenType.Slash))
{
    var nToken = Expect(TokenType.IntLiteral, "Expected integer after '/' in note duration");
    int n = (int)nToken.Value!;

    if (Match(TokenType.Colon))
    {
        // TUP-08: C4/X:Y[suffix]
        var yToken = Expect(TokenType.IntLiteral, "Expected integer Y after ':' in per-note tuplet ratio");
        int y = (int)yToken.Value!;
        if (n < 1) { ReportError("Tuplet ratio numerator X must be ≥ 1; got {n}"); n = 1; }
        if (y < 1) { ReportError("Tuplet ratio denominator Y must be ≥ 1; got {y}"); y = 1; }
        tupletRatio = (n, y);
        overrideDurSuffix = TryParseDurationSuffix();  // optional level w/h/q/e/s/t
    }
    else
    {
        // TUP-04: C4/N
        if (n < 1) { ReportError("Duration denominator must be ≥ 1; got {n}"); n = 1; }
        tupletRatio = (n, 1);  // sentinel encoding
    }
}
```

The `(N, 1)` sentinel is internal-only — never exposed to the user. The compiler distinguishes TUP-04 from TUP-08 by `if (y == 1)`. Y=1 is musically meaningless for a real tuplet (would mean "X notes in the time of 1 = X-fold acceleration"), so reusing this shape as the TUP-04 sentinel collides with no real user-visible ratio.

## TupletRatio Encoding Contract

| Form | TupletRatio | Interpretation | Example |
|------|-------------|----------------|---------|
| `C4/N` (TUP-04) | `(N, 1)` | sentinel: fractional whole-note duration | `C4/12` → (12, 1) → 1/12 whole |
| `C4/X:Y` (TUP-08, no suffix) | `(X, Y)` | per-note tuplet member, default quarter level | `C4/3:2` → (3, 2) → 1/3 quarter |
| `C4/X:Y[s]` (TUP-08, with suffix) | `(X, Y)` + DurationSuffix=s | per-note tuplet member at level `s` | `C4/5:4h` → (5, 4) + "h" → 2/5 quarter |
| Plain note | `null` | non-fractional path (Phase 18 dormancy) | `C4q` → null |

Y is preserved on the AST for Plan 19-04 MIDI TPQN union (denominators) but does NOT enter per-note duration math (X alone determines fill).

## Compiler Branch Math

`NoteStreamCompiler.CompileNoteElement` gains a 17-line branch BEFORE the existing velocity/articulation logic:

```csharp
Fraction? durationFraction = null;
if (note.TupletRatio.HasValue)
{
    var (x, y) = note.TupletRatio.Value;
    if (y == 1)
    {
        // TUP-04: C4/N — DurationFraction = 1/N whole = 4/N quarter
        durationFraction = new Fraction(4, x);
    }
    else
    {
        // TUP-08: suffix × 1/X (default suffix "q" per SPEC)
        string suffixForFraction = note.DurationSuffix ?? "q";
        Fraction suffixFrac = SuffixToQuarterFraction(suffixForFraction, note.IsDotted);
        durationFraction = suffixFrac * new Fraction(1, x);
    }
}
// ... velocity/articulation unchanged ...
return new MusicalNoteData(..., durationFraction: durationFraction);
```

`SuffixToQuarterFraction` was added in Plan 19-01 — reused unchanged.

**Worked TUP-08 examples:**

| Source | x | y | suffix | Computation | DurationFraction |
|--------|---|---|--------|-------------|-------|
| `C4/3:2` | 3 | 2 | (default q) | (1/1) × (1/3) | Fraction(1, 3) quarter |
| `C4/5:4h` | 5 | 4 | h | (2/1) × (1/5) | Fraction(2, 5) quarter (= 1/10 whole) |
| `C4/3:2q` | 3 | 2 | q | (1/1) × (1/3) | Fraction(1, 3) quarter |
| `C4/5:4q` | 5 | 4 | q | (1/1) × (1/5) | Fraction(1, 5) quarter |

**Worked TUP-04 examples:**

| Source | x | y | Computation | DurationFraction |
|--------|---|---|-------------|-------|
| `C4/12` | 12 | 1 | Fraction(4, 12) | Fraction(1, 3) quarter |
| `C4/1` | 1 | 1 | Fraction(4, 1) | Fraction(4, 1) quarter (= one whole) |

## Facts Shipped (9)

All in `flow-lang.Tests/Unit/Phase19/FractionalDurationTests.cs`:

| # | Test | Pinned |
|---|------|--------|
| 1 | `SlashTwelve_ProducesThreeOneTwelfthNotes` | TUP-04: `C4/12 D4/12 E4/12` → 3× Fraction(1, 3) quarter; sum = 1.0 quarter beat |
| 2 | `SlashOne_ProducesWholeNote` | TUP-04: `C4/1` → Fraction(4, 1) quarter (= whole); GetBeats(4) = 4.0 |
| 3 | `SlashZero_RaisesParseError` | TUP-04: `C4/0` → parse error containing "Duration denominator must be ≥ 1" |
| 4 | `PerNoteThreeAgainstTwo_EquivalentToBracket` | TUP-08: `C4/3:2 D4/3:2 E4/3:2` byte-equal DurationFractions to `{3:2 C4 D4 E4}q`; each Fraction(1, 3) quarter |
| 5 | `PerNoteWithHalfSuffix_OneTenthWhole` | TUP-08: `C4/5:4h` → Fraction(2, 5) quarter (= 1/10 whole per SPEC) |
| 6 | `MixedRatios_AdjacentNotesLegal` | TUP-08: `C4/3:2q D4/5:4q E4/3:2q` → 3 notes (1/3, 1/5, 1/3) quarter (CONTEXT D-02 independence) |
| 7 | `PerNoteZeroNumerator_RaisesParseError` | TUP-08: `C4/0:2` → parse error containing "Tuplet ratio numerator X must be ≥ 1" |
| 8 | `RandomChoiceWeights_AndPerNoteTuplet_DoNotCollide` | T-19-05 / RESEARCH Pitfall: `(? C4:50 E4:50) D4/3:2` → 1× RandomChoiceElement (2 weighted) + 1× NoteElement(D4, TupletRatio=(3,2)) |
| 9 | `NoteStreamCompiler_NoSlashSyntax_TupletRatioStaysNull` | Phase 18 dormancy regression — `C4q D4q E4q` → all NoteElements null TupletRatio AND null DurationFraction |

## Pitfall Regression: Random-Choice Colon-Collision Pinned

Per RESEARCH §Pitfall Phase-19-specific (random-choice weight syntax `(? C4:50 E4:50)` colliding with per-note `/X:Y`):

The disambiguation is **structural** — not lexical. Both syntaxes use `Colon`, but they live in different parser contexts:

- Random choice: gated by `(?` outer-paren BEFORE the NoteLiteral arm fires. Parser dispatches into the random-choice handler at line 102 of `ParseNoteStream`, which consumes its own `Colon` between note and weight.
- Per-note `/X:Y`: gated by `Slash` AFTER NoteLiteral. The `Colon` here is consumed by the new TUP-08 branch ONLY after a `Slash IntLiteral` peek succeeded.

AST shapes don't overlap: `RandomChoiceElement` has `IReadOnlyList<(string Note, int? Weight)> Choices`; `NoteElement` has `(int Num, int Denom)? TupletRatio`. Test 8 pins both elements coexisting in one bar without parser ambiguity.

## Phase 18 Byte-Identical Regression Gate

**HELD.** `dotnet test --filter "FullyQualifiedName~Phase18"` reports **19/19 passed** post-commit. The new parser arm only fires when `Slash` follows a `NoteLiteral` in note-stream context — non-note-stream code paths and existing `.flow` scripts are unaffected. Test 9 (`NoteStreamCompiler_NoSlashSyntax_TupletRatioStaysNull`) is the structural pin guaranteeing Phase 18 dormancy contract for non-fractional notes.

ByteIdenticalTutorialTests + ByteIdenticalShowcaseTests + MusicalNoteDataTests + FractionTests all GREEN.

**Cumulative Phase19 count:** 17 = Plan 19-01 (8) + Plan 19-02 (9).

**Full suite:** 323/323 passed = 306 pre-Phase-19 baseline + 17 Phase19 Facts.

## Plan 19-01 Dependency Verification

Plan 19-01 commit `a7f94ef` confirmed in HEAD ancestor history via `git log --oneline | grep -F "a7f94ef"`. The TupletElement record + NoteElement.TupletRatio defaulted field shipped by 19-01 are the load-bearing pre-requisites for this plan's parser populate + compiler dispatch.

## Deviations from Plan

**None.** Plan executed exactly as written. RESEARCH + PLAN cascade was high-fidelity:

- Build cleared on first attempt (no API drift like 19-01's `parser.ParseProgram` vs `parser.Parse` correction)
- All 9 Facts passed on first run
- Phase 18 byte-identical regression held without intervention
- No Rule 4 architectural decisions surfaced
- No auth gates encountered
- No out-of-scope detection (clean — all changes localized to parser + compiler + new test file)

The plan's `<read_first>` already noted that Plan 19-01 had encountered the `parser.Parse()` vs `parser.ParseProgram()` API drift; this plan used the correct method name from the start.

## Atomic Commit

**`9aae23c`** — `feat(19-02): TUP-04/08 per-note fractional + tuplet-ratio shorthand`

Files: 3 (2 modified + 1 created)
Insertions: 323 lines
Deletions: 7 lines
No accidental file deletions (post-commit `git diff --diff-filter=D` empty).

## Phase 19 Forward-Readiness

Both bracket-form (Plan 19-01) AND per-note (Plan 19-02) paths now produce non-null `DurationFraction` on every tuplet/fractional note. Plans 19-03 + 19-04 can walk a single uniform AST shape:

- **Plan 19-03 (TUP-05 bar-fit validator):** Walks NoteElement.DurationFraction for sum validation (rational `+` arithmetic from Phase 18). Needs to handle BOTH bracket-form `{N:M ...}q` (TupletElement children) AND per-note `C4/X:Y` (NoteElement.TupletRatio populated) — both produce identical `MusicalNoteData.DurationFraction` shape, so validator code can be unified.
- **Plan 19-04 (TUP-06 MIDI TPQN):** Per-note `/X:Y` ratios feed `union(tuplet_denominators)` alongside bracket-form ratios. Walk NoteElement.TupletRatio.Denom (Y) AND TupletElement.Denominator collecting denominators for LCM math.
- **Plan 19-05 (TUP-07 augment/diminish regression):** Doubles/halves DurationFraction via Fraction `*` operator from Phase 18; both bracket-form and per-note inputs produce equivalent rational sequences for the regression Fact.

## Self-Check: PASSED

- ✓ `flow-lang/Parsing/Parser.NoteStream.cs` exists (verified via Read)
- ✓ `Match(TokenType.Slash)` appears 2× (main NoteLiteral arm + ParseTupletChildren NoteLiteral arm)
- ✓ `Tuplet ratio numerator X must be ≥ 1` appears 2×
- ✓ `Tuplet ratio denominator Y must be ≥ 1` appears 2×
- ✓ `Duration denominator must be ≥ 1` appears 2×
- ✓ `tupletRatio = (n, 1)` appears 2× (TUP-04 sentinel encoding)
- ✓ `tupletRatio = (n, y)` appears 2× (TUP-08 encoding)
- ✓ Both NoteElement constructors pass `tupletRatio` (2 hits for `tupletRatio)`)
- ✓ `flow-lang/Runtime/NoteStreamCompiler.cs` exists, has `if (note.TupletRatio.HasValue)` (1 hit)
- ✓ `new Fraction(4, x)` appears 1× (TUP-04 quarter-units conversion)
- ✓ `SuffixToQuarterFraction` appears 6× (1 declaration + 1 new TUP-08 use + 4 pre-existing TupletElement uses)
- ✓ `durationFraction: durationFraction` appears 1× (CompileNoteElement now passes the computed value)
- ✓ `flow-lang.Tests/Unit/Phase19/FractionalDurationTests.cs` exists, namespace `FlowLang.Tests.Unit.Phase19` (1 hit), 9 [Fact] attributes (9 hits)
- ✓ All 9 test method names present in source (9-way alternation grep returns 9)
- ✓ Build clean: 0 errors
- ✓ Phase19.FractionalDurationTests: 9/9 passed
- ✓ Phase19 cumulative (19-01 + 19-02): 17/17 passed
- ✓ Phase18 byte-identical regression: 19/19 passed (held)
- ✓ Full suite: 323/323 passed (= 306 + 17)
- ✓ Commit `9aae23c` exists in HEAD (`git log --oneline -1`)
- ✓ No accidental file deletions (post-commit `git diff --diff-filter=D` empty)
- ✓ Plan 19-01 dependency commit `a7f94ef` confirmed in HEAD ancestor history

---

*Phase: 19-tuplets-arbitrary-fractional-durations*
*Plan: 19-02*
*Atomic commit: 9aae23c*
*Completed: 2026-04-26*
