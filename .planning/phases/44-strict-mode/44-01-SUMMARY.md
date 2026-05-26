---
phase: 44-strict-mode
plan: 01
subsystem: language-core
tags: [pragma, strict-mode, execution-context, module-loader, file-scope, phase-44]

requires:
  - phase: 44-strict-mode/44-00
    provides: "Phase44TestCategory trait + StrictErrorManifestLoader (the Wave 0 test infrastructure consumed by Plan 44-01's Phase44 Facts)"
  - phase: 21-pragmas
    provides: "PragmaRegistry.KnownPragmas closed-set + PragmaScanner D-11 / D-12 paths (the file-scope pragma extraction infrastructure this plan extends)"
  - phase: 32-tuning
    provides: "FlowEngine.ApplyTuningPragma precedent (the parse-then-set posture this plan mirrors for ApplyStrictPragma)"

provides:
  - "`strict` entry in PragmaRegistry.KnownPragmas with D-04 verbatim description"
  - "ExecutionContext.StrictMode auto-property (per-DECLARING-file bit; default false) — read by OverloadResolver in Plan 44-02"
  - "ExecutionContext.CallerStrictMode auto-property (call-dispatch SNAPSHOT; default false) — read by stdlib clamp + advisory leaf sites in Plans 44-05/06/07"
  - "FlowEngine.ApplyStrictPragma method called between parse and interpret"
  - "ModuleLoader inner try/finally save-set-restore of StrictMode around imported file's Execute (D-03 per-DECLARING-file scope)"

affects:
  - "44-02 (OverloadResolver wires CallerStrictMode snapshot push/pop on call dispatch)"
  - "44-05 (TransformFunctions §6a HIGH input-perimeter clamps read CallerStrictMode)"
  - "44-06 (HIGH-priority advisory sites read CallerStrictMode)"
  - "44-07 (MED/LOW advisory sites read CallerStrictMode)"
  - "44-10 (REPL session-flag persistence layered on top of per-Execute StrictMode flip)"

tech-stack:
  added: []
  patterns:
    - "ApplyStrictPragma mirrors ApplyTuningPragma — single-line override of an ExecutionContext bool from program.Pragmas.Has(...) between parse and interpret."
    - "Inner try/finally save-set-restore around interpreter.Execute in ModuleLoader (D-03 + Anti-Pattern 1) — the pattern Plan 44-XX will reuse for any future per-file-scope mutable state on ExecutionContext."

key-files:
  created:
    - "flow-lang.Tests/Integration/Phase44/PragmaRegistryStrictTests.cs"
    - "flow-lang.Tests/Integration/Phase44/ExecutionContextStrictModeTests.cs"
    - "flow-lang.Tests/Integration/Phase44/ModuleLoaderStrictPropagationTests.cs"
  modified:
    - "flow-lang/Lexing/PragmaRegistry.cs"
    - "flow-lang/Runtime/ExecutionContext.cs"
    - "flow-lang/Core/FlowEngine.cs"
    - "flow-lang/Runtime/ModuleLoader.cs"
    - "flow-lang.Tests/Unit/Phase21/PragmaRegistryFacts.cs (Rule 1 auto-fix: alphabetized known-names string pinned to old 6-entry set)"

key-decisions:
  - "ApplyStrictPragma unconditionally overwrites StrictMode (no 'persistence' branch like ApplyTuningPragma) — absent `enable strict;` MUST flip false so a prior REPL session's strict bit cannot leak into a fresh non-strict file. Plan 44-10 layers REPL session persistence on top of this base."
  - "Both StrictMode + CallerStrictMode land in Plan 44-01 (not split across Plans 44-01/44-02) — avoids an extra ExecutionContext edit cycle. CallerStrictMode stays unread until Plan 44-02 wires the call-dispatch snapshot."
  - "Pragma application order in FlowEngine.Execute: tuning first (Phase 32 precedent), strict second. The two pragmas are independent — neither reads the other's state."

patterns-established:
  - "Pattern S1: file-scope pragma save-set-restore — ModuleLoader.LoadModule's inner try/finally around interpreter.Execute(program). Inner finally restores the saved bit regardless of how Execute exited (success / error-via-reporter / thrown exception caught by the outer try). The OUTER try/finally cleans _currentlyLoading; the INNER finally cleans the strict-bit save. Distinct concerns, distinct try/finally pairs."

