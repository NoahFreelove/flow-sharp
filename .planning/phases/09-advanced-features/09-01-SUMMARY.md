---
phase: 09-advanced-features
plan: 01
subsystem: audio
tags: [tempo-ramp, ritardando, accelerando, audio-rendering, bpm-interpolation]

requires:
  - phase: 02-audio-pipeline
    provides: "BarRenderer, SongRenderer, SequenceRenderer, MixVoicesToStereoBuffer"
provides:
  - "tempoRamp built-in function for gradual BPM changes across a sequence"
  - "TempoRampRenderer class with linear BPM interpolation"
affects: [audio, composition, song-rendering]

tech-stack:
  added: []
  patterns: ["bar-midpoint BPM interpolation for smooth tempo ramps"]

key-files:
  created:
    - flow-lang/StandardLibrary/Audio/TempoRampRenderer.cs
    - tests/test_tempo_ramp.flow
  modified:
    - flow-lang/StandardLibrary/BuiltInFunctions.cs
    - flow-lang/audio.flow

key-decisions:
  - "Use bar midpoint for BPM interpolation so single-bar sequences get averaged BPM"

patterns-established:
  - "TempoRampRenderer pattern: per-bar BPM interpolation using midpoint fraction of total beats"

requirements-completed: [AUDIO-08]

duration: 4min
completed: 2026-04-04
---

# Phase 09 Plan 01: Tempo Ramp Summary

**tempoRamp built-in renders sequences with linearly interpolated BPM across bars for smooth ritardando/accelerando**

## Performance

- **Duration:** 4 min
- **Started:** 2026-04-04T02:25:23Z
- **Completed:** 2026-04-04T02:29:12Z
- **Tasks:** 1
- **Files modified:** 4

## Accomplishments
- Implemented `tempoRamp(Sequence, startBPM, endBPM)` and `tempoRamp(Sequence, startBPM, endBPM, instrument)` overloads
- Linear BPM interpolation produces correct frame counts: ritardando yields more frames, accelerando yields fewer
- Frame counts verified between constant-slow and constant-fast bounds
- All 7 test assertions pass

## Task Commits

Each task was committed atomically:

1. **Task 1 (RED): Add failing test for tempoRamp** - `b81392d` (test)
2. **Task 1 (GREEN): Implement tempoRamp built-in** - `52af881` (feat)

## Files Created/Modified
- `flow-lang/StandardLibrary/Audio/TempoRampRenderer.cs` - Core tempo ramp rendering with per-bar BPM interpolation
- `flow-lang/StandardLibrary/BuiltInFunctions.cs` - Registration of TempoRampRenderer
- `flow-lang/audio.flow` - Internal proc declarations for tempoRamp overloads
- `tests/test_tempo_ramp.flow` - 7 test assertions covering ritardando, accelerando, identity, and instrument override

## Decisions Made
- Used bar midpoint for BPM interpolation (offsetBeats + barBeats/2) so that a single-bar sequence gets an averaged BPM between start and end, rather than always using startBPM (which would happen with offset-based interpolation where the first bar is at offset 0)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed test syntax for Flow language comparisons**
- **Found during:** Task 1 (test creation)
- **Issue:** Test initially used `>` and `<` infix operators which Flow does not support; Flow uses prefix function calls `(gt a b)` and `(lt a b)`
- **Fix:** Rewrote all comparison expressions to use `(gt ...)`, `(lt ...)`, `(equals ...)` function call syntax
- **Files modified:** tests/test_tempo_ramp.flow
- **Verification:** All 7 tests pass
- **Committed in:** 52af881 (part of GREEN commit)

**2. [Rule 1 - Bug] Fixed single-bar interpolation producing constant BPM**
- **Found during:** Task 1 (implementation)
- **Issue:** With offset-based interpolation, a single-bar sequence at offset 0 always got t=0, meaning BPM=startBPM with no ramp effect
- **Fix:** Changed to midpoint-based interpolation: t = (offsetBeats + barBeats/2) / totalBeats
- **Files modified:** flow-lang/StandardLibrary/Audio/TempoRampRenderer.cs
- **Verification:** Ritardando 120->80 produces 105,840 frames (between 88,200 at 120 and 132,300 at 80)
- **Committed in:** 52af881 (part of GREEN commit)

---

**Total deviations:** 2 auto-fixed (2 bugs)
**Impact on plan:** Both fixes necessary for correctness. No scope creep.

## Issues Encountered
None beyond the auto-fixed deviations above.

## Known Stubs
None -- all functionality is fully wired and producing correct audio output.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- tempoRamp is available for use in Flow scripts via `use "@audio"`
- Ready for Plan 02 of Phase 09

---
*Phase: 09-advanced-features*
*Completed: 2026-04-04*
