---
phase: 02-audio-pipeline
plan: 03
subsystem: audio
tags: [polyphony, voice-allocation, voice-stealing, audio-pipeline]

# Dependency graph
requires: []
provides:
  - "VoiceAllocator with configurable max voices (default 32) and steal-quietest policy"
  - "setMaxVoices(Int) built-in for runtime voice limit configuration"
  - "Automatic voice allocation integrated into SequenceRenderer pipeline"
affects: [audio-pipeline, song-rendering]

# Tech tracking
tech-stack:
  added: []
  patterns: ["steal-quietest voice allocation for batch rendering"]

key-files:
  created:
    - flow-lang/StandardLibrary/Audio/VoiceAllocator.cs
    - tests/test_voice_allocation.flow
  modified:
    - flow-lang/StandardLibrary/Audio/SequenceRenderer.cs
    - flow-lang/StandardLibrary/BuiltInFunctions.cs
    - flow-lang/audio.flow

key-decisions:
  - "Voice allocation at SequenceRenderer level only (not BarRenderer) to avoid double-limiting"
  - "Batch steal-quietest: sort by peak amplitude, keep loudest N voices"

patterns-established:
  - "Voice allocation integrated at sequence render level for transparent polyphony limits"

requirements-completed: [AUDIO-04]

# Metrics
duration: 8min
completed: 2026-04-02
---

# Phase 02 Plan 03: Voice Allocation Summary

**Polyphonic voice allocator with steal-quietest policy, configurable 32-voice default limit, integrated into SequenceRenderer pipeline**

## Performance

- **Duration:** 8 min
- **Started:** 2026-04-02T00:33:14Z
- **Completed:** 2026-04-02T00:42:06Z
- **Tasks:** 2
- **Files modified:** 5

## Accomplishments
- VoiceAllocator class with steal-quietest policy drops lowest-amplitude voices when limit exceeded
- setMaxVoices(Int) built-in allows runtime configuration of voice limit
- Dense 8-note chords render cleanly under both default (32) and restricted (4) voice limits
- Integration is transparent -- existing code automatically benefits from voice allocation

## Task Commits

Each task was committed atomically:

1. **Task 1: VoiceAllocator class with steal-quietest policy** - `f013558` (feat)
2. **Task 2: Integrate VoiceAllocator into render pipeline and create test** - `0a5e19a` (feat)

## Files Created/Modified
- `flow-lang/StandardLibrary/Audio/VoiceAllocator.cs` - Static voice allocator with MaxVoices property, Allocate method, peak amplitude scoring, 5ms fade-out for stolen voices
- `flow-lang/StandardLibrary/BuiltInFunctions.cs` - setMaxVoices(Int) registration with validation (>= 1)
- `flow-lang/StandardLibrary/Audio/SequenceRenderer.cs` - Both RenderSequenceToVoices overloads call VoiceAllocator.Allocate
- `flow-lang/audio.flow` - internal proc setMaxVoices declaration
- `tests/test_voice_allocation.flow` - Integration test for voice allocation with default and restricted limits
- `flow-interpreter/flow-interpreter.csproj` - Removed invalid Release artifact references (pre-existing build fix)

## Decisions Made
- Applied allocation at SequenceRenderer level only (not BarRenderer) to avoid double-limiting when BarRenderer is called from SequenceRenderer
- Used batch steal-quietest approach (sort by peak amplitude, keep loudest N) rather than per-time-point allocation, since Flow renders voices in batch

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Fixed pre-existing csproj build error**
- **Found during:** Task 1 (build verification)
- **Issue:** flow-interpreter.csproj referenced Release build artifacts (std.flow, collections.flow) that did not exist, causing MSB3030 build failure
- **Fix:** Removed invalid ItemGroup referencing bin/Release/net9.0/*.flow files
- **Files modified:** flow-interpreter/flow-interpreter.csproj
- **Verification:** dotnet build succeeds
- **Committed in:** f013558 (Task 1 commit)

**2. [Rule 1 - Bug] Added missing internal proc declaration for setMaxVoices**
- **Found during:** Task 2 (test execution)
- **Issue:** setMaxVoices was registered in C# but lacked the `internal proc` declaration in audio.flow, causing "Function not found" errors at runtime
- **Fix:** Added `internal proc setMaxVoices(Int: maxVoices)` to flow-lang/audio.flow
- **Files modified:** flow-lang/audio.flow
- **Verification:** test_voice_allocation.flow runs successfully
- **Committed in:** 0a5e19a (Task 2 commit)

---

**Total deviations:** 2 auto-fixed (1 blocking, 1 bug)
**Impact on plan:** Both fixes necessary for correctness. No scope creep.

## Issues Encountered
- Pre-existing test failures in test_full_song.flow (missing output directory) and test_musical_context_errors.flow (expected error reporting exits non-zero) -- not caused by this plan's changes

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Voice allocation foundation complete
- Ready for future real-time voice stealing (fade-out infrastructure already in place)
- SongRenderer could optionally integrate allocation at section level for even more control

---
*Phase: 02-audio-pipeline*
*Completed: 2026-04-02*
