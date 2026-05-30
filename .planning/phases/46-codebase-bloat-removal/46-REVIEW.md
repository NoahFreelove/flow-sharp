---
phase: 46-codebase-bloat-removal
reviewed: 2026-05-30T00:00:00Z
depth: deep
files_reviewed: 31
files_reviewed_list:
  - flow-lang/Audio/TimelineMap.cs
  - flow-lang/StandardLibrary/Audio/SongRenderer.cs
  - flow-lang/StandardLibrary/Audio/BarRenderer.cs
  - flow-lang/StandardLibrary/Audio/SequenceRenderer.cs
  - flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs
  - flow-lang/audio.flow
  - flow-lang/Audio/PulseAudioSimpleBackend.cs
  - flow-lang/StandardLibrary/Audio/PlaybackFunctions.cs
  - flow-lang/StandardLibrary/Audio/FileIO.cs
  - flow-lang/StandardLibrary/BuiltInFunctions.cs
  - flow-lang/StandardLibrary/BuiltInDocs.cs
  - flow-lang/Runtime/PrngRegistry.cs
  - flow-lang/Runtime/ExecutionContext.cs
  - flow-lang.Tests/Helpers/Phase37Fixtures.cs
  - flow-lang/test.flow
  - flow-lang/StandardLibrary/Audio/Timeline.cs
  - flow-lang/StandardLibrary/Audio/Track.cs
  - flow-lang/StandardLibrary/Bars.cs
  - flow-lang/bars.flow
  - flow-lang/composition.flow
  - examples/showcase.flow
  - flow-lang.Tests/Unit/Phase46/NoteSynthesizerByteGuardTests.cs
  - flow-lang.Tests/Unit/Phase46/ProgressionDslTests.cs
  - tests/demo_feature_showcase.flow
  - tests/test_full_song.flow
  - tests/test_section_bare_expr.flow
  - tests/test_section_gain_bare_expr.flow
  - tests/test_test_library.flow
  - tests/test_wav_loading.flow
  - tests/test_writewav.flow
  - flow-lang.Tests/FlowScriptData.cs
findings:
  critical: 0
  warning: 1
  info: 2
  total: 3
status: issues_found
---

# Phase 46: Code Review Report

**Reviewed:** 2026-05-30
**Depth:** deep
**Files Reviewed:** 31
**Status:** issues_found

## Summary

Phase 46 is a pure removal/redirect pass. All four key correctness questions from the prompt are answered:

1. **Dangling references to removed symbols?** None in production code. `TimelineMap`, `exportWav`/`ExportWav`, and the legacy `test.flow` procs leave no unresolved references. The build is clean (0 errors). One cosmetic artifact remains: three renderer files retain a `using FlowLang.Audio;` import that was only consumed by the now-deleted `TimelineMap` — see WR-01.

2. **D-03 fallback byte-identical?** Yes. All four synth classes (Sine/Saw/Square/Triangle) keep their inline oscillator loops (absolute-time formula `t = i/sampleRate`) and redirect only `BeatsToSeconds` + `CreateSilence` to `SynthUtils`. The `NoteSynthesizerByteGuardTests` oracle exactly mirrors the inline arithmetic element-for-element. No rendered sample is touched.

3. **`exportWav` → `writeWav` arg order correct at every callsite?** Yes. All four migrated test scripts correctly flip from `(exportWav buf path)` to `(writeWav path buf)`. `FlowScriptData.cs` drops the old sentinel; the new sentinel matches the updated script output. No inversion survives.

4. **D-16 notes accidentally add deprecation attribute or runtime advisory?** No. Grepping `[Obsolete]`, `WarnOnce`, `Console.Error.*legacy`, and `stderr` across all five D-16 targets (`Timeline.cs`, `Track.cs`, `Bars.cs`, `bars.flow`, `composition.flow`) finds nothing. Only XML-doc comments were added.

Three minor findings below (one warning, two info).

## Warnings

### WR-01: Stale `using FlowLang.Audio` imports in three renderer files

**Files:**
- `flow-lang/StandardLibrary/Audio/SequenceRenderer.cs:1`
- `flow-lang/StandardLibrary/Audio/BarRenderer.cs:3`
- `flow-lang/StandardLibrary/Audio/SongRenderer.cs:1`

**Issue:** `TimelineMap` (deleted by D-02) was the sole type consumed from the `FlowLang.Audio` namespace in all three renderer files. After deletion, `using FlowLang.Audio;` resolves to nothing used in each file. The C# compiler accepts stale `using` directives without error, so the build stays clean, but the imports now misrepresent the dependency surface. A future contributor adding a type to `FlowLang.Audio` could mistakenly infer the renderer already depends on that namespace.

**Fix:** Remove the three stale directives:

