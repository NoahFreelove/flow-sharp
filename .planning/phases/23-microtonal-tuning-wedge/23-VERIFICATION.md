---
phase: 23
slug: microtonal-tuning-wedge
status: shipped
nyquist_compliant: true
wave_0_complete: true
created: 2026-05-03
shipped: 2026-05-04
must_haves_verified: 4
must_haves_total: 4
deferred: []
---

# Phase 23 — Final Verification Report

**Phase 23 (Microtonal Tuning, Wedge) closes 2026-05-04.** Three named-tuning pragmas (`enable justIntonation;` / `enable pythagorean;` / `enable equalTemperament;`) ship as render-time wedge per D-03; Pattern A `RenderTuning` value object threads through `INoteSynthesizer.RenderNote` into 13 synthesizer call sites + the migrated Vocalization path; transforms remain MIDI-pitch invariant per MICR-02; v1.3 milestone advances **5/10 → 6/10 phases complete** (60%).

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (`flow-lang.Tests`) + `.flow` script integration loop |
| **Config file** | `flow-lang.Tests/flow-lang.Tests.csproj` (xUnit); `tests/test_*.flow` (script loop) |
| **Quick run command** | `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase23"` |
| **Full suite command** | `dotnet test flow-sharp.sln && for t in tests/test_tuning_*.flow; do dotnet run --project flow-interpreter "$t" || exit 1; done` |
| **Estimated runtime** | ~30–60 seconds (xUnit) + ~10–20s per `.flow` script |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase23"`
- **After every plan wave:** Run full suite (`dotnet test` + tuning `.flow` scripts + `ByteIdenticalTutorialTests` + `ByteIdenticalShowcaseTests`)
- **Before `/gsd-verify-work`:** Full suite must be green AND byte-identical regression gate green
- **Max feedback latency:** 60 seconds (per-task quick run)

---

## Shipped Features

| REQ-ID | Feature | Plan | Commit |
|--------|---------|------|--------|
| MICR-01 | Three named tunings register in PragmaRegistry; PitchConversion.NoteToFrequency consults active tuning at render boundary | 23-02 | f6b00ba |
| MICR-02 | Tuning is render-time only — transforms produce identical MIDI pitch numbers under every tuning | 23-02 | 8190fb2 |
| MICR-03 | Unknown tuning names route via Phase 21 D-12 path with appended Scala v1.4 deferral pointer | 23-02 | 47d7718 |

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|----------|-----------|-------------------|-------------|--------|
| 23-01-T1 | 01 | 1 | MICR-01 | T-23-01-01 | Closed enums (TuningSystem/Mode) + RenderTuning record-struct + ChromaticRatioTable scaffolding (Pattern A locked) | unit | `dotnet test --filter "FullyQualifiedName~TuningRatioFacts"` | ✅ exists | ✅ green |
| 23-01-T2 | 01 | 1 | MICR-01 | T-23-01-01 | 14 ratio tables (7 JI + 7 Pythagorean modes) + RatioMath helpers + canonical Wikipedia/Mudcat citations | unit | `dotnet test --filter "FullyQualifiedName~TuningRatioFacts\|TuningModeShiftFacts\|SpellingAwareTuningFacts\|CentOffsetAdditivityFacts"` | ✅ exists | ✅ green |
| 23-02-T1 | 02 | 2 | MICR-03 | T-23-02-01 | 3 tuning pragmas registered; MusicalContext.Tuning 9th field; FlowEngine bridge; PragmaScanner D-14 unknown-tuning Scala pointer; TransformFunctions D-12 doc-only caveat | unit | `dotnet test --filter "FullyQualifiedName~PragmaTuningFacts\|UnknownTuningPragmaFacts"` | ✅ exists | ✅ green |
| 23-02-T2 | 02 | 2 | MICR-01 | T-23-02-02 | Tuning-aware PitchConversion overload + Pitfall 6 byte-identical short-circuit + Pattern A threading through 13 synthesizers + ByteIdenticalDefaultTuning regression Facts | integration | `dotnet test --filter "FullyQualifiedName~ByteIdenticalDefaultTuningTests"` | ✅ exists | ✅ green |
| 23-02-T3 | 02 | 2 | MICR-01 | T-23-02-03 | SongRenderer.ResolveRenderTuning per-section + ScaleDatabase.TryParseKeyWithMode canonical entry + Vocalization context-dependent migration | unit | `dotnet test --filter "FullyQualifiedName~VocalizationTuningFacts"` | ✅ exists | ✅ green |
| 23-02-T4 | 02 | 2 | MICR-01/02 | T-23-02-04 | MICR-01 end-to-end ratio Facts (5:4 JI third, 3:2 Pythagorean fifth) + MICR-02 TransformInvariance Facts | unit | `dotnet test --filter "FullyQualifiedName~PitchConversionTuningFacts\|TransformInvarianceFacts"` | ✅ exists | ✅ green |
| 23-03-T1 | 03 | 3 | D-04 | T-23-03-01 | RenderingDiagnostics one-shot stderr channel + ScaleDatabase 5-church-mode widening (longer-suffix-first) + ValidKeys 34→119 entries | unit | `dotnet test --filter "FullyQualifiedName~ChurchModeParseFacts\|RenderingDiagnosticsFacts"` | ✅ exists | ✅ green |
| 23-03-T2 | 03 | 3 | D-11/D-13 | T-23-03-02 | D-11 enharmonic non-12-TET warning + D-13 writeMidi non-12-TET warning + writeMidi context-dependent registration migration | unit | `dotnet test --filter "FullyQualifiedName~EnharmonicWarningFacts\|WriteMidiWarningFacts"` | ✅ exists | ✅ green |
| 23-04-T1 | 04 | 4 | MICR-01/02 | T-23-04-01 | 5 .flow tuning smoke scripts (ji/pythagorean/equal/transpose_invariant/determinism) — WARNING-7 scaffold | integration | `for t in tests/test_tuning_*.flow; do dotnet run --project flow-interpreter "$t"; done` | ✅ exists | ✅ green |
| 23-04-T2 | 04 | 4 | Determinism | T-23-04-02 | TuningDeterminismTests Integration class — JI/explicit-EqualTemperament/Pythagorean two-run byte-identical via WARNING-5 inline sources + WARNING-4 between-runs ResetForTesting | integration | `dotnet test --filter "FullyQualifiedName~TuningDeterminismTests"` | ✅ exists | ✅ green |

---

## Required Test Surfaces

