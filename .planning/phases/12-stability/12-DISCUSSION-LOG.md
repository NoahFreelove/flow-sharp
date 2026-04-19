# Phase 12: Stability — Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in 12-CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-19
**Phase:** 12-stability
**Areas discussed:** Test scope reframing, FIX-07a fix mechanism, FIX-06 Thunk caching, Plan structure & cadence (incl. xUnit framework adoption)

---

## Pre-Discussion: Empirical Test Run

Before presenting gray areas, the three "failing" tests were executed against `master`:

| Test | Audit claim | Actual result |
|------|-------------|---------------|
| `tests/test_while_loop.flow` | break/continue not interpreted | Passes (output `5,3,0,0,1,0,3`) — break/continue ARE wired (Interpreter.cs:120-124,321-322,354-355) |
| `tests/test_custom_oscillator.flow` | `range(Int,Int)` missing | `range` works; fails on `(if (gt frames1 0) "PASS" "FAIL")` — missing `if(Bool,String,String)` overload |
| `tests/test_full_song.flow` | `bpm`/`createStereoTrack`/`renderBars` missing | All three implemented; fails because `tests/output/` directory doesn't exist |

This finding shaped the gray areas presented.

---

## Test scope reframing

| Option | Description | Selected |
|--------|-------------|----------|
| Edit REQUIREMENTS, fix actual failures | Update REQUIREMENTS.md TEST-01/02/03 to reality; ship `if`-overload + auto-mkdir | ✓ |
| Keep audit wording, document already-done | Plans verify each, write 'verified-existing' note + numeric regression test | |
| Treat as 'make tests green', planner decides | No requirements update; planner picks surgical fixes | |
| Audit-spike v2 first | Phase 11.1 mini-spike to re-verify each TEST-0N before Phase 12 commits | |

**User's choice:** Edit REQUIREMENTS, fix actual failures (recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| `exportWav` auto-creates parent dirs | One-line FileIO.cs change; broader DX win | ✓ |
| Add gitignore-tracked `tests/output/.keep` | Smaller blast radius; only fixes this test | |
| Rewrite test to write to current dir | Removes failure without touching production code | |

**User's choice:** exportWav auto-creates parent dirs (recommended)

**Notes:** TEST-01 closed (range exists), TEST-02 closed (break/continue work), TEST-03 reframed (missing `if(Bool,String,String)` overload + exportWav must auto-create parent dirs).

---

## FIX-07a fix mechanism

| Option | Description | Selected |
|--------|-------------|----------|
| Lock break-and-continue | Phase 11's proposal verbatim: 7 returns→breaks; body runs under partial/default context | ✓ |
| Skip body on validation error, but keep frame | Reports error but does NOT run body — current behavior, RED test stays RED | |
| Fail-fast: throw on invalid context | Breaks v1.0 soft-failure contract (success criterion 5) | |
| Defer body via continuation | Larger refactor, not justified for a 7-line fix | |

**User's choice:** Lock break-and-continue (recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| Reuse spike c1 RED + existing musical-context tests | spike c1 flips RED→GREEN + test_musical_context_errors.flow continues passing | ✓ |
| Add dedicated soft-failure regression test | New test/test_soft_failure_contract.flow | |
| Trust the spike test alone | Only c1-musical-context-body.flow | |

**User's choice:** Reuse spike c1 RED + existing musical-context tests (recommended)

**Notes:** Frame balance via try/finally at Interpreter.cs:287-290 must NOT be altered. The 7 early-exit lines: 151, 164, 178, 224, 240, 255, 263.

---

## FIX-06 Thunk caching

| Option | Description | Selected |
|--------|-------------|----------|
| `ExceptionDispatchInfo` | Capture via `.Capture(ex)`; re-raise via `.Throw()`. Preserves original stack trace | ✓ |
| Store and rethrow same instance | Simpler but mangles stack trace on every re-raise | |
| Store type only, re-raise fresh | Loses original cause chain | |

**User's choice:** ExceptionDispatchInfo (recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| Single-threaded assumption | No lock; matches existing Thunk.cs simplicity | |
| Lock around Force() | Future-proof against concurrency | ✓ |
| Defer decision to v1.3 concurrency phase | Note as future work | |

**User's choice:** Lock around Force()

| Option | Description | Selected |
|--------|-------------|----------|
| Per-thunk lock (`lock (this)`) | Standard double-checked locking | |
| Static lock | Single static lock across all thunks | |
| `Lazy<T>` with `LazyThreadSafetyMode` | Replace state with `Lazy<Value>` set to `ExecutionAndPublication` | ✓ |

**User's choice:** Lazy<T>-style with LazyThreadSafetyMode

