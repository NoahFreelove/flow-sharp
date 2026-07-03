---
phase: 260702-vud
plan: 01
subsystem: audio-sfz
status: complete
tags: [sfz, sampler, ampeg_release, envelope, articulation, audio]
requires:
  - SfzRenderer (Phase 33 sample-based SFZ patch renderer)
  - SampledInstrumentRenderer (Phase 29 baseRelease=0 + exponential-tail precedent)
provides:
  - Sustained SFZ notes ring out past the authored end via an ampeg_release tail
affects:
  - flow-lang/StandardLibrary/Audio/Sfz/SfzRenderer.cs
tech-stack:
  added: []
  patterns:
    - "authoredFrames vs totalFrames split; hold-sustain-to-end + exponential tail"
key-files:
  created:
    - flow-lang.Tests/Integration/Quick260702Vud/SfzAmpegReleaseTailTests.cs
  modified:
    - flow-lang/StandardLibrary/Audio/Sfz/SfzRenderer.cs
    - flow-lang.Tests/Integration/Phase33/SfzArticulationTests.cs
decisions:
  - "Tail gate uses region.AmpegRelease > 0.001 (parser absent-sentinel), NOT > 0.0, to preserve byte-identity for ampeg_release-absent patches (Rule 1 deviation)"
metrics:
  tasks: 2
  files_changed: 3
  commits: 3
  completed: 2026-07-02
---

# Phase 260702-vud Plan 01: Fix SFZ Note Cutoff (ampeg_release Tail) Summary

Sustained-articulation SFZ notes now hold their envelope at the sustain level through the authored note end and ring out via an exponential `ampeg_release` tail appended PAST the authored boundary, instead of squeezing the whole release into the note window (the 93%-fade cutoff bug that made VSCO CE `ampeg_release=0.7` patches sound detached/staccato/quiet).

## What Changed

**`SfzRenderer.RenderInternal`** — the old single `targetFrames` is split into `authoredFrames` (= `durationSeconds * sampleRate`) and `totalFrames` (= `authoredFrames + tailFrames`). `tailFrames` is non-zero only when the articulation is sustained AND the region declares a meaningful `ampeg_release`; it is `clamp(AmpegRelease, 0, 10) * sampleRate` (charitable clamp per CLAUDE.md). The picked `region` drives the tail decision; the same `totalFrames` threads to both render paths (single-region hard-switch and the `RenderAndSumXfadeLayers` velocity-crossfade summing path). `AssembleBody` needed no change — it already fills the whole target-length array (loop path continues into the tail; non-loop path zero-pads).

**`SfzRenderer.FinishMono`** — new `authoredFrames` + `totalFrames` signature. When `hasTail`:
- Phase 28 envelope is generated over the AUTHORED window with `baseRelease = 0.0` so the envelope holds the sustain level (1.0) through the authored end, meeting the tail continuously (mirrors Phase 29 `SampledInstrumentRenderer`).
- The SAMP-03 articulation multiplier is bounded to `Math.Min(authoredFrames, fitted.Length)` and samples its A/D/S/R quartiles against `authoredFrames` (mirrors the sweep-0614 fix) so the tail is not reshaped by the articulation quartiles.
- A separate exponential release tail multiplies `[authoredFrames, fitted.Length)` starting at `level = 1.0` (continuity, no seam step) with `decayPerFrame = 0.001^(1/tailFrames)`, reaching x0.001 (~-60 dB) at the final tail frame.

When `hasTail` is false (staccato/marcato with sustain=0, or ampeg_release-absent patches) the whole path is byte-identical to the pre-change renderer: `authoredFrames == fitted.Length`, `baseRelease` unchanged, SAMP-03 window == `fitted.Length`, no tail.

**`IsSustainedArticulation(a)`** = `a != Staccato && a != Marcato` — the two articulations `SynthUtils.GenerateArticulationADSR` forces to sustain=0.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Tail gate threshold: `> 0.001` not `> 0.0`**
- **Found during:** Task 1 (verifying must-have truth #4 against the parser).
- **Issue:** The plan's `key_links` specified `tailFrames = ... region.AmpegRelease > 0.0 ? ...`. But `SfzParser` defaults an ABSENT `ampeg_release` opcode to the `0.001`s sentinel (`SfzParser.cs:543`, `ReadDouble(region, "ampeg_release", 0.001, ...)`), not 0. A `> 0.0` gate would hand every ampeg_release-absent patch a ~44-frame tail, directly violating must-have truth #4 ("absent/0 → buffer length authoredFrames, byte-identical, no tail") and failing the `AmpegReleaseAbsent_NoTail` test.
- **Fix:** Gate on `region.AmpegRelease > AbsentAmpegReleaseSentinel` (a `0.001` named constant kept in sync with the parser default). Absent patches stay byte-identical (their `0.001` sentinel still feeds `baseRelease` in `FinishMono` unchanged); meaningfully-declared releases (VSCO CE `0.7`, the `0.05`s smoke fixture) ring out.
- **Files modified:** `flow-lang/StandardLibrary/Audio/Sfz/SfzRenderer.cs`
- **Commit:** 92ac67e

### Test refresh (plan-directed)

`SfzArticulationTests.SixArticulations_AudibleDuration_WithinTolerance` — the smoke fixture's `C4Region` declares `ampeg_release=0.05`, so sustained articulations (incl. Tenuto) now ring ~0.05s past the authored end. Raised the Tenuto upper bound from `authoredFrames * 1.05` to `(authoredFrames + (int)(0.05 * SampleRate)) * 1.05`. All other ratio/relative assertions (staccato/marcato/legato/accent/sforzando, `>=` smoke/loop length bounds, RMS-relative pan/velocity/round-robin tests) survived unchanged.

## New Tests (`SfzAmpegReleaseTailTests`)

Loop-continuous inline patch with `ampeg_release=0.7`, 0.5-beat @ 120 BPM note (0.25s authored, so the 0.7s tail dominates):
1. `SustainedNote_HoldsLevelAtAuthoredEnd` — level in the last 200 authored frames >= 90% of the mid-note level (anti-93%-fade regression).
2. `SustainedNote_BufferLength_IsAuthoredPlusRelease` — `buf.Frames == authoredFrames + (int)(0.7 * 44100)` (±2).
3. `StaccatoNote_NoTail_SamePatch` — staccato from the same patch → `buf.Frames == authoredFrames` (±2).
4. `Tail_IsContinuousAndDecays` — seam continuity (|after - before| < 0.05) + last-tail-quarter RMS < 20% of first-tail-quarter RMS.
5. `AmpegReleaseAbsent_NoTail` — parsed patch omitting `ampeg_release` → no tail.
6. `TwoRuns_ByteIdentical` — two renders element-wise equal (determinism, no RNG).

## Verification

- `dotnet build flow-lang/flow-lang.csproj` (Desktop) — 0 errors.
- `dotnet build flow-lang/flow-lang.csproj -p:FlowTarget=Web` — 0 errors (change is inside the already-Web-stripped `Sfz/` directory; no new guards).
- `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Sfz"` — **104 passed, 0 failed, 2 skipped** (Web-target skips). Was 98 pre-change; +6 new tail tests.

## Self-Check: PASSED

- `flow-lang/StandardLibrary/Audio/Sfz/SfzRenderer.cs` — FOUND (modified)
- `flow-lang.Tests/Integration/Quick260702Vud/SfzAmpegReleaseTailTests.cs` — FOUND (created)
- Commits cd48a25, 92ac67e, c47bd83 — FOUND in git log.
