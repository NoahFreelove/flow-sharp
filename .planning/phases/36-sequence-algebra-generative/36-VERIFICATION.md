---
phase: 36-sequence-algebra-generative
verified: 2026-05-22T20:40:00Z
status: passed
score: 9/9 requirements verified
overrides_applied: 0
re_verification:
  previous_status: null
  previous_score: null
  gaps_closed: []
  gaps_remaining: []
  regressions: []
---

# Phase 36: Sequence Algebra & Generative Verification Report

**Phase Goal:** Composer can write Tidal-style pattern algebra over `Sequence` values (13 combinators that compose via direct calls and `->`), generate musical material from Markov chains / L-systems / cellular automata / Lorenz attractors as first-class stdlib primitives, parameterize sections with positional + named args + Phase 35 pattern signatures, and improvise chord-aware Markov solos over a progression — all with deterministic seeding routed through the new `Runtime/PrngRegistry`.

**Verified:** 2026-05-22
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths (9 must-haves derived from REQUIREMENTS.md Phase 36 REQ-IDs + ROADMAP.md Success Criteria)

| # | Must-have / Truth | Status | Evidence |
|---|---|---|---|
| 1 | **PAT-01:** 13 Tidal-style combinators on `Sequence` ship in `@patterns` stdlib (`every`, `fast`, `slow`, `chunk`, `phase`, `rev`, `iter`, `palindrome`, `jux`, `superimpose`, `sometimes`, `degrade`, `sparseSeq`). All compose via direct calls; lambda-required transform-arg style per D-36-03; cycle unit is bars per D-36-04. | ✓ VERIFIED | `flow-lang/StandardLibrary/Patterns/PatternFunctions.cs` (895 lines, 14 registrations including default-prob `sometimes` overload); `flow-lang/patterns.flow` forward decls; `PatternEveryTests` 13/13 GREEN; `tests/test_patterns_every.flow` 5/5 PASS; `tests/test_patterns_chain.flow` 13/13 PASS (Plan 36-05 commits `a0f9882` + `4ddbf86`). |
| 2 | **PAT-02:** Combinator semantics typed on Flow's `Sequence`; charitable interpretation everywhere — zero-length seq + degenerate factor (e.g. `(fast seq 0)`) returns input + stderr advisory; never throws. | ✓ VERIFIED | `PatternChalkyEdgeCasesTests` 8/8 GREEN — fast/slow zero factor, chunk/iter negative N, phase NaN, sometimes/sparseSeq out-of-range prob clamp, empty seq passthrough all return input + `RenderingDiagnostics.WarnOnce` (Plan 36-05 commit `a0f9882`). |
| 3 | **GEN-01:** Markov primitive ships in BOTH shapes — one-shot `(markov corpus order length seed)` + train/generate split `(markovTrain corpus order) → MarkovModel + (markovGenerate model length seed)`. First-class `MarkovModel` reference-identity value type per D-36-06. Order clamp [1, 3] with charitable advisory. | ✓ VERIFIED | `flow-lang/Runtime/MarkovModelData.cs` (146 lines, plain class per Pitfall 6); `flow-lang/TypeSystem/SpecialTypes/MarkovModelType.cs` (specificity 148); `flow-lang/StandardLibrary/Generative/MarkovFunctions.cs` (455 lines, 6 registered overloads incl. features=Symbol/Tuple named-arg dispatch); `MarkovModelTests` 13/13 GREEN; `tests/test_markov_oneshot.flow` + `test_markov_train_generate.flow` PASS (Plan 36-06 commits `3628c64` + `89bd359` + `2a9067a`). |
| 4 | **GEN-02:** L-system primitive — `(lsystem axiom rules iterations)` one-shot + `(lsystemModel + lsystemGenerate)` split. `LsystemModel` ref-identity value type. Symbol alphabet (D-36-08). T-36-17 DoS guard via 20-iteration cap. | ✓ VERIFIED | `flow-lang/Runtime/LsystemModelData.cs` (129 lines); `LsystemModelType` (specificity 149); `flow-lang/StandardLibrary/Generative/LsystemFunctions.cs` (340 lines, 5 builtins incl. `lsystemToSequence` composer-mapper); `LsystemModelTests` 12/12 GREEN; `LsystemDeterminismTests` 2/2 GREEN; `tests/test_lsystem_oneshot.flow` 3/3 + `test_lsystem_train_generate.flow` 5/5 PASS (Plan 36-07 commits `28091f1` + `e4b93ba` + `3bac210`). |
| 5 | **GEN-03:** Cellular automata — 1D `(cellular rule width steps seed)` (Wolfram canonical patterns) + escape-hatch `(cellularSeeded ... initialPattern)` + 2D `(life width height steps seed)` Conway. T-36-19 DoS guard via 1024 per-dimension cap. | ✓ VERIFIED | `flow-lang/StandardLibrary/Generative/CellularFunctions.cs` (325 lines, 3 builtins); `CellularTests` 10/10 GREEN (Rule 30 + Rule 90 canonical Wolfram patterns pinned via hand-computed boolean rows); `CellularDeterminismTests` 3/3 GREEN; `tests/test_cellular_rule30.flow` 3/3 + `test_cellular_life.flow` 2/2 PASS (Plan 36-08 commits `292585c` + `c1c3a32` + `8478f11`). |
| 6 | **GEN-04:** Chaos maps — `(lorenz sigma rho beta length seed)` returns `Array[Double]` (x-axis); `(logistic r length seed)` returns `Array[Double]`. Bridge via `(quantizeToScale series scale)` overloads (String + Array[Note]). | ✓ VERIFIED | `flow-lang/StandardLibrary/Generative/ChaosFunctions.cs` (511 lines, 4 registered builtins); `ChaosTests` 10/10 GREEN (Lorenz canonical butterfly fallback + logistic recurrence + quantize round-trip); `ChaosDeterminismTests` 4/4 GREEN; `tests/test_lorenz_quantize.flow` 3/3 + `test_logistic.flow` 4/4 PASS. D-36-09 cross-platform FP divergence documented in xmldoc + module header + test headers (Plan 36-09 commits `f96b5b2` + `061f2ab` + `f77e66a`). |
| 7 | **GEN-05:** Determinism contract — all GEN-* + stochastic PAT-* primitives route PRNG through `Runtime/PrngRegistry` keyed by `(SourceLocation, generator-name)`. Unseeded calls reseed at `renderSong`/`writeWav` boundary preserving two-run cmp-clean. Lorenz cross-platform FP divergence documented; same-platform two-run cmp-clean preserved. | ✓ VERIFIED | `flow-lang/Runtime/PrngRegistry.cs` ships with FNV-1a deterministic seed (Plan 36-01); `PrngRegistryNewRandomGateTests` source-grep CI gate enforces zero unsanctioned `new Random(` across `Patterns/Generative/Improv/` directories (PRNG-SANCTIONED marker convention for documented explicit-seed exceptions). Two-run cmp-clean verified across 9 stochastic test/example files: patterns_chain `ca90fcad...`, markov_oneshot, lsystem_oneshot `509d3994...`, cellular_rule30, cellular_life, lorenz_quantize, logistic, jam_jazz, **markov_jazz `f46c1ca9...`**, **tidal_combinators `6d301369...`**, **parameterized `7d6d99c4...`** (Plan 36-12 closure verification, Linux x64). |
| 8 | **SECT-01:** Parameterized sections — `section verse(Note root, Int repeats)` declared, called as `[verse(C4, 2) chorus]` (D-36-13). Section args bind in synthetic stack frame on call, closure over outer musical context preserved (Pitfall 7 dynamic scope). Full Phase 35 pattern syntax in signatures (D-36-17): typed bindings + tuple destructure + music-aware extractors (chord literal). Section overloading via OverloadResolver (D-36-18). Repeat operator `*N` (D-36-14). Defaults (D-36-15). Rust-style multi-line diagnostics (D-36-16). Legacy zero-arg form unchanged. | ✓ VERIFIED | `flow-lang/Ast/Elements/SongElement.cs` + `SectionCallElement.cs`; `flow-lang/Ast/Statements/SectionDeclaration.cs` (Parameters + DefaultValues defaulted-positional); `flow-lang/Interpreter/SectionOverloadDispatch.cs`; `SectionParamsParserTests` + `SectionOverloadTests` + `SectionDefaultsTests` + `SectionDiagnosticsTests` 24/24 GREEN; `tests/test_section_params.flow` + `test_section_overload.flow` + `test_section_pattern_destructure.flow` + `test_section_repeat.flow` + `test_section_defaults.flow` all PASS (Plan 36-10 commits `e935991` + `d0ddfb9` + `ac07132` + `c02aa12`). |
| 9 | **IMPROV-01:** Chord-aware Markov improvisation — `(jam over=chords style=#jazz length=8 seed=N)`. Style symbol resolves to a locked Flow-file rule pack at `flow-lang/improv/styles/*.flow` (shipped jazz / blues / classical) with XDG override discovery at `~/.config/flow/styles/*.flow` (Pitfall 8 last-write-wins + one-shot advisory). Output is `Sequence`. Deterministic when `seed` provided. | ✓ VERIFIED | `flow-lang/StandardLibrary/Improv/StyleRegistry.cs` (272 lines) + `JamFunctions.cs` (~650 lines); `flow-lang/improv.flow` + `flow-lang/improv/styles/{jazz,blues,classical}.flow` shipped packs; `flow-lang/improv/styles/README.md` Dict-shape contract; `StyleRegistryTests` 6/6 + `JamFunctionsTests` 10/10 + `JamDeterminismTests` 2/2 GREEN; `tests/test_jam_jazz.flow` 2/2 + `test_jam_key_override.flow` + `test_jam_styles.flow` PASS (Plan 36-11 commits `4e8957d` + `1291b87` + `f9dc75f`). |