requirements-completed:
  - REQ-STRICT-01
  - REQ-STRICT-02

duration: 11min
completed: 2026-05-25
---

# Phase 44 Plan 01: Strict Pragma Foundation Summary

**`enable strict;` registered with PragmaRegistry + ExecutionContext.StrictMode/CallerStrictMode fields + per-DECLARING-file save-set-restore at FlowEngine.Execute and ModuleLoader.LoadModule boundaries — the strict bit is now RECOGNIZED and CARRIED, ready for consumer wiring in Plans 44-02 through 44-07.**

## Performance

- **Duration:** 11 min
- **Started:** 2026-05-25T00:30:21Z
- **Completed:** 2026-05-25T00:42:05Z
- **Tasks:** 2 (each TDD: RED test commit + GREEN feat commit)
- **Files modified:** 4 production files + 1 existing test (Rule 1 auto-fix) + 3 new Phase44 test files

## Accomplishments

- **PragmaRegistry entry**: `KnownPragmas["strict"]` registered with D-04 verbatim description string. D-12 unknown-pragma path now suggests `strict` for `stric;` typos via the existing levenshtein closed-set candidate scan — Pitfall 5 mitigated as free behavior with zero PragmaScanner.cs code change.
- **ExecutionContext two new auto-property fields**: `StrictMode` (the per-DECLARING-file bit FlowEngine + ModuleLoader write) + `CallerStrictMode` (the call-dispatch SNAPSHOT field Plan 44-02 will write). Both default `false`. XML docs cite D-02 / D-03 / D-05 read-site routing and reference 44-PATTERNS.md Anti-Pattern 1 (DO NOT confuse the two).
- **FlowEngine.ApplyStrictPragma**: new private method called between `ApplyTuningPragma` and `_interpreter.Execute(program)`. Mirrors `ApplyTuningPragma`'s parse-then-set posture. Unconditional overwrite (not persistence-preserving) so REPL non-strict files cannot inherit a prior strict session's bit.
- **ModuleLoader inner try/finally**: `prevStrict` save + `context.StrictMode = pragmaSet.Has("strict")` set + `finally { context.StrictMode = prevStrict; }` restore around the imported file's Execute + ModuleRegistry-hook block. Restore runs even on Execute throw or registration error — Anti-Pattern 1 regression-pinned by `Fact_StrictFileImportFailure_OuterBitStillRestored`.
- **13 new Phase44 Facts GREEN**: 4 PragmaRegistryStrict (entry exists / D-04 verbatim / levenshtein typo recovery / W6 pragma-position-error) + 4 ExecutionContextStrictMode (both fields default false + settable) + 5 ModuleLoaderStrictPropagation (top-level enable / no-enable / strict imports non-strict / non-strict imports strict / import-failure restore).
- **No regression**: 117 broader pragma + module Facts GREEN; 4 smoke `.flow` scripts (`test_chord_runtime`, `test_chords`, `test_song_structure`, `test_audio_in_pipeline`) execute unchanged. Two-pragma compose case (`enable strict; enable justIntonation;`) verified by ad-hoc smoke (Pitfall 9 mitigated).

## Task Commits

Each task was committed atomically (TDD RED then GREEN):

1. **Task 1 RED: PragmaRegistry + ExecutionContext failing tests** — `7eaf8a1` (test)
2. **Task 1 GREEN: register strict + add StrictMode/CallerStrictMode** — `1a7de14` (feat)
3. **Task 2 RED: ModuleLoader save/restore failing tests** — `9ad5369` (test)
4. **Task 2 GREEN: FlowEngine.ApplyStrictPragma + ModuleLoader try/finally** — `228e048` (feat)

## Files Created/Modified

