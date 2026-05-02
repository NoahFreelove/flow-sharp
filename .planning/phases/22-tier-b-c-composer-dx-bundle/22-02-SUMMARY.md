---
phase: 22-tier-b-c-composer-dx-bundle
plan: 02
subsystem: audio
tags: [loadwav, varispeed, resample, overload, charitable-interpretation, dx-15]

requires:
  - phase: 14-composer-dx-part-1
    provides: existing 1-arg loadWav(path) and Resample linear-interpolation template
  - phase: 18-foundation-rational-duration-arithmetic
    provides: byte-identical regression gate (Tutorial WAV+MIDI, Showcase WAV+MIDI)
  - phase: 22-tier-b-c-composer-dx-bundle
    provides: 22-01 sibling-overload registration pattern (extend in place, preserve byte-identical)
provides:
  - "loadWav(path, semitones:Int) overload — varispeed pitch shift via 2^(semi/12) ratio"
  - "loadWav(path, ratio:Double) overload — varispeed pitch shift at arbitrary ratio"
  - "FileIO.VarispeedResample(buffer, ratio) helper — linear-interpolation resample preserving SampleRate + Channels"
  - "Identity short-circuits at semitones==0 and ratio==1.0 (no resample work, byte-identical to 1-arg path)"
  - "DoS guard: ratio <= 0.0 or NaN throws ArgumentException (T-22-V5-09)"
affects: [22-03-voicings, 22-04-delay-sync, 22-05-quantize, 22-06-legato-portamento, 22-07-closure]

tech-stack:
  added: []
  patterns:
    - "Sibling-overload registration alongside existing 1-arg signature (preserves byte-identical regression)"
    - "Identity short-circuit at unit values (semi=0 / ratio=1.0) — return source buffer without resample work"
    - "Wave 0 RED stub pattern: NotImplementedException keeps build green while assertions stay RED until GREEN body lands in Wave 2"

key-files:
  created:
    - flow-lang.Tests/Unit/Phase22/LoadWavVarispeedFacts.cs
    - tests/test_dx_loadwav_varispeed.flow
  modified:
    - flow-lang/StandardLibrary/Audio/FileIO.cs
    - flow-lang/StandardLibrary/BuiltInFunctions.cs
    - flow-lang/audio.flow
    - flow-lang.Tests/FlowScriptData.cs

key-decisions:
  - "DX-15 Int + Double overloads added in-place after the existing 1-arg loadWav signature (preserves byte-identical regression — same pattern as 22-01 for arpeggio)"
  - "VarispeedResample uses Math.Round(source.Frames / ratio) for output frame count — at ratio 2.0 this is exactly half (44100 → 22050) and at ratio 1.5 exactly 44100/1.5 = 29400; matches Pitfall 8 ±1 acceptance with zero margin needed in practice"
  - "Identity short-circuits at semitones==0 and ratio==1.0 return the loaded buffer unchanged — avoids wasted resample work AND preserves byte-identity for the no-shift case"
  - "ratio <= 0.0 OR double.IsNaN(ratio) throws ArgumentException per RESEARCH §V5 Input Validation and threat T-22-V5-09 (negative/NaN ratio would yield infinite/NaN frame counts)"
  - "Pure linear interpolation chosen as v1.3 default per CONTEXT 'Resampler choice' D-15; OLA windowing and sinc resamplers explicitly deferred to v1.4"
  - "Negative ratio guard placed AFTER the LoadWavInternal call — file existence check happens first to surface familiar errors; ratio guard is the final precondition before VarispeedResample"

patterns-established:
  - "Pattern: Wave 0 RED stub — NotImplementedException-throwing static method matching the eventual signature lets the test file compile while keeping every assertion RED. Lands in Task 1; replaced with real body in Task 2"
  - "Pattern: identity short-circuit at no-op input — semitones=0 / ratio=1.0 return source buffer directly, avoiding allocation and preserving byte-identical loadWav(path) bytes when the no-shift overload is used"