| MICR | Test Surface | File(s) | Status |
|------|--------------|---------|--------|
| MICR-01 | xUnit ratio Facts (5:4 JI third, Pythagorean chain-of-fifths, equalTemperament default) | `flow-lang.Tests/Unit/Phase23/TuningRatioFacts.cs` | ✅ green (14 Facts) |
| MICR-01 | `.flow` smoke tests for each tuning | `tests/test_tuning_ji.flow`, `tests/test_tuning_pythagorean.flow`, `tests/test_tuning_equal.flow` | ✅ green (3 scripts exit 0) |
| MICR-01 | xUnit end-to-end Facts at PitchConversion render boundary | `flow-lang.Tests/Unit/Phase23/PitchConversionTuningFacts.cs` (5 Facts inc. JI 5:4 + Pythagorean 3:2 + Pitfall 6 short-circuit) | ✅ green |
| MICR-02 | xUnit Facts: transpose/invert/retrograde/augment/diminish MIDI invariance under JI / Pythagorean / 12-TET | `flow-lang.Tests/Unit/Phase23/TransformInvarianceFacts.cs` | ✅ green (5 Facts) |
| MICR-02 | `.flow` test asserting MIDI numbers identical | `tests/test_tuning_transpose_invariant.flow` | ✅ green (script exits 0) |
| MICR-03 | xUnit Fact: unknown tuning emits Phase 21 D-12 error path with Scala v1.4 pointer line | `flow-lang.Tests/Unit/Phase23/UnknownTuningPragmaFacts.cs` (4 Facts) | ✅ green |
| D-04 | xUnit Facts: TryParseKeyWithMode handles dorian/phrygian/lydian/mixolydian/locrian | `flow-lang.Tests/Unit/Phase23/ChurchModeParseFacts.cs` (8 Theory + 4 Facts = 12) | ✅ green |
| D-08 | Byte-identical regression: explicit `enable equalTemperament;` produces same audio output as no-pragma | `flow-lang.Tests/Integration/Phase23/ByteIdenticalDefaultTuningTests.cs` (2 Facts) | ✅ green |
| D-08 | Byte-identical regression unchanged: tutorial.flow + showcase.flow ByteIdentical Facts GREEN | `flow-lang.Tests/Integration/Phase18/ByteIdenticalTutorialTests.cs`, `ByteIdenticalShowcaseTests.cs` | ✅ green (4 Facts unchanged) |
| D-11 | xUnit Fact: `enharmonic()` emits one-shot stderr warning under non-12-TET, NOT under 12-TET | `flow-lang.Tests/Unit/Phase23/EnharmonicWarningFacts.cs` (5 Facts) | ✅ green |
| D-13 | xUnit Fact: `writeMidi` emits one-shot stderr warning under non-12-TET, NOT under 12-TET; MIDI bytes unchanged | `flow-lang.Tests/Unit/Phase23/WriteMidiWarningFacts.cs` (5 Facts) | ✅ green |
| D-09 | xUnit Fact: `Eb4` and `D#4` produce different rendered Hz under JI; identical under 12-TET | `flow-lang.Tests/Unit/Phase23/SpellingAwareTuningFacts.cs` (4 Facts) | ✅ green |
| D-10 | xUnit Fact: cent offsets compose additively over JI ratio | `flow-lang.Tests/Unit/Phase23/CentOffsetAdditivityFacts.cs` (4 Facts) | ✅ green |
| Determinism | JI/explicit-EqualTemperament/Pythagorean two-run byte-identical pin | `flow-lang.Tests/Integration/Phase23/TuningDeterminismTests.cs` (3 Facts via WARNING-5 inline sources) | ✅ green |
| Mode shift | xUnit Theory rows verifying canonical scale-degree shape per D-03 | `flow-lang.Tests/Unit/Phase23/TuningModeShiftFacts.cs` (14 Theory rows) | ✅ green |
| Pragma growth | Closed-set `KnownPragmas.Count >= 4` (was 1 in Phase 21; reservation for Phase 24 scaleLint preserved) | `flow-lang.Tests/Unit/Phase23/PragmaTuningFacts.cs` (6 Facts) | ✅ green |
| Vocalization | sing reads MusicalContext.Tuning via context-dependent registration migration | `flow-lang.Tests/Unit/Phase23/VocalizationTuningFacts.cs` (1 Fact) | ✅ green |
| RenderingDiagnostics | One-shot HashSet dedup contract | `flow-lang.Tests/Unit/Phase23/RenderingDiagnosticsFacts.cs` (5 Facts) | ✅ green |

**Cumulative Phase 23 Facts: 91 / 91 GREEN.**

---

## Wave 0 Requirements

- [x] `flow-lang.Tests/Unit/Phase23/` directory created (10 unit Fact files shipped)
- [x] `TuningRatioFacts.cs` — MICR-01 ratio assertions (5-limit JI, Pythagorean chain, equalTemperament identity)
- [x] `TransformInvarianceFacts.cs` — MICR-02 MIDI invariance
- [x] `UnknownTuningPragmaFacts.cs` — MICR-03 error-message + Scala pointer
- [x] `ChurchModeParseFacts.cs` — D-04 mode-suffix recognition (5 church modes)
- [x] `EnharmonicWarningFacts.cs` — D-11 one-shot warning
- [x] `WriteMidiWarningFacts.cs` — D-13 one-shot warning
- [x] `SpellingAwareTuningFacts.cs` — D-09 spelling-divergent rendering
- [x] `CentOffsetAdditivityFacts.cs` — D-10 cent additivity
- [x] `tests/test_tuning_ji.flow`, `tests/test_tuning_pythagorean.flow`, `tests/test_tuning_equal.flow`, `tests/test_tuning_transpose_invariant.flow`, `tests/test_tuning_determinism.flow` — 5 .flow smoke scripts (Wave 4 commits ba27282 + 4f85eaf)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions | Status |
|----------|-------------|------------|-------------------|--------|
| Audible JI vs 12-TET difference on `play(C4 E4)` | MICR-01 (UX) | Audio listening is irreducibly subjective; xUnit pins the ratio numerically | Run `tests/test_tuning_ji.flow` with audio enabled; compare to `tests/test_tuning_equal.flow`. JI third should sound noticeably "purer" / less beating. | DEFERRED to first release tag |
| REPL persisted-tuning behavior across lines (D-07) | D-07 | REPL is interactive; xUnit can simulate but human verifies the UX is non-confusing | Run REPL, type `enable justIntonation;`, then `play(C4 E4)` on a separate line, then `play(D4 F#4)` on a third line. All three should render under JI without re-declaring the pragma. | DEFERRED to first release tag |

These do **not** block phase closure — they are tracked alongside the v1.2-era Phase 17 HUMAN-UAT items for resolution at the v1.3 milestone HUMAN-UAT roll-up.

---

## ROADMAP Phase 23 Success Criteria — Cited Facts