**Score:** 9/9 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|---|---|---|---|
| `flow-lang/Runtime/PrngRegistry.cs` | FNV-1a deterministic seed + (SourceLocation, name) keying | ✓ VERIFIED | Plan 36-01 |
| `flow-lang/StandardLibrary/Patterns/PatternFunctions.cs` | 13 combinator registrations | ✓ VERIFIED | 895 lines, Plan 36-05 |
| `flow-lang/patterns.flow` | @patterns stdlib forward decls | ✓ VERIFIED | 29 lines |
| `flow-lang/Runtime/MarkovModelData.cs` | MarkovModel ref-identity class | ✓ VERIFIED | 146 lines, Pitfall 6 |
| `flow-lang/TypeSystem/SpecialTypes/MarkovModelType.cs` | Sealed singleton specificity 148 | ✓ VERIFIED | Plan 36-06 |
| `flow-lang/StandardLibrary/Generative/MarkovFunctions.cs` | 6-overload Markov surface | ✓ VERIFIED | 455 lines |
| `flow-lang/Runtime/LsystemModelData.cs` | LsystemModel ref-identity class | ✓ VERIFIED | 129 lines |
| `flow-lang/TypeSystem/SpecialTypes/LsystemModelType.cs` | Sealed singleton specificity 149 | ✓ VERIFIED | Plan 36-07 |
| `flow-lang/StandardLibrary/Generative/LsystemFunctions.cs` | 5-builtin L-system surface | ✓ VERIFIED | 340 lines |
| `flow-lang/StandardLibrary/Generative/CellularFunctions.cs` | 3-builtin cellular surface | ✓ VERIFIED | 325 lines |
| `flow-lang/StandardLibrary/Generative/ChaosFunctions.cs` | 4-builtin chaos surface | ✓ VERIFIED | 511 lines |
| `flow-lang/generative.flow` | @generative stdlib forward decls | ✓ VERIFIED | All 5 GEN-* surfaces |
| `flow-lang/Ast/Elements/SongElement.cs` + `SectionCallElement.cs` | New AST family | ✓ VERIFIED | Plan 36-10 |
| `flow-lang/Interpreter/SectionOverloadDispatch.cs` | Section overload + default-value dispatch | ✓ VERIFIED | Plan 36-10 |
| `flow-lang/StandardLibrary/Improv/JamFunctions.cs` | jam chord-aware Markov | ✓ VERIFIED | ~650 lines, Plan 36-11 |
| `flow-lang/StandardLibrary/Improv/StyleRegistry.cs` | XDG style-pack discovery | ✓ VERIFIED | 272 lines |
| `flow-lang/improv.flow` | @improv stdlib forward decls | ✓ VERIFIED | jam + registerStyle + listStyles |
| `flow-lang/improv/styles/{jazz,blues,classical}.flow` | 3 baseline rule packs | ✓ VERIFIED | Composer-editable Flow files |
| `flow-lang/improv/styles/README.md` | Dict-shape composer contract | ✓ VERIFIED | 179 lines |
| `scripts/test_two_run_determinism.sh` | Two-run cmp-clean harness | ✓ VERIFIED | Plan 36-01 |
| `examples/generative/markov_jazz.flow` | Composer tutorial — Markov + jam + combinator chain | ✓ VERIFIED | Plan 36-12 |
| `examples/generative/tidal_combinators.flow` | Composer tutorial — all 13 combinators | ✓ VERIFIED | Plan 36-12 |
| `examples/sections/parameterized.flow` | Composer tutorial — overloading + defaults + *N | ✓ VERIFIED | Plan 36-12 |
| `tests/test_markov_jazz_example.flow` | markov_jazz regression (5/5 PASS) | ✓ VERIFIED | Plan 36-12 |
| `tests/test_tidal_combinators_example.flow` | tidal regression (11/11 PASS) | ✓ VERIFIED | Plan 36-12 |
| `tests/test_parameterized_example.flow` | parameterized regression (7/7 PASS) | ✓ VERIFIED | Plan 36-12 |