**Notes:** Refactor Thunk internals to wrap evaluator in `Lazy<Value>` with `ExecutionAndPublication` mode. Failure-cache via ExceptionDispatchInfo plugs into the value factory.

---

## Plan structure & cadence

| Option | Description | Selected |
|--------|-------------|----------|
| 5 plans, one per concrete change | FIX-05/06/07a + (if-overload+exportWav) + (REQUIREMENTS edits) | ✓ (initial) |
| 3 thematic plans | Confirmed bugs / Test unblock / Verification rollup | |
| 2 plans by intent | Bug fixes / Test suite green | |
| 6 plans, one per requirement ID | Maximum traceability; trivial plans for closed TEST-01/02 | |

**User's choice:** 5 plans, one per concrete change (recommended) — superseded by 6-plan structure after xUnit addition

| Option | Description | Selected |
|--------|-------------|----------|
| Pin specific values via print + grep | `(if (eq actual expected) "PASS" "FAIL")` — matches existing convention | |
| Add a tiny .NET xUnit test project | New flow-lang.Tests project; CI-friendly | ✓ |
| Stdout golden file comparison | Paired `.expected` files; fragile to whitespace | |

**User's choice:** Add a tiny .NET xUnit test project

---

## xUnit (architecture follow-up)

| Option | Description | Selected |
|--------|-------------|----------|
| C# unit tests for FIX-* internals | New project tests Collections.Init / Thunk.Force / ExecuteMusicalContext directly | ✓ |
| xUnit drives `.flow` scripts as test cases | Replaces bash for-loop with real test runner | |
| xUnit ONLY for Phase 12 numeric assertions | Tiny project, just 3 FIX-* tests; no expansion plan | |
| Reconsider — use `.flow` scripts after all | Stay with stdout convention | |

**User's choice:** C# unit tests for FIX-05/06/07a internals (recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| Keep `.flow` tests as-is, xUnit added alongside | No migration in Phase 12 | |
| Migrate all `.flow` tests to xUnit harness | Wrap every `.flow` test in xUnit Theory cases | ✓ |
| Migrate only Phase 11 spike tests | Bring tests/spike/c1-c5 under xUnit; split convention | |

**User's choice:** Migrate all `.flow` tests to xUnit harness

| Option | Description | Selected |
|--------|-------------|----------|
| Wrap-as-Theory: xUnit runs each `.flow` file | `[Theory]` case per file; scripts NOT rewritten; runner changes from bash loop to xUnit | ✓ |
| Full rewrite: `.flow` logic recreated in C# | Loses `.flow`-as-living-spec property; ~5x scope inflation | |
| Hybrid: wrap existing + new tests in C# | Two conventions coexist | |

**User's choice:** Wrap-as-Theory (recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| xUnit Theory expects GREEN after FIX-07a commit | Initially RED in test report; FIX-07a commit flips it GREEN. Bisectability preserved | ✓ |
| Keep c1 RED via `[Skip]` until FIX-07a | Cleaner CI but loses 'visible RED that flips' signal | |
| Migrate spike tests after FIX-07a lands | Avoids RED state but creates temporal exception | |

**User's choice:** xUnit Theory expects GREEN after FIX-07a commit (recommended)

---

## Plans (revised — 6 plans)

| Option | Description | Selected |
|--------|-------------|----------|
| 6 plans | 12-01 xUnit scaffold + wrap; 12-02 FIX-05; 12-03 FIX-06; 12-04 FIX-07a; 12-05 if-overload+exportWav; 12-06 REQUIREMENTS+verification | ✓ |
| 7 plans (split scaffold from migration) | Lets FIX-05 ship without waiting for full migration | |
| 5 plans (bundle xUnit into FIX-05) | Tighter; couples framework adoption to a bug fix | |
| Defer `.flow` migration to Phase 13 | Smaller Phase 12 but Phase 13 was scoped to validation, not test migration | |

**User's choice:** 6 plans (recommended)

---

## Claude's Discretion

- Exact xUnit Theory case naming convention
- Exact wording of REQUIREMENTS.md status updates
- Internal field names in the Lazy-refactored Thunk
- Whether to use `[InlineData]` per `.flow` script vs `[ClassData]` source generator
- Substring-assert expected-stdout fragments for each Theory case

## Deferred Ideas

- CI integration of `dotnet test`
- Native C# rewrites of selected `.flow` tests
- Lock-granularity tuning for Thunk after concurrency phase exists
- DryWetMidi / PulseAudio P/Invoke isolation in xUnit
- Full rewrite of `.flow` test intent into C# Asserts (explicitly rejected)
