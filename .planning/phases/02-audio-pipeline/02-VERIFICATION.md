---
phase: 02-audio-pipeline
verified: 2026-04-02T01:02:15Z
status: gaps_found
score: 3/4 must-haves verified
gaps:
  - truth: "pan context block sets panning for all voices rendered within its scope"
    status: failed
    reason: "MusicalContext.Pan is set by the interpreter when a pan { ... } block executes, but neither BarRenderer.RenderBarToVoices nor SequenceRenderer.RenderSequenceToVoices read the musical context; Voice.Pan always defaults to 0.0 regardless of any pan context block. The render pipeline has no parameter for musical context, so context.Pan can never propagate to voice objects."
    artifacts:
      - path: "flow-lang/StandardLibrary/Audio/BarRenderer.cs"
        issue: "RenderBarToVoices creates Voice objects with Pan = 0.0 (default). No musical context parameter exists; MusicalContext.Pan is never read."
      - path: "flow-lang/StandardLibrary/Audio/SequenceRenderer.cs"
        issue: "RenderSequenceToVoices passes no musical context to BarRenderer. Even if MusicalContext.Pan is active in the interpreter, it is lost before voice creation."
      - path: "flow-lang/StandardLibrary/BuiltInFunctions.cs"
        issue: "renderSequenceToVoices registration (line 794-803) does not read the execution context's musical context, so pan context from enclosing pan { } block is ignored."
    missing:
      - "BarRenderer.RenderBarToVoices must accept an optional MusicalContext parameter and apply context.Pan to each created Voice object"
      - "SequenceRenderer.RenderSequenceToVoices must thread the musical context through to BarRenderer calls"
      - "BuiltInFunctions.cs renderSequenceToVoices registration must read the current musical context from the execution context and pass it to SequenceRenderer"
      - "test_panning.flow must verify voice.Pan is actually set (e.g., render a sequence inside pan 0.5 { } and check the resulting voice has Pan == 0.5)"
---

# Phase 2: Audio Pipeline Verification Report

**Phase Goal:** Users can load audio samples, position sounds in the stereo field, apply sidechain compression, and play polyphonic arrangements without voice clipping
**Verified:** 2026-04-02T01:02:15Z
**Status:** gaps_found
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | User can call `loadWav("kick.wav")` and use the returned buffer in compositions (mix, effects, playback) | VERIFIED | `FileIO.LoadWavInternal` + `LoadWav` wrapper present; `loadWav` registered in `BuiltInFunctions.cs` line 399; round-trip test passes printing "WAV loading test passed" |
| 2 | User can call `pan(buffer, -1.0)` through `pan(buffer, 1.0)` to position a voice left-to-right in stereo output | VERIFIED | `Panner.Apply` with constant-power cos/sin law; `PanningFunctions.Register` wired; test prints "All stereo: true" and "Panning test passed" |
| 3 | User can apply sidechain compression to a bass buffer triggered by a kick buffer, producing the characteristic pumping effect | VERIFIED | `SidechainCompressor.Apply` with envelope follower; both 4-arg and 6-arg overloads registered in `EffectsFunctions.cs`; test prints "Sidechain test passed" with correct frame count preservation |
| 4 | User can render a Song with 8+ simultaneous notes and hear clean polyphony with configurable voice limits and voice stealing | VERIFIED | `VoiceAllocator.Allocate` integrated into `SequenceRenderer.RenderSequenceToVoices`; `setMaxVoices` registered; test verifies 8-note chord is limited to 4 voices when `setMaxVoices 4` |