### Per-Requirement Verification Table

| Requirement | Plan(s) | Verification | Status | Commits |
|---|---|---|---|---|
| **PAT-01** | 36-05 | `dotnet test --filter "Phase36.Pattern"` + `flow test tests/test_patterns_every.flow` + `flow test tests/test_patterns_chain.flow` | ✓ VERIFIED | `a0f9882`, `4ddbf86`, `c823c83` |
| **PAT-02** | 36-05 | `dotnet test --filter "Phase36.PatternChalkyEdgeCases"` (8/8 GREEN) + `flow test tests/test_patterns_edge_cases.flow` | ✓ VERIFIED | `a0f9882`, `c823c83` |
| **GEN-01** | 36-06 | `dotnet test --filter "Phase36.MarkovModel"` (13/13) + `flow test tests/test_markov_oneshot.flow` + `tests/test_markov_train_generate.flow` + `bash scripts/test_two_run_determinism.sh tests/test_markov_oneshot.flow` | ✓ VERIFIED | `3628c64`, `89bd359`, `2a9067a` |
| **GEN-02** | 36-07 | `dotnet test --filter "Phase36.Lsystem"` (14/14) + `flow test tests/test_lsystem_oneshot.flow` + `tests/test_lsystem_train_generate.flow` + two-run cmp-clean (SHA-256 `509d3994...`) | ✓ VERIFIED | `28091f1`, `e4b93ba`, `3bac210` |
| **GEN-03** | 36-08 | `dotnet test --filter "Phase36.Cellular"` (13/13) + `flow test tests/test_cellular_rule30.flow` + `tests/test_cellular_life.flow` + two-run cmp-clean | ✓ VERIFIED | `6ea3f7f`, `292585c`, `c1c3a32`, `8478f11` |
| **GEN-04** | 36-09 | `dotnet test --filter "Phase36.Chaos"` (14/14) + `flow test tests/test_lorenz_quantize.flow` + `tests/test_logistic.flow` + same-platform two-run cmp-clean (D-36-09 caveat docs) | ✓ VERIFIED | `57b0633`, `f96b5b2`, `061f2ab`, `f77e66a` |
| **GEN-05** | 36-01 + 36-05/06/07/08/09/11 | `PrngRegistryNewRandomGateTests` source-grep gate + `bash scripts/test_two_run_determinism.sh` on every stochastic test/example file (11 files total inc. Plan 36-12 examples) — all PASS with byte-identical SHA-256s | ✓ VERIFIED | `164483d`, `5a234f1`, `bca3dec` (foundation); reinforced across all stochastic plans |
| **SECT-01** | 36-10 | `dotnet test --filter "Phase36.Section"` (24/24) + `flow test tests/test_section_params.flow` + 4 sibling test files + `bash scripts/test_two_run_determinism.sh tests/test_section_overload.flow` | ✓ VERIFIED | `e935991`, `d0ddfb9`, `ac07132`, `c02aa12` |
| **IMPROV-01** | 36-11 | `dotnet test --filter "Phase36.Jam"` + `Phase36.StyleRegistry` (18/18 total) + `flow test tests/test_jam_jazz.flow` + `test_jam_key_override.flow` + `test_jam_styles.flow` + two-run cmp-clean on jam_jazz | ✓ VERIFIED | `4e8957d`, `1291b87`, `f9dc75f` |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|---|---|---|---|
| Build clean | `dotnet build flow-lang/flow-lang.csproj` | 0 Errors, 5 pre-existing Warnings (unrelated) | ✓ PASS |
| Phase 36 xUnit GREEN | `dotnet test --filter "FullyQualifiedName~Phase36"` | 173/173 passed in 462ms | ✓ PASS |
| All Phase 36 .flow tests GREEN | `for f in tests/test_{patterns,markov,lsystem,cellular,lorenz,logistic,jam,section,tidal,parameterized}*.flow; do flow test "$f"; done` | 24/24 files PASS, 100+ total tests | ✓ PASS |
| Two-run cmp-clean (markov_jazz example) | `bash scripts/test_two_run_determinism.sh examples/generative/markov_jazz.flow` | SHA-256 `f46c1ca9360661c502a45f4e05495facc657130a0306bf9ef91b984512b30a64` byte-identical across runs | ✓ PASS |
| Two-run cmp-clean (tidal_combinators example) | `bash scripts/test_two_run_determinism.sh examples/generative/tidal_combinators.flow` | SHA-256 `6d301369841d3ecdaba332c6517d6d670b4a27d40b846ee3957e525f127d1a2d` byte-identical | ✓ PASS |
| Two-run cmp-clean (parameterized example) | `bash scripts/test_two_run_determinism.sh examples/sections/parameterized.flow` | SHA-256 `7d6d99c46f0172071e739f5628ae1dd3e9bbe626be2c760526d5b1df2a422ba6` byte-identical | ✓ PASS |
| markov_jazz example regression | `flow test tests/test_markov_jazz_example.flow` | 5/5 PASS | ✓ PASS |
| tidal_combinators example regression | `flow test tests/test_tidal_combinators_example.flow` | 11/11 PASS | ✓ PASS |
| parameterized example regression | `flow test tests/test_parameterized_example.flow` | 7/7 PASS | ✓ PASS |
| Phase 35 regression (no new breakage) | `dotnet test --filter "FullyQualifiedName~Phase35"` | 79/80 (1 pre-existing test-ordering limitation per Phase 35 VERIFICATION) | ✓ PASS (no new regression) |

