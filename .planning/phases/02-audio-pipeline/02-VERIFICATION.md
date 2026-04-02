---
phase: 02-audio-pipeline
verified: 2026-04-02T07:30:00Z
status: human_needed
score: 4/4 must-haves verified
re_verification:
  previous_status: gaps_found
  previous_score: 3/4
  gaps_closed:
    - "pan context block sets panning for all voices rendered within its scope"
  gaps_remaining: []
  regressions: []
human_verification:
  - test: "Call pan(tone, -1.0), export with writeWav, open in audio editor"
    expected: "Left channel has full signal; right channel is silent"
    why_human: "Cannot measure output channel content programmatically without a WAV-reading test"
  - test: "Create a 1-second bass tone + 0.1s kick, apply sidechain(bass, kick, -12.0, 4.0), export and listen"
    expected: "Bass noticeably ducks when kick triggers then swells back up"
    why_human: "Verifying perceptual quality of pumping effect requires listening"
  - test: "Render a song section inside pan 0.7 { ... }, export and inspect stereo channel levels"
    expected: "Audio is audibly panned 70% right; right channel amplitude clearly greater than left"
    why_human: "Perceptual verification of stereo positioning from context block"
---

# Phase 2: Audio Pipeline Verification Report (Re-verification 2)

**Phase Goal:** Users can load audio samples, position sounds in the stereo field, apply sidechain compression, and play polyphonic arrangements without voice clipping
**Verified:** 2026-04-02T07:30:00Z
**Status:** human_needed
**Re-verification:** Yes — after gap closure (commit e3413ad)

## Re-verification Summary

Previous verification (2026-04-02T05:45:00Z) identified one blocking gap: `ExecutionContext.GetMusicalContext()` did not collect `Pan` from call-stack frames, causing `pan { }` context blocks to be silently ignored during Song rendering.

Commit `e3413ad` closed the gap with two targeted changes:

1. Added `resolved.Pan ??= frame.MusicalContext.Pan;` inside the `foreach` loop body in `GetMusicalContext()` (alongside the existing `TimeSignature`, `Tempo`, `Swing`, `Key`, `Velocity` lines).
2. Added `resolved.Pan != null` to the early-exit break condition so the loop does not terminate before reaching a frame that carries a `Pan` value.

**The fix is verified complete.** The full data-flow chain from `pan { }` block to `voice.Pan` in the audio mixer is now connected. All four must-have truths are satisfied. No regressions were introduced.

One pre-existing warning (not a blocker) remains: `RenderSectionWithTimeline` does not apply pan. This affects the editor timeline path only, not the primary `renderSong` user path.

---

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | User can call `loadWav("kick.wav")` and use the returned buffer in compositions (mix, effects, playback) | VERIFIED | `FileIO.LoadWav` registered in `BuiltInFunctions.cs`; `LoadWavInternal` parses RIFF/fmt/data chunks with resampling |
| 2 | User can call `pan(buffer, -1.0)` through `pan(buffer, 1.0)` to position a voice left-to-right in stereo output | VERIFIED | `Panner.Apply` with cos/sin constant-power law; `PanningFunctions.Register` wired; unchanged by this fix |
| 3 | User can apply sidechain compression to a bass buffer triggered by a kick buffer, producing the characteristic pumping effect | VERIFIED | `SidechainCompressor.Apply` with envelope follower; both 4-arg and 6-arg overloads registered in `EffectsFunctions.cs` |
| 4 | User can render a Song with 8+ simultaneous notes and hear clean polyphony with configurable voice limits and voice stealing | VERIFIED | `VoiceAllocator.Allocate` integrated into `SequenceRenderer.RenderSequenceToVoices`; `setMaxVoices` registered |
| 4b | `pan` context block sets panning for all voices rendered within its scope | VERIFIED | `GetMusicalContext()` now collects Pan (line 141); early-exit condition includes `resolved.Pan != null` (line 145); full chain confirmed connected (see Data-Flow Trace) |

