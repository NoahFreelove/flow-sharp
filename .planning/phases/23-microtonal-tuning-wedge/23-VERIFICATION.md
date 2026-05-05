---
phase: 23
slug: microtonal-tuning-wedge
status: shipped
verified: 2026-05-03T00:00:00Z
verifier: gsd-verifier (goal-backward)
score: 4/4 ROADMAP success criteria + 14/14 locked decisions + 91/91 Phase23 Facts + 8/8 ByteIdentical + 5/5 .flow smokes + 608/608 full suite
overrides_applied: 0
must_haves_verified: 4
must_haves_total: 4
deferred: []
re_verification:
  previous_status: shipped (executor draft)
  previous_score: 4/4
  gaps_closed: []
  gaps_remaining: []
  regressions: []
gaps: []
---

## VERIFICATION PASSED

**Phase 23 (Microtonal Tuning, Wedge) goal-backward verified at the codebase level on 2026-05-03.** Three named-tuning pragmas (`enable justIntonation;` / `enable pythagorean;` / `enable equalTemperament;`) ship as a render-time wedge per ROADMAP D-03; transforms remain MIDI-pitch invariant per MICR-02; unknown tunings route to the Phase 21 D-12 unknown-pragma path with the canonical Scala-loader v1.4 deferral pointer per MICR-03. All 4 ROADMAP success criteria are pinned by GREEN xUnit Facts at the canonical render boundary AND by GREEN `.flow` smoke scripts; the Phase 18 byte-identical regression contract holds at 8/8. Verifier ran fresh `dotnet build` (0 errors), the Phase23 filter (91/91 GREEN), the ByteIdentical filter (8/8 GREEN), and all 5 tuning `.flow` smokes (5/5 exit 0 + `: PASSED` sentinel). The executor's draft VERIFICATION.md is rewritten as canonical here without scope reduction.

---

## Goal-Backward: ROADMAP Success Criteria → Cited Facts

| # | Criterion (ROADMAP) | Cited Fact / Smoke | Verified at Run | Commit |
|---|--------------------|-----|---|---|
| 1 | `enable justIntonation;` makes `play(C4 E4)` produce ratio 5:4 (1.25) — not 12-TET ~1.2599; pythagorean and equalTemperament also ship (MICR-01) | `TuningRatioFacts.JustMajor_CtoE_Is5to4` + `PitchConversionTuningFacts.PitchConversionEndToEnd_JI_CtoE_FrequencyRatio_Is5to4` (asserts `eFreq/cFreq == 5.0/4.0` at precision 10 at the render boundary) + `tests/test_tuning_ji.flow` smoke | xUnit GREEN + smoke exit 0 with `JI ratio applied` + `: PASSED` | f6b00ba + 8190fb2 + ba27282 |
| 2 | `transpose(seq, 5)` produces same MIDI pitch numbers under every tuning; only frequencies differ (MICR-02) | `TransformInvarianceFacts` (5 Facts × transpose/invert/retrograde/augment/diminish, MIDI invariance asserted across JI / Pythagorean / 12-TET) + `tests/test_tuning_transpose_invariant.flow` smoke | xUnit GREEN + smoke exit 0 with `MIDI invariance preserved` + `: PASSED` | 8190fb2 + ba27282 |
| 3 | Tuning system applies at render-time only — transforms remain pitch-class-based and tuning-agnostic (MICR-02) | `TransformInvarianceFacts` (transforms produce identical MIDI shape across tunings — render-time-only contract) + `grep -c "transpose would put" TransformFunctions.cs == 1` (verified — body bit-identical, only XML `<remarks>` D-12 caveat added) + Pattern A `RenderTuning` payload threading at PitchConversion / synth interface | xUnit GREEN + grep verified | b6b916b + f6b00ba + 8190fb2 |
| 4 | Unknown tuning name raises clear error pointing at v1.4 Scala loader (MICR-03) | `UnknownTuningPragmaFacts.UnknownTuning_ErrorIncludesScalaPointer` + `UnknownTuningPragmaFacts.UnknownTuning_DidYouMean_FromLevenshtein` + `UnknownTuningPragmaFacts.UnknownTuning_ErrorContainsAlphabetizedList` + `UnknownTuningPragmaFacts.UnknownNonTuningPragma_DoesNotIncludeScalaPointer` (4 Facts; canonical pointer `Full Scala (.scl) loader is documented as deferred to v1.4 — see ADR/REQUIREMENTS.md D-03.` lives in `PragmaScanner.ScalaLoaderDeferralPointer` const) | xUnit GREEN | 47d7718 |