**Score:** 3/4 truths verified (Truth 2 is partially verified — `pan()` function works, but the `pan` context block does NOT wire through to voice panning; see Gaps)

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `flow-lang/StandardLibrary/Audio/FileIO.cs` | LoadWavInternal method — inverse of ExportWavInternal | VERIFIED | Contains `LoadWavInternal`, `LoadWav`, `ReadSamples`, `Resample` |
| `flow-lang/StandardLibrary/Audio/DSP/SidechainCompressor.cs` | Sidechain compression DSP with separate trigger/source buffers | VERIFIED | `public static class SidechainCompressor` with full envelope-follower `Apply` method |
| `flow-lang/StandardLibrary/Audio/EffectsFunctions.cs` | sidechain function registration | VERIFIED | `RegisterSidechain` method registers both 4-arg and 6-arg overloads; calls `SidechainCompressor.Apply` |
| `flow-lang/StandardLibrary/Audio/DSP/Panner.cs` | Constant-power stereo panning DSP | VERIFIED | `public static class Panner` with cos/sin pan law, stereo output always |
| `flow-lang/StandardLibrary/Audio/PanningFunctions.cs` | pan function registration | VERIFIED | `RegisterPanning` registers `pan(Buffer, Double)` → `Panner.Apply` |
| `flow-lang/StandardLibrary/Audio/SongRenderer.cs` | Voice.Pan wired into MixVoicesToStereoBuffer | VERIFIED | Line 97: `float panAngle = (float)((voice.Pan + 1.0) * 0.25 * Math.PI)` |
| `flow-lang/StandardLibrary/Audio/VoiceAllocator.cs` | Polyphonic voice pool management with steal-quietest policy | VERIFIED | `public static class VoiceAllocator` with `MaxVoices = 32`, `Allocate` sorts by peak amplitude |
| `tests/test_wav_loading.flow` | Integration test for WAV loading | VERIFIED | Runs and prints "WAV loading test passed" |
| `tests/test_sidechain.flow` | Integration test for sidechain compression | VERIFIED | Runs and prints "Sidechain test passed" with correct frame counts |
| `tests/test_panning.flow` | Integration test for panning | PARTIAL | Runs and prints "Panning test passed" — but pan context block test only verifies execution, NOT that `voice.Pan` is set |
| `tests/test_voice_allocation.flow` | Integration test for voice allocation | VERIFIED | Runs and prints "Dense chord voices (max 4): 4" — voice limit enforced |

### Key Link Verification

| From | To | Via | Status | Details |
|------|-----|-----|--------|---------|
| `EffectsFunctions.cs` | `DSP/SidechainCompressor.cs` | Lambda calls `SidechainCompressor.Apply` | WIRED | Line 292: `var result = SidechainCompressor.Apply(source, trigger, threshold, ratio)` |
| `BuiltInFunctions.cs` | `Audio/FileIO.cs` | loadWav registration calls `FileIO.LoadWav` | WIRED | Line 399: `registry.Register("loadWav", loadWavSignature, Audio.FileIO.LoadWav)` |
| `PanningFunctions.cs` | `DSP/Panner.cs` | Registration lambda calls `Panner.Apply` | WIRED | Line 39: `var result = Panner.Apply(buffer, panValue)` |
| `SongRenderer.cs` | `Voice.Pan` | MixVoicesToStereoBuffer reads `voice.Pan` for constant-power panning | WIRED | Line 97 reads `voice.Pan` in panning calculation |
| `Parser.cs` | `Interpreter.cs` | Pan musical context type parsed then executed | WIRED | `TokenType.Pan` → `MusicalContextType.Pan` → `Interpreter.ExecuteMusicalContext` case |
| `BarRenderer.cs` | `MusicalContext.Pan` | Voice.Pan set from MusicalContext.Pan during voice creation | NOT WIRED | `BarRenderer.RenderBarToVoices` has no musical context parameter; `Voice.Pan` is always 0.0 |
| `SequenceRenderer.cs` | `MusicalContext.Pan` | Musical context threaded through sequence render to voices | NOT WIRED | `RenderSequenceToVoices` passes no context to `BarRenderer`; pan value never reaches voices |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|--------------|--------|-------------------|--------|
| `FileIO.LoadWav` | `AudioBuffer` from file | `BinaryReader` over WAV file bytes | Yes — parses RIFF/fmt/data chunks | FLOWING |
| `SidechainCompressor.Apply` | `result` buffer | Envelope follower on trigger buffer | Yes — real DSP computation | FLOWING |
| `Panner.Apply` | `result` buffer | cos/sin of pan angle applied to mono sum | Yes — real DSP computation | FLOWING |
| `VoiceAllocator.Allocate` | `kept` list | Peak amplitude scan of voice buffers | Yes — real amplitude measurement | FLOWING |
| `pan { } context → voice.Pan` | `voice.Pan` | `MusicalContext.Pan` from interpreter frame | No — MusicalContext.Pan never read by BarRenderer | DISCONNECTED |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| loadWav round-trip | `dotnet run --project flow-interpreter tests/test_wav_loading.flow` | "WAV loading test passed", frame counts match | PASS |
| sidechain output length = source length | `dotnet run --project flow-interpreter tests/test_sidechain.flow` | "Sidechain test passed", 44100 frames in all cases | PASS |
| pan() produces stereo output | `dotnet run --project flow-interpreter tests/test_panning.flow` | "All stereo: true", "Panning test passed" | PASS |
| voice count limited to 4 with setMaxVoices 4 | `dotnet run --project flow-interpreter tests/test_voice_allocation.flow` | "Dense chord voices (max 4): 4" | PASS |
| dotnet build clean | `dotnet build --nologo --verbosity quiet` | 0 errors, 4 warnings (pre-existing) | PASS |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| AUDIO-01 | 02-01-PLAN.md | User can load WAV files as audio buffers via `loadWav` function | SATISFIED | `FileIO.LoadWav` registered, handles 16/24/32-bit PCM, resamples to 44100 Hz, test passes |
| AUDIO-02 | 02-02-PLAN.md | User can control stereo panning per voice/buffer with `pan` function | PARTIAL | `pan(buffer, value)` function works correctly. `Voice.Pan` is used in SongRenderer. However, `pan` context block does not propagate pan value to voice objects created within it. |
| AUDIO-03 | 02-01-PLAN.md | User can apply sidechain compression driven by a trigger buffer | SATISFIED | `SidechainCompressor` with trigger-driven envelope follower, both overloads, composable via `->`, test passes |
| AUDIO-04 | 02-03-PLAN.md | User can allocate polyphonic voices with configurable voice limits and stealing | SATISFIED | `VoiceAllocator` with steal-quietest policy, `setMaxVoices` built-in, integrated into `SequenceRenderer`, test passes |