### Locked Decisions Verification

| Decision | Source | Status | Evidence |
|---|---|---|---|
| D-v1.5-06: PrngRegistry keyed by (SourceLocation, name); render-boundary reseed | REQUIREMENTS.md | ✓ VERIFIED | Plan 36-01 `PrngRegistry.cs` + source-grep gate enforces no unsanctioned `new Random(` |
| D-36-01: 13 combinators set (every/fast/slow/chunk/phase/rev/iter/palindrome/jux/superimpose + sometimes/degrade/sparseSeq) | 36-CONTEXT.md | ✓ VERIFIED | All 14 registrations (incl. default-prob sometimes overload) in `PatternFunctions.cs` |
| D-36-03: Lambda-required transform-arg style | 36-CONTEXT.md | ✓ VERIFIED | All transform-arg combinators declare Function: cb param; tests + examples use lambda form |
| D-36-04: Cycle unit is bars for every/chunk/phase | 36-CONTEXT.md | ✓ VERIFIED | `PatternEveryTests.EveryAppliesFnToBarsAtCycleBoundary` + `ChunkAppliesOneChunkPerCycle` + `PhaseRotatesByBarFraction` pin |
| D-36-06: Markov + L-system both shapes (one-shot + train/generate split) | 36-CONTEXT.md | ✓ VERIFIED | 6 markov overloads + 5 lsystem builtins ship both shapes |
| D-36-09: Cross-platform FP divergence documented for chaos primitives | 36-CONTEXT.md | ✓ VERIFIED | Documented in `ChaosFunctions.cs` xmldoc + `generative.flow` module header + `test_lorenz_quantize.flow` + `test_logistic.flow` headers |
| D-36-11: Universal named-argument syntax | 36-CONTEXT.md | ✓ VERIFIED | Plan 36-02 lexer + parser + OverloadResolver; Plans 36-03/04 backfill ~150 builtin signatures; `ParameterNamesCoverageTest` gates completeness |
| D-36-13..18: Parameterized sections (parens-call + *N + defaults + diagnostics + Phase 35 patterns + overload) | 36-CONTEXT.md | ✓ VERIFIED | Plan 36-10 ships all 6 D-* deliverables; `SectionParamsParserTests` + `SectionOverloadTests` + `SectionDefaultsTests` + `SectionDiagnosticsTests` pin |
| D-36-10: jam signature with key= override + style packs as Flow files at flow-lang/improv/styles/ | 36-CONTEXT.md | ✓ VERIFIED | Plan 36-11 ships 6 jam overloads + 3 baseline packs + XDG override discovery |
| D-36-12: Style packs are MUSICAL CONTENT — composer-editable Flow files; user packs override shipped via Pitfall 8 last-write-wins | 36-CONTEXT.md | ✓ VERIFIED | `flow-lang/improv/styles/{jazz,blues,classical}.flow` + `~/.config/flow/styles/*.flow` discovery; `StyleRegistryTests.UserPackOverridesShippedWithAdvisory` pins |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|---|---|---|---|---|
| Phase 35 `MatchExhaustivenessDefaultTests.WarnDedupedPerMatchSpan` | — | Pre-existing test-ordering limitation; passes in isolation, fails in full-suite run order | ℹ️ Info | Inherited Phase 35 baseline; NOT introduced by Phase 36 (verified against Phase 35-VERIFICATION.md line 145 anti-pattern table) |

