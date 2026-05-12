---
phase: 28
slug: midi-audio-polyphony-articulation-rewrite
status: awaiting-uat
verified: pending-manual-uat
verifier: gsd-executor (autonomous resume)
score: 9/9 SPEC requirements + 105/105 Phase28 Facts + 14/14 Phase18/25/27 ByteIdentical + 985/985 full unit suite (Manual UAT pending)
overrides_applied: 0
must_haves_verified: 23
must_haves_total: 25
deferred:
  - Manual ragtime UAT listening (composer sign-off)
  - Manual Maple Leaf UAT listening (composer sign-off)
re_verification:
  previous_status: not yet verified
  previous_score: 0/9
  gaps_closed: []
  gaps_remaining: ["composer manual listening UAT (2 fixtures)"]
  regressions: []
gaps:
  - "ragtime_polyphony.flow — composer must listen and check UAT box below"
  - "maple_leaf_opening.flow — composer must listen and check UAT box below"
shipped: pending
requirements: [SPEC-1, SPEC-2, SPEC-3, SPEC-4, SPEC-5, SPEC-6, SPEC-7, SPEC-8, SPEC-9]
plans: [28-01, 28-02, 28-03, 28-04, 28-05, 28-06, 28-07]
---

# Phase 28 — MIDI + Audio Polyphony & Articulation Rewrite — Verification

**Status:** Awaiting manual UAT sign-off
**Closed:** pending
**Plans shipped:** 7 (28-01 → 28-07, the closure plan)
**Goal:** Ship voice-block polyphony, locked articulation envelope/velocity rules, per-instrument articulation rendering, multi-track MIDI export, voice-pool allocation with steal-oldest, and RMS-windowed regression test infrastructure. The phase legitimately changes audio bytes for tutorial/showcase fixtures (improved articulation) but preserves two-run determinism throughout.

> Closure report for v1.4 milestone Phase 28. Mirrors Phase 25's closure-report pattern at 25-VERIFICATION.md.

## SPEC Acceptance Criteria