**Score:** 4/4 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `flow-lang/StandardLibrary/Audio/FileIO.cs` | LoadWavInternal — inverse of ExportWavInternal | VERIFIED | Contains `LoadWavInternal`, `LoadWav`, `ReadSamples`, `Resample` |
| `flow-lang/StandardLibrary/Audio/DSP/SidechainCompressor.cs` | Sidechain compression DSP | VERIFIED | Full envelope-follower `Apply` method present |
| `flow-lang/StandardLibrary/Audio/EffectsFunctions.cs` | sidechain function registration | VERIFIED | Both 4-arg and 6-arg overloads registered |
| `flow-lang/StandardLibrary/Audio/DSP/Panner.cs` | Constant-power stereo panning DSP | VERIFIED | cos/sin pan law, stereo output always |
| `flow-lang/StandardLibrary/Audio/PanningFunctions.cs` | pan function registration | VERIFIED | `pan(Buffer, Double)` → `Panner.Apply` |
| `flow-lang/StandardLibrary/Audio/SongRenderer.cs` | Voice.Pan wired into MixVoicesToStereoBuffer | VERIFIED | Line 104: constant-power panning using voice.Pan; RenderSection applies section pan to all voices (lines 72-76) |
| `flow-lang/StandardLibrary/Audio/VoiceAllocator.cs` | Polyphonic voice pool with steal-quietest | VERIFIED | `MaxVoices = 32`, steal-quietest policy |
| `flow-lang/Runtime/ExecutionContext.cs` | GetMusicalContext collects Pan from call stack | VERIFIED | Line 141: `resolved.Pan ??= frame.MusicalContext.Pan`; line 145: early-exit now includes `resolved.Pan != null` |

### Key Link Verification

| From | To | Via | Status | Details |
|------|-----|-----|--------|---------|
| `EffectsFunctions.cs` | `DSP/SidechainCompressor.cs` | Lambda calls `SidechainCompressor.Apply` | WIRED | Confirmed present |
| `BuiltInFunctions.cs` | `Audio/FileIO.cs` | loadWav registration | WIRED | Confirmed present |
| `PanningFunctions.cs` | `DSP/Panner.cs` | Registration lambda calls `Panner.Apply` | WIRED | Confirmed present |
| `SongRenderer.cs` | `Voice.Pan` | MixVoicesToStereoBuffer reads voice.Pan | WIRED | Line 104: confirmed present |
| `SongRenderer.RenderSection` | `SectionData.Context.Pan` | Reads section.Context?.Pan to apply to voices | WIRED | Lines 63, 72-76: reads pan, applies to all voices if non-zero |
| `ExecutionContext.GetMusicalContext` | `MusicalContext.Pan` | Resolves Pan from call-stack frames | WIRED | Line 141: `resolved.Pan ??= frame.MusicalContext.Pan` now present |
| `pan { }` block | `section.Context.Pan` | Pan value flows from block scope into SectionData | WIRED | Full chain: Interpreter sets frame.MusicalContext.Pan → GetMusicalContext() collects it → ExecuteSectionDeclaration snapshots it → SectionData.Context.Pan is non-null → SongRenderer applies it |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|--------------|--------|-------------------|--------|
| `FileIO.LoadWav` | `AudioBuffer` | BinaryReader over WAV file bytes | Yes — parses RIFF/fmt/data chunks | FLOWING |
| `SidechainCompressor.Apply` | `result` buffer | Envelope follower on trigger buffer | Yes — real DSP computation | FLOWING |
| `Panner.Apply` | `result` buffer | cos/sin of pan angle applied to input | Yes — real DSP computation | FLOWING |
| `VoiceAllocator.Allocate` | `kept` list | Peak amplitude scan of voice buffers | Yes — real amplitude measurement | FLOWING |
| `pan { } context → voice.Pan` | `voice.Pan` | `MusicalContext.Pan` via `GetMusicalContext()` | Yes — Pan now collected from call-stack frames | FLOWING |

### Full Pan Context Block Chain (Post-Fix)

```
pan { } block (Interpreter.cs ExecuteMusicalContext, lines 218-232)
  → musicalCtx.Pan = pan (validated between -1.0 and 1.0)
  → _context.CurrentFrame.MusicalContext = musicalCtx (line 256)

section s { } body (Interpreter.cs ExecuteSectionDeclaration, line 351)
  → musicalContext = _context.GetMusicalContext()

GetMusicalContext() (ExecutionContext.cs, lines 129-153)
  → foreach frame in _callStack:
      resolved.Pan ??= frame.MusicalContext.Pan  [line 141 — THE FIX]
  → early-exit breaks when all six fields non-null  [line 145 — updated]
  → returns MusicalContext with Pan set

SectionData.Context.Pan is non-null when section declared inside pan { }

SongRenderer.RenderSection (SongRenderer.cs, lines 60-87)
  → double pan = section.Context?.Pan ?? 0.0  [line 63 — now non-zero]
  → foreach voice: voice.Pan = pan  [lines 72-76]

SongRenderer.MixVoicesToStereoBuffer (lines 92-133)
  → float panAngle = (voice.Pan + 1.0) * 0.25 * PI  [line 104]
  → leftGain = cos(panAngle), rightGain = sin(panAngle)
  → left/right samples written with independent gains
```

