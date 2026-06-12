---
quick_id: 260611-wp2
slug: fix-note-stream-cutoff-sequencedata-bar-
status: complete
date: 2026-06-12
---

# Quick Task 260611-wp2 — Summary

## What was fixed

Note streams cut off after ~1 bar in both native Flow and the WASM playground
(`(play | C4q D4q E4q F4q G4q A4q B4q C5h |)` → only first 4 notes; `key Cmajor { (play | [chords] |) }`
→ only first 2 chords).

**Root cause:** `flow-lang/TypeSystem/SpecialTypes/SequenceType.cs` computed a non-pickup
bar's length as the time-signature **numerator** rather than its actual content. A note
stream packs all its notes into one bar, so a 9-beat stream in 4/4 reported 4 beats →
`PlaybackFunctions.MixVoicesToBuffer` sized the mix buffer to 4 beats and dropped the rest.

## Change

`SequenceType.cs` — extracted a `BarLengthBeats(BarData)` helper used by both `AddBar` and
`ToTimeline`:

- **Pickup bars:** `GetActualBeats()` (unchanged).
- **Monophonic bars:** `Math.Max(numerator, GetActualBeats())` — honors overfull streams,
  still pads underfull/exact bars to the full bar (no layout regression, two-run determinism
  preserved).
- **Parallel `{voice}` bars:** keep the numerator. `GetActualBeats()` SUMS simultaneous
  voices (plus the compiler's placeholder full-bar rest), which would over-count a bar whose
  voices actually fit. (First, broader `Math.Max` attempt broke
  `Phase28.MultiLineEightBars_CompilesToEightBars` 16→32; restricting to monophonic bars
  fixed it. Overfull *parallel* voices are out of scope.)

## Verification

- Native repro now: stream `Sequence[1 bars, 9 beats total]`, rendered WAV **4.5s** (was 2.0s,
  all 8 notes audible); chords `Sequence[1 bars, 8 beats total]`.
- `flow-lang.Tests`: **2432 passed**, 19 skipped. The 4-note/2-chord truncation is gone.
- `Phase28.StaccatoGraceNoteRegressionTests` (parallel-voice cases): 6/6 pass.
- Smoke: `tests/test_full_song.flow`, `test_note_streams.flow`, `test_song_structure.flow` clean.

### Baseline regenerated (intentional)

`flow-lang.Tests/baselines/Phase37/piano_warmth_smoke.wav` — the fixture's `rh`/`lh` are
12-beat **monophonic** streams in a single `| |`; the old baseline (105840 frames / 2.4s)
had pinned the **truncation bug** (only 4 beats). Regenerated to the correct full 12-beat
render (317519 frames / 7.2s); two renders are byte-identical (determinism preserved).
`PIANO01_BundledPianoWarmth_RmsMatchesBaseline` passes against the new baseline.

## Pre-existing / flaky failures (NOT caused by this change)

Confirmed by running each against clean HEAD (change stashed) and in isolation:

- `Phase41.Showcase_RmsWithinTolerance` — **pre-existing**, fails on clean HEAD too (RMS
  level drift ~1.06 dB in window 1, a level issue not a length issue). Out of scope; flagged
  for separate follow-up.
- `Phase40.ClockSlaveDrivesTempo`, `Phase48.RunFromJs_SimpleScript…`,
  `Phase48.RunFromJs_ToneRender…` — **flaky** under full-suite load (real-time timing /
  suite-ordering / shared stdout-redirect state); pass in isolation with and without the
  change.

## Follow-on

This fix is needed before the WASM bundle is regenerated for the live playground — it will
ship in that regen alongside the already-landed `createSineTone` fix.