No 🛑 BLOCKERS. No ⚠️ WARNINGS. The single Phase 35 failure is documented and matches the Phase 35 closure baseline.

### Gaps Summary

**No gaps found.** Phase 36 goal achieved: 13 Tidal-style combinators (`@patterns`), Markov + L-system + cellular + chaos generative primitives (`@generative`), parameterized sections with full Phase 35 pattern support + overloading + defaults + `*N` repeat, chord-aware Markov improvisation with composer-editable Flow-file style packs (`@improv`), universal named-argument syntax with ~150-builtin backfill, and the new `Runtime/PrngRegistry` foundation are all in place and behaviorally verified.

The Phase 36 surface area is composer-reachable through three tutorial chapters (`examples/generative/markov_jazz.flow`, `examples/generative/tidal_combinators.flow`, `examples/sections/parameterized.flow`) that render cleanly and pass `scripts/test_two_run_determinism.sh` two-run cmp-clean (byte-identical SHA-256 across consecutive renders).

The v1.5 milestone is unblocked for Phase 37 (Sound Design + Sampler Polish): the PrngRegistry contract is established and Phase 37's granular-jitter (DSP-01) + sampler round-robin (SAMP-01) routings will inherit it.

---

_Verified: 2026-05-22T20:40:00Z_
_Verifier: Claude (Plan 36-12 executor)_
