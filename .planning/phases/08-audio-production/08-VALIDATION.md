---
phase: 8
slug: audio-production
status: passed
nyquist_compliant: true
wave_0_complete: true
created: 2026-04-19
backfilled: true
---

# Phase 8 — Validation Strategy

> Retroactive VALIDATION.md authored under TEST-04 (Phase 13 Nyquist Validation Backfill). Phase 8 shipped without a VALIDATION.md; this file is authored two-pass strict (Pass 1 from `v1.1-REQUIREMENTS.md` + `v1.1-ROADMAP.md` success criteria alone; Pass 2 reconciles against the shipped codebase).

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit.v3 3.2.2 |
| **Config file** | `flow-lang.Tests/flow-lang.Tests.csproj` |
| **Quick run command** | `dotnet test flow-sharp.sln --filter "FullyQualifiedName~Phase08"` |
| **Full suite command** | `dotnet test flow-sharp.sln` |
| **Estimated runtime** | ~20 seconds full suite |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test --filter` scoped to the just-authored Fact class (e.g. `FullyQualifiedName~MixTests`)
- **After every plan wave:** Run `dotnet test flow-sharp.sln`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 60 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 08-backfill-01 | 13-03 | 1 | AUDIO-05 (additive math) | — | `mix` sums samples (not overwrite/average); pure-C# API test on AudioCore.Mix | unit | `dotnet test --filter "FullyQualifiedName~MixTests"` | ✅ | ✅ green |
| 08-backfill-02 | 13-03 | 1 | AUDIO-05 (frame count) | — | `(mix bufA bufB)` returns buffer with 22050 frames (0.5s × 44100Hz) | integration (Theory) | `RequiredSentinels["test_mix.flow"]` | ✅ | ✅ green |
| 08-backfill-03 | 13-03 | 1 | AUDIO-06 (per-section gain) | — | Nested gain contexts render expected frame counts | integration (Theory) | `RequiredSentinels["test_gain_context.flow"]` | ✅ | ✅ green |
| 08-backfill-04 | 13-03 | 1 | AUDIO-06 × FIX-02 (gain-nested bare expr) | — | Cross-ref to Plan 13-01's `SectionGainBareExpressionTests.GainNestedInSection_RendersNonZeroFrames` (intersection of AUDIO-06 and FIX-02 — one Fact covers both) | integration | `dotnet test --filter "FullyQualifiedName~SectionGainBareExpressionTests"` | ✅ (created by 13-01) | ✅ green |
| 08-backfill-05 | 13-03 | 1 | AUDIO-07 | — | `SynthesizerFactory.Create("strings"\|"organ"\|"bell")` returns the expected synthesizer type | unit | `dotnet test --filter "FullyQualifiedName~SynthesizerFactoryTests"` | ✅ | ✅ green |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [x] `flow-lang.Tests/Unit/Phase08/` — NEW subdirectory (created 2026-04-20 by Plan 13-03)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Perceptual distinctness of strings / organ / bell timbres | AUDIO-07 | Subjective — requires listening to distinguish timbre character | Render with each preset via `dotnet run --project flow-interpreter tests/test_synth_presets.flow`, listen to output WAV, confirm each is musically distinct |

---

## Observable Invariants

Each invariant is a concrete check that would fail if the Phase 8 feature were removed:

1. **AUDIO-05 (additive math):** A pure-C# Unit Fact calling `AudioCore.Mix(bufA_with_sample_0.5, bufB_with_sample_0.3)` produces an output buffer whose first sample equals `0.8f` within float32 precision. This pins ADDITIVE semantics (not overwrite or average).
2. **AUDIO-05 (frame count):** stdout of `tests/test_mix.flow` contains `"mix frames: 22050"` — deterministic at 0.5s × 44100Hz.
3. **AUDIO-06 (per-section gain):** stdout of `tests/test_gain_context.flow` contains sentinels proving nested `gain` contexts evaluated.
4. **AUDIO-06 × FIX-02 (gain-nested bare expr, cross-phase invariant):** See Plan 13-01's `SectionGainBareExpressionTests.GainNestedInSection_RendersNonZeroFrames` — that single Fact covers both AUDIO-06 and FIX-02 since the AUDIT integration gap was their intersection.
5. **AUDIO-07:** `SynthesizerFactory.Create("strings")` returns a `StringsSynthesizer` instance; `"organ"` returns `OrganSynthesizer`; `"bell"` returns `BellSynthesizer`. Deterministic structural check, no audio required.

---

## Pass 1 Draft (Requirements-First)

Authored by reading ONLY `v1.1-REQUIREMENTS.md` + the Phase 8 success criteria from `v1.1-ROADMAP.md` (lines 44–52). `.flow` source, `flow-lang/` source, phase SUMMARY/PLAN/RESEARCH/CONTEXT files, and existing test code were NOT consulted during this pass. Per D-13, any reality-correction happens in Pass 2 and is logged in `## Divergences`.

