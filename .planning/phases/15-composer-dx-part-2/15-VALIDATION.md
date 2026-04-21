---
phase: 15
slug: composer-dx-part-2
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-04-20
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
| F-01 | DX-07 / D-01 | `reverbTime 2.5 { }` parses and stores in context | unit | `dotnet test --filter "ReverbTimeContextTests.Parse_Positive_StoresInContext"` | `flow-lang.Tests/Unit/Phase15/ReverbTimeContextTests.cs` ❌ W0 | ⬜ pending |
| F-02 | DX-07 / D-02 | `reverbTime 0 { }` produces dry output (short-circuits Reverb.Apply) | integration | `dotnet test --filter "ReverbTimeRenderTests.Zero_ShortCircuitsReverb"` | `flow-lang.Tests/Integration/Phase15/ReverbTimeRenderTests.cs` ❌ W0 | ⬜ pending |
| F-03 | DX-07 / D-03 negative | `reverbTime -2.5 { }` raises parse error — contains "reverbTime cannot be negative" | unit | `dotnet test --filter "ReverbTimeContextTests.Parse_Negative_ParseError"` | `flow-lang.Tests/Unit/Phase15/ReverbTimeContextTests.cs` ❌ W0 | ⬜ pending |
| F-04 | DX-07 / D-03 clamp | `reverbTime 45 { }` silently clamps to 30.0; assert `GetMusicalContext().ReverbTime == 30.0` | unit | `dotnet test --filter "ReverbTimeContextTests.Parse_AboveMax_ClampsTo30"` | `flow-lang.Tests/Unit/Phase15/ReverbTimeContextTests.cs` ❌ W0 | ⬜ pending |
| F-05 | DX-07 / D-04 | Nested `gain 0.5 { reverbTime 2.0 { } }` resolves both axes independently at innermost frame | unit | `dotnet test --filter "ReverbTimeContextTests.Nested_WithGain_Independent"` | `flow-lang.Tests/Unit/Phase15/ReverbTimeContextTests.cs` ❌ W0 | ⬜ pending |
| F-06 | DX-07 / D-13 | `Reverb.Apply(buffer, rt60, damping, mix)` overload decays impulse to ~-60dB at t=rt60 (±3dB) | unit | `dotnet test --filter "ReverbApplyRt60Tests.Rt60_ProducesExpectedDecay"` | `flow-lang.Tests/Unit/Phase15/ReverbApplyRt60Tests.cs` ❌ W0 | ⬜ pending |
| F-07 | DX-07 / D-14 | Per-voice reverb applied in SongRenderer voice loop — WAV tail lengthens vs no-reverb reference | integration | `dotnet test --filter "ReverbTimeRenderTests.PerVoice_Applies"` | `flow-lang.Tests/Integration/Phase15/ReverbTimeRenderTests.cs` ❌ W0 | ⬜ pending |
| F-08 | DX-07 / D-16 | Explicit `reverb()` + context `reverbTime` stack (both apply) | integration | `dotnet test --filter "ReverbTimeRenderTests.Explicit_And_Context_Stack"` | `flow-lang.Tests/Integration/Phase15/ReverbTimeRenderTests.cs` ❌ W0 | ⬜ pending |
| F-09 | DX-09 / D-05 range | `swing = 1.5` clamps to 1.0; max accent == base + 1.0 | unit | `dotnet test --filter "EuclideanSwingTests.Swing_AboveMax_ClampsTo1"` | `flow-lang.Tests/Unit/Phase15/EuclideanSwingTests.cs` ❌ W0 | ⬜ pending |
| F-10 | DX-09 / D-05,D-08 negative | `swing = -0.3` anti-accents off-beats > on-beats | unit | `dotnet test --filter "EuclideanSwingTests.NegativeSwing_AccentsOffBeats"` | `flow-lang.Tests/Unit/Phase15/EuclideanSwingTests.cs` ❌ W0 | ⬜ pending |
| F-11 | DX-09 / D-06 | On-beat detection matches step-grid for (3,8) and (5,8) patterns | unit | `dotnet test --filter "EuclideanSwingTests.OnBeat_DetectionMatchesGrid"` | `flow-lang.Tests/Unit/Phase15/EuclideanSwingTests.cs` ❌ W0 | ⬜ pending |
| F-12 | DX-09 / D-07 | `swing = 0.25` adds exactly 0.25 to accented set (raw delta, no multiplier) | unit | `dotnet test --filter "EuclideanSwingTests.AccentAmount_IsRawDelta"` | `flow-lang.Tests/Unit/Phase15/EuclideanSwingTests.cs` ❌ W0 | ⬜ pending |
| F-13 | DX-09 / D-08 | Asymmetric accent — only accented set moves, unaccented stays at base | unit | `dotnet test --filter "EuclideanSwingTests.Asymmetric_UnaccentedStaysAtBase"` | `flow-lang.Tests/Unit/Phase15/EuclideanSwingTests.cs` ❌ W0 | ⬜ pending |
| F-14 | DX-09 / D-09 | `humanize = 0.1` jitter stays within `[base - 0.1, base + 0.1]` | unit | `dotnet test --filter "EuclideanHumanizeTests.Humanize_JitterInRange"` | `flow-lang.Tests/Unit/Phase15/EuclideanHumanizeTests.cs` ❌ W0 | ⬜ pending |
| F-15 | DX-09 / D-10 | `humanize = 2.0` clamps to 1.0 | unit | `dotnet test --filter "EuclideanHumanizeTests.Humanize_AboveMax_ClampsTo1"` | `flow-lang.Tests/Unit/Phase15/EuclideanHumanizeTests.cs` ❌ W0 | ⬜ pending |
| F-16 | DX-09 / D-11 | Uniform distribution over `[-humanize, +humanize]` (10 buckets over 1000 samples within ±30% of expected count) | unit | `dotnet test --filter "EuclideanHumanizeTests.Humanize_Uniform_NotGaussian"` | `flow-lang.Tests/Unit/Phase15/EuclideanHumanizeTests.cs` ❌ W0 | ⬜ pending |
| F-17 | DX-09 / D-12 | Perturbed velocity clamps to `[0, 1]` — base 0.98 + jitter 0.5 saturates at 1.0, does NOT wrap | unit | `dotnet test --filter "EuclideanHumanizeTests.Humanize_Overflow_Clamps"` | `flow-lang.Tests/Unit/Phase15/EuclideanHumanizeTests.cs` ❌ W0 | ⬜ pending |
| F-18 | DX-09 / D-17 | Local PRNG isolation — `euclidean(seed=42)` → `vary(seed=99)` → `euclidean(seed=42)` produces byte-identical first and third outputs | unit | `dotnet test --filter "EuclideanHumanizeTests.LocalPrng_IsolatedAcrossCalls"` | `flow-lang.Tests/Unit/Phase15/EuclideanHumanizeTests.cs` ❌ W0 | ⬜ pending |
| F-19 | DX-09 / D-18 | Two renders of identical script produce byte-identical MIDI bytes | integration | `dotnet test --filter "EuclideanByteIdenticalTests.SameSeed_ByteIdenticalMidi"` | `flow-lang.Tests/Integration/Phase15/EuclideanByteIdenticalTests.cs` ❌ W0 | ⬜ pending |
| F-20 | DX-09 / D-18 | Two renders of identical script produce byte-identical WAV bytes | integration | `dotnet test --filter "EuclideanByteIdenticalTests.SameSeed_ByteIdenticalWav"` | `flow-lang.Tests/Integration/Phase15/EuclideanByteIdenticalTests.cs` ❌ W0 | ⬜ pending |
| F-21 | ROADMAP #1 | `swing` changes velocity only — note durations identical at `swing=0` vs `swing=0.5` | unit | `dotnet test --filter "EuclideanSwingTests.Swing_ChangesVelocity_NotTiming"` | `flow-lang.Tests/Unit/Phase15/EuclideanSwingTests.cs` ❌ W0 | ⬜ pending |
| F-22 | ROADMAP #4 | 8th `GetMusicalContext` field walks correctly — ReverbTime at root resolves from innermost frame | unit | `dotnet test --filter "ReverbTimeContextTests.GetMusicalContext_AllFieldsResolvedSearchesReverbTime"` | `flow-lang.Tests/Unit/Phase15/ReverbTimeContextTests.cs` ❌ W0 | ⬜ pending |
| F-23 | ROADMAP #4 | Nested `reverbTime` inside `tempo`/`key` resolves | unit | `dotnet test --filter "ReverbTimeContextTests.Nested_InsideTempoAndKey_Resolves"` | `flow-lang.Tests/Unit/Phase15/ReverbTimeContextTests.cs` ❌ W0 | ⬜ pending |
| F-24 | ROADMAP #5 | Pre-landing grep of `examples/`, `tests/`, `flow-lang/*.flow` for `reverbTime` — zero hits (transcript pinned in 15-VERIFICATION.md) | manual | `grep -rn "reverbTime" examples/ tests/ flow-lang/*.flow` | recorded in `.planning/phases/15-composer-dx-part-2/15-VERIFICATION.md` | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

