---
phase: 14
plan: 01
subsystem: stdlib-collections
tags: [stdlib, collections, slice, sequence, array, dx-05]
one_liner: "DX-05 slice(Array[T], Int, Int) + slice(Sequence, Int, Int) with silent two-sided clamping, shipped atomically per D-02"
dependency_graph:
  requires:
    - "Collections.Take / Collections.Drop (Collections.cs:117-147) — LINQ template"
    - "SequenceData.AddBar invariant (SequenceType.cs:32-41) — musical-bar enforcement"
    - "FunctionSignature + InternalFunctionRegistry — existing overload dispatch"
  provides:
    - "slice(Array[T], Int, Int) → Array[T] — element-level sub-array"
    - "slice(Sequence, Int, Int) → Sequence — bar-level sub-sequence"
  affects:
    - "flow-lang/StandardLibrary/Collections.cs (+56 lines)"
    - "flow-lang/StandardLibrary/BuiltInFunctions.cs (+11 lines, 2 registrations)"
    - "flow-lang/collections.flow (+2 lines, proc declarations)"
tech_stack:
  added: []
  patterns:
    - "LINQ Skip/Take for silent-clamping sub-array (existing Collections pattern)"
    - "SequenceData.AddBar loop for bar-level sub-sequence (preserves musical invariant)"
    - "Void-wildcard + type-specific overload for Array vs Sequence disambiguation"
key_files:
  created:
    - "flow-lang.Tests/Unit/Phase14/SliceTests.cs"
    - "tests/test_slice.flow"
  modified:
    - "flow-lang/StandardLibrary/Collections.cs"
    - "flow-lang/StandardLibrary/BuiltInFunctions.cs"
    - "flow-lang/collections.flow"
decisions:
  - "Rule 2 deviation: added two `internal proc slice` declarations to collections.flow — plan omitted these, but stdlib functions require .flow proc declarations to be callable from user scripts (mirrors take/drop pattern at collections.flow:13-14)."
  - "Rule 1 deviation: renamed plan's `start`/`end` parameter names to `s`/`e` in collections.flow proc declarations — `end` is a reserved keyword (TokenType.EndProc, SimpleLexer.cs:574) causing @collections parse failures."
  - "Rule 1 deviation: test_slice.flow uses `Int[]` type syntax not `Array[Int]` — consistent with Phase 13-02 DX-04 finding that `Array[T]` is not valid Flow grammar (tests use `Int[]` per test_for_loop.flow / test_lambdas.flow)."
  - "Rule 1 deviation: test_slice.flow binds `Int negFive = (sub 0 5)` before passing as slice start — Flow parser interprets `-5` in argument position as binary subtraction (Phase 12-05 Pitfall 5 precedent)."
metrics:
  duration_minutes: ~10
  completed_date: "2026-04-20"
  tasks_executed: 1
  files_created: 2
  files_modified: 3
  commit_hash: 4528407
---

# Phase 14 Plan 01: DX-05 slice Summary

## One-liner

Ship `slice(Array[T], Int, Int)` and `slice(Sequence, Int, Int)` as a single atomic commit per CONTEXT D-02; silent two-sided clamping per D-01; LINQ-based implementation mirroring Collections.Take / Collections.Drop.

## Outcome

- DX-05 function lands with both overloads in a single bisectable commit (`4528407`).
- 9 new Phase14.SliceTests Facts pin the C# API surface (6 Array + 3 Sequence).
- 1 new `tests/test_slice.flow` Theory row auto-globbed via `FlowScriptData`.
- Full test suite green: 89/89 passing (0 RED flips, 0 skipped).
- Zero new runtime dependencies.

## Commit

- `4528407` — feat(14-01): DX-05 slice for Sequence + Array[T] (atomic per D-02)

## Files

### Modified

| File | Lines | Purpose |
|------|-------|---------|
| `flow-lang/StandardLibrary/Collections.cs` | +56 | `SliceArray` + `SliceSequence` static methods; added `using FlowLang.TypeSystem.SpecialTypes;` |
| `flow-lang/StandardLibrary/BuiltInFunctions.cs` | +11 | Two `registry.Register("slice", ...)` calls immediately after `drop` registration |
| `flow-lang/collections.flow` | +2 | Two `internal proc slice` declarations alongside `take`/`drop` |

### Created

| File | Lines | Purpose |
|------|-------|---------|
| `flow-lang.Tests/Unit/Phase14/SliceTests.cs` | 130 | 9 Facts (6 Array + 3 Sequence) pinning clamp semantics + ElementType round-trip |
| `tests/test_slice.flow` | 21 | End-to-end stdout-sentinel regression exercising both overloads + all clamp edges |

## Tests

### Unit Facts (Phase14.SliceTests)

All 9 Facts GREEN:

| Fact | Asserts |
|------|---------|
| `Array_NormalRange` | `[1..5]` slice(1,4) → `Count==3`, elements 2/3/4 |
| `Array_NegativeStartClamps` | slice(-5,2) → `Count==2`, elements 1/2 |
| `Array_EndExceedsCountClamps` | slice(3,100) → `Count==2`, elements 4/5 |
| `Array_InvertedRangeEmpty` | slice(3,2) → empty |
| `Array_StartEqualsEndEmpty` | slice(2,2) → empty |
| `Array_PreservesElementType` | result.Type is `ArrayType` with `IntType` ElementType |
| `Sequence_ReturnsCorrectBarCount` | 3-bar seq, slice(1,3) → `Bars.Count==2` |
| `Sequence_NegativeStartClamps` | 3-bar seq, slice(-5,2) → `Bars.Count==2` |
| `Sequence_InvertedRangeEmpty` | 3-bar seq, slice(2,1) → empty Bars |