- **AUDIO-05:** expected `mix(bufferA, bufferB)` to layer the two buffers — REQUIREMENTS says "layers two audio buffers by summing samples", so additive semantics (not overwrite/average/min/max). Two observable pins: a pure-C# Unit Fact asserting `0.5 + 0.3 == 0.8` at the sample-math level, plus a frame-count sentinel on `tests/test_mix.flow` producing `"mix frames: 22050"` at a 0.5s × 44100Hz reference.
- **AUDIO-06:** expected per-section gain in `Song` rendering to produce measurable volume differences between sections (e.g., quiet intro + loud chorus — per ROADMAP success criterion 2). Observable pin: stdout sentinels on `tests/test_gain_context.flow` confirming nested `gain` blocks evaluated their bodies. The AUDIO-06 × FIX-02 intersection (bare expressions inside `section { gain N { ... } }`, the audit-surfaced composition gap) is covered by Plan 13-01's `SectionGainBareExpressionTests.GainNestedInSection_RendersNonZeroFrames` — no duplicate Fact authored here; cross-reference only.
- **AUDIO-07:** expected setting instrument to `"strings"`, `"organ"`, or `"bell"` to select a different synthesizer class per preset name — REQUIREMENTS says "detuned saws" / "Hammond additive" / "Risset inharmonic partials" describing timbres, so three distinct classes. Observable pin: a pure-C# Theory Fact over `SynthesizerFactory.Create(presetName)` asserting the return type is `StringsSynthesizer` / `OrganSynthesizer` / `BellSynthesizer` respectively. No audio rendering required — structural dispatch check.

---

## Pass 2 Implementation Map

Reality check + test authoring performed 2026-04-20 against the post-v1.1 codebase at HEAD.

- **AUDIO-05 (additive math):** `flow-lang.Tests/Unit/Phase08/MixTests.cs::Mix_SumsSamples_AdditiveSemantics` — pure-C# API test. Constructs two 1-frame mono `AudioBuffer`s (sample 0.5 and 0.3), wraps each with `Value.Buffer(...)` to match the built-in dispatcher's `IReadOnlyList<Value>` arg shape, calls `AudioCore.Mix(args)`, unwraps the returned `Value.As<AudioBuffer>()`, asserts first sample == `0.8f` (precision 4). Anchor: `flow-lang/StandardLibrary/Audio/AudioCore.cs:200` — `result.Data[i] = sampleA + sampleB`.
- **AUDIO-05 (frame count):** `flow-lang.Tests/FlowScriptData.cs::RequiredSentinels["test_mix.flow"]` — pinned on `"mix frames: 22050"` (0.5s × 44100Hz) + `"mix channels: 2"` (empirical: `createSineTone` produces stereo buffers) + `"mix tests passed"` whole-run gate.
- **AUDIO-06 (per-section gain):** `flow-lang.Tests/FlowScriptData.cs::RequiredSentinels["test_gain_context.flow"]` — pinned on `"gain 0.5 block executed"` + `"nested gain context executed"` + `"quiet section frames: 88200"` (deterministic 2s × 44100Hz render from 4 quarter notes at 120bpm) + `"gain context tests passed"`.
- **AUDIO-06 × FIX-02 (intersection):** covered by Plan 13-01's `flow-lang.Tests/Integration/Phase06/SectionGainBareExpressionTests.cs::GainNestedInSection_RendersNonZeroFrames`. No duplicate Fact authored.
- **AUDIO-07:** `flow-lang.Tests/Unit/Phase08/SynthesizerFactoryTests.cs::Create_ReturnsExpectedSynthesizerType` — Theory over 3 preset names (`"strings"`, `"organ"`, `"bell"`) asserts correct synthesizer class dispatch via `SynthesizerFactory.Create(string)`. Anchor: `flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs:231-233`. Supplementary Theory sentinels on `test_synth_presets.flow` pin end-to-end render success.

---

## Divergences

Three reality-corrections applied in Pass 2:

- **AUDIO-05 / AudioCore.Mix signature:** Pass 1 interfaces block drafted `AudioCore.Mix(params Value[] buffers)` or `AudioCore.Mix(AudioBuffer, AudioBuffer)`. Pass 2 read `flow-lang/StandardLibrary/Audio/AudioCore.cs:170` and found the real signature is `public static Value Mix(IReadOnlyList<Value> args)` — the standard built-in dispatcher shape. The Fact wraps `AudioBuffer` instances via `Value.Buffer(bufA)` / `Value.Buffer(bufB)` into a 2-element array before invocation. Assertion math (0.5 + 0.3 == 0.8) ships unchanged.

- **AUDIO-07 / SynthesizerFactory namespace:** Pass 1 draft guessed `using FlowLang.StandardLibrary.Audio.Synthesizers;` as the single import. Pass 2 read `flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs:5` and found `SynthesizerFactory` lives in `FlowLang.StandardLibrary.Audio` (the outer namespace), while the three synth classes (`StringsSynthesizer`, `OrganSynthesizer`, `BellSynthesizer`) live in `FlowLang.StandardLibrary.Audio.Synthesizers` (the inner namespace). The Fact imports BOTH namespaces. Class names matched the draft verbatim (no `HammondOrganSynthesizer`-style substitution needed).

- **AUDIO-05 / test_mix.flow stereo channel count:** Pass 1 draft for `RequiredSentinels["test_mix.flow"]` proposed `"mix channels: 1"` — reasoning from "mono-to-stereo promotion is a conditional code path". Pass 2 executed `dotnet run --project flow-interpreter tests/test_mix.flow` and captured `"mix channels: 2"`: the test script uses `createSineTone`, which produces stereo buffers, so `mix(stereo, stereo)` returns stereo without triggering the `MonoToStereo` promotion path. Sentinel tightened to the empirical `"mix channels: 2"`. This is a specificity win, not a regression — pins the actual shipped contract.

The two-pass discipline continues to pay off on backfills: drafting from REQUIREMENTS.md + external behavior surfaces signature drift (Mix args shape, SynthesizerFactory namespace) that would otherwise only be caught at compile/run time.

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 60s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved 2026-04-20 (76/76 `dotnet test flow-sharp.sln` green at commit `511085f`)
