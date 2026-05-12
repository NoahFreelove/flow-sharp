---
phase: 28
slug: midi-audio-polyphony-articulation-rewrite
status: draft
nyquist_compliant: true
wave_0_complete: false
created: 2026-05-10
---

# Phase 28 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution. Derived from RESEARCH.md `## Validation Architecture` and SPEC `## Acceptance Criteria` (19 boxes).

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.x (existing) |
| **Config file** | `flow-lang.Tests/flow-lang.Tests.csproj` |
| **Quick run command** | `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase28"` |
| **Full suite command** | `dotnet test flow-lang.Tests` |
| **Estimated runtime** | ~30 seconds (54 articulation facts × 0.5s + integration ≤ 5s) |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase28"`
- **After every plan wave:** Run `dotnet test flow-lang.Tests` (full suite — guards backward compat with Phase 22 LegatoFacts and Phase 18/25/27 ByteIdentical)
- **Before `/gsd-verify-work`:** Full suite must be green AND manual UAT sign-off in `28-VERIFICATION.md` for both ragtime fixtures
- **Max feedback latency:** 30 seconds (full Phase 28 suite); 60 seconds (full Phase 1–28 suite)

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 28-01-01 | 01 | 1 | SPEC-1 | — | Voice-block parser accepts `{voice C4w}` without error | unit | `dotnet test --filter "VoiceBlockParser_AcceptsBasicSyntax"` | ❌ W0 | ⬜ pending |
| 28-01-02 | 01 | 1 | SPEC-1 | — | Voice-block compiler emits N parallel BarData per block | unit | `dotnet test --filter "VoiceBlockCompiler_EmitsParallelBars"` | ❌ W0 | ⬜ pending |
| 28-01-03 | 01 | 1 | SPEC-3 | — | `Articulation.Legato` exists in enum | unit | `dotnet test --filter "Legato_EnumValueExists"` | ❌ W0 | ⬜ pending |
| 28-01-04 | 01 | 1 | SPEC-3 | — | Parser accepts `leg` token; compiles to `Articulation.Legato` | unit | `dotnet test --filter "Parser_AcceptsLegToken"` | ❌ W0 | ⬜ pending |
| 28-02-01 | 02 | 1 | SPEC-4 | — | Staccato → 25% duration, ±5% tolerance | unit | `dotnet test --filter "Articulation_Staccato_25Percent"` | ❌ W0 | ⬜ pending |
| 28-02-02 | 02 | 1 | SPEC-4 | — | All 6 articulations meet locked rules ±5% / ±2 vel | unit | `dotnet test --filter "ArticulationRules_AllSix"` | ❌ W0 | ⬜ pending |
| 28-02-03 | 02 | 1 | SPEC-4 | — | Marcato envelope = Staccato envelope; velocity = +30% | unit | `dotnet test --filter "Marcato_StaccEnvelope_AccentVelocity"` | ❌ W0 | ⬜ pending |
| 28-03-01 | 03 | 2 | SPEC-5 | — | Per-synth GenerateArticulationADSR helper | unit | `dotnet test --filter "GenerateArticulationADSR_Helper"` | ❌ W0 | ⬜ pending |
| 28-03-02 | 03 | 2 | SPEC-5 | — | 54 facts: each (synth × art) Normal/Staccato cosine < 0.95 | unit | `dotnet test --filter "PerSynthArticulation"` | ❌ W0 | ⬜ pending |
| 28-04-01 | 04 | 2 | SPEC-6 | — | MidiFile.Chunks.Count == 1 + uniqueSequenceCount | integration | `dotnet test --filter "MultiTrackMidi_ChunkCount"` | ❌ W0 | ⬜ pending |
| 28-04-02 | 04 | 2 | SPEC-6 | — | Each non-conductor track has correct ProgramChange | integration | `dotnet test --filter "MultiTrackMidi_ProgramChange"` | ❌ W0 | ⬜ pending |
| 28-04-03 | 04 | 2 | SPEC-6 | — | Cross-section same-name sequences concatenate onto one track | integration | `dotnet test --filter "MultiTrackMidi_CrossSection"` | ❌ W0 | ⬜ pending |
| 28-04-04 | 04 | 2 | SPEC-6 | — | Drum sequence routes to channel 9 | integration | `dotnet test --filter "MultiTrackMidi_DrumChannel9"` | ❌ W0 | ⬜ pending |
| 28-05-01 | 05 | 2 | SPEC-7 | — | `voicePool 16 { ... }` parses + applies | unit | `dotnet test --filter "VoicePool_ParsesAndApplies"` | ❌ W0 | ⬜ pending |
| 28-05-02 | 05 | 2 | SPEC-7 | — | 50-onset stress test caps voice count at 32 | integration | `dotnet test --filter "VoicePool_50OnsetsStealOldest"` | ❌ W0 | ⬜ pending |
| 28-05-03 | 05 | 2 | SPEC-7 | — | Steal-oldest deterministic across two runs | integration | `dotnet test --filter "VoicePool_DeterministicTwoRun"` | ❌ W0 | ⬜ pending |
| 28-06-01 | 06 | 3 | SPEC-2, SPEC-8 | — | RmsRegression helper + positive baseline | unit | `dotnet test --filter "RmsRegression_PositiveBaseline"` | ❌ W0 | ⬜ pending |
| 28-06-02 | 06 | 3 | SPEC-8 | — | Negative test — intentional regression triggers diagnostic | unit | `dotnet test --filter "RmsRegression_NegativeDiagnostic"` | ❌ W0 | ⬜ pending |
| 28-06-03 | 06 | 3 | SPEC-2 | — | Held-note RMS last-50ms ≥ 50% first-50ms | integration | `dotnet test --filter "HeldNote_NonTruncation"` | ❌ W0 | ⬜ pending |
| 28-06-04 | 06 | 3 | SPEC-1, SPEC-2 | — | Voice-block render WAV: held + running attacks distinguishable | integration | `dotnet test --filter "VoiceBlock_HeldPlusRunning"` | ❌ W0 | ⬜ pending |
| 28-07-01 | 07 | 3 | SPEC-9 | — | `examples/tests/ragtime_polyphony.flow` exists + renders | integration | `dotnet test --filter "Ragtime_SyntheticFixture_Renders"` | ❌ W0 | ⬜ pending |
| 28-07-02 | 07 | 3 | SPEC-9 | — | `examples/tests/maple_leaf_opening.flow` exists + renders | integration | `dotnet test --filter "Ragtime_MapleLeaf_Renders"` | ❌ W0 | ⬜ pending |
| 28-07-03 | 07 | 3 | SPEC-9 | — | Manual UAT checkboxes in 28-VERIFICATION.md | manual | (manual sign-off) | ❌ W0 | ⬜ pending |
| 28-07-04 | 07 | 3 | (closure) | — | ROADMAP/STATE/CLAUDE.md updated; full suite GREEN | manual | `dotnet test flow-lang.Tests` | — | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `flow-lang.Tests/Unit/Phase28/` directory created
- [ ] `flow-lang.Tests/Integration/Phase28/` directory created
- [ ] `flow-lang.Tests/Helpers/` directory created (RmsRegressionTests helper class lives here)
- [ ] `flow-lang.Tests/baselines/Phase28/` directory created (committed reference WAVs)
- [ ] `examples/tests/` directory created if absent (ragtime fixtures live here)

