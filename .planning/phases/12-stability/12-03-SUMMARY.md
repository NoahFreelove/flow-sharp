---
phase: 12-stability
plan: 03
subsystem: runtime-thunk
tags: [fix-06, thunk, lazy, exception-dispatch-info, failure-caching, regression-test]
type: execute
wave: 2
depends_on: [01]
requires:
  - flow-lang/Runtime/Thunk.cs (previous hand-rolled double-checked-lock implementation, to be replaced)
  - flow-lang/Interpreter/ExpressionEvaluator.cs (Evaluate method — promoted to virtual to enable test doubles)
  - flow-lang/Ast/Expressions/LiteralExpression.cs (minimal Expression concrete type — unchanged)
  - flow-lang/Runtime/ExecutionContext.cs (ctor (ErrorReporter, InternalFunctionRegistry) — unchanged, consumed API surface)
  - flow-lang/Interpreter/IFunctionInvoker.cs (interface surface — unchanged, stubbed by NoopInvoker)
  - flow-lang.Tests project (xUnit v3 harness established in plan 12-01)
provides:
  - Lazy<Value>-backed Thunk with failure caching via LazyThreadSafetyMode.ExecutionAndPublication
  - Stack-preserving exception re-throw on repeated Force() calls (ExceptionDispatchInfo semantics)
  - flow-lang.Tests/Unit/ThunkTests.cs regression suite (4 Facts)
  - ExpressionEvaluator.Evaluate is now virtual (testability enablement — zero existing behavioral change)
affects:
  - StdLib.If / StdLib.Eval / StdLib.And / StdLib.Or — downstream Force() callers now propagate exceptions instead of silently returning null (never observed in .flow tests — all succeed under pre-fix code path)
  - Any future test that wishes to substitute a custom ExpressionEvaluator — virtual Evaluate opens the door
tech-stack:
  added: []
  patterns:
    - "BCL Lazy<T> with LazyThreadSafetyMode.ExecutionAndPublication as the canonical memoization primitive for runtime thunks — replaces hand-rolled double-checked-lock + boolean-flag pattern"
    - "Virtual evaluator hook for test doubles — CountingEvaluator pattern subclasses ExpressionEvaluator to inject Func<Value> and count invocations"
    - "Four-Fact regression pattern for deferred-evaluation primitives: success-cache, failure-cache, stack-preservation, failure-cache-durability"
key-files:
  created:
    - flow-lang.Tests/Unit/ThunkTests.cs
  modified:
    - flow-lang/Runtime/Thunk.cs (full rewrite: 49 -> 44 lines; removed _expression/_evaluator/_cachedValue/_isEvaluated/_lock fields; single Lazy<Value> field drives the state machine)
    - flow-lang/Interpreter/ExpressionEvaluator.cs (1-token change: "public Value Evaluate" -> "public virtual Value Evaluate" — testability enablement)
key-decisions:
  - "Lazy<Value> alone satisfies both D-05 (ExceptionDispatchInfo stack preservation) and D-06 (thread-safe memoization) — no manual ExceptionDispatchInfo.Capture/.Throw needed; BCL Lazy<T> already wraps that idiom internally per Microsoft docs"
  - "Made ExpressionEvaluator.Evaluate virtual (Rule 3 deviation) to enable a clean CountingEvaluator test double — minimal, non-breaking surface change with zero existing subclasses in the codebase"
  - "Single atomic commit (Task 1 fix + Task 2 tests bundled) for bisectability, preserving TDD discipline by verifying RED state transiently (Force_CachesExceptionAndRethrows would fail under pre-fix Thunk because the old evaluator-clear-after-evaluation path would NullReference on the second Force call) before landing GREEN"
  - "Test double strategy resolved via option (a) — virtual Evaluate + CountingEvaluator subclass. Option (b) (real evaluator + marker expression) was rejected because it couldn't count invocations without production-code hooks and couldn't return arbitrary values for the success-path test"
  - "Added a 4th test (Force_EvaluatorInvokedExactlyOnce_EvenWhenThrowing) beyond the plan's 3-Fact spec — verifies failure-cache durability across 5 repeated Force calls. Strengthens FIX-06 regression coverage without architectural change (Rule 2: essential for correctness verification of the failure-cache contract)"
  - "IsEvaluated (Lazy.IsValueCreated) returns false after a factory-thrown exception — this mirrors pre-refactor behavior where _isEvaluated was only set on success, so the public contract is unchanged even though the failure-cache mechanism is new"
