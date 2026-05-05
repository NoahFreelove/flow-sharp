---
phase: 23
slug: microtonal-tuning-wedge
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-05-03
---

# Phase 23 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (`flow-lang.Tests`) + `.flow` script integration loop |
| **Config file** | `flow-lang.Tests/flow-lang.Tests.csproj` (xUnit); `tests/test_*.flow` (script loop) |
| **Quick run command** | `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase23"` |
| **Full suite command** | `dotnet test && for t in tests/test_tuning_*.flow; do dotnet run --project flow-interpreter "$t" || exit 1; done` |
| **Estimated runtime** | ~30–60 seconds (xUnit) + ~10–20s per `.flow` script |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase23"`
- **After every plan wave:** Run full suite (`dotnet test` + tuning `.flow` scripts + `ByteIdenticalTutorialTests` + `ByteIdenticalShowcaseTests`)
- **Before `/gsd-verify-work`:** Full suite must be green AND byte-identical regression gate green
- **Max feedback latency:** 60 seconds (per-task quick run)

---

## Per-Task Verification Map

> Filled by the planner during plan generation. Each task in every PLAN.md must map to a row here.

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 23-XX-YY | XX | W | MICR-XX | — | {behavior} | unit/integration/regression | `{command}` | ✅ / ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

**Required test surfaces (planner MUST cover):**

| MICR | Test Surface | File(s) |
|------|--------------|---------|
| MICR-01 | xUnit ratio Facts (5:4 JI third, Pythagorean chain-of-fifths, equalTemperament default) | `flow-lang.Tests/Unit/Phase23/TuningRatioFacts.cs` |
| MICR-01 | `.flow` smoke tests for each tuning | `tests/test_tuning_ji.flow`, `tests/test_tuning_pythagorean.flow`, `tests/test_tuning_equal.flow` |
| MICR-02 | xUnit Facts: `transpose(seq, 5)` MIDI invariance under JI / Pythagorean / 12-TET | `flow-lang.Tests/Unit/Phase23/TransformInvarianceFacts.cs` |
| MICR-02 | `.flow` test asserting MIDI numbers identical, frequencies different | `tests/test_tuning_transpose_invariant.flow` |
| MICR-03 | xUnit Fact: unknown tuning emits Phase 21 D-12 error path with Scala v1.4 pointer line | `flow-lang.Tests/Unit/Phase23/UnknownTuningPragmaFacts.cs` |
| D-04 | xUnit Facts: `ParseKeyName` handles dorian/phrygian/lydian/mixolydian/locrian | `flow-lang.Tests/Unit/Phase23/ChurchModeParseFacts.cs` |
| D-08 | Byte-identical regression: explicit `enable equalTemperament;` produces same audio output as no-pragma | `flow-lang.Tests/Integration/ByteIdenticalEqualTemperamentExplicitTests.cs` |
| D-08 | Byte-identical regression unchanged: `tutorial.flow` + `showcase.flow` 19/19 GREEN | `flow-lang.Tests/Integration/ByteIdenticalTutorialTests.cs`, `ByteIdenticalShowcaseTests.cs` |
| D-11 | xUnit Fact: `enharmonic()` emits one-shot stderr warning under non-12-TET, NOT under 12-TET | `flow-lang.Tests/Unit/Phase23/EnharmonicWarningFacts.cs` |
| D-13 | xUnit Fact: `writeMidi` emits one-shot stderr warning under non-12-TET, NOT under 12-TET | `flow-lang.Tests/Unit/Phase23/WriteMidiWarningFacts.cs` |
| D-09 | xUnit Fact: `Eb4` and `D#4` produce different rendered Hz under JI; identical under 12-TET | `flow-lang.Tests/Unit/Phase23/SpellingAwareTuningFacts.cs` |
| D-10 | xUnit Fact: cent offsets compose additively over JI ratio | `flow-lang.Tests/Unit/Phase23/CentOffsetAdditivityFacts.cs` |
| Determinism | `tests/test_tuning_determinism.flow` pins JI/Pythagorean output per spec | `tests/test_tuning_determinism.flow` |

---

## Wave 0 Requirements

- [ ] `flow-lang.Tests/Unit/Phase23/` directory created
- [ ] `flow-lang.Tests/Unit/Phase23/TuningRatioFacts.cs` — RED stubs for MICR-01 ratio assertions (5-limit JI table, Pythagorean chain, equalTemperament identity)
- [ ] `flow-lang.Tests/Unit/Phase23/TransformInvarianceFacts.cs` — RED stubs for MICR-02 MIDI invariance
- [ ] `flow-lang.Tests/Unit/Phase23/UnknownTuningPragmaFacts.cs` — RED stub for MICR-03 error-message assertion
- [ ] `flow-lang.Tests/Unit/Phase23/ChurchModeParseFacts.cs` — RED stubs for D-04 mode-suffix recognition
- [ ] `flow-lang.Tests/Unit/Phase23/EnharmonicWarningFacts.cs` — RED stubs for D-11 one-shot warning
- [ ] `flow-lang.Tests/Unit/Phase23/WriteMidiWarningFacts.cs` — RED stubs for D-13 one-shot warning
- [ ] `flow-lang.Tests/Unit/Phase23/SpellingAwareTuningFacts.cs` — RED stubs for D-09 spelling-divergent rendering
- [ ] `flow-lang.Tests/Unit/Phase23/CentOffsetAdditivityFacts.cs` — RED stubs for D-10 cent additivity
- [ ] `tests/test_tuning_ji.flow`, `tests/test_tuning_pythagorean.flow`, `tests/test_tuning_equal.flow`, `tests/test_tuning_transpose_invariant.flow`, `tests/test_tuning_determinism.flow` — RED smoke scripts asserting expected output

*Existing infrastructure (xUnit + `.flow` script loop + byte-identical regression suites) covers everything else; no framework install required.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Audible JI vs 12-TET difference on `play(C4 E4)` | MICR-01 (UX) | Audio listening is irreducibly subjective; xUnit pins the ratio numerically | Run `tests/test_tuning_ji.flow` with audio enabled; compare to `tests/test_tuning_equal.flow`. JI third should sound noticeably "purer" / less beating. |
| REPL persisted-tuning behavior across lines (D-07) | D-07 | REPL is interactive; xUnit can simulate but human verifies the UX is non-confusing | Run REPL, type `enable justIntonation;`, then `play(C4 E4)` on a separate line, then `play(D4 F#4)` on a third line. All three should render under JI without re-declaring the pragma. |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references (Phase23 xUnit Facts directory + RED .flow scripts)
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
