---
phase: 24-scale-linting-flow-lsp
plan: 01
subsystem: language-pragma
tags: [pragma, registry, closed-set, scaleLint, lint-02, phase-24, wave-1]

# Dependency graph
requires:
  - phase: 21-pragma-infrastructure
    provides: PragmaRegistry closed-set scaffolding, hAsB pragma, IsKnown/AlphabetizedKnownNames/SuggestNearest API
  - phase: 23-microtonal-tuning-wedge
    provides: closed-set growth pattern (3 entries), Phase 23 PragmaTuningFacts lower-bound (>= 4) precedent, justIntonation sentinel migration template
provides:
  - scaleLint registered as recognized pragma (5th entry in KnownPragmas)
  - LINT-02 closed-set foundation — `enable scaleLint;` parses without D-12 unknown-pragma error
  - Phase 24 closed-set growth Facts (4 in flow-lang.Tests/Unit/Phase24/PragmaRegistryScaleLintFacts.cs)
  - Phase 21 PragmaRegistryFacts migration (sentinel "futureUnknownPragma" + CSV updated for 5 entries)
affects: [24-02-DiatonicSpellings, 24-03-ScaleLintAnalyzer, 24-04, 24-05, future-pragma-additions]

# Tech tracking
tech-stack:
  added: []  # Zero new dependencies — single one-line entry in existing dictionary literal
  patterns:
    - "Phase 24 D-04 'zero flow-lang touch' goal at maximally-conservative interpretation: ONE production line in flow-lang/"
    - "Closed-set growth pattern: lower-bound test (>= N) intentionally upper-unconstrained (WARNING-3)"
    - "Sentinel-replacement migration for negative-assertion Facts when closed-set grows (mirrors Phase 23 justIntonation migration)"

key-files:
  created:
    - flow-lang.Tests/Unit/Phase24/PragmaRegistryScaleLintFacts.cs
  modified:
    - flow-lang/Lexing/PragmaRegistry.cs
    - flow-lang.Tests/Unit/Phase21/PragmaRegistryFacts.cs

key-decisions:
  - "scaleLint description text fixed verbatim per plan: 'Inside `key { ... }` blocks, surface non-diatonic notes as Information-severity LSP diagnostics.' — informational only (per PragmaRegistry doc comment lines 11-15); not surfaced in errors."
  - "Phase 21 sentinel chosen as 'futureUnknownPragma' (plan-prescribed) — explicitly forward-looking name signals 'this Fact pins the unknown-pragma path even as the closed set grows'."
  - "TDD ordering preserved: RED test commit → GREEN registry commit → migration commit. The plan's 3-step Action allowed this; the deliberate 3-commit split documents the gate sequence in git history."

patterns-established:
  - "Phase 24 test directory: flow-lang.Tests/Unit/Phase24/ — created in this plan; subsequent Phase 24 plans (24-03, 24-04, 24-05) will populate."
  - "Phase 24 Facts namespace: FlowLang.Tests.Unit.Phase24 — mirrors Phase 21/23 convention."

requirements-completed: [LINT-02]

# Metrics
duration: ~3min (executor commits) + build/test time
completed: 2026-05-04
---

# Phase 24 Plan 01: Pragma Registry scaleLint Entry Summary

**Single-line scaleLint registration in PragmaRegistry.KnownPragmas — the only flow-lang touch in Phase 24 (D-04 / ROADMAP "zero flow-lang touch" goal at maximally-conservative interpretation), unblocking `enable scaleLint;` as a parse-clean opt-in for the LSP-side LINT-02 analyzer.**

## Performance

- **Duration:** ~3 min (executor wall-clock, RED → GREEN → migration)
- **Started:** 2026-05-04T17:15:00Z
- **Completed:** 2026-05-04T17:18:32Z
- **Tasks:** 1 (TDD-decomposed into 3 commits)
- **Files modified:** 3 (1 production, 2 test)

## Accomplishments

- `scaleLint` registered as the 5th entry in `PragmaRegistry.KnownPragmas` (D-04 single-line touch)
- 4 new Facts in Phase 24 closed-set test file pin: membership, lower-bound count (>= 5), CSV inclusion, regression of all prior entries
- Phase 21 PragmaRegistryFacts migrated: sentinel `futureUnknownPragma` replaces `scaleLint` in the negative assertion; CSV expectation extended with `scaleLint` in correct ordinal-sort position (e < h < j < p < s)
- `dotnet test` full suite: 609/609 passed, 0 failed
- `IsKnown` / `AlphabetizedKnownNames` / `SuggestNearest` methods unchanged — they pick up the new entry at runtime via the dictionary read

## Task Commits

Each step committed atomically (TDD gate sequence):

1. **Task 1 (RED gate):** test(24-01): add failing scaleLint pragma registry Facts (RED) — `1270c05` (3 of 4 Facts intentionally fail at this commit)
2. **Task 1 (GREEN gate):** feat(24-01): register scaleLint in PragmaRegistry.KnownPragmas (GREEN) — `354a4de` (the single one-line flow-lang touch)
3. **Task 1 (Phase 21 migration):** test(24-01): migrate Phase 21 PragmaRegistryFacts for closed-set growth — `52a3dff` (Pitfall 2 sentinel + CSV)

