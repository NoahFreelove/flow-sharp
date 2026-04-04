---
phase: 08-audio-production
plan: 02
subsystem: audio
tags: [synthesizer, additive-synthesis, sawtooth, risset-bell, organ, strings]

requires:
  - phase: 08-audio-production
    provides: "Audio production infrastructure, existing synthesizer pattern (PianoSynthesizer)"
provides:
  - "StringsSynthesizer: detuned sawtooth pad with slow attack"
  - "OrganSynthesizer: Hammond-style additive synthesis with 6 drawbar harmonics"
  - "BellSynthesizer: Risset inharmonic bell with per-partial exponential decay"
  - "SynthesizerFactory registration for strings/string, organ, bell instrument names"
affects: [audio-production, composition, song-rendering]

tech-stack:
  added: []
  patterns: ["Per-partial exponential decay envelopes for bell/metallic timbres", "Detuned oscillator pairs for ensemble/chorus effects"]

key-files:
  created:
    - flow-lang/StandardLibrary/Audio/Synthesizers/StringsSynthesizer.cs
    - flow-lang/StandardLibrary/Audio/Synthesizers/OrganSynthesizer.cs
    - flow-lang/StandardLibrary/Audio/Synthesizers/BellSynthesizer.cs
    - tests/test_synth_presets.flow
  modified:
    - flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs

key-decisions:
  - "4 cents detune for strings ensemble warmth (enough beating without pitch drift)"
  - "Hammond drawbar amplitudes: 1.0/0.8/0.6/0.5/0.3/0.2 for classic tonewheel sound"
  - "Risset bell partials at ratios 1.0/2.2/3.6/4.1/5.8 with per-partial exponential decay"

patterns-established:
  - "Inharmonic partial synthesis: loop partials with individual envelopes instead of shared ADSR"
  - "Anti-click ramp: short linear attack on first 50 samples for percussive envelopes"

requirements-completed: [AUDIO-07]

duration: 3min
completed: 2026-04-04
---

# Phase 08 Plan 02: Synth Presets Summary

**Three new synthesizer presets (strings pad, Hammond organ, Risset bell) with factory registration and test coverage**

## Performance

- **Duration:** 3 min
- **Started:** 2026-04-04T01:58:03Z
- **Completed:** 2026-04-04T02:01:13Z
- **Tasks:** 2
- **Files modified:** 5

## Accomplishments
- StringsSynthesizer produces warm detuned sawtooth pad with slow attack ADSR and lowpass warmth filter
- OrganSynthesizer produces Hammond-style additive tone with 6 drawbar harmonics and near-instant attack
- BellSynthesizer produces metallic Risset bell with 5 inharmonic partials and per-partial exponential decay
- All three registered in SynthesizerFactory and verified working with renderSequenceToVoices and renderSong

## Task Commits

Each task was committed atomically:

1. **Task 1: Create StringsSynthesizer and OrganSynthesizer** - `832dce9` (feat)
2. **Task 2: Create BellSynthesizer, register all three in factory, and test** - `e5827b1` (feat)

## Files Created/Modified
- `flow-lang/StandardLibrary/Audio/Synthesizers/StringsSynthesizer.cs` - Detuned sawtooth pad synth (4 cents detune, slow ADSR, lowpass)
- `flow-lang/StandardLibrary/Audio/Synthesizers/OrganSynthesizer.cs` - Hammond additive organ (6 drawbar harmonics, instant attack)
- `flow-lang/StandardLibrary/Audio/Synthesizers/BellSynthesizer.cs` - Risset inharmonic bell (5 partials, per-partial exponential decay)
- `flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs` - Added strings/string, organ, bell to SynthesizerFactory switch
- `tests/test_synth_presets.flow` - Test script exercising all three presets via renderSequenceToVoices and renderSong

## Decisions Made
- 4 cents detune for strings: produces audible warm beating without perceived pitch drift
- Hammond drawbar amplitudes tuned for classic organ sound (16' through 2' stops)
- Risset bell uses per-partial exponential decay rather than shared ADSR, as this is the defining characteristic of bell timbres
- Added lowpass warmth filter on strings to soften saw harmonics
- Anti-click ramp (50 samples) on bell to avoid transient pop from non-ADSR envelope

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed test script calling convention**
- **Found during:** Task 2
- **Issue:** Plan suggested `renderSequence(seq, "strings")` but no such 2-arg function exists; the API is `renderSequenceToVoices` with 4 args using S-expression syntax
- **Fix:** Rewrote test to use `(renderSequenceToVoices seq "strings" 44100 120.0)` and `(renderSong song "strings")` with correct Flow calling conventions
- **Files modified:** tests/test_synth_presets.flow
- **Committed in:** e5827b1

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** Necessary correction for test to work with actual API. No scope creep.

## Issues Encountered
None beyond the test API correction above.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- All three synth presets available for use in compositions
- Existing instruments (piano, brass, sax, drums, flute) unaffected
- Pattern established for adding future synth presets

---
*Phase: 08-audio-production*
*Completed: 2026-04-04*

## Self-Check: PASSED

All 4 created files verified on disk. Both task commits (832dce9, e5827b1) verified in git log.
