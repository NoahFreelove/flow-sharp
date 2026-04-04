---
phase: 10-vocalization
plan: 02
subsystem: audio
tags: [vocalization, formant-synthesis, tts, built-in-functions]

requires:
  - phase: 10-01
    provides: FormantSynthesizer, ConsonantSynthesizer, FormantData DSP engine

provides:
  - sing() built-in function for formant vocal synthesis from Flow scripts
  - tts() built-in function for external TTS engine integration
  - setTtsCommand() for configuring TTS backend
  - Integration test covering vowels, consonants, mixing, WAV export

affects: []

tech-stack:
  added: []
  patterns: [external-process-wrapper-with-timeout, note-string-to-frequency-parsing]

key-files:
  created:
    - flow-lang/StandardLibrary/Audio/Vocalization/TtsHook.cs
    - flow-lang/StandardLibrary/Audio/Vocalization/VocalizationFunctions.cs
    - tests/test_vocalization.flow
  modified:
    - flow-lang/StandardLibrary/BuiltInFunctions.cs
    - flow-lang/audio.flow

key-decisions:
  - "TtsHook uses Process with 30s timeout and WAV stream parsing"
  - "sing() parses Note string inline rather than adding a PitchConversion overload"

patterns-established:
  - "Vocalization registration pattern: VocalizationFunctions.Register follows EffectsFunctions pattern"

requirements-completed: [VOC-01, VOC-02]

duration: 2min
completed: 2026-04-04
---

# Phase 10 Plan 02: Vocalization Runtime Integration Summary

**Wired formant synthesis and TTS hook into Flow runtime with sing(), tts(), and setTtsCommand() built-in functions**

## Performance

- **Duration:** 2 min
- **Started:** 2026-04-04T03:21:33Z
- **Completed:** 2026-04-04T03:23:34Z
- **Tasks:** 2
- **Files modified:** 5

## Accomplishments
- sing() built-in synthesizes vowels and consonant-vowel syllables at any pitch and duration
- tts() wraps external TTS processes (espeak-ng default) with 30-second timeout and WAV parsing
- Integration test validates all 5 vowels, 3 consonant syllables, mixing, pitch variation, and WAV export

## Task Commits

Each task was committed atomically:

1. **Task 1: Create TtsHook and VocalizationFunctions, register in BuiltInFunctions and audio.flow** - `ce7a277` (feat)
2. **Task 2: Create integration test script** - `534fd33` (test)

## Files Created/Modified
- `flow-lang/StandardLibrary/Audio/Vocalization/TtsHook.cs` - External TTS process wrapper with WAV stream parsing and 30s timeout
- `flow-lang/StandardLibrary/Audio/Vocalization/VocalizationFunctions.cs` - Registers sing, tts, setTtsCommand built-ins
- `flow-lang/StandardLibrary/BuiltInFunctions.cs` - Added VocalizationFunctions.Register call
- `flow-lang/audio.flow` - Added internal proc declarations for vocalization functions
- `tests/test_vocalization.flow` - Integration test covering vowels, consonants, mixing, export

## Decisions Made
- TtsHook parses WAV from MemoryStream (mirroring FileIO.LoadWavInternal) rather than writing to temp file, for better performance
- Note string parsing done inline in Sing() method rather than adding another PitchConversion overload

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required. TTS functions gracefully error when espeak-ng is not installed.

## Next Phase Readiness
- Vocalization phase complete: formant DSP engine + runtime integration + tests all working
- sing() output is compatible with existing audio pipeline (mix, effects, writeWav, play)

---
*Phase: 10-vocalization*
*Completed: 2026-04-04*

## Self-Check: PASSED