**Plan metadata:** (orchestrator owns final docs commit per parallel-worktree protocol — STATE.md / ROADMAP.md NOT touched here)

## Files Created/Modified

- `flow-lang/Lexing/PragmaRegistry.cs` — Added `["scaleLint"] = "Inside \`key { ... }\` blocks, surface non-diatonic notes as Information-severity LSP diagnostics."` as 5th dictionary literal entry (line 23). Trailing-comma added to prior `equalTemperament` entry. NO method bodies changed.
- `flow-lang.Tests/Unit/Phase24/PragmaRegistryScaleLintFacts.cs` — NEW. 4 Facts: `IsKnown_ScaleLint_ReturnsTrue`, `KnownPragmas_HasAtLeastFiveEntries`, `AlphabetizedKnownNames_IncludesScaleLint`, `IsKnown_PriorEntries_StillRegistered`. Doc comment cross-references D-04 and D-19.
- `flow-lang.Tests/Unit/Phase21/PragmaRegistryFacts.cs` — Sentinel migration (line 29: `futureUnknownPragma` replaces `scaleLint`) + CSV expectation updated to include `scaleLint` (line 39). Doc comments at lines 25-28 and 36-37 updated to reflect Phase 24 closed-set growth. The other 3 Facts (`IsKnown_HAsB_ReturnsTrue`, `SuggestNearest_FindsClose_HAsBForHasb`, `SuggestNearest_ReturnsNullForFarAway`) untouched per plan.

## Decisions Made

None - followed plan as specified. Description text, sentinel name, test-method names, doc comments, and ordinal-sort position all match the plan's verbatim "Final shape" code blocks.

## Deviations from Plan

None functional. The plan executed exactly as written.

### Documentation note (acceptance criterion grep counts)

Plan acceptance criteria at lines 254-255 specify:
- `grep -c 'futureUnknownPragma' flow-lang.Tests/Unit/Phase21/PragmaRegistryFacts.cs returns 1`
- `grep -c 'scaleLint' flow-lang.Tests/Unit/Phase21/PragmaRegistryFacts.cs returns 1`

Actual counts after migration:
- `futureUnknownPragma` = 2 (one in updated doc comment + one in assertion)
- `scaleLint` = 4 (file header doc on line 8 was pre-existing + new comment on lines 25-26 + CSV expectation on line 39)

Substance is correct: the plan's own "Final shape" code blocks (lines 218-227, 232-240) explicitly include those words in the doc comments, so a grep count of 1 was never achievable from the plan as written. The functional invariants — sentinel exists, `IsKnown("scaleLint")` removed, CSV correctly extended, all 5 Facts pass — are met. Treating this as a grep-precision artifact in the plan, not a deviation in implementation. (Surfacing here for transparency per Rule 2 documentation discipline.)

## Issues Encountered

- Initial worktree HEAD was on the project root commit (5b3687c) instead of expected merge-base (a5bab72). Resolved by `git reset --hard a5bab72` per the `<worktree_branch_check>` protocol. No data loss — worktree was empty.
- `dotnet run --project flow-interpreter -e 'enable scaleLint; Int x = 5;'` printed only the banner (no D-12 error), which is the expected positive case. Sanity check with a deliberately-unknown name (`scaleLintBogusXYZ`) also produced no output in `-e` mode — appears to be a separate `flow-interpreter -e` quirk with parse-error suppression, orthogonal to this plan. Unit Facts cover the membership invariants directly.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- **24-02 (DiatonicSpellings)**: Already declared parallel-safe (different file set). No coupling.
- **24-03 (ScaleLintAnalyzer)**: Now unblocked — analyzer activation gate `Ast.Pragmas.Has("scaleLint")` (D-19) can fire because the pragma is registered.
- **24-04 / 24-05**: Inherit registry presence transitively.

## Self-Check: PASSED

- `flow-lang/Lexing/PragmaRegistry.cs` — exists, contains `["scaleLint"]` (1 occurrence)
- `flow-lang.Tests/Unit/Phase24/PragmaRegistryScaleLintFacts.cs` — exists, contains 4 `[Fact]` attributes
- `flow-lang.Tests/Unit/Phase21/PragmaRegistryFacts.cs` — modified, 0 occurrences of `IsKnown("scaleLint")`, contains `futureUnknownPragma` sentinel
- Commits in `git log`: `1270c05` (RED), `354a4de` (GREEN), `52a3dff` (migration) — all on branch `worktree-agent-a1c8c7ddc6cd2c52b`
- `dotnet test --nologo`: 609 passed, 0 failed (full suite, including all PragmaRegistry + PragmaTuning Facts)
- Build: 0 errors

## TDD Gate Compliance

- RED commit (`1270c05`): test(24-01) — 3 of 4 Facts failed as expected at this commit
- GREEN commit (`354a4de`): feat(24-01) — registry change makes all 4 Facts pass
- REFACTOR: not applicable (single-line dictionary entry has no shape to clean up)
- Migration commit (`52a3dff`): test(24-01) — preserves Phase 21 invariants, completes the closed-set growth contract

Gate sequence verified in git log; all three commits present in linear order.

---
*Phase: 24-scale-linting-flow-lsp, Plan: 01*
*Completed: 2026-05-04*