**⚠️ Planner note:** Row F-02 (dry short-circuit via `reverbTime 0`) honors CONTEXT D-02 and contradicts ROADMAP success criterion #3 wording ("rejects negative or zero"). Plan to correct ROADMAP wording as a doc-only deliverable during phase closure (analogous to Phase 12 TEST-03 REQUIREMENTS reframe). Do NOT error on `reverbTime 0`.

---

## Wave 0 Requirements

Items that MUST exist before any plan's Wave 1 can run. Planner MUST include a Wave 0 plan (or wave-0 tasks in Plan 01) covering:

- [ ] `flow-lang.Tests/Unit/Phase15/` — new directory for unit test files
- [ ] `flow-lang.Tests/Integration/Phase15/` — new directory for integration test files
- [ ] `flow-lang.Tests/Shared/MidiReadHelpers.cs` — shared helper promoted from Phase 14 inline pattern (DEFER-05 trigger). Required signature:
  ```csharp
  internal static class MidiReadHelpers
  {
      public static byte[] GetVelocityBytes(string midiPath);
      public static int[] GetNoteNumbers(string midiPath);
      public static byte[] ReadAllBytes(string midiPath);
  }
  ```
  Reused by `EuclideanByteIdenticalTests` (F-19) and any future phase that asserts byte-identical MIDI output.