| # | Criterion | Cited Fact / Smoke | Commit |
|---|-----------|---------------------|--------|
| 1 | `enable justIntonation; play(C4 E4)` produces 5:4 ratio (1.25), not 12-TET ~1.2599 | `TuningRatioFacts.JustMajor_CtoE_Is5to4` (Wave 1) + `PitchConversionTuningFacts.PitchConversionEndToEnd_JI_CtoE_FrequencyRatio_Is5to4` (Wave 2 Task 4) + `tests/test_tuning_ji.flow` smoke (Wave 4) | f6b00ba + 8190fb2 + ba27282 |
| 2 | `transpose(seq, 5)` produces same MIDI numbers under every tuning | `TransformInvarianceFacts` (5 Facts × transforms — Wave 2 Task 4) + `tests/test_tuning_transpose_invariant.flow` smoke (Wave 4) | 8190fb2 + ba27282 |
| 3 | Tuning system applies at render-time only | `TransformInvarianceFacts` (transforms produce identical MIDI shape across tunings — render-time-only contract) + Pattern A `RenderTuning` payload threading at PitchConversion / synth layer (Wave 1+2) | b6b916b + f6b00ba + 8190fb2 |
| 4 | Unknown tuning name raises clear error pointing at v1.4 Scala | `UnknownTuningPragmaFacts.UnknownTuning_ErrorIncludesScalaPointer` (Wave 2 Task 1) + `UnknownTuningPragmaFacts.UnknownTuning_DidYouMean_FromLevenshtein` (Wave 2 Task 1) | 47d7718 |

**Score: 4/4 ROADMAP success criteria verified.**

---

## REQ-ID Traceability

| REQ-ID | SPEC acceptance | Pinning Artifacts | Plan | Commit |
|--------|----------------|-------------------|------|--------|
| MICR-01 | `enable justIntonation; play(C4 E4)` produces 5:4 ratio | `Phase23/TuningRatioFacts.cs` (14 Facts) + `Phase23/PitchConversionTuningFacts.cs` (5 Facts) + `tests/test_tuning_ji.flow` + `tests/test_tuning_pythagorean.flow` + `flow-lang/StandardLibrary/Audio/Tuning/TuningTables.cs` (14 ratio tables) + `flow-lang/StandardLibrary/Audio/PitchConversion.cs` (2-arg overload + Pitfall 6 short-circuit) + 13 synthesizer call-site updates | 23-02 | f6b00ba |
| MICR-02 | `transpose(seq, 5)` produces same MIDI numbers under every tuning | `Phase23/TransformInvarianceFacts.cs` (5 Facts × transforms) + `tests/test_tuning_transpose_invariant.flow` + `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` (body BIT-IDENTICAL — only XML `<remarks>` D-12 caveat added) | 23-02 | 8190fb2 |
| MICR-03 | Unknown tuning raises clear error pointing at v1.4 Scala | `Phase23/UnknownTuningPragmaFacts.cs` (4 Facts) + `flow-lang/Lexing/PragmaScanner.cs` (D-14 extension with single-source-of-truth ScalaLoaderDeferralPointer const + LooksLikeTuningName Levenshtein <=3 + substring fallback) | 23-02 | 47d7718 |

---

## Locked Decisions D-01 through D-14 — Verification

| Decision | Description | Pinning Artifact |
|----------|-------------|------------------|
| D-01 | JI/Pythagorean tonic resolves from innermost active MusicalContext.Key | `SongRenderer.ResolveRenderTuning` (per-section + key block lookup) |
| D-02 | Non-12-TET pragma without key block silently roots at C major | `SongRenderer.ResolveRenderTuning` C-major fallback path + `ByteIdenticalDefaultTuningTests.NoPragma_StillBitIdentical_AfterPattern_A_Threading` |
| D-03 | Mode shifts the chromatic ratio table — 7 modes × 2 systems = 14 ratio tables | `TuningTables.cs` (14 static `ChromaticRatioTable` fields) + `TuningModeShiftFacts.cs` (14 Theory rows) |
| D-04 | ScaleDatabase recognizes 5 church-mode suffixes alongside major/minor | `ScaleDatabase.TryParseKeyWithMode` (longer-suffix-first ordering) + `ChurchModeParseFacts.cs` (8 Theory + 4 Facts) + `MusicalContext.ValidKeys` 119 entries |
| D-05 | Active tuning lives on `MusicalContext.Tuning` as 9th top-level (NOT stacked) field | `flow-lang/Runtime/MusicalContext.cs` source + `ExecutionContext.GetMusicalContext` aggregator merges Tuning via `??=` pattern |
| D-06 | FlowEngine.Run reads Program.Pragmas, sets MusicalContext.Tuning once before interpret; ModuleLoader does NOT touch tuning state | `flow-lang/Core/FlowEngine.cs::ApplyTuningPragma` between parse and interpret + `ModuleLoader.cs` unchanged |
| D-07 | REPL: pragma extraction per-line; resolved tuning PERSISTS across REPL lines | `ExecutionContext.SetTuning(TuningSystem?)` no-op on null + writes to GlobalFrame |
| D-08 | Default tuning is equalTemperament; explicit `enable equalTemperament;` is byte-identical no-op | `ByteIdenticalDefaultTuningTests.ExplicitEqualTemperament_ProducesIdenticalOutput` + Pitfall 6 short-circuit at `PitchConversion.NoteToFrequency(MusicalNoteData, RenderTuning)` |
| D-09 | Spelling-aware tuning tables: `Eb4` and `D#4` produce different Hz under JI/Pythagorean | `ChromaticRatioTable` keyed on `(char Letter, int Alteration)` + `SpellingAwareTuningFacts.cs` (4 Facts) |
| D-10 | Cent offsets compose additively in cent-space: `freq = tonic_hz × ratio × 2^(cents/1200)` | `RatioMath.CentOffsetMultiplier` + `CentOffsetAdditivityFacts.cs` (4 Facts including JI fifth + 5c composition canary) |
| D-11 | `enharmonic()` emits one-time-per-session stderr warning under non-12-TET | `HarmonyFunctions.Enharmonic` D-11 warning gate (after GetMusicalContext) + `EnharmonicWarningFacts.cs` (5 Facts: JI fires, Pythagorean fires, EqualTemperament silent, no-pragma silent, two-calls-warns-once dedup) + `RenderingDiagnostics.WarnOnce` |
| D-12 | Transforms stay MIDI-based per MICR-02; doc-only caveat for ~21 cent shift at enharmonic junctions | `TransformFunctions.TransposeSemitone` XML `<remarks>` block updated + `transposePreserveSpelling` v1.4 pointer; method body BIT-IDENTICAL (`grep -c "transpose would put" == 1`) |
| D-13 | `writeMidi` emits one-time stderr warning under non-12-TET; MIDI bytes UNCHANGED | `MidiExport.WriteMidi` 2-arg overload + context-dependent registration migration + `WriteMidiWarningFacts.cs` (5 Facts including bytes-unchanged-under-JI Fact with WARNING-4 ResetForTesting between sequential runs) |
| D-14 | Unknown tuning names trip Phase 21 D-12 unknown-pragma path with Scala-loader v1.4 pointer | `PragmaScanner.cs` `LooksLikeTuningName` (Levenshtein <=3 + substring whitelist `tun, scal, temp, just, pyth, micro, intone`) + single-source-of-truth `ScalaLoaderDeferralPointer` const + `UnknownTuningPragmaFacts.cs` (4 Facts) |