**Note on AUDIO-02:** The `pan(buffer, value)` built-in function fully satisfies the REQUIREMENTS.md text ("User can control stereo panning per voice/buffer with `pan` function"). The gap is in the more advanced PLAN 02 truth that the `pan` context block propagates to voice objects — which was a plan-level design goal beyond the literal requirement text.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `tests/test_panning.flow` | 33-45 | Pan context block test only checks execution, not effect | Warning | Test does not assert that `voice.Pan` is set; the gap is invisible to the test suite |

No placeholder returns, empty implementations, or TODOs found in production code files.

### Human Verification Required

#### 1. Audio Quality of Panning

**Test:** Call `pan(tone, -1.0)`, export with `exportWav`, open in an audio editor and verify sound is in left channel only.
**Expected:** Left channel has full signal; right channel is silent.
**Why human:** Can't measure output channel content programmatically without a WAV-reading test.

#### 2. Sidechain Pumping Effect

**Test:** Create a 1-second bass tone. Create a 0.1-second kick at the start. Apply `sidechain(bass, kick, -12.0, 4.0)`. Export and listen.
**Expected:** Bass noticeably ducks at the start when kick triggers, then swells back up — the classic EDM pumping effect.
**Why human:** Verifying the perceptual quality of the effect requires listening.

#### 3. Pan Context Block Effect on Song Rendering

**Test:** Render a song section inside `pan 0.7 { ... }`, mix to stereo, export and inspect that audio is panned right.
**Expected:** Audio should be panned 70% right in the stereo field.
**Why human:** As documented in the gaps section, this currently does NOT work (voice.Pan is never set from context). This test would confirm the gap is user-visible.

### Gaps Summary

**One confirmed gap blocking the `pan` context block truth:**

The `pan { ... }` musical context block is syntactically complete (lexer, parser, interpreter all handle it) and `MusicalContext.Pan` is correctly set and scoped. However, the value never reaches the voices. The render pipeline — `BarRenderer.RenderBarToVoices` → `Voice` constructor — has no path from the interpreter's execution context to the voice's `Pan` property.

The fix requires threading the musical context (or at minimum the pan value) through the render pipeline:

1. `BuiltInFunctions.cs` `renderSequenceToVoices` registration must capture the current `MusicalContext` from the execution context at call time
2. `SequenceRenderer.RenderSequenceToVoices` must accept an optional `MusicalContext?` parameter
3. `BarRenderer.RenderBarToVoices` must accept an optional `MusicalContext?` and set `voice.Pan = context.Pan.Value` on created voices when the context has a pan value

The `pan(buffer, value)` built-in function is unaffected and works correctly. `SongRenderer.MixVoicesToStereoBuffer` already reads `voice.Pan` and applies it correctly — the gap is only in the context-to-voice data flow for the `pan { }` block.

The other three requirements (AUDIO-01, AUDIO-03, AUDIO-04) are fully satisfied. The build is clean and all tests pass.

---

_Verified: 2026-04-02T01:02:15Z_
_Verifier: Claude (gsd-verifier)_
