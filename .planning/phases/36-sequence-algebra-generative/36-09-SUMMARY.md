---
phase: 36-sequence-algebra-generative
plan: 09
subsystem: standard-library
tags: [chaos, lorenz, logistic, attractor, generative, deterministic, GEN-04, D-36-08, D-36-09, cross-platform-fp]

# Dependency graph
requires:
  - phase: 36-sequence-algebra-generative
    plan: 01
    provides: "PrngRegistryNewRandomGateTests source-grep gate (Plan 36-09 passes with 2 sanctioned hits via `// PRNG-SANCTIONED:` markers on lorenz + logistic explicit-seed sites)"
  - phase: 36-sequence-algebra-generative
    plan: 02
    provides: "FunctionSignature.ParameterNames defaulted-positional field (lorenz / logistic / quantizeToScale registrations use it)"
  - phase: 36-sequence-algebra-generative
    plan: 06
    provides: "Generative/ subdirectory + `// PRNG-SANCTIONED:` marker convention (Plan 36-09 reuses both — two sanctioned hits total)"
  - phase: 36-sequence-algebra-generative
    plan: 08
    provides: "ClampDimensionWithAdvisory shape (Plan 36-09's ClampLengthWithAdvisory mirrors it — single-dimension `length` clamp + WarnOnce at 100_000 DoS cap)"
provides:
  - "@generative stdlib extension — lorenz / logistic / quantizeToScale (×2 overloads) builtins (GEN-04)"
  - "ChaosFunctions.RegisterContextDependent wiring in FlowEngine.cs"
  - "D-36-09 cross-platform FP divergence documented in C# xmldoc + generative.flow module header + composer test file headers"
  - "T-36-21 DoS guard pattern (length cap with WarnOnce advisory) — single-dimension simplification of Plan 36-08's per-dimension clamp"
affects: [36-10, 36-11, 36-12]

# Tech tracking
tech-stack:
  added: []   # Hand-rolled C# per D-v1.5-06; forward-Euler integration is ~15 LOC; logistic recurrence is one line
  patterns:
    - "Forward-Euler integration over a 3-state ODE: dt=0.01, 100 warm-up iterations discarded (chaotic transient), x-axis trajectory captured. Initial conditions (1, 0, 0) + seed-derived perturbation within ±5e-4 so distinct seeds yield distinct trajectories without escaping the bounded attractor."
    - "Per-builtin explicit-seed Random construction via `new Random(seed)`, marked `// PRNG-SANCTIONED:` per the Plan 36-06 convention. No PrngRegistry routing because the REQ signature REQUIRES the seed arg — there is no unseeded path."
    - "Charitable param fallback for Lorenz: degenerate params (σ<0 OR ρ<=0 OR β<=0) → fall back to canonical butterfly (σ=10, ρ=28, β=8/3) + WarnOnce. Logistic mirror: r outside [0, 4] → clamp + WarnOnce."
    - "Two-overload `quantizeToScale`: String-form via `ScaleDatabase.GetScaleNotes` + Array[Note] direct form (escape hatch from ScaleDatabase). Both normalise the input series to [0, 1] via min/max scaling, floor-map to scale-note index, emit one quarter-note per value packed into a single 4/4 bar."
    - "Unknown scale name → charitable fallback to chromatic 12-tone (C4..B4) + WarnOnce per CLAUDE.md ergonomics. Composer hears the warning but renders something — NO hard error."
    - "Cross-platform FP divergence documented as platform-specific limitation per D-36-09: chained FP arithmetic in chaotic integration amplifies platform-specific FPU / Math.* quirks exponentially after ~50 iterations. SAME-PLATFORM two-run cmp-clean preserved; CROSS-PLATFORM reproducibility NOT guaranteed."

