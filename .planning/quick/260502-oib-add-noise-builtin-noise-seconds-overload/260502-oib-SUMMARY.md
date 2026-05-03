---
phase: 260502-oib
plan: 01
subsystem: standard-library/audio
tags: [audio, signal-generation, builtin, white-noise, plumbing]
dependency_graph:
  requires:
    - "flow-lang/StandardLibrary/Audio/SynthUtils.cs (GenerateWhiteNoise) -- existing"
    - "flow-lang/StandardLibrary/Audio/AudioCore.cs (AudioBuffer) -- existing"
  provides:
    - "noise/1, noise/2, noise/3, noise/4 -- four arity-based overloads of the noise builtin"
  affects:
    - "BuiltInDocs.cs:97 'Generates a white-noise buffer.' description -- now backed by an actual implementation"
tech_stack:
  added: []
  patterns:
    - "Arity-based overloading via OverloadResolver (same name, different param counts)"
    - "Charitable-interpretation clamping (silent edge-case handling) per user memory"
    - "Delegating overloads -- 1/2/3-arity wrap the 4-arity core with default Value boxes"
key_files:
  created:
    - "tests/test_noise_builtin.flow"
  modified:
    - "flow-lang/StandardLibrary/Audio/SignalGeneration.cs"
    - "flow-lang/StandardLibrary/BuiltInFunctions.cs"
    - "flow-lang/audio.flow"
decisions:
  - "Use the existing SynthUtils.GenerateWhiteNoise (in FlowLang.StandardLibrary.Audio.Synthesizers) rather than introduce a new noise generator -- keeps a single source of randomness and reuses the deterministic SynthNoiseSeed RNG"
  - "1/2/3-arity overloads delegate to the 4-arity Noise() core with default Value boxes (amplitude=1.0, channels=1, sampleRate=44100) rather than duplicate clamping logic"
  - "Clamp silently rather than throw on invalid args (per user 'charitable interpretation' memory): negative seconds -> 0 frames, channels < 1 -> 1, sampleRate <= 0 -> 44100"
  - "Test uses (concat str-prefix (str int)) instead of (str a b c) -- str is single-arg in this stdlib and concat is binary"
metrics:
  duration_minutes: ~5
  completed: "2026-05-02"
  tasks_completed: 2
  files_changed: 4
---

# Phase 260502-oib Plan 01: Add noise builtin Summary

Added a `noise` builtin to flow-lang exposing four arity-based overloads (1/2/3/4 args) that wrap the existing `SynthUtils.GenerateWhiteNoise` C# function -- pure plumbing, no new DSP.

## What Was Built

### Four registered signatures (BuiltInFunctions.cs)

| Arity | Signature | Defaults filled |
|-------|-----------|-----------------|
| 1 | `noise(Double seconds)` | amplitude=1.0, channels=1, sampleRate=44100 |
| 2 | `noise(Double seconds, Double amplitude)` | channels=1, sampleRate=44100 |
| 3 | `noise(Double seconds, Double amplitude, Int channels)` | sampleRate=44100 |
| 4 | `noise(Double seconds, Double amplitude, Int channels, Int sampleRate)` | (none -- core overload) |

The OverloadResolver disambiguates by argument count. The 1/2/3-arity methods (`Noise1`, `Noise2`, `Noise3`) delegate to the 4-arity core (`Noise`) with default `Value` boxes, so all clamping logic lives in one place.

### Defaults chosen

- **Sample rate:** 44100 Hz (matches `DEFAULT_SAMPLE_RATE` constant in audio.flow and the existing `CreateClip` / `CreateSineTone` defaults)
- **Channels:** 1 (mono) -- matches `CreateClip` and follows the principle that explicit stereo should be opt-in for raw signal generators
- **Amplitude:** 1.0 (full-scale white noise, the canonical default)

### Charitable clamping (per user memory feedback)

Rather than throw `ArgumentException` on invalid input, the core `Noise()` method silently corrects:

| Input | Behavior |
|-------|----------|
| `seconds < 0` | clamped to 0 (returns 0-frame buffer) |
| `channels < 1` | promoted to 1 |
| `sampleRate <= 0` | falls back to 44100 |

Test 5a/5b/5c in `tests/test_noise_builtin.flow` exercise each clamping path. This matches the user's documented preference for "silent-and-documented assumptions over errors; music > rigid correctness."

### `internal proc` declarations (audio.flow)

Four `internal proc noise(...)` declarations placed alongside the existing signal-generation declarations (next to `internal proc createClip`), documented with `Note:` comments describing each arity.

## Verification

- `dotnet build` -> 0 errors, 0 new warnings (3 pre-existing nullable warnings only)
- `dotnet run --project flow-interpreter tests/test_noise_builtin.flow` -> exits 0
- Output of all 8 buffer-shape assertions matches expected values (frames, channels, sampleRate)
- `prettyBuffer` output of a 44-sample noise buffer at amplitude 0.5 reports `peak 0.4979` -- proves SynthUtils.GenerateWhiteNoise is actually wired in and writing samples
- Regression: `tests/test_buffer_printing.flow` and `tests/test_custom_oscillator.flow` still pass

## Documentation Gap Closed

`flow-lang/StandardLibrary/Audio/BuiltInDocs.cs:97` advertised `noise` as "Generates a white-noise buffer." but no signature was registered. Calling `(noise ...)` from a `.flow` script would error with `Function 'noise' not found`. That description is now backed by an actual implementation across all four advertised arities.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Test script used (str a b c) but `str` is single-arg**
- **Found during:** TDD GREEN -- test failed with `No matching overload for function 'str' with argument types (String, String, String)`
- **Issue:** Original test code (and plan example) called `(str "  frames=" (str (getFrames b1)) " (expected 44100)")` with three args; flow-lang's `StdLib.cs` `str` overloads all take exactly 1 arg
- **Fix:** Switched to nested `(concat ... (concat (str x) "..."))` -- the canonical pattern used by other tests (`test_tempo_expression.flow`, `test_slice_negative.flow`, `test_range.flow`)
- **Files modified:** tests/test_noise_builtin.flow
- **Commit:** included in 08d505a (GREEN commit)

No other deviations -- plan executed as written.

## Authentication Gates

None.

## Known Stubs

None.

## TDD Gate Compliance

This plan used `tdd="true"` on Task 1. Gate sequence verified in git log:

1. RED gate: `bc68451 test(260502-oib): add failing test for noise builtin (RED)` -- test failed with `Function 'noise' not found` before implementation
2. GREEN gate: `08d505a feat(260502-oib): implement noise builtin with 4 arity overloads (GREEN)` -- test passes after implementation
3. REFACTOR gate: skipped (no cleanup needed -- code is already minimal)

Both required gates present.

## Self-Check: PASSED

- [x] tests/test_noise_builtin.flow exists (created in bc68451, updated in 08d505a)
- [x] flow-lang/StandardLibrary/Audio/SignalGeneration.cs modified (08d505a)
- [x] flow-lang/StandardLibrary/BuiltInFunctions.cs modified (08d505a)
- [x] flow-lang/audio.flow modified (08d505a)
- [x] Commit bc68451 (RED) exists in git log
- [x] Commit 08d505a (GREEN) exists in git log
- [x] grep counts: 4 Noise methods / 4 noise registrations / 4 internal proc noise declarations
- [x] dotnet build succeeds (0 errors)
- [x] dotnet run tests/test_noise_builtin.flow exits 0 with "All noise-builtin tests passed."
- [x] Regression tests still pass (test_buffer_printing.flow, test_custom_oscillator.flow)