- [ ] `tests/output/.gitignore` — entry for new WAV/MIDI regression artifacts generated by F-19/F-20 (regenerate on each run; do NOT commit binary artifacts)
- [ ] `tests/test_reverb_time.flow` — script-level sanity test + Theory row in `FlowScriptData.cs`
- [ ] `tests/test_euclidean_swing.flow` — script-level sanity test + Theory row
- [ ] `tests/test_euclidean_humanize.flow` — script-level sanity test + Theory row

No framework install needed; xUnit.v3 is already in `flow-lang.Tests.csproj`. No new NuGet packages.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| `reverbTime` identifier collision pre-check | ROADMAP #5 | One-shot grep across user-authored `.flow` files; no feedback loop value from automating | `grep -rn "\breverbTime\b" examples/ tests/ flow-lang/*.flow` — assert 0 hits or rename collisions before landing. Transcript pinned in `15-VERIFICATION.md`. Matches Phase 14 D-21 pattern. |

---

## Validation Sign-Off

- [ ] Every task (except Wave 0 scaffold) has `<automated>` verify or a Wave 0 dependency
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all ❌ W0 references above (Phase15 test directories, `MidiReadHelpers`, `.gitignore`, script-level `.flow` Theory rows)
- [ ] No watch-mode flags in test commands
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter after full Fact wiring

**Approval:** pending