requirements-completed: [DX-15]

duration: 5min
completed: 2026-05-02
---

# Phase 22 Plan 02: DX-15 Varispeed loadWav Overloads Summary

**`loadWav(path, semitones:Int)` and `loadWav(path, ratio:Double)` extend WAV loading with linear-interpolation varispeed pitch-shift, preserving SampleRate + Channels and byte-identical 1-arg loadWav.**

## Performance

- **Duration:** ~5 min (283 s wall clock)
- **Started:** 2026-05-02T18:52:21Z
- **Completed:** 2026-05-02T18:57:04Z
- **Tasks:** 3 (RED + GREEN + verify)
- **Files modified:** 6 (2 created, 4 modified)

## Accomplishments

- DX-15 closed: `loadWav(path, Int: semitones)` and `loadWav(path, Double: ratio)` both registered alongside the existing 1-arg signature
- `FileIO.VarispeedResample(source, ratio)` helper performs linear-interpolation resample at arbitrary ratio; preserves SampleRate + Channels (only frame count changes)
- Identity short-circuits at semitones=0 and ratio=1.0 — return loaded buffer unchanged
- DoS guard: ratio <= 0.0 or NaN throws ArgumentException (threat T-22-V5-09)
- 12 LoadWavVarispeedFacts GREEN; `tests/test_dx_loadwav_varispeed.flow` exits 0 with sentinel
- Smoke output: src=44100 → +12 semi=22050 (exactly half), ratio 1.5=29400 (exact 44100/1.5), semi 0=44100 (identity)
- ByteIdentical regression gate 6/6 GREEN (Tutorial WAV+MIDI, Showcase WAV+MIDI, Euclidean WAV+MIDI)
- Full test suite 436/436 GREEN — zero regressions (+12 LoadWavVarispeedFacts +1 DX-15 sentinel Theory vs 423 baseline at 22-01 close)

## Task Commits

Each task was committed atomically:

1. **Task 1: Wave 0 RED — Failing LoadWavVarispeedFacts + DX-15 smoke** — `1bf3b90` (test)
   - 12 xUnit Facts: 11 RED (require new overloads / VarispeedResample), 1 GREEN (`SingleArgUnchanged` regression gate — already holds against 1-arg path)
   - `tests/test_dx_loadwav_varispeed.flow` smoke script with `DX-15 varispeed: PASSED` sentinel
   - `FlowScriptData.cs` sentinel entry
   - `FileIO.LoadWavSemitones` / `LoadWavRatio` / `VarispeedResample` stubs (NotImplementedException) so build is green and assertions stay RED
2. **Task 2: Wave 2 GREEN — Implement DX-15 overloads** — `95582e7` (feat)
   - `LoadWavSemitones`: ratio = 2^(semi/12); short-circuit at 0
   - `LoadWavRatio`: short-circuit at 1.0; throws on ratio <= 0.0 or NaN
   - `VarispeedResample`: linear-interpolation core (mirrors existing `Resample` math with arbitrary ratio); `Math.Round(Frames/ratio)` for output frame count
   - `BuiltInFunctions.cs`: `loadWavSemiSig` (String, Int) + `loadWavRatioSig` (String, Double) registrations after existing 1-arg signature
   - `audio.flow`: two new `internal proc loadWav` declarations alongside the existing one
   - All 12 LoadWavVarispeedFacts flipped GREEN
3. **Task 3: Wave 2 — Smoke run + byte-identical regression gate** — `aa5e293` (chore, verification-only empty commit)
   - `dotnet run --project flow-interpreter tests/test_dx_loadwav_varispeed.flow` → exit 0, sentinel printed, exact frame counts: 44100 → 22050 / 29400 / 44100
   - ByteIdentical 6/6 GREEN; full suite 436/436 GREEN

## Files Created/Modified

