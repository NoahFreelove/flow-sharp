---
phase: 46-codebase-bloat-removal
plan: 03
subsystem: audio
tags: [refactor, dead-code, indirection-removal, audio, flow-stdlib, clampsamples]

requires:
  - phase: 46-codebase-bloat-removal
    provides: D-01 indirection-removal mandate; audit of dead internal decls + thin-wrapper shims
provides:
  - "audio.flow minus the 2 dead internal createSineTone forward-decls (stereo proc wrappers intact)"
  - "PulseAudioSimpleBackend.cs + PlaybackFunctions.cs: 2 ClampSamples shims removed, 3 callsites inlined to AudioUtils.ClampSamples"
affects: [46-04, audio.flow editors, audio playback backends]

tech-stack:
  added: []
  patterns:
    - "Same-namespace static-helper calls written directly (no per-class thin-wrapper shim) — aligns to CoreAudioBackend.cs:149 precedent"

key-files:
  created:
    - .planning/phases/46-codebase-bloat-removal/46-03-SUMMARY.md
  modified:
    - flow-lang/audio.flow
    - flow-lang/Audio/PulseAudioSimpleBackend.cs
    - flow-lang/StandardLibrary/Audio/PlaybackFunctions.cs

key-decisions:
  - "Audit's '16 dead createSineTone decls' was wrong — exactly 2 existed (audio.flow:224,227); Saw/Square/Triangle had none. Removed only those 2."
  - "Left the C# SignalGeneration.CreateSineTone mono builtin registered (BuiltInFunctions.cs) — D-05 scope is internal Flow proc decls ONLY; removing the now-undeclared builtin would widen the diff for no behavior gain."
  - "8 full-suite test failures (OSC loopback, WASM publish/determinism, Phase35 CLI subprocess) are pre-existing environment/toolchain failures, not regressions — none reference touched code; FailingTestExitsNonZero reproduced identically against base D-08 files."

patterns-established:
  - "Indirection removal (D-01): delete dead forward-decls + private thin-wrapper shims, route callers to the shared helper directly."

requirements-completed: [CLEAN-05, CLEAN-08]

duration: 12min
completed: 2026-05-30
---

# Phase 46 Plan 03: Dead Decl + ClampSamples Shim Removal Summary

**Removed the 2 dead internal `createSineTone` forward-decls in `audio.flow` and inlined the 2 `ClampSamples` thin-wrapper shims (3 callsites) to direct `AudioUtils.ClampSamples()` — pure indirection removal, zero behavior change.**

## Performance

- **Duration:** ~12 min
- **Started:** 2026-05-30T15:29:00Z
- **Completed:** 2026-05-30T15:41:25Z
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments
- **D-05 (CLEAN-05):** Deleted the 2 dead `internal proc createSineTone` forward-decls (audio.flow:224,227) plus their orphaned lead-in comment. The composer-facing stereo `proc createSineTone` wrappers (~352/365) and `internal proc noteToFrequency` are untouched and still resolve — confirmed by `test_mix.flow` + `test_writewav.flow` passing.
- **D-08 (CLEAN-08):** Removed the 2 private `ClampSamples` shims (PulseAudioSimpleBackend.cs, PlaybackFunctions.cs) and rewrote all 3 callsites to call `AudioUtils.ClampSamples()` directly — the same helper, so no behavior or byte change. Aligns these two backends to the existing `CoreAudioBackend.cs:149` precedent (untouched).
- `dotnet build flow-lang/flow-lang.csproj` green after each change.

## Task Commits

Each task was committed atomically:

1. **Task 1: Remove dead internal createSineTone decls (D-05)** - `4dd8a3d` (refactor)
2. **Task 2: Inline ClampSamples shims to AudioUtils (D-08)** - `1c31267` (refactor)

## Files Created/Modified
- `flow-lang/audio.flow` - Removed 2 dead internal createSineTone forward-decls + orphaned comment
- `flow-lang/Audio/PulseAudioSimpleBackend.cs` - Removed private ClampSamples shim; callsite → AudioUtils.ClampSamples
- `flow-lang/StandardLibrary/Audio/PlaybackFunctions.cs` - Removed private ClampSamples shim; 2 callsites → AudioUtils.ClampSamples
- `.planning/phases/46-codebase-bloat-removal/deferred-items.md` - Logged pre-existing environment test failures (out of scope)

## Decisions Made
- Removed exactly 2 internal decls (audit's "16" was wrong; verified by grep — only Sine had internal forward-decls).
- Left the C# `CreateSineTone` mono builtin registered — out of D-05 scope; removing it widens the diff with no behavior benefit.
- Edits to `audio.flow` confined to the D-05 lines (lines ~222-228 region) so plan 46-04 (Wave 2, non-overlapping audio.flow region) applies cleanly.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
- Full `dotnet test` reported 8 failures / 2182 passed / 9 skipped. Investigated: all 8 are in environment/toolchain-dependent families (OSC UDP loopback, WASM publish workload + determinism, Phase35 CLI subprocess spawning). None reference `createSineTone`, `ClampSamples`, `PulseAudioSimpleBackend`, or `PlaybackFunctions`. Reverted the D-08 edits and re-ran `FailingTestExitsNonZero` against base files — it failed identically, confirming pre-existing. Logged to `deferred-items.md`. Restored D-08 edits and re-verified build green. No regression attributable to this plan.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Plan 46-04 (Wave 2) can proceed; its `audio.flow` edit region does not overlap the D-05 lines removed here.
- D-01 indirection-removal pattern (delete dead decls + inline thin-wrapper shims) established for remaining phase 46 cleanups.

## Self-Check: PASSED

- FOUND: flow-lang/audio.flow (`internal proc createSineTone` count = 0; `proc createSineTone` wrappers = 2; `noteToFrequency` intact)
- FOUND: flow-lang/Audio/PulseAudioSimpleBackend.cs (no shim; `AudioUtils.ClampSamples` at :97)
- FOUND: flow-lang/StandardLibrary/Audio/PlaybackFunctions.cs (no shim; `AudioUtils.ClampSamples` at :189, :243)
- FOUND commit: 4dd8a3d (D-05)
- FOUND commit: 1c31267 (D-08)
- dotnet build flow-lang green (0 errors)

---
*Phase: 46-codebase-bloat-removal*
*Completed: 2026-05-30*
