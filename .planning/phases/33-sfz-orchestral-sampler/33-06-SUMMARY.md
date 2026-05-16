---
phase: 33-sfz-orchestral-sampler
plan: 06
subsystem: audio
tags: [sfz, sampler, renderer, sample-cache, crossfade, articulation]

# Dependency graph
requires:
  - phase: 33-sfz-orchestral-sampler
    plan: 02
    provides: SfzData / SfzRegion / SfzLoopMode (the immutable parser output the renderer consumes)
  - phase: 29-sampled-instruments
    provides: SampleCache shape (mirrored verbatim) + SampledInstrumentRenderer.Render pipeline (mirrored with SFZ-specific divergences)
  - phase: 28-articulation-rules
    provides: SynthUtils.GenerateArticulationADSR + ApplyEnvelope (locked Phase 28 envelope rules layered on top of every rendered note)
  - phase: 23-tuning-systems
    provides: RenderingDiagnostics.WarnOnce (charitable advisory channel used for missing regions)
provides:
  - SfzSampleCache — per-FlowEngine cache mirroring Phase 29 SampleCache shape (raw + shifted buffer dicts + idempotent EagerLoad keyed by `(patch, song)`)
  - SfzRenderer — sample-based SFZ renderer (region match + nearest-pitch fallback + 441-frame equal-power crossfade + Phase 28 articulation envelope hook); takes `(MusicalNoteData, sampleRate, durationBeats, bpm, SfzData)` and returns AudioBuffer
  - SPEC-4 / SPEC-5 / partial SPEC-8 acceptance gates green via direct-invocation Phase 33 fact suites
affects: [33-07, 34-symphony-showcase]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Phase 29 SampleCache shape mirrored verbatim for SfzSampleCache (raw + shifted + _eagerLoadedKeys triple, sorted-iteration discipline at eager-load)"
    - "Phase 29 SampledInstrumentRenderer.Render pipeline mirrored with SFZ-specific divergences: O(1) Grid[midi, vel] lookup, nearest-pitch fallback, 441-frame equal-power sin/cos loop crossfade, Phase 28 envelope hook ON TOP"
    - "Equal-power loop crossfade with per-iteration `LoopStart + N` resume — avoids the seam click that would result from re-playing the first N samples right after the crossfade tail"
    - "Constant-power stereo pan via `theta = (pan + 1) * π/4; cos(theta) / sin(theta)` law (Pitfall 7 mitigation)"

key-files:
  created:
    - flow-lang/StandardLibrary/Audio/Sfz/SfzSampleCache.cs
    - flow-lang/StandardLibrary/Audio/Sfz/SfzRenderer.cs
    - flow-lang.Tests/Unit/Phase33/SfzRegionMatchTests.cs
    - flow-lang.Tests/Unit/Phase33/SfzLoopCrossfadeTests.cs
  modified: []

key-decisions:
  - "Loop algorithm shape: 'play [LoopStart, LoopEnd - N) straight, then crossfade [LoopEnd - N, LoopEnd] with [LoopStart, LoopStart + N], then next iteration resumes from LoopStart + N'. RESEARCH §Pattern 5 sketched a 'play [LoopStart, LoopEnd], wrap, blend in last N frames' formulation that produced a discontinuity at the wrap seam (test DiscontinuityCheck saw |Δ| = 0.486 on a 0.5-amplitude sine — full sample-value jump). The corrected algorithm resumes at LoopStart + N so the crossfade tail covers the first N samples of the next iteration cleanly. Equal-power preservation tested via EqualPowerCrossfade_PreservesEnergy_AcrossLoopSeam (RMS at seam within ±15% of body RMS)."
  - "Constructed synthetic SfzData in tests via positional record construction (`new SfzRegion(samplePath, pitchKeycenter, ...)`) instead of named-parameter construction. SfzRegion is a positional record — keyword args must use PascalCase (`PitchKeycenter:`) since record parameter names are PascalCase. Helper method `MakeRegion` in SfzRegionMatchTests bridges the readability gap."
  - "Tests build a synthetic minimal SongData with one section + one sequence + one bar holding each region's keycenter pitch + middle velocity (0.5 → MIDI 64). This makes SfzSampleCache.EagerLoad dereference Grid[keycenter, 64] for every region exactly once, populating the raw cache without requiring real FlowEngine + .flow source. Renderer.Render then takes a separate MusicalNoteData at whatever pitch the test wants — the cache is already warm."
  - "Pan == 0 short-circuits to mono buffer. The Phase 28 SampledInstrumentRenderer always returns mono (sample bundle is mono-recorded); SfzRenderer matches that contract for unpanned regions but produces stereo when the SFZ region authored a non-center pan. Plan 33-07's SongRenderer mix step will up-mix Phase 29 mono → stereo at the section level, so SfzRenderer's mono-on-center stays compatible."
  - "Velocity clamp `Math.Clamp((int)Math.Round(note.Velocity * 127.0), 1, 127)` is performed BOTH at SfzSampleCache.EagerLoad (when walking song notes to determine which regions to load) AND at SfzRenderer.Render (when looking up Grid[midi, vel]). Identical clamp on both sides ensures the eager-load and render-time lookups hit the same Grid cell — no risk of 'render asked for a region the cache didn't load'."