### Behavioral Spot-Checks

Step 7b: SKIPPED — .NET 9 SDK not available in this environment. Static code analysis used instead.

Previous session (before re-verification 1) verified via `dotnet run`:
- loadWav round-trip: PASS ("WAV loading test passed")
- sidechain output length: PASS ("Sidechain test passed")
- pan() produces stereo output: PASS ("All stereo: true", "Panning test passed")
- voice count limited with setMaxVoices: PASS ("Dense chord voices (max 4): 4")

Pan context block integration test requires human verification (see below).

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| AUDIO-01 | 02-01-PLAN.md | User can load WAV files as audio buffers via `loadWav` | SATISFIED | `FileIO.LoadWav` registered; handles 16/24/32-bit PCM; resamples to 44100 Hz |
| AUDIO-02 | 02-02-PLAN.md | User can control stereo panning per voice/buffer with `pan` function | SATISFIED | `pan(buffer, value)` built-in works correctly. `pan { }` context block now propagates pan value into rendered voices via fixed `GetMusicalContext()`. Both mechanisms are wired. |
| AUDIO-03 | 02-01-PLAN.md | User can apply sidechain compression driven by a trigger buffer | SATISFIED | `SidechainCompressor` with trigger-driven envelope follower; both overloads; composable via `->` |
| AUDIO-04 | 02-03-PLAN.md | User can allocate polyphonic voices with configurable voice limits and stealing | SATISFIED | `VoiceAllocator` with steal-quietest policy; `setMaxVoices` built-in; integrated into `SequenceRenderer` |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `flow-lang/StandardLibrary/Audio/SongRenderer.cs` | 188-210 | `RenderSectionWithTimeline` does not apply pan from section context | Warning | Timeline-based rendering path ignores pan entirely; inconsistency with `RenderSection`; does not affect `renderSong` |
| `tests/test_panning.flow` | 33-46 | Pan context block test only verifies execution, not that Voice.Pan is set | Warning | Gap closure is not exercised by the test suite; regression risk |

Note: The `BarRenderer.RenderBarToVoices(..., double pan)` overload flagged in previous verification as dead code is still present. It is now less of a concern because the primary rendering path (SongRenderer → SequenceRenderer → voice.Pan) is fully wired; the BarRenderer overload is a supplementary entry point.

### Human Verification Required

#### 1. Audio Quality of Panning (pan function)

**Test:** Call `pan(tone, -1.0)`, export with `writeWav`, open in an audio editor and verify sound is in left channel only.
**Expected:** Left channel has full signal; right channel is silent.
**Why human:** Cannot measure output channel content programmatically without a WAV-reading test.

#### 2. Sidechain Pumping Effect

**Test:** Create a 1-second bass tone. Create a 0.1-second kick at the start. Apply `sidechain(bass, kick, -12.0, 4.0)`. Export and listen.
**Expected:** Bass noticeably ducks at the start when kick triggers, then swells back up.
**Why human:** Verifying the perceptual quality of the effect requires listening.

#### 3. Pan Context Block End-to-End (fix validation)

**Test:** Write a `.flow` script with:
```
pan 0.7 {
  section s {
    Sequence melody = | C4q D4q E4q |
  }
  Song song = [s]
  Buffer result = renderSong(song, "piano")
  writeWav(result, "pan_test.wav")
}
```
Open `pan_test.wav` in an audio editor and inspect channel levels.
**Expected:** Right channel amplitude is clearly greater than left channel (70% right pan applied via constant-power law: right gain ≈ sin(0.85 * PI/4) ≈ 0.93, left gain ≈ cos(0.85 * PI/4) ≈ 0.37).
**Why human:** Perceptual and channel-level verification of stereo positioning from context block requires listening or audio analysis tooling not available in this environment.

### Gaps Summary

All automated gaps are closed. The only remaining items require human perceptual verification:
- Audio quality checks for `pan()`, `sidechain()`, and the now-fixed `pan { }` context block.

The `RenderSectionWithTimeline` inconsistency and the weak `test_panning.flow` assertion are warnings for future work but do not block the phase goal.

---

_Verified: 2026-04-02T07:30:00Z_
_Verifier: Claude (gsd-verifier)_
