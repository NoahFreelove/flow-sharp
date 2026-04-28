---
phase: 18-foundation-rational-duration-arithmetic
plan: 01
subsystem: type-system
tags: [foundation, rational-arithmetic, type-system, tdd, fraction, gcd, record-struct]

# Dependency graph
requires:
  - phase: none
    provides: Fraction primitive sits above all existing TypeSystem helpers — no upstream dependency
provides:
  - "Fraction readonly record struct at flow-lang/TypeSystem/Fraction.cs"
  - "GCD-normalizing constructor (sign on numerator, zero-denom throws)"
  - "+, *, <, > arithmetic operators (no double, all-int)"
  - "Free == / != / GetHashCode from record struct value-equality"
  - "ToString -> Num/Denom always (D-USER-03, no special-casing 1/1)"
  - "9 unit Facts pinning FRAC-01 acceptance + edge cases"
affects:
  - "Plan 18-02 (FRAC-02 — wires MusicalNoteData.DurationFraction nullable field)"
  - "Phase 19 tuplets (TUP-01..08) — consumes Fraction for duration ratios"

# Tech tracking
tech-stack:
  added:
    - "readonly record struct Fraction (hand-rolled, ~57 LOC, zero NuGet)"
  patterns:
    - "RED+GREEN bundled atomic commit (Phase 12-02 6e5a960 precedent)"
    - "Helper-type-not-FlowType placement in TypeSystem/ (sibling of ArrayType.cs)"
    - "GCD idiom mirrored verbatim from PolyrhythmFunctions.cs:117 for stylistic consistency"

key-files:
  created:
    - "flow-lang/TypeSystem/Fraction.cs (57 lines)"
    - "flow-lang.Tests/Unit/Phase18/FractionTests.cs (75 lines)"
  modified: []

key-decisions:
  - "Fraction is HELPER not FlowType — sibling of ArrayType.cs in TypeSystem/, never user-spelled in .flow source per ARCHITECTURE.md §3"
  - "readonly record struct chosen over class — stack-allocated, free value-equality + GetHashCode, no GC pressure per CLAUDE.md"
  - "GCD idiom copied verbatim from PolyrhythmFunctions.cs:117 (`b == 0 ? a : Gcd(b, a % b)`) — single codebase idiom, no new abstraction"
  - "Sign carried on numerator (D-USER-03 + RESEARCH Pattern 3 + Assumption A7) — standard rational convention"
  - "Zero denominator throws DivideByZeroException eagerly in ctor — RESEARCH Pitfall 3 mitigation"
  - "Operator surface minimal: +, *, <, > only — NO -, /, <=, >= in Phase 18 (Phase 19 adds only what TUP-01..08 need; avoid premature surface area)"
  - "ToString always emits Num/Denom (NEVER special-cases 1/1) — D-USER-03 + RESEARCH Open Q 2 + 1/1 pinned in Test 8 of Fact suite"
  - "Math.Abs(num) wrapping in Gcd call to handle negative-numerator path (sign-normalized denom is always positive after `if (denom < 0)` flip; numerator may still be negative)"
  - "RED+GREEN bundled atomic commit (Phase 12-02 6e5a960 precedent) — bisectable but HEAD never carries broken build; same atomic pattern Phase 12-02 used for FIX-05 + Plan 15 used for byte-identical determinism"
  - "Fraction.cs imports zero from FlowLang namespace tree — pure-stdlib (System.Math, System.DivideByZeroException) — keeps Fraction at the bottom of the dependency DAG so MusicalNoteData (Plan 18-02) can consume it without cycle risk"

patterns-established:
  - "Atomic RED+GREEN commit for foundation-tier primitives where test scope is tightly bounded — both files land together, RED proven via incremental local build before commit, GREEN proven via 9/9 Fact pass + 296/296 full-suite"
  - "Phase 18 'nothing existing changes' inverse-success criterion — full-suite count grows by exactly +9 (287 -> 296) and zero pre-existing Facts regress"

requirements-completed: [FRAC-01]

# Metrics
duration: 2min
completed: 2026-04-26
---

