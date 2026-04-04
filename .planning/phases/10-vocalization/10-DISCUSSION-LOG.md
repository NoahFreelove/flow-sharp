# Phase 10: Vocalization - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.

**Date:** 2026-04-04
**Phase:** 10-vocalization
**Areas discussed:** Synthesis approach, API design, Scope, Integration

---

## Synthesis Approach

| Option | Description | Selected |
|--------|-------------|----------|
| Formant synthesis | Hand-rolled vowel/consonant via formant filters. Zero deps. | yes |
| Concatenative | Pre-recorded phoneme samples stitched together | |
| External TTS engine | Shell out to espeak-ng/festival | as secondary |

**User's choice:** Formant synthesis as primary, with external TTS hook as secondary option
**Notes:** User suggested adding TTS integration like "kitty tts where it can call some function and get the audio back" — acknowledged unknown audio length is fine

## API Design

| Option | Description | Selected |
|--------|-------------|----------|
| Function-based | sing("ah", C4, 1.0) and tts("text") | yes |
| Note stream integration | | "ah"C4q in note streams | |
| Vocal section type | vocal "ah ee" { notes } | |

**User's choice:** Function-based — sing() for formant, tts() for external
**Notes:** None

## Scope

| Option | Description | Selected |
|--------|-------------|----------|
| 5 vowels + basic consonants | ah/ee/oh/oo/eh + s/t/n | yes |
| Vowels only | Just 5 vowels | |
| Full phoneme set | All ~44 English phonemes | |

**User's choice:** 5 vowels + 3 consonants (s/t/n) for syllables, plus external TTS hook
**Notes:** None

## Integration

| Option | Description | Selected |
|--------|-------------|----------|
| Buffer-level mixing | Vocals are AudioBuffers, mix with mix() | yes |
| Song section integration | Vocal section type in Song | |

**User's choice:** Buffer-level mixing — keep it simple
**Notes:** None

## Claude's Discretion

- Formant frequency tables
- Buzz source waveform details
- Consonant timing
- TTS error handling

## Deferred Ideas

- Full English phoneme set — future milestone
- Note stream vocal syntax — needs parser work
- Vocal sections in Song — prove API first
- Vibrato/portamento — future effect
- Multi-voice choir — future
