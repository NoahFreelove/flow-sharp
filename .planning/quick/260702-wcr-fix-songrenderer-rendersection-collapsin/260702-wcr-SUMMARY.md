---
phase: quick-260702-wcr
plan: 01
subsystem: audio-render
tags: [song-renderer, rest-section, silence, offline-render, charitable-interpretation]
requires:
  - flow-lang/StandardLibrary/Audio/SongRenderer.cs (RenderSection terminal guard)
  - flow-lang/TypeSystem/SpecialTypes/SequenceType.cs (bar-capacity TotalBeats)
provides:
  - all-rest Song sections render notated-length silence instead of collapsing to zero frames
affects:
  - any multi-section Song where an instrument/section is tacet for whole sections
tech-stack:
  added: []
  patterns:
    - "split terminal guard: maxBeats<=0 (empty) vs allVoices.Count==0 with maxBeats>0 (all-rest silence)"
    - "silent stereo buffer sized by the same frame formula MixVoicesToStereoBuffer uses, off the resolved section bpm"
key-files:
  created:
    - flow-lang.Tests/Integration/Quick260702Wcr/AllRestSectionLengthTests.cs
  modified:
    - flow-lang/StandardLibrary/Audio/SongRenderer.cs
decisions:
  - "All-rest section = silence of its notated length (charitable interpretation); genuinely-empty section (no sequences, maxBeats<=0) still returns zero-length"
  - "Frame count uses the resolved section bpm so tempo context is honored; songs without all-rest sections are byte-identical (both untouched branches)"
metrics:
  duration: ~15m
  completed: 2026-07-02
  tasks: 2
  files: 2
status: complete
---

# Quick Task 260702-wcr: Fix SongRenderer.RenderSection Collapsing All-Rest Sections Summary

`SongRenderer.RenderSection` was collapsing an all-rest Song section to zero frames, shifting every later section early; it now renders such a section as tempo-scaled silence of its notated length, so note content after a tacet section starts at the correct frame offset.

## What Changed

### Task 1 — RenderSection guard split (commit cddf57c)

The single core overload `RenderSection(SectionData, Func<string,INoteSynthesizer>)` had one terminal guard:

```csharp
if (allVoices.Count == 0 || maxBeats <= 0)
    return new AudioBuffer(0, StereoChannels, DefaultSampleRate);
```

Rests produce no `Voice` objects, so an all-rest section reached this guard with `allVoices.Count == 0` but `maxBeats > 0` (bars are capacity-based, so `SequenceData.TotalBeats` reports the full bar-grid length) and returned a zero-length buffer — `AppendBuffers` then concatenated nothing and later sections started early.

The guard is now split into two cases:
- `maxBeats <= 0` (genuinely empty section — no sequences, or only zero-length sequences): **unchanged** — returns the historical zero-length buffer.
- `allVoices.Count == 0` with `maxBeats > 0` (all-rest section): returns a zero-filled stereo buffer of the notated length, computed with the SAME formula `MixVoicesToStereoBuffer` uses — `(int)(maxBeats * (60.0 / bpm) * DefaultSampleRate)` — using the already-resolved section `bpm` local so tempo context is honored.
- `allVoices.Count > 0`: falls through to the existing `MixVoicesToStereoBuffer` call, untouched.

A freshly-constructed `AudioBuffer(frames, channels, sampleRate)` is zero-filled, so the silent buffer is frame-count-identical to what a zero-voice additive mix would produce. This is the single funnel for `RenderSong` (single-synth), `sampler:NAME` (`RenderSongWithSfz`), and `RenderSongAuto`.

### Task 2 — Regression suite (commit 5ec808f)

`flow-lang.Tests/Integration/Quick260702Wcr/AllRestSectionLengthTests.cs` — 4 `[Fact]`s, `[Collection("FlowScripts")]`, buffers captured WITHOUT WAV round-trip (WAV write applies seeded dither, which would defeat the exact-zero assertion). Uses the `organ` synth (full sustain, no sample-length cap):

1. `AllRestSection_ThenNoteSection_NoteContentStartsAtRestDuration` — `[tacet melody]` vs `[melody]` under `tempo 100`: `withRest.Frames == noRest.Frames + restFrames` (±2), the lead over the rest is silent (peak < 1e-6), and audible energy exists after it.
2. `AllRestSection_Alone_IsSilentBufferOfNotatedLength` — `[tacet]` renders a notated-length buffer (not zero) with every sample exactly 0.0.
3. `EmptySection_NoSequences_StaysZeroLength` — direct-construction empty section renders zero-length (pins the preserved `maxBeats <= 0` branch).
4. `AllRestSection_HalfTempo_DoublesFrames` — the same 8-beat rest section at `tempo 60` vs `tempo 120` gives 2× frames, proving the resolved section bpm drives the silent length.

## Verification

- `dotnet build flow-lang/flow-lang.csproj` (Desktop) — Build succeeded.
- `dotnet build flow-lang/flow-lang.csproj -p:FlowTarget=Web` — Build succeeded (edit is plain arithmetic, no `#if` guards, no stripped API).
- `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Quick260702Wcr"` — 4/4 passed.
- `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Sfz|FullyQualifiedName~ReverbTimeRender|FullyQualifiedName~SustainPedal"` — 110 passed, 2 skipped, 0 failed (these multi-section render suites funnel through `RenderSection` and contain no all-rest sections, so their output is unchanged — no regression).

## Deviations from Plan

None — plan executed exactly as written. The `maxBeats` source (`max(sequence.TotalBeats)`, bar-capacity based) was confirmed correct as the plan anticipated; no fallback length-derivation was needed.

## Known Stubs

None.

## Self-Check: PASSED

- `flow-lang/StandardLibrary/Audio/SongRenderer.cs` — FOUND (modified, commit cddf57c)
- `flow-lang.Tests/Integration/Quick260702Wcr/AllRestSectionLengthTests.cs` — FOUND (commit 5ec808f)
- Commit cddf57c — FOUND
- Commit 5ec808f — FOUND