### Production
- `flow-lang/Lexing/PragmaRegistry.cs` — single-line `["strict"] = "..."` insertion at the end of `KnownPragmas` dict literal. Trailing-comma style switched: previous last entry (`matchExhaustive`) had no trailing comma; new last entry (`strict`) has none either, and `matchExhaustive` gains one. No other PragmaRegistry surface touched (`IsKnown` / `AlphabetizedKnownNames` / `SuggestNearest` are fully data-driven by the dict).
- `flow-lang/Runtime/ExecutionContext.cs` — two auto-property bool fields added adjacent to the `OscEnabled` / `NotationIoEnabled` cluster (line ~301), with a new "Phase 44 — strict mode (D-02 / D-03 / D-05)" section comment. Both fields use the `public bool X { get; set; } = false;` shape mirroring `OscEnabled`. ~33 lines of XML docs documenting field semantics, write sites, read sites, and Anti-Pattern 1 routing.
- `flow-lang/Core/FlowEngine.cs` — one `ApplyStrictPragma(program);` call inserted between `ApplyTuningPragma(program);` and `_interpreter.Execute(program);` in `Execute` (~line 290). New `ApplyStrictPragma` private method added immediately after `ApplyTuningPragma` (~line 322) with XML doc citing D-02 / D-03 and explaining the non-persistence design choice.
- `flow-lang/Runtime/ModuleLoader.cs` — `prevStrict = context.StrictMode;` + `context.StrictMode = pragmaSet.Has("strict");` immediately before `interpreter.Execute(program)` (~line 124); `try { ... } finally { context.StrictMode = prevStrict; }` wrap added around interpreter.Execute + the Phase 43 ModuleRegistry-hook block. Inner finally is distinct from the existing outer `finally { _currentlyLoading.Remove(...) }` — they have separate concerns.

### Tests
- `flow-lang.Tests/Integration/Phase44/PragmaRegistryStrictTests.cs` — 4 Facts pinning D-01 / D-04 / D-12 / W6 / Pitfall 7.
- `flow-lang.Tests/Integration/Phase44/ExecutionContextStrictModeTests.cs` — 4 Facts pinning D-02 / D-05 (both default false + settable).
- `flow-lang.Tests/Integration/Phase44/ModuleLoaderStrictPropagationTests.cs` — 5 Facts pinning D-03 per-DECLARING-file scope + Anti-Pattern 1 try/finally restore on error path. Uses an `internal proc _test_strict_observe ()` declared in the outer file before the `use` (so the proc enters CurrentFrame before inner runs), backed by a `_test_strict_observe` C# builtin registered via `engine.Context.InternalRegistry.Register` that captures `engine.Context.StrictMode` into a closed-over `bool?` at invocation time.
- `flow-lang.Tests/Unit/Phase21/PragmaRegistryFacts.cs` — Rule 1 auto-fix: existing `AlphabetizedKnownNames_ReturnsCsvSorted` test asserted the 6-entry CSV verbatim; growing the closed set is expected per the comment pattern Phase 23 / 31 / 35 already established. Updated to the new 7-entry CSV plus a Phase 44 explanatory comment.

## Decisions Made

- **Unconditional overwrite in ApplyStrictPragma**: `_context.StrictMode = program.Pragmas.Has("strict")` rather than the persistence-preserving form `if (program.Pragmas.Has("strict")) _context.StrictMode = true;`. Rationale: a REPL session that previously evaluated a strict file MUST NOT carry strict-mode into a subsequent non-strict file evaluation. Plan 44-10 will add session-level persistence at a higher layer (REPL command flag) without touching this method.
- **CallerStrictMode lands in Plan 44-01, not Plan 44-02**: The XML doc explicitly documents that the field is unread until Plan 44-02. This avoids a second ExecutionContext.cs edit cycle for what is a trivial field addition. Field name `CallerStrictMode` chosen over the RESEARCH-suggested alternative `StrictModeAtCallSite` for shorter call sites.
- **Tuning-first / strict-second pragma application order**: Both pragmas are independent (neither reads the other's state), so the ordering is purely a code-organization convention.
- **Inner try/finally distinct from outer**: The existing outer `try/catch/finally` in `ModuleLoader.LoadModule` cleans `_currentlyLoading`. The new inner try/finally cleans the strict-bit save. Kept distinct rather than merged because the outer also `_errorReporter.ReportError`s on caught exceptions — merging would entangle two unrelated cleanup concerns.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Test pinning of closed-set string] PragmaRegistryFacts.AlphabetizedKnownNames_ReturnsCsvSorted**
- **Found during:** Task 1 GREEN (running pragma regression test suite after PragmaRegistry edit)
- **Issue:** Phase 21 test (`flow-lang.Tests/Unit/Phase21/PragmaRegistryFacts.cs:42`) asserted the 6-entry alphabetized CSV string verbatim. Adding `strict` grows the closed set; the assertion failed by appending `, strict` to the end. The comment pattern in the file already established that closed-set growth is expected (Phase 23 added `justIntonation`/`pythagorean`/`equalTemperament`; Phase 24 added `scaleLint`; Phase 35 added `matchExhaustive`).
- **Fix:** Updated the assertion to the new 7-entry string + appended a Phase 44 comment noting `strict` joined the closed set per D-01 / D-04.
- **Files modified:** `flow-lang.Tests/Unit/Phase21/PragmaRegistryFacts.cs`
- **Verification:** Test passes; the broader 117-Fact pragma + module suite passes.
- **Committed in:** `1a7de14` (Task 1 GREEN commit, alongside the production change that necessitated the test update).

