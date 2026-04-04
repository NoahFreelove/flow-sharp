---
phase: 10-vocalization
plan: 01
subsystem: audio
tags: [formant-synthesis, dsp, vowel, consonant, vocalization]

requires:
  - phase: existing
    provides: "Filter.Bandpass, SynthUtils (GenerateSaw, OnePoleLP, GenerateADSR, ApplyEnvelope, GenerateWhiteNoise)"
provides:
  - "FormantData: Csound tenor formant frequency tables for 5 vowels"
  - "FormantSynthesizer: vowel and syllable synthesis via parallel bandpass filtering"
  - "ConsonantSynthesizer: fricative, plosive, and nasal consonant onset generation"
affects: [10-02-vocalization, sing-function, vocal-builtins]

tech-stack:
  added: []
  patterns: [parallel-bandpass-formant-synthesis, consonant-vowel-crossfade]

key-files:
  created:
    - flow-lang/StandardLibrary/Audio/Vocalization/FormantData.cs
    - flow-lang/StandardLibrary/Audio/Vocalization/FormantSynthesizer.cs
    - flow-lang/StandardLibrary/Audio/Vocalization/ConsonantSynthesizer.cs
  modified: []

key-decisions:
  - "Combined Task 1 and Task 2 into single commit due to compilation dependency (FormantSynthesizer references ConsonantSynthesizer)"
  - "Used Csound Appendix D Tenor formant values as reference data"
  - "Master gain 0.3f applied post-formant-sum to prevent clipping"

patterns-established:
  - "Vocalization namespace: FlowLang.StandardLibrary.Audio.Vocalization for all vocal synthesis classes"
  - "Formant synthesis pattern: buzz source -> spectral tilt -> parallel bandpass -> sum with dB gains -> envelope -> master gain"
  - "Consonant-vowel crossfade: 2ms linear crossfade at junction to avoid clicks"

requirements-completed: [VOC-01]

duration: 2min
completed: 2026-04-04
---

# Phase 10 Plan 01: Formant Vocal Synthesis Engine Summary

**Kraftwerk-style formant synthesis engine with 5 vowels (ah/ee/eh/oh/oo), 3 consonants (s/t/n), and syllable crossfade combining**

## Performance

- **Duration:** 2 min
- **Started:** 2026-04-04T03:17:15Z
- **Completed:** 2026-04-04T03:19:11Z
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments
- FormantData with Csound tenor formant tables (5 formants x 5 vowels) and DbToLinear utility
- FormantSynthesizer producing vowel AudioBuffers via sawtooth buzz through parallel bandpass filters with ADSR envelope
- ConsonantSynthesizer generating fricative (80ms highpassed noise), plosive (10ms exponential-decay burst), and nasal (40ms lowpassed buzz) onsets
- SynthesizeSyllable combining consonant onset + vowel body with 2ms linear crossfade

## Task Commits

Each task was committed atomically:

1. **Task 1+2: FormantData, FormantSynthesizer, ConsonantSynthesizer** - `4801470` (feat)

Note: Tasks 1 and 2 were combined into a single commit because FormantSynthesizer.SynthesizeSyllable references ConsonantSynthesizer, making them a compilation unit.

## Files Created/Modified
- `flow-lang/StandardLibrary/Audio/Vocalization/FormantData.cs` - Vowel formant frequency tables (Csound tenor reference), DbToLinear utility
- `flow-lang/StandardLibrary/Audio/Vocalization/FormantSynthesizer.cs` - Core formant synthesis: buzz source + parallel bandpass filtering, syllable assembly
- `flow-lang/StandardLibrary/Audio/Vocalization/ConsonantSynthesizer.cs` - Consonant approximations: fricative (s), plosive (t), nasal (n)

## Decisions Made
- Combined both tasks into a single commit because FormantSynthesizer.SynthesizeSyllable calls ConsonantSynthesizer.Generate and ConsonantSynthesizer.IsConsonant, creating a compile-time dependency
- Used Csound Appendix D Tenor formant values as the canonical reference data
- Applied 0.3f master gain post-sum to prevent clipping from 5 overlapping formant bands

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Created ConsonantSynthesizer in Task 1 to enable compilation**
- **Found during:** Task 1 (FormantData and FormantSynthesizer)
- **Issue:** FormantSynthesizer.SynthesizeSyllable references ConsonantSynthesizer which was planned for Task 2
- **Fix:** Created full ConsonantSynthesizer implementation alongside Task 1 files
- **Files modified:** flow-lang/StandardLibrary/Audio/Vocalization/ConsonantSynthesizer.cs
- **Verification:** dotnet build succeeds with 0 errors
- **Committed in:** 4801470 (Task 1+2 combined commit)

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** Task ordering adjusted to resolve compilation dependency. No scope creep. All planned functionality delivered.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Known Stubs
None - all methods are fully implemented with working DSP logic.

## Next Phase Readiness
- Formant synthesis engine ready to be wired into Flow's built-in function registry (plan 10-02)
- All three classes are self-contained in the Vocalization namespace
- SynthesizeVowel and SynthesizeSyllable return AudioBuffer, ready for direct use by a `sing()` built-in

## Self-Check: PASSED

All 3 created files verified on disk. Commit 4801470 verified in git log.

---
*Phase: 10-vocalization*
*Completed: 2026-04-04*