patterns-established:
  - "Lazy<T> with explicit ExecutionAndPublication mode for thunk-like memoization — pattern should propagate to any future deferred-evaluation wrappers in flow-lang runtime"
  - "CountingEvaluator/NoopInvoker test doubles for unit-testing code that depends on ExpressionEvaluator — no need to boot a full FlowEngine"
requirements-completed: [FIX-06]
metrics:
  duration: "~10 min"
  completed: "2026-04-19T14:27:00Z"
  tasks_total: 2
  tasks_completed: 2
  files_created: 1
  files_modified: 2
---

# Phase 12 Plan 03: FIX-06 Thunk Lazy<Value> Failure Caching Summary

**`Thunk.Force()` now caches failed evaluations via `Lazy<Value>` + `ExecutionAndPublication`, re-throwing the original exception with preserved stack on every subsequent call — audit finding C7 (failed thunks silently returning null / retrying with null evaluator) is closed as one atomic, bisectable commit.**

## Performance

- **Duration:** ~10 min
- **Started:** 2026-04-19T14:19:25Z (immediately after plan 12-02 landed)
- **Completed:** 2026-04-19T14:27:00Z
- **Tasks:** 2 (both completed)
- **Files modified:** 2 (`Thunk.cs` rewrite, `ExpressionEvaluator.cs` 1-token virtual)
- **Files created:** 1 (`ThunkTests.cs`)

## Accomplishments

- Replaced the 49-line hand-rolled double-checked-lock Thunk with a 44-line `Lazy<Value>`-backed implementation. The state machine collapses into a single field: `Lazy<Value> _lazy`, initialized with `LazyThreadSafetyMode.ExecutionAndPublication`.
- BCL `Lazy<T>` satisfies both CONTEXT decisions in one BCL primitive:
  - **D-05 (ExceptionDispatchInfo stack preservation):** `Lazy<T>` internally uses `ExceptionDispatchInfo.Capture` + `.Throw()` on every `.Value` access after a factory throw, preserving the original stack trace.
  - **D-06 (thread-safe memoization):** `ExecutionAndPublication` mode guarantees one-time factory execution across threads.
- Made `ExpressionEvaluator.Evaluate` virtual — a 1-token testability enablement. Zero existing subclasses in the codebase, zero behavioral change for all production callers.
- Shipped 4 Facts (3 from plan + 1 durability-strengthener) verifying success cache, failure cache, stack preservation, and evaluator-called-once-even-under-5-throws.

## Task Commits

Single atomic commit (Task 1 fix + Task 2 tests bundled per plan §Commit boundary):

1. **Task 1: Rewrite Thunk.cs as Lazy<Value> wrapper preserving public API** — `557923a` (fix)
2. **Task 2: Write FIX-06 regression tests** — `557923a` (fix, same commit — bundled for bisectability)

Plan metadata commit (STATE/ROADMAP/SUMMARY) follows separately.

## Post-fix code delta

### `Thunk.cs` (49 -> 44 lines, full rewrite)

Removed fields: `_expression`, `_evaluator`, `_cachedValue`, `_isEvaluated`, `_lock`.
Added field: `_lazy: Lazy<Value>`.

Key new line:

```csharp
_lazy = new Lazy<Value>(
    () => evaluator.Evaluate(expression),
    LazyThreadSafetyMode.ExecutionAndPublication);
```

Public API unchanged:

| Member | Before | After |
| --- | --- | --- |
| `Thunk(Expression, ExpressionEvaluator)` | validates non-null | validates non-null |
| `Value Force()` | double-checked lock, evaluate, cache, null out refs | `=> _lazy.Value` |
| `bool IsEvaluated` | returns `_isEvaluated` (success only) | returns `_lazy.IsValueCreated` (success only — Lazy<T> docs) |

### `ExpressionEvaluator.cs`

Exactly one token changed at line 31:

```diff
- public Value Evaluate(Expression expr)
+ public virtual Value Evaluate(Expression expr)
```

## Files Created/Modified

- `flow-lang/Runtime/Thunk.cs` — Rewritten: single `Lazy<Value>` field replaces the 5-field hand-rolled state machine. 49 -> 44 lines. Public API preserved verbatim.
- `flow-lang/Interpreter/ExpressionEvaluator.cs` — `Evaluate` method made virtual (testability enablement, non-breaking). No other changes.
- `flow-lang.Tests/Unit/ThunkTests.cs` — New file. 4 Facts + 2 test-double classes (`CountingEvaluator`, `NoopInvoker`).

## Decisions Made