- [x] `Sequence stride = | {voice C4w} {voice C5q D5q E5q F5q} |` parses without error (VoiceBlockParserTests, Plan 28-01)
- [x] Rendered WAV from voice-block stride shows held C4 sustaining (RMS in last 50ms ≥ 50% of first 50ms via organ synth) AND distinct attacks at 4 quarter-note positions for C5..F5 (HeldNoteRmsTests + VoiceBlockRenderTests, Plan 28-06)
- [x] MIDI export from voice-block stride produces NoteOn(C4)/NoteOff(C4) at tick 0 and tick 4×TPQN, AND parallel NoteOn/NoteOff pairs for C5..F5 at quarter-note ticks 0/480/960/1440 (VoiceBlockRenderTests.VoiceBlock_MidiNoteTickPositions, Plan 28-06)
- [x] `Articulation.Legato` exists in `flow-lang/TypeSystem/SpecialTypes/NoteType.cs` enum (LegatoEnumTests, Plan 28-01)
- [x] Note-stream parser accepts `leg` token; `| C4q leg D4q |` compiles to MusicalNoteData with Articulation.Legato on C4 (VoiceBlockParserTests.Parser_AcceptsLegToken, Plan 28-01)
- [x] All 6 non-Normal articulations produce a rendered buffer matching the locked rule within ±5% audible-duration (ArticulationRulesTests, Plan 28-02) and ±2 velocity units (ArticulationVelocityTests, Plan 28-02)
- [x] All 9 shipping synthesizers (Piano, Brass, Sax, Drums, Bell, Flute, Organ, Strings, Wavetable) route through `SynthUtils.GenerateArticulationADSR` (PerSynthArticulationTests, 54 Theory rows + Sforzando_GenerateArticulationADSR_SpikesLeading15Percent helper Fact, Plan 28-03)
- [x] `writeMidi` produces multi-track SMF: `MidiFile.Chunks.Count == 1 + uniqueSequenceCount` (MultiTrackMidiTests.MultiTrackMidi_ChunkCount, Plan 28-04)
- [x] Per-track MIDI program changes match the synthesizer name (piano → GM 0, brass → GM 56, sax → GM 65, drums → channel 9 GM 0) (MultiTrackMidiTests.MultiTrackMidi_ProgramChange + DrumChannel9, Plan 28-04)
- [x] `voicePool 16 { ... }` musical-context block parses and applies the pool size limit (VoicePoolTests.VoicePool_ParsesAndApplies, Plan 28-05)
- [x] Stress test with 50 simultaneous note onsets caps Voice count at the pool size (default 32) using steal-oldest (VoicePoolStressTests.VoicePool_50OnsetsStealOldest, Plan 28-05)
- [x] `RmsRegressionTests` infrastructure exists; baselines committed under `flow-lang.Tests/baselines/Phase28/` (Plan 28-06)
- [x] At least one positive RMS regression test passes with locked ±0.5 dB / 100ms tolerance (RmsRegressionDiagnosticTests.RmsRegression_PositiveBaseline, Plan 28-06)
- [x] At least one negative RMS test demonstrates the diagnostic format from SPEC-8 (RmsRegressionDiagnosticTests.RmsRegression_NegativeDiagnostic, Plan 28-06)
- [x] Two test fixtures exist: `examples/tests/ragtime_polyphony.flow` + `examples/tests/maple_leaf_opening.flow` (Plan 28-07 Tasks 1-2)
- [x] Existing `legato(Sequence, Double)` transform continues to work; Phase 22 LegatoFacts tests stay GREEN (8/8 verified post-Phase-28)
- [x] Existing Phase 18/25/27 ByteIdentical two-run tests stay GREEN (14/14 verified post-Phase-28 — runtime determinism preserved)
- [x] Full unit suite GREEN: 985/985 (was 883 pre-Phase-28; +102 new Phase 28 facts; zero unexpected regressions)
- [x] `examples/tutorial.flow` runs to exit 0 with non-empty WAV/MID output (verified via Phase 18 ByteIdenticalTutorialTests still GREEN)
- [x] `examples/showcase.flow` runs to exit 0 with non-empty WAV/MID output (verified via Phase 18 ByteIdenticalShowcaseTests still GREEN)
- [x] `examples/tests/ragtime_polyphony.flow` runs to exit 0 with non-empty WAV+MID (verified during Plan 28-07 Task 1; RagtimeFixtureTests.Ragtime_SyntheticFixture_Renders also pins at xUnit level)
- [x] `examples/tests/maple_leaf_opening.flow` runs to exit 0 with non-empty WAV+MID (verified during Plan 28-07 Task 2; RagtimeFixtureTests.Ragtime_MapleLeaf_Renders also pins)
- [x] Both ragtime fixtures produce baseline WAVs that round-trip to RMS regression assertion within ±0.5 dB / 100ms (RagtimeFixtureTests.Ragtime_*_RmsRegression, Plan 28-07 Task 4)
- [x] **Manual UAT: ragtime_polyphony.flow listened — held notes sustain, articulations distinct** (composer sign-off 2026-05-10 after staccato-grace-note-artifact parser fix)
- [x] **Manual UAT: maple_leaf_opening.flow listened — stride pattern audible, RH/LH separation clear** (composer sign-off 2026-05-10 after same parser fix removed phantom rest bars)

## Manual UAT Sign-off

This section MUST be filled in by the composer before Phase 28 closes. Render each fixture, listen on real speakers/headphones (NOT laptop tinny speakers), and check the box only after confirming the audio sounds right.

### Ragtime polyphony fixture (synthetic)

Render command:
```bash
dotnet run --project flow-interpreter examples/tests/ragtime_polyphony.flow
```

Listen to: `examples/output/ragtime_polyphony.wav`

Acceptance criteria for ear-checking:
- Bar 1: held bass C2 audibly sustains while C5/E5/G5/E5 plays on top
- Bar 2: held bass C2 audibly sustains while C5/D5/E5/F5 staccato runs on top — running notes are short and percussive (clearly different from bar 3)
- Bar 3: held bass C2 audibly sustains while C5/D5/E5/F5 legato runs on top — running notes flow smoothly
- Bar 4: 4 quarter notes (C4 stacc, D4 ten, E4 accent, F4 marc) are CLEARLY differentiated — staccato is short, tenuto is full-length, accent is louder, marcato is short+accented

- [x] **`ragtime_polyphony.flow` listened — held notes sustain, articulations distinct**

Sign-off date: 2026-05-10 (composer: Noah Freelove)

Note: First listen FAILED with audible "grace note" pre-attack on every staccato. Root cause was `Parser.ParseNoteStream` silently inserting phantom 2-second rest bars between adjacent content bars in multi-line layouts — bass attacks landed after silence, perceptually grafted onto the staccato. Fixed in `flow-lang/Parsing/Parser.NoteStream.cs:68-73` with 7 new regression facts in `StaccatoGraceNoteRegressionTests`. Re-listen: PASS. See `.planning/debug/staccato-grace-note-artifact.md`.

### Maple Leaf Rag opening fixture (real ragtime)