**2. [Rule 1 - Test corpus drift] tests/test_comprehensive.flow no longer exists**
- **Found during:** Task 2 GREEN smoke verification
- **Issue:** The plan's `<automated>` verification block referenced three smoke `.flow` scripts (`test_chord_runtime.flow`, `test_comprehensive.flow`, `test_audio_in_pipeline.flow`). `test_comprehensive.flow` is no longer in the repo — the `tests/` corpus has evolved between the plan being written and now.
- **Fix:** Substituted three available scripts of similar breadth (`test_chord_runtime.flow`, `test_chords.flow`, `test_song_structure.flow`, `test_audio_in_pipeline.flow`). All four execute cleanly with no regressions.
- **Files modified:** None — substitution was at verification time only.
- **Verification:** All four scripts run without errors and print their `PASS` / "All ... tests passed" sentinels.
- **Committed in:** N/A (no code change).

---

**Total deviations:** 2 auto-fixed (both Rule 1 — keeping existing tests / smoke verification consistent with the actual repo state).
**Impact on plan:** Both auto-fixes preserve the plan's verification intent without scope creep. No production behavior was altered beyond what the plan specified.

## Issues Encountered

- **Initial RED iteration of ModuleLoader tests called the observer builtin from a fresh-context inner file without declaring the proc**: The first authored test files placed `(_test_strict_observe)` inside `inner.flow` as a bare function call. The first test run printed `Function '_test_strict_observe' not found` because the proc was registered in the C# InternalRegistry but never declared at the Flow language level. Fix: outer file declares `internal proc _test_strict_observe ()` BEFORE the `use`, leveraging Flow's "imports execute in caller's context — no new frame" semantics so the proc is in scope when inner runs.

## User Setup Required

None — no external configuration introduced.

## Next Phase Readiness

Plan 44-02 can read `ctx.StrictMode` from `OverloadResolver` and write `ctx.CallerStrictMode` from `Interpreter.ExecuteUserFunctionWithCaptures` with no further plumbing — both fields exist, the file-load boundary already populates `StrictMode`, and the test surface is in place for the wave-1 cluster to extend.

The strict pragma is also visible to composers immediately: a `.flow` file beginning with `enable strict;` will parse cleanly and execute with `engine.Context.StrictMode == true` after Execute. The bit is unread by stdlib leaf sites until Plan 44-05 — so for now the pragma is a no-op as far as runtime behavior, but it is RECOGNIZED (no "unknown pragma" error) and CARRIED to the right place.

## Self-Check: PASSED

- All 7 modified/created production + test files exist on disk.
- All 4 task commits (`7eaf8a1`, `1a7de14`, `9ad5369`, `228e048`) present in `git log --all`.
- PragmaRegistry contains exactly 1 `"strict"` literal; ExecutionContext contains exactly 1 `public bool StrictMode` declaration and 1 `public bool CallerStrictMode` declaration; FlowEngine.cs references `ApplyStrictPragma` 3 times (declaration site + 1 call site + 1 XML doc reference); ModuleLoader.cs references `prevStrict` twice (save + restore).
- Two-pragma compose smoke (`enable strict; enable justIntonation;`) executes cleanly — Pitfall 9 mitigated.

---
*Phase: 44-strict-mode*
*Plan: 01*
*Completed: 2026-05-25*
