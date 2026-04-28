# Phase 19: Tuplets & Arbitrary Fractional Durations — Specification

**Created:** 2026-04-26
**Ambiguity score:** 0.16 (gate: ≤ 0.20)
**Requirements:** 8 locked

## Goal

Composers can write triplets, quintuplets, septuplets, nested tuplets, and arbitrary fractional note durations like `C4/12` inside note streams, with correct WAV + MIDI output that preserves Flow's byte-identical determinism contract. Lead capability of v1.3.

## Background

**What exists today (v1.2 base):**

- `flow-lang/Runtime/NoteStreamCompiler.cs` compiles `| C4 D4 E4 |` into `SequenceData` using a power-of-2 `NoteValueType` enum (WHOLE, HALF, QUARTER, EIGHTH, SIXTEENTH, THIRTYSECOND) — see `DurationSuffixMap` at line 29
- Note-stream syntax supports duration suffixes (`C4q`), dotted notes (`C4q.`), tied notes (`C4h~`), cent offsets (`C4+50c`), chord brackets `[C4 E4 G4]q`, named chords (`Cmaj7`), roman numerals (`I`, `IV`), random choice `(? ...)`, ghost notes `(ghost ...)`, grace notes `(grace ...)`
- `MusicalNoteData` (`flow-lang/TypeSystem/SpecialTypes/NoteType.cs:211`) has `int? DurationValue` — no `Fraction` field yet
- `NoteStreamElement` is a sealed-discriminated-union of 9 record types (NoteElement, RestElement, ChordElement, NamedChordElement, RomanNumeralElement, RandomChoiceElement, VariableReferenceElement, GhostNoteElement, GraceNoteElement) — no `TupletElement` yet
- `CalculateAutoFitDuration` (NoteStreamCompiler.cs:206) uses `double` math to map remaining beats to closest power-of-2 NoteValue — no support for non-power-of-2 durations
- MIDI export via DryWetMidi 8.0.3 with default TPQN=480 — handles 3:2 (160 ticks each, exact) and 5:4 (96 each, exact) cleanly, but 7:N requires elevation
- `TransformFunctions.cs:239,261` augment/diminish work on enum-based durations only — not yet tuplet-aware
- AUDIT-VERIFIED markers at `Interpreter.cs:292` (C1 fixed) and `TransformFunctions.cs:239,261` (C5 dismissed) — must remain valid post-Phase-19

**What's missing (gap to target):**

- No `Fraction` rational arithmetic primitive (Phase 18 will add it; Phase 19 consumes it)
- No tuplet bracket syntax — `{N:M ... }` brackets do not parse today
- No arbitrary-denominator duration syntax — `C4/12` is currently a parse error
- No bar-fit validator for non-power-of-2 sums
- No TPQN auto-elevation for tuplets requiring >480 ticks/quarter
- No tuplet-aware regression Facts for `augment`/`diminish` (TUP-07 closes this)

## Requirements

1. **TUP-01: Tuplet bracket compiles to recursive AST node**
   - Current: `{...}` curly braces are unused in note-stream context; parsing them raises a token error
   - Target: `{N:M element element element}q` parses to a new `TupletElement` AST record (recursive — children are heterogeneous `NoteStreamElement`s including nested `TupletElement`s). Per locked decision D-01, brackets use `{ }`. NoteStreamCompiler emits `MusicalNoteData` instances whose `DurationFraction` reflects the N:M ratio applied to the parent duration suffix.
   - Acceptance: `| {3:2 C4 D4 E4}q |` renders three `MusicalNoteData` instances each with `DurationFraction = 1/12` (one-third of one quarter = one-twelfth of one whole). Their summed beat duration equals one quarter.

2. **TUP-02: `{N ...}` shorthand defaults to music21 conventions**
   - Current: shorthand syntax does not exist (no tuplet syntax at all)
   - Target: `{N elem elem elem}` (no `:M`) defaults to music21's standard tuplet conventions: 3-tuplet → 3:2, 5-tuplet → 5:4, 6-tuplet → 6:4, 7-tuplet → 7:4, 9-tuplet → 9:8 etc. Lookup table covers tuplet element counts 2–11; counts ≥12 require explicit `:M`.
   - Acceptance: `{3 C4 D4 E4}q` produces output identical to `{3:2 C4 D4 E4}q`. `{5 C4 D4 E4 F4 G4}q` produces output identical to `{5:4 C4 D4 E4 F4 G4}q`. `{12 ...}` without explicit `:M` raises a parse error citing the lookup-table bounds.

