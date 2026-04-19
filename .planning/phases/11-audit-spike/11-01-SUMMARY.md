---
phase: 11-audit-spike
plan: 01
subsystem: interpreter
tags: [audit-spike, musical-context, interpreter, spike, red-test]

verdict: Confirmed
evidence_path: tests/spike/c1-musical-context-body.flow
next_action: Phase 12 FIX-07a — replace early `return` with `break` (or equivalent) at Interpreter.cs:151, 164, 178, 224, 240, 255, 263 so the body loop at 270-284 executes under partial/default musical context; flip tests/spike/c1-musical-context-body.flow GREEN.

# Dependency graph
requires:
  - phase: 10-roadmap-v12
    provides: Phase 11 scope defined in ROADMAP.md with 4 success criteria (esp. #4 no production code changes)
provides:
  - Empirical verdict on C1: body is silently skipped after validation error (hypothesis A confirmed)
  - RED regression test tests/spike/c1-musical-context-body.flow ready for Phase 12 FIX-07a
  - Inline AUDIT-VERIFIED marker at Interpreter.cs:291 (greppable audit trail)
affects: [11-06-verification, 12-fix-stability, phase-12-FIX-07a]

# Tech tracking
tech-stack:
  added: []
  patterns: [AUDIT-VERIFIED inline-comment marker (D-02); tests/spike/ RED-commit pattern (D-08)]

key-files:
  created:
    - tests/spike/c1-musical-context-body.flow
  modified:
    - flow-lang/Interpreter/Interpreter.cs  # single-line AUDIT-VERIFIED comment at line 291 only

key-decisions:
  - "Used `key NotAKey` (bare identifier) rather than `key \"NotAKey\"` (string literal) in probe 3 because the Flow parser requires an Identifier token, not a StringLiteral, for the key-context operand (Parser.cs:476). This is a plan-spec correction, not a semantic change — probe 3 still exercises the early-return path at Interpreter.cs:255."
  - "Plan acceptance criterion said `dotnet run ... exits 0`; actual shipped behavior is exit 1 when any error is reported via ErrorReporter (confirmed against tests/test_musical_context_errors.flow and tests/test_error_masking.flow). The soft-failure invariant is *continuation*, not zero-exit. Evidence is stdout presence/absence of sentinel strings — unambiguous either way."
  - "Committed the test RED per D-08. Phase 12 FIX-07a flips it GREEN by letting the body loop run under default/partial musical context rather than skipping."

patterns-established:
  - "AUDIT-VERIFIED marker: `// AUDIT-VERIFIED YYYY-MM-DD: C[N] — <verdict> (<evidence path>)` at the end of the method/section referenced by the audit. Greppable via `grep -rn \"AUDIT-VERIFIED\"` for a repo-wide audit trail."
  - "Spike test convention: `tests/spike/c[N]-<slug>.flow` with four numbered probes (3 invalid-path + 1 valid-path control), each probe printing a distinct sentinel string so stdout reveals the verdict without any assertion harness."

requirements-completed: [SPIKE-01]

# Metrics
duration: 5min
completed: 2026-04-18
---

# Phase 11 Plan 01: SPIKE-01 C1 Verdict Summary

**C1 confirmed: `ExecuteMusicalContext` silently drops the block body after any validation error — the `try/finally` correctly balances the frame, but seven `return` statements inside the `try` exit before the body-execution loop runs.**

## Performance

- **Duration:** ~5 min
- **Completed:** 2026-04-18
- **Tasks:** 2
- **Files modified:** 2 (1 created, 1 single-line comment insert)

## Verdict

**Confirmed — Hypothesis A (pitfalls agent) is correct.** The architecture researcher was right that the `finally { _context.PopFrame(); }` at Interpreter.cs:286-289 balances the stack — there is no frame leak. But the *real* bug the audit meant is different and genuine: every `return` at lines 151, 164, 178, 224, 240, 255, 263 exits the method before reaching the `foreach (var stmt in ctx.Body)` loop at lines 270-284, so the block body is silently dropped.

Users get:
- stderr: the validation error (e.g., `Tempo must be positive, got -5`)
- stdout: no trace that the 1..N statements inside the block were skipped
- Subsequent top-level statements still execute (`_returnValue` is never set).

This is a composer-DX failure: a malformed `tempo -5 { | C4 D4 E4 F4 | }` drops the notes without saying so.

## Evidence

Run: `dotnet run --project flow-interpreter tests/spike/c1-musical-context-body.flow`
Exit code: 1 (soft-failure convention — errors reported, execution continues)

**stdout (observed):**
```
Flow Language Interpreter v0.1

c1-probe1-after-block
c1-probe2-after-block
c1-probe3-after-block
c1-probe4-body-ran
c1-probe4-after-block
```

**stderr (observed):**
```
tests/spike/c1-musical-context-body.flow:9:1: error: Tempo must be positive, got -5
tests/spike/c1-musical-context-body.flow:15:1: error: Swing must be between 0.0 and 1.0, got 2
tests/spike/c1-musical-context-body.flow:24:1: error: Unrecognized key 'NotAKey'. Valid keys include: Cmajor, Aminor, Fsharpmajor, etc.
```

**Sentinel matrix:**

| Sentinel                  | Expected (A: bug) | Expected (B: no bug) | Actual   | Supports   |
| ------------------------- | ----------------- | -------------------- | -------- | ---------- |
| `c1-probe1-body-ran`      | ABSENT            | PRESENT              | ABSENT   | A          |
| `c1-probe1-after-block`   | PRESENT           | PRESENT              | PRESENT  | both       |
| `c1-probe2-stmt1`         | ABSENT            | PRESENT              | ABSENT   | A          |
| `c1-probe2-stmt2`         | ABSENT            | PRESENT              | ABSENT   | A          |
| `c1-probe2-after-block`   | PRESENT           | PRESENT              | PRESENT  | both       |
| `c1-probe3-body-ran`      | ABSENT            | PRESENT              | ABSENT   | A          |
| `c1-probe3-after-block`   | PRESENT           | PRESENT              | PRESENT  | both       |
| `c1-probe4-body-ran`      | PRESENT           | PRESENT              | PRESENT  | control    |
| `c1-probe4-after-block`   | PRESENT           | PRESENT              | PRESENT  | control    |

All four "body" sentinels for invalid-path probes 1-3 are ABSENT. The control probe 4 (`tempo 120 { ... }`) runs its body — confirming the bug is specifically tied to the validation-error path, not a general interpreter fault.

## Inline marker

- **File:** `flow-lang/Interpreter/Interpreter.cs`
- **Line:** 291 (immediately after the `finally` block's closing brace at line 290, which itself closes `ExecuteMusicalContext`)
- **Comment:** `// AUDIT-VERIFIED 2026-04-18: C1 — Confirmed: body skipped after validation error (tests/spike/c1-musical-context-body.flow)`
- **Greppability:** `grep -rn "AUDIT-VERIFIED 2026-04-18: C1" flow-lang/` returns exactly one hit (`flow-lang/Interpreter/Interpreter.cs:291`).
- **Diff stat:** `+1 insertion(+), 0 deletions` on Interpreter.cs — respects ROADMAP success criterion 4 (no production logic changes in Phase 11).

## Next action

→ **Phase 12 FIX-07a.** The RED test `tests/spike/c1-musical-context-body.flow` is Phase 12's handoff. Proposed fix approach (per PITFALLS.md Pitfall 1):

1. Replace each `return;` at Interpreter.cs:151, 164, 178, 224, 240, 255, 263 with `break;` (exits the `switch` but NOT the method).
2. Let the body loop at 270-284 execute under the partial/default `musicalCtx` — for tempo/swing/pan/gain/dynamics, `GetMusicalContext()` inheritance already supplies defaults; for key, unresolved numerals already render as rests, so the body still produces audible-if-imperfect output rather than silence.
3. DO NOT convert the `return` to `throw` — violates soft-failure error model (`CLAUDE.md`, `PROJECT.md`).
4. DO NOT change `ReportError` to set `_returnValue` — pairs incorrectly with C2.
5. Flip the spike test GREEN: updated expectation is that `c1-probe1-body-ran`, `c1-probe2-stmt1`, `c1-probe2-stmt2`, `c1-probe3-body-ran` ALSO appear on stdout, because the body still runs under default/inherited context.

## Task Commits

1. **Task 1: Author tests/spike/c1-musical-context-body.flow** — `2b59433` (test)
2. **Task 2: Record AUDIT-VERIFIED comment in Interpreter.cs** — `a74db38` (docs)

## Files Created/Modified

- `tests/spike/c1-musical-context-body.flow` — 4-probe empirical test for C1; committed RED per D-08; force-added via `git add -f` because `tests/` and `*.flow` are in `.gitignore` (matches the prevailing pattern for already-tracked tests)
- `flow-lang/Interpreter/Interpreter.cs` — single-line AUDIT-VERIFIED comment inserted at line 291 (between ExecuteMusicalContext method closing `}` at line 290 and ExecuteForStatement method opener at line 293)

## Decisions Made

- **Probe 3 uses `key NotAKey` (identifier) not `key "NotAKey"` (string literal)** — the plan's mandated content used a string literal, but Flow's parser (`Parser.cs:476`) rejects that form with `Expected key name (e.g., Cmajor, Aminor). Got StringLiteral '"NotAKey"'`, which halts parsing before any probe runs. Bare identifiers reach `IsValidKey(keyName)` at `Interpreter.cs:250` and correctly trigger the early-return at line 255.
- **Accepted exit code 1 as pass criterion** — the plan text says "exits 0," but empirical check against `tests/test_musical_context_errors.flow` (also reports a tempo error) and `tests/test_error_masking.flow` (reports function-not-found) shows exit 1 is the shipped convention when any error is reported via `ErrorReporter`. The verdict itself rests on sentinel presence/absence in stdout, which is unambiguous.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Replaced `key "NotAKey"` with `key NotAKey` in probe 3**
- **Found during:** Task 1 verification run
- **Issue:** Plan's required content used `key "NotAKey"` (string literal). Flow's `Parser.cs:476` expects `Identifier`, not `StringLiteral`, so the test halted with a parse error before any probe ran. Without this fix, Task 1 could not produce the verdict evidence it was authored to produce.
- **Fix:** Changed probe 3 to `key NotAKey` and added two `Note:` lines explaining the parser constraint. `NotAKey` is still an unknown key identifier, so `IsValidKey(keyName)` returns false and the early return at Interpreter.cs:255 is still exercised — the probe's semantic purpose (force the key-path validation error) is preserved.
- **Files modified:** tests/spike/c1-musical-context-body.flow
- **Verification:** Post-fix run yields the three expected validation errors on stderr and no probe-3 body sentinel on stdout.
- **Committed in:** `2b59433` (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (Rule 3 — blocking parser grammar mismatch in plan spec)
**Impact on plan:** No scope creep. The fix is a single-line semantic-preserving correction in the test file; ROADMAP criterion 4 (no production source changes except the one AUDIT-VERIFIED comment) is untouched.

## Issues Encountered

- **Exit code spec mismatch (noted above).** Not an execution problem — the plan's acceptance criterion text says "exits 0" but the shipped soft-failure convention is exit 1 on any reported error. Verdict evidence is stdout-based, so the spec mismatch is cosmetic. Flagged here so the verifier knows to consult sentinel presence, not `$?`.

## TDD Gate Compliance

Not applicable — plan `type: execute`, not `type: tdd`. However, D-08 mandates that confirmed-bug tests commit RED for Phase 12 to flip. This plan satisfies D-08:
- `2b59433` test(11-01): add RED spike — the test currently fails the Phase-12-target assertion (4 body sentinels absent from stdout). No GREEN commit follows; Phase 12 FIX-07a is the GREEN commit.

## Self-Check: PASSED

- tests/spike/c1-musical-context-body.flow exists: FOUND
- Commit 2b59433 (Task 1): FOUND
- Commit a74db38 (Task 2): FOUND
- AUDIT-VERIFIED marker at Interpreter.cs:291: FOUND (exactly 1 grep hit in flow-lang/)
- `git diff --stat` Interpreter.cs across both commits: +1 insertion, 0 deletions (criterion 4 satisfied)
- Build: dotnet build flow-lang/flow-lang.csproj → 0 errors, 3 pre-existing unrelated warnings

## Next Phase Readiness

- **Phase 11 Plan 06 (VERIFICATION.md aggregation):** C1 row data ready — Verdict=Confirmed, Evidence=tests/spike/c1-musical-context-body.flow, Next=Phase 12 FIX-07a.
- **Phase 12 FIX-07a:** Test is RED-committed and ready to flip; fix strategy documented above under "Next action."

---
*Phase: 11-audit-spike*
*Plan: 01*
*Completed: 2026-04-18*
