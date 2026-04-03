---
phase: 04-composition-tools
plan: 02
subsystem: composition
tags: [polyrhythm, variation, generative, diatonic, lcm, mutation]

requires:
  - phase: 02-audio-pipeline
    provides: SongRenderer mixing, SequenceRenderer, Voice, AudioBuffer infrastructure
provides:
  - polyrhythm() built-in function with LCM cycle alignment and beat count override
  - vary() built-in function with four mutation types and diatonic pitch support
  - Composition/ directory as new standard library module area
affects: [05-live-coding, composition-tools]

tech-stack:
  added: []
  patterns: [internal proc declarations in .flow files for C# built-in mapping, Composition namespace for generative tools]

key-files:
  created:
    - flow-lang/StandardLibrary/Composition/PolyrhythmFunctions.cs
    - flow-lang/StandardLibrary/Composition/VariationFunctions.cs
  modified:
    - flow-lang/StandardLibrary/Audio/SongRenderer.cs
    - flow-lang/StandardLibrary/BuiltInFunctions.cs
    - flow-lang/composition.flow

key-decisions:
  - "Changed SongRenderer.MixVoicesToStereoBuffer from private to internal for reuse by PolyrhythmFunctions"
  - "Registered with DoubleType for probability parameter since Flow float literals resolve as Double"
  - "Added internal proc declarations to composition.flow (required for Flow function resolution)"

patterns-established:
  - "Composition namespace: generative/compositional built-ins go in StandardLibrary/Composition/"
  - "Internal proc pattern: C# built-ins need matching internal proc declarations in .flow stdlib files"

requirements-completed: [COMP-03, COMP-04]

duration: 12min
completed: 2026-04-03
---

# Phase 4 Plan 2: Polyrhythm and Variation Summary

**Polyrhythm layering with LCM cycle alignment and probabilistic pattern variation with diatonic pitch mutations**

## Performance

- **Duration:** 12 min
- **Started:** 2026-04-03T02:37:42Z
- **Completed:** 2026-04-03T02:49:42Z
- **Tasks:** 2
- **Files modified:** 5

## Accomplishments
- polyrhythm() overlays sequences with different time signatures, using LCM for cycle alignment with optional beat count override
- vary() provides six overloads supporting random/typed/seeded/diatonic variations across four mutation types
- Diatonic pitch mutations use ScaleDatabase.GetScaleNotes() to keep notes within key context
- Established Composition/ directory as the namespace for generative composition tools

## Task Commits

Each task was committed atomically:

1. **Task 1: Implement polyrhythm built-in function with registration** - `62d2966` (feat)
2. **Task 2: Implement variation built-in function with diatonic pitch support** - `83bdd10` (feat)

## Files Created/Modified
- `flow-lang/StandardLibrary/Composition/PolyrhythmFunctions.cs` - polyrhythm() with LCM calculation, voice looping, and stereo mixing
- `flow-lang/StandardLibrary/Composition/VariationFunctions.cs` - vary() with pitch/rhythm/rest/velocity mutations and diatonic support
- `flow-lang/StandardLibrary/Audio/SongRenderer.cs` - MixVoicesToStereoBuffer changed from private to internal
- `flow-lang/StandardLibrary/BuiltInFunctions.cs` - Registration of PolyrhythmFunctions and VariationFunctions
- `flow-lang/composition.flow` - Internal proc declarations for polyrhythm and vary overloads

## Decisions Made
- Changed SongRenderer.MixVoicesToStereoBuffer visibility from private to internal to allow reuse by PolyrhythmFunctions without code duplication
- Used DoubleType.Instance for probability parameter registration since Flow's float literals (e.g., 0.3) resolve as Double at runtime
- Added internal proc declarations to composition.flow since Flow requires matching proc declarations in .flow files for C# built-in function resolution
- Implemented MIDI helpers locally in VariationFunctions (same logic as TransformFunctions) since those are private to their class

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Added internal proc declarations to composition.flow**
- **Found during:** Task 1 (polyrhythm registration)
- **Issue:** Flow requires matching `internal proc` declarations in .flow standard library files for built-in C# functions to be resolvable at runtime. The plan only mentioned C# registration.
- **Fix:** Added `internal proc` declarations for all polyrhythm and vary overloads to composition.flow
- **Files modified:** flow-lang/composition.flow
- **Verification:** polyrhythm and vary functions resolve and execute correctly in test scripts

**2. [Rule 1 - Bug] Removed // comments from test files**
- **Found during:** Task 1 (test file creation)
- **Issue:** Flow language does not support // comments; test files used them causing parse errors
- **Fix:** Removed all // comments from test .flow files, used Note: prefix where needed
- **Files modified:** tests/test_polyrhythm.flow, tests/test_variation.flow

---

**Total deviations:** 2 auto-fixed (1 blocking, 1 bug)
**Impact on plan:** Both fixes necessary for correct function resolution and test execution. No scope creep.

## Issues Encountered
- .NET 9 SDK not available in current environment (only .NET 8 SDK 8.0.125). Temporarily switched csproj target to net8.0 for build verification, then restored. Code is compatible with both .NET 8 and 9.
- tests/ directory is in .gitignore so test files are not committed. Tests verified execution but live in local worktree only.

## Known Stubs
None - all functions are fully implemented with real logic.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Polyrhythm and variation tools complete, ready for use in composition workflows
- Phase 4 composition tools are now complete (plans 01 and 02)

---
*Phase: 04-composition-tools*
*Completed: 2026-04-03*

## Self-Check: PASSED
- All 5 key files found
- Both task commits (62d2966, 83bdd10) verified in git log
