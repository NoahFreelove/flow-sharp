---
phase: 46-codebase-bloat-removal
plan: 02
subsystem: infra
tags: [dead-code-removal, render-path, timelinemap, song-renderer, refactor]

# Dependency graph
requires:
  - phase: 28-voice-allocation-articulation
    provides: SongRenderer / BarRenderer / SequenceRenderer primary render path (untouched here)
provides:
  - "TimelineMap editor-highlighting stack fully removed (~237 LOC) — TimelineMap.cs deleted + 4 *WithTimeline / TimelineMap-typed renderer overloads removed"
  - "Primary (non-TimelineMap) Song/Section render path byte-identical — BarType.ToTimeline() / SequenceType.ToTimeline() preserved"
affects: [46-codebase-bloat-removal subsequent plans, future LSP live-highlighting work]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Dead-code removal by typed-signature re-location (work bottom-up, never trust stale line numbers)"

key-files:
  created: []
  modified:
    - flow-lang/Audio/TimelineMap.cs (DELETED)
    - flow-lang/StandardLibrary/Audio/SongRenderer.cs
    - flow-lang/StandardLibrary/Audio/BarRenderer.cs
    - flow-lang/StandardLibrary/Audio/SequenceRenderer.cs

key-decisions:
  - "ToTimeline() on BarType/SequenceType is the PRIMARY render path's beat-offset projection (Track/Timeline DAW layer, D-10 KEEP) — name-overlap with TimelineMap is coincidental; left untouched."

patterns-established:
  - "When deleting members whose line numbers shift, re-locate by typed signature (TimelineMap parameter / WithTimeline name), not raw line number."

requirements-completed: [CLEAN-02]

# Metrics
duration: 8min
completed: 2026-05-30
---

# Phase 46 Plan 02: Remove TimelineMap Editor-Highlighting Stack Summary

**Deleted the ~237-LOC dead TimelineMap editor-highlighting render plumbing (TimelineMap.cs + the four parallel `*WithTimeline` / TimelineMap-typed renderer overloads) that was scaffolded for an LSP live-highlighting feature never wired — primary Song/Section render path is byte-identical and `ToTimeline()` is preserved.**

## Performance

- **Duration:** 8 min
- **Started:** 2026-05-30T15:31Z
- **Completed:** 2026-05-30T15:39Z
- **Tasks:** 1
- **Files modified:** 4 (1 deleted, 3 edited)

## Accomplishments
- Deleted `flow-lang/Audio/TimelineMap.cs` (`TimelineEntry` record + `TimelineMap` class, 67 LOC).
- Removed `RenderSongWithTimeline` (public) + `RenderSectionWithTimeline` (private) from `SongRenderer.cs` including their `/// <summary>` blocks (~106 LOC).
- Removed the four TimelineMap-threading overloads from `BarRenderer.cs` (`RenderBarToVoices(...,TimelineMap,...)` string+synth pair + `RenderBarAtBeat(...,TimelineMap,...)` string+synth pair, ~85 LOC).
- Removed the two TimelineMap-aware `RenderSequenceToVoices(...,TimelineMap,...)` overloads from `SequenceRenderer.cs` (~43 LOC).
- `grep -rn "TimelineMap\|TimelineEntry\|RenderSongWithTimeline\|RenderSectionWithTimeline"` across flow-lang/flow-interpreter/flow-cli/flow-lsp/flow-lang.Tests/tests returns **ZERO**.
- `BarType.ToTimeline()` (BarType.cs:182) and `SequenceType.ToTimeline()` (SequenceType.cs:46) both retained — verified present.

## Task Commits

Each task was committed atomically:

1. **Task 1: Delete TimelineMap.cs and all parallel *WithTimeline / TimelineMap-typed renderer overloads** - `refactor(46): remove TimelineMap editor-highlighting stack (D-02)`

## Files Created/Modified
- `flow-lang/Audio/TimelineMap.cs` - DELETED (TimelineEntry record + TimelineMap accumulator class).
- `flow-lang/StandardLibrary/Audio/SongRenderer.cs` - Removed RenderSongWithTimeline + RenderSectionWithTimeline (+ summaries); primary RenderSong path untouched.
- `flow-lang/StandardLibrary/Audio/BarRenderer.cs` - Removed 4 TimelineMap-threading overloads; non-timeline RenderBarToVoices / RenderBarAtBeat retained.
- `flow-lang/StandardLibrary/Audio/SequenceRenderer.cs` - Removed 2 TimelineMap-aware RenderSequenceToVoices overloads; non-timeline overloads retained.

## Decisions Made
- None beyond plan — followed the D-02 KEEP/Pitfall-4 guidance exactly: `ToTimeline()` left untouched (Track/Timeline DAW layer, distinct feature despite the name overlap). Re-located members by typed signature rather than raw line number since deletions shift lines.

## Deviations from Plan

None - plan executed exactly as written. No Rule 1/2/3/4 deviations. The plan referenced `46-RESEARCH.md` for exact line ranges; that file does not exist in this worktree, so ranges were derived directly from the current tree via grep + Read (the plan body itself supplied the same ranges and the KEEP warning, so no information was lost).

## Issues Encountered

- **Two whole-suite `dotnet test` failures, both pre-existing and out-of-scope:** `WasmDeterminismTests.SameSource_TwoRuns_IdenticalStdout` + `...IdenticalRunResultJson` (Phase 48). Both **PASS in isolation** (`--filter FullyQualifiedName~Phase48.WasmDeterminismTests` → 2/2 PASS); they fail only under the whole-suite run due to a Phase-48 test-isolation issue (a prior test's `Console.Out` redirection leaking into `WasmEntry`'s static shared-engine stdout capture). Documented as a known transient in `45-06-SUMMARY.md`. Zero reference to TimelineMap / SongRenderer / BarRenderer / SequenceRenderer — entirely independent of this plan's dead-code removal. Logged to `.planning/phases/46-codebase-bloat-removal/deferred-items.md`; NOT fixed per the SCOPE BOUNDARY rule.

## Verification
- `dotnet build flow-lang/flow-lang.csproj` → 0 Errors (8 pre-existing warnings).
- `dotnet build` (full solution) → 0 Errors.
- `dotnet test` → 2188 passed / 2 failed / 9 skipped; the 2 failures are the documented pre-existing WasmDeterminism transient (pass in isolation), not regressions.
- Primary render path smoke: `tests/test_song_structure.flow` → "All song structure tests passed!".
- `grep` for TimelineMap/TimelineEntry/RenderSongWithTimeline/RenderSectionWithTimeline → ZERO matches repo-wide.
- `ToTimeline()` present on both BarType and SequenceType.

## Next Phase Readiness
- TimelineMap stack removed cleanly; no behavior change to the audio pipeline. Subsequent Phase 46 bloat-removal plans unaffected.

## Self-Check: PASSED

- FOUND: `.planning/phases/46-codebase-bloat-removal/46-02-SUMMARY.md`
- DELETED: `flow-lang/Audio/TimelineMap.cs`
- FOUND commit `b5fe0ba` (remove TimelineMap editor-highlighting stack)
- GREP CLEAN: zero matches for TimelineMap/TimelineEntry/RenderSongWithTimeline/RenderSectionWithTimeline across flow-lang/flow-interpreter/flow-cli/flow-lsp/flow-lang.Tests/tests
- ToTimeline() retained on BarType.cs + SequenceType.cs

---
*Phase: 46-codebase-bloat-removal*
*Completed: 2026-05-30*