**Score: 4/4 ROADMAP success criteria verified at the codebase level.**

---

## Test Gates (verifier ran fresh on 2026-05-03)

| Gate | Command | Expected | Observed | Status |
|---|---|---|---|---|
| Build | `dotnet build flow-sharp.sln` | 0 errors / 0 warnings | 0 errors / 0 warnings | ✅ PASS |
| Phase 23 filter | `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase23"` | 91/91 GREEN | 91 passed / 0 failed / 1.33s | ✅ PASS |
| ByteIdentical contract | `dotnet test flow-lang.Tests --filter "FullyQualifiedName~ByteIdentical"` | 8/8 GREEN (tutorial WAV + MIDI, showcase WAV + MIDI, euclidean WAV + MIDI, ByteIdenticalDefaultTuning ×2) | 8 passed / 0 failed / 7.0s | ✅ PASS |
| Full suite | `dotnet test flow-sharp.sln` | 608/608 GREEN | 608 passed / 0 failed / 24s | ✅ PASS |
| `.flow` smoke: JI | `dotnet run --project flow-interpreter tests/test_tuning_ji.flow` | exit 0 + `: PASSED` | exit 0; `JI ratio applied`; `test_tuning_ji: PASSED` | ✅ PASS |
| `.flow` smoke: Pythagorean | `dotnet run --project flow-interpreter tests/test_tuning_pythagorean.flow` | exit 0 + `: PASSED` | exit 0; `Pythagorean ratio applied`; `test_tuning_pythagorean: PASSED` | ✅ PASS |
| `.flow` smoke: Equal | `dotnet run --project flow-interpreter tests/test_tuning_equal.flow` | exit 0 + `: PASSED` | exit 0; `EqualTemperament explicit no-op`; `test_tuning_equal: PASSED` | ✅ PASS |
| `.flow` smoke: Transpose invariant | `dotnet run --project flow-interpreter tests/test_tuning_transpose_invariant.flow` | exit 0 + `: PASSED` | exit 0; `MIDI invariance preserved`; `test_tuning_transpose_invariant: PASSED` | ✅ PASS |
| `.flow` smoke: Determinism | `dotnet run --project flow-interpreter tests/test_tuning_determinism.flow` | exit 0 + `: PASSED` | exit 0; `JI deterministic render complete`; `test_tuning_determinism: PASSED` | ✅ PASS |

**All 9 automated gates GREEN.**

---

## Decision Coverage D-01..D-14 — Implementation Sites Verified

Each decision verified by reading the cited file and confirming the substantive code is present (not just a stub).

