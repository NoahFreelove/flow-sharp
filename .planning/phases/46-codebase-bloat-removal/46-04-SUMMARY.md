---
phase: 46-codebase-bloat-removal
plan: 04
subsystem: testing
tags: [audio, wav-export, test-framework, alias-removal, caller-migration]

# Dependency graph
requires:
  - phase: 46-codebase-bloat-removal (plan 03)
    provides: audio.flow createSineTone internal-decl removal at non-overlapping region; byte-guard test
provides:
  - exportWav legacy reversed-arg alias removed; all 7 callers on path-first writeWav
  - ExportWav/ExportWavWithBitDepth C# shims dropped; WriteWav/WriteWavWithBitDepth/ExportWavInternal core retained
  - test.flow legacy pure-Flow assertion library removed; @test module surface kept
  - tests/test_test_library.flow ported to @test surface with FAIL cases inverted via (assert (not ...))
affects: [future phases touching WAV export or the @test framework]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Reversed-arg alias removal: migrate callers by arg-order swap to the canonical path-first builtin (D-01b strictly-better-equivalent)"
    - "Legacy-assert-to-@test port: throwing @test asserts require FAIL cases inverted as (assert (not ...)) since they throw rather than return Bool"

key-files:
  created: []
  modified:
    - flow-lang/StandardLibrary/Audio/FileIO.cs
    - flow-lang/StandardLibrary/BuiltInFunctions.cs
    - flow-lang/StandardLibrary/BuiltInDocs.cs
    - flow-lang/audio.flow
    - flow-lang/test.flow
    - flow-lang/Runtime/PrngRegistry.cs
    - flow-lang/Runtime/ExecutionContext.cs
    - flow-lang.Tests/FlowScriptData.cs
    - flow-lang.Tests/Helpers/Phase37Fixtures.cs
    - tests/test_full_song.flow
    - tests/demo_feature_showcase.flow
    - tests/test_section_bare_expr.flow
    - tests/test_section_gain_bare_expr.flow
    - tests/test_wav_loading.flow
    - tests/test_writewav.flow
    - tests/test_test_library.flow

key-decisions:
  - "Removed the dangling exportWav BuiltInDocs entry (Rule 2: a :help exportWav would document a non-existent builtin)"
  - "Refreshed stale exportWav boundary doc-comments in PrngRegistry/ExecutionContext/FileIO/Phase37Fixtures so they no longer name a removed code path"
  - "Kept the two FlowScriptData comment mentions of exportWav (historical 'FIXED by 12-05' note + the new removal-documenting comment) — intentional, non-functional residue"

patterns-established:
  - "Arg-order-swap caller migration: buffer-first exportWav → path-first writeWav across 7 sites"
  - "FAIL-case inversion: legacy Bool-returning negative assertions become positive (assert (not ...)) under the throwing @test surface"

requirements-completed: [CLEAN-06, CLEAN-07]

# Metrics
duration: 18min
completed: 2026-05-30
---

# Phase 46 Plan 04: exportWav alias removal + test.flow legacy-half port Summary

**Removed the reversed-arg `exportWav` WAV-export alias (migrating all 7 callers to path-first `writeWav`) and the pre-Phase-35 pure-Flow assertion half of `test.flow` (porting its sole consumer to the throwing `@test` surface with FAIL cases inverted via `(assert (not …))`).**

## Performance

- **Duration:** ~18 min
- **Started:** 2026-05-30
- **Completed:** 2026-05-30
- **Tasks:** 2
- **Files modified:** 16

## Accomplishments

- **D-06:** Deleted `ExportWav`/`ExportWavWithBitDepth` shims in `FileIO.cs` + their two registrations in `BuiltInFunctions.cs` + the two `exportWav` internal proc decls in `audio.flow`. Kept `WriteWav`/`WriteWavWithBitDepth` and the shared private `ExportWavInternal` core (not renamed).
- **D-06:** Migrated all 7 callers to path-first `writeWav` via arg-order swap. Rewrote `test_writewav.flow` (whose purpose was the alias) to test only `writeWav`, and updated the `FlowScriptData.cs` expected-output map in lockstep (dropped the pinned `exportWav`-compat PASS substring).
- **D-06:** Removed the dangling `exportWav` `BuiltInDocs` entry and refreshed stale `exportWav` boundary doc-comments.
- **D-07:** Removed the legacy pure-Flow assertion library from `test.flow` (assertTrue/assertFalse/assertEqual/assertNotEqual/ordering asserts/assertApproxEqual/runTest/summary/notBool/printResult + the `@std`/`@collections` imports). Kept the `@test` module surface (module decl + 6 internal proc decls).
- **D-07:** Ported `tests/test_test_library.flow` to the `@test` surface — every body wrapped in `(test "name" lazy(...))`, all 9 original FAIL cases inverted to positive `(assert (not …))` / `(assert (gt …))` assertions. All 20 ported tests PASS under `flow-cli test`.

## Task Commits

1. **Task 1: Remove exportWav alias + migrate all 7 callers to writeWav (D-06)** — `ba7aaae` (refactor)
2. **Task 2: Remove test.flow legacy assertion half + port test_test_library.flow to @test (D-07)** — `983d6e3` (refactor)

## Files Created/Modified

