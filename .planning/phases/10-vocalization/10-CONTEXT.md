# Phase 10: Vocalization - Context

**Gathered:** 2026-04-04
**Status:** Ready for planning

<domain>
## Phase Boundary

Add vocal synthesis to Flow — formant-based vowel/consonant synthesis as a built-in engine, plus an external TTS hook for higher-quality speech. Vocals produce regular AudioBuffers that users mix with instrumental tracks. Separate from the core interpreter pipeline.

</domain>

<decisions>
## Implementation Decisions

### Synthesis Approach
- **D-01:** Primary engine: **formant synthesis** — zero dependencies, hand-rolled in C#. Buzz source (pulse wave at pitch) filtered through formant bandpass filters to produce vowel sounds.
- **D-02:** Each vowel is defined by a set of formant frequencies (F1, F2, F3). Standard acoustic phonetics values.
- **D-03:** Consonants use noise bursts (s), clicks (t), and nasal resonance (n) — simple approximations.
- **D-04:** Secondary engine: **external TTS hook** — `tts(String text) -> Buffer` shells out to a configurable command (e.g., espeak-ng, piper-tts) and captures the WAV output. Unknown audio length is fine — returned Buffer has whatever length TTS produces.

### API Design
- **D-05:** `sing(String phoneme, Note pitch, Double duration) -> Buffer` — formant synthesis of a single vowel/syllable at a given pitch and duration in seconds.
- **D-06:** `tts(String text) -> Buffer` — external TTS hook. Calls a configurable system command, captures WAV output, returns as Buffer.
- **D-07:** Both functions return standard `AudioBuffer` — composable via `->` and mixable with `mix()`. No special vocal type needed.
- **D-08:** TTS command configurable via `setTtsCommand(String)` built-in. Default: `"espeak-ng --stdout"`. User can set to any command that writes WAV to stdout.

### Scope (v1.1 Foundation)
- **D-09:** 5 vowel phonemes: "ah" (a), "ee" (i), "oh" (o), "oo" (u), "eh" (e). Each with standard formant frequency table.
- **D-10:** 3 consonant approximations (stretch goal): "s" (noise burst), "t" (click transient), "n" (nasal resonance). Allow simple syllables: "na", "ta", "sa".
- **D-11:** Syllable parsing: if phoneme string is 2+ chars and starts with a consonant, split into consonant onset + vowel nucleus. E.g., "na" = "n" onset + "ah" vowel.
- **D-12:** External TTS hook as separate function, not integrated with formant engine.

### Integration
- **D-13:** Vocals produce regular `AudioBuffer`. Users combine with instruments using existing `mix()` function. No special song-level vocal integration.
- **D-14:** Implementation lives in `flow-lang/StandardLibrary/Audio/Vocalization/` — new subdirectory. Keeps vocal code separate from the instrument synth code.
- **D-15:** Register `sing` and `tts` in `BuiltInFunctions.cs`. Add `internal proc` declarations in `audio.flow`.

### Claude's Discretion
- Exact formant frequency tables (standard acoustic phonetics references)
- Buzz source waveform details (pulse width, spectral tilt)
- Bandpass filter implementation (reuse existing DSP filters or new dedicated formant filter)
- Consonant timing (onset duration in ms)
- TTS command error handling (what to return if command fails)
- Whether to add a `singSequence(String[] phonemes, Sequence notes) -> Buffer` convenience function

</decisions>

<canonical_refs>
## Canonical References

### DSP Infrastructure
- `flow-lang/StandardLibrary/Audio/DSP/Filter.cs` — Existing bandpass filter. May reuse for formant filtering.
- `flow-lang/StandardLibrary/Audio/SynthUtils.cs` — GenerateSine, GenerateADSR, shared utilities.
- `flow-lang/StandardLibrary/Audio/AudioCore.cs` — Buffer operations, MixBuffers.

### Synthesizer Pattern
- `flow-lang/StandardLibrary/Audio/Synthesizers/BrassSynthesizer.cs` — Uses formant-like filtering for timbre shaping. Reference pattern.
- `flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs` — INoteSynthesizer interface (vocal synth may or may not implement this).

### Registration
- `flow-lang/StandardLibrary/BuiltInFunctions.cs` — Register sing/tts functions.
- `flow-lang/audio.flow` — Internal proc declarations.

### External Process
- `System.Diagnostics.Process` — .NET built-in for shelling out to TTS command. No new dependencies.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `Filter.ApplyBandpass` — Existing bandpass filter for formant filtering
- `SynthUtils.GenerateADSR` — Envelope shaping for vocal amplitude
- `AudioCore.MixBuffers` — Combining vocal with instrumental
- `PitchConversion.NoteToFrequency` — Note-to-Hz conversion for vocal pitch

### Integration Points
- `BuiltInFunctions.cs` — Register sing/tts
- `audio.flow` — Internal proc declarations
- New directory: `StandardLibrary/Audio/Vocalization/`

</code_context>

<specifics>
## Specific Ideas

- The formant sound should be distinctive — think Kraftwerk "The Robots" or chiptune vocals
- `(sing "ah" C4 2.0)` should produce a recognizable "ah" vowel at middle C for 2 seconds
- External TTS hook enables users to bring in any TTS engine they have installed
- Syllable combination ("na", "ta") adds musicality beyond pure vowels

</specifics>

<deferred>
## Deferred Ideas

- Full phoneme set (all English IPA phonemes) — future milestone
- Note stream integration (`| "ah"C4q "ee"E4q |`) — requires parser changes, defer
- Vocal section type in Song expressions — defer until API proven
- Vibrato/portamento on vocals — could add later as effect
- Multi-voice choir synthesis — future

</deferred>

---

*Phase: 10-vocalization*
*Context gathered: 2026-04-04*