# Phase 18 Plan 01: Foundation — Rational Duration Arithmetic — Fraction Primitive Summary

**Hand-rolled `readonly record struct Fraction` at flow-lang/TypeSystem/Fraction.cs with GCD-normalizing ctor, +/*/</> operators, free record-struct value-equality, and 9-Fact unit suite pinning FRAC-01 acceptance — primitive is in isolation; zero consumers wired (Plan 18-02 territory).**

## Performance

- **Duration:** ~2 min (1m on file authorship + 1m on build/test/commit pipeline)
- **Started:** 2026-04-26T16:39:11Z
- **Completed:** 2026-04-26T16:41:35Z
- **Tasks:** 2 (Task 1 RED tests, Task 2 GREEN impl)
- **Files created:** 2 (`Fraction.cs` 57 lines + `FractionTests.cs` 75 lines = 132 LOC total)
- **Files modified:** 0

## Accomplishments

- `Fraction` rational arithmetic primitive shipped at `flow-lang/TypeSystem/Fraction.cs` (57 LOC).
- GCD-normalizing constructor: `2/4 == 1/2`, `3/12 == 1/4` via record-struct value-equality.
- `+`, `*`, `<`, `>` operators (all-int, no double cast — drift-free per RESEARCH Pitfall 1).
- Sign normalization on numerator (`new Fraction(1, -2)` → Num=-1, Denom=2).
- Eager zero-denominator detection (`new Fraction(1, 0)` throws `DivideByZeroException`).
- `ToString` always emits `Num/Denom` form (never special-cases 1/1) per D-USER-03.
- 9 unit Facts at `flow-lang.Tests/Unit/Phase18/FractionTests.cs` pin FRAC-01 acceptance + edge cases:
  1. `TripletThirds_SumToOne` — 1/3 + 1/3 + 1/3 == 1
  2. `TwoFourths_NormalizeToOneHalf` — 2/4 == 1/2 normalize
  3. `ThreeTwelfths_NormalizeToOneFourth` — 3/12 == 1/4 non-trivial GCD
  4. `MultiplicationProducesProduct` — 1/3 * 1/4 == 1/12
  5. `LessThanIsRational` — 1/3 < 1/2 via cross-multiply (no double)
  6. `ZeroDenominator_Throws` — `new Fraction(1, 0)` raises DivideByZeroException
  7. `NegativeDenom_SignOnNumerator` — `new Fraction(1, -2)` → Num=-1, Denom=2
  8. `ToString_FormatNumSlashDenom` — emits "3/4" AND "1/1" (no special-case)
  9. `GetHashCode_EqualFractionsHashEqual` — record-struct value-equal hash on normalized fields
- Full suite: 296/296 passed (287 baseline + 9 new). Zero pre-existing Facts regressed.

## Task Commits

Per the plan's bundled-atomic-commit directive (Phase 12-02 6e5a960 precedent — RED+GREEN bisectable but HEAD never broken):

1. **Task 1 + Task 2 (atomic):** `2092f32` — `feat(18-01): FRAC-01 ship Fraction rational-arithmetic primitive`
   - flow-lang.Tests/Unit/Phase18/FractionTests.cs (NEW, 75 lines)
   - flow-lang/TypeSystem/Fraction.cs (NEW, 57 lines)

_Note: TDD discipline preserved — Task 1's RED state was empirically confirmed via local `dotnet build` BEFORE Task 2's Fraction.cs was written. Build emitted 19+ `CS0246: type or namespace 'Fraction' could not be found` errors at the precise FractionTests.cs sites. Task 2's Fraction.cs flipped all 9 Facts GREEN on first run (no iteration needed)._

**Plan metadata commit:** Pending — will land alongside SUMMARY.md, STATE.md, ROADMAP.md, REQUIREMENTS.md updates as the docs(18-01) closure commit.

## Files Created/Modified

### Created

- **`flow-lang/TypeSystem/Fraction.cs`** (57 lines) — `public readonly record struct Fraction { int Num, int Denom }` with GCD-normalizing ctor, +/*/</> operators, ToString override; recursive Euclidean GCD copied verbatim from `PolyrhythmFunctions.cs:117`. Zero `using` statements (only implicit `System` from project's ImplicitUsings). Sibling of `ArrayType.cs`; not a `FlowType` subclass.

- **`flow-lang.Tests/Unit/Phase18/FractionTests.cs`** (75 lines) — 9 `[Fact]` xUnit.v3 tests pinning FRAC-01 acceptance examples + edge cases. `using FlowLang.TypeSystem`; namespace `FlowLang.Tests.Unit.Phase18`. Mirrors existing `Phase14`/`Phase15`/`Phase17` test directory layout.

### Modified

- **None.** Per plan verification §4 ("No consumer wiring") — Phase 18-01 ships Fraction in isolation; no `MusicalNoteData`, `NoteStreamCompiler`, `BarRenderer`, or any other consumer was touched. Plan 18-02 will wire `MusicalNoteData.DurationFraction` nullable field via defaulted-parameter additive-migration pattern (RESEARCH §3 Pattern 1).

## Decisions Made

- **Helper-type placement at TypeSystem root** — Fraction sits as `flow-lang/TypeSystem/Fraction.cs`, sibling of `ArrayType.cs`, NOT under `PrimitiveTypes/` or `SpecialTypes/`. Per ARCHITECTURE.md §3 + RESEARCH §3: users never spell `Fraction` in `.flow` source, so it must NOT be registered as a FlowType. Verification: `grep -cE 'class Fraction|: FlowType' flow-lang/TypeSystem/Fraction.cs` returns 0.

- **`readonly record struct` over `class`** — Stack-allocated; record-struct generates value-equality + `GetHashCode` over normalized fields automatically. Per CLAUDE.md "no GC pressure in hot paths" + RESEARCH Standard Stack table. Equal fractions like `new Fraction(2,4)` and `new Fraction(1,2)` hash identically because constructor normalizes both to `Num=1, Denom=2` before record-struct hashing fires.

- **Operator surface minimal** — Only `+`, `*`, `<`, `>` shipped in Phase 18. No `-`, `/`, `<=`, `>=`, no `Reduce()` method, no implicit/explicit conversion to/from `double`. Phase 19 will add only what TUP-01..08 acceptance Facts require. Avoids premature API surface that would carry behavioural-test debt.

- **Math.Abs wrapping in Gcd call** — After sign normalization (`if (denom < 0) { num = -num; denom = -denom; }`), denom is always positive, but num may still be negative if input was `Fraction(-3, 6)`. `Gcd` recursive impl assumes non-negative inputs (mod operator behaviour on negative-int LHS in C# returns negative remainder). `Math.Abs(num)` ensures correctness without changing the canonical Gcd idiom.

- **Bundled atomic commit (Task 1 + Task 2 in one commit `2092f32`)** — Per plan's explicit `<action>` directive citing Phase 12-02 commit `6e5a960` precedent. RED state was empirically confirmed via local incremental build BEFORE the GREEN Fraction.cs was written; preserves TDD discipline within the atomic-commit constraint while keeping HEAD compile-clean for git-bisect.

## Deviations from Plan

**None — plan executed exactly as written.**

The plan was unusually high-fidelity (post-RESEARCH §6 Code Examples ships canonical FRAC-01 acceptance Facts verbatim; canonical Fraction shape published in §3 Pattern 3). The plan's `<action>` blocks were copy-pasteable and produced correct output on first run.

- No Rule 1 (bug fix) deviations — Fraction.cs compiled cleanly on first write; all 9 Facts passed on first test run.
- No Rule 2 (missing critical) deviations — operator surface, edge cases (zero denom, negative denom), and ToString format all pre-specified.
- No Rule 3 (blocking) deviations — xUnit.v3 3.2.2 + xunit.runner.visualstudio 3.1.5 already pinned per plan 12-01 retrospective; `Phase18/` directory created cleanly alongside existing `Phase14/15/17`.
- No Rule 4 (architectural) deviations — Fraction's helper-not-FlowType placement decision was pre-locked in ARCHITECTURE.md §3.

**Total deviations:** 0
**Impact on plan:** None — pure adherence. Validates the RESEARCH-then-PLAN cascade for foundation-tier primitive work where prior research has already reduced ambiguity to zero.

## Issues Encountered

**None.** Build clean (0 errors after Task 2 lands; only pre-existing warnings unrelated to Fraction). Full suite GREEN (296/296). Two-runner / cmp regression gates not exercised because no consumer was touched (plan verification §4 — structurally guaranteed).

### Assumption A8 follow-up

RESEARCH Assumption A8 noted xUnit.v3 3.2.2 might struggle with `record struct` parameters in `[InlineData]` Theory rows. The plan deliberately used `[Fact]` per case to side-step this. **Outcome:** All 9 Facts ran cleanly; no Theory needed. A8 remains untested but the conservative choice avoided the issue entirely.

## User Setup Required

**None** — no external service configuration, no environment variables, no third-party API keys. Pure C# code addition with zero dependency surface change.

## Next Phase Readiness

### Plan 18-02 (FRAC-02) ready to execute

`Fraction` primitive is shipped, tested (9/9 GREEN), and import-stable. Plan 18-02 will:

1. Add `Fraction? DurationFraction` defaulted-nullable parameter at the END of `MusicalNoteData` constructor (RESEARCH §3 Pattern 1 — additive-field migration).
2. Add `GetBeats` branch on `DurationFraction.HasValue` (RESEARCH §3 Pattern 2).
3. Add `MusicalNoteDataTests.cs` Facts pinning ctor-wiring + GetBeats branching.
4. Add `ByteIdenticalTutorialTests.cs` + `ByteIdenticalShowcaseTests.cs` integration Facts using the Phase 15 `EuclideanByteIdenticalTests.cs` two-runner pattern.
5. Run cmp gate to verify zero byte drift across `examples/tutorial.flow` + `examples/showcase.flow` + 54 `tests/test_*.flow` scripts.

Phase 18's foundation tier is half-shipped. Plan 18-02 closes FRAC-02 and the phase.

### Phase 19 forward-readiness

When Phase 19 (tuplets) starts, it consumes:
- `Fraction` arithmetic primitive (THIS plan)
- `MusicalNoteData.DurationFraction` storage field (Plan 18-02)
- `MusicalNoteData.GetBeats` rational branch (Plan 18-02)

Phase 19 work then becomes: lexer/parser changes for `{N:M ...}q` and `C4/12` syntax, AST node `TupletElement`, NoteStreamCompiler emission of non-null `DurationFraction` values, MIDI tick auto-elevation up to TPQN cap 9600 per D-05, bar-fit validator extension for tuplet sums (Pitfall 2). All of these lean on the Fraction surface this plan shipped.

## Self-Check: PASSED

- [x] `flow-lang/TypeSystem/Fraction.cs` exists (verified via `git show 2092f32 --stat`).
- [x] `flow-lang.Tests/Unit/Phase18/FractionTests.cs` exists (verified via `git show 2092f32 --stat`).
- [x] Commit `2092f32` exists in git log (verified via `git log --oneline | head -1`).
- [x] Build succeeded with 0 errors (verified via `dotnet build flow-sharp.sln` post-commit).
- [x] All 9 Phase18 Facts pass (verified via `dotnet test --filter "FullyQualifiedName~Phase18.FractionTests"` → 9/9 GREEN, 30ms).
- [x] Full suite 296/296 GREEN (verified via `dotnet test flow-sharp.sln --no-build` → 296 passed, 0 failed, 0 skipped, 18s).
- [x] No consumer wiring (verified via `grep -rn "DurationFraction\|new Fraction(" flow-lang/ --include="*.cs" | grep -v Fraction.cs` → 0 lines).
- [x] No deletions in commit (verified via `git diff --diff-filter=D --name-only HEAD~1 HEAD` → empty).

---
*Phase: 18-foundation-rational-duration-arithmetic*
*Plan: 01 (FRAC-01 — Fraction primitive)*
*Completed: 2026-04-26*