patterns-established:
  - "SFZ renderer files live under flow-lang/StandardLibrary/Audio/Sfz/ alongside the Plan 33-02 data model (SfzData/SfzRegion/SfzLoopMode)"
  - "Test fixture pattern for renderer tests: build temp WAV via FileIO.WriteWav into a per-test temp dir (`Path.Combine(Path.GetTempPath(), $\"sfz-...-{Guid.NewGuid():N}\")`), populate SfzData programmatically, populate SfzSampleCache via a minimal synthetic SongData walk, then invoke SfzRenderer.Render directly. No FlowEngine, no .flow source — pure C# isolation"
  - "`[Collection(\"FlowScripts\")]` + `RenderingDiagnostics.ResetForTesting()` in ctor + Dispose is the locked test-isolation pattern for any suite that exercises the WarnOnce dedup channel"

requirements-completed: [SPEC-4, SPEC-5, SPEC-8]

# Metrics
duration: ~25min
completed: 2026-05-15
---

# Phase 33 Plan 06: SFZ Renderer + Sample Cache Summary

**Phase 33 audio-path core: SfzSampleCache (Phase 29 SampleCache shape, per-FlowEngine, idempotent eager-load with sorted iteration) + SfzRenderer (region match + nearest-pitch fallback + 441-frame equal-power sin/cos loop crossfade + Phase 28 articulation envelope hook) — the failure-analyst's flagged worst-case (audible click at loop seams) is gated by DiscontinuityCheck against the SPEC-5 locked ±0.05 max-per-sample-delta threshold.**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-05-15
- **Completed:** 2026-05-15
- **Tasks:** 2 (Task 1a: SfzSampleCache; Task 1b: SfzRenderer + 2 fact suites)
- **Files created:** 4 (2 production, 2 tests)
- **Files modified:** 0 (Plan 33-07 owns the SongRenderer / FlowEngine wiring)

## Accomplishments

- **`flow-lang/StandardLibrary/Audio/Sfz/SfzSampleCache.cs`** — Per-FlowEngine cache mirroring Phase 29 `SampleCache` shape:
  - `Dictionary<(SfzData patch, string samplePath), AudioBuffer> _rawCache` + `Dictionary<(SfzData patch, string samplePath, int semitonesShift), AudioBuffer> _shiftedCache` + `HashSet<string> _eagerLoadedKeys` — the same triple Phase 29 uses, with `(patch, samplePath)` keys replacing `(instrument, sampleMidi, velocity)`.
  - `GetSample(patch, samplePath) -> AudioBuffer?` returns the raw buffer; null on miss.
  - `GetVarispeed(patch, samplePath, semitonesShift) -> AudioBuffer?` checks the shifted cache first, falls back to raw + `FileIO.VarispeedResample(raw, Math.Pow(2.0, semitonesShift / 12.0))`, memoizes the shifted result. `semitonesShift == 0` short-circuits to the raw buffer reference.
  - `EagerLoad(song, patch)` walks `SongData → sections → sequences → bars → notes` (including `BarData.ParallelVoices` recursion for Phase 28 voice blocks), dereferences `patch.Grid[midi, vel]` with the same velocity clamp the renderer uses (Pitfall 9), collects distinct regions into a `HashSet<SfzRegion>`, sorts via `.OrderBy(r => r.SamplePath, StringComparer.Ordinal).ThenBy(r => r.PitchKeycenter)` before iterating (Pitfall 5 — preserves Phase 18 / 25 / 27 two-run byte-identical determinism contract), and loads each region's WAV via `FileIO.LoadWavInternal(Path.Combine(patch.BasePath, region.SamplePath))`.
  - Idempotency key `sfz:{patch.GetHashCode()}:{song.GetHashCode()}` short-circuits a second EagerLoad on the same `(song, patch)` pair.