| Decision | Description | Implementation Site Verified | Status |
|----------|-------------|------------------------------|--------|
| D-01 | JI/Pythagorean tonic resolves from innermost active `MusicalContext.Key` | `flow-lang/StandardLibrary/Audio/SongRenderer.cs::ResolveRenderTuning` (per-section + key block lookup) | ✅ |
| D-02 | Non-12-TET pragma without key block silently roots at C major | `SongRenderer.ResolveRenderTuning` C-major fallback path; `PitchConversion.cs:46` doc comment confirms tonic = ('C', 0); `ByteIdenticalDefaultTuningTests.NoPragma_StillBitIdentical_AfterPattern_A_Threading` GREEN | ✅ |
| D-03 | Mode shifts the chromatic ratio table — 7 modes × 2 systems = 14 ratio tables | `flow-lang/StandardLibrary/Audio/Tuning/TuningTables.cs` (14 static tables) + `TuningRatioFacts.Tables_HasExactly14Entries` GREEN + `TuningModeShiftFacts` (14 Theory rows) GREEN | ✅ |
| D-04 | `ScaleDatabase` recognizes 5 church-mode suffixes alongside major/minor | `flow-lang/StandardLibrary/Harmony/ScaleDatabase.cs:207` defines `TryParseKeyWithMode`; longer-suffix-first ordering pinned by `ChurchModeParseFacts.TryParseKeyWithMode_LongerSuffixWins_MixolydianNotLydian` Fact GREEN; `MusicalContext_ValidKeys_HasExpectedCount` Fact GREEN (119 entries) | ✅ |
| D-05 | Active tuning lives on `MusicalContext.Tuning` as 9th top-level (NOT stacked) field | `flow-lang/Runtime/MusicalContext.cs:59` declares `public TuningSystem? Tuning { get; set; }` | ✅ |
| D-06 | `FlowEngine.Run` reads `Program.Pragmas`, sets `MusicalContext.Tuning` once before interpret; `ModuleLoader` does NOT touch tuning state | `flow-lang/Core/FlowEngine.cs:95` calls `ApplyTuningPragma(program)` between parse and interpret; `ApplyTuningPragma` (lines 110-125) inspects `program.Pragmas.Has("justIntonation"/"pythagorean"/"equalTemperament")` | ✅ |
| D-07 | REPL: pragma extraction per-line; resolved tuning PERSISTS across REPL lines | `ExecutionContext.SetTuning(TuningSystem?)` no-op on null + writes to GlobalFrame; `FlowEngine.cs:113-114` doc comment confirms persistence semantics | ✅ |
| D-08 | Default tuning is `equalTemperament`; explicit `enable equalTemperament;` is byte-identical no-op | `ByteIdenticalDefaultTuningTests.ExplicitEqualTemperament_ProducesIdenticalOutput` Fact GREEN; Pitfall 6 short-circuit at `PitchConversion.cs:66` (`if (tuning.System == TuningSystem.EqualTemperament) return ... 1-arg overload`) | ✅ |
| D-09 | Spelling-aware tuning tables: `Eb4` and `D#4` produce different Hz under JI/Pythagorean | `flow-lang/StandardLibrary/Audio/Tuning/ChromaticRatioTable.cs:9-10` declares `IReadOnlyDictionary<(char Letter, int Alteration), double> Ratios`; `SpellingAwareTuningFacts` (4 Facts) GREEN | ✅ |
| D-10 | Cent offsets compose additively in cent-space: `freq = tonic_hz × ratio × 2^(cents/1200)` | `flow-lang/StandardLibrary/Audio/Tuning/RatioMath.cs::CentOffsetMultiplier` + `CentOffsetAdditivityFacts` (4 Facts) GREEN | ✅ |
| D-11 | `enharmonic()` emits one-time-per-session stderr warning under non-12-TET | `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs:58-60` calls `RenderingDiagnostics.WarnOnce("enharmonic-non-equal-temperament", "[enharmonic] called inside tuning != equalTemperament; conversion is destructive (≈ 21 cent shift)")`; `EnharmonicWarningFacts` (5 Facts) GREEN — JI fires, Pythagorean fires, EqualTemperament silent, no-pragma silent, two-calls-warns-once dedup | ✅ |
| D-12 | Transforms stay MIDI-based per MICR-02; doc-only caveat for ~21 cent shift | `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` body BIT-IDENTICAL — verified `grep -c "transpose would put" TransformFunctions.cs == 1`; XML `<remarks>` block updated with v1.4 `transposePreserveSpelling` pointer; `TransformInvarianceFacts` (5 Facts) GREEN pin MIDI-pitch invariance | ✅ |
| D-13 | `writeMidi` emits one-time stderr warning under non-12-TET; MIDI bytes UNCHANGED | `flow-lang/StandardLibrary/Audio/MidiExport.cs:168-170` calls `RenderingDiagnostics.WarnOnce(...)` when `musicalCtx?.Tuning is TuningSystem activeTuning && activeTuning != TuningSystem.EqualTemperament`; `WriteMidiWarningFacts` (5 Facts including `WriteMidi_BytesUnchanged_UnderJI`) GREEN | ✅ |
| D-14 | Unknown tuning names trip Phase 21 D-12 unknown-pragma path with Scala-loader v1.4 pointer | `flow-lang/Lexing/PragmaScanner.cs:31-32` declares `private const string ScalaLoaderDeferralPointer = "Full Scala (.scl) loader is documented as deferred to v1.4 — see ADR/REQUIREMENTS.md D-03.";` + `LooksLikeTuningName` (line 41) Levenshtein/substring; `UnknownTuningPragmaFacts` (4 Facts) GREEN | ✅ |