- `flow-lang.Tests/Unit/Phase22/LoadWavVarispeedFacts.cs` (created) — 12 xUnit Facts pinning DX-15 acceptance behavior; uses direct `FileIO.VarispeedResample` calls for math invariants and `FlowEngineRunner.GetVariable` for engine-eval overload-dispatch verification
- `tests/test_dx_loadwav_varispeed.flow` (created) — Smoke script: synth 1s sine → writeWav → loadWav at +12 semi / 1.5 ratio / 0 semi (identity); prints frame counts and PASSED sentinel
- `flow-lang/StandardLibrary/Audio/FileIO.cs` (modified) — Added `LoadWavSemitones`, `LoadWavRatio`, `VarispeedResample` static methods immediately after the existing 1-arg `LoadWav`. Existing `LoadWav` and `LoadWavInternal` and `Resample` UNTOUCHED (byte-identity invariant)
- `flow-lang/StandardLibrary/BuiltInFunctions.cs` (modified) — Two new `FunctionSignature` registrations after the existing 1-arg `loadWavSignature`: `loadWavSemiSig` (String, Int) → `LoadWavSemitones`, `loadWavRatioSig` (String, Double) → `LoadWavRatio`
- `flow-lang/audio.flow` (modified) — Two new `internal proc loadWav` declarations next to the existing one, with explanatory comments
- `flow-lang.Tests/FlowScriptData.cs` (modified) — `RequiredSentinels` entry for `test_dx_loadwav_varispeed.flow` pinning the `DX-15 varispeed: PASSED` sentinel

## Decisions Made

- **Sibling-overload registration**: The two new overloads register immediately after the existing 1-arg `loadWavSignature` in `BuiltInFunctions.RegisterAudioCore` rather than in a new method. Mirrors 22-01's DX-10 pattern (extend in place — CONTEXT D-08 / Anti-Pattern: do NOT create `loadWavShifted`). Existing 1-arg `loadWav(String)` registration unchanged.
- **`Math.Round(source.Frames / ratio)` for output frame count**: Per Pitfall 8 the acceptance criterion allows ±1 tolerance, but using `Round` (rather than truncating cast) yields exactly half at ratio 2.0 (44100 → 22050) and exactly 44100/1.5 = 29400 at ratio 1.5. Zero margin needed in practice. The existing `Resample` uses `(int)(Frames / ratio)` (truncation) — DX-15 deliberately uses `Round` because the math `2^(semitones/12)` produces irrational ratios where rounding is more musically intuitive.
- **Identity short-circuits at unit inputs**: `semitones==0` and `ratio==1.0` both return the loaded buffer directly without entering `VarispeedResample`. Three benefits: (1) avoids wasted allocation; (2) preserves byte-identical bytes for the no-shift case; (3) makes the function safe to call defensively in user scripts where the shift amount may be a variable.
- **NaN check alongside `<= 0.0` guard**: `double.IsNaN(ratio)` is checked explicitly because `NaN <= 0.0` is `false` in IEEE 754, so a bare `<= 0.0` check would let NaN through. NaN would produce `(int)Math.Round(Frames / NaN) = 0` and `result.SampleRate` would be a 0-frame buffer — surface error early per existing `ClampSample` pattern.
- **Pure linear interpolation per CONTEXT D-15**: Tier-D OLA windowing and sinc-quality resamplers are explicitly out of scope for v1.3 per CONTEXT 'Resampler choice'. The acceptance criterion (sample count exactly halves at +12 semi) is met cleanly by linear interpolation.

## Deviations from Plan

**Total deviations:** 2 minor (1 plan-text correction, 1 docs/counting echo from 22-01)
**Impact on plan:** None — verification still GREEN; both deviations are inherited or surface-level.

### 1. [Rule 1 - Plan-text bug] Smoke script used non-existent `(sine 440.0 1.0 44100)` builtin

