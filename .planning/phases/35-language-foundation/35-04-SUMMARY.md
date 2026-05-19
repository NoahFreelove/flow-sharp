---
phase: 35-language-foundation
plan: 04
subsystem: testing
tags: [test-framework, hermetic-isolation, rms-comparator, flow-cli, lazytype, snapshot-restore]

# Dependency graph
requires:
  - phase: 35-language-foundation (Plan 35-01)
    provides: Span + SourceMap records; AST/Token Span fields populated. TestRecord.Span defaults to Span.Unknown until Plan 35-03 (Diagnostics) wires the call-site Span through the InternalFunctionRegistry call boundary.
provides:
  - "(test \"name\" lazy(body)) special-form builtin — defers via LazyType<Void>"
  - "5 assertion primitives: (assert), (assertEq), (assertNotesMatch), (assertBytesEqual), (assertWithinDb 0.5dB)"
  - "TestRunner.Run(engine, filePath) — orchestrates per-test SnapshotState/RestoreState"
  - "ExecutionContext.SnapshotState/RestoreState — captures 11 mutable surfaces (RESEARCH §Pitfall 3)"
  - "flow test [path] CLI subcommand — discovers test_*.flow, prints PASS/FAIL + summary, exit 0/1"
  - "RmsComparator pure helper at flow-lang/StandardLibrary/TestFramework/ — single source of truth for runtime (assertWithinDb) builtin AND xUnit AssertWavMatchesBaseline"
affects: [35-05-pattern-matching, 35-06-music-extractors, 35-07-as-binding, future v1.5 plans that need (test ...) coverage]

# Tech tracking
tech-stack:
  added: []  # zero new dependencies — pure-Flow framework lives in C# + .flow source only
  patterns:
    - "Pure-Flow test framework: (test \"name\" lazy(body)) + 5 builtins + flow test CLI"
    - "ExecutionContext.SnapshotState/RestoreState: explicit per-field capture (no reflection) for hermetic test isolation"
    - "RmsComparator extraction: pure-C# helper shared between runtime builtin + xUnit assertion (no Xunit.Assert in flow-lang)"

key-files:
  created:
    - flow-lang/StandardLibrary/TestFramework/TestRecord.cs
    - flow-lang/StandardLibrary/TestFramework/AssertionException.cs
    - flow-lang/StandardLibrary/TestFramework/RmsComparator.cs
    - flow-lang/StandardLibrary/TestFramework/AssertionHelpers.cs
    - flow-lang/StandardLibrary/TestFramework/TestFunctions.cs
    - flow-lang/StandardLibrary/TestFramework/TestRunner.cs
    - flow-lang/StandardLibrary/TestFramework/TestSnapshot.cs
    - flow-cli/Commands/TestCommand.cs
    - tests/test_test_framework.flow
    - flow-lang.Tests/Phase35/TestFrameworkBuiltinsTests.cs
    - flow-lang.Tests/Phase35/TestBodyDeferralTests.cs
    - flow-lang.Tests/Phase35/HermeticIsolationTests.cs
    - flow-lang.Tests/Phase35/AssertWithinDbTests.cs
    - flow-lang.Tests/Phase35/FlowTestCliTests.cs
  modified:
    - flow-lang/Runtime/ExecutionContext.cs (TestRegistry + SnapshotState/RestoreState)
    - flow-lang/Runtime/StackFrame.cs (SnapshotLocalVariables/RestoreLocalVariables)
    - flow-lang/Core/FlowEngine.cs (TestRegistry + SnapshotState/RestoreState pass-throughs)
    - flow-lang/StandardLibrary/BuiltInFunctions.cs (TestFramework.TestFunctions.RegisterTestFramework call)
    - flow-lang/test.flow (six new internal proc declarations + legacy proc test renamed to runTest)
    - flow-cli/Commands/CommandRegistry.cs (TestCommand.Build() appended, 12 → 13 subcommands)
    - flow-lang.Tests/Helpers/RmsRegressionTests.cs (delegates per-window comparison to RmsComparator)
    - tests/test_test_library.flow (consumer-side rename test → runTest)