**Score: 14/14 locked decisions verified by direct codebase inspection.**

---

## must_haves Audit (per plan)

Verified that every plan's `must_haves.truths` block has shipping evidence:

| Plan | must_haves Coverage | Evidence | Status |
|------|---------------------|----------|--------|
| 23-01 (math foundation + ratio tables) | TuningSystem (3 entries), Mode (7 entries), TuningTables 14 tables, RatioMath helpers, 30+ Facts as Pass 1 RED | All 6 production files present at `flow-lang/StandardLibrary/Audio/Tuning/`; 4 Fact files in `Phase23/`; ratio canaries (5/4 JI third, 81/64 Pythagorean third, 3/2 fifth) all GREEN | ✅ |
| 23-02 (pragma → PitchConversion → synth pipeline) | 3 tuning pragmas registered; `MusicalContext.Tuning` 9th field; FlowEngine bridge; 2-arg PitchConversion overload + Pitfall 6 short-circuit; 13 synth call sites threaded; MICR-01/02/03 Facts | `MusicalContext.cs:59` + `FlowEngine.cs:95,116-125` + `PitchConversion.cs:57-87` (2-arg overload + short-circuit) verified; PragmaTuningFacts (6) + UnknownTuningPragmaFacts (4) + PitchConversionTuningFacts (5) + TransformInvarianceFacts (5) + ByteIdenticalDefaultTuningTests (2) all GREEN | ✅ |
| 23-03 (RenderingDiagnostics + 5 church modes + D-11/D-13 warnings) | `RenderingDiagnostics.WarnOnce` channel; `ScaleDatabase.TryParseKeyWithMode` widened to 7 modes; D-11 enharmonic warning; D-13 writeMidi warning; writeMidi context-dependent registration | `flow-lang/Diagnostics/RenderingDiagnostics.cs` exists; `ScaleDatabase.cs:207` `TryParseKeyWithMode` longer-suffix-first; `HarmonyFunctions.cs:58-60` D-11 warning gate; `MidiExport.cs:168-170` D-13 warning gate; ChurchModeParseFacts (12) + RenderingDiagnosticsFacts (5) + EnharmonicWarningFacts (5) + WriteMidiWarningFacts (5) all GREEN | ✅ |
| 23-04 (`.flow` smokes + determinism Integration) | 5 `.flow` smoke scripts exit 0 with sentinel; TuningDeterminismTests Integration class JI/explicit-EqualTemperament/Pythagorean two-run byte-identical | All 5 scripts present at `tests/test_tuning_*.flow`; verifier ran each fresh; all exit 0 with `: PASSED` sentinel; `TuningDeterminismTests` (3 Facts) all GREEN | ✅ |
| 23-05 (closure docs) | REQUIREMENTS/ROADMAP/STATE/14-deferred-items updates | REQUIREMENTS.md MICR-01/02/03 marked `[x]` and Shipped; ROADMAP Phase 23 row marked complete with 5 plan bullets + commits; STATE.md `completed_phases: 6`; 14-deferred-items.md Phase 23 closure cross-reference section present | ✅ |

**Score: 5/5 plans' must_haves shipped with codebase evidence.**

---

## REQ-ID Traceability

