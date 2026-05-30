---
phase: 46-codebase-bloat-removal
plan: 06
subsystem: audio-synthesis
tags: [cleanup, refactor, byte-contract, synth, D-03]
requires:
  - "46-01 (Wave 0 exact-byte guard: NoteSynthesizerByteGuardTests)"
provides:
  - "NoteSynthesizer with all 8 private duplicate helpers removed (BeatsToSeconds + CreateSilence redirected to SynthUtils)"
  - "Oscillator byte contract preserved byte-for-byte (inline math retained per documented D-03 fallback)"
affects:
  - "flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs"
tech-stack:
  added: []
  patterns:
    - "Redirect duplicate internal plumbing to a shared util only where it is provably byte-identical; retain divergent math behind the exact-byte guard"
key-files:
  created: []
  modified:
    - "flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs"
decisions:
  - "D-03 fallback TAKEN: the Wave 0 exact-byte guard went RED on the full oscillator redirect, so the inline oscillator loops were retained verbatim and only the safe BeatsToSeconds + CreateSilence halves were redirected to SynthUtils."
metrics:
  duration: "~25m"
  completed: "2026-05-30"
  tasks: 1
  files: 1
---

# Phase 46 Plan 06: NoteSynthesizer → SynthUtils Redirect (D-03) Summary

Removed all 8 private duplicate helpers (4× `BeatsToSeconds` + 4× `CreateSilence`) from `NoteSynthesizer.cs` by redirecting to the existing `SynthUtils.BeatsToSeconds` / `SynthUtils.CreateSilence`; the oscillator loops were kept inline (documented D-03 fallback) because `SynthUtils.Generate*` incremental-phase accumulation diverges in IEEE-754 from the inline absolute-time formula — the Wave 0 exact-byte guard confirmed it.

## D-03 Branch Taken: SAFE-HALF FALLBACK (oscillator math retained inline)

This is the byte-sensitive plan. I followed the plan's exact gated procedure:

1. **Step 1 (SAFE — always):** Redirected all four synth classes' `BeatsToSeconds`/`CreateSilence` calls to `SynthUtils.*` and deleted all 8 private helper bodies. Added a `using SynthUtils = FlowLang.StandardLibrary.Audio.Synthesizers.SynthUtils;` alias (SynthUtils lives in the `.Synthesizers` namespace, NoteSynthesizer in the parent `.Audio` namespace).
2. **Step 2 (full oscillator redirect — attempted):** Replaced each inline oscillator loop with the additive `new float[numSamples]` → `SynthUtils.GenerateSine/Saw/Square/Triangle` → `SynthUtils.ToMonoBuffer` shape.
3. **Step 3 (GATE — `dotnet test --filter NoteSynthesizerByteGuard`):** **RED.** The guard reported exact-byte divergence:
   - `[sine]` diverged at sample 2205: expected `1.6674353E-15` (0x26F04D81), got `1.3714517E-12` (0x2BC103C1).
   - `[square]`/`[saw]` diverged at sample 2205 with a full sign flip (±0.126) — the `phase >= 1.0` wrap boundary lands on a different sample than the `(f·t) % 1.0` formula.
   - This is exactly the IEEE-754 incremental-phase-vs-absolute-time drift that 46-RESEARCH §D-03 + Open Q2 (assumption A1) predicted analytically.
4. **Fallback applied (explicit, not silent):** Reverted ONLY the Step-2 oscillator changes — restored the inline `Math.Sin(2π·f·t)` / `(f·t) % 1.0` loops verbatim (they ARE the byte contract for the composer-callable `generateSine`/etc. builtins) — while KEEPING the Step-1 `BeatsToSeconds`/`CreateSilence` redirect. Each retained loop carries an inline comment explaining why it stays inline.
5. **Re-ran the guard → GREEN (5/5).** The guard was NOT edited or weakened.

Net effect: all 8 duplicate helpers removed (the duplication that D-03 targets), the oscillator byte contract preserved byte-for-byte, zero risk. `NoteSynthesizer.cs`: 25 insertions / 56 deletions.

## Tasks

| Task | Name | Commit | Files |
| ---- | ---- | ------ | ----- |
| 1 | Redirect NoteSynthesizer helpers to SynthUtils, gated by Wave 0 byte guard (D-03) | (see final commit) | flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs |

## Verification

- **Byte guard:** `NoteSynthesizerByteGuard` — 5/5 PASS (GREEN) after fallback.
- **Build:** `dotnet build flow-lang/flow-lang.csproj` — 0 errors.
- **Synthesis/render/RMS suite:** filtered run (`Synth|Rms|Phase28|Phase29|NoteSynthesizer|Oscillator|Render`) — 247/247 PASS.
- **Two-run cmp-clean:** sine-synth-driven render (`renderSequenceToVoices "sine"` → `renderBars` → `writeWav`) twice → `cmp` clean (GREEN).
- **`private BeatsToSeconds`/`private CreateSilence` count in NoteSynthesizer.cs:** 0.
- **FlowFunctionSynthesizer (188-208):** untouched (D-deferred section 2.6).

## Deviations from Plan

None — the plan explicitly defined the RED→fallback branch as an acceptance criterion, and that branch was taken as written.

## Pre-existing Failures (NOT introduced here)

The full `flow-lang.Tests` run shows 4 failures per run (set varies by run — flaky/environment-dependent), all OUTSIDE the touched synthesis path and matching the orchestrator's known-failures list:
- Phase48 `WasmDeterminismTests` / `BundleSizeBudgetTests` / `WasmBuildPipelineTests` / `DryWetMidiWasmPublishTests` (Wasm publish + DryWetMidi-under-Wasm).
- Phase38 `OscLoopbackTests.RoundTrip_...` (network loopback timing).
- Phase35 `FlowTestCliTests` (CLI subprocess timing).

None touch NoteSynthesizer or any synthesis/render path; the 247-test synthesis filter is fully green.

## Self-Check: PASSED
- flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs — FOUND (modified, 0 private helpers, oscillator loops inline).
- NoteSynthesizerByteGuard — GREEN (5/5), not edited.
