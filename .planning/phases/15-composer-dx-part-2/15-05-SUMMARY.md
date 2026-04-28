---
phase: 15-composer-dx-part-2
plan: 05
subsystem: testing
tags: [dx-09, determinism, byte-identical, midi, wav, two-pass-strict, audio-rng-seeding]

# Dependency graph
requires:
  - phase: 15-composer-dx-part-2
    provides: Plan 01 promoted MidiReadHelpers + tests/output/.gitignore; Plan 04 shipped the 6-arg euclidean overload (DX-09 core) with in-process SameSeed_ProducesIdenticalVelocities supporting Fact
provides:
  - "F-19 EuclideanByteIdenticalTests.SameSeed_ByteIdenticalMidi — cross-file MIDI byte identity + empirical velocity-byte pin"
  - "F-20 EuclideanByteIdenticalTests.SameSeed_ByteIdenticalWav — cross-file WAV byte identity (full audio pipeline)"
  - "Audio-layer determinism: SynthUtils.ResetNoiseRng() hook + fixed-seed reset on every renderSong / RenderSongWithLambda / RenderSongWithTimeline entry"
  - "WAV-export determinism: FileIO TPDF dither RNG reseeded on every ExportWavInternal entry"
  - "MidiReadHelpers retroactively validated: 2 consumers (Phase 14 DX-08 + Phase 15 F-19), zero duplicate MidiFile.Read call sites in flow-lang.Tests/"
affects: [15-06 (next plan in phase, .flow scripts), 15-07 (closure rollup), future audio refactors must preserve fixed-seed RNG contract for D-18 cross-render reproducibility]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Two-pass strict empirical capture for cross-version determinism Facts (Pass 1 RED placeholder → Pass 2 observed-byte pin); now applied at the file-byte level in addition to the stdout/Value level used in Phases 13-14"
    - "Deterministic-by-design RNG seeding in shared static fields: fixed-seed `new Random(seed)` reseeded on every renderSong / writeWav entry. Decorrelates within a single render (the only audible property dither/noise need); reproduces across renders (the D-18 contract). Single-line edits at the call sites that own state"
    - "Architectural-fix-bundled-into-test plan precedent (Phase 14 DX-08 D-13 bundle clause): when a Pass 1 Fact RED surfaces a pre-existing audio-layer determinism bug, the fix bundles into the same plan with the test. Plan 15-03 SUMMARY's documentation of the static unseeded RNG made the fix scope obvious"

key-files:
  created:
    - flow-lang.Tests/Integration/Phase15/EuclideanByteIdenticalTests.cs
  modified:
    - flow-lang/StandardLibrary/Audio/SynthUtils.cs
    - flow-lang/StandardLibrary/Audio/FileIO.cs
    - flow-lang/StandardLibrary/Audio/SongRenderer.cs

key-decisions:
  - "WAV byte identity required fixing two pre-existing audio-layer non-determinism sources (synth white-noise RNG in SynthUtils, TPDF dither RNG in FileIO) — both were unseeded `new Random()` and produced different bytes on every export. Plan 15-03 documented the dither bug; the synth-noise bug was discovered via Pass-1 RED on F-20"
  - "Fixed seeds chosen for the two RNG fields (0xD17E2 for dither, 0x55EED for synth noise) are arbitrary stable values — the seed identity does not matter, only that consecutive calls reseed to the same value. Comment in each file documents the contract"
  - "Reseed at function entry (not at object construction) — `private static Random Random = new Random(SEED)` becomes effectively a sequence-restart on every `ExportWavInternal` / `RenderSong` call via an inline `Random = new Random(SEED)`. Within a single export the RNG advances normally so dither/noise still decorrelates per sample (the only audible property TPDF/white-noise need)"
  - "MIDI Pass-1 outcome: A — primary SequenceEqual gate GREEN on first run before any Pass-2 byte capture. Plan 04's in-process determinism extended cleanly through DryWetMidi serialization without any gap-fix. Empirical bytes captured on net10.0.107: [122, 70, 108]"
  - "WAV Pass-1 outcome: RED on the SequenceEqual gate — same length (352844 bytes both runs), bytes diverged starting at byte 49. Two static unseeded RNGs surfaced; minimal gap-fix (5 lines across 3 files) closed the gap. After fix, both runs produce identical 352844-byte WAVs"