- **Found during:** Task 1 (smoke script authoring)
- **Issue:** Plan's `<action>` block for Task 1 specified `Buffer src = (sine 440.0 1.0 44100)` for the smoke script. No such builtin exists — `sine` is a generator that fills an existing buffer (`generateSine`). The `(amplitude, freq, duration, sampleRate)` shape doesn't match any registered signature.
- **Fix:** Used `(createSineTone 1.0 440.0 0.8)` which is the canonical 3-arg sine-buffer generator (duration, frequency, amplitude → 44100 Hz stereo Buffer), matching the existing `tests/test_wav_loading.flow` convention.
- **Files modified:** `tests/test_dx_loadwav_varispeed.flow`
- **Verification:** Smoke script runs to completion, prints all sentinels and exits 0; frame counts are exactly as predicted (44100 → 22050 / 29400 / 44100). Plan's `<read_first>` already directed reader to `tests/test_wav_loading.flow` so the correction follows that file's conventions verbatim.
- **Committed in:** 1bf3b90 (Task 1 commit)

### 2. [Documentation] Plan referenced "ByteIdentical 19/19" but actual count is 6

- **Found during:** Task 3 (verification gate)
- **Issue:** Plan's `<verification>` and `<acceptance_criteria>` blocks reference `ByteIdenticalTutorialTests + ByteIdenticalShowcaseTests stay 19/19 GREEN`. The actual byte-identical regression gate consists of 6 tests across 3 classes: `ByteIdenticalTutorialTests` (2: WAV + MIDI), `ByteIdenticalShowcaseTests` (2: WAV + MIDI), `EuclideanByteIdenticalTests` (2: WAV + MIDI). Same documentation lag observed and corrected in 22-01.
- **Fix:** Documented actual count (6/6) in Task 3 commit message and this summary. No code change required.
- **Files modified:** none (commit message + this SUMMARY only)
- **Verification:** `dotnet test --filter ByteIdentical` enumerates and runs 6 tests; all 6 GREEN.

## Issues Encountered

- **`Value.Int` stores CLR `int`, not `long`**: Initial draft of engine-eval Facts (`OverloadDispatch_*`) cast `runner.GetVariable("frames").As<long>()` — would have failed at runtime with `InvalidCastException`. Caught pre-commit by reading `Value.cs:24` (`Value.Int(int value) => new(value, IntType.Instance)`). Corrected to `.As<int>()` before the Task 1 commit.
- **`tests/` directory is gitignored**: First `git add tests/test_dx_loadwav_varispeed.flow` would fail because `.gitignore` line 7 (`tests/`) blocks the path. Resolved with `git add -f` — same convention as 22-01 (`test_dx_arpeggio.flow`).

## Next Phase Readiness

- DX-15 closes the 6th of 6 Phase 22 DX features; 22-03 (DX-11 voicings), 22-04 (DX-12 delay sync), 22-05 (DX-13 quantize), 22-06 (DX-14 legato/portamento) and 22-07 (closure) remain. None depend on this plan's outputs (per Phase 22 design — features are independently shippable).
- `VarispeedResample(buffer, ratio)` helper is reusable for any future varispeed transform (e.g., a sequence-level `pitchShiftBuffer(buf, semitones)` transform if composers want post-load pitch shifting). The math is decoupled from file I/O.
- The Wave 0 RED stub pattern (NotImplementedException → real body in Wave 2) is now established for any future plan where the test file references symbols that the implementation will create.
- Byte-identical regression gate proven robust under varispeed extension — confirms the 22-01 sibling-overload pattern is safe to reuse for any future Phase 22 audio-pipeline plan touching FileIO.

## Self-Check: PASSED

Files verified:
- FOUND: `flow-lang.Tests/Unit/Phase22/LoadWavVarispeedFacts.cs`
- FOUND: `tests/test_dx_loadwav_varispeed.flow`
- FOUND: `.planning/phases/22-tier-b-c-composer-dx-bundle/22-02-SUMMARY.md`

Commits verified:
- FOUND: `1bf3b90` (Task 1 RED)
- FOUND: `95582e7` (Task 2 GREEN)
- FOUND: `aa5e293` (Task 3 verification)

---
*Phase: 22-tier-b-c-composer-dx-bundle*
*Completed: 2026-05-02*