3. **TUP-03: Nested tuplets compose via accumulating Fraction propagation**
   - Current: nesting impossible — no tuplet syntax
   - Target: `NoteStreamCompiler` propagates an accumulating `Fraction outerScale` through recursive descent over `TupletElement` children. Inner tuplet ratios multiply through outer ratios cleanly without floating-point drift (Pitfall 1 mitigation).
   - Acceptance: `| {3:2 C4 {3:2 D4 E4 F4}q G4}h |` compiles to 5 `MusicalNoteData` instances. Outer 3:2 over half = each outer slot is 1/6 of a whole; inner 3:2 over the middle outer slot = each inner is 1/18 of a whole. Pinned: C4 = 1/6 whole, D4/E4/F4 each = 1/18 whole, G4 = 1/6 whole. Sum = 3·(1/18) + 2·(1/6) = 1/6 + 1/3 = 1/2 whole = one half. Validates against the bar's `h` outer suffix.

4. **TUP-04: Arbitrary fractional duration `C4/N` syntax**
   - Current: `/` inside note-stream context is not recognized; arithmetic `/` only fires in regular expression context
   - Target: Lexer recognizes `/N` (where N is an unsigned positive integer) as a duration suffix when it follows a note name inside `| ... |` note-stream context. `C4/N` produces a `MusicalNoteData` with `DurationFraction = 1/N` of a whole note. Per Round-1 lock: `C4/1` is valid (= whole note, equivalent to `C4w`); `C4/0` raises a parse error citing zero-denominator.
   - Acceptance: `| C4/12 D4/12 E4/12 |` parses to three notes each with `DurationFraction = 1/12 whole`. `| C4/1 |` parses to one whole note. `| C4/0 |` raises a parse-time error: `"Duration denominator must be ≥ 1; got 0 at <line>:<col>"`.

5. **TUP-05: Bar-fit validator handles tuplet/fractional sums charitably**
   - Current: `CalculateAutoFitDuration` uses `double` math; bar-fit checks compare doubles. No rational-fraction support.
   - Target: NoteStreamCompiler validates that the rational sum of bar element durations equals the time-signature value. Sums that match exactly validate clean. Sums that exceed the bar (overflow) silently truncate at the bar boundary AND emit an advisory diagnostic via `ErrorReporter.ReportInfo` (per Round-1 charitable-interpretation lock). Sums that underflow are accepted (rest is implicit at end). Overflow truncation preserves the byte-identical determinism contract — same input always produces same output. Per Round-1 lock: tuplet brackets without explicit duration suffix raise a parse error (no auto-fit inside tuplets).
   - Acceptance:
     - `tempo 120 timesig 4/4 { | {3:2 C4 D4 E4}q {3:2 F4 G4 A4}q B4q C5q | }` validates clean (1/12+1/12+1/12 + 1/12+1/12+1/12 + 1/4 + 1/4 = 1/4 + 1/4 + 1/4 + 1/4 = 4/4).
     - `tempo 120 timesig 4/4 { | {3:2 C4 D4 E4}q B4q C5q D5q E5q | }` (sums to 5/4) silently truncates at the 4/4 bar boundary, emits an Info-severity diagnostic naming the rational sum `5/4 > 4/4`, and continues rendering.
     - `{3:2 C4 D4 E4}` (no suffix) raises a parse error: `"Tuplet bracket requires explicit duration suffix"`.

6. **TUP-06: MIDI export auto-elevates TPQN, capped at 9600**
   - Current: DryWetMidi default TPQN=480; `Audio/MidiExport.cs` does not vary TPQN by content
   - Target: MIDI export computes `requiredTPQN = LCM(480, 2 × union(tuplet_denominators in song))`. If `requiredTPQN ≤ 9600` (per locked decision D-05), elevate the SMF header's ticks-per-quarter to `requiredTPQN` and scale all delta-times accordingly. If `requiredTPQN > 9600`, raise a clear error: `"MIDI export requires TPQN={requiredTPQN}, exceeds cap 9600 (locked v1.3 D-05). Tuplet ratios in this song: [{list}]"`.
   - Acceptance:
     - `{3:2 ...}` exports at TPQN=480 (480/3=160 ticks each, exact).
     - `{5:4 ...}` exports at TPQN=480 (480/5=96 ticks each, exact).
     - `{7:8 ...}` auto-elevates to TPQN=3360 (480 × 7).
     - `{11:13 ...}` raises a TPQN-cap error with the message format above.
     - Existing test scripts (no tuplets) continue exporting at TPQN=480 (no auto-elevation when no tuplets present).