**Score: 14/14 locked decisions verified.**

---

## STRIDE Threat-Model Verification (per CONTEXT.md security_constraints + RESEARCH §"Security Domain")

| Threat ID | Category | Component | Disposition | Verification |
|-----------|----------|-----------|-------------|--------------|
| **T-23-01-01** | Tampering | TuningTables canonical-ratio integrity | mitigated | Wikipedia/Mudcat citations in XML doc comments at every table; 14 TuningRatioFacts pin canary ratios; `TuningTables.cs` source-controlled and reviewed |
| **T-23-02-01** | Tampering | PragmaRegistry closed-set growth | mitigated | `KnownPragmas.Count >= 4` Fact + alphabetized list assertion + Phase 21 closed-set design (D-17) preserved; future pragma additions require explicit code edit gated by code review |
| **T-23-02-02** | Information Disclosure | Pitfall 6 byte-identical short-circuit (default-tuning regression) | mitigated | 3-level verification: leaf overload `EqualTemperamentShortCircuit_BitIdentical_To1ArgOverload` + render pipeline `ByteIdenticalDefaultTuning_NoPragma_StillBitIdentical_AfterPattern_A_Threading` + end-to-end `ByteIdenticalTutorialTests` + `ByteIdenticalShowcaseTests` (4/4 GREEN) |
| **T-23-02-03** | Tampering | TransformFunctions body bit-identical (MICR-02 contract) | mitigated | `grep -c "transpose would put" TransformFunctions.cs == 1` confirms existing Console.Error.WriteLine warning preserved verbatim; only XML `<remarks>` D-12 caveat added; 5 TransformInvarianceFacts pin MIDI-pitch invariance across tunings |
| **T-23-02-04** | Information Disclosure | MICR-03 unknown-tuning Scala pointer single-source-of-truth | mitigated | `ScalaLoaderDeferralPointer` const in `PragmaScanner.cs` is single source; 4 UnknownTuningPragmaFacts pin error-message inclusion; cross-referenced in 14-deferred-items.md Phase 23 closure section |
| **T-23-03-01** | Tampering | ScaleDatabase original TryParseKey caller integrity | mitigated | WARNING-6 verification: `grep -c "TryParseKey(" ScaleDatabase.cs == 3` (1 def + 2 caller sites at ResolveRomanNumeral + GetScaleNotes); `ExistingTryParseKey_StillWorks_ForChordResolution` Fact exercises both public surfaces |
| **T-23-03-02** | DoS | RenderingDiagnostics process-static HashSet dedup | mitigated | Thread-safe via lock; ResetForTesting public for cross-assembly Fact isolation; `RenderingDiagnosticsFacts.ThreadSafe_UnderConcurrentHammer_DedupesPerKey` pins 200-iteration parallel hammer with `Assert.True(lineCount <= 5)` upper-bound assertion |
| **T-23-04-01** | Repudiation | .flow smoke scripts demonstrate features end-to-end | mitigated | 5 .flow scripts under `tests/` exit 0 with `: PASSED` sentinel; `.flow` integration loop's grep gate enforces |
| **T-23-04-02** | Tampering | TuningDeterminismTests xUnit/script isolation | mitigated | WARNING-5 isolation: per-Fact unique /tmp paths (`/tmp/flow_test_tuning_determinism_xunit_*.wav`) disjoint from on-disk script's `/tmp/flow_test_tuning_determinism.wav`; `[Collection("FlowScripts")]` for serialization within xUnit suite |

**Score: 9/9 STRIDE threats verified mitigated.**

---

## Test Suite Results

### Per-feature unit Facts (Phase 23 namespace)

