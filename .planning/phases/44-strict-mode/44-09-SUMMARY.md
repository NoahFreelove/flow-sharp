---
plan: 44-09
status: complete
phase: 44-strict-mode
started: 2026-05-24
completed: 2026-05-25
---

# Plan 44-09 Summary — Axis C strict surface

## Goal Achieved

Layered the strict-mode behavior for `(and)`/`(or)`/`(not)`/`if`/cross-type comparisons/`(equals)` on top of Plan 44-08's Void-wildcard charitable handlers. D-13 Dict type-strict matching pinned via dedicated regression test. Non-strict callers preserve existing charitable behavior byte-identical (Pitfall 5 two-run cmp-clean contract).

## Tasks

| # | Title | Commit | Result |
|---|-------|--------|--------|
| 1 | Strict-aware `(and)`/`(or)` Bool-required + AxisCBoolRequiredTests | `299149a` | RED→GREEN |
| 2 | Strict `(not)`/`if` Bool-required + cross-type comparison + set-theoretic equals + Dict-strict regression pin | `d6bd503` | RED→GREEN |

## Files Modified

- `flow-lang/StandardLibrary/BuiltInFunctions.cs` — strict-branch wiring for `(not)`/`if` Void-wildcard handlers; cross-type comparison strict gate
- `flow-lang/StandardLibrary/StdLib.cs` — strict-mode Bool checks layered on Plan 44-08's `IfTruthy`/`NotCharitable`/`AndLastTruthy`/`OrLastTruthy` helpers
- `flow-lang/StandardLibrary/Utils.cs` — set-theoretic equals (D-11) under strict; existing `LooseEquals` retained for non-strict
- `flow-lang/StandardLibrary/ConversionFunctions.cs` — primitive numeric cross-cast overloads (`(double Int|Long|Float|Double)` etc., 16 registrations in new `numericPrims` loop)
- `flow-lang/std.flow` — 16 surface declarations for primitive numeric cross-casts

## Files Created

- `flow-lang.Tests/Integration/Phase44/AxisCBoolRequiredTests.cs` (Task 1)
- `flow-lang.Tests/Integration/Phase44/CrossTypeComparisonStrictTests.cs` (Task 2)
- `flow-lang.Tests/Integration/Phase44/DictTypeStrictRegressionTests.cs` (Task 2)

## Verification

- **Phase 44 xUnit:** 195/195 GREEN in worktree (up from 171 at Wave 4 close)
- **Plan 44-09 net new Facts:** ~24 (AxisC Bool-required + CrossType strict + Dict-strict regression)
- **D-11 set-theoretic equals:** `(equals 1 1.0)` strict → false; non-strict → true (LooseEquals preserved)
- **D-13 Dict type-strict:** Int 1 ≠ Float 1.0; Symbol #foo ≠ String "foo" — pinned in both modes via empty-`(dict)` + `(set)` construction pattern
- **Cross-type comparison strict error message:** verbatim `[strict] cross-type comparison <T1> vs <T2> — use explicit (double x) / (int x)`

## Deviations

### Mid-flight socket error recovery (Task 2)

Task 2 was interrupted mid-execution by an API socket error (`The socket connection was closed unexpectedly`) after the production-code edits landed but before committing. The orchestrator salvaged the in-flight work by:

1. Spot-checking the worktree: build green, 190/195 Phase44 tests passing
2. Diagnosing the 5 remaining failures
3. Applying surgical fixes (described below)
4. Committing as the Task 2 deliverable

### Rule 1 auto-fix — primitive numeric cross-cast overloads (gap exposed by CrossTypeComparisonStrictTests)

Plan 44-04 registered `(double X)` only for music-type sources (Decibel/Hertz/Cent/Millisecond/Second/Semitone). Plan 44-09's strict cross-type comparison error message says "use explicit `(double x) / (int x)`" — but `(double 1)` (where `1` is Int) had no matching overload, making the strict escape hatch unusable.

Fix: registered 16 primitive numeric cross-cast overloads (`(double Int)`, `(double Long)`, `(double Float)`, `(double Double)` × 4 extractors) in `ConversionFunctions.RegisterReverseExtractors` + corresponding `internal proc` declarations in `std.flow`. Identity casts are no-ops; narrowing follows `StdLib.DoubleToInt` floor convention. This closes the gap that the strict-mode error message promised.

### Rule 1 auto-fix — DictTypeStrictRegressionTests construction shape

Initial test draft used `Dict<Number, String> d = (dict 1 "one" 1.0 "one-point-oh")` — but Number is NOT in the hashable key set per CLAUDE.md (`Int, Long, Float, String, Symbol, Note, Chord, Tuple-of-hashables`), and Dict literal `(dict K V K V…)` infers `Dict<K, V>` from the FIRST key (no heterogeneous-key dict literal in Flow's type system).

Fix: rewrote 4 tests to use empty `(dict)` (typed `Dict<Void, V>`) + incremental `(set)` for heterogeneous keys. Same runtime hash behavior, declarable shape. The D-13 type-strict matching contract is exercised identically.

## Known Caveats

None — all 5 post-merge failures resolved.

## Cross-Plan Integration

- Consumes Plan 44-08's Void-wildcard `print`/`if`/`not`/`and`/`or` handler dispatch (`CallerStrictMode` snapshot)
- Consumes Plan 44-03's strict-tier filter (drops numeric widening under strict) — exposes the need for primitive cross-cast overloads (this plan fills that gap)
- Consumes Plan 44-04's reverse-extractor pattern — extends it with primitive numeric source types
- Cross-type comparison error message guides composers to use `(double x)` / `(int x)` — those now exist for both music-type AND primitive numeric sources
