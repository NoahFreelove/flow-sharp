---
phase: 36
slug: sequence-algebra-generative
status: closed
nyquist_compliant: true
wave_0_complete: true
created: 2026-05-20
modified: 2026-05-22
closed: 2026-05-22
---

# Phase 36 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit.v3 3.2.2 (C#) + Phase 35 `(test ...)` framework (Flow) |
| **Config file** | `flow-lang.Tests/flow-lang.Tests.csproj` (xUnit); `flow-cli/Commands/TestCommand.cs` (`flow test`) |
| **Quick run command** | `dotnet test --filter "FullyQualifiedName~Phase36"` |
| **Full suite command** | `dotnet test` + `for f in tests/test_*.flow; do flow test "$f"; done` |
| **Estimated runtime** | ~120s (Phase36 quick), ~8min (full suite + flow tests) |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test --filter "Phase36"` (Phase 36 facts only — fast feedback)
- **After every plan wave:** Run `dotnet test` (full xUnit suite — verify no Phase 35 / earlier regressions)
- **Before `/gsd:verify-work`:** Full suite must be green
- **Phase gate:** Two-run cmp-clean integration on `examples/generative/markov_jazz.flow`, `examples/generative/tidal_combinators.flow`, `examples/sections/parameterized.flow` — SHA-256 byte-identical on consecutive renders of WAV+MIDI (Linux)
- **Max feedback latency:** 120s

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 36-01-01 | 01 | 1 | GEN-05 | T-36-30 | PrngRegistry source-location keying; deterministic across runs at same SHA | unit | `dotnet test --filter "FullyQualifiedName~Phase36.PrngRegistryTests"` | ❌ W0 | ⬜ pending |
| 36-01-02 | 01 | 1 | GEN-05 | T-36-30 | Snapshot/restore semantics + reseed boundary | unit + composer | `dotnet test --filter "FullyQualifiedName~Phase36.PrngRegistryTests"` + `flow test tests/test_prng_determinism.flow` | ❌ W0 | ⬜ pending |
| 36-02-01 | 02 | 2 | D-36-11 | T-36-07 | Lexer + parser for `(fn name=val ...)` named-arg syntax | unit | `dotnet test --filter "FullyQualifiedName~Phase36.NamedArgsParserTests"` | ❌ W0 | ⬜ pending |
| 36-02-02 | 02 | 2 | D-36-11 | T-36-08 | Resolver fold + positional backcompat preserved | unit + composer | `dotnet test --filter "FullyQualifiedName~Phase36.NamedArgBackcompatTests"` + `flow test tests/test_named_args.flow` | ❌ W0 | ⬜ pending |
| 36-02-03 | 02 | 2 | D-36-11 | T-36-04, T-36-05, T-36-06 | OverloadResolver named-arg dispatch + ExpressionEvaluator re-ordering + composer test | unit + composer | `dotnet test --filter "FullyQualifiedName~Phase36.NamedArgsResolverTests"` + `flow test tests/test_named_args.flow` | ❌ W0 | ⬜ pending |
| 36-03-01 | 03 | 3 | PAT-01 | T-36-07 | BuiltInFunctions.cs / Collections / Bars ParameterNames backfill | unit (grep) | `dotnet test --filter "FullyQualifiedName~Phase36.ParameterNamesCoverageTest"` | ❌ W0 | ⬜ pending |
| 36-03-02 | 03 | 3 | PAT-01 | T-36-08 | ParameterNamesCoverageTest scaffold with ALL [InlineData] rows populated (covers names Plan 36-04 backfills) | unit (grep) | `dotnet test --filter "FullyQualifiedName~Phase36.ParameterNamesCoverageTest"` | ❌ W0 | ⬜ pending |
| 36-04-01 | 04 | 3 | PAT-01 | T-36-09 | Audio/DSP/Tuning/SFZ/Vocalization/Visualization ParameterNames backfill | unit (grep) | `dotnet test --filter "FullyQualifiedName~Phase36.ParameterNamesCoverageTest"` | ✅ (test file shipped by 36-03) | ⬜ pending |
| 36-04-02 | 04 | 3 | PAT-01 | T-36-10 | Transforms/Composition/Harmony/TestFramework ParameterNames backfill | unit (grep) | `dotnet test --filter "FullyQualifiedName~Phase36.ParameterNamesCoverageTest"` | ✅ (test file shipped by 36-03) | ⬜ pending |
| 36-05-01 | 05 | 4 | PAT-01 | — | 10 deterministic combinators (every/fast/slow/chunk/phase/rev/iter/palindrome/jux/superimpose) | unit + composer | `dotnet test --filter "FullyQualifiedName~Phase36.PatternEveryTests"` + `flow test tests/test_patterns_every.flow` | ❌ W0 | ⬜ pending |
| 36-05-02 | 05 | 4 | PAT-01, PAT-02, GEN-05 | T-36-30 | Stochastic combinators (sometimes/degrade/sparseSeq) routed via PrngRegistry + charitable edge cases | unit + composer + integration | `dotnet test --filter "FullyQualifiedName~Phase36.PatternChalkyEdgeCasesTests"` + `bash scripts/test_two_run_determinism.sh tests/test_patterns_chain.flow` | ❌ W0 | ⬜ pending |
| 36-06-01 | 06 | 4 | GEN-01 | T-36-30 | Markov train/generate split + MarkovModel reference identity | unit + composer | `dotnet test --filter "FullyQualifiedName~Phase36.MarkovModelTests"` + `flow test tests/test_markov_train_generate.flow` | ❌ W0 | ⬜ pending |
| 36-06-02 | 06 | 4 | GEN-01 | T-36-30 | One-shot `(markov corpus 2 16 seed)` deterministic | composer + integration | `flow test tests/test_markov_oneshot.flow` + `bash scripts/test_two_run_determinism.sh tests/test_markov_oneshot.flow` | ❌ W0 | ⬜ pending |
| 36-07-01 | 07 | 5 | GEN-02 | T-36-30 | L-system with Symbol alphabet; one-shot + split shapes | composer | `flow test tests/test_lsystem_oneshot.flow` + `flow test tests/test_lsystem_train_generate.flow` | ❌ W0 | ⬜ pending |
| 36-07-02 | 07 | 5 | GEN-02, GEN-05 | T-36-17, T-36-18 | LsystemFunctions builtins + Lindenmayer algae canonical + LsystemDeterminismTests source-grep gate | unit + composer + integration | `dotnet test --filter "FullyQualifiedName~Phase36.Lsystem"` + `bash scripts/test_two_run_determinism.sh tests/test_lsystem_oneshot.flow` | ❌ W0 | ⬜ pending |
| 36-08-01 | 08 | 5 | GEN-03 | T-36-30 | 1D cellular `(cellular 30 16 32 seed)` rule 30 produces canonical chaos | composer | `flow test tests/test_cellular_rule30.flow` | ❌ W0 | ⬜ pending |
| 36-08-02 | 08 | 5 | GEN-03 | T-36-30 | 2D `(life ...)` deterministic | composer | `flow test tests/test_cellular_life.flow` | ❌ W0 | ⬜ pending |
| 36-09-01 | 09 | 5 | GEN-04 | T-36-30 | Lorenz returns Array[Double]; quantize via ScaleData | composer | `flow test tests/test_lorenz_quantize.flow` | ❌ W0 | ⬜ pending |
| 36-09-02 | 09 | 5 | GEN-04 | T-36-30 | Logistic deterministic at seed | composer | `flow test tests/test_logistic.flow` | ❌ W0 | ⬜ pending |
| 36-10-01 | 10 | 6 | SECT-01 | T-36-24, T-36-26 | Parameterized section AST + Parser + SectionCallElement | unit | `dotnet test --filter "FullyQualifiedName~Phase36.SectionParamsParserTests"` | ❌ W0 | ⬜ pending |
| 36-10-02 | 10 | 6 | SECT-01 | T-36-24, T-36-25 | OverloadResolver section dispatch + synthetic-frame dynamic scope (Pitfall 7) | unit | `dotnet test --filter "FullyQualifiedName~Phase36.Section"` | ❌ W0 | ⬜ pending |
| 36-10-03 | 10 | 6 | SECT-01 | T-36-24 | Rust-style diagnostics + `*N` repeat + composer-facing tests | unit + composer + integration | `dotnet test --filter "FullyQualifiedName~Phase36.SectionDiagnostics"` + `bash scripts/test_two_run_determinism.sh tests/test_section_overload.flow` | ❌ W0 | ⬜ pending |
| 36-11-01 | 11 | 6 | IMPROV-01 | T-36-27, T-36-28, T-36-V12 | StyleRegistry + 3 baseline rule packs + XDG override discovery | unit | `dotnet test --filter "FullyQualifiedName~Phase36.StyleRegistryTests"` | ❌ W0 | ⬜ pending |
| 36-11-02 | 11 | 6 | IMPROV-01, GEN-05 | T-36-29, T-36-30 | Chord-aware Markov jam + determinism gate + charitable style/key incompatibility | unit + composer + integration | `dotnet test --filter "FullyQualifiedName~Phase36.Jam"` + `bash scripts/test_two_run_determinism.sh tests/test_jam_jazz.flow` | ❌ W0 | ⬜ pending |
| 36-12-01 | 12 | 7 | All | All | Phase gate: full suite + 3-file two-run cmp-clean (examples/generative/markov_jazz.flow, examples/generative/tidal_combinators.flow, examples/sections/parameterized.flow) | integration | `dotnet test` + `bash scripts/test_two_run_determinism.sh examples/generative/markov_jazz.flow && bash scripts/test_two_run_determinism.sh examples/generative/tidal_combinators.flow && bash scripts/test_two_run_determinism.sh examples/sections/parameterized.flow` | ❌ W0 (example files new) | ⬜ pending |
| 36-12-02 | 12 | 7 | All | — | Validation closure: flip frontmatter `status: passed`, `nyquist_compliant: true`, `wave_0_complete: true` | manual flag | n/a (frontmatter edit at phase closure) | n/a | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

All Phase 36 test files are new. Framework install: NONE — xUnit.v3 + `flow test` CLI already exist; no new test infrastructure needed.

xUnit test files (`flow-lang.Tests/Phase36/`):
- [ ] `flow-lang.Tests/Phase36/PatternEveryTests.cs` — PAT-01 `every` cycle unit (bars)
- [ ] `flow-lang.Tests/Phase36/PatternChalkyEdgeCasesTests.cs` — PAT-02 charitable interpretation
- [ ] `flow-lang.Tests/Phase36/PatternDeterminismTests.cs` — GEN-05 PRNG-routing source-grep gate
- [ ] `flow-lang.Tests/Phase36/MarkovModelTests.cs` — GEN-01 model identity + train/generate split
- [ ] `flow-lang.Tests/Phase36/PrngRegistryTests.cs` — GEN-05 source-location keying + reseed boundary + snapshot/restore
- [ ] `flow-lang.Tests/Phase36/SectionParamsParserTests.cs` — SECT-01 parser facts
- [ ] `flow-lang.Tests/Phase36/SectionOverloadTests.cs` — SECT-01 / D-36-18 overload dispatch
- [ ] `flow-lang.Tests/Phase36/SectionDiagnosticsTests.cs` — D-36-16 Rust-style arity/type errors
- [ ] `flow-lang.Tests/Phase36/SectionDefaultsTests.cs` — D-36-15 default values + Pitfall 7 dynamic scope
- [ ] `flow-lang.Tests/Phase36/NamedArgsParserTests.cs` — D-36-11 lexer + parser
- [ ] `flow-lang.Tests/Phase36/NamedArgBackcompatTests.cs` — positional form preserved
- [ ] `flow-lang.Tests/Phase36/ParameterNamesCoverageTest.cs` — backfill completeness gate (full [InlineData] roster shipped by Plan 36-03 Task 2)
- [ ] `flow-lang.Tests/Phase36/StyleRegistryTests.cs` — IMPROV-01 XDG discovery + override semantics
- [ ] `flow-lang.Tests/Phase36/JamFunctionsTests.cs` — IMPROV-01 jam dispatch + chord-tone bias
- [ ] `flow-lang.Tests/Phase36/JamDeterminismTests.cs` — IMPROV-01 source-grep new-Random gate

Composer-facing `.flow` test files (`tests/`):
- [ ] `tests/test_patterns_every.flow` — composer-facing PAT-01
- [ ] `tests/test_patterns_chain.flow` — all 13 combinators + sparseSeq exercised
- [ ] `tests/test_patterns_edge_cases.flow` — PAT-02 charitable paths
- [ ] `tests/test_markov_oneshot.flow` — GEN-01 one-shot
- [ ] `tests/test_markov_train_generate.flow` — GEN-01 split
- [ ] `tests/test_lsystem_oneshot.flow` — GEN-02
- [ ] `tests/test_lsystem_train_generate.flow` — GEN-02 split (lsystemModel)
- [ ] `tests/test_cellular_rule30.flow` — GEN-03 1D
- [ ] `tests/test_cellular_life.flow` — GEN-03 2D
- [ ] `tests/test_lorenz_quantize.flow` — GEN-04 + quantizeToScale
- [ ] `tests/test_logistic.flow` — GEN-04 logistic
- [ ] `tests/test_section_params.flow` — SECT-01 basic
- [ ] `tests/test_section_overload.flow` — SECT-01 / D-36-18
- [ ] `tests/test_section_pattern_destructure.flow` — D-36-17
- [ ] `tests/test_section_repeat.flow` — D-36-14 `*N`
- [ ] `tests/test_section_defaults.flow` — D-36-15
- [ ] `tests/test_jam_jazz.flow` — IMPROV-01 baseline
- [ ] `tests/test_jam_key_override.flow` — IMPROV-01 key=
- [ ] `tests/test_jam_styles.flow` — IMPROV-01 all 3 packs
- [ ] `tests/test_named_args.flow` — D-36-11 surface
- [ ] `tests/test_prng_determinism.flow` — GEN-05

Phase-gate integration script:
- [ ] `scripts/test_two_run_determinism.sh` — renders a `.flow` file twice, SHA-256-cmp on WAV+MIDI outputs

Phase-gate example files (Plan 36-12 closure):
- [ ] `examples/generative/markov_jazz.flow`
- [ ] `examples/generative/tidal_combinators.flow`
- [ ] `examples/sections/parameterized.flow`

Framework install: NONE — xUnit.v3 + `flow test` CLI already exist.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|

*All phase behaviors have automated verification.*

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 120s
- [x] `nyquist_compliant: true` set in frontmatter (flipped by Plan 36-12 at closure)
- [x] `wave_0_complete: true` set in frontmatter (flipped by Plan 36-12 at closure)

**Approval:** signed-off 2026-05-22 (Plan 36-12 closure executor) — 9/9 requirements verified per 36-VERIFICATION.md; Phase 36 xUnit 173/173 GREEN; 24/24 composer .flow test files PASS; 3-file two-run cmp-clean PASS (markov_jazz `f46c1ca9...`, tidal_combinators `6d301369...`, parameterized `7d6d99c4...`).
