---
phase: 08-audio-production
plan: 01
subsystem: audio
tags: [mix, gain, musical-context, song-renderer, buffer]

requires:
  - phase: 02-audio-pipeline
    provides: AudioBuffer, SongRenderer, DSP effects, musical context infrastructure

provides:
  - mix(Buffer, Buffer) built-in function with mono-to-stereo promotion
  - gain musical context block for per-section volume control
  - gain context threading through SongRenderer

affects: [08-audio-production, composition-tools]

tech-stack:
  added: []
  patterns:
    - "Musical context keyword dual-use pattern (gain as both context block and function name)"
    - "Mono-to-stereo promotion in buffer operations"

key-files:
  created:
    - tests/test_mix.flow
    - tests/test_gain_context.flow
  modified:
    - flow-lang/StandardLibrary/Audio/AudioCore.cs
    - flow-lang/StandardLibrary/BuiltInFunctions.cs
    - flow-lang/audio.flow
    - flow-lang/Runtime/MusicalContext.cs
    - flow-lang/Runtime/ExecutionContext.cs
    - flow-lang/Ast/Statements/MusicalContextStatement.cs
    - flow-lang/Lexing/TokenType.cs
    - flow-lang/Lexing/SimpleLexer.cs
    - flow-lang/Parsing/Parser.cs
    - flow-lang/Interpreter/Interpreter.cs
    - flow-lang/StandardLibrary/Audio/SongRenderer.cs

key-decisions:
  - "Gain keyword uses same dual-use pattern as pan: lookahead disambiguates context block vs function name"
  - "Gain range [0.0, 2.0] allows slight boost beyond unity; default is 1.0 at usage site"
  - "mix() sums samples without normalization -- users apply gain separately if needed"

patterns-established:
  - "Keyword dual-use: add TokenType, keyword mapping, lookahead in parser, identifier fallback in 5 parser locations"

requirements-completed: [AUDIO-05, AUDIO-06]

duration: 11min
completed: 2026-04-04
---

# Phase 08 Plan 01: Mix Function and Gain Context Summary

**mix(Buffer, Buffer) for buffer layering and gain context block for per-section volume control in song rendering**

## Performance

- **Duration:** 11 min
- **Started:** 2026-04-04T01:57:56Z
- **Completed:** 2026-04-04T02:08:54Z
- **Tasks:** 2
- **Files modified:** 11

## Accomplishments
- Added mix(Buffer, Buffer) built-in that sums samples at unity gain, handles different-length buffers via zero-padding, and promotes mono to stereo when channel counts differ
- Added gain musical context block (gain 0.5 { ... }) with [0.0, 2.0] range validation, full stack threading, and per-section gain multiplication in SongRenderer
- Preserved backward compatibility: existing gain DSP effect function still works via pipe syntax

## Task Commits

Each task was committed atomically:

1. **Task 1: Add mix(Buffer, Buffer) built-in function** - `41e76d9` (feat)
2. **Task 2: Add gain musical context block with per-section gain in SongRenderer** - `ddd72c5` (feat)

## Files Created/Modified
- `flow-lang/StandardLibrary/Audio/AudioCore.cs` - Added Mix() and MonoToStereo() methods
- `flow-lang/StandardLibrary/BuiltInFunctions.cs` - Registered mix function signature
- `flow-lang/audio.flow` - Declared mix as internal proc
- `flow-lang/Runtime/MusicalContext.cs` - Added Gain property, Clone support, IsValidGain validation
- `flow-lang/Runtime/ExecutionContext.cs` - Added Gain to stack walk resolution and early-exit check
- `flow-lang/Ast/Statements/MusicalContextStatement.cs` - Added Gain to MusicalContextType enum
- `flow-lang/Lexing/TokenType.cs` - Added Gain token type
- `flow-lang/Lexing/SimpleLexer.cs` - Added "gain" keyword mapping
- `flow-lang/Parsing/Parser.cs` - Added gain context block parsing with lookahead, identifier fallback in 5 locations
- `flow-lang/Interpreter/Interpreter.cs` - Added Gain case in ExecuteMusicalContext
- `flow-lang/StandardLibrary/Audio/SongRenderer.cs` - Applied gain to Voice.Gain in both render methods
- `tests/test_mix.flow` - Tests for mix with same-length, different-length buffers, and pipe syntax
- `tests/test_gain_context.flow` - Tests for gain context blocks, nested contexts, song rendering, and gain DSP pipe

## Decisions Made
- Gain keyword uses same dual-use pattern as pan: numeric lookahead disambiguates context block from function name
- Gain range is [0.0, 2.0] to allow slight boost beyond unity; default 1.0 at usage site (not stored in context)
- mix() sums samples without gain normalization -- users apply gain/compress separately if clipping occurs

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Added gain as identifier in 5 parser locations**
- **Found during:** Task 2 (gain context block)
- **Issue:** Tokenizing "gain" as TokenType.Gain broke existing `gain` DSP function usage (e.g., `comp -> gain negThree` in test_full_song.flow)
- **Fix:** Added Check(TokenType.Gain) / Match(TokenType.Gain) alongside Pan in all 5 parser locations where identifiers are expected: proc names, param names, paren function calls, primary expressions, and ExpectParameterName
- **Files modified:** flow-lang/Parsing/Parser.cs
- **Verification:** test_full_song.flow passes, gain DSP pipe works in test_gain_context.flow
- **Committed in:** ddd72c5 (Task 2 commit)

**2. [Rule 3 - Blocking] Added mix declaration to audio.flow**
- **Found during:** Task 1 (mix function)
- **Issue:** Registering mix in BuiltInFunctions.cs alone was insufficient; Flow requires `internal proc` declarations in the .flow stdlib module for functions to be discoverable via `use "@audio"`
- **Fix:** Added `internal proc mix(Buffer: a, Buffer: b)` declaration to audio.flow
- **Files modified:** flow-lang/audio.flow
- **Verification:** test_mix.flow passes with `use "@audio"`
- **Committed in:** 41e76d9 (Task 1 commit)

---

**Total deviations:** 2 auto-fixed (2 blocking)
**Impact on plan:** Both auto-fixes necessary for correctness. No scope creep.

## Issues Encountered
- renderSequence crashes with index-out-of-range when called directly (pre-existing bug, not caused by this plan). Test adjusted to use section + renderSong pattern instead.

## Known Stubs
None -- all functionality is fully wired.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- mix() and gain context ready for use in composition workflows
- gain context integrates cleanly with existing tempo/key/pan context stack

---
*Phase: 08-audio-production*
*Completed: 2026-04-04*