patterns-established:
  - "Deterministic-RNG-seeding pattern for shared static state: when a static `Random` is used for decorrelation-only purposes (TPDF dither, white-noise injection), seed it with a fixed value at entry to the public boundary (writeWav / renderSong). Audible quality unchanged; cross-call reproducibility gained"
  - "Test commit + fix commit separation: F-19 (no audio fix needed) committed standalone (10c9557, test). F-20 + audio-layer determinism gap-fix bundled (af09ce5, fix) — the commit type signals that this commit landed code changes beyond pure test authorship"

requirements-completed: [DX-09]

# Metrics
duration: 8min
completed: 2026-04-25
---

# Phase 15 Plan 05: Byte-Identical Determinism Summary

**Two new Integration Facts close ROADMAP criterion #2 across both serialization boundaries: F-19 pins byte-identical MIDI (Pass-2 empirical bytes [122, 70, 108] on net10.0.107) and F-20 pins byte-identical WAV (352844 bytes) — required reseeding two pre-existing static unseeded `Random` instances (synth white-noise + TPDF dither) at the renderSong / writeWav boundary.**

## Performance

- **Duration:** 8 min
- **Started:** 2026-04-25T18:24:46Z
- **Completed:** 2026-04-25T18:33:09Z
- **Tasks:** 2 (both committed atomically)
- **Files modified:** 4 (1 new test file + 3 audio-layer source files)

## Accomplishments

- **ROADMAP criterion #2 closed end-to-end:** byte-identical MIDI (F-19) AND byte-identical WAV (F-20) for the same `euclidean(3, 8, "C4", 0.3, 0.1, 42)` call across two separate FlowEngine runs. Both Facts run consecutively GREEN.
- **Two-pass strict empirical capture executed for the MIDI side** (Phase 14 D-13 protocol): Pass 1 placeholder `[0, 0, 0]` → Pass 1 outcome A (primary SequenceEqual gate GREEN, secondary RED with observed `[122, 70, 108]`) → Pass 2 commits the observed array. Plan 04's in-process velocity determinism extended through DryWetMidi serialization without gap-fix.
- **Audio-layer determinism gap-fix bundled** (Phase 14 D-13 divergence-bundle precedent): Pass 1 of F-20 RED on `bytes1.SequenceEqual(bytes2)` (same length, different content) surfaced two pre-existing static unseeded `Random` fields:
  - `flow-lang/StandardLibrary/Audio/SynthUtils.cs:11` — synth white-noise RNG used by piano hammer transient + saxophone breath noise (also drum hits)
  - `flow-lang/StandardLibrary/Audio/FileIO.cs:11` — TPDF dither RNG used in `FloatToInt16` and `WriteInt24`
  Both reseeded with fixed values; reset hooks called at every public-boundary entry (renderSong, ExportWavInternal). Audible quality unchanged (same distributions, just deterministic sequences) — cross-render reproducibility gained.
- **MidiReadHelpers retroactively validated** (DEFER-05 closure from Plan 01): 2 consumers (Phase 14 DX-08 DynamicsMidiVelocityTests + Phase 15 F-19 EuclideanByteIdenticalTests). `grep -rn "MidiFile.Read" flow-lang.Tests/` returns exactly 2 lines, both inside `Shared/MidiReadHelpers.cs` itself — zero duplicate call sites leaked.

## Task Commits

Each task was committed atomically:

1. **Task 1: SameSeed_ByteIdenticalMidi (F-19) — two-pass strict empirical capture** — `10c9557` (test)
2. **Task 2: SameSeed_ByteIdenticalWav (F-20) + audio-layer determinism gap-fix** — `af09ce5` (fix)

## Files Created/Modified

**Created (1):**

- `flow-lang.Tests/Integration/Phase15/EuclideanByteIdenticalTests.cs` — `[Collection("FlowScripts")]` xUnit class with 2 Facts. Uses `MidiReadHelpers.GetVelocityBytes` (single read path), `bytes1.SequenceEqual(bytes2)` for cross-file byte identity (both Facts), and an empirical velocity-byte pin on the MIDI Fact for cross-version drift detection.

**Modified (3, all in flow-lang/StandardLibrary/Audio/):**

- `SynthUtils.cs` — replaced unseeded `private static readonly Random Rng = new()` with fixed-seed `private static Random Rng = new Random(SynthNoiseSeed)` (`SynthNoiseSeed = 0x55EED`) plus `public static void ResetNoiseRng()` to reseed at render boundaries.
- `FileIO.cs` — replaced unseeded `private static readonly Random Random = new Random()` with fixed-seed `private static Random Random = new Random(DitherSeed)` (`DitherSeed = 0xD17E2`); added `Random = new Random(DitherSeed)` line at the start of `ExportWavInternal` so consecutive WAV exports reseed deterministically.
- `SongRenderer.cs` — added `using FlowLang.StandardLibrary.Audio.Synthesizers;` and `SynthUtils.ResetNoiseRng();` calls at the entry of `RenderSong`, `RenderSongWithLambda`, and `RenderSongWithTimeline` so every renderSong call starts from the fixed seed.