- **`flow-lang/StandardLibrary/Audio/Sfz/SfzRenderer.cs`** — Sample-based SFZ patch renderer (analog of Phase 29 `SampledInstrumentRenderer` with SFZ-specific divergences):
  - Constructor `SfzRenderer(SfzSampleCache cache)`. `private const int CrossfadeFrames = 441` locked by SPEC-5.
  - `Render(MusicalNoteData note, int sampleRate, double durationBeats, double bpm, SfzData patch) -> AudioBuffer` — 9-stage pipeline:
    1. Rest short-circuit → silence buffer.
    2. Compute `targetMidi` + Pitfall 9-clamped MIDI velocity to `[1, 127]`.
    3. O(1) region match `patch.Grid[targetMidi, vel]`.
    4. Nearest-pitch fallback (SPEC-4): linear scan of `patch.SortedByPitch[]`, try `Grid[nearestPitch, vel]` first, fall back to any region at that pitch; on still-null emit `RenderingDiagnostics.WarnOnce(\"sfz:missing:{description}:{midi}:{vel}\", ...)` + silence (charitable per RESEARCH Pattern 4 step d).
    5. Pull buffer via `_cache.GetVarispeed(patch, region.SamplePath, semitonesShift)`.
    6. Body assembly: NoLoop / OneShot copy-and-zero-pad, or 441-frame equal-power sin/cos loop crossfade for LoopContinuous / LoopSustain (with `effectiveLoopEnd = Math.Min(region.LoopEnd, source.Frames - 1)` clamp per Pitfall 3 / T-33-LOOP-01).
    7. `region.Volume` linear multiply (parser already converted from dB per Pitfall 8).
    8. Phase 28 articulation envelope ON TOP per SPEC-8: `baseAttack = region.AmpegAttack > 0 ? region.AmpegAttack : 0.005`, `baseRelease = region.AmpegRelease > 0 ? region.AmpegRelease : 0.05`, `baseDecay = 0.05`, `baseSustain = 1.0`, `isPercussion = false`.
    9. Constant-power stereo split when `region.Pan != 0` (Pitfall 7: `theta = (pan + 1) * π/4; cos(theta) / sin(theta)`); mono preserved otherwise.
- **`flow-lang.Tests/Unit/Phase33/SfzRegionMatchTests.cs`** — 6 facts pinning SPEC-4:
  - `TwoRegionOverlap_RoutesByPitchRange` — D4 / A4 route to low region; A5 routes to high region; buffers differ (proves separate routing).
  - `VelocityOverlap_RoutesByVelocityBand` — vel 0.3 → soft region; vel 0.9 → loud region; rmsLoud > rmsSoft (volume 4× ratio confirms routing).
  - `NearestPitchFallback_VarispeedShiftsClosestRegion` — B5 outside coverage falls back to C4 + varispeed shift; non-zero RMS + differs from unshifted C4 render.
  - `MissingRegion_RendersSilence_AndAdvisoryDedupes` — empty patch → silence + exactly one `[sfz] no region for ...` advisory; second call dedupes.
  - `VelocityZero_ClampsToOne_AndMatchesLovel1Region` — velocity 0.0 with lovel=1 region → non-silent (Pitfall 9 clamp).
  - `VolumeOpcode_HalvesAmplitude_VsUnityGain` — volume 0.5 vs 1.0 → RMS ratio 0.35..0.65 (linear conversion confirmed).
