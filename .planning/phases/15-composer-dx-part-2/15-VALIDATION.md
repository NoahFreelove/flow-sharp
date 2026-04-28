---
phase: 15
slug: composer-dx-part-2
status: verified
nyquist_compliant: true
wave_0_complete: true
created: 2026-04-20
verified: 2026-04-25
---

# Phase 15 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Source of truth: `15-RESEARCH.md` §Validation Architecture. This file is the executor-facing contract; the planner wires every `<automated>` block against the rows below.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit.v3 3.2.2 + xunit.runner.visualstudio 3.1.5 |
| **Config file** | `flow-lang.Tests/flow-lang.Tests.csproj` (no separate `xunit.runner.json`) |
| **Quick run command** | `dotnet test flow-lang.Tests/flow-lang.Tests.csproj --filter "FullyQualifiedName~Phase15" --nologo` |
| **Full suite command** | `dotnet test flow-sharp.sln --nologo` |
| **Estimated runtime** | ~10s Phase 15 filter, ~30s full suite |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test flow-lang.Tests/flow-lang.Tests.csproj --filter "FullyQualifiedName~Phase15" --nologo`
- **After every plan wave:** Run `dotnet test flow-sharp.sln --nologo`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 30 seconds

---

## Per-Task Verification Map

> Planner assigns each row to a specific Plan/Task ID. Until planner assignment, Plan/Task cells remain TBD. All automated rows MUST be wired in `<automated>` blocks with these exact filter commands.

| Fact ID | Req / Decision | Behavior | Test Type | Automated Command | File | Status |
|---------|----------------|----------|-----------|-------------------|------|--------|
| F-01 | DX-07 / D-01 | `reverbTime 2.5 { }` parses and stores in context | unit | `dotnet test --filter "ReverbTimeContextTests.Parse_Positive_StoresInContext"` | `flow-lang.Tests/Unit/Phase15/ReverbTimeContextTests.cs` (Plan 15-02) | ✅ green |
| F-02 | DX-07 / D-02 | `reverbTime 0 { }` produces dry output (short-circuits Reverb.Apply) | integration | `dotnet test --filter "ReverbTimeRenderTests.Zero_ShortCircuitsReverb"` | `flow-lang.Tests/Integration/Phase15/ReverbTimeRenderTests.cs` (Plan 15-03 Task 2) | ✅ green |
| F-03 | DX-07 / D-03 negative | `reverbTime -2.5 { }` raises parse error — contains "reverbTime cannot be negative" | unit | `dotnet test --filter "ReverbTimeContextTests.Parse_Negative_ParseError"` | `flow-lang.Tests/Unit/Phase15/ReverbTimeContextTests.cs` (Plan 15-02) | ✅ green |
| F-04 | DX-07 / D-03 clamp | `reverbTime 45 { }` silently clamps to 30.0; assert `GetMusicalContext().ReverbTime == 30.0` | unit | `dotnet test --filter "ReverbTimeContextTests.Parse_AboveMax_ClampsTo30"` | `flow-lang.Tests/Unit/Phase15/ReverbTimeContextTests.cs` (Plan 15-02) | ✅ green |
| F-05 | DX-07 / D-04 | Nested `gain 0.5 { reverbTime 2.0 { } }` resolves both axes independently at innermost frame | unit | `dotnet test --filter "ReverbTimeContextTests.Nested_WithGain_Independent"` | `flow-lang.Tests/Unit/Phase15/ReverbTimeContextTests.cs` (Plan 15-02) | ✅ green |
| F-06 | DX-07 / D-13 | `Reverb.Apply(buffer, rt60, damping, mix)` overload decays impulse to ~-60dB at t=rt60 (±3dB) | unit | `dotnet test --filter "ReverbApplyRt60Tests.Rt60_ProducesExpectedDecay"` | `flow-lang.Tests/Unit/Phase15/ReverbApplyRt60Tests.cs` (Plan 15-03 Task 1) | ✅ green |
| F-07 | DX-07 / D-14 | Per-voice reverb applied in SongRenderer voice loop — divergent-sample count > 50% vs no-reverb reference | integration | `dotnet test --filter "ReverbTimeRenderTests.PerVoice_Applies"` | `flow-lang.Tests/Integration/Phase15/ReverbTimeRenderTests.cs` (Plan 15-03 Task 2) | ✅ green |
| F-08 | DX-07 / D-16 | Explicit `reverb()` + context `reverbTime` stack (both apply) | integration | `dotnet test --filter "ReverbTimeRenderTests.Explicit_And_Context_Stack"` | `flow-lang.Tests/Integration/Phase15/ReverbTimeRenderTests.cs` (Plan 15-03 Task 2) | ✅ green |
| F-09 | DX-09 / D-05 range | `swing = 1.5` clamps to 1.0; max accent == base + 1.0 | unit | `dotnet test --filter "EuclideanSwingTests.Swing_AboveMax_ClampsTo1"` | `flow-lang.Tests/Unit/Phase15/EuclideanSwingTests.cs` (Plan 15-04 Task 2) | ✅ green |
| F-10 | DX-09 / D-05,D-08 negative | `swing = -0.3` anti-accents off-beats > on-beats | unit | `dotnet test --filter "EuclideanSwingTests.NegativeSwing_AccentsOffBeats"` | `flow-lang.Tests/Unit/Phase15/EuclideanSwingTests.cs` (Plan 15-04 Task 2) | ✅ green |
| F-11 | DX-09 / D-06 | On-beat detection matches step-grid for (3,8) and (5,8) patterns | unit | `dotnet test --filter "EuclideanSwingTests.OnBeat_DetectionMatchesGrid"` | `flow-lang.Tests/Unit/Phase15/EuclideanSwingTests.cs` (Plan 15-04 Task 2) | ✅ green |
| F-12 | DX-09 / D-07 | `swing = 0.25` adds exactly 0.25 to accented set (raw delta, no multiplier) | unit | `dotnet test --filter "EuclideanSwingTests.AccentAmount_IsRawDelta"` | `flow-lang.Tests/Unit/Phase15/EuclideanSwingTests.cs` (Plan 15-04 Task 2) | ✅ green |
| F-13 | DX-09 / D-08 | Asymmetric accent — only accented set moves, unaccented stays at base | unit | `dotnet test --filter "EuclideanSwingTests.Asymmetric_UnaccentedStaysAtBase"` | `flow-lang.Tests/Unit/Phase15/EuclideanSwingTests.cs` (Plan 15-04 Task 2) | ✅ green |
| F-14 | DX-09 / D-09 | `humanize = 0.1` jitter stays within `[base - 0.1, base + 0.1]` | unit | `dotnet test --filter "EuclideanHumanizeTests.Humanize_JitterInRange"` | `flow-lang.Tests/Unit/Phase15/EuclideanHumanizeTests.cs` (Plan 15-04 Task 2) | ✅ green |
| F-15 | DX-09 / D-10 | `humanize = 2.0` clamps to 1.0 | unit | `dotnet test --filter "EuclideanHumanizeTests.Humanize_AboveMax_ClampsTo1"` | `flow-lang.Tests/Unit/Phase15/EuclideanHumanizeTests.cs` (Plan 15-04 Task 2) | ✅ green |
| F-16 | DX-09 / D-11 | Uniform distribution over `[-humanize, +humanize]` (10 buckets over 1000 samples within ±30% of expected count; humanize narrowed to 0.3 to keep range inside [0,1]) | unit | `dotnet test --filter "EuclideanHumanizeTests.Humanize_Uniform_NotGaussian"` | `flow-lang.Tests/Unit/Phase15/EuclideanHumanizeTests.cs` (Plan 15-04 Task 2) | ✅ green |
| F-17 | DX-09 / D-12 | Perturbed velocity clamps to `[0, 1]` — `dynamics ff` base 0.875 + jitter saturates at 1.0, does NOT wrap | unit | `dotnet test --filter "EuclideanHumanizeTests.Humanize_Overflow_Clamps"` | `flow-lang.Tests/Unit/Phase15/EuclideanHumanizeTests.cs` (Plan 15-04 Task 2) | ✅ green |
| F-18 | DX-09 / D-17 | Local PRNG isolation — `euclidean(seed=42)` → `vary(seed=99)` → `euclidean(seed=42)` produces byte-identical first and third outputs | unit | `dotnet test --filter "EuclideanHumanizeTests.LocalPrng_IsolatedAcrossCalls"` | `flow-lang.Tests/Unit/Phase15/EuclideanHumanizeTests.cs` (Plan 15-04 Task 2) | ✅ green |
| F-19 | DX-09 / D-18 | Two renders of identical script produce byte-identical MIDI bytes (empirical Pass-2 pin `[122, 70, 108]` on net10.0.107) | integration | `dotnet test --filter "EuclideanByteIdenticalTests.SameSeed_ByteIdenticalMidi"` | `flow-lang.Tests/Integration/Phase15/EuclideanByteIdenticalTests.cs` (Plan 15-05) | ✅ green |
| F-20 | DX-09 / D-18 | Two renders of identical script produce byte-identical WAV bytes (352844 bytes both runs; required reseeding SynthUtils + FileIO RNGs) | integration | `dotnet test --filter "EuclideanByteIdenticalTests.SameSeed_ByteIdenticalWav"` | `flow-lang.Tests/Integration/Phase15/EuclideanByteIdenticalTests.cs` (Plan 15-05) | ✅ green |
| F-21 | ROADMAP #1 | `swing` changes velocity only — note durations identical at `swing=0` vs `swing=0.5` | unit | `dotnet test --filter "EuclideanSwingTests.Swing_ChangesVelocity_NotTiming"` | `flow-lang.Tests/Unit/Phase15/EuclideanSwingTests.cs` (Plan 15-04 Task 2) | ✅ green |
| F-22 | ROADMAP #4 | 8th `GetMusicalContext` field walks correctly — ReverbTime at root resolves from innermost frame | unit | `dotnet test --filter "ReverbTimeContextTests.GetMusicalContext_AllFieldsResolvedSearchesReverbTime"` | `flow-lang.Tests/Unit/Phase15/ReverbTimeContextTests.cs` (Plan 15-02) | ✅ green |
| F-23 | ROADMAP #4 | Nested `reverbTime` inside `tempo`/`key` resolves | unit | `dotnet test --filter "ReverbTimeContextTests.Nested_InsideTempoAndKey_Resolves"` | `flow-lang.Tests/Unit/Phase15/ReverbTimeContextTests.cs` (Plan 15-02) | ✅ green |
| F-24 | ROADMAP #5 | Pre-landing grep of `examples/`, `tests/`, `flow-lang/*.flow` for `reverbTime` — hits only in `tests/test_reverb_time.flow` (zero stdlib/.flow/examples collisions) | manual | `grep -rn "reverbTime" examples/ tests/ flow-lang/*.flow` | recorded in `.planning/phases/15-composer-dx-part-2/15-VERIFICATION.md` (Plan 15-07) | ✅ green (manual; pinned in 15-VERIFICATION.md) |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

**⚠️ Planner note:** Row F-02 (dry short-circuit via `reverbTime 0`) honors CONTEXT D-02 and contradicts ROADMAP success criterion #3 wording ("rejects negative or zero"). Plan to correct ROADMAP wording as a doc-only deliverable during phase closure (analogous to Phase 12 TEST-03 REQUIREMENTS reframe). Do NOT error on `reverbTime 0`.

---

## Wave 0 Requirements

Items that MUST exist before any plan's Wave 1 can run. Planner MUST include a Wave 0 plan (or wave-0 tasks in Plan 01) covering:

- [x] `flow-lang.Tests/Unit/Phase15/` — new directory for unit test files (Plan 15-01)
- [x] `flow-lang.Tests/Integration/Phase15/` — new directory for integration test files (Plan 15-01)
- [x] `flow-lang.Tests/Shared/MidiReadHelpers.cs` — shared helper promoted from Phase 14 inline pattern (DEFER-05 trigger, CLOSED 2026-04-21 by Plan 15-01). Required signature:
  ```csharp
  internal static class MidiReadHelpers
  {
      public static byte[] GetVelocityBytes(string midiPath);
      public static int[] GetNoteNumbers(string midiPath);
      public static byte[] ReadAllBytes(string midiPath);
  }
  ```
  Reused by `EuclideanByteIdenticalTests` (F-19) and Phase 14's `DynamicsMidiVelocityTests` (refactored from inline). Confirmed in service across both phases.
- [x] `tests/output/.gitignore` — entry for new WAV/MIDI regression artifacts generated by F-19/F-20 (regenerate on each run; do NOT commit binary artifacts) (Plan 15-01)
- [x] `tests/test_reverb_time.flow` — script-level sanity test + Theory row in `FlowScriptData.cs` (Plan 15-01 placeholder, Plan 15-03 real body)
- [x] `tests/test_euclidean_swing.flow` — script-level sanity test + Theory row (Plan 15-01 placeholder, Plan 15-06 real body)
- [x] `tests/test_euclidean_humanize.flow` — script-level sanity test + Theory row (Plan 15-01 placeholder, Plan 15-06 real body)

No framework install needed; xUnit.v3 is already in `flow-lang.Tests.csproj`. No new NuGet packages.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| `reverbTime` identifier collision pre-check | ROADMAP #5 | One-shot grep across user-authored `.flow` files; no feedback loop value from automating | `grep -rn "\breverbTime\b" examples/ tests/ flow-lang/*.flow` — assert 0 hits or rename collisions before landing. Transcript pinned in `15-VERIFICATION.md`. Matches Phase 14 D-21 pattern. |

---

## Validation Sign-Off

- [x] Every task (except Wave 0 scaffold) has `<automated>` verify or a Wave 0 dependency
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all ❌ W0 references above (Phase15 test directories, `MidiReadHelpers`, `.gitignore`, script-level `.flow` Theory rows)
- [x] No watch-mode flags in test commands
- [x] Feedback latency < 30s
- [x] `nyquist_compliant: true` set in frontmatter after full Fact wiring

## Fact-Wiring Evidence

All 24 Facts (F-01..F-24) wired and GREEN as of 2026-04-25. F-01..F-23 are
xUnit-automated under `flow-lang.Tests/{Unit,Integration}/Phase15/`; F-24
is the manual collision grep with the verbatim transcript pinned in
[15-VERIFICATION.md §F-24 Pre-Landing Collision Grep — Transcript](./15-VERIFICATION.md).

| Source | Plan | Facts | Status |
|--------|------|-------|--------|
| `Unit/Phase15/ReverbTimeContextTests.cs` | 15-02 | F-01, F-03, F-04, F-05, F-22, F-23 + `Parse_Zero_ProducesDry` | 7/7 GREEN |
| `Unit/Phase15/ReverbApplyRt60Tests.cs` | 15-03 | F-06 + `Rt60_Zero_DoesNotThrow` + `Rt60_ExistingOverloadUnchanged` | 3/3 GREEN |
| `Integration/Phase15/ReverbTimeRenderTests.cs` | 15-03 | F-02, F-07, F-08 | 3/3 GREEN |
| `Unit/Phase15/EuclideanSwingTests.cs` | 15-04 | F-09, F-10, F-11, F-12, F-13, F-21 | 6/6 GREEN |
| `Unit/Phase15/EuclideanHumanizeTests.cs` | 15-04 | F-14, F-15, F-16, F-17, F-18 + `SameSeed_ProducesIdenticalVelocities` | 6/6 GREEN |
| `Integration/Phase15/EuclideanByteIdenticalTests.cs` | 15-05 | F-19, F-20 | 2/2 GREEN |
| `tests/test_reverb_time.flow` (FlowScriptData Theory) | 15-01/15-03 | sentinel rows | GREEN |
| `tests/test_euclidean_swing.flow` (FlowScriptData Theory) | 15-01/15-06 | sentinel rows | GREEN |
| `tests/test_euclidean_humanize.flow` (FlowScriptData Theory) | 15-01/15-06 | sentinel rows | GREEN |
| F-24 manual collision grep | 15-07 | transcript pinned in 15-VERIFICATION.md | pinned |
| **Total** | — | **30 automated + 1 manual** | **GREEN** |

Full-suite count at Phase 15 close: **287/287** (`dotnet test
flow-sharp.sln --nologo`).

**Approval:** approved 2026-04-25
