# Phase 25: Gaussian Humanize (LAST PRNG phase) - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-04
**Phase:** 25-gaussian-humanize-last-prng-phase
**Areas discussed:** Function Signature (DA-1) · PRNG Strategy (DA-2) · Box-Muller Variant (DA-3) · Velocity Scaling + Clamping (DA-4) · Rest + Edge Handling (DA-5) · File Location + Registration (DA-6) · Existing humanize Invariance (DA-7) · Showcase + Tutorial Updates (DA-8) · Test Coverage (DA-9) · std.flow Public Declaration (DA-10)
**Mode:** `--auto` (single-pass; recommended option auto-selected for every question per `modes/auto.md`)

---

## Function Signature (DA-1)

| Option | Description | Selected |
|--------|-------------|----------|
| `humanizeGaussian(Sequence, Double, Int)` with `(seq, amount, seed)` | Matches REQ DEFER-06 verbatim; mirrors euclidean 6-arg seed-as-last convention | ✓ |
| `humanizeGaussian(Sequence, Int, Double)` with `(seq, seed, amount)` | Seed-first ordering — inconsistent with codebase convention | |
| Add explicit stddev parameter `humanizeGaussian(Sequence, Double, Double, Int)` | Splits "amount" into amount + stddev — overcomplicates for marginal expressiveness | |

**Auto choice:** Option 1 (recommended default; matches REQ wording verbatim).
**Notes:** Locked decision D-01.

---

## PRNG Strategy (DA-2)

| Option | Description | Selected |
|--------|-------------|----------|
| LOCAL `new Random(seed)` per call | Mirrors VarySeeded:71-77 + euclidean D-17 | ✓ |
| Read from `ExecutionContext.GetRand` with seed override | Couples to global PRNG state — breaks isolation | |
| Custom xorshift / PCG implementation | Adds complexity without semantic benefit; .NET Random is sufficient for compile-time velocity jitter | |

**Auto choice:** Option 1 (recommended default; matches established codebase pattern).
**Notes:** Locked decision D-03.

---

## Box-Muller Variant (DA-3)

| Option | Description | Selected |
|--------|-------------|----------|
| Basic Box-Muller (cos/sin) | Two NextDouble calls per sample; deterministic; no rejection sampling | ✓ |
| Marsaglia polar method | ~10% faster but rejection loop creates state complexity for marginal benefit | |
| Ziggurat algorithm | Fastest but heavyweight for compile-time use; overkill | |

**Auto choice:** Option 1 (recommended default; simplest deterministic-friendly variant).
**Notes:** Locked decision D-05.

---

## Velocity Scaling + Clamping (DA-4)

| Option | Description | Selected |
|--------|-------------|----------|
| Match existing `humanize` scale (`* amount * 0.2`) + clamp [0.05, 1.0] | Composers learn one mental model; preserves musical sensibility | ✓ |
| Use `amount` as raw stddev (no `* 0.2`) | Larger jitter range; breaks symmetry with uniform humanize | |
| Reflect at boundaries instead of clamping | More mathematically pure but musically unhelpful (silent ghost notes when reflected to 0) | |

**Auto choice:** Option 1 (recommended default; mirrors existing humanize:893-894).
**Notes:** Locked decisions D-07, D-08, D-09.

---

## Rest + Edge Handling (DA-5)

| Option | Description | Selected |
|--------|-------------|----------|
| Rests passthrough; amount=0 short-circuits; negative amount silently clamps | Charitable interpretation memory: silent-and-documented over errors | ✓ |
| Throw on negative amount | Breaks charitable-interpretation memory | |
| Apply jitter to rests too (NaN-velocity rests) | Nonsensical — rests have no audible velocity | |

**Auto choice:** Option 1 (recommended default; aligns with charitable-interpretation memory).
**Notes:** Locked decisions D-10, D-11, D-12, D-13, D-14.

---

## File Location + Registration (DA-6)

| Option | Description | Selected |
|--------|-------------|----------|
| Adjacent to existing `Humanize` in `TransformFunctions.cs` (sibling pattern) | Minimal-impact placement; codebase convention for paired transforms | ✓ |
| New file `flow-lang/StandardLibrary/Transforms/HumanizeGaussian.cs` | Premature decomposition — sub-100-line addition doesn't justify a new file | |
| New module `flow-lang/StandardLibrary/Random/` | Overengineering; one function | |

