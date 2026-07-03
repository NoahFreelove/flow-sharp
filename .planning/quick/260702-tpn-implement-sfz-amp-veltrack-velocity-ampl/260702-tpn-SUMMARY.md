---
phase: 260702-tpn
plan: 01
subsystem: flow-lang/StandardLibrary/Audio/Sfz
tags: [sfz, sampler, velocity, dynamics, audio]
requires:
  - SFZ render path (Phase 33) + velocity-crossfade (Phase 37 SAMP-02)
provides:
  - SFZ amp_veltrack velocity-amplitude curve applied in the sample render path
affects:
  - Every SFZ-sampled instrument render (renderSong "sampler:NAME"); VSCO-CE dynamics
tech-stack:
  added: []
  patterns:
    - "Renderer-side charitable clamp of a parsed opcode's effective range (parser stores raw)"
    - "ComputeVelocityGain mirrors ComputeXfadeGain / *_TestOnly pattern"
key-files:
  created:
    - flow-lang.Tests/Integration/Quick260702Tpn/SfzAmpVeltrackTests.cs
  modified:
    - flow-lang/StandardLibrary/Audio/Sfz/SfzRegion.cs
    - flow-lang/StandardLibrary/Audio/Sfz/SfzParser.cs
    - flow-lang/StandardLibrary/Audio/Sfz/SfzRenderer.cs
    - flow-lang.Tests/Integration/Phase37/SfzVelocityCrossfadeRenderTests.cs
decisions:
  - "amp_veltrack default 100.0 (Sforzando/ARIA default) → gain (vel/127)^2; 0 disables the curve"
  - "Parser stores the RAW declared value; renderer owns the [0,1] track-fraction clamp + advisory"
  - "Velocity gain folded into the same single-pass scale as region.Volume — applied exactly once per body, per-layer in the xfade summing path"
metrics:
  duration: ~10m
  completed: 2026-07-02
status: complete
---

# Phase 260702-tpn Plan 01: Implement SFZ amp_veltrack velocity-amplitude curve — Summary

SFZ note velocity now scales rendered output amplitude via the `amp_veltrack` opcode curve `(1-t)+t·(vel/127)²` (t = clamp(ampVeltrack/100, [0,1])), closing the flat/inverted-dynamics bug where VSCO Community Edition's per-layer makeup gains rendered pp louder than ff on every SFZ instrument.

## What Was Built

- **`SfzRegion.AmpVeltrack`** (new `double`, default `100.0`) appended after the Phase 37 optional record params so every existing positional constructor call stays valid; documented in the class field-semantics block.
- **`SfzParser`**: `amp_veltrack` added to the `KnownOpcodes` whitelist (22 → 23; doc updated), read in `BuildRegion` via `ReadDouble(..., 100.0, ...)` (inherits the global/group/region cascade because the merged region dict already holds global+group at `<region>` open), and passed as the final `new SfzRegion(...)` argument. Raw value stored — no parser-side clamp.
- **`SfzRenderer`**: new pure `ComputeVelocityGain(ampVeltrack, vel)` + `ComputeVelocityGain_TestOnly` mirroring the `ComputeXfadeGain` pattern. `RenderRegionToMono` gained an `int vel` param (both call sites — the single-region path in `RenderInternal` and the per-layer call in `RenderAndSumXfadeLayers`); the velocity gain is folded into the same single-pass scale as `region.Volume` (`combinedScale = region.Volume * velGain`, `!= 1.0` short-circuit against the combined value) so it applies exactly once per body and honors per-layer `amp_veltrack` differences. A one-shot `WarnOnce` advisory (keyed per patch) fires when the declared value is outside `[0,100]`.

## Verification

- `dotnet build flow-lang/flow-lang.csproj` — 0 errors (Desktop).
- `dotnet build flow-lang/flow-lang.csproj -p:FlowTarget=Web` — 0 errors (drift gate; SFZ files stripped on Web, trivially green).
- `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Sfz"` — **98 passed, 2 skipped (Web-only), 0 failed.**

New `SfzAmpVeltrackTests` pins: curve math (`(100,127)==1.0`, `(100,64)≈0.2540`, `(0,*)==1.0`, clamp of 150→100 / -50→0), parser default (absent→100.0; `amp_veltrack=0`→0.0; `=50`→50.0), and the headline loudness ordering (two-layer +18 dB soft / +6 dB loud makeup-gain patch → pp RMS < ff RMS). `SfzVelocityCrossfadeRenderTests` refreshed to a per-velocity reference (`expectedSingleDb = refDb + 40·log10(vel/50)`) since the velocity-squared curve now folds into every render.

## Deviations from Plan

None — plan executed exactly as written.

## Commits

- `1cf8a3e` feat(quick-260702-tpn): apply SFZ amp_veltrack velocity-amplitude curve
- `5c308a5` test(quick-260702-tpn): pin amp_veltrack curve, parser default, loudness ordering

## Self-Check: PASSED

- All modified/created source + test files present on disk.
- Both task commits present in git history.