7. **TUP-08: Per-note tuplet shorthand `C4/X:Y[suffix]`**
   - Current: per-note tuplet ratio syntax does not exist; only the `{X:Y ...}` bracket form is planned (TUP-01)
   - Target: Inside note-stream context, `C4/X:Y[suffix]` is shorthand for one tuplet member at the X:Y ratio. Optional level suffix (`w/h/q/e/s/t`, default `q`). Mathematically equivalent to one member of `{X:Y note note ... note}[suffix]` — i.e. `DurationFraction = suffix_fraction / X` of a whole. The `Y` is preserved as the tuplet-ratio label (used for MIDI export TPQN math and AST metadata) but does not affect per-note duration math (X alone determines fill). Per-note instances are independent — no implicit grouping requirement, no consecutive-must-match rule. Mixing `C4/3:2 D4/5:4 E4/3:2` is legal (three independent tuplet members at different ratios).
   - Acceptance:
     - `| C4/3:2 D4/3:2 E4/3:2 |` produces three `MusicalNoteData` instances each with `DurationFraction = 1/12 whole`, semantically identical to `| {3:2 C4 D4 E4}q |`.
     - `| C4/5:4h |` produces one note with `DurationFraction = (1/2)/5 = 1/10 whole`.
     - `| C4/3:2q D4/5:4q E4/3:2q |` is legal — three independent per-note tuplets.
     - `| C4/0:2 |` raises the same parse error as `C4/0` (zero-numerator denominator-class — error message `"Tuplet ratio numerator X must be ≥ 1; got 0"`).
     - MIDI export TPQN computation includes per-note `/X:Y` ratios in the `union(tuplet_denominators)` set the same way bracket-form ratios do (so `| C4/7:8 |` triggers the same TPQN auto-elevation as `{7:8 ...}q`).

8. **TUP-07: AUDIT-VERIFIED C5 re-validated against tuplet sequences**
   - Current: AUDIT-VERIFIED markers at `TransformFunctions.cs:239,261` cite C5-dismissed status from Phase 11. Existing regression coverage uses power-of-2 NoteValue durations only. Tuplet sequences would silently invalidate the marker.
   - Target: New regression Facts pin `augment(tupletSeq)` and `diminish(tupletSeq)` behavior on rational durations. `augment` doubles the rational durations; `diminish` halves them. AUDIT-VERIFIED comment at `TransformFunctions.cs:239,261` is updated with `2026-04-NN: re-validated against tuplet sequences (Phase 19 TUP-07)`.
   - Acceptance:
     - `augment` Fact: input `[1/12, 1/12, 1/12]` → output `[1/6, 1/6, 1/6]`. Each `DurationFraction` is exactly doubled (rational arithmetic, no `double` drift).
     - `diminish` Fact: input `[1/12, 1/12, 1/12]` → output `[1/24, 1/24, 1/24]`. Each `DurationFraction` is exactly halved.
     - AUDIT-VERIFIED comment text updated; grep finds the new date marker.

## Boundaries

**In scope:**

- `TupletElement` AST record (recursive children, optional `:M` denominator, required duration suffix)
- `{N:M ...}` and `{N ...}` lexer/parser/compiler dispatch inside note-stream context only
- `C4/N` arbitrary-denominator note-stream lexer/parser support (only inside `| ... |`)
- `C4/X:Y[suffix]` per-note tuplet shorthand (TUP-08; same lexer state as `/N`, extended with optional `:Y` and optional level suffix)
- `CalculateAutoFitDuration` extension to handle rational-bar-fit math (without auto-fit inside tuplets)
- Bar-overflow charitable-truncate behavior + Info diagnostic
- MIDI export TPQN auto-elevation logic + 9600 cap error
- `augment`/`diminish` regression Facts on tuplet sequences + AUDIT-VERIFIED marker refresh
- Pre-landing collision grep transcript for `{`, `}` over `tests/`, `examples/`, `flow-lang/*.flow` (sanity check; brace tokens are not currently used in note streams)

**Out of scope:**