## Decisions Made

- **Pass-2 empirical byte capture (MIDI):** observed `[122, 70, 108]` for `euclidean(3, 8, "C4", swing=0.3, humanize=0.1, seed=42)` on `Microsoft.NETCore.App 10.0.7` (SDK 10.0.107). The three bytes correspond to Bjorklund(3,8) hits at step indices [0, 3, 6]: step 0 lands on the on-beat grid (D-06), gets the +0.3 swing accent over base 0.63, then +humanize jitter; steps 3 and 6 are off-beat under D-06's `floor(steps/hits)` rule and stay unaccented. Documented inline as the cross-version drift gate.
- **Pass-1 outcome flag:**
  - F-19 MIDI: **Outcome A** — primary `bytes1.SequenceEqual(bytes2)` gate GREEN on first run. Plan 04's in-process velocity determinism (CONTEXT D-17 local PRNG) extended cleanly through DryWetMidi without any required fix.
  - F-20 WAV: **Outcome B (gap-fix bundled)** — primary `bytes1.SequenceEqual(bytes2)` RED on first run with same byte length (352844 vs 352844) and divergence beginning at byte 49. Investigation surfaced two pre-existing static unseeded RNGs in audio code; both reseeded as a minimal gap-fix per the D-13 bundle clause. After fix, both Facts GREEN consecutively.
- **WAV byte length on net10.0.107:** 352844 bytes (16-bit stereo PCM, 44 byte header + sample data for the 3-hit euclidean Sequence under tempo 120 / 4/4). Diagnostic only — not asserted in source.
- **Plan 15-03 SUMMARY pre-existing-bug visibility:** the dither RNG was already documented at `15-03-SUMMARY.md:117-118` ("FileIO.cs:220-221 uses a static shared `Random` for TPDF dither noise added before int16 quantization. Two sequential `writeWav` calls in the same process (regardless of audio content) produce different dither bytes at ~1 LSB."). Plan 15-03 worked around it via `CountDivergentPcmSamples` and trailing-RMS observables; Plan 15-05 completes the closure by fixing the underlying RNG instead. The 15-03 observables remain valid — they assert RMS within 10% of the dither floor, which still holds with deterministic dither.
- **Synth-noise RNG bug discovered fresh by Pass-1 RED on F-20** — was NOT documented anywhere prior. The piano synthesizer's `SynthUtils.GenerateWhiteNoise(transient, 0.025 * note.Velocity)` at `PianoSynthesizer.cs:71` injects a hammer transient that previously varied across renders. Fixed in the same commit as the dither fix to keep the audio-layer determinism gap-fix atomic.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 — Bug] WAV non-determinism from unseeded synth white-noise RNG (`SynthUtils.cs:11`)**
- **Found during:** Task 2 (F-20 SameSeed_ByteIdenticalWav Pass 1)
- **Issue:** `private static readonly Random Rng = new()` at SynthUtils.cs:11 used a time-seeded RNG. Piano hammer transient (`PianoSynthesizer.cs:71`) and saxophone breath noise (`SaxSynthesizer.cs:71`) both call `SynthUtils.GenerateWhiteNoise`, so every renderSong call produced different sample bytes for the same input — violating ROADMAP criterion #2 / D-18 the moment two renders are compared.
- **Fix:** Reseeded with fixed value `0x55EED`; added `public static void ResetNoiseRng()` so SongRenderer can reset the sequence at render boundaries. Decorrelation within a single render still holds (RNG advances normally per sample).
- **Files modified:** `flow-lang/StandardLibrary/Audio/SynthUtils.cs`, `flow-lang/StandardLibrary/Audio/SongRenderer.cs` (3 reset calls at entry to RenderSong, RenderSongWithLambda, RenderSongWithTimeline).
- **Verification:** F-20 GREEN after fix; full suite 287/287 GREEN — zero regression.
- **Committed in:** `af09ce5` (Task 2 commit).

