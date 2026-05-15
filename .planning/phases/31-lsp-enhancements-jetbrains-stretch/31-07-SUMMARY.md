---
phase: 31-lsp-enhancements-jetbrains-stretch
plan: 07
subsystem: lexer-migration
tags: [lexer, comment-forms, migration-audit, byte-identical-determinism, phase-27, phase-28]

# Dependency graph
requires:
  - phase: 31-lsp-enhancements-jetbrains-stretch
    provides: "Plan 31-03 — `;` / `TODO:` / `FIXME:` column-0 comment arms in SimpleLexer per D-11 Option A"
provides:
  - "Empirical zero-collision audit document recording every committed .flow file parses + executes under Phase 31 lexer"
  - "Recorded ByteIdentical 20/20 GREEN evidence post-lexer-change"
  - "Recorded baseline (62 pre-existing Phase 28 PerSynthArticulation FFT failures) stays stable — zero new regressions"
affects: ["31-08 (JetBrains scaffolding)", "v1.4 release closeout", "v1.5 sample-articulation envelope work (out of scope here)"]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Audit-by-grep + smoke-by-execution: the audit document records both source-text patterns AND empirical end-to-end execution"
    - "Pre-existing baseline preservation: 62 Phase 28 failures held stable as the regression gate (not the absolute pass count)"

key-files:
  created:
    - ".planning/phases/31-lsp-enhancements-jetbrains-stretch/31-MIGRATION-AUDIT.md"
  modified: []

key-decisions:
  - "Classify 4 comment-form hits in vscode-extension/tests/grammar/comment-forms.flow as Upgrades not Regressions: the file is a tmGrammar test fixture intentionally exhibiting each comment form, paired with comment-forms.flow.snap that confirms tmGrammar tokenizes them as comments — the new C# lexer arms agree."
  - "Classify 6 non-zero-exit tests/*.flow files as out-of-scope pre-existing intentional negative-path probes (all pre-date Phase 31 by multiple phases). Not regressions caused by Plan 31-03."
  - "Classify 4 non-zero-exit vscode-extension/tests/grammar/*.flow files as pre-existing tmGrammar fixtures NOT designed for the C# interpreter (C-style proc/{} body syntax). Pre-Phase-17, unrelated to lexer arms."

patterns-established:
  - "Migration audit doc shape: Step 1 grep (each pattern + classification table), Step 2 smoke-by-category counts, Step 3 ByteIdentical regression, Step 4 phase-scoped regression, Step 5 full suite, Migrations performed (with zero-migration option), Conclusion with per-criterion check-list"

requirements-completed: [SPEC-6]

# Metrics
duration: 9m22s
completed: 2026-05-12
---

# Phase 31 Plan 07: SPEC-6 In-Repo Lexer Migration Audit Summary

**Empirical zero-collision result recorded: 126 tracked .flow files audited under the Phase 31 lexer (`;`/`TODO:`/`FIXME:` column-0 comment arms); zero source-text migrations required, ByteIdentical 20/20 GREEN, Phase 17/21/24/31 regression 252/252 GREEN, 62-failure Phase 28 baseline preserved.**

## Performance

- **Duration:** 9m 22s
- **Started:** 2026-05-12T23:42:34Z
- **Completed:** 2026-05-12T23:51:56Z
- **Tasks:** 1
- **Files modified:** 1 (created `31-MIGRATION-AUDIT.md`; no source-text migrations)

## Accomplishments

- Audited every git-tracked `.flow` file in the repo (126 files across `tests/`, `examples/`, `flow-lang/`, `vscode-extension/tests/grammar/`, `flow-lang.Tests/Fixtures/midi/sources/`, `flow-cli/Scaffold/Templates/`)
- Confirmed zero source-text migrations needed under D-11 Option A
- Recorded ByteIdentical 20/20 GREEN — two-run byte-identical determinism preserved
- Recorded Phase 17/21/24/31 regression 252/252 GREEN — LSP-adjacent surface stable
- Verified full unit suite holds the established 62-failure Phase 28 baseline (1098 PASS / 62 FAIL; same count as Plan 31-02 deferred-items.md entry)
- All 5 Phase 27 fixtures (tutorial.flow, showcase.flow, h_alias.flow, microtonal_ji.flow, long_demo.flow) + all 3 Phase 28 ragtime fixtures parse + execute cleanly