- `flow-lang/StandardLibrary/Audio/FileIO.cs` — removed ExportWav/ExportWavWithBitDepth shims; ExportWavInternal/WriteWav core retained; refreshed a shared-helper comment
- `flow-lang/StandardLibrary/BuiltInFunctions.cs` — removed the 2 exportWav registrations
- `flow-lang/StandardLibrary/BuiltInDocs.cs` — removed dangling exportWav doc entry
- `flow-lang/audio.flow` — removed the 2 exportWav internal proc decls
- `flow-lang/test.flow` — removed legacy pure-Flow assertion library; kept @test surface
- `flow-lang/Runtime/PrngRegistry.cs`, `flow-lang/Runtime/ExecutionContext.cs` — refreshed stale exportWav boundary doc-comments
- `flow-lang.Tests/FlowScriptData.cs` — updated test_writewav.flow expected-output map
- `flow-lang.Tests/Helpers/Phase37Fixtures.cs` — refreshed a stale `<c>FileIO.ExportWav</c>` doc reference (code already used WriteWav)
- `tests/test_full_song.flow`, `tests/demo_feature_showcase.flow`, `tests/test_section_bare_expr.flow`, `tests/test_section_gain_bare_expr.flow`, `tests/test_wav_loading.flow` — exportWav → writeWav (arg-order swapped)
- `tests/test_writewav.flow` — rewritten to test only path-first writeWav
- `tests/test_test_library.flow` — ported to @test surface (FAIL cases inverted)

## Decisions Made

- Removed the dangling `exportWav` `BuiltInDocs` entry — without the builtin, a `:help exportWav` meta-command would document a non-existent function (Rule 2: missing correctness for the doc surface). All other stale doc-comment mentions of `exportWav` (PrngRegistry / ExecutionContext boundary comments, FileIO shared-helper comment, Phase37Fixtures `<c>` ref) were refreshed for accuracy.
- Left the two `FlowScriptData.cs` comment mentions of `exportWav` intact: line 58 is accurate history ("FIXED by 12-05") and line 120 is the new removal-documenting comment. These are intentional, non-functional residue; the acceptance gate excludes comment residue.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing critical functionality] Removed dangling exportWav BuiltInDocs entry + refreshed stale boundary doc-comments**
- **Found during:** Task 1 (final grep audit)
- **Issue:** The plan enumerated the registration + shim + audio.flow decls but the `BuiltInDocs.cs` entry for `exportWav` and several doc-comments (PrngRegistry, ExecutionContext, FileIO, Phase37Fixtures) still named the removed symbol. The BuiltInDocs entry is live (drives `:help exportWav`).
- **Fix:** Removed the `BuiltInDocs` exportWav entry; refreshed the doc-comments to name only the surviving `renderSong`/`writeWav` boundary / `WriteWav` ref.
- **Files modified:** flow-lang/StandardLibrary/BuiltInDocs.cs, flow-lang/Runtime/PrngRegistry.cs, flow-lang/Runtime/ExecutionContext.cs, flow-lang/StandardLibrary/Audio/FileIO.cs, flow-lang.Tests/Helpers/Phase37Fixtures.cs
- **Verification:** `dotnet build` green; final grep shows only the two intentional FlowScriptData comment mentions.
- **Committed in:** ba7aaae (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (Rule 2 — completing the removal across the doc surface).
**Impact on plan:** Necessary for a consistent removal (no dangling doc entry for a removed builtin). No scope creep — confined to the exportWav surface the plan targeted.

## Issues Encountered

None. The `tests/` directory is gitignored but the touched files are already tracked, so they staged and committed normally (the "paths are ignored" hint applies only to adding new ignored files).

## Verification

- `grep -rn "exportWav\|ExportWav" flow-lang flow-lang.Tests tests examples --include='*.cs' --include='*.flow'` (excluding `bin/` and `ExportWavInternal`) → only 2 intentional comment mentions in FlowScriptData.cs; ZERO live symbols.
- All 7 migrated `.flow` scripts exit 0 with their PASS sentinels via `flow-interpreter`.
- `tests/test_test_library.flow`: registers + prints sentinel via `flow-interpreter`; all 20 ported tests PASS via `flow-cli test` (Total: 20; Passed: 20; Failed: 0).
- `dotnet build` (full solution) green: 0 errors.
- `dotnet test` FlowScript facts: 156/156 PASS (includes rewritten test_writewav.flow + test_test_library.flow).
- Full `dotnet test`: 2197 passed, 9 skipped, 3 failed — all 3 failures are the pre-existing Phase 48 Wasm-bundle tests (WasmDeterminismTests / BundleSizeBudgetTests / WasmBuildPipelineTests) which require a `browser-wasm` restore not configured in this worktree. None touch code modified by this plan.

## Next Phase Readiness

- D-06 and D-07 complete; CLEAN-06 and CLEAN-07 satisfied. WAV export now has a single path-first surface (`writeWav`); the `@test` module is the sole assertion surface.
- No blockers introduced. Wave 2 plan 46-04 ready for the orchestrator's wave-completion roll-up.

## Self-Check: PASSED

- 46-04-SUMMARY.md exists.
- Commits ba7aaae (D-06), 983d6e3 (D-07), 00cb4c8 (docs) present in git log.
- STATE.md / ROADMAP.md not modified (orchestrator owns those writes).

---
*Phase: 46-codebase-bloat-removal*
*Completed: 2026-05-30*
