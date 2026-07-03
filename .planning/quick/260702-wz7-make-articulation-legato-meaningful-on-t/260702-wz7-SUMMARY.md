---
task: quick-260702-wz7
title: Make Articulation.Legato meaningful on the SFZ sampler path
status: complete
completed: 2026-07-02
requirements: [QUICK-WZ7-01]
commits:
  - 78d1389  test(quick-260702-wz7): add SFZ Legato offset + softened-attack numeric suite
  - 738a7fa  feat(quick-260702-wz7): Legato sample-start offset + softened attack on SFZ path
files_created:
  - flow-lang.Tests/Integration/Quick260702Wz7/SfzLegatoRenderTests.cs
files_modified:
  - flow-lang/StandardLibrary/Audio/Sfz/SfzRenderer.cs
---

# Quick 260702-wz7: Legato on the SFZ Sampler Path — Summary

Made `Articulation.Legato` audibly meaningful on the SFZ sampler path: connected
string/wind lines no longer retrigger the full recorded bow-start/tongue attack
transient at full level on every note ("sound like a keyboard is playing them" —
composer feedback on the Swan Lake render). Legato notes now (a) skip the first
`min(0.1s, source.Frames/4)` frames of the pitch-resolved source body and (b) get
a softened `>= 80ms` envelope attack that masks the skipped-transient seam. Both
halves are gated to `Articulation.Legato` ONLY — every other articulation is
byte-identical to before (golden-hash pinned).

## What changed

### `SfzRenderer.cs` (implementation)
- **Sample-start offset.** `RenderRegionToMono` gained a trailing `Articulation`
  parameter; after `source` resolves through both the percussion `PitchShiftEngine`
  branch and the varispeed branch, it computes
  `startOffsetFrames = articulation == Legato ? Min((int)(0.1*sr), source.Frames/4) : 0`
  and passes it to `AssembleBody`. The articulation threads from both `RenderInternal`
  call sites through `RenderAndSumXfadeLayers` (velocity-crossfade summing path) and
  the single-region path.
- **`AssembleBody`** gained a final `int startOffsetFrames = 0` param, defensively
  clamped into `[0, source.Frames-1]`. NoLoop/OneShot straight-copy reads from the
  offset (`fitted[i] = ReadFrameMono(source, i + off)`); the loop paths (main +
  short-loop fallback) advance the pre-attack head read to start at `loopOff =
  Min(off, LoopStart)`, leaving the loop-body / `srcReadPos` / crossfade math intact
  so the loop simply plays `loopOff` frames earlier and stays seamless. `off == 0`
  (every non-Legato note, and any Legato patch whose relevant start is 0) is
  byte-identical to the pre-change reads.
- **Envelope attack softening** in `FinishMono`: the `baseAttack` argument of
  `GenerateArticulationADSR` becomes
  `note.Articulation == Legato ? Max(region.AmpegAttack, 0.08) : (existing)`.
  `GenerateArticulationADSR`'s Legato case uses `attack = baseAttack` directly
  (confirmed at `SynthUtils.cs:184-190`), so this yields a real 80ms attack ramp.

No `#if FLOW_WEB` guards — SFZ is already Web-stripped; this is a Desktop-only path.
The tpn velocity gain, vud release tail, region.Volume/Pan, and every other
articulation's shaping are untouched — the offset only changes WHICH source frames
feed `fitted`.

### `SfzLegatoRenderTests.cs` (5 facts, tests-first)
In-memory fixtures (no committed WAV — `SfzSampleCache.SetRaw_TestOnly`):
- **Constant fixture** (all 0.5f) isolates the envelope change.
- **Step fixture** (1.0 for frame < 8820, else 0.2) isolates the 4410-frame offset.

Facts: (1) `LegatoSoftensAttack_EarlyRmsLowerThanNormal` (constant, early RMS
< 0.6×Normal), (2) `LegatoSkipsAttackTransient_BodyReadsOffsetMaterial` (step,
settled RMS < 0.5×Normal), (3) `NonLegato_ByteIdentical_GoldenHash` (Normal +
Staccato SHA256 pinned), (4) `TwoRuns_Legato_ByteIdentical` (determinism),
(5) `Legato_EarlyRms_LowerThanNormal_StepFixture` (combined offset+attack).

Golden hashes captured against the UNMODIFIED renderer in Task 1:
- Normal: `6ae07c024350572e9d6d330baca51aa73fdd829b1e07f7be940dce54427e5caf`
- Staccato: `c085c38c4d6f10595e1826388cd36c78c7dd554236478a746a48f69397f25ffc`

Both are unchanged after Task 2 → non-Legato path proven byte-identical.

## Test-first evidence (RED → GREEN)
- Task 1, unmodified renderer: facts 3 + 4 PASS, facts 1/2/5 FAIL RED
  (Legato == Normal, RMS ratios exactly 1.0) — the tests detect the missing change.
- Task 2, after implementation: all 5 Wz7 facts GREEN; goldens unchanged.

## Verification
- `dotnet build` (Desktop) — 0 errors.
- `dotnet build flow-lang/flow-lang.csproj -p:FlowTarget=Web` — 0 errors (SFZ stays
  stripped; no new guards).
- `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Sfz"` — **109 passed,
  0 failed, 2 skipped** (Web-strip skips). Includes the pre-existing Vud
  (ampeg_release tail) + Tpn (amp_veltrack) suites → the offset composes with, and
  does not regress, those changes.

## Deviations from plan
None — plan executed exactly as written. The short-loop fallback loop-body anchor
was adjusted from `region.LoopStart` to the offset-shifted head end
`(LoopStart - loopOff)` so the wrap stays seamless under a Legato offset; this is
byte-identical when `loopOff == 0` (the must-have byte-identity condition) and only
differs for Legato on a pathologically short looped patch (unreachable in VSCO CE,
which declares no loops). This is within the plan's "apply it by STARTING the
pre-attack head read at source frame loopOff" instruction.

## Self-Check: PASSED
- `flow-lang/StandardLibrary/Audio/Sfz/SfzRenderer.cs` — FOUND (modified)
- `flow-lang.Tests/Integration/Quick260702Wz7/SfzLegatoRenderTests.cs` — FOUND (created)
- Commit `78d1389` — FOUND
- Commit `738a7fa` — FOUND