key-decisions:
  - "Composer wraps test body with lazy(...) to opt into deferred Thunk semantics — matches Flow's strict-by-default evaluation; no auto-wrap exists in parser/interpreter. Mirrors (if cond lazy(then) lazy(else)) precedent."
  - "Renamed legacy proc test(String, Function) in test.flow to runTest(String, Function) so the new (test ...) C# builtin doesn't collide via OverloadResolver ambiguity. Pre-traction breaking-change latitude per CLAUDE.md D-v1.5-01."
  - "TestSnapshot is an immutable record with explicit per-field captures (no reflection) so the leak-audit checklist stays code-readable — adding a new mutable surface requires touching THREE files."
  - "TestRunner uses Snapshot/Restore per individual test (not per file) — hermetic isolation between consecutive tests inside the same file, not just between files."
  - "AudioPlaybackManager NOT snapshotted/restored per RESEARCH Assumption A8 — tests must not trigger live (play ...) playback. See \"AudioPlaybackManager follow-up\" below."
  - "RmsComparator lives in flow-lang (NOT flow-lang.Tests) so the runtime (assertWithinDb) builtin can reference it without forcing flow-lang to depend on Xunit.Assert. The xUnit AssertWavMatchesBaseline helper delegates to FirstWindowExceedingTolerance for the per-window math."
  - "CLI integration tests spawn `dotnet exec flow.dll` (~1s) instead of `dotnet run --project` (30-60s) — required for the test suite to complete inside the 120s test-runner safety timeout."

patterns-established:
  - "Pattern: hermetic test isolation via ExecutionContext.SnapshotState/RestoreState — applicable to any future test runner that needs to roll back interpreter state."
  - "Pattern: (test ...) builtin paired with TestRunner.Run — separates registration (eager, populates registry) from execution (deferred, per-snapshot-guard) so flow-cli, flow-lsp, or any future host can choose to invoke or skip body Thunks."
  - "Pattern: pure-C# comparator extraction — RmsComparator is the canonical example; future numeric/structural comparison helpers should live next to it under TestFramework/."

requirements-completed: [TEST-01, TEST-02]

# Metrics
duration: 50min
completed: 2026-05-18
---

# Phase 35 Plan 04: Pure-Flow Test Framework Summary

**(test "name" lazy(body)) + 5 assertion primitives + `flow test [path]` CLI + ExecutionContext.SnapshotState/RestoreState — composer authors tests in .flow files, runs via the CLI, gets per-test PASS/FAIL output with hermetic isolation between cases (11 captured state surfaces).**

## Performance