key-files:
  created:
    - "flow-lang/StandardLibrary/Generative/ChaosFunctions.cs (511 lines — class + 4 registered builtins (lorenz / logistic / quantizeToScale×2) + Lorenz forward-Euler + logistic recurrence + min/max normalise+quantize core + ChariteableLorenzParams + ClampRWithAdvisory + ClampLengthWithAdvisory + ScaleDatabase string-form resolver + chromatic-fallback helper + MidiPitch internal record)"
    - "flow-lang.Tests/Phase36/ChaosTests.cs (265 lines — 10 facts: 3 Lorenz + 3 logistic + 4 quantizeToScale)"
    - "flow-lang.Tests/Phase36/ChaosDeterminismTests.cs (149 lines — 4 facts: 2 same-seed determinism + 1 different-seed sanity + 1 source-grep gate)"
    - "tests/test_lorenz_quantize.flow (77 lines — 3 composer tests + renderable WAV target for two-run determinism harness)"
    - "tests/test_logistic.flow (85 lines — 4 composer tests + renderable WAV target)"
  modified:
    - "flow-lang/Core/FlowEngine.cs (+10 lines — ChaosFunctions.RegisterContextDependent wired alongside CellularFunctions)"
    - "flow-lang/generative.flow (+17 lines — chaos-map module header documenting cross-platform FP caveat + 4 internal proc forward decls: lorenz / logistic / quantizeToScale ×2)"

key-decisions:
  - "**Warm-up = 100 iterations for BOTH Lorenz and logistic.** The plan's `<interfaces>` specified 100 for Lorenz; I extended the same value to logistic for consistency. Logistic with r∈(1,3) settles to the fixed point x*=1-1/r within ~30 iterations; 100 is generous-but-safe headroom. Logistic with r∈[3.57, 4.0] is fully chaotic from any starting x in (0,1) so warm-up has no effect there; the warm-up only matters for the fixed-point regime where it removes transient behaviour."
  - "**Canonical butterfly fallback for Lorenz uses EXACT 8.0/3.0 = 2.6666...**, not the 4-decimal-rounded 2.6667 mentioned in the plan's `<interfaces>` prose. Forward-Euler integration over a chaotic attractor amplifies even sub-1e-5 discrepancies in β to ~3e-3 visible at trajectory index 0 over 100 iterations. The xUnit fact `LorenzDegenerateParamsFallsBackToCanonical` compares against `(div 8.0 3.0)` to match the implementation's canonical constant."
  - "**Normalize-then-quantize flow: min/max scaling of the input series to [0, 1], then floor-map to `scaleNotes.Length`-bucketed indices.** A constant series (max ≈ min within 1e-12) treats the range as 1.0 to avoid divide-by-zero — every value maps to scale index 0. The output Sequence packs all notes into a single 4/4 bar without bar-fitting; the composer applies `(fast)` / `(slow)` / bar-splitting downstream if needed. This single-bar shape matches the Plan 36-07 `lsystemToSequence` pattern (single-bar Sequence output) — composers get a uniform shape across the three Plan 36-07/08/09 → Sequence mapping primitives."
  - "**Unknown scale name charitably falls back to chromatic 12-tone (C4..B4), NOT a hard error.** Per CLAUDE.md ergonomics + D-v1.5-05 charitable interpretation: composer hears a stderr advisory but renders something. The fallback uses MIDI 60..71 (12 chromatic semitones) so the same series produces a richer, denser output than a 7-note diatonic scale — composers can audibly distinguish 'wrong scale name' from a deliberately chromatic Choice. The plan's `<behavior>` block flagged this as a Claude's-Discretion pick between hard-error and charitable; I picked charitable to match the existing Plan 36-06/07/08 charitable-fallback patterns."
  - "**Same-platform two-run cmp-clean preserved; cross-platform reproducibility explicitly NOT guaranteed for chaotic-system outputs (D-36-09 / Pitfall 4).** The xmldoc on `Lorenz` and `Logistic` C# methods, the `flow-lang/generative.flow` module-level Note block, and the headers of both composer test files (`test_lorenz_quantize.flow` + `test_logistic.flow`) all document this caveat explicitly. The verification block ran the harness on Linux x64 (the project's primary platform) — both runs produced byte-identical SHA-256s. Cross-platform CI gates (if/when added in Phase 41) MUST exclude Lorenz/logistic fixtures from shared-baseline comparison."
  - "**`new Random(seed)` is called BEFORE the warm-up loop, immediately after the param-fallback check.** This puts the perturbation derivation at a deterministic point in the algorithm — both the degenerate-fallback path and the canonical-direct path use the same RNG draw count (one NextDouble call) so their post-warmup trajectories are byte-identical given the same effective (σ, ρ, β) and same seed."