```csharp
// SequenceRenderer.cs line 1 — DELETE:
using FlowLang.Audio;

// BarRenderer.cs line 3 — DELETE:
using FlowLang.Audio;

// SongRenderer.cs line 1 — DELETE:
using FlowLang.Audio;
```

These are the only lines in each file that reference `FlowLang.Audio`; no other types from that namespace are used.

## Info

### IN-01: `ExportWavInternal` private method retains export-legacy name

**File:** `flow-lang/StandardLibrary/Audio/FileIO.cs:30`

**Issue:** The core WAV-write implementation is a private method named `ExportWavInternal`. After D-06 removed the `ExportWav`/`ExportWavWithBitDepth` public API, this method is now called exclusively by `WriteWav` and `WriteWavWithBitDepth`. The name is a historical artifact of the old `exportWav` surface. It is private and has no external contract, so this is a naming-clarity issue only.

**Fix:** Rename to `WriteWavInternal` (or `WriteWavCore`) to match the surviving public API. Update the three call sites in the same file (`line 251`, `line 263`).

### IN-02: `FEATURES.md` still advertises removed `exportWav` alias

**File:** `FEATURES.md:245`

**Issue:** The feature table entry reads:

```
| WAV export (`writeWav` / `exportWav`) | Fully | 16 / 24 / 32-bit PCM; ... |
```

`exportWav` was removed by D-06. External readers consulting this file see a documented function that no longer exists.

**Fix:** Update the entry to reference only `writeWav`:

```
| WAV export (`writeWav`) | Fully | 16 / 24 / 32-bit PCM; sample rate from buffer; auto-create parent directory |
```

---

## Correctness confirmations (no findings)

The following were explicitly verified and found sound:

- **TimelineMap (D-02):** Deleted cleanly. `RenderSongWithTimeline`, `RenderSectionWithTimeline`, and the two `RenderSequenceToVoices`/`RenderBarAtBeat` timeline-overloads are gone. The primary (non-timeline) render paths in all three renderer files are byte-identical to pre-removal. No remaining call site anywhere in the production tree, test tree, or interpreter.

- **NoteSynthesizer D-03 fallback (oscillator math):** The four synth classes keep their inline `t = i/(double)sampleRate` absolute-time formula verbatim. `SynthUtils.BeatsToSeconds` and `SynthUtils.CreateSilence` are semantically equivalent to the old inline arithmetic (`(beats/bpm)*60.0` and `(int)(seconds*sampleRate)` respectively) — the redirected helpers use the same expression, so `numSamples` is computed identically in all four synths. The byte-guard test (`NoteSynthesizerByteGuardTests`) reconstructs the oracle from the same inline formula and asserts element-wise bit-identity.

- **exportWav → writeWav arg-order migration:** All five callers correctly inverted the argument order. `exportWav(buf, path)` → `writeWav(path, buf)` confirmed in `test_section_bare_expr.flow`, `test_section_gain_bare_expr.flow`, `test_wav_loading.flow`, `demo_feature_showcase.flow`, and `test_writewav.flow`. `FlowScriptData.cs` drops the `"PASS: exportWav(Buffer, String) backwards compat succeeded"` sentinel and pins only the `writeWav` sentinel.

- **audio.flow D-05 + D-06 removals:** The dead `internal proc createSineTone` (two overloads) and both `internal proc exportWav` declarations are gone. The composer-facing stereo `proc` wrappers (`createSineTone`, `createSawTone`, `createSquareTone`, `createTriangleTone` at lines 341–397) and `noteToFrequency` are intact.

- **ClampSamples D-08 inline:** Both `PulseAudioSimpleBackend.cs` and `PlaybackFunctions.cs` now call `AudioUtils.ClampSamples(...)` directly. The thin wrapper methods are removed. Behavior is unchanged since those wrappers were single-line delegations.

- **test.flow D-07:** Lines 30–138 (the legacy pure-Flow assertion library) are removed. No remaining `.flow` file in `tests/` or `examples/` calls `assertTrue`, `assertEqual`, `runTest`, `assertFalse`, `assertGreater`, `assertLess`, or `summary` as Flow proc invocations. `test_test_library.flow` is fully ported to the `@test` surface.

- **D-16 legacy notes:** `Timeline.cs`, `Track.cs`, `Bars.cs`, `bars.flow`, and `composition.flow` received comment-only additions (XML-doc or `Note:` lines describing "legacy / superseded by X — kept as a usable surface"). No `[Obsolete]` attribute, no `WarnOnce` call, no `Console.Error` write, no stderr advisory was introduced. Compliant with D-16.

- **PrngRegistry / ExecutionContext / Phase37Fixtures comment cleanup:** Three comment-only edits remove `exportWav` from the boundary-description text. No code paths were touched.

---

_Reviewed: 2026-05-30_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: deep_