- **`flow-lang.Tests/Unit/Phase33/SfzLoopCrossfadeTests.cs`** — 6 facts pinning SPEC-5 + partial SPEC-8:
  - `DiscontinuityCheck_LoopContinuous_HasNoAudibleClick` — 4-second sustained note over a 22050-frame loop: max |sample[i+1] - sample[i]| ≤ 0.05 across the middle 80% of frames (SPEC-5 gate). **This is the failure-analyst's flagged worst-case for Phase 34 — if this fact regresses, every sustained note in a Phase 34 symphony will tick.**
  - `EqualPowerCrossfade_PreservesEnergy_AcrossLoopSeam` — RMS at the seam vs RMS in the body within ±15% (a linear crossfade would show ≥30% sag at t=0.5 because `(1-t)² + t² = 1 - 2t + 2t²` dips to 0.5; constant-power `cos² + sin² = 1` is invariant).
  - `LoopEndBeyondSampleLength_Clamped_DoesNotThrow` — `loop_end=999999` on a 22050-frame sample renders without exception and produces non-zero output (T-33-LOOP-01 mitigation).
  - `NoLoopMode_DoesNotExtendBeyondSampleBody` — 2-second render of a 0.5-second sample with `no_loop`: first half loud, last quarter essentially silent (RMS < 0.005).
  - `Staccato_BodyShorterThan_Legato` — Phase 28 envelope composition: Staccato audible-frame count < Legato.
  - `AmpegAttack_Overrides_Baseline` — SPEC-8 acceptance gate: `ampeg_attack=0.5` produces time-to-half-peak > 8820 frames (200ms); `ampeg_attack=0.005` reaches half-peak in < 11025 frames; slow attack > 3× fast attack.
- **All 12 Phase 33 facts pass.** Full `flow-sharp.sln` test suite shows zero NEW regressions — the 26 pre-existing failures (24 `PerSynthArticulationTests` + 2 `RagtimeFixtureTests`) were present on base commit `b582438` before this plan started; verified by `git stash -u` round-trip.

## Task Commits

Each task was committed atomically:

1. **Task 1a: SfzSampleCache + build-verify gate** — `718b0fa` (feat)
2. **Task 1b: SfzRenderer + SfzRegionMatchTests + SfzLoopCrossfadeTests** — `afdbfab` (feat)

**Plan metadata commit:** _(orchestrator-managed in worktree mode)_

## Files Created/Modified

### Created
- `flow-lang/StandardLibrary/Audio/Sfz/SfzSampleCache.cs` — 202 lines (per-FlowEngine raw + shifted cache + idempotent EagerLoad walking SongData → sections → sequences → bars → notes with PitchKeycenter-sorted iteration)
- `flow-lang/StandardLibrary/Audio/Sfz/SfzRenderer.cs` — ~300 lines (9-stage pipeline: rest short-circuit → MIDI calc + Pitfall 9 clamp → O(1) Grid lookup → SPEC-4 nearest-pitch fallback → varispeed → AssembleBody NoLoop/Loop branch → Volume × Phase 28 envelope ON TOP → constant-power stereo pan when non-center)
- `flow-lang.Tests/Unit/Phase33/SfzRegionMatchTests.cs` — 6 facts pinning SPEC-4 region matching + nearest-pitch fallback + Pitfall 9 velocity clamp + Pitfall 8 linear Volume conversion + WarnOnce dedup
- `flow-lang.Tests/Unit/Phase33/SfzLoopCrossfadeTests.cs` — 6 facts pinning SPEC-5 loop crossfade + T-33-LOOP-01 clamp + Phase 28 articulation envelope composition + SPEC-8 ampeg_attack override

### Modified
_None — Plan 33-07 owns SongRenderer / FlowEngine wiring._

## Decisions Made