**2. [Rule 1 — Bug] WAV non-determinism from unseeded TPDF dither RNG (`FileIO.cs:11`)**
- **Found during:** Task 2 (F-20 Pass 1, after the synth-noise fix above isolated the dither contribution)
- **Issue:** `private static readonly Random Random = new Random()` at FileIO.cs:11 was time-seeded. Two `writeWav` calls in the same process produced different LSB-level dither bytes — known-since-Plan-15-03, worked around there via RMS observables, gating closure of ROADMAP #2.
- **Fix:** Reseeded with fixed value `0xD17E2`; converted from `readonly` to mutable; added `Random = new Random(DitherSeed)` inline at the start of `ExportWavInternal` so every WAV export starts from the same dither sequence. Same trade-off as the synth-noise fix — decorrelation within a single export holds, cross-export reproducibility gained.
- **Files modified:** `flow-lang/StandardLibrary/Audio/FileIO.cs`.
- **Verification:** F-20 GREEN; consecutive runs both GREEN; Plan 15-03's F-02/F-07/F-08 still GREEN (their RMS-within-10% observables tolerate deterministic dither just as easily as time-varying dither).
- **Committed in:** `af09ce5` (bundled with the Task 2 commit per the Phase 14 D-13 divergence-bundle clause).

**3. [Rule 1 — Test-source bug] `Buffer buf = ...` collided with `Buf` token at parse time**
- **Found during:** Task 2 (Pass 1 of F-20)
- **Issue:** The plan's drafted source used `Buffer buf = (renderSong song "piano")` but Flow's lexer tokenizes `buf` as a `Buf` keyword (or a stem of one), producing `error: Expected variable name. Got Buf 'buf'`. Plan 03's `ReverbTimeRenderTests.cs` had already adopted `Buffer rendered = ...` for the same reason — the plan-time draft simply replicated a documented hazard.
- **Fix:** Renamed `buf` → `rendered` in both `sourceRun1` and the `Replace("run1", "run2")` derivation. One-line surface fix.
- **Files modified:** `flow-lang.Tests/Integration/Phase15/EuclideanByteIdenticalTests.cs`.
- **Verification:** Script parses cleanly, F-20 progresses past `RunSource` to the byte comparison.
- **Committed in:** `af09ce5` (folded into the Task 2 commit).

---

**Total deviations:** 3 (2 audio-layer bug fixes per Rule 1, 1 test-source naming fix per Rule 1).
**Impact on plan:** All three deviations bundled into the Task 2 commit per the Phase 14 D-13 divergence-bundle clause. Audio-layer fixes touched 3 files outside the plan's `<files>` list (SynthUtils, FileIO, SongRenderer); these were necessary to satisfy the plan's own acceptance criterion ("F-20 GREEN") and ROADMAP criterion #2 ("byte-identical ... WAV"). Plan 15-03's working observable contracts remain valid (the RMS / divergent-sample-count thresholds tolerate either time-varying or deterministic noise floors).

## Issues Encountered

None beyond the three deviations above. Pass-1 RED on F-20 was the canonical Phase-14-D-13 trigger event — surfaced and resolved in the same plan, in the same commit.

## .NET Runtime Pin (for future cross-version comparison)

- **SDK:** `dotnet --version` → `10.0.107`
- **Runtime:** `Microsoft.NETCore.App 10.0.7`
- **Captured velocity bytes (MIDI F-19):** `[122, 70, 108]` for `euclidean(3, 8, C4, swing=0.3, humanize=0.1, seed=42)` rendered through `writeMidi`.
- **WAV byte length (F-20 diagnostic):** 352844 bytes (renderSong with "piano", tempo 120, 4/4, 3-hit Sequence).

If a future .NET 10 patch update changes `System.Random(42).NextDouble()` algorithm output, the empirical-pin assertion in F-19 goes RED and the Phase 15 owner must triage: accept (major-version-equivalent break) or revert the patch update. ROADMAP D-18 explicitly scopes determinism to .NET 10.x patch versions, NOT future major versions.

## Pre-Landing Grep Status

```
$ grep -rn "MidiFile.Read" flow-lang.Tests/
flow-lang.Tests/Shared/MidiReadHelpers.cs:12:        var midiFile = MidiFile.Read(midiPath);
flow-lang.Tests/Shared/MidiReadHelpers.cs:18:        var midiFile = MidiFile.Read(midiPath);
```

Both occurrences are inside `MidiReadHelpers.cs` itself (one for `GetVelocityBytes`, one for `GetNoteNumbers`). Zero duplicate call sites in the test tree — DEFER-05 retroactively validated by F-19 reusing the helper rather than authoring its own `MidiFile.Read` block.