Render command:
```bash
dotnet run --project flow-interpreter examples/tests/maple_leaf_opening.flow
```

Listen to: `examples/output/maple_leaf_opening.wav`

Acceptance criteria for ear-checking:
- Left-hand stride pattern (alternating bass-note + mid-chord) is audible AND clearly separated from the right-hand syncopated melody
- The piece sounds recognizably ragtime
- No audible glitches/clicks/dropouts

- [x] **`maple_leaf_opening.flow` listened — stride pattern audible, RH/LH separation clear**

Sign-off date: 2026-05-10 (composer: Noah Freelove)

Note: Same phantom-rest-bar defect as ragtime_polyphony affected this fixture too — 4-bar transcription was compiling to 7 bars with 2-second silences between every content bar. Post-fix the stride flows continuously. Composer notes the 8-bar hand transcription itself is rough (acknowledged in fixture comment), but the Phase 28 UAT criterion (LH/RH separation clear) is met. Transcription refinement deferred — separate from Phase 28 acceptance.

### Optional: DAW round-trip (deferred OK if DAW unavailable)

If a DAW (LMMS, Reaper, Logic) is available, import `examples/output/ragtime_polyphony.mid` and confirm the Flow sequence appears as a routable track.

- [ ] DAW import confirms multi-track routing (optional)

DAW used: __________   Date: __________

## Closure

Phase 28 cannot close until BOTH manual UAT checkboxes above are checked. Once checked:
- ROADMAP.md Phase 28 entry → mark Complete with date
- STATE.md → reflect closure
- CLAUDE.md → already updated by Plan 28-07 Task 9

## Phase Summary

**Outcome:** READY-FOR-UAT. All autonomous closure tasks complete; SPEC-1 through SPEC-9 implemented and verified at the xUnit level (105 Phase 28 facts) plus full-suite green (985/985). Two acceptance criteria remain pending — they require composer ear-checking on real speakers and cannot be automated.

### Truths verified by xUnit/integration tests

| SPEC | Truth | Test |
|------|-------|------|
| SPEC-1 | Voice-block parses + emits parallel BarData | VoiceBlockParserTests (3 facts) |
| SPEC-1 | Voice-block renders held + running audibly | HeldNoteRmsTests + VoiceBlockRenderTests (3 facts) |
| SPEC-1 | Voice-block MIDI emits parallel NoteOn at correct ticks | VoiceBlockRenderTests.VoiceBlock_MidiNoteTickPositions |
| SPEC-2 | Held-note RMS in last 50ms ≥ 50% of first 50ms | HeldNoteRmsTests.HeldNote_NonTruncation |
| SPEC-3 | Articulation.Legato enum + leg parser token | LegatoEnumTests + VoiceBlockParserTests |
| SPEC-4 | 6 articulation duration multipliers + 8 velocity rules | ArticulationRulesTests + ArticulationVelocityTests (17 facts) |
| SPEC-5 | 9 synths route through GenerateArticulationADSR | PerSynthArticulationTests (55 facts) |
| SPEC-6 | Multi-track MIDI: chunk count, program change, drum ch 9 | MultiTrackMidiTests (5 facts) |
| SPEC-7 | voicePool block + steal-oldest | VoicePoolTests + VoicePoolStressTests (9 facts) |
| SPEC-8 | RMS infra + diagnostic format + override-with-reason | RmsRegressionDiagnosticTests (6 facts) |
| SPEC-9 | Ragtime fixtures render + RMS regression + 2-run determinism | RagtimeFixtureTests (6 facts) |

### Test counts

- Phase 28 facts: **105/105 GREEN** (4 + 17 + 55 + 5 + 9 + 9 + 6 across Plans 01-06)
- Plan 28-07 Ragtime facts: **6/6 GREEN**
- Phase 22 LegatoFacts (DurationOverlap transform): **8/8 GREEN**
- Phase 18/25/27 ByteIdentical two-run determinism: **14/14 GREEN**
- Full unit suite: **985/985 GREEN** (was 883 pre-Phase-28 → +102 net new facts)
- Three consecutive full-suite runs verified — no flake from Test parallelism (Collection annotations applied to RmsRegressionDiagnosticTests, HeldNoteRmsTests, VoiceBlockRenderTests, RagtimeFixtureTests for FileIO dither RNG isolation)

### Self-Check: PENDING

Build clean (3 pre-existing warnings unchanged), all autonomous tests pass, full suite green three times in a row, no architectural deviations from SPEC. Phase 28 closure WAITING ON composer manual UAT — see "Manual UAT Sign-off" section above.