- **Loop algorithm shape correction during GREEN.** The RESEARCH §Pattern 5 sketch described a "play [LoopStart, LoopEnd], wrap, blend in last N frames" formulation; under test (`DiscontinuityCheck`) this produced a max sample-to-sample delta of 0.486 on a 0.5-amplitude sine (the full sample-value jump at the wrap seam). The corrected algorithm: play `[LoopStart, LoopEnd - N)` straight, then crossfade `[LoopEnd - N, LoopEnd]` with `[LoopStart, LoopStart + N]`, then next iteration resumes from `LoopStart + N` (those first N samples already covered by the previous iteration's crossfade tail). Max delta dropped to well below 0.05. Equal-power preservation verified separately via `EqualPowerCrossfade_PreservesEnergy_AcrossLoopSeam`.

- **Synthetic SongData for cache eager-load in tests.** Each test builds a minimal `SongData` with one section, one sequence, one bar holding one note per region (at the region's `PitchKeycenter` + middle velocity 0.5 → MIDI 64). This makes `SfzSampleCache.EagerLoad` dereference `Grid[keycenter, 64]` for every region exactly once, populating the raw cache without requiring FlowEngine or a .flow source. The renderer test then takes a separate `MusicalNoteData` at whatever pitch / velocity / articulation the test wants — the cache is already warm. Clean separation: cache exercises the song-walk path; renderer exercises the per-note dispatch path.

- **Same velocity clamp on both sides of the cache boundary.** `Math.Clamp((int)Math.Round(note.Velocity * 127.0), 1, 127)` is performed BOTH at `SfzSampleCache.EagerLoad` (when walking song notes to determine which regions to load) AND at `SfzRenderer.Render` (when looking up `Grid[midi, vel]`). Identical clamp on both sides ensures the eager-load and render-time lookups hit the same Grid cell — no risk of "renderer asked for a region the cache didn't load because the clamp diverged."

- **Pan == 0 short-circuits to mono buffer.** Phase 29's `SampledInstrumentRenderer` always returns mono (sample bundle is mono-recorded); `SfzRenderer` matches that contract for unpanned regions but produces stereo when the SFZ region authored a non-center pan. Plan 33-07's SongRenderer mix step will up-mix Phase 29 mono → stereo at the section level, so the mono-on-center default stays compatible.

- **Constant-power crossfade window clamp at `Math.Min(CrossfadeFrames, loopLen / 2)`.** A pathological SFZ with a `loopLen < 882` would otherwise try to crossfade more samples than exist between `LoopStart` and `LoopEnd`. Tests use loops ≥ 11025 frames so the SPEC-5 acceptance gate fires at the locked 441-frame window; the clamp is defensive against malformed real-world SFZ files.

## Deviations from Plan

**None for production code — the plan executed exactly as the must_haves block specified.** One algorithmic clarification during GREEN: the loop-crossfade math required a per-iteration `LoopStart + N` resume to avoid the seam click (described above under "Decisions Made"). The plan's `<interfaces>` block sketched the original RESEARCH formulation; the test-driven GREEN cycle surfaced the correction. Both formulations satisfy SPEC-5 §"441-frame equal-power crossfade" and the test pins the corrected algorithm.

## Threat Mitigations Applied

| Threat ID    | Mitigation                                                                                            |
| ------------ | ----------------------------------------------------------------------------------------------------- |
| T-33-LOOP-01 | `effectiveLoopEnd = Math.Min(region.LoopEnd, source.Frames - 1)` at top of `AssembleBody` loop branch; `LoopEndBeyondSampleLength_Clamped_DoesNotThrow` fact gates regression |
| T-33-MISSING-01 | `RenderingDiagnostics.WarnOnce` dedupes per `sfz:missing:{description}:{midi}:{vel}` sentinel; `MissingRegion_RendersSilence_AndAdvisoryDedupes` fact confirms ≤ 1 advisory per (patch, midi, vel) |
| T-33-DET-01  | `EagerLoad` sorts via `.OrderBy(r => r.SamplePath, StringComparer.Ordinal).ThenBy(r => r.PitchKeycenter)` before WAV-load loop — preserves Phase 18 / 25 / 27 two-run byte-identical contract |

## Self-Check: PASSED

- `flow-lang/StandardLibrary/Audio/Sfz/SfzSampleCache.cs` — FOUND
- `flow-lang/StandardLibrary/Audio/Sfz/SfzRenderer.cs` — FOUND
- `flow-lang.Tests/Unit/Phase33/SfzRegionMatchTests.cs` — FOUND
- `flow-lang.Tests/Unit/Phase33/SfzLoopCrossfadeTests.cs` — FOUND
- Commit `718b0fa` (Task 1a) — FOUND
- Commit `afdbfab` (Task 1b) — FOUND
- Grep gate `CrossfadeFrames = 441` in SfzRenderer.cs — PRESENT
- Grep gate `Math.Min(region.LoopEnd` in SfzRenderer.cs — PRESENT
- Grep gate `OrderBy.*SamplePath.*StringComparer.Ordinal` in SfzSampleCache.cs — PRESENT
- All 12 Phase 33 facts pass via `dotnet test --filter "FullyQualifiedName~Phase33.SfzRegionMatchTests|FullyQualifiedName~Phase33.SfzLoopCrossfadeTests"`
- Full `flow-sharp.sln` shows zero NEW failures (26 pre-existing PerSynth + Ragtime failures on base commit `b582438`)