## Task Commits

1. **Task 1: SPEC-6 in-repo migration audit complete** — `828ed22` (docs)

No per-task TDD split — this plan is an empirical audit task; the deliverable is the audit document itself.

## Files Created/Modified

- `.planning/phases/31-lsp-enhancements-jetbrains-stretch/31-MIGRATION-AUDIT.md` — Post-lexer-change empirical audit doc (grep audit + 126-file smoke summary + ByteIdentical evidence + acceptance-criteria check-list)

## Decisions Made

See `key-decisions` in frontmatter. Three classification calls were made during the audit:

1. **`vscode-extension/tests/grammar/comment-forms.flow` hits are Upgrades, not Regressions.** The file is a TextMate-grammar test fixture (paired with `comment-forms.flow.snap`) intentionally created in Plan 31-06 to exhibit each comment form. The 4 hits (column-0 `;` × 2, `TODO:`, `FIXME:`) are precisely the patterns the new lexer arms are designed to consume as comments — they're the audit's empirical positive case, not a collision.
2. **6 tests/*.flow non-zero-exit files are out-of-scope pre-existing negative-path probes.** Each exits non-zero because it intentionally exercises an error path (negative tempo, unhashable Dict key, unknown function, iteration-limit guard, etc.). All 6 pre-date Phase 31 by multiple phases (oldest from v1.1, newest from Phase 26-29 chore). Not regressions caused by Plan 31-03's lexer change.
3. **4 vscode-extension/tests/grammar/*.flow non-zero-exit files are pre-existing tmGrammar fixtures.** They use C-style `proc xxx () { ... }` brace syntax which Flow's actual parser doesn't accept (Flow uses indentation, not braces). These were introduced in Phase 17 (commit `302f950`) solely as tmGrammar tokenizer fixtures — they've never been C# interpreter targets. Unrelated to lexer arms.

## Deviations from Plan

None — plan executed exactly as written. The plan explicitly anticipated the "zero migrations needed" empirical result, and that's what was found.

## Issues Encountered

None. The audit confirmed every prediction from Plan 31-01 RESEARCH §Migration Audit:

- Grep audit returned zero collisions outside the intentional `comment-forms.flow` fixture (which the new arms are designed to consume).
- Every tracked `.flow` file that was happy-path-runnable before Plan 31-03 remains happy-path-runnable after.
- ByteIdentical determinism contracts preserved by construction.
- 62-failure Phase 28 baseline (deferred-items.md tracked) held stable — zero new regressions.

## User Setup Required

None — no external service configuration needed for an audit task.

## Next Phase Readiness

- **SPEC-6 closed empirically.** The audit document at `.planning/phases/31-lsp-enhancements-jetbrains-stretch/31-MIGRATION-AUDIT.md` is the receipt downstream phases reference.
- **Ready for Plan 31-08** (JetBrains scaffolding stretch). The lexer change underlying Phase 31's LSP enhancements is now empirically validated as non-disruptive.
- **No blockers.**

## TDD Gate Compliance

This plan is not type=tdd; the type=auto audit task does not require the RED→GREEN→REFACTOR gate sequence. Verification gate (`test -f 31-MIGRATION-AUDIT.md && dotnet test ... ByteIdentical`) passed before commit.

## Self-Check: PASSED

Verified after commit `828ed22`:

- ✓ `.planning/phases/31-lsp-enhancements-jetbrains-stretch/31-MIGRATION-AUDIT.md` exists
- ✓ Commit `828ed22` exists in `git log`
- ✓ Document contains the literal phrase "ZERO source-text migrations performed; D-11 Option A held empirically." (per acceptance criteria)
- ✓ Document contains the literal phrase "Migration audit" (per acceptance criteria)
- ✓ All 5 audit step sections present (grep, smoke, ByteIdentical, phase-regression, full-suite)
- ✓ Conclusion section lists each SPEC-6 criterion with ✓ or ✗
- ✓ ByteIdentical filter exits 0 with all 20 tests passing

---
*Phase: 31-lsp-enhancements-jetbrains-stretch*
*Plan: 07*
*Completed: 2026-05-12*