- The `Fraction` struct itself — produced in **Phase 18** (FRAC-01), consumed here (binding pre-ordering #1)
- `MusicalNoteData.DurationFraction` field declaration — produced in **Phase 18** (FRAC-02), populated here
- Pragma system or `enable` keyword — Phase 21 (PRAG-01/02 + DEFER-02/03)
- Multi-letter enharmonic edges (E↔Fb etc.) — Phase 20 (DEFER-04)
- Microtonal tuning interaction with tuplet rendering — Phase 23 (MICR-01..03); tuplets in Phase 19 use existing 12-TET path
- Scale linting interaction — Phase 24 (LINT-01..03)
- Gaussian humanize — Phase 25 (DEFER-06)
- LSP semantic-tokens / completion / hover updates for tuplet syntax — flow-lsp gets a follow-up touch in a later phase if needed; v1.2 LSP infrastructure handles graceful parse errors during incomplete-typing already
- Auto-fit duration inside tuplet brackets — locked NO per Round-1 D-06 (explicit duration required)
- Hard-error bar overflow — locked NO per Round-1 D-07 (silent truncate + Info diagnostic per charitable-interpretation memory)
- Tutorial demonstration of tuplets — Phase 26 (QOL-04 tutorial refresh; tuplet chapter added there)

## Constraints

- **Phase 18 must land first.** Phase 19 consumes the `Fraction` struct + `MusicalNoteData.DurationFraction` field — without these, no requirement here is implementable. Binding pre-ordering #1 from REQUIREMENTS.md.
- **Byte-identical determinism contract preserved.** All ~70 existing `.flow` test scripts and `examples/tutorial.flow` + `examples/showcase.flow` must produce byte-identical WAV + MIDI before and after Phase 19. The existing power-of-2 path (when `MusicalNoteData.DurationFraction` is null) must remain unchanged. Verified via `cmp` regression gate.
- **AUDIT-VERIFIED markers must not silently invalidate.** Specifically C5 (TransformFunctions.cs:239,261) per Pitfall 9. TUP-07 re-validates by Fact + comment update.
- **No new external dependencies.** Hand-roll only per minimal-deps philosophy; SUMMARY.md confirms zero new deps for v1.3 (no Fractions/Rationals/BigRational NuGet additions).
- **Charitable interpretation per CLAUDE.md memory.** Bar overflow is silent-truncate + Info diagnostic, not error (locked Round-1 D-07).
- **Tuplet bracket REQUIRES explicit duration suffix** (locked Round-1 D-06). No auto-fit inside `{...}`. Parse error if no suffix.
- **`C4/N` requires N ≥ 1** (locked Round-1 D-08). `/0` is a parse error; `/1` is a whole note.

## Acceptance Criteria

- [ ] `| {3:2 C4 D4 E4}q |` compiles, renders 3 notes each with `DurationFraction = 1/12 whole`, sum = 1 quarter
- [ ] `{3 C4 D4 E4}q` produces output identical to `{3:2 C4 D4 E4}q`
- [ ] `{12 ...}` without explicit `:M` raises a parse error citing music21 lookup-table bounds
- [ ] Nested tuplets `| {3:2 C4 {3:2 D4 E4 F4}q G4}h |` produce 5 notes with rational durations [1/6, 1/18, 1/18, 1/18, 1/6] of whole
- [ ] `| C4/12 D4/12 E4/12 |` parses to three 1/12-whole notes
- [ ] `| C4/1 |` parses to a whole note (= `C4w`)
- [ ] `| C4/0 |` raises parse-time error: `"Duration denominator must be ≥ 1; got 0"`
- [ ] `| C4/3:2 D4/3:2 E4/3:2 |` produces three notes each with `DurationFraction = 1/12 whole`, identical to `| {3:2 C4 D4 E4}q |`
- [ ] `| C4/5:4h |` produces one note with `DurationFraction = 1/10 whole`
- [ ] `| C4/3:2q D4/5:4q E4/3:2q |` parses without error (mixed ratios legal in per-note form)
- [ ] `| C4/0:2 |` raises parse-time error: `"Tuplet ratio numerator X must be ≥ 1; got 0"`
- [ ] `| C4/7:8 |` triggers TPQN auto-elevation to 3360 (same path as bracket-form `{7:8 ...}q`)
- [ ] `| {3:2 C4 D4 E4}q {3:2 F4 G4 A4}q B4q C5q |` under `timesig 4/4` validates clean
- [ ] Bar overflow `| {3:2 C4 D4 E4}q B4q C5q D5q E5q |` (5/4) silent-truncates with Info diagnostic naming `5/4 > 4/4`
- [ ] `{3:2 C4 D4 E4}` without duration suffix raises parse error
- [ ] MIDI export of `{3:2}`-only song uses TPQN=480 (no elevation)
- [ ] MIDI export of `{7:8}`-containing song elevates TPQN to 3360
- [ ] MIDI export of `{11:13}`-containing song raises TPQN-cap error with message `"MIDI export requires TPQN=..., exceeds cap 9600 (locked v1.3 D-05)..."`
- [ ] `augment` Fact: tuplet sequence `[1/12, 1/12, 1/12]` → `[1/6, 1/6, 1/6]` (exact rational doubling)
- [ ] `diminish` Fact: tuplet sequence `[1/12, 1/12, 1/12]` → `[1/24, 1/24, 1/24]` (exact rational halving)
- [ ] `TransformFunctions.cs:239,261` AUDIT-VERIFIED comment updated with `2026-04-NN: re-validated against tuplet sequences (Phase 19 TUP-07)`
- [ ] All existing `.flow` test scripts (no tuplets) produce byte-identical WAV + MIDI to v1.2 baseline
- [ ] `examples/tutorial.flow` + `examples/showcase.flow` produce byte-identical WAV + MIDI to v1.2 baseline (cmp-clean)
- [ ] Pre-landing collision grep transcript for `{` `}` in note-stream contexts of `tests/`, `examples/`, `flow-lang/*.flow` recorded in VERIFICATION.md (expected: zero hits — braces are unused in note-stream context today)

## Ambiguity Report

| Dimension          | Score  | Min  | Status | Notes                                                                  |
|--------------------|--------|------|--------|------------------------------------------------------------------------|
| Goal Clarity       | 0.90   | 0.75 | ✓      | Lead capability for v1.3; 7 falsifiable REQs with concrete examples   |
| Boundary Clarity   | 0.85   | 0.70 | ✓      | Explicit out-of-scope list pointing to other v1.3 phases               |
| Constraint Clarity | 0.80   | 0.65 | ✓      | Phase-18 dependency, byte-identical determinism, charitable overflow  |
| Acceptance Criteria| 0.80   | 0.70 | ✓      | 19 pass/fail criteria; rational-fraction values pinned                |
| **Ambiguity**      | **0.1525** | ≤0.20| ✓  | Gate passed                                                            |

Status: ✓ = met minimum, ⚠ = below minimum (planner treats as assumption)

## Interview Log

| Round | Perspective | Question summary                                                            | Decision locked                                                                    |
|-------|-------------|-----------------------------------------------------------------------------|------------------------------------------------------------------------------------|
| 0     | Initial     | Score from REQUIREMENTS.md alone                                            | Ambiguity 0.2375 — slightly above gate, three boundary edges to lock              |
| 1     | Researcher  | Auto-fit inside tuplets when no duration suffix?                            | D-06: REQUIRE explicit duration suffix. `{3:2 C4 D4 E4}` (no `q`) is a parse error |
| 1     | Researcher  | Bar overflow when tuplets + other notes exceed time-signature?              | D-07: Silent truncate + Info diagnostic (charitable-interpretation memory)         |
| 1     | Researcher  | `C4/0` and `C4/1` edge cases?                                               | D-08: `C4/1` = whole note (valid); `C4/0` = parse error citing zero-denominator    |
| 2     | User add-on | Variable duration suffix like `x:y`?                                        | D-09: Add TUP-08 — `C4/X:Y[suffix]` per-note tuplet shorthand (Option B). Mixed ratios in adjacent notes are legal (independent per-note). Same TPQN auto-elevation path as bracket form. |

Plus 5 milestone-level decisions (D-01..D-05) inherited from `/gsd-new-milestone` discussion:

- **D-01**: Tuplet bracket syntax is `{N:M ...}` (braces, not parens)
- **D-02**: Pragmas file-scope only — irrelevant to Phase 19 (Phase 21 territory)
- **D-03**: Microtonal named-tunings wedge — irrelevant to Phase 19 (Phase 23 territory)
- **D-04**: Gaussian humanize separate function — irrelevant to Phase 19 (Phase 25 territory)
- **D-05**: MIDI TPQN cap 9600 — directly drives TUP-06 acceptance criteria

---

*Phase: 19-tuplets-arbitrary-fractional-durations*
*Spec created: 2026-04-26*
*Next step: /gsd-discuss-phase 19 — implementation decisions (parser dispatch shape, NoteStreamCompiler recursion structure, MIDI delta-time scaling math)*
