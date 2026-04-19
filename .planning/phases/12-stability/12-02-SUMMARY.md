---
phase: 12-stability
plan: 02
subsystem: stdlib-collections
tags: [fix-05, collections, init, empty-array-guard, regression-test]
type: execute
wave: 2
depends_on: [01]
requires:
  - flow-lang/StandardLibrary/Collections.cs (Head/Last error-format convention, Collections.cs:48-82)
  - flow-lang/Runtime/Value.cs (Value.Array, Value.Int, As<T> — unchanged, consumed API surface)
  - flow-lang/TypeSystem/PrimitiveTypes/IntType.cs (IntType.Instance — consumed)
  - flow-lang/TypeSystem/PrimitiveTypes/VoidType.cs (VoidType.Instance — consumed)
  - flow-lang.Tests project (xUnit v3 harness established in plan 12-01)
provides:
  - Collections.Init empty-array guard with exact error shape "Cannot get init of empty array"
  - flow-lang.Tests/Unit/CollectionsTests.cs regression suite (3 Facts)
affects:
  - Any .flow script that called init([]) and relied on silent-[] return (none found in tests/*.flow — audit confirmed prior to fix)
tech-stack:
  added: []
  patterns:
    - "empty-collection guard: if (elements.Count == 0) throw new InvalidOperationException(\"Cannot get <op> of empty array\") — mirrors Head (Collections.cs:55-56) and Last (Collections.cs:78-79)"
    - "native xUnit Fact triad for stdlib primitive: empty→throws, single→boundary, multi→happy-path"
key-files:
  created:
    - flow-lang.Tests/Unit/CollectionsTests.cs
  modified:
    - flow-lang/StandardLibrary/Collections.cs (+3 lines: empty-count guard inserted between elements-extraction and Take LINQ)
decisions:
  - "Error message exactly 'Cannot get init of empty array' — matches Head/Last suffix pattern 'Cannot get <op> of empty array' verified against Collections.cs:55-56 and :78-79 (CONTEXT D-07 authority)"
  - "BuiltInFunctions.cs:352-353 registration NOT modified — signature (ArrayType(VoidType)) is stable; only the implementation now raises on empty (plan explicit constraint)"
  - "Task 1 tests staged but NOT committed separately; bundled with Task 2 fix into ONE atomic commit for bisectability (plan §Commit boundary)"
  - "VoidType.Instance used for empty-array element type (since the array has no elements to infer from) — matches Collections.List:19-20 when args.Count == 0"
metrics:
  duration: "~1 min"
  completed: "2026-04-19T14:19:25Z"
  tasks_total: 2
  tasks_completed: 2
  files_created: 1
  files_modified: 1
---

# Phase 12 Plan 02: FIX-05 Collections.Init Empty-Array Guard Summary

Adds a single empty-array guard to `Collections.Init` that raises `InvalidOperationException("Cannot get init of empty array")`, bringing it into exact alignment with the existing `Head` and `Last` error-format convention; ships with three native xUnit Facts as regression coverage.

## One-liner

`init([])` now errors cleanly in the same format as `head([])` / `last([])` — the FIX-05 audit-finding C6 silent-failure path (LINQ `Take(-1)` returning `[]`) is closed as one atomic, bisectable commit.

## Post-fix code delta (Collections.cs:84-95)

**Lines changed:** +3 (two-line guard + one blank separator), from 9-line function to 12-line function.

```csharp
public static Value Init(IReadOnlyList<Value> args)
{
    var arr = args[0];
    if (arr.Type is not ArrayType arrayType)
        throw new InvalidOperationException($"Expected Array, got {arr.Type}");

    var elements = arr.As<IReadOnlyList<Value>>();
    if (elements.Count == 0)                                                // NEW
        throw new InvalidOperationException("Cannot get init of empty array"); // NEW
                                                                            // NEW (blank)
    return Value.Array(elements.Take(elements.Count - 1).ToArray(), arrayType.ElementType);
}
```

`grep -c "elements.Count == 0" flow-lang/StandardLibrary/Collections.cs` now returns **4** (Head :55, Last :78, Init :91, Empty :101) — plan expected ≥ 3.

## Commit

| Hash    | Message                                                                                                |
| ------- | ------------------------------------------------------------------------------------------------------ |
| 6e5a960 | fix(12-02): init([]) throws InvalidOperationException matching head/last semantics (FIX-05)            |

**Files in commit (verified via `git show --stat HEAD`):**
- `flow-lang/StandardLibrary/Collections.cs` — +3 lines
- `flow-lang.Tests/Unit/CollectionsTests.cs` — +41 lines (new file)
- Total: 2 files, +44 insertions, 0 deletions. **Registration at BuiltInFunctions.cs:352-353 was NOT modified — confirmed untouched per plan constraint.**

## Tasks executed

| # | Name                                                                       | Status | Notes                                                                                             |
| - | -------------------------------------------------------------------------- | ------ | ------------------------------------------------------------------------------------------------- |
| 1 | Write failing FIX-05 unit tests (RED) against current Collections.Init     | done   | `Init_EmptyArray_Throws` FAILED pre-fix as predicted (no-exception-thrown); Tests 2 and 3 passed. File staged, not committed separately. |
| 2 | Add empty-array check to Collections.Init, matching Head/Last pattern      | done   | Edit applied at Collections.cs:91-92. All 3 Facts GREEN post-fix. Full suite regression unchanged. |

## Verification results

### Focused filter (`FullyQualifiedName~CollectionsTests`)

```
Passed FlowLang.Tests.Unit.CollectionsTests.Init_EmptyArray_ThrowsInvalidOperationException [11 ms]
Passed FlowLang.Tests.Unit.CollectionsTests.Init_SingleElementArray_ReturnsEmpty            [ 3 ms]
Passed FlowLang.Tests.Unit.CollectionsTests.Init_MultipleElements_ReturnsAllButLast         [ 2 ms]
Total tests: 3  Passed: 3  Failed: 0
```

### Full solution (`dotnet test flow-sharp.sln`)

```
Total tests: 58  Passed: 57  Failed: 1
```

The single failure is `spike/c1-musical-context-body.flow` — **expected RED baseline carried forward from plan 12-01** (CONTEXT D-11; slated to flip GREEN in plan 12-04 after Interpreter.cs `return;`→`break;` fix). This is the exact baseline declared in plan 12-01's SUMMARY; FIX-05 introduces zero new regressions.

## Decisions made

1. **Error message is literal `"Cannot get init of empty array"`** — not `$"..."`, not programmatic. This exactly matches Head/Last string form at Collections.cs:56 and :79 (CONTEXT D-07 authority).
2. **Atomic bundling** — Task 1's test file and Task 2's fix landed as one commit. Bisect pinpoints `6e5a960` when interrogating `init([])` behavior, and the tests were written against pre-fix code to prove RED before landing GREEN (TDD discipline preserved despite single-commit).
3. **Registration unmodified** — `BuiltInFunctions.cs:352-353` (init's `(ArrayType(VoidType))` signature) left untouched. Only the implementation now raises on empty; callers in `.flow` scripts see the new behavior without any signature-level change.
4. **VoidType element type for empty-array fixture** — `Value.Array(new List<Value>(), VoidType.Instance)` matches the convention at `Collections.List:19-20` when `args.Count == 0`. The Init function never reads `arrayType.ElementType` on the throw path, so the fixture element type is irrelevant to the assertion — using `VoidType.Instance` is the minimum-information choice.

## Deviations from Plan

None — plan executed exactly as written. Verified items:

- Plan-predicted +3-line delta in Collections.cs: **confirmed** (`git show --stat HEAD` shows `Collections.cs | 3 +++`).
- Plan-predicted 2-file commit: **confirmed**.
- Plan-predicted RED state (Init_EmptyArray fails pre-fix, others pass): **confirmed verbatim in xUnit output**.
- Plan-predicted GREEN state (all 3 Facts pass post-fix): **confirmed**.
- Plan-predicted baseline regression (only spike/c1 still RED, same as 12-01): **confirmed**.
- Value/type/namespace surface (`Value.Array(IReadOnlyList<Value>, FlowType)`, `VoidType.Instance`, `IntType.Instance`): **verified pre-write against source**; no namespace adjustments required.

No surprises to report. No auth gates. No Rule 1-4 deviations triggered.

## Threat Flags

None. The threat model predicted `T-12-03` (DoS: .flow scripts relying on silent-return break loudly) with disposition `accept` — this IS the fix. No new attack surface introduced; error-path hardening only.

## Self-Check: PASSED

- FOUND: flow-lang/StandardLibrary/Collections.cs (modified, contains "Cannot get init of empty array")
- FOUND: flow-lang.Tests/Unit/CollectionsTests.cs (created, contains 3 [Fact] declarations, contains namespace FlowLang.Tests.Unit)
- FOUND: commit 6e5a960 (git log --oneline -1 matches "fix(12-02): init([]) throws InvalidOperationException matching head/last semantics (FIX-05)")
- Test evidence: 3/3 CollectionsTests green; 57/58 full suite (only spike/c1 RED, expected baseline)