1. **`Lazy<Value>` replaces hand-rolled locking.** `LazyThreadSafetyMode.ExecutionAndPublication` (default for `Lazy<T>(Func<T>)`, specified explicitly for intent + future-proofing) gives us one-time execution + exception caching in one BCL primitive. Manual `ExceptionDispatchInfo.Capture` / `.Throw()` would be redundant — `Lazy<T>` already uses that idiom per Microsoft docs.
2. **`ExpressionEvaluator.Evaluate` made virtual.** Needed for the `CountingEvaluator` test double (Rule 3 — blocking for test authorship). Zero existing subclasses verified via `grep -r "class.*:\s*ExpressionEvaluator" flow-lang/` (none). Non-breaking surface change.
3. **Single atomic commit.** Task 1 (fix) and Task 2 (tests) landed as commit `557923a`. TDD discipline preserved: `Force_CachesExceptionAndRethrows` would have failed under pre-fix Thunk (second Force on a failed thunk either NRE'd on the nulled `_evaluator` or returned the uninitialized `_cachedValue!`) — the tests prove the new contract.
4. **4 Facts instead of 3.** Added `Force_EvaluatorInvokedExactlyOnce_EvenWhenThrowing` to strengthen failure-cache durability coverage (5 repeated Force calls, CallCount must remain 1). Deviation under Rule 2 — strengthens correctness verification of the failure-cache contract that is the plan's core requirement.
5. **Test-double strategy option (a) — subclass.** Rejected option (b) (real evaluator + marker expression) because it couldn't count invocations without production hooks AND couldn't return `Value.Int(42)` for the success-path test. The virtual-Evaluate enablement is the minimal-surface path to clean test doubles.
6. **`Assert.Same(first, second)` included.** Verifies the STRONGER Lazy<T> contract — not just that *an* `InvalidOperationException` is thrown, but that the *same instance* is cached and re-thrown. Confirms ExceptionDispatchInfo semantics at the instance level, not just type+message level.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Made `ExpressionEvaluator.Evaluate` virtual to enable `CountingEvaluator` test double**

- **Found during:** Task 2 (test authorship)
- **Issue:** Plan recommended approach (a) — subclass `ExpressionEvaluator` with a mock that overrides `Evaluate` — but the current `Evaluate` was non-virtual. Without `virtual`, a subclass `override` won't compile. Approach (b) (real evaluator + LiteralExpression) couldn't satisfy both the invocation-count AND the success-path-returns-42 requirements.
- **Fix:** Added `virtual` keyword at `ExpressionEvaluator.cs:31`. One-token change. Zero existing subclasses (verified via grep), so non-breaking for production callers.
- **Files modified:** `flow-lang/Interpreter/ExpressionEvaluator.cs`
- **Verification:** Full suite runs green (61/62, only spike/c1 RED per baseline — same as pre-plan).
- **Committed in:** `557923a` (bundled with the Thunk fix)

**2. [Rule 2 - Missing Critical] Added a 4th Fact (`Force_EvaluatorInvokedExactlyOnce_EvenWhenThrowing`) beyond the plan's 3-Fact spec**

- **Found during:** Task 2 (test authorship)
- **Issue:** The plan's 3 Facts cover success-cache, failure-cache-via-two-calls, and stack preservation. None explicitly verifies that the failure cache remains durable across MANY Force calls — a subtle regression risk (e.g., a future refactor that accidentally resets the Lazy after N accesses would pass the 2-call test but break durability).
- **Fix:** Added `Force_EvaluatorInvokedExactlyOnce_EvenWhenThrowing` — 5 repeated Force calls on a throwing thunk, asserts `CallCount == 1`.
- **Files modified:** `flow-lang.Tests/Unit/ThunkTests.cs`
- **Verification:** Passes in 0ms.
- **Committed in:** `557923a` (bundled with the Thunk fix)

---

**Total deviations:** 2 auto-fixed (1 blocking, 1 missing-critical — strengthening the failure-cache regression coverage).
**Impact on plan:** Both deviations minimal and non-architectural. The `virtual` change is the cleanest test-double enablement path and is explicitly permitted under the plan's "Claude's Discretion" clause for test doubles. The extra Fact adds durability coverage without architectural change. No scope creep.

## Issues Encountered

1. **Plan sample test 2 asserted `thunk.IsEvaluated == true` after a thrown evaluator.** This is incorrect per Microsoft Lazy<T> docs — `IsValueCreated` returns `false` after a factory-thrown exception (it only flips true on successful materialization). I caught this during test run (`Expected: True, Actual: False`), inspected the Lazy<T> contract, and relaxed the assertion to `Assert.False(thunk.IsEvaluated)`. This actually matches the PRE-REFACTOR behavior too — the old code only set `_isEvaluated = true` inside the try block on the success path, so the public contract is identical pre/post-refactor.
2. **Grep escaping for `Lazy<Value>`.** The plan's acceptance-criteria grep `grep -c "Lazy<Value>"` can fail against ripgrep's generic-bracket handling; used bare `Lazy` as the locator instead. Two hits confirmed (field + constructor), matching the acceptance criterion's intent.

## Verification Results

### Focused filter (`FullyQualifiedName~ThunkTests`)

```
Passed FlowLang.Tests.Unit.ThunkTests.Force_CachesExceptionAndRethrows                         [22 ms]
Passed FlowLang.Tests.Unit.ThunkTests.Force_EvaluatorInvokedExactlyOnce_EvenWhenThrowing       [< 1 ms]
Passed FlowLang.Tests.Unit.ThunkTests.Force_RethrowPreservesStackTrace                         [17 ms]
Passed FlowLang.Tests.Unit.ThunkTests.Force_CachesSuccessValue                                 [< 1 ms]
Total tests: 4  Passed: 4  Failed: 0
```

### Full solution (`dotnet test flow-sharp.sln`)

```
Total tests: 62  Passed: 61  Failed: 1
```

The single failure is `spike/c1-musical-context-body.flow` — **expected RED baseline carried forward from plans 12-01 and 12-02** (CONTEXT D-11; slated to flip GREEN in plan 12-04 after Interpreter.cs `return;` -> `break;` fix). Baseline delta: +4 new Facts (all green), 0 new failures. FIX-06 introduces zero regressions.

### Downstream callers (verified untouched)

- `StdLib.If` at `StdLib.cs:331-345` — calls `args[1].As<Thunk>().Force()` — unchanged.
- `StdLib.Eval` at `StdLib.cs:327` — calls `thunk.Force()` — unchanged.
- `StdLib.And` / `StdLib.Or` at `StdLib.cs:360, 365` — unchanged.

`git show 557923a --stat` confirms only 3 files in the commit: `Thunk.cs`, `ExpressionEvaluator.cs`, `ThunkTests.cs`.

## Threat Flags

None new. Per the plan's threat register:

- `T-12-04` (Information Disclosure via stack-frame leakage) — disposition `accept`. Stack traces are the debugging interface; no PII traverses Thunk. FIX-06 IS the V7 Error Handling control.
- `T-12-05` (Tampering via unsafe thread-safety mode) — disposition `mitigate`. Explicit `LazyThreadSafetyMode.ExecutionAndPublication` present in ctor. Future-proof against runtime default changes.

No new surface introduced. No network, auth, file-access, or schema changes.

## Self-Check: PASSED

- FOUND: `flow-lang/Runtime/Thunk.cs` (modified; contains `Lazy<Value>` ×2, `LazyThreadSafetyMode.ExecutionAndPublication`, `public Value Force()`, `public bool IsEvaluated`)
- FOUND: `flow-lang/Interpreter/ExpressionEvaluator.cs` (modified; `public virtual Value Evaluate` at line 31)
- FOUND: `flow-lang.Tests/Unit/ThunkTests.cs` (created; contains `namespace FlowLang.Tests.Unit`, 4 `[Fact]` declarations, `CountingEvaluator`, `Force_CachesExceptionAndRethrows`, `CallCount`)
- FOUND: commit `557923a` (`git log --oneline -1 | grep 557923a` matches)
- Test evidence: 4/4 ThunkTests green; 61/62 full suite (only spike/c1 RED, expected baseline)
- File line count: `wc -l flow-lang/Runtime/Thunk.cs` = 44 (down from 49)
- Acceptance criteria: all pass (Lazy<Value> count ≥ 2 ✓, ExecutionAndPublication present ✓, Force/IsEvaluated surface preserved ✓, old state fields removed ✓, build clean ✓, baseline preserved ✓)

## Next Plan Readiness

- Plan 12-04 next: FIX-07a — Interpreter.cs `return;` -> `break;` in musical-context validation paths (Timesig/Tempo/Swing/Pan/Gain/Key). Will flip spike/c1 to GREEN.
- Thunk is now robust for the downstream If/And/Or/Eval callers. If any future plan introduces new lazy-evaluation paths, the `Lazy<Value>` pattern is the established template.

---
*Phase: 12-stability*
*Completed: 2026-04-19*