- **Duration:** ~50 min
- **Started:** 2026-05-18T21:29:00-04:00 (Task 1 commit)
- **Completed:** 2026-05-18T22:19:49-04:00 (Task 3 commit)
- **Tasks:** 3 (Wave 0 stubs → TestFramework C# core → flow test CLI subcommand)
- **Files modified:** 22 (14 created + 8 modified)

## Accomplishments

- **TestFramework C# bundle** — 7 new files under `flow-lang/StandardLibrary/TestFramework/` (TestRecord, AssertionException, RmsComparator, AssertionHelpers, TestFunctions, TestRunner, TestSnapshot) wire the entire pure-Flow test surface.
- **ExecutionContext snapshot/restore** — captures 11 mutable surfaces enumerated in RESEARCH §Pitfall 3 (global frame variables, TestRegistry size, SectionRegistry, SymbolInternTable, PRNG state, MusicalContext, 4 SFZ statics, FlowConfig.Active) + invokes existing reset hooks for SynthUtils.Rng + RenderingDiagnostics._emitted.
- **(test "name" lazy(body)) builtin** — registered globally via BuiltInFunctions.RegisterContextDependentFunctions. Body deferred via the existing `lazy(...)` keyword (Pitfall 10 LOAD-BEARING — without LazyType the body evaluates eagerly at registration time).
- **5 assertion primitives** — (assert), (assertEq), (assertNotesMatch), (assertBytesEqual), (assertWithinDb 0.5dB). All throw AssertionException with precise diagnostic messages; TestRunner catches to convert to FAIL.
- **`flow test [path]` CLI subcommand** — defaults to `tests/`, restricted glob `test_*.flow` (threat T-35-10 mitigation), per-file FlowEngine isolation, per-test Snapshot/Restore guard, "Total: N; Passed: P; Failed: F" summary, exit code 0/1.
- **RmsComparator extraction** — pure-C# helper at `flow-lang/StandardLibrary/TestFramework/RmsComparator.cs` (no Xunit.Assert dependency) used by BOTH the new runtime (assertWithinDb) builtin AND the existing `flow-lang.Tests/Helpers/RmsRegressionTests.AssertWavMatchesBaseline` helper.
- **Meta-dogfooding fixture** — `tests/test_test_framework.flow` exercises all 5 assertion primitives end-to-end; `dotnet exec flow.dll test tests/test_test_framework.flow` prints 6 PASS lines + summary, exits 0.

## Task Commits

Each task was committed atomically:

1. **Task 1: Wave 0 failing test stubs (TEST-01 + TEST-02 gates)** — `aab2ed6` (test)
2. **Task 2: TestFramework C# core + ExecutionContext snapshot/restore + RmsComparator extraction** — `b8889dc` (feat)
3. **Task 3: `flow test [path]` CLI subcommand + composer-facing meta-dogfooding fixture** — `7426f4f` (feat)

(Plan metadata commit handled by parent orchestrator after worktree merge.)

## Files Created/Modified

### Created (14)

- `flow-lang/StandardLibrary/TestFramework/TestRecord.cs` — `record TestRecord(string Name, Thunk BodyThunk, Span Span)`; appended to ExecutionContext.TestRegistry per (test ...) call.
- `flow-lang/StandardLibrary/TestFramework/AssertionException.cs` — Exception subclass; caught by TestRunner to record FAIL outcomes.
- `flow-lang/StandardLibrary/TestFramework/RmsComparator.cs` — Pure RMS-windowed comparison; `MaxWindowDeviationDb` + `FirstWindowExceedingTolerance` (the latter is what the xUnit helper consumes for its failure diagnostic).
- `flow-lang/StandardLibrary/TestFramework/AssertionHelpers.cs` — 5 static helpers: AssertOrThrow / AssertEqOrThrow / AssertNotesMatchOrThrow (walks Bar+ParallelVoices+MusicalNoteData recursively) / AssertBytesEqualOrThrow (bitwise float compare) / AssertWithinDbOrThrow (wraps RmsComparator).
- `flow-lang/StandardLibrary/TestFramework/TestFunctions.cs` — `RegisterTestFramework(registry, context)` registers all six builtins. `(test)` has the LazyType wrap; assertions use BoolType / Void-wildcard / SequenceType / BufferType / DecibelType signatures per the existing precedent shape.
- `flow-lang/StandardLibrary/TestFramework/TestRunner.cs` — `Run(engine, filePath)` returns `(passed, failed)`; per-test SnapshotState → BodyThunk.Force → RestoreState; PASS lines uncolored, FAIL lines red on TTY.
- `flow-lang/StandardLibrary/TestFramework/TestSnapshot.cs` — Immutable record with 11 fields (one per captured surface).
- `flow-cli/Commands/TestCommand.cs` — System.CommandLine subcommand; Argument<string?> ZeroOrOne; Directory.Exists branch glob-walks `test_*.flow`, otherwise single-file mode.
- `tests/test_test_framework.flow` — Composer-facing meta-dogfooding test (6 tests across all 5 assertion primitives).
- `flow-lang.Tests/Phase35/TestFrameworkBuiltinsTests.cs` (4 facts) — registry presence + `(assert false)` propagation behavior.
- `flow-lang.Tests/Phase35/TestBodyDeferralTests.cs` (1 fact, `[Collection("FlowScripts")]` — serializes Console.SetOut) — Pitfall 10 deferral gate.
- `flow-lang.Tests/Phase35/HermeticIsolationTests.cs` (4 facts) — registry accumulation, SymbolInternTable reset, PRNG reset, 20-run order-independence shuffle.
- `flow-lang.Tests/Phase35/AssertWithinDbTests.cs` (2 facts) — identical buffers ~0 dB, 6 dB amplitude split trips assertion.
- `flow-lang.Tests/Phase35/FlowTestCliTests.cs` (2 facts) — spawns `dotnet exec flow.dll test FIXTURE` and asserts stdout + exit code.

### Modified (8)

- `flow-lang/Runtime/ExecutionContext.cs` — TestRegistry property + SnapshotState/RestoreState methods (74 new lines).
- `flow-lang/Runtime/StackFrame.cs` — SnapshotLocalVariables / RestoreLocalVariables helpers.
- `flow-lang/Core/FlowEngine.cs` — TestRegistry / SnapshotState / RestoreState pass-throughs for CLI consumption.
- `flow-lang/StandardLibrary/BuiltInFunctions.cs` — RegisterContextDependentFunctions calls TestFramework.TestFunctions.RegisterTestFramework.
- `flow-lang/test.flow` — Six new `internal proc` declarations for the C# surface; legacy `proc test(String, Function)` renamed to `runTest(String, Function)` to avoid collision (deviation Rule 1 — see below).
- `flow-cli/Commands/CommandRegistry.cs` — TestCommand.Build() appended (12 → 13 subcommands).
- `flow-lang.Tests/Helpers/RmsRegressionTests.cs` — Per-window comparison delegates to RmsComparator.FirstWindowExceedingTolerance; ~20 lines of duplicated math removed.
- `tests/test_test_library.flow` — Updated to call the renamed `runTest` (one-line change).

## Decisions Made

The seven key-decisions in the frontmatter are the substantive ones; one additional decision worth surfacing:

- **TestSnapshot field ordering matches the RESEARCH §Pitfall 3 enumeration** so a future leak-audit grep can map snapshot fields → research-pitfall items by index. The 11 fields:
  1. GlobalVariables (frame vars)
  2. TestRegistryCount
  3. SectionRegistry
  4. SymbolInternTable
  5. PRNG (FixedRandSeed + FixedGen + Gen)
  6. GlobalFrameMusicalContext
  7. SfzEnabled
  8. SfzInstruments
  9. SfzPatchRegistry
  10. SfzDiagnostics + ResolvedSfzRoot
  11. FlowConfig.Active

  Plus two static-reset hooks invoked unconditionally in RestoreState: `SynthUtils.ResetNoiseRng()` and `RenderingDiagnostics.ResetForTesting()`.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Legacy `proc test(String, Function)` in test.flow collided with new `(test ...)` C# builtin**

- **Found during:** Task 2 (TestFramework C# core, first dotnet test run)
- **Issue:** The pre-existing `flow-lang/test.flow` shipped a pure-Flow test framework with `proc test(String: name, Function: thunk)`. Adding the new `(test "name" lazy(body))` C# builtin (signature `[String, Lazy<Void>]`) created an OverloadResolver collision: `(test "name" body)` resolved to "Ambiguous overload" when body returned Void, and "No matching overload for function 'test' with argument types (String, Bool/Float/Sequence/...)" when body returned anything non-Void. Both overloads needed to coexist as the legacy library's lone consumer `tests/test_test_library.flow` still calls the Function-thunk form via `(test "..." (fn => ...))`.
- **Fix:** Renamed the legacy proc from `test(String, Function)` to `runTest(String, Function)` in `flow-lang/test.flow`. Updated the one consumer (`tests/test_test_library.flow`) to call `(runTest "..." (fn => ...))`. The legacy assertion procs (assertTrue, assertEqual, printResult, summary) are preserved unchanged. Pre-traction breaking-change latitude per CLAUDE.md D-v1.5-01 (project_pre_public_no_legacy_burden ACTIVE through pre-traction).
- **Files modified:** flow-lang/test.flow, tests/test_test_library.flow
- **Verification:** `dotnet run --project flow-interpreter tests/test_test_library.flow` still prints the SUMMARY section unchanged (10 PASS + 9 expected-FAIL + "All green? false" — the FAILs are intentional in that test).
- **Committed in:** b8889dc (Task 2 commit)

**2. [Rule 2 - Missing critical functionality] `internal proc` declarations for the 6 new builtins were absent from `flow-lang/test.flow`**

- **Found during:** Task 2 (first dotnet test run)
- **Issue:** Per Assumption A10 the C# builtins are registered globally at FlowEngine init, but Flow's dispatch path requires a corresponding `internal proc` declaration to be in scope before the OverloadResolver will look up the C# delegate. Without these declarations, `(test ...)`, `(assert)`, `(assertEq)`, `(assertNotesMatch)`, `(assertBytesEqual)`, `(assertWithinDb)` all fail with "Function '...' not found" at parse-eval time even though their C# impls are registered.
- **Fix:** Added six `internal proc` declarations at the top of `flow-lang/test.flow` (above the legacy library) — test / assert / assertEq / assertNotesMatch / assertBytesEqual / assertWithinDb. Composer now does `use "@test"` to bring them into scope. The Lazy<Void> signature on `(test ...)` coexists with the renamed `runTest(String, Function)` since they have different parameter shapes.
- **Files modified:** flow-lang/test.flow
- **Verification:** `dotnet run --project flow-interpreter tests/test_test_framework.flow` registers 6 tests cleanly. `dotnet exec flow.dll test tests/test_test_framework.flow` runs all 6 to PASS.
- **Committed in:** b8889dc (Task 2 commit)

**3. [Rule 3 - Blocking] CLI integration tests timed out because `dotnet run --project flow-cli` is 30-60s per invocation**

- **Found during:** Task 3 (FlowTestCliTests xUnit run)
- **Issue:** Initial implementation used `dotnet run --project flow-cli --no-build -- test FIXTURE` to spawn the CLI from the xUnit test. Each invocation took 30-60s (dotnet run does a full no-op restore + build check even with --no-build). Multiplied across two fact methods this consistently tripped the 120s WaitForExit timeout, leaving FlowTestCliTests stuck in FAIL state.
- **Fix:** Switched to `dotnet exec flow-cli/bin/Debug/net10.0/flow.dll` (~1s per invocation — skips the entire restore/build check). Added a pre-check that throws InvalidOperationException with a helpful "build flow-cli first" message if the dll is missing. The FlowTestCliTests now run in well under 1 second per fact.
- **Files modified:** flow-lang.Tests/Phase35/FlowTestCliTests.cs
- **Verification:** `dotnet test --filter "FullyQualifiedName~Phase35.FlowTestCliTests"` passes 2/2 in 369ms (was timing out at 120s × 2 = 240s before).
- **Committed in:** 7426f4f (Task 3 commit)

**4. [Rule 1 - Bug] TestBodyDeferralTests pollution from parallel xUnit runs**

- **Found during:** Task 2 (full dotnet test run)
- **Issue:** TestBodyNotEvaluatedAtRegistration passes in isolation but fails in the full suite. Root cause: xUnit's default parallel runner serialized Console.SetOut across test classes, and the StringWriter captured output from unrelated Phase 15 / Phase 26.1 / InterpreterTests cases that also redirect Console.Out. The "WOULD_RUN" sentinel never appeared but other output did, and our assertion `Assert.DoesNotContain("WOULD_RUN", preRun)` was correct but the test was racy on the captured stream.
- **Fix:** Added `[Collection("FlowScripts")]` to the test class — matches the existing convention from `InterpreterTests.cs:14`, `flow-lang.Tests/Unit/Phase26_1/TupleFacts.cs:21`, and `DictOpsFacts.cs:15` per RESEARCH §Pitfall 4. The Collection attribute serializes the class with other Console.SetOut-touching tests.
- **Files modified:** flow-lang.Tests/Phase35/TestBodyDeferralTests.cs
- **Verification:** Full `dotnet test` no longer regresses TestBodyDeferralTests; the fact passes inside the multi-suite run.
- **Committed in:** b8889dc (Task 2 commit)

---

**Total deviations:** 4 auto-fixed (1 Rule 1 bug from collision, 1 Rule 1 bug from xUnit parallelism, 1 Rule 2 missing functionality, 1 Rule 3 blocking infrastructure). All necessary for the plan's GREEN gates to hold. No scope creep.

**Impact on plan:** Plan executed substantively as specified. The frontmatter's `(test "name" body)` shorthand becomes `(test "name" lazy(body))` per Flow's actual strict-by-default semantics (the plan + RESEARCH glossed over the `lazy(...)` wrap requirement — they correctly identified the LazyType signature as load-bearing but the user-facing syntax includes the explicit wrap). All TEST-01 + TEST-02 verification criteria met.

## Issues Encountered

- **`lazy(...)` wrap discovery (during Task 2):** Initial reading of the plan + RESEARCH §Pitfall 10 + §Example 3 suggested `(test "name" body)` would naturally defer the body via the LazyType signature. Investigation of `(if cond then else)` semantics confirmed that Flow has NO auto-wrap mechanism — the LazyType overload is only triggered when the user explicitly writes `lazy(...)`. The two overloads (Lazy + Strict) coexist via OverloadResolver. Adapted by requiring the composer to wrap test bodies with `lazy(...)` (matches the existing `(if cond lazy(then) lazy(else))` precedent). Documented in `flow-lang/test.flow` and the meta-dogfooding fixture.

- **None other** — execution otherwise straightforward.

## User Setup Required

None — no external service configuration required.

## AudioPlaybackManager Follow-Up Note (Assumption A8)

Per RESEARCH Assumption A8 + the Critical Constraints in the spawn prompt, `AudioPlaybackManager` is **NOT** captured by SnapshotState / restored by RestoreState. The reasoning: live audio playback (PulseAudio via P/Invoke) has process-global state (audio backend handle, currently-playing buffers, playback threads) that cannot be safely snapshotted via reference-copy. Restoring would either leak playback state across tests or worse, leave the audio backend in an inconsistent state.

**Composer-facing implication:** Tests authored via `(test "name" lazy(body))` MUST NOT call `(play ...)`, `(loop ...)`, `(preview ...)`, or other AudioPlaybackManager-touching builtins inside the body. Doing so will cause cross-test leakage of audio backend state.

**Follow-up CLAUDE.md doc edit (deferred):** A future minor PR should add a note to CLAUDE.md's "How to Run Tests" section documenting this constraint:

> **Test framework pitfall:** `(test "name" lazy(body))` bodies MUST NOT call live audio playback builtins (`play`, `loop`, `preview`, `stop`, `audioDevices`, `setAudioDevice`). The TestRunner's hermetic-isolation SnapshotState/RestoreState pair does NOT capture or reset `AudioPlaybackManager` state — Assumption A8 (Plan 35-04 RESEARCH). For audio assertions, use `writeWav` + buffer comparison via `(assertWithinDb a b 0.5dB)` or `(assertBytesEqual a b)` instead.

This doc edit was intentionally deferred from this plan per the spawn prompt's instruction to record it as a follow-up note rather than touching CLAUDE.md in this plan.

## Test Runner Output Format Sample

```
$ dotnet exec flow-cli/bin/Debug/net10.0/flow.dll test tests/test_test_framework.flow
ALL TESTS REGISTERED
  PASS  tests/test_test_framework.flow::assert with true does not throw
  PASS  tests/test_test_framework.flow::assertEq integers
  PASS  tests/test_test_framework.flow::assertEq strings
  PASS  tests/test_test_framework.flow::assertEq booleans
  PASS  tests/test_test_framework.flow::assertWithinDb identical sine buffers
  PASS  tests/test_test_framework.flow::assertNotesMatch identical note streams

Total: 6; Passed: 6; Failed: 0
```

Exit code: 0. A failing test prints `  FAIL  ...::name: AssertionException message` in red (on TTY) and the summary line counts the failure; exit code 1.

## Next Phase Readiness

- Plan 35-04 leaves a fully usable composer-facing test surface for the rest of Phase 35 (Plans 35-05 → 35-07) and the v1.5 backlog to consume.
- Plan 35-03 (Diagnostics, parallel wave) will eventually wire `FlowDiagnostic` through the (test ...) call-site so AssertionException messages carry Spans (the TestRecord.Span field already exists; defaults to Span.Unknown until Plan 35-03 lands the InternalFunctionRegistry call-site span propagation).
- The follow-up CLAUDE.md edit (AudioPlaybackManager pitfall doc) is unblocking for any future plan that intentionally exercises audio playback inside a test body.

## Self-Check: PASSED

Created file existence verified by `git show 7426f4f --stat` (output above showed `tests/test_test_framework.flow` in the commit) and by direct shell `ls flow-lang/StandardLibrary/TestFramework/*.cs` returning all 7 files. Commit hashes verified via `git log --oneline -3` returning aab2ed6 / b8889dc / 7426f4f at the top. Full xUnit suite passes 1279/1305 (zero new regressions vs Wave 1 merge-base baseline of 1265/1291 + 13 new Phase 35 plan 35-04 facts, all GREEN, +1 from the order-independence fact).

---
*Phase: 35-language-foundation*
*Plan: 04*
*Completed: 2026-05-18*