**Auto choice:** Option 1 (recommended default; codebase convention).
**Notes:** Locked decisions D-16, D-17.

---

## Existing `humanize` Invariance (DA-7)

| Option | Description | Selected |
|--------|-------------|----------|
| FREEZE existing `humanize(Sequence, Double)` — no modification, no overload, no deprecation | Required by D-04 to preserve v1.2 byte-identical contract | ✓ |
| Add a 3-arg seeded `humanize` overload alongside Gaussian | Competes with `humanizeGaussian` for same problem space; not requested | |
| Deprecate uniform `humanize` in favor of Gaussian | Breaking change; unnecessary | |

**Auto choice:** Option 1 (recommended default; mandated by D-04).
**Notes:** Locked decisions D-18, D-19.

---

## Showcase + Tutorial Updates (DA-8)

| Option | Description | Selected |
|--------|-------------|----------|
| ONE additive `humanizeGaussian` call site in showcase + new tutorial chapter | Additive only — preserves baseline; QOL-04 v1.3 demonstration coverage | ✓ |
| Replace existing humanize call in showcase | Would change v1.2 baseline output → break byte-identical regression | |
| Don't touch showcase / tutorial | Misses success criterion 3 (showcase-with-Gaussian byte-identical) and QOL-04 v1.3 coverage | |

**Auto choice:** Option 1 (recommended default; additive only).
**Notes:** Locked decisions D-20, D-21, D-22.

---

## Test Coverage (DA-9)

| Option | Description | Selected |
|--------|-------------|----------|
| 7 xUnit Facts (deterministic pin, two-runs identity, cross-seed difference, amount=0 short-circuit, rests passthrough, clamp, statistical sanity) + 1 byte-identical integration test | Pins DEFER-06 acceptance + regression sentinels + statistical confidence | ✓ |
| Just the deterministic pin Fact | Insufficient coverage — would let regressions slip through | |
| Add Shapiro-Wilk normality tests | Overkill for 1000-sample size; mean+stddev tolerance is sufficient | |

**Auto choice:** Option 1 (recommended default; balances coverage with maintenance cost).
**Notes:** Locked decisions D-23, D-24.

---

## std.flow Public Declaration (DA-10)

| Option | Description | Selected |
|--------|-------------|----------|
| Add `internal proc humanizeGaussian (Sequence: seq, Double: amount, Int: seed)` to std.flow:137 | Required for user scripts to call the function; mirrors humanize:136 + euclidean:154 | ✓ |
| Skip std.flow declaration | Function would be invisible to user .flow scripts even after registry registration | |

**Auto choice:** Option 1 (recommended default; mandatory for visibility).
**Notes:** Locked decision D-25.

---

## Deferred Ideas

- Other distributions (Cauchy, Laplace, exponential, triangular) — future v1.4 phase
- Per-axis humanize (timing, duration) — future enhancement phase
- `humanize(Sequence, Double, Int)` seeded uniform overload — not requested; conflicts with humanizeGaussian
- Marsaglia polar method or Ziggurat — premature optimization
- Cached second Box-Muller sample — order-dependence violates determinism contract
- Statistical normality tests (Shapiro-Wilk, Anderson-Darling) — overkill
- SIMD/vectorized Box-Muller — premature optimization
- Modifying `humanize(Sequence, Double)` to be deterministic — breaks v1.2 byte-identity by definition
- LSP linting / hover / completion for `humanizeGaussian` — LSP is parse-time only; runtime velocity perturbation has no analyzer

## Claude's Discretion

- Inline Box-Muller in HumanizeGaussian method body OR extract to private static helper (planner decides)
- u1 near-zero guard syntax (`Math.Max` vs `if`) — both equivalent
- Theory matrix size for cross-seed Facts (1 seed vs 3 seeds)
- Exact base velocity in test fixtures (0.63 recommended)
- Showcase.flow call site target Sequence (melody recommended)

---

*Mode log: discuss-phase auto-selected all 10 gray areas and chose the recommended option for each per `workflows/discuss-phase/modes/auto.md`. No user prompts were issued.*