*Wave 0 is implicit in this phase — directories created as part of Plan 06 (RMS test infra) and Plan 07 (fixtures + closure).*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Synthetic ragtime audibly correct | SPEC-9 | Subjective listening — composer hears held + running, articulations, legato | `dotnet run --project flow-interpreter examples/tests/ragtime_polyphony.flow`; play `examples/output/ragtime_polyphony.wav`; check "Audibly correct: ✓" in 28-VERIFICATION.md |
| Maple Leaf opening audibly correct | SPEC-9 | Subjective listening — stride pattern audible, RH/LH separation clear | `dotnet run --project flow-interpreter examples/tests/maple_leaf_opening.flow`; play output WAV; check "Audibly correct: ✓" in 28-VERIFICATION.md |
| DAW import smoke test | SPEC-6 | Round-trip in real DAW (LMMS / Reaper / Logic) | Load multi-sequence `.mid` exported from `examples/tests/ragtime_polyphony.flow`; confirm each Flow sequence appears as routable track |
| Per-instrument articulation timbre | SPEC-5 | FFT cosine < 0.95 catches *any* difference; human listening confirms it's the *right kind* of difference | Render same C4q under Normal/Staccato/Legato per synth; listen; mark per-synth note in 28-VERIFICATION.md |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references (test directories created)
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending — to be marked approved after manual UAT in `28-VERIFICATION.md`.