patterns-established:
  - "Single-dimension `ClampLengthWithAdvisory(value, ctx, siteName)`: simplified shape of Plan 36-08's per-dimension `ClampDimensionWithAdvisory` — no `dimName` parameter because chaos-map primitives have one bounded dimension (`length`). Plans 36-10/11/12 stochastic primitives with single-`length` arg can reuse verbatim."
  - "Per-builtin `// PRNG-SANCTIONED:` marker convention extended to two-hit caps (Plan 36-09 introduces the first per-file cap > 1 in the Generative/ directory). The marker convention scales: each sanctioned `new Random(` line carries an explicit reason in the trailing comment (`PRNG-SANCTIONED: explicit-seed REQ contract per D-36-09`)."
  - "Multi-overload registration for `quantizeToScale` (String form + Array[Note] form): two separate `registry.Register` calls with distinct ParameterNames — the OverloadResolver picks at call time based on the 2nd arg's runtime type. Matches Plan 36-06's `markovTrain` defaulted-vs-features=-Symbol pattern."

requirements-completed: [GEN-04, GEN-05]
# GEN-04 (chaos-map primitives — lorenz / logistic / quantizeToScale) — primary delivery.
# GEN-05 (two-run cmp-clean determinism) — reinforced via
# `scripts/test_two_run_determinism.sh tests/test_lorenz_quantize.flow` exit 0
# AND `scripts/test_two_run_determinism.sh tests/test_logistic.flow` exit 0
# on Linux x64 (D-36-09 same-platform contract).

# Metrics
duration: ~30 min
completed: 2026-05-22
---

# Phase 36 Plan 09: Chaos Map Primitives Summary

**Lorenz attractor (forward-Euler integration of the canonical 3-state ODE with σ=10, ρ=28, β=8/3 butterfly fallback for degenerate params), logistic map (x_{n+1} = r * x_n * (1 - x_n) with r clamped to [0, 4]), and `quantizeToScale` bridge in two overloads (String scale-name via ScaleDatabase + Array[Note] direct form). Both chaos primitives derive their single PRNG draw from the REQ-mandated seed arg only — two `// PRNG-SANCTIONED:` markers cap the source-grep gate at 2 hits. T-36-21 DoS guard via `ClampLengthWithAdvisory` at 100_000-element cap. D-36-09 cross-platform FP divergence documented as platform-specific limitation in C# xmldoc + generative.flow module header + composer test file headers; same-platform two-run cmp-clean preserved (both harness invocations exit 0 with identical SHA-256s on Linux x64).**

## Performance

- **Duration:** ~30 min
- **Tasks:** 2 of 2
- **Files created:** 5
- **Files modified:** 2

## Accomplishments