## Test Results

- **Phase 15 filter:** `dotnet test --filter "FullyQualifiedName~Phase15" --nologo` → **27/27 Passed** (25 baseline + 2 new from F-19/F-20).
- **Full suite:** `dotnet test flow-sharp.sln --nologo` → **287/287 Passed** (269 baseline + 12 from Plan 04 already counted + 2 from this plan + 4 unaccounted accumulated since 04 — net zero regressions on this plan's commits).
- **Build:** `dotnet build flow-sharp.sln --nologo` → **0 Errors, 5 Warnings** (all pre-existing, none introduced by this plan).
- **Consecutive run stability:** F-19 and F-20 both pass on two back-to-back runs of the test command (in-process determinism stable).

## Phase 15 Fact Count Delta

- **Plan 04 cumulative:** 25 Phase15 Facts GREEN
- **Plan 05 added:** +2 Facts (F-19 SameSeed_ByteIdenticalMidi, F-20 SameSeed_ByteIdenticalWav)
- **Plan 05 cumulative:** 27 Phase15 Facts GREEN

## ROADMAP Criterion #2 Status

> "Rendering the same `euclidean(…, humanize, seed)` call twice produces byte-identical MIDI and WAV output."

- **MIDI half:** observable via F-19. Cross-file `SequenceEqual` gate + empirical-velocity-byte pin. GREEN.
- **WAV half:** observable via F-20. Cross-file `SequenceEqual` gate. GREEN.
- **Audio-layer dependencies:** synth white-noise RNG and TPDF dither RNG both reseeded; no remaining static unseeded `Random` in `flow-lang/StandardLibrary/Audio/`.

ROADMAP criterion #2 is observable and Shipped at this plan's commit (`af09ce5`).

## Next Phase / Plan Readiness

- **Plan 15-06** (next plan in Wave 3) authors the .flow scripts that exercise euclidean swing/humanize end-to-end via the Theory harness. Independent of this plan's audio-layer changes (the test_euclidean_humanize.flow script will write a single MIDI file and assert sentinels — no cross-render comparison).
- **Plan 15-07** (Wave 4 closure) will reference both F-19 and F-20 in the rollup, plus the `MidiFile.Read` ≤ 1-file invariant from this plan.
- Future audio refactors must preserve the fixed-seed RNG contract: any new static `Random` field in `flow-lang/StandardLibrary/Audio/` should follow the same pattern (fixed seed + reset hook called at the public boundary).

## Self-Check: PASSED

Verified all claims:

- `flow-lang.Tests/Integration/Phase15/EuclideanByteIdenticalTests.cs`: FOUND
- Commit `10c9557` (Task 1, F-19): FOUND in `git log`
- Commit `af09ce5` (Task 2, F-20 + audio fix): FOUND in `git log`
- `grep -c "MidiReadHelpers.GetVelocityBytes" flow-lang.Tests/Integration/Phase15/EuclideanByteIdenticalTests.cs`: **1**
- `grep -c "MidiFile.Read" flow-lang.Tests/Integration/Phase15/EuclideanByteIdenticalTests.cs`: **0**
- `grep -Fc "SequenceEqual" flow-lang.Tests/Integration/Phase15/EuclideanByteIdenticalTests.cs`: **3** (1 MIDI Fact + 2 WAV Fact — exceeds the ≥1 requirement)
- `grep -c "SameSeed_ByteIdenticalWav" flow-lang.Tests/Integration/Phase15/EuclideanByteIdenticalTests.cs`: **1**
- `grep -c "phase15_seed42_run1.wav\|phase15_seed42_run2.wav" flow-lang.Tests/Integration/Phase15/EuclideanByteIdenticalTests.cs`: **3**
- `grep -c "PASS-2 PLACEHOLDER" flow-lang.Tests/Integration/Phase15/EuclideanByteIdenticalTests.cs`: **0** (placeholder replaced post-Pass-2)
- `dotnet test --filter "FullyQualifiedName~EuclideanByteIdenticalTests" --nologo`: 2/2 Passed
- `dotnet test --filter "FullyQualifiedName~Phase15" --nologo`: 27/27 Passed
- `dotnet test flow-sharp.sln --nologo`: 287/287 Passed
- `git status --short tests/output/`: empty (gitignore effective)

---
*Phase: 15-composer-dx-part-2*
*Plan: 05 (DX-09 byte-identical MIDI/WAV regression)*
*Wave: 3 (parallel with Plan 15-06)*
*Completed: 2026-04-25*