| REQ-ID | SPEC acceptance | Pinning Artifacts | Status |
|--------|----------------|-------------------|--------|
| MICR-01 | `enable justIntonation;` `play(C4 E4)` produces 5:4 ratio | `TuningRatioFacts.cs` (14 Facts) + `PitchConversionTuningFacts.cs` (5 Facts) + `tests/test_tuning_ji.flow` + `TuningTables.cs` (14 ratio tables) + `PitchConversion.cs` (2-arg overload + Pitfall 6 short-circuit) | ✅ Shipped f6b00ba |
| MICR-02 | `transpose(seq, 5)` produces same MIDI numbers under every tuning | `TransformInvarianceFacts.cs` (5 Facts) + `tests/test_tuning_transpose_invariant.flow` + `TransformFunctions.cs` (body BIT-IDENTICAL) | ✅ Shipped 8190fb2 |
| MICR-03 | Unknown tuning raises clear error pointing at v1.4 Scala | `UnknownTuningPragmaFacts.cs` (4 Facts) + `PragmaScanner.cs` (D-14 extension with single-source-of-truth `ScalaLoaderDeferralPointer` const) | ✅ Shipped 47d7718 |

REQUIREMENTS.md lines 73–75 + 148–150 confirm all three rows marked `[x]` / `Shipped {hash}`.

---

## Cross-cutting Truths Verified by Verifier

- ✅ Pattern A locked over Pattern B at every level — `grep -c "MusicalContext\.Current" flow-lang/ -r` returns 0 across all source files (Pattern B static-accessor never introduced).
- ✅ Pitfall 6 byte-identical short-circuit verified at 3 levels — leaf overload (`PitchConversion.cs:66` short-circuit), render pipeline (`NoPragma_StillBitIdentical_AfterPattern_A_Threading`), end-to-end (`ByteIdenticalTutorialTests` + `ByteIdenticalShowcaseTests` + `EuclideanByteIdenticalTests` all GREEN).
- ✅ ScaleDatabase canonical entry — `TryParseKeyWithMode` is the canonical entry from Wave 2 (single source of truth, no inline write-then-delete helper).
- ✅ TransformFunctions body bit-identical per MICR-02 / WARNING-1 — `grep -c "transpose would put" TransformFunctions.cs == 1` (existing Console.Error.WriteLine warning preserved verbatim; only XML `<remarks>` D-12 caveat added).
- ✅ No new NuGet packages added; DryWetMidi 8.0.3 stays as the only external dep.
- ✅ Tutorial/showcase don't use tuning pragmas — `git grep -wnE 'enable\s+(justIntonation|pythagorean|equalTemperament)' examples/*.flow` returns empty (verified by verifier on closure commit). Combined with Pitfall 6 short-circuit, byte-identical determinism contract for tutorial.flow + showcase.flow holds STRUCTURALLY.

---

## Anti-Pattern Scan (verifier-run)

| File | Pattern | Severity | Disposition |
|------|---------|----------|-------------|
| `TransformFunctions.cs` | TODO/FIXME comments | n/a | None found in modified scope |
| `Tuning/*.cs` | Empty implementations / placeholders | n/a | None — all 6 files contain substantive math (ratio tables + helpers); ChromaticRatioTable.Build throws on missing natural |
| `RenderingDiagnostics.cs` | Stub return statements | n/a | None — HashSet-backed dedup with thread-safety lock; `WarnOnce` writes to `Console.Error` |
| All Phase 23 facts | `[Fact(Skip=...)]` skip markers | n/a | None — verified all 91 facts run unconditionally (no skips) |

No blockers, no warnings, no info items. The phase ships clean.

---

## Behavioral Spot-Checks (verifier-run)

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| JI 5:4 ratio at render boundary | `dotnet test --filter "FullyQualifiedName~PitchConversionEndToEnd_JI_CtoE_FrequencyRatio_Is5to4"` | Passed at precision 10 | ✅ |
| Transform MIDI-pitch invariance under JI | `dotnet test --filter "FullyQualifiedName~TransformInvarianceFacts"` | 5/5 Passed | ✅ |
| Unknown tuning emits Scala pointer | `dotnet test --filter "FullyQualifiedName~UnknownTuning_ErrorIncludesScalaPointer"` | Passed | ✅ |
| ByteIdentical default-tuning regression | `dotnet test --filter "FullyQualifiedName~ByteIdenticalDefaultTuning"` | 2/2 Passed | ✅ |
| `enable justIntonation;` smoke renders correctly | `dotnet run --project flow-interpreter tests/test_tuning_ji.flow` | exit 0, `: PASSED` | ✅ |