- `flow-lang/StandardLibrary/Generative/ChaosFunctions.cs` — 511 lines. Four registered builtins:
  - `lorenz(Double, Double, Double, Int, Int) → Array[Double]` — forward-Euler trajectory of the x-axis with dt=0.01, warmup=100; canonical butterfly fallback (σ=10, ρ=28, β=8/3) on degenerate params (σ<0 OR ρ<=0 OR β<=0).
  - `logistic(Double, Int, Int) → Array[Double]` — x_{n+1} = r * x_n * (1 - x_n) recurrence in [0, 1]; r outside [0, 4] charitably clamps + WarnOnce.
  - `quantizeToScale(Double[], String) → Sequence` — String-form scale-name lookup via `ScaleDatabase.GetScaleNotes`; unknown name charitably falls back to chromatic 12-tone (C4..B4) + WarnOnce.
  - `quantizeToScale(Double[], Note[]) → Sequence` — Array[Note] direct form (composer's escape hatch).
- Charitable interpretation per D-v1.5-05:
  - Lorenz degenerate (σ<0 OR ρ<=0 OR β<=0) → canonical butterfly + WarnOnce
  - Logistic r < 0 OR r > 4 → clamp + WarnOnce (r > 4 escapes [0, 1] and produces NaN)
  - length <= 0 → return empty Array[Double] + WarnOnce
  - length > 100_000 → clamp to 100_000 + WarnOnce (T-36-21 DoS guard)
  - quantizeToScale unknown scale-name → chromatic fallback + WarnOnce
  - quantizeToScale empty series → empty Sequence + WarnOnce
- D-36-09 cross-platform FP divergence documented in **three** places (C# xmldoc on lorenz + logistic methods; flow-lang/generative.flow module header; composer test file headers): same-platform two-run cmp-clean preserved; cross-platform reproducibility NOT guaranteed for chaotic-system outputs.
- 14 xUnit facts: 10 in `ChaosTests` (3 Lorenz + 3 logistic + 4 quantizeToScale) + 4 in `ChaosDeterminismTests` (2 same-seed + 1 different-seed sanity + 1 source-grep gate) — all GREEN.
- 7 composer-facing tests across `tests/test_lorenz_quantize.flow` (3 tests) + `tests/test_logistic.flow` (4 tests) — all PASS via `flow-cli test`.
- Phase 36 regression: 131/131 GREEN (no regression vs Plan 36-08 baseline; 14 new Chaos facts net).
- Two-run cmp-clean determinism: both `bash scripts/test_two_run_determinism.sh tests/test_lorenz_quantize.flow` and `bash scripts/test_two_run_determinism.sh tests/test_logistic.flow` exit 0 with identical SHA-256s on Linux x64.

## Task Commits

Each task was committed atomically:

1. **Task 1 RED — Failing Chaos tests (Lorenz + logistic + quantizeToScale)** — `57b0633` (test)
2. **Task 1 GREEN — ChaosFunctions implementation + FlowEngine wiring + generative.flow decls** — `f96b5b2` (feat)
3. **Task 2 — Composer-facing tests/test_lorenz_quantize.flow + tests/test_logistic.flow + two-run determinism gate** — `061f2ab` (test)

## Files Created/Modified

### Created

- `flow-lang/StandardLibrary/Generative/ChaosFunctions.cs` — Four registered builtins + algorithm implementations + min/max normalise+quantize core + charitable guards
- `flow-lang.Tests/Phase36/ChaosTests.cs` — 10 xUnit facts pinning Lorenz returns/bounded-envelope/degenerate-fallback + logistic [0,1]-bound/fixed-point/r-clamp + quantizeToScale string/array/normalisation/unknown-name-charitable
- `flow-lang.Tests/Phase36/ChaosDeterminismTests.cs` — 4 facts: 2 same-seed bit-identical + 1 different-seed sanity + 1 source-grep gate (≤ 2 `new Random(` hits)
- `tests/test_lorenz_quantize.flow` — 3 composer tests + writeWav target for the two-run determinism harness
- `tests/test_logistic.flow` — 4 composer tests + writeWav target

### Modified

- `flow-lang/Core/FlowEngine.cs` — `ChaosFunctions.RegisterContextDependent` wiring alongside Markov/Lsystem/Cellular
- `flow-lang/generative.flow` — Four `internal proc` forward decls (lorenz / logistic / quantizeToScale × 2) with cross-platform FP caveat documentation block

## Decisions Made

See key-decisions in the frontmatter for full rationale. The two highest-impact decisions:

- **Warm-up = 100 iterations for both primitives.** Lorenz needs it to discard the chaotic transient and let the trajectory settle onto the attractor; logistic in fixed-point regime (r ∈ (1, 3)) settles to x* = 1 - 1/r within ~30 iterations, so 100 is generous-but-safe headroom. The chaotic regime (r ∈ [3.57, 4.0]) is insensitive to warmup count.
- **Cross-platform FP divergence documented as platform-specific limitation, NOT mitigated in v1.5.** Per D-36-09 + RESEARCH Pitfall 4: replacing `Math.Sin/Cos/Sqrt` with a software-only deterministic library (e.g. MathNet's deterministic mode) is the v1.6+ mitigation path. v1.5 ships the platform-specific contract: same-machine two-run cmp-clean preserved (single-machine IEEE 754 reproducibility); cross-platform reproducibility NOT guaranteed.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 — Bug] `LorenzDegenerateParamsFallsBackToCanonical` test compared fallback (β = 8/3 = 2.6666...) against direct call with β = 2.6667 (4-decimal rounding)**

- **Found during:** Task 1 GREEN — initial test run.
- **Issue:** Forward-Euler integration over a chaotic attractor amplifies even sub-1e-5 discrepancies in β exponentially. The plan's `<interfaces>` prose mixed "8/3" (exact canonical) and "2.6667" (4-decimal rounded). My implementation correctly uses the exact `8.0/3.0`, so the test's `(lorenz 10.0 28.0 2.6667 100 42)` direct-canonical comparison diverged at trajectory index 0 by ~3e-4, increasing with each iteration.
- **Fix:** Updated the test to use `Double beta = (div 8.0 3.0)` for the direct-canonical reference call. The fallback path and the direct-canonical path now both use the exact β = 8/3 internal constant; trajectories are byte-identical given the same seed.
- **Files modified:** flow-lang.Tests/Phase36/ChaosTests.cs (LorenzDegenerateParamsFallsBackToCanonical body)
- **Verification:** Fact GREEN after fix.
- **Committed in:** f96b5b2 (Task 1 GREEN, same commit as the implementation)

---

**Total deviations:** 1 auto-fixed (Rule 1 — test design bug surfaced by initial GREEN run).
**Impact on plan:** Zero — composer-facing API unchanged, all xUnit + composer tests pass, both two-run determinism harnesses pass with identical SHA-256s on Linux x64.

## Issues Encountered

### Worktree path-safety incident (Rule 1 fix during execution)

**Pre-existing failure log:** The first attempt to write `ChaosTests.cs` + `ChaosDeterminismTests.cs` accidentally targeted the MAIN repo path (`/home/noah/Desktop/projects/flow-sharp/flow-lang.Tests/Phase36/`) instead of the worktree path. This was caught immediately — the absolute path was assembled from a stale `pwd`-derived prefix without rederiving from `git rev-parse --show-toplevel` inside the worktree, exactly the failure mode `<execution_context>`'s worktree-path-safety reference describes (#3099). The misplaced files were deleted from the main repo before any commit (no main-repo state changed); the second Write call used the canonical worktree path and committed cleanly. No source change required — this was a methodology fix during execution.

### Pre-existing orphan working-tree changes inherited from prior worktree base

**Same orphan state as Plans 36-01/05/06/07/08:** The worktree base (58413de) inherits a debug session's uncommitted modifications to `SampledInstrumentRenderer.cs`, `BarRenderer.cs`, `NoteStreamCompiler.cs`, `FlowGenerator.cs`, `Quantizer.cs`, and a couple of other files. These cause **32 pre-existing failures** in `Phase28.PerSynthArticulationTests` (29 rows) and `Phase29.ArticulationOnSampleTests` (3 rows), all driven by `SampledInstrumentRenderer.cs`'s tail-extension experiment adding ~0.5s of frames past the authored duration (FFT cosine-similarity tests catch the timbral mismatch). Per the SCOPE BOUNDARY deviation rule, these are out-of-scope for Plan 36-09 — documented in Plans 36-01/05/06/07/08 SUMMARYs and inherited here unchanged.

**In-scope test results:**

| Suite | Pass/Total | Status |
|-------|------------|--------|
| Phase 36 (full — incl. 36-01..08 + 36-09) | 131/131 | green |
| Chaos surface (Plan 36-09 facts) | 14/14 | green |
| Cross-Generative source-grep gate (PrngRegistryNewRandomGateTests) | 3/3 | green |
| Two-run cmp-clean determinism on tests/test_lorenz_quantize.flow | SHA 0bde9224... | green |
| Two-run cmp-clean determinism on tests/test_logistic.flow | SHA 9ec25551... | green |
| Composer tests (test_lorenz_quantize + test_logistic) | 7/7 | green |

## Self-Check: PASSED

**Files asserted:**

- `[ -f flow-lang/StandardLibrary/Generative/ChaosFunctions.cs ]` → FOUND
- `[ -f flow-lang.Tests/Phase36/ChaosTests.cs ]` → FOUND
- `[ -f flow-lang.Tests/Phase36/ChaosDeterminismTests.cs ]` → FOUND
- `[ -f tests/test_lorenz_quantize.flow ]` → FOUND
- `[ -f tests/test_logistic.flow ]` → FOUND

**Commits asserted:**

- `57b0633` (Task 1 RED) → FOUND in `git log --oneline`
- `f96b5b2` (Task 1 GREEN) → FOUND in `git log --oneline`
- `061f2ab` (Task 2) → FOUND in `git log --oneline`

**No-regression assertions:**

- Phase 36 full: 131/131 PASS (matches Plan 36-08 baseline of 117 + 14 new Chaos facts = 131 expected; verified by `dotnet test --filter "Phase36"`)
- Two-run cmp-clean: both Plan 36-09 composer tests produce identical SHA-256 across consecutive renders on Linux x64
- Source-grep gate: 2 sanctioned `new Random(` hits in ChaosFunctions.cs (one per primitive, both marked `// PRNG-SANCTIONED:`); cross-Generative-directory gate (PrngRegistryNewRandomGateTests) reports zero unsanctioned hits across Patterns/Generative/Improv

## What This Unblocks

- **Plan 36-10 — @improv stdlib (jam)** — D-36-09 cross-platform FP caveat pattern is reusable: any new primitive that uses chained transcendental Math.* calls in chaotic-system context should inherit the same xmldoc + module-header documentation pattern.
- **Plan 36-11 — Universal named-argument syntax** — chaos primitives' `ParameterNames` (`["sigma", "rho", "beta", "length", "seed"]` etc.) are ready for the named-arg backfill; no additional action required.
- **Plan 36-12 — Phase 36 GEN-05 phase gate** — `tests/test_lorenz_quantize.flow` and `tests/test_logistic.flow` join the canonical two-run cmp-clean target list (joining `test_patterns_chain.flow`, `test_markov_oneshot.flow`, `test_lsystem_oneshot.flow`, `test_cellular_rule30.flow`, `test_cellular_life.flow`). The phase gate documentation MUST note the cross-platform FP caveat — chaos fixtures gate same-platform reproducibility only.

## Threat Surface Scan

No new threat surface beyond the plan's `<threat_model>` register:

| Threat | Disposition | Status |
|--------|-------------|--------|
| T-36-21 (DoS / length > 100_000 forcing huge Array allocation) | mitigate | ✓ `ClampLengthWithAdvisory` clamps to 100_000 + WarnOnce |
| T-36-22 (Integrity / Lorenz cross-platform FP divergence) | accept | ✓ Documented in C# xmldoc + generative.flow module header + composer test headers; same-platform two-run cmp-clean preserved (verified) |
| T-36-23 (Integrity / Logistic r > 4 producing NaN escape) | mitigate | ✓ `ClampRWithAdvisory` clamps r to 4.0 + WarnOnce |

No new threat flags emerged.

---

*Phase: 36-sequence-algebra-generative*
*Plan: 09*
*Completed: 2026-05-22*