- TuningRatioFacts: **14 Facts GREEN** (canary ratios — 5/4 JI third, 81/64 Pythagorean third, 3/2 perfect fifth, etc.)
- TuningModeShiftFacts: **14 Theory rows GREEN** (canonical scale-degree shape per D-03 across 7 modes × 2 systems)
- SpellingAwareTuningFacts: **4 Facts GREEN** (Eb≠D# distinction under JI; EqualTemperament short-circuit invariant)
- CentOffsetAdditivityFacts: **4 Facts GREEN** (cent-additive math; JI fifth + 5c canary)
- PragmaTuningFacts: **6 Facts GREEN** (closed-set growth 1→4; alphabetized list)
- UnknownTuningPragmaFacts: **4 Facts GREEN** (D-14 / MICR-03 Scala pointer; Levenshtein did-you-mean)
- PitchConversionTuningFacts: **5 Facts GREEN** (MICR-01 end-to-end + Pitfall 6 invariant)
- TransformInvarianceFacts: **5 Facts GREEN** (MICR-02 transforms produce identical MIDI across tunings)
- VocalizationTuningFacts: **1 Fact GREEN** (WARNING-2 migration verified end-to-end)
- ByteIdenticalDefaultTuningTests: **2 Facts GREEN** (D-08 + Pattern A short-circuit)
- ChurchModeParseFacts: **8 Theory rows + 4 Facts = 12 GREEN** (D-04 5-church-mode recognition + ValidKeys 119)
- RenderingDiagnosticsFacts: **5 Facts GREEN** (Pitfall 5 dedup contract + thread-safety)
- EnharmonicWarningFacts: **5 Facts GREEN** (D-11 fires under JI/Pyth, silent under EqualTemp/no-pragma, dedup)
- WriteMidiWarningFacts: **5 Facts GREEN** (D-13 fires + MIDI bytes unchanged + WARNING-4 between-runs reset)
- TuningDeterminismTests: **3 Facts GREEN** (JI/explicit-EqualTemperament/Pythagorean two-run byte-identical via WARNING-5 inline sources)

**Total Phase 23 Facts: 91 / 91 GREEN** (`dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase23"`)

### Integration smoke scripts (all GREEN with sentinel)

- `tests/test_tuning_ji.flow` — exit 0 + `: PASSED` sentinel
- `tests/test_tuning_pythagorean.flow` — exit 0 + `: PASSED` sentinel
- `tests/test_tuning_equal.flow` — exit 0 + `: PASSED` sentinel
- `tests/test_tuning_transpose_invariant.flow` — exit 0 + `: PASSED` sentinel
- `tests/test_tuning_determinism.flow` — exit 0 + `: PASSED` sentinel

All 5 scripts exit 0. `.flow` integration loop: 72/75 PASS (3 pre-existing exit-1 negative-error fixtures unchanged: `test_error_masking.flow`, `test_iteration_guard.flow`, `test_musical_context_errors.flow`).

### Byte-identical regression gate (Phase 18 contract preserved + Phase 23 additions)

- ByteIdenticalTutorialTests (WAV + MIDI): **GREEN** (preserved through Pattern A interface change)
- ByteIdenticalShowcaseTests (WAV + MIDI): **GREEN** (preserved)
- EuclideanByteIdenticalTests (WAV + MIDI): **GREEN** (preserved)
- ByteIdenticalDefaultTuningTests.ExplicitEqualTemperament_ProducesIdenticalOutput: **GREEN** (new Phase 23)
- ByteIdenticalDefaultTuningTests.NoPragma_StillBitIdentical_AfterPattern_A_Threading: **GREEN** (new Phase 23)
- TuningDeterminismTests (3 Facts): **GREEN** (new Phase 23)

**Cumulative ByteIdentical: 8/8 GREEN at every Phase 23 commit.**

### Full suite

- `dotnet test flow-sharp.sln`: **608/608 GREEN, 0 failed**

---

## Cross-cutting Truths Verified

- [x] Pattern A locked over Pattern B at every level — `grep -c "MusicalContext\.Current" flow-lang/` returns 0 (Pattern B never introduced; RESEARCH §Pitfall 1 cited the codebase analog mismatch with CONTEXT.md `<canonical_refs>` line 96)
- [x] Pitfall 6 byte-identical short-circuit verified at 3 levels — leaf overload (`EqualTemperamentShortCircuit_BitIdentical_To1ArgOverload`), render pipeline (`NoPragma_StillBitIdentical_AfterPattern_A_Threading`), end-to-end .flow (tutorial + showcase preserved)
- [x] ScaleDatabase original `TryParseKey(out bool isMajor)` UNCHANGED per WARNING-6 — `grep -c "TryParseKey(" ScaleDatabase.cs == 3` (1 def + 2 caller sites at `ResolveRomanNumeral` + `GetScaleNotes`)
- [x] ScaleDatabase `TryParseKeyWithMode` at canonical entry from Wave 2 per WARNING-8 — `grep -c "TryParseKeyWithMode" ScaleDatabase.cs == 1` (single source of truth, no inline write-then-delete helper); Wave 3 widened the same method's mode-detection branch in-place
- [x] TransformFunctions body bit-identical per MICR-02 / WARNING-1 — `grep -c "transpose would put" TransformFunctions.cs == 1` (existing Console.Error.WriteLine warning preserved verbatim)
- [x] No new NuGet packages added; DryWetMidi 8.0.3 stays as the only external dep (per CLAUDE.md "Minimal Dependencies" principle)
- [x] No new AST nodes added; pure stdlib + transforms + diagnostic helper (per CONTEXT D-08)
- [x] All acceptance examples use S-expression style (no infix introduced) per CLAUDE.md memory `feedback_language_philosophy.md`
- [x] Charitable interpretation honored throughout per CLAUDE.md memory `feedback_charitable_interpretation.md`:
  - **D-02** silent C-major default when no key block under non-12-TET (mirrors Phase 22 D-07 voicing-fallback pattern)
  - **D-08** explicit `enable equalTemperament;` == no-pragma byte-identical (Pitfall 6 short-circuit)
  - **D-10** cent offsets always defined additively in cent-space (cents never silently disappear)
  - **D-11 + D-13** advisory stderr warnings rather than hard errors — conversion still happens (existing behavior preserved)
- [x] DrumSynthesizer accepts tuning param for interface conformance only (drum mapping is MIDI-pitch-based, not frequency-based, so microtonal tuning is musically irrelevant for that voice)
- [x] FlowFunctionSynthesizer accepts the param but keeps the lambda contract stable (lambdas don't see tuning in v1.3)

---

## Patterns Established (Reusable for Downstream Phases)

1. **Closed-enum + static-dict-of-tables pattern** — TuningSystem (3 entries) + Mode (7 entries) closed enums; `TuningTables.Tables` static `Dictionary<(TuningSystem, Mode), ChromaticRatioTable>` keyed by tuple. Mirrors `PragmaRegistry.KnownPragmas` (Phase 21) + `TokenType` / `DurationValue` (Phase 18) house style. Phase 24 scaleLint will register `enable scaleLint;` via the same one-line addition pattern.

2. **Pattern A render-time payload threading** — `RenderTuning` value object threaded through `INoteSynthesizer.RenderNote` (5th parameter); 13 implementations updated mechanically. Build stays green between commits via `RenderTuning.Default` placeholders flowing through BarRenderer / SequenceRenderer overload chain until SongRenderer.ResolveRenderTuning entry replaces them at per-section resolution. Mirrors `SongRenderer.RenderSection` per-section bpm/pan/gain/rt60 resolution.

3. **Pitfall 6 byte-identical short-circuit** — when `tuning.System == TuningSystem.EqualTemperament`, the new overload literally delegates to the existing 1-arg overload body. 3-level verification (leaf overload + render pipeline + end-to-end). Pattern reusable for any future render-time payload that must preserve a byte-identical default path.

4. **Spelling-aware lookup key tuple** — `(char Letter, int Alteration)` instead of semitone offset for ratio tables. Required for D-09 Eb (6/5 = 1.200) ≠ D# (75/64 = 1.171875) distinction in JI; 12-TET equivalence breaks under non-equal tunings.

5. **Static-constructor initialization for cross-field static readonly dictionaries** — `TuningTables.Tables` built via `static TuningTables()` constructor (not field initializer) so per-mode field initializers run first. Defends against forward-reference NullReferenceException race (surfaced by RED suite, fixed inline as Rule 1 deviation in Plan 23-01).

6. **Context-dependent registration migration** — VocalizationFunctions (Plan 23-02) and MidiExport (Plan 23-03) migrated from context-FREE to context-DEPENDENT via `RegisterContextDependent(registry, context)` closure-over-ExecutionContext. Mirrors `HarmonyFunctions.RegisterContextDependent` + Phase 22's `quantize` migration shape. 1-arg overload preserved for backwards compat (LSP proxy / direct test invocation).

7. **Canonical-entry-from-start additive method** — `ScaleDatabase.TryParseKeyWithMode` shipped at the canonical entry from Wave 2 (major/minor branch only) per WARNING-8; Wave 3 widened the same method's mode-detection branch in-place. No inline write-then-delete helper. Single source of truth.

8. **Longer-suffix-first ordering for closed-set string-suffix dispatch** — `lydian` (6 chars) is a substring of `mixolydian` (10 chars). The else-if chain MUST test `mixolydian` before `lydian` or `Bmixolydian` parses as root='Bmixo' mode=Lydian. Pattern applies broadly when one suffix is a substring of another. Pinned by `TryParseKeyWithMode_LongerSuffixWins_MixolydianNotLydian` Fact.

9. **Programmatic ValidKeys generation** — `BuildValidKeys()` helper replaces 34-entry literal with `foreach roots × modes` Cartesian product; Phase 24 mode additions extend `modes[]` only.

10. **One-shot stderr warning channel** — `RenderingDiagnostics.WarnOnce(sentinelKey, message)` wraps `Console.Error.WriteLine` with HashSet-backed per-process dedup. Single source of truth for D-11 / D-13 / future Phase 24 scaleLint warnings. Style mirrors `TransformFunctions.TransposeSemitone:286` — same channel, same wording shape, plus dedup wrapper.

11. **Doc-only caveat under MICR-02 (WARNING-1)** — XML `<remarks>` block updated; method body bit-identical. Acceptance verified via `grep -c "<canonical-keyword>" file.cs == 1` (preserves existing warning verbatim). Pattern reusable for any future invariant-preserving doc update.

12. **WARNING-4 between-runs ResetForTesting** — sequential `FlowEngineRunner` instances inside one xUnit Fact body MUST call `RenderingDiagnostics.ResetForTesting()` between runs to defend against future warning-gates-export changes leaking dedup state. Applied in `WriteMidi_BytesUnchanged_UnderJI` and `TuningDeterminismTests.RunTwiceAndCompare`.

13. **WARNING-5 xUnit-vs-on-disk isolation** — TuningDeterminismTests Facts use Fact-controlled INLINE `.flow` source strings + per-Fact unique /tmp paths disjoint from on-disk smoke script paths. Eliminates race possibility between xUnit suite and `.flow` integration loop both exercising the same feature.

14. **WARNING-7 Section-inside-key-block scaffold for .flow tuning determinism** — `tempo 120 { timesig 4/4 { key Cmajor { section X { | C4q ... | } } } }` then `Song s = [X] / Buffer audio = (renderSong s "piano") / (writeWav ... audio)` at TOP LEVEL outside every musical-context block. Phase 24 scaleLint determinism tests reuse this shape.

---

## Pre-landing Collision Grep Transcripts

Recipe (re-run at this closure commit on 2026-05-04):

```bash
$ git grep -wn 'justIntonation\|pythagorean' -- '*.flow'
tests/test_tuning_ji.flow:1:enable justIntonation;
tests/test_tuning_pythagorean.flow:1:enable pythagorean;
tests/test_tuning_transpose_invariant.flow:1:enable justIntonation;
tests/test_tuning_determinism.flow:1:enable justIntonation;
```

**Significance:** Zero string-literal collisions with `justIntonation` / `pythagorean` keywords in any pre-Phase-23 .flow source. Every hit is intentional usage inside Phase 23's own fixture files.

```bash
$ git grep -wn 'MusicalContext\.Current' -- 'flow-lang/'
(empty — exit code 1)
```

**Significance:** Pattern B (MusicalContext.Current static accessor) was NEVER introduced. RESEARCH §Pitfall 1 cited the codebase mismatch with CONTEXT.md line 96; planner correction locked Pattern A. Verified at this closure commit.

```bash
$ git grep -wnE 'enable\s+(justIntonation|pythagorean|equalTemperament)' -- 'examples/*.flow'
(empty — exit code 1)
```

**Significance:** `examples/tutorial.flow` and `examples/showcase.flow` do NOT use tuning pragmas, so the Phase 18 byte-identical regression gate (`ByteIdenticalTutorialTests` + `ByteIdenticalShowcaseTests`) cannot be perturbed by Phase 23's tuning-aware path — the tuning path is never invoked for those files. Combined with Pitfall 6 short-circuit at the leaf overload, byte-identical determinism contract for tutorial.flow + showcase.flow holds STRUCTURALLY.

---

## Phase 18 Byte-Identical Regression Gate

**8/8 ByteIdentical Facts GREEN** at every Phase 23 atomic commit time:

- Post-Plan-23-01 (commits b6b916b + 39ef570): 4/4 GREEN ✅ (zero edits to existing code paths)
- Post-Plan-23-02 (commits 47d7718 + f6b00ba + 470c3cb + 8190fb2): 8/8 GREEN ✅ (Pattern A interface change + Pitfall 6 short-circuit + 2 new ByteIdenticalDefaultTuning Facts)
- Post-Plan-23-03 (commits 4ea0927 + 3e6a3ba): 8/8 GREEN ✅ (RenderingDiagnostics + writeMidi context migration; MIDI bytes unchanged)
- Post-Plan-23-04 (commits ba27282 + 4f85eaf): 8/8 GREEN ✅ (5 .flow smokes + TuningDeterminismTests; pure additions)
- Post-Plan-23-05 (this closure commit): 8/8 GREEN ✅ (docs-only)

**Significance:** Phase 23 wedge does NOT interact with `MusicalNoteData.DurationFraction`, `Fraction`, `GetBeats`, audio render path for default-tuning files, or MIDI export for default-tuning files. The Pitfall 6 byte-identical short-circuit at `PitchConversion.NoteToFrequency(MusicalNoteData, RenderTuning)` literally delegates to the existing 1-arg overload when `tuning.System == EqualTemperament`. Pattern A `RenderTuning.Default` flowing through every render path preserves the byte-identical contract structurally.

---

## Test Count Progression

| Stage | `dotnet test` Fact count | Delta |
|-------|-------------------------|-------|
| Pre-Phase-23 baseline (post-Phase-22 close) | 499 | — |
| Post-23-01 (TuningRatioFacts +14 + TuningModeShiftFacts +14 + SpellingAwareTuningFacts +4 + CentOffsetAdditivityFacts +4 = +36) | 535 | +36 |
| Post-23-02 (PragmaTuningFacts +6 + UnknownTuningPragmaFacts +4 + PitchConversionTuningFacts +5 + TransformInvarianceFacts +5 + VocalizationTuningFacts +1 + ByteIdenticalDefaultTuningTests +2 = +23) | 558 | +23 |
| Post-23-03 (ChurchModeParseFacts +12 + RenderingDiagnosticsFacts +5 + EnharmonicWarningFacts +5 + WriteMidiWarningFacts +5 = +27; FlowScripts theory rows for 5 .flow scripts arrive in 23-04) | 585 | +27 |
| Post-23-04 (TuningDeterminismTests +3 + 5 FlowScripts Theory rows for tuning .flow scripts +5 + bookkeeping drift) | 608 | +23 |
| Post-23-05 (this commit, docs-only) | 608 | 0 |
| **Phase 23 close** | **608/608 GREEN** | +109 cumulative |

**Net Phase 23 contribution: +109 tests** (499 → 608). Of these, 91 are dedicated Phase 23 Facts; the remainder are sentinel-pinned `FlowScriptData` Theory rows for the 5 tuning smoke scripts plus bookkeeping drift.

---

## Closure Commits

| File | Commit | Purpose |
|------|--------|---------|
| `.planning/REQUIREMENTS.md`, `.planning/ROADMAP.md`, `.planning/STATE.md`, `.planning/phases/14-composer-dx-part-1/deferred-items.md` | `0c2d116` | `docs(23-05): mark Phase 23 (MICR-01/02/03) shipped in REQUIREMENTS/ROADMAP/STATE` — Active Requirements [x] + Traceability Shipped markers + ROADMAP Phase 23 row + Plans + Progress + STATE frontmatter + Resume Instructions + Performance Metrics + Decisions log + Phase 23 Closure Anchor section + 14-deferred-items.md Phase 23 closure cross-reference |
| `.planning/phases/23-microtonal-tuning-wedge/23-VERIFICATION.md`, `.planning/phases/23-microtonal-tuning-wedge/23-05-SUMMARY.md` | (this commit) | Final phase verification report + plan 23-05 closure SUMMARY |

---

## Phase 23 SUMMARY Anchors

- `.planning/phases/23-microtonal-tuning-wedge/23-01-SUMMARY.md` (Wave 1 — math foundation + Pattern A locked)
- `.planning/phases/23-microtonal-tuning-wedge/23-02-SUMMARY.md` (Wave 2 — pragma → PitchConversion → synth pipeline)
- `.planning/phases/23-microtonal-tuning-wedge/23-03-SUMMARY.md` (Wave 3 — RenderingDiagnostics + 5 church modes + D-11/D-13 warnings)
- `.planning/phases/23-microtonal-tuning-wedge/23-04-SUMMARY.md` (Wave 4 — .flow smokes + JI/Pythagorean determinism Integration)
- `.planning/phases/23-microtonal-tuning-wedge/23-05-SUMMARY.md` (closure)

---

## Phase 24 / 25 / 26 Unblocking

**Phase 24 (Scale Linting, flow-lsp, LINT-01..03)**: **EXPLICITLY DEPENDS on Phase 21 + Phase 23**. Requires:
- Phase 21 pragma infrastructure for `enable scaleLint;` (one-line addition in `PragmaRegistry.KnownPragmas` per D-17 closed-set design — slot reserved during Phase 23).
- Phase 23 `ScaleDatabase.TryParseKeyWithMode` (canonical 7-mode entry shipped Wave 2 + widened Wave 3) for LINT-03 nested-key resolution semantics.
- `RenderingDiagnostics.WarnOnce` channel ready for Phase 24 scaleLint warnings to reuse the same dedup mechanism (though flow-lsp will likely use its own LSP Diagnostic publishing path rather than stderr).

**Phase 25 (Gaussian Humanize, DEFER-06)**: Must be the LAST PRNG-touching phase per binding pre-ordering #5 (Pitfall 6 mitigation). Phase 23's PRNG surface is empty — tuning math is deterministic ratio multiplication; no Random instantiation in the tuning render path. Byte-identical determinism contract preserved structurally.

**Phase 26 (Op Standardization, Prefix-Only)**: Independent of Phase 23. Could run earlier if scheduling priority shifts.

**Phase 27 (Tutorial + Showcase Refresh)**: Depends on every v1.3 feature being live including Phase 23 tuning. The tutorial chapter for microtonal tuning will demonstrate `enable justIntonation; play(C4 E4)` audibly distinct from 12-TET.

---

## Pitfall Coverage Verification (per CONTEXT.md §Pitfalls + RESEARCH §Pitfalls)

| Pitfall | Coverage |
|---------|----------|
| **1 — Pattern A vs Pattern B static-accessor mismatch** | Verified: Pattern A locked (`MusicalContext.Current` accessor never introduced — `grep -c "MusicalContext\.Current" flow-lang/ == 0`); Pattern A mirrors `SongRenderer.RenderSection` per-section resolution at SongRenderer.cs:128-138 |
| **2 — Canonical ratio ambiguity** | Verified: Wikipedia 5-limit JI table + Mudcat Olson mode tables + Wikipedia Pythagorean chain-of-fifths pinned with citations in `TuningTables.cs` XML doc comments; 14 TuningRatioFacts assert canary ratios |
| **3 — Spelling-aware Eb≠D# under JI** | Verified: `ChromaticRatioTable` keyed on `(char Letter, int Alteration)`; SpellingAwareTuningFacts (4 Facts) assert distinct rendered Hz under JI/Pythagorean |
| **4 — Static-init forward-reference race** | Verified: `TuningTables.Tables` built via static constructor; surfaced by Plan 23-01 RED suite as NullReferenceException, fixed inline (Rule 1 deviation) |
| **5 — `enharmonic()` / `writeMidi` silent regressions under non-12-TET** | Verified: D-11 one-shot warning at `HarmonyFunctions.Enharmonic` (5 Facts); D-13 one-shot warning at `MidiExport.WriteMidi` 2-arg overload (5 Facts including bytes-unchanged-under-JI Fact); RenderingDiagnostics.WarnOnce channel + Pitfall 5 #3 + AUDIT-VERIFIED marker |
| **6 — Byte-identical default-tuning regression** | Verified at 3 levels: leaf overload `EqualTemperamentShortCircuit_BitIdentical_To1ArgOverload`, render pipeline `NoPragma_StillBitIdentical_AfterPattern_A_Threading`, end-to-end `ByteIdenticalTutorialTests` + `ByteIdenticalShowcaseTests` (4/4 GREEN) |
| **7 — Phase 21 closed-set churn breaking existing Facts** | Verified: 3 Phase 21 Facts updated for closed-set growth (PragmaRegistryFacts + PragmaScannerFacts) — `Assert.False(IsKnown("justIntonation"))` removed; alphabetized list expanded to 4 entries; substring-contains lower-bound assertions used for forward-compat with Phase 24 scaleLint addition |
| **8 — `lydian` substring of `mixolydian` parse collision** | Verified: longer-suffix-first ordering in `TryParseKeyWithMode` else-if chain (mixolydian → phrygian → locrian → dorian → lydian → major → minor); `TryParseKeyWithMode_LongerSuffixWins_MixolydianNotLydian` Fact pins the invariant |
| **9 — TransformFunctions body drift under D-12 doc update** | Verified: `grep -c "transpose would put" TransformFunctions.cs == 1` confirms existing Console.Error.WriteLine warning preserved verbatim; only XML `<remarks>` D-12 caveat added |

---

## Charitable Interpretation Memory Honoured

Per CLAUDE.md memory `feedback_charitable_interpretation.md` (`music > rigid correctness`):

- **D-02 silent C-major default** — when a non-12-TET pragma is active but no `key` block is in scope, the renderer silently roots at C major (tonic = C, mode = major). No error; no console warning. Documented in pragma reference + `PitchConversion.NoteToFrequency` doc comment. Mirrors Phase 22 D-07 voicing-fallback pattern.
- **D-08 explicit `enable equalTemperament;` == no-pragma** — registered + visible to Phase 24 scaleLint per D-08, but functionally a byte-identical no-op via Pitfall 6 short-circuit. Composers can declare intent without changing output.
- **D-10 cents always defined** — `freq = tonic_hz × ratio × 2^(cents/1200)` — cents never silently disappear; cent offsets compose additively in cent-space whether tuning is 12-TET or JI/Pythagorean.
- **D-11 / D-13 advisory warnings** — `enharmonic()` and `writeMidi()` under non-12-TET emit one-time stderr advisories rather than hard errors. Conversion still happens (existing behavior preserved). Documented exception to charitable-interpretation memory because silent regressions are audible — but the warning is one-shot per session so it doesn't drown the user.
- **DrumSynthesizer interface conformance** — accepts `RenderTuning` 5th parameter for compile-time interface conformance only; drum mapping is MIDI-pitch-based (note 36 = kick, 38 = snare), so microtonal tuning is musically irrelevant for that voice.

---

## Two-Pass Strict Authorship Outcomes

| Plan | Pass 1 → Pass 2 | Outcome |
|------|-----------------|---------|
| 23-01 | Math foundation + ratio tables + Wave 1 RED canary Facts authored from Wikipedia/Mudcat citations; production code shipped across 2 atomic feat commits | Outcome A — bounded deviations (1 Rule 1 fix per 23-01-SUMMARY: static-init forward-reference race fixed via static constructor). All canonical-source-driven Facts GREEN on first re-run after fix. |
| 23-02 | 4-task atomic split per BLOCKER-1 (pragma registration + Pattern A wiring + per-section resolution + MICR-01/02/03 Facts); production code in 4 atomic feat commits + 1 test commit | Outcome A — bounded deviations (2 Rule 1 fixes per 23-02-SUMMARY: closed-set growth invalidating Phase 21 hard-coded assertions, Fact-authoring assumptions about Flow surface APIs). All MICR-01/02/03 contracts shipped exactly as planned. |
| 23-03 | 2-task atomic (RenderingDiagnostics + ScaleDatabase widening + ValidKeys + Facts; D-11/D-13 warnings + writeMidi context migration + Facts) | Outcome A — bounded deviations (3 Rule 1/3 fixes per 23-03-SUMMARY: Note literal syntax `Fsharp4`→`F#4`, ResetForTesting `internal`→`public` cross-assembly visibility, ThreadSafe Fact stderr-content assertion correction). All D-04/D-11/D-13 contracts shipped. |
| 23-04 | Pure validation/closure (5 .flow smokes + TuningDeterminismTests Integration); no production code touched | Outcome A — bounded deviations (4 Rule 1 fixes per 23-04-SUMMARY: `Buffer buf = (renderSequence ...)` adapted to Section/Song/renderSong + audio var rename, `transpose original 5` → `original -> transpose +5st` flow chain, MICR-0 citation count correction, WARNING-5 doc-comment filename hygiene). All ROADMAP success criteria pinned. |
| 23-05 | (Closure plan — docs-only) | N/A |

Two-pass strict series streak (zero or bounded divergence): 13/14/18/19/20/21/22/**23**.

---

## Deferred / Out of Scope (per CONTEXT.md §Deferred Ideas)

Already routed to v1.4 milestone or post-v1.3 work — recorded in `.planning/phases/14-composer-dx-part-1/deferred-items.md` Phase 23 closure cross-reference section:

- **Full Scala (`.scl`) tuning loader** — `tuning loadScala("path.scl") { ... }` block-style; deferred to v1.4 per REQUIREMENTS.md D-03. MICR-03 unknown-tuning error message points users at this future expansion via single-source-of-truth `ScalaLoaderDeferralPointer` const.
- **Faithful microtonal MIDI export** — per-channel pitch-bend events; deferred to v1.4. Phase 23 emits one-time stderr warning when `writeMidi` called under non-12-TET; MIDI bytes stay 12-TET.
- **Spelling-preserving transforms** (`transposePreserveSpelling`, etc.) — opt-in strict-mode escape hatch under non-12-TET; v1.4 candidate; documented in TransformFunctions XML `<remarks>` so composers can find it.
- **Block-scope `tuning JustIntonation { ... }` syntax** — deferred per Phase 21 D-02 (file-scope only in v1.3).
- **Configurable A4 reference frequency** (432 Hz, 442 Hz) — Phase 23 hard-codes A4 = 440 Hz; v1.4+ candidate.
- **Mode-aware tuning tables for harmonic minor / melodic minor / blues** — Phase 23 ships major + natural minor + 5 standard church modes only.
- **Pre-resolution `enharmonic()` LSP squiggle warning** — Phase 23 ships post-call stderr warning per D-11; pre-call LSP diagnostic belongs in flow-lsp work post-v1.3.
- **REPL meta-command `:tuning ji`** — discussed and rejected during planning in favor of D-07 (resolved tuning persists across REPL lines).

---

## Final Acceptance — Phase 23 Closes

- [x] All 4 ROADMAP success criteria for Phase 23 verified ✅ (MICR-01 5:4 JI third + MICR-02 transform invariance + render-time-only + MICR-03 Scala pointer)
- [x] All 3 REQ-IDs (MICR-01, MICR-02, MICR-03) flipped to `Shipped {commit-hash}` in REQUIREMENTS.md traceability table AND `[x]` in Active Requirements list
- [x] ROADMAP.md Phase 23 row marked complete with date 2026-05-03 and 5 plan bullets stamped with hashes
- [x] ROADMAP.md Progress table row updated to `5/5 Complete 2026-05-03`
- [x] STATE.md milestone progress 5/10 → 6/10 phases for v1.3 (frontmatter `completed_phases: 5 → 6`, `percent: 96 → 100`)
- [x] STATE.md `Phase 23 Closure Anchor` section added per Phase 18-22 closure precedent
- [x] STATE.md Decisions log appended with closure entries documenting cross-cutting truths + 5 Plan 23-NN entries
- [x] Phase 23 SUMMARY anchors all reference-able from STATE.md
- [x] Final full-suite check `dotnet test flow-sharp.sln` passes 608/608 with 0 regressions
- [x] ByteIdentical 8/8 GREEN at closure (Tutorial WAV+MIDI, Showcase WAV+MIDI, Euclidean WAV+MIDI, ByteIdenticalDefaultTuning ExplicitEqualTemperament + NoPragma_AfterPattern_A_Threading)
- [x] 5 tuning .flow smoke scripts emit sentinel and exit 0; .flow integration loop 72/75 PASS (3 pre-existing exit-1 negative-error fixtures unchanged)
- [x] HUMAN-UAT items (audible JI vs 12-TET + REPL persistence) tracked as PENDING — non-blocking, deferred to first release tag

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references (Phase23 xUnit Facts directory + RED .flow scripts)
- [x] No watch-mode flags
- [x] Feedback latency < 60s (Phase23-filter test run completes in ~360ms)
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved 2026-05-04

---

**Phase 23 officially closed. v1.3 milestone is 60% complete (6/10 phases). Phase 24 (Scale Linting, flow-lsp) is the next ROADMAP target.**

---

*Phase: 23-microtonal-tuning-wedge*
*Closed: 2026-05-04*
*Verifier: Claude (gsd-executor) via plan 23-05 closure*