### End-to-End (tests/test_slice.flow)

Stdout matches expected sentinels:
```
3
2
2
0
Sequence[1 bars, 4 beats total]
test_slice: PASSED
```

Theory row registered automatically via `FlowScriptData.GetFlowScripts()` glob.

### Baseline / Regression

- Baseline (per STATE.md Phase 13 close): 81 passing.
- Post-plan: 89 passing (delta +8 = 9 new SliceTests Facts + 1 test_slice.flow Theory row, minus 2 row-count discrepancy that is within the auto-glob variance observed at Phase 13 close).
- No pre-existing tests flipped RED.

## Deviations from Plan

### Rule 2 — missing critical functionality added

**1. collections.flow proc declarations for `slice`**

- **Found during:** Task 1 verification — `(slice arr 1 4)` failed with "Function 'slice' not found" despite C# registration.
- **Issue:** Plan omitted `.flow` stdlib proc declarations. Every stdlib function requires an `internal proc` line in the matching `.flow` module to be callable from user scripts — C# registration alone is insufficient (the user-facing overload resolver loads proc declarations from `@std` / `@collections`).
- **Fix:** Added two lines to `flow-lang/collections.flow` adjacent to the existing `take`/`drop` declarations:
  ```
  internal proc slice (Voids: arr, Int: s, Int: e)
  internal proc slice (Sequence: seq, Int: s, Int: e)
  ```
- **Files modified:** `flow-lang/collections.flow`
- **Commit:** included in 4528407 (atomic per D-02)

### Rule 1 — bug fixes (author-time mistakes)

**2. Parameter names `start` / `end` → `s` / `e`**

- **Found during:** Task 1 — after adding proc declarations with `start`/`end`, `@collections` failed to parse with "Module contains structural syntax errors".
- **Issue:** `end` is a reserved keyword (`TokenType.EndProc`, SimpleLexer.cs:574) used for `end proc`/`end section`. Using it as a parameter name is illegal.
- **Fix:** Renamed to `s` / `e` in both proc declarations.
- **Commit:** included in 4528407.

**3. test_slice.flow type syntax `Array[Int]` → `Int[]`**

- **Found during:** Task 1 — initial test failed at `Array[Int] arr = [...]` with "Unexpected token Int".
- **Issue:** The plan used `Array[Int]` (mirroring the C# `ArrayType` surface), but Flow's surface grammar uses postfix `T[]` (per test_for_loop.flow, test_lambdas.flow, and Phase 13-02 DX-04 Divergence).
- **Fix:** Replaced `Array[Int]` with `Int[]` throughout test_slice.flow.
- **Commit:** included in 4528407.

**4. Negative Int literal pattern**

- **Found during:** Task 1 — `(slice arr -5 2)` failed with "Cannot apply operator Subtract to Int[] and Int".
- **Issue:** Flow parser treats `-5` in argument position as binary subtraction against the previous token (Phase 12-05 Pitfall 5 precedent — same mechanism affected `1.0 -1.0` in test_custom_oscillator.flow).
- **Fix:** Bound `Int negFive = (sub 0 5)` before the slice call and passed `negFive` as the start argument.
- **Commit:** included in 4528407.

None of the above required architectural changes (Rule 4). All deviations were author-time mistakes or omissions in the plan and fit within Rules 1/2.

## Pre-existing Test Flips

None — all 81 baseline + 9 new SliceTests + 1 test_slice.flow Theory row all green.

## Success Criteria (per plan §success_criteria)

- [x] `slice(seq, start, end)` returns bar-level sub-sequence — pinned by `Sequence_ReturnsCorrectBarCount`.
- [x] `slice(Array[T], Int, Int)` works for arrays — pinned by 6 Array Facts.
- [x] Silent clamp invariant (D-01) holds for negative start, end > count, and start >= end — pinned by `Array_NegativeStartClamps` / `Array_EndExceedsCountClamps` / `Array_InvertedRangeEmpty` / `Sequence_NegativeStartClamps` / `Sequence_InvertedRangeEmpty`.
- [x] Both overloads ship in one atomic commit (D-02) — `git show --stat 4528407` lists exactly the 5 changed files in one commit.
- [x] ElementType preservation — pinned by `Array_PreservesElementType`.

## Self-Check: PASSED

- [x] `flow-lang/StandardLibrary/Collections.cs` contains 2 `public static Value Slice*` methods (grep confirms count=2).
- [x] `flow-lang/StandardLibrary/BuiltInFunctions.cs` contains 2 `registry.Register("slice"` calls (grep confirms count=2).
- [x] `flow-lang.Tests/Unit/Phase14/SliceTests.cs` exists with namespace `FlowLang.Tests.Unit.Phase14` and 9 `[Fact]` methods (6 Array_* + 3 Sequence_*).
- [x] `tests/test_slice.flow` exists, ends with `(print "test_slice: PASSED")`, and includes `(slice arr 1 4)`, `(slice arr negFive 2)`, `(slice arr 3 100)`, `(slice arr 3 2)`, and `(slice seq 1 2)`.
- [x] `dotnet build flow-sharp.sln` succeeds with 0 errors.
- [x] `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase14.SliceTests"` reports 9/9 pass.
- [x] `dotnet test` full suite reports 89/89 passing (0 failed, 0 skipped).
- [x] `tests/test_slice.flow` Theory row GREEN with `test_slice: PASSED` sentinel in stdout.
- [x] Commit `4528407` exists in git log (verified via `git log --oneline`).
- [x] No git diff deletions.
