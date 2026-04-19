---
phase: 11-audit-spike
plan: 02
subsystem: interpreter
tags: [audit-spike, interpreter, statement-dispatch, spike, green-test]

verdict: Dismissed
evidence_path: tests/spike/c2-return-value-short-circuit.flow
next_action: Closed. No Phase 12 sub-requirement for C2 -- remove C2 from FIX-07 scope. The `if (_returnValue != null) return;` guard at Interpreter.cs:73-74 is correct early-return semantics; no code change required.

# Dependency graph
requires:
  - phase: 11-audit-spike/11-01
    provides: 11-01 established AUDIT-VERIFIED marker convention, test/spike directory pattern, and the adjacent Interpreter.cs C1 marker at line 292; 11-02 must land its C2 marker at a non-adjacent position (line 75).
provides:
  - Empirical verdict on C2: _returnValue is only written by the ReturnStatement handler; error paths do not set it; the short-circuit guard at Interpreter.cs:73-74 fires only for explicit user `return` (correct semantics)
  - GREEN regression test tests/spike/c2-return-value-short-circuit.flow (committed per D-08 dismissal branch) showing soft-failure error reporting is preserved across an expression-error boundary
  - Inline AUDIT-VERIFIED marker at Interpreter.cs:75 (greppable audit trail; 2nd marker in this file after 11-01's C1 at 292)
affects: [11-06-verification, 12-fix-stability (C2 removed from scope)]

# Tech tracking
tech-stack:
  added: []
  patterns: [AUDIT-VERIFIED inline-comment marker (D-02); tests/spike/ GREEN-commit pattern for dismissal (D-05, D-08); source-trace + empirical-probe dual evidence for audit dismissal]

key-files:
  created:
    - tests/spike/c2-return-value-short-circuit.flow
  modified:
    - flow-lang/Interpreter/Interpreter.cs  # single-line AUDIT-VERIFIED comment at line 75 only

key-decisions:
  - "Marker placed INSIDE the method body (line 75, immediately after the guarded-return at lines 73-74), not after the method's closing brace, because the plan explicitly required adjacency to the `if (_returnValue != null) return;` statement the comment annotates. This differs from 11-01's placement (after the method's closing brace) because 11-01's audit claim was about the method as a whole, whereas C2 is about a single guard statement."
  - "Accepted exit code 1 as pass signal (plan spec said 'exits 0'). Same shipped soft-failure convention documented in 11-01-SUMMARY.md -- any error reported via ErrorReporter yields exit 1 while execution continues. Verdict rests on stdout sentinel presence/absence, unambiguous either way. No plan amendment needed; 11-01 already normalized this."
  - "Committed GREEN (not RED) per D-08 dismissal branch -- the test documents the correct behavior and will NOT be flipped by any future fix. Phase 12 does NOT inherit a C2 sub-requirement."

patterns-established:
  - "Dismissal-path spike convention: GREEN test + inline AUDIT-VERIFIED comment. When a claim is dismissed, the test documents the observed-correct behavior (all sentinels present) and stays GREEN forever. This is the mirror image of 11-01's Confirmed-path RED-commit convention."

requirements-completed: [SPIKE-02]

# Metrics
duration: 10min
completed: 2026-04-18
tasks: 2
commits: 2
---

# Phase 11 Plan 02: SPIKE-02 C2 Verdict Summary

**C2 dismissed: `_returnValue` is never written by any error path. The `if (_returnValue != null) return;` guard at `Interpreter.cs:73-74` is standard early-return implementation -- it fires only after an explicit user `return` statement, never from `ErrorReporter`. The audit claim "every following statement is silently skipped" is a false positive.**

## Performance

- **Duration:** ~10 min (including pre-flight source trace and build verification)
- **Completed:** 2026-04-18
- **Tasks:** 2
- **Files modified:** 2 (1 created, 1 single-line comment insert)

## Verdict

**Dismissed -- Architecture hypothesis confirmed.** The pitfalls-researcher suspicion that "C2 pairs with C1 -- errors may set return-like state" does not hold up to inspection or empirical probing:

### Source trace: every `_returnValue =` site

`grep -n "_returnValue\s*=" flow-lang/Interpreter/Interpreter.cs`:

| Line | Context | Sets to |
| ---- | ------- | ------- |
| 570  | `ExecuteReturn(ReturnStatement ret)` | `_evaluator.Evaluate(ret.Value)` -- legitimate explicit return |
| 664  | `ExecuteUserFunctionWithCaptures` body start | `null` -- reset at proc entry, clears any prior return state |
| 684  | `ExecuteUserFunctionWithCaptures` body end | `null` -- reset at proc exit, releases captured value |

All three writes are inside the user-function / return-statement path. No error-reporting code touches `_returnValue`. `ErrorReporter.ReportError` accumulates errors into a list and returns; it does not enter any code path that reaches lines 570 / 664 / 684.

The *reads* of `_returnValue` at lines 73, 283, 317, 350, 396, 668, 681 are the six short-circuit checks (in `ExecuteStatement`, `ExecuteMusicalContext`, `ExecuteForStatement`, `ExecuteWhileStatement`, `ExecuteSectionDeclaration`, and the two inside `ExecuteUserFunctionWithCaptures`). All six are correct: once a user's `return` fires, subsequent statements inside the enclosing proc/block/loop must be skipped. That is the definition of early return.

### Empirical probe: all sentinels present

Run: `dotnet run --project flow-interpreter tests/spike/c2-return-value-short-circuit.flow`
Exit code: 1 (soft-failure convention -- error reported, execution continues)

**stdout (observed):**
```
Flow Language Interpreter v0.1

c2-probe1-after-call
42
c2-probe2-stmt-A
c2-probe2-stmt-B
c2-probe3-after-error-stmt-A
c2-probe3-after-error-stmt-B
c2-probe4-after-call
100
```

**stderr (observed):**
```
tests/spike/c2-return-value-short-circuit.flow:29:1: error: Function 'nonExistentFn' not found
```

**Sentinel matrix:**

| Sentinel                          | Expected (Dismissed) | Expected (Confirmed) | Actual   | Supports  |
| --------------------------------- | -------------------- | -------------------- | -------- | --------- |
| `c2-probe1-after-call`            | PRESENT              | ABSENT               | PRESENT  | Dismissed |
| `42` (probe 1 return value)       | PRESENT              | either               | PRESENT  | control   |
| `c2-probe2-stmt-A`                | PRESENT              | ABSENT               | PRESENT  | Dismissed |
| `c2-probe2-stmt-B`                | PRESENT              | ABSENT (leak past stmt-A) | PRESENT | Dismissed |
| `c2-probe3-after-error-stmt-A`    | PRESENT              | ABSENT (error path sets _returnValue) | PRESENT | **most diagnostic** |
| `c2-probe3-after-error-stmt-B`    | PRESENT              | ABSENT               | PRESENT  | Dismissed |
| `c2-probe4-after-call`            | PRESENT              | ABSENT (prior error latched return state) | PRESENT | Dismissed |
| `100` (probe 4 return value)      | PRESENT              | ABSENT               | PRESENT  | Dismissed |

All four probes' after-markers appeared. Most diagnostic is probe 3: an error was reported AND the two statements after it still executed -- direct contradiction of the audit claim that "every following statement is silently skipped" after an error.

### Interpretation

The audit author saw `if (_returnValue != null) return;` at the top of `ExecuteStatement` and reasoned "if anything sets `_returnValue`, subsequent statements skip." That reasoning is correct but vacuous -- *nothing* sets `_returnValue` except the `return` statement handler. The short-circuit is the standard way to implement explicit early return in an interpreter with a linear statement-evaluation loop; C-like languages, Lisp evaluators, and JavaScript engines all do variants of this. It is not a bug.

C2 pairs cleanly with the C1 verdict from 11-01: C1 showed that validation errors inside `ExecuteMusicalContext` exit that method early (which IS a real body-skip bug, because `foreach (var stmt in ctx.Body)` never runs) but those early exits use plain `return;` -- they do NOT touch `_returnValue`. The C1 fix (replace `return;` with `break;` in the validation branches) has no interaction with C2's guard.

## Inline marker

- **File:** `flow-lang/Interpreter/Interpreter.cs`
- **Line:** 75 (immediately after `return; // Already returned` at line 74; before the `switch (stmt)` at line 77)
- **Comment:** `// AUDIT-VERIFIED 2026-04-18: C2 — Dismissed: _returnValue only set by ReturnStatement; guard is correct (tests/spike/c2-return-value-short-circuit.flow)`
- **Greppability:** `grep -rn "AUDIT-VERIFIED 2026-04-18: C2" flow-lang/` returns exactly one hit at `flow-lang/Interpreter/Interpreter.cs:75`.
- **Marker coexistence:** Interpreter.cs now carries two AUDIT-VERIFIED markers -- C2 at line 75 (this plan) and C1 at line 292 (11-01). They are 217 lines apart -- non-adjacent as required. `grep -c "AUDIT-VERIFIED 2026-04-18" flow-lang/Interpreter/Interpreter.cs` returns 2.
- **Diff stat:** `+1 insertion(+), 0 deletions` on Interpreter.cs -- respects ROADMAP success criterion 4 (no production logic changes in Phase 11).

## Next action

**Closed. No Phase 12 sub-requirement.**

- REQUIREMENTS.md edit in Phase 11 Plan 06 (verification aggregation) should NOT produce a `FIX-07b` for C2 -- the claim is dismissed with positive evidence and inline audit trail.
- The GREEN test `tests/spike/c2-return-value-short-circuit.flow` stays in the repo as a regression guard: if a future change accidentally makes any error path write to `_returnValue`, this test will flip RED (probe 3's after-markers would disappear).
- The `Recommended Next Phase` table in `CODEBASE-AUDIT-2026-04-18.md:143` says `"C1, C2 — fix musical-context frame leak and statement-skip-after-error in Interpreter.cs"` -- Phase 11 Plan 06 should update that bullet to remove C2 (the statement-skip-after-error claim is unfounded; C1 stands).

## Task Commits

1. **Task 1: Author tests/spike/c2-return-value-short-circuit.flow** -- `b01359f` (test)
2. **Task 2: Record AUDIT-VERIFIED comment in Interpreter.cs** -- `645969e` (docs)

## Files Created/Modified

- `tests/spike/c2-return-value-short-circuit.flow` -- 4-probe empirical test for C2; committed GREEN per D-08 dismissal branch; force-added via `git add -f` because `tests/` and `*.flow` are in `.gitignore` (matches 11-01's handling).
- `flow-lang/Interpreter/Interpreter.cs` -- single-line AUDIT-VERIFIED comment inserted at line 75 (between the guard's `return; // Already returned` at line 74 and the `switch (stmt)` at line 77).

## Decisions Made

- **Placed marker inside the method body adjacent to the guard, not after the method's closing brace.** 11-01 put its C1 marker after the method's `}` because C1 is about the method as a whole. C2 is about a single two-line guard; the plan explicitly required adjacency ("directly INSIDE the method body, above `switch (stmt)`"). Placing it at line 75 satisfies the "between Interpreter.cs:73 and Interpreter.cs:78" acceptance criterion in the plan.
- **Accepted exit code 1 as pass signal** (plan said "exits 0"). Identical soft-failure convention 11-01 already normalized; verdict rests on stdout sentinels.
- **Committed GREEN** per D-08 dismissal branch rather than RED. The test documents observed-correct behavior; there is no Phase 12 fix that would flip it.
- **Did NOT rewrite the test to avoid the `nonExistentFn` error.** Probe 3 is the single most diagnostic probe -- it is the *only* probe that would reveal the alleged bug if C2 were real. Removing it would mean the test cannot refute the claim it is authored to refute.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 -- Blocking] Corrected proc-body syntax in test file from plan spec**

- **Found during:** Task 1 verification run
- **Issue:** The plan's mandated test content used curly-brace proc bodies (`proc c2Probe1() { return 42 }`) but Flow's parser requires `end proc` terminators -- see `flow-lang/Parsing/Parser.cs:255` (`Expect(TokenType.EndProc, "Expected 'end' after procedure body");`). The plan's form halts parsing with `Unexpected token LBrace` before any probe runs, yielding zero verdict evidence.
- **Fix:** Rewrote each proc using Flow's actual form:
  ```
  proc c2Probe1 ()
      return 42
  end proc
  ```
  This preserves the probe's semantic purpose (0-arg proc with explicit return) while satisfying the parser.
- **Files modified:** tests/spike/c2-return-value-short-circuit.flow
- **Verification:** Post-fix run yields all 9 sentinel prints on stdout plus the one expected `Function 'nonExistentFn' not found` error on stderr.
- **Committed in:** `b01359f` (Task 1 commit)

### Analogous to 11-01

Mirror of 11-01's probe-3 identifier-vs-string-literal fix. Both plans were authored with surface-syntax details that diverged from the actual shipped parser; both corrections are semantic-preserving (Rule 3 blocking fixes).

---

**Total deviations:** 1 auto-fixed (Rule 3 -- blocking parser grammar mismatch in plan spec)
**Impact on plan:** No scope creep. Single-line semantic-preserving correction in the test file; ROADMAP criterion 4 (no production source changes beyond the one AUDIT-VERIFIED comment) is untouched.

## Issues Encountered

- **Exit code spec mismatch** (noted above, same as 11-01): plan says "exits 0" but shipped soft-failure convention yields exit 1 on any reported error. Verdict evidence is stdout-based and unambiguous; cosmetic only.
- **Plan text inconsistency on AUDIT-VERIFIED anchor line.** Plan says both "between Interpreter.cs:73 and Interpreter.cs:78" AND "adjacent to line 73-74" AND "above `switch (stmt)` (line 76)" -- all three are satisfied by line 75. No action needed.

## TDD Gate Compliance

Not applicable -- plan `type: execute`, not `type: tdd`. D-08's dismissal branch (GREEN test + inline AUDIT-VERIFIED comment) is satisfied:
- `b01359f` test(11-02): GREEN spike -- observed behavior matches the Dismissed hypothesis; no fix will flip it.
- `645969e` docs(11-02): AUDIT-VERIFIED marker -- greppable audit trail.

## Self-Check: PASSED

- `tests/spike/c2-return-value-short-circuit.flow` exists on disk: FOUND
- Commit `b01359f` (Task 1 test): FOUND in git
- Commit `645969e` (Task 2 marker): FOUND in git
- AUDIT-VERIFIED C2 marker at Interpreter.cs:75: FOUND (exactly 1 grep hit for `AUDIT-VERIFIED 2026-04-18: C2` in flow-lang/)
- AUDIT-VERIFIED total count in Interpreter.cs: 2 (C1@292 + C2@75), non-adjacent (217 lines apart)
- `git diff --stat flow-lang/Interpreter/Interpreter.cs` for commit `645969e`: +1 insertion, 0 deletions (criterion 4 satisfied)
- Build: `dotnet build` -> 0 errors, 5 pre-existing unrelated warnings
- No modifications to STATE.md / ROADMAP.md / REQUIREMENTS.md (honored per orchestrator directive)

## Next Phase Readiness

- **Phase 11 Plan 06 (VERIFICATION.md aggregation):** C2 row data ready -- Verdict=Dismissed, Evidence=tests/spike/c2-return-value-short-circuit.flow, Next=Closed. C2 removed from FIX-07 scope in REQUIREMENTS.md edit.
- **Phase 12 (FIX-07):** C2 does NOT produce a sub-requirement. FIX-07a (C1) is still active; C2 contributes no work to Phase 12.
- **CODEBASE-AUDIT-2026-04-18.md §6 bullet correction:** Phase 11 Plan 06 should clarify that C1's fix target is the musical-context-body skip only; C2's "statement-skip-after-error" framing is unfounded.

---
*Phase: 11-audit-spike*
*Plan: 02*
*Completed: 2026-04-18*
