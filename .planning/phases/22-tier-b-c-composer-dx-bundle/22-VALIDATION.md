---
phase: 22
slug: tier-b-c-composer-dx-bundle
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-05-01
---

# Phase 22 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Sourced from `22-RESEARCH.md` § Validation Architecture (lines 727–822).

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit.v3 3.2.2 (`flow-lang.Tests/flow-lang.Tests.csproj`) |
| **Config file** | `flow-lang.Tests/flow-lang.Tests.csproj` (no separate config) |
| **Quick run command** | `dotnet test flow-lang.Tests/flow-lang.Tests.csproj --filter "FullyQualifiedName~Phase22"` |
| **Full suite command** | `dotnet test flow-sharp.sln` |
| **Estimated runtime** | ~5s (Phase 22 only) / ~30s (full suite) |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test flow-lang.Tests/flow-lang.Tests.csproj --filter "FullyQualifiedName~Phase22"` (Phase 22 Facts only — fast, ~5s)
- **After every plan wave:** Run `dotnet test flow-sharp.sln` (full suite — ~30s)
- **Before `/gsd-verify-work`:** Full suite green AND `ByteIdenticalTutorialTests` + `ByteIdenticalShowcaseTests` 19/19 GREEN
- **Max feedback latency:** 30 seconds

---

## Per-Task Verification Map

Maps each phase requirement to its automated verification. Plan IDs (22-01..22-07) are derived from RESEARCH's recommended decomposition; the planner may merge/reflow but every REQ-ID must remain covered.

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 22-01-W0 | 01 | 1 | DX-10 | — | N/A | unit (RED) | `dotnet test --filter "ArpeggioFacts"` | ❌ W0 | ⬜ pending |
| 22-01-01 | 01 | 1 | DX-10 | — | N/A | unit (GREEN) | `dotnet test --filter "ArpeggioFacts.UpLinear_FourNoteAscent"` | ❌ W0 | ⬜ pending |
| 22-01-02 | 01 | 1 | DX-10 | — | N/A | unit | `dotnet test --filter "ArpeggioFacts.DirectionDownReversesNotes"` | ❌ W0 | ⬜ pending |
| 22-01-03 | 01 | 1 | DX-10 | — | N/A | integration | `dotnet run --project flow-interpreter tests/test_dx_arpeggio.flow` | ❌ W0 | ⬜ pending |
| 22-02-W0 | 02 | 2 | DX-15 | T-22-V5 | range-validate ratio>0 | unit (RED) | `dotnet test --filter "LoadWavVarispeedFacts"` | ❌ W0 | ⬜ pending |
| 22-02-01 | 02 | 2 | DX-15 | — | N/A | unit | `dotnet test --filter "LoadWavVarispeedFacts.TwelveSemitones_HalvesFrames"` | ❌ W0 | ⬜ pending |
| 22-02-02 | 02 | 2 | DX-15 | — | N/A | unit | `dotnet test --filter "LoadWavVarispeedFacts.RatioOverload_RescalesFrames"` | ❌ W0 | ⬜ pending |
| 22-02-03 | 02 | 2 | DX-15 | — | byte-identical regression | unit | `dotnet test --filter "LoadWavVarispeedFacts.SingleArgUnchanged"` | ❌ W0 | ⬜ pending |
| 22-02-04 | 02 | 2 | DX-15 | — | N/A | integration | `dotnet run --project flow-interpreter tests/test_dx_loadwav_varispeed.flow` | ❌ W0 | ⬜ pending |
| 22-03-W0 | 03 | 2 | DX-11 | — | N/A | unit (RED) | `dotnet test --filter "VoicingFacts"` | ❌ W0 | ⬜ pending |
| 22-03-01 | 03 | 2 | DX-11 | — | charitable on incomplete (D-07) | unit | `dotnet test --filter "VoicingFacts.FirstInversion_RaisesLowestNoteOctave"` | ❌ W0 | ⬜ pending |
| 22-03-02 | 03 | 2 | DX-11 | — | N/A | unit | `dotnet test --filter "VoicingFacts.Drop2_LowersSecondFromTop"` | ❌ W0 | ⬜ pending |
| 22-03-03 | 03 | 2 | DX-11 | — | charitable on incomplete (D-07) | unit | `dotnet test --filter "VoicingFacts.Drop2_OnTriad_ReturnsUnchanged"` | ❌ W0 | ⬜ pending |
| 22-03-04 | 03 | 2 | DX-11 | — | N/A | integration | `dotnet run --project flow-interpreter tests/test_dx_voicings.flow` | ❌ W0 | ⬜ pending |
| 22-04-W0 | 04 | 3 | DX-12 | — | N/A | unit (RED) | `dotnet test --filter "DelaySyncFacts"` | ❌ W0 | ⬜ pending |
| 22-04-01 | 04 | 3 | DX-12 | — | N/A | unit | `dotnet test --filter "DelaySyncFacts.NoteValueToMs"` | ❌ W0 | ⬜ pending |
| 22-04-02 | 04 | 3 | DX-12 | — | byte-identical regression | unit | `dotnet test --filter "DelaySyncFacts.Existing_MsRateOverload_Unchanged"` | ❌ W0 | ⬜ pending |
| 22-04-03 | 04 | 3 | DX-12 | — | N/A | integration | `dotnet run --project flow-interpreter tests/test_dx_delay_sync.flow` | ❌ W0 | ⬜ pending |
| 22-05-W0 | 05 | 4 | DX-13 | T-22-V5 | clamp swing/strength range | unit (RED) | `dotnet test --filter "QuantizeFacts"` | ❌ W0 | ⬜ pending |
| 22-05-01 | 05 | 4 | DX-13 | — | N/A | unit | `dotnet test --filter "QuantizeFacts.Strength1_HardSnaps"` | ❌ W0 | ⬜ pending |
| 22-05-02 | 05 | 4 | DX-13 | — | byte-identical regression | unit | `dotnet test --filter "QuantizeFacts.Strength0_IsIdentity"` | ❌ W0 | ⬜ pending |
| 22-05-03 | 05 | 4 | DX-13 | — | N/A | unit | `dotnet test --filter "QuantizeFacts.Swing_SignSymmetric"` | ❌ W0 | ⬜ pending |
| 22-05-04 | 05 | 4 | DX-13 | — | N/A | integration | `dotnet run --project flow-interpreter tests/test_dx_quantize.flow` | ❌ W0 | ⬜ pending |
| 22-06-W0 | 06 | 5 | DX-14 | T-22-V5 | clamp CC5 to [0,127] | unit (RED) | `dotnet test --filter "LegatoFacts\|PortamentoMidiFacts"` | ❌ W0 | ⬜ pending |
| 22-06-01 | 06 | 5 | DX-14 | — | N/A | unit | `dotnet test --filter "LegatoFacts.OverlapHalf_Extends15x"` | ❌ W0 | ⬜ pending |
| 22-06-02 | 06 | 5 | DX-14 | — | N/A | unit | `dotnet test --filter "LegatoFacts.OnsetsUnchanged"` | ❌ W0 | ⬜ pending |
| 22-06-03 | 06 | 5 | DX-14 | — | N/A | unit | `dotnet test --filter "PortamentoMidiFacts.WriteMidi_ContainsCC65AndCC5"` | ❌ W0 | ⬜ pending |
| 22-06-04 | 06 | 5 | DX-14 | — | N/A | integration | `dotnet run --project flow-interpreter tests/test_dx_legato.flow` | ❌ W0 | ⬜ pending |
| 22-06-05 | 06 | 5 | DX-14 | — | N/A | integration | `dotnet run --project flow-interpreter tests/test_dx_portamento.flow` | ❌ W0 | ⬜ pending |
| 22-07-01 | 07 | 6 | ALL | — | byte-identical regression | integration | `dotnet test --filter "ByteIdentical"` | ✓ existing | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

xUnit Facts files (mirror `flow-lang.Tests/Unit/Phase21/` layout):
- [ ] `flow-lang.Tests/Unit/Phase22/` directory
- [ ] `flow-lang.Tests/Unit/Phase22/ArpeggioFacts.cs` — covers DX-10
- [ ] `flow-lang.Tests/Unit/Phase22/VoicingFacts.cs` — covers DX-11
- [ ] `flow-lang.Tests/Unit/Phase22/DelaySyncFacts.cs` — covers DX-12
- [ ] `flow-lang.Tests/Unit/Phase22/QuantizeFacts.cs` — covers DX-13
- [ ] `flow-lang.Tests/Unit/Phase22/LegatoFacts.cs` — covers DX-14 legato
- [ ] `flow-lang.Tests/Unit/Phase22/PortamentoMidiFacts.cs` — covers DX-14 portamento (uses MidiReadHelpers from Phase 15 DEFER-05)
- [ ] `flow-lang.Tests/Unit/Phase22/LoadWavVarispeedFacts.cs` — covers DX-15

`.flow` smoke scripts (one per feature):
- [ ] `tests/test_dx_arpeggio.flow` — DX-10 smoke
- [ ] `tests/test_dx_voicings.flow` — DX-11 smoke
- [ ] `tests/test_dx_delay_sync.flow` — DX-12 smoke
- [ ] `tests/test_dx_quantize.flow` — DX-13 smoke
- [ ] `tests/test_dx_legato.flow` — DX-14 legato smoke
- [ ] `tests/test_dx_portamento.flow` — DX-14 portamento smoke (writeMidi assertion)
- [ ] `tests/test_dx_loadwav_varispeed.flow` — DX-15 smoke

Sentinel registration:
- [ ] `FlowScriptData.RequiredSentinels` entries for all new `tests/test_dx_*.flow` scripts

*(No framework install needed — xUnit.v3 + DryWetMidi 8.0.3 already locked.)*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| DX-14 portamento glide audibly correct on a real MIDI synth | DX-14 | Audio perception is subjective; receiver-side CC65/CC5 interpretation varies by synth | Open generated `.mid` in a DAW (Reaper/Ableton/LMMS), route to a synth that supports portamento (e.g., Surge XT, Diva), play and confirm pitch glides |
| DX-15 varispeed pitch shift sounds correct (no clicks/aliasing) at typical settings | DX-15 | Audio perception is subjective; OLA windowing deferred to v1.4 means linear interp may exhibit minor artifacts at extreme ratios | Open generated `.wav` from `tests/test_dx_loadwav_varispeed.flow`, listen on monitors/headphones, confirm acceptable quality at ±12 semitones |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