---

## Deferred-Items Handoff

`.planning/phases/14-composer-dx-part-1/deferred-items.md` lines 226+ contain the Phase 23 closure cross-reference section listing all v1.4-deferred items:
- Full Scala (`.scl`) tuning loader
- Faithful microtonal MIDI export (per-channel pitch-bend)
- Spelling-preserving transforms (`transposePreserveSpelling`)
- Block-scope `tuning { }` syntax
- Configurable A4 reference frequency
- Mode-aware tuning tables for harmonic/melodic minor/blues
- Pre-resolution `enharmonic()` LSP squiggle warning
- REPL meta-command `:tuning ji` (rejected in favor of D-07)

Verified: handoff lines present in deferred-items.md, cross-referencing Phase 23 audit trail.

---

## Manual-Only Verifications (deferred — non-blocking)

| Behavior | Why Manual | Status |
|----------|------------|--------|
| Audible JI vs 12-TET difference on `play(C4 E4)` (UX subjective) | Audio listening is irreducibly subjective; xUnit pins ratio numerically | DEFERRED to v1.3 milestone HUMAN-UAT roll-up |
| REPL persisted-tuning behavior across lines (D-07) | Interactive UX; xUnit can simulate but human verifies non-confusing experience | DEFERRED to v1.3 milestone HUMAN-UAT roll-up |

These do **not** block phase closure — they are tracked alongside v1.2-era Phase 17 HUMAN-UAT items.

---

## Final Acceptance — Phase 23 Closes

- [x] All 4 ROADMAP success criteria verified by GREEN xUnit Facts at the canonical render boundary AND GREEN `.flow` smokes
- [x] All 14 locked decisions D-01..D-14 verified by direct codebase inspection
- [x] All 3 REQ-IDs (MICR-01, MICR-02, MICR-03) marked `Shipped {hash}` in REQUIREMENTS.md
- [x] ROADMAP.md Phase 23 row marked complete (Shipped 2026-05-03)
- [x] STATE.md milestone progress 5/10 → 6/10 phases for v1.3
- [x] Full xUnit suite 608/608 GREEN, 0 failures, 0 skips (`dotnet test flow-sharp.sln`)
- [x] Phase 18 byte-identical regression contract preserved: 8/8 ByteIdentical Facts GREEN (tutorial WAV+MIDI, showcase WAV+MIDI, euclidean WAV+MIDI, ByteIdenticalDefaultTuning ×2)
- [x] All 5 tuning `.flow` smoke scripts exit 0 with `: PASSED` sentinel
- [x] Build is clean: 0 errors, 0 warnings (`dotnet build flow-sharp.sln`)
- [x] Pattern A locked structurally — Pattern B static accessor never introduced (`grep -c "MusicalContext\.Current" flow-lang/ == 0`)
- [x] Tutorial.flow / showcase.flow don't reference tuning pragmas — byte-identical contract holds structurally + via Pitfall 6 short-circuit
- [x] Anti-pattern scan clean — no TODO/FIXME/stub/skip markers in modified scope
- [x] Deferred-items handoff present in `14-composer-dx-part-1/deferred-items.md` Phase 23 closure section

---

## VERIFICATION PASSED

Phase 23 (Microtonal Tuning, Wedge) ships clean. v1.3 milestone advances **5/10 → 6/10 phases complete (60%)**. Phase 24 (Scale Linting, LINT-01..03) is the next ROADMAP target — already unblocked by Phase 21 pragma infrastructure + Phase 23 `ScaleDatabase.TryParseKeyWithMode` canonical entry + `RenderingDiagnostics.WarnOnce` reusable diagnostic channel.

---

*Phase: 23-microtonal-tuning-wedge*
*Verified: 2026-05-03 (goal-backward, codebase-evidence verifier)*
*Verifier: Claude (gsd-verifier)*
*Supersedes: executor's draft 23-VERIFICATION.md (no scope reduction; canonical rewrite)*
