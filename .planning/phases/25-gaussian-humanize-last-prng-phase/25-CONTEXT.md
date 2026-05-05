# Phase 25: Gaussian Humanize (LAST PRNG phase) - Context

**Gathered:** 2026-05-04
**Status:** Ready for planning
**Source:** /gsd-discuss-phase 25 --auto (single-pass auto mode)

<domain>
## Phase Boundary

Adds a NEW `humanizeGaussian(Sequence, Double, Int)` built-in (Box-Muller transform; LOCAL seeded PRNG per call) so composers can opt into Gaussian-distributed velocity perturbation. The existing `humanize(Sequence, Double)` transform at `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs:866-903` remains FROZEN — same signature, same body, same static `HumanizeRng = new()` semantics — preserving the v1.2 byte-identical determinism contract for `examples/tutorial.flow` and `examples/showcase.flow`.

Locked by REQUIREMENTS.md DEFER-06 (lines 109–110), PROJECT.md decision D-04 ("Gaussian humanize ships as a separate `humanizeGaussian()` function"), and the binding pre-ordering note "DEFER-06 (Gaussian) MUST be the LAST PRNG-touching phase".

**In scope:**
- New `humanizeGaussian(Sequence, Double, Int)` registered in `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` adjacent to the existing `RegisterHumanize` / `Humanize` pair (sibling pattern). Signature: `(seq, amount, seed)` — matches REQ DEFER-06 acceptance text verbatim and mirrors `euclidean`'s 6-arg seed-as-last convention.
- Box-Muller transform implementation (basic cos/sin form) producing a fresh `N(0, 1)` sample per non-rest note via two `rng.NextDouble()` calls. Multiplied by `amount * 0.2` to match the existing `humanize` jitter scale, giving `N(0, (amount*0.2)²)` velocity perturbation.
- LOCAL `new Random(seed)` per call — mirrors `VariationFunctions.VarySeeded:71-77` and `BuiltInFunctions.cs:1258` (euclidean 6-arg D-17). Does NOT touch `ExecutionContext.GetRand` or any global PRNG state.
- xUnit Facts in `flow-lang.Tests/Unit/Phase25/HumanizeGaussianFacts.cs` pinning specific velocity bytes for `seed=42` (DEFER-06 acceptance), plus statistical sanity Facts (mean ≈ 0, stddev within tolerance), plus rest-passthrough + clamp + amount=0 short-circuit Facts.
- Phase 18 byte-identical regression tests extended: `flow-lang.Tests/Integration/Phase25/ByteIdenticalShowcaseGaussianTests.cs` proving `tutorial.flow` and `showcase.flow` produce cmp-clean WAV + MIDI across two consecutive runs (success criterion 3).
- New showcase.flow call site: ONE additive `humanizeGaussian(seq, amount, seed)` invocation on an existing Sequence (not a replacement of any existing transform — additive only, preserves baseline).
- Optional tutorial.flow chapter demonstrating `humanizeGaussian` for QOL-04 v1.3 final-tutorial coverage.

**Out of scope (deferred or other phases):**
- Modifying existing `humanize(Sequence, Double)` — D-04 / D-08 invariant; would break the v1.2 byte-identical contract. The static `HumanizeRng` non-determinism is preserved as-is.
- Adding a 3-arg seeded `humanize(Sequence, Double, Int)` overload — D-04 explicitly says "ships as a SEPARATE `humanizeGaussian()` function". A seeded uniform overload would compete with `humanizeGaussian` for the same problem space and isn't requested.
- Other distributions (Cauchy, Laplace, exponential) — REQ scope is Gaussian only; future phase if composer feedback requests.
- Caching the second Box-Muller sample (the basic transform paired output) — D-14: each note gets its own fresh `cos/sin` pair. Caching would create order-of-call dependence between consecutive notes and complicate parity-of-count edge cases for marginal perf benefit.
- Vectorized/SIMD Box-Muller — premature optimization; the transform runs at compile time, not in the audio hot path.
- Per-note independent seeding (e.g., `seed_for_note_i = base_seed + i`) — overcomplicated; LOCAL `new Random(seed)` advances internally per call to NextDouble, sufficient for determinism.
- Touching any other PRNG-using function (`euclidean`, `vary`, random note streams `(? ...)` / `(?? ...)`) — Phase 25 is the LAST PRNG-touching phase per Pitfall 6 mitigation. After this phase ships, no further PRNG changes are allowed in v1.3.

</domain>

<decisions>
## Implementation Decisions

### Function Signature (DA-1)

- **D-01:** Signature is `humanizeGaussian(Sequence, Double, Int)` with parameter order `(seq, amount, seed)`. Matches REQ DEFER-06 acceptance text "`humanizeGaussian(seq, 0.1, 42)`" verbatim. Mirrors `euclidean`'s 6-arg form which puts seed last (`euclidean(hits, steps, note, swing, humanize, seed)`). Single overload — no Sequence-only or Sequence+Double overloads (those would invite confusion with the existing uniform `humanize`).
- **D-02:** Function name is `humanizeGaussian` (camelCase) — locked by REQ DEFER-06 + PROJECT.md D-04. No alternative considered.

### PRNG Strategy (DA-2)

- **D-03:** Use LOCAL `new Random(seed)` per call. Mirrors `VariationFunctions.VarySeeded` at `flow-lang/StandardLibrary/Composition/VariationFunctions.cs:71-77` and `BuiltInFunctions.cs:1258` (euclidean 6-arg D-17 comment: *"LOCAL new Random(seed) scoped to THIS call; does NOT read or mutate ExecutionContext.GetRand. Mirrors VariationFunctions.VarySeeded at :71-77."*). Isolation from `ExecutionContext.GetRand` is essential — global PRNG state would couple `humanizeGaussian` to call order with other PRNG-using functions and break determinism.
- **D-04:** Per-note PRNG advances internally — the same `Random` instance produced from `new Random(seed)` is consumed by all notes in the sequence in iteration order. Determinism of velocity bytes for `seed=42` is therefore tied to the iteration order over `Sequence.Bars` and `Bar.MusicalNotes`, which the existing `Humanize` method already uses (matched pattern).

### Box-Muller Variant (DA-3)

- **D-05:** Use the BASIC Box-Muller transform (cos/sin form), not Marsaglia's polar method. Per-note implementation:
  ```csharp
  double u1 = rng.NextDouble();
  double u2 = rng.NextDouble();
  // Guard u1 from log(0): if u1 == 0, the transform diverges. Probability is negligible
  // (1 in 2^53 with .NET's NextDouble), but `u1 = Math.Max(u1, 1e-300)` is the standard guard.
  double z = Math.Sqrt(-2.0 * Math.Log(Math.Max(u1, 1e-300))) * Math.Cos(2.0 * Math.PI * u2);
  // z is now distributed as N(0, 1)
  ```
  Rationale: deterministic per-call (no rejection-sampling state), simpler than Marsaglia (no `do {} while ()` retry loop), statistically equivalent for our use case. Two `NextDouble` calls produce one Gaussian sample.
- **D-06:** Box-Muller's paired output (the `sin` companion) is DISCARDED. Each non-rest note consumes a fresh pair (`u1`, `u2`); the cos branch is the sample, the sin branch is thrown away. This wastes ~50% of generated normals but keeps the implementation parity-of-note-count agnostic and removes a stateful "previous sample cache" that would complicate `seed=42` byte-pinning across rest-heavy sequences.

### Velocity Scaling + Clamping (DA-4)

- **D-07:** Velocity perturbation formula: `velJitter = z * amount * 0.2` where `z ~ N(0, 1)`. The `* 0.2` scale matches the existing `humanize` jitter range exactly (`(rng.NextDouble() * 2.0 - 1.0) * amount * 0.2` at `TransformFunctions.cs:893`). Effect: composers learn one mental model — `amount` is the same parameter on both functions, but Gaussian gives a bell distribution (most jitter near 0, occasional larger excursions) versus uniform's flat distribution. Final perturbation is `N(0, (amount*0.2)²)`.
- **D-08:** `amount` is clamped to `[0.0, 1.0]` via `Math.Clamp(amount, 0.0, 1.0)` — matches existing `humanize:878`. Out-of-range amounts silently clamp (charitable interpretation memory: silent-and-documented over errors).
- **D-09:** Velocity is clamped to `[0.05, 1.0]` via `Math.Clamp(note.Velocity + velJitter, 0.05, 1.0)` — matches existing `humanize:894`. The lower bound 0.05 (not 0.0) prevents inaudible "ghost" notes that would silently drop in MIDI export. The upper bound 1.0 caps at full velocity. Clamping not reflection.
- **D-10:** Short-circuit when `amount == 0.0` (post-clamp): return the input sequence unchanged. Avoids consuming PRNG state + matches the philosophical "amount=0 means no change" invariant. The existing `humanize` does NOT short-circuit, but `humanizeGaussian` should because Box-Muller's `Math.Log(u1)` is wasted compute when the result is multiplied by zero anyway.

### Rest + Edge Handling (DA-5)

- **D-11:** Rests (`note.IsRest == true`) pass through unchanged — matches existing `humanize:887-890` loop. No PRNG consumption for rests, so determinism is unaffected by rest density.
- **D-12:** Sequences with zero non-rest notes return unchanged (no-op). The PRNG is constructed but never advanced. No special-case branch needed; the loop body just doesn't execute.
- **D-13:** Empty sequences (`seq.Bars.Count == 0`) return unchanged. Same fall-through as D-12.
- **D-14:** Negative `amount` values are clamped to 0 via D-08 (Math.Clamp lower bound). No "negative jitter" mode — would invert the distribution which makes no semantic sense.
- **D-15:** `seed` parameter is `Int` (not `Long`) — matches `Random(int)` ctor and the euclidean 6-arg D-17 precedent. Composers typing literal seeds (`humanizeGaussian(seq, 0.1, 42)`) get Int automatically.

### File Location + Registration (DA-6)

- **D-16:** New code lives in `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` adjacent to the existing `// ===== Humanize =====` section at line 864. Add a new `// ===== Humanize Gaussian =====` block with `RegisterHumanizeGaussian(registry)` called from `RegisterAll(registry)` at line 28 (immediately after `RegisterHumanize(registry)`). Sibling pattern matches the codebase convention for grouping related transforms.
- **D-17:** No new files required — this is a sub-100-line addition to an existing file. The `Box-Muller` implementation is inline as a `private static double NextGaussianSample(Random rng)` helper at the bottom of the `TransformFunctions` class (or just inlined in the `HumanizeGaussian` method body — planner decides).

### Existing `humanize` Invariance (DA-7)

- **D-18:** The existing `humanize(Sequence, Double)` at `TransformFunctions.cs:866-903` is FROZEN. NOT modified, NOT renamed, NOT deprecated, NOT overloaded with a seeded variant. The static `HumanizeRng = new()` non-determinism is part of the v1.2 baseline and is preserved as-is. (Rationale: `examples/tutorial.flow` line 502 documents the seeded behavior as belonging to `euclidean`'s 6-arg form, and `examples/showcase.flow` uses `euclidean`'s seeded humanize parameter rather than the standalone `humanize()` transform — so the standalone non-determinism does not affect the byte-identical contract.)
- **D-19:** The Phase 18 byte-identical regression tests (`flow-lang.Tests/Integration/Phase18/ByteIdenticalTutorialTests.cs` + `ByteIdenticalShowcaseTests.cs`) MUST stay GREEN after this phase. The `cmp` between two consecutive `dotnet run --project flow-interpreter examples/showcase.flow` runs MUST produce zero-byte diff for both WAV and MIDI output. This is the binding success criterion — failure here means the phase regressed and must roll back.

### Showcase + Tutorial Updates (DA-8)

- **D-20:** Add ONE additive `humanizeGaussian(seq, amount, seed)` call site to `examples/showcase.flow`. The call must be additive (insert a new line that wraps an existing Sequence) NOT a replacement of any existing transform. Replacing an existing call would change the v1.2 baseline output and break byte-identity by definition. Recommended placement: wrap the existing `melody` Sequence at line ~22 (after the `crescendo` is applied to `pad`), since melody is currently velocity-static and a Gaussian humanize call there is musically natural.
- **D-21:** Pre-emptively run two consecutive `dotnet run --project flow-interpreter examples/showcase.flow` invocations and `cmp` the outputs as the integration smoke for Phase 25 success criterion 3. Pin via `flow-lang.Tests/Integration/Phase25/ByteIdenticalShowcaseGaussianTests.cs` (mirroring the Phase 18 pattern at `flow-lang.Tests/Integration/Phase18/ByteIdenticalShowcaseTests.cs`).
- **D-22:** Add a `humanizeGaussian` chapter to `examples/tutorial.flow` for QOL-04 v1.3 final-tutorial coverage. Chapter contents: signature explanation, deterministic-with-seed example, contrast with uniform `humanize` (uniform = flat-distribution box; Gaussian = bell-distribution most-jitter-near-zero). Append after the existing `humanize` chapter (around line 567).

### Test Coverage (DA-9)

- **D-23:** xUnit Facts in `flow-lang.Tests/Unit/Phase25/HumanizeGaussianFacts.cs`. Required Facts:
  - `Seeded42_FirstNoteVelocity_PinnedExactly` — pin the first-note velocity for `humanizeGaussian(testSeq, 0.1, 42)` to a specific double (computed once via test-first run, then frozen). Acceptance for DEFER-06 "deterministic velocity bytes pinned by Fact".
  - `Seeded42_TwoConsecutiveCalls_ProduceIdenticalOutput` — same input + same seed = byte-identical output (regression).
  - `DifferentSeeds_ProduceDifferentOutput` — `seed=42` and `seed=43` produce measurably different velocity sequences (sanity check against accidentally returning the input unchanged).
  - `AmountZero_ReturnsInputUnchanged` — D-10 short-circuit verification.
  - `Rests_PassThroughUnchanged` — D-11 rest invariance.
  - `Velocity_ClampedTo_005_to_10` — D-09 clamp verification (use a high-amount + extreme-seed pair that would produce out-of-range without clamping).
  - `LargeSequence_DistributionIsApproximatelyNormal` — statistical sanity: 1000-note sequence with `amount=0.5, seed=42` produces mean within ±0.02 of base velocity AND stddev within ±20% of `0.5 * 0.2 = 0.1`. Allows looser tolerances given finite sample size.
- **D-24:** Integration test in `flow-lang.Tests/Integration/Phase25/ByteIdenticalShowcaseGaussianTests.cs` — run `examples/showcase.flow` twice and `cmp` WAV + MIDI byte-by-byte, expecting zero-byte diff. Mirrors `ByteIdenticalShowcaseTests.cs` at `flow-lang.Tests/Integration/Phase18/`.

### Std.flow Public Declaration (DA-10)

- **D-25:** Add `internal proc humanizeGaussian (Sequence: seq, Double: amount, Int: seed)` declaration to `flow-lang/std.flow` immediately after the existing `internal proc humanize` at line 136. Mirrors the `euclidean` 6-arg pattern. Without this `.flow`-side declaration, composers cannot call the function from user scripts — the registry registration alone is insufficient.

### Claude's Discretion (Planner Decides)

- Whether to inline Box-Muller in the `HumanizeGaussian` method body or extract to a `private static double NextGaussianSample(Random rng)` helper. Recommendation: extract — improves testability, the Box-Muller logic could conceivably be reused by future distribution functions.
- Whether the `u1` near-zero guard uses `Math.Max(u1, 1e-300)` or `if (u1 == 0.0) u1 = 1e-300`. Both are equivalent at runtime; planner picks based on existing codebase style.
- The exact base velocity used in `humanizeGaussian` test fixtures. Recommendation: 0.63 (matches `BuildEuclideanSequence` baseline at `BuiltInFunctions.cs:1283-1284`) for consistency with the codebase's de facto "default velocity" convention.
- Whether to add a Theory `[InlineData]` matrix over multiple seeds (42, 100, 12345) for the deterministic-pin Fact, or just one seed. Recommendation: single seed (42) Fact + 1-2 cross-seed differentiation Facts. The deterministic contract is "same seed → same output", not "every seed produces a unique output".
- Whether `examples/showcase.flow`'s new call site uses `melody` (recommended in D-20) or a different Sequence. Planner can pick based on the showcase's musical structure at planning time.

### Folded Todos

None — `gsd-sdk query todo.match-phase 25` returned 0 matches.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 25 Locked Requirements
- `.planning/REQUIREMENTS.md` lines 109–110 — DEFER-06 acceptance contract (the canonical contract for `humanizeGaussian(seq, 0.1, 42)`).
- `.planning/REQUIREMENTS.md` line 5 — D-04 "Gaussian humanize ships as a separate `humanizeGaussian()` function".
- `.planning/REQUIREMENTS.md` (Pre-ordering note) — "DEFER-06 (Gaussian) MUST be the LAST PRNG-touching phase".
- `.planning/ROADMAP.md` Phase 25 entry — goal, success criteria, dependency on Phases 18-24.
- `.planning/PROJECT.md` D-04 — same statement as REQUIREMENTS.md D-04 (cross-referenced).

### Existing Code This Phase Touches or Reads
- `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs:864-903` — existing `Humanize` method + `RegisterHumanize`. **FROZEN — DO NOT modify.** New `HumanizeGaussian` method + `RegisterHumanizeGaussian` lands adjacent to this section per D-16.
- `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs:28` — `RegisterAll(registry)` call site. Add `RegisterHumanizeGaussian(registry)` immediately after `RegisterHumanize(registry)`.
- `flow-lang/StandardLibrary/Composition/VariationFunctions.cs:71-77` — `VarySeeded` analog: `new Random(seed)` LOCAL per-call. The exact pattern this phase mirrors per D-03.
- `flow-lang/StandardLibrary/BuiltInFunctions.cs:1207, 1256-1258` — euclidean 6-arg D-17 comment: *"LOCAL new Random(seed) scoped to THIS call; does NOT read or mutate ExecutionContext.GetRand. Mirrors VariationFunctions.VarySeeded at :71-77."* Cite this comment style verbatim in the `HumanizeGaussian` method header.
- `flow-lang/std.flow:136` — `internal proc humanize` declaration. Add `internal proc humanizeGaussian` immediately after per D-25.
- `flow-lang/Runtime/MusicalNoteData.cs` — `MusicalNoteData` record (constructor at TransformFunctions.cs:896-898 reference). Velocity is a `double`. The constructor itself clamps velocity to `[0, 1]` (belt-and-braces per BuiltInFunctions.cs:1311 comment).
- `examples/showcase.flow` — add ONE additive `humanizeGaussian` call site (D-20). Recommended target: `melody` Sequence around line 22.
- `examples/tutorial.flow:567` — existing humanize chapter. Append `humanizeGaussian` chapter after, per D-22.

### Test Patterns to Follow
- `flow-lang.Tests/Integration/Phase18/ByteIdenticalShowcaseTests.cs` — Phase 18 byte-identical baseline pattern. Mirror in Phase 25's integration tests.
- `flow-lang.Tests/Integration/Phase15/EuclideanByteIdenticalTests.cs` — seeded-PRNG byte-identical pattern (showcases the exact "two consecutive runs cmp-clean" assertion shape).
- `flow-lang.Tests/FlowScriptData.cs:225-231` — Phase 15 DX-09 euclidean 6-arg humanize byte-identical pattern: writes WAV + MIDI from a `.flow` script, runs twice, compares bytes, asserts "two runs byte-identical: PASSED".
- `flow-lang.Tests/Unit/Phase15/EuclideanSwingTests.cs:32, 65, 83-87` — exact-velocity pinning pattern for seeded jitter Facts (asserts `note.Velocity == 0.63 + 0.3` style with explicit tolerance constant `Tol`).
- `flow-lang.Tests/Unit/Phase22/ArpeggioFacts.cs:13` — "byte-identical determinism" comment header style for the Phase 25 Facts header.

### Pitfalls and Constraints
- `.planning/research/PITFALLS.md` Pitfall 6 — the binding rationale for "DEFER-06 must be the LAST PRNG-touching phase". Cite when writing PLAN.md frontmatter.
- Phase 17 architecture — LSP does NOT run inside the REPL. `humanizeGaussian` is composer-only at evaluation time; LSP-side has no analyzer for this function. No flow-lsp work needed in Phase 25.

### Project Memory (CLAUDE.md auto-memory)
- `~/.claude/projects/-home-noah-Desktop-projects-flow-sharp/memory/feedback_charitable_interpretation.md` — informs D-08 (silent amount clamp), D-10 (silent amount=0 short-circuit), D-11 (rest passthrough), D-14 (negative amount silently clamped). "Music > rigid correctness" applies: composers typing weird inputs get reasonable defaults, not exceptions.
- `~/.claude/projects/-home-noah-Desktop-projects-flow-sharp/memory/feedback_language_philosophy.md` — informs the function-shape decision (no infix operators; S-expression style call `(humanizeGaussian seq 0.1 42)` is the canonical Flow style; positional args with seed-last per the euclidean precedent).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`HumanizeRng = new Random()` static field** (`TransformFunctions.cs:873`) — INTENTIONALLY NOT REUSED. The new `humanizeGaussian` constructs its own LOCAL `new Random(seed)` per call (D-03). The static field stays as-is for the existing 2-arg `humanize` (D-18).
- **`Math.Clamp(amount, 0.0, 1.0)` + `Math.Clamp(note.Velocity + velJitter, 0.05, 1.0)`** (`TransformFunctions.cs:878, 894`) — exact clamp pattern reused verbatim by `humanizeGaussian` per D-08 + D-09.
- **Sequence iteration loop** (`TransformFunctions.cs:881-901`) — `foreach (var bar in seq.Bars) { foreach (var note in bar.MusicalNotes) { ... } }` — the exact loop structure `humanizeGaussian` mirrors. Rest passthrough at lines 886-890 reused per D-11.
- **`MusicalNoteData` ctor** (`TransformFunctions.cs:896-898`) — exact field set reused verbatim. Note: ctor itself clamps velocity to `[0, 1]` per BuiltInFunctions.cs:1311 comment ("belt-and-braces"). Phase 25 keeps the explicit `Math.Clamp(.., 0.05, 1.0)` for the lower bound which the ctor's `[0, 1]` clamp does NOT enforce.
- **`new Random(seed)` LOCAL pattern** — see `VariationFunctions.cs:71-77` and `BuiltInFunctions.cs:1258`. The Phase 25 implementation literally copies these 1-2 lines into the new `HumanizeGaussian` method head.

### Established Patterns
- **`internal proc <name> (Type: param, ...)` in std.flow** — required `.flow`-side declaration per D-25. Without this, registry registration is invisible to user scripts. See `std.flow:136` (`humanize`) and `std.flow:154` (`euclidean` 6-arg) for the exact format.
- **Static `Random` for unseeded variants vs LOCAL `Random(seed)` for seeded variants** — codebase-wide split. Existing `Humanize` uses static (non-deterministic, by design); `VarySeeded`, `euclidean` 6-arg, and now `humanizeGaussian` use LOCAL.
- **Sibling pattern for paired functions** — when a function gets a "more advanced" variant (like uniform → Gaussian here), both live in the same file with adjacent registration calls. See: `Humanize` + `RegisterHumanize` block; coming `HumanizeGaussian` + `RegisterHumanizeGaussian` block immediately after.
- **xUnit Theory tolerance constants** — `private const double Tol = 1e-9;` style for floating-point velocity assertions (per Phase 15 EuclideanSwingTests.cs:38). Phase 25 Facts use the same constant for the deterministic-pin Fact.
- **Phase 18 byte-identical regression contract** — every PRNG-touching phase MUST extend the byte-identical integration tests. Phase 25 ships `ByteIdenticalShowcaseGaussianTests.cs` per D-21.

### Integration Points
- **`RegisterAll(registry)` at TransformFunctions.cs:28** — single addition: `RegisterHumanizeGaussian(registry);` immediately after `RegisterHumanize(registry);`.
- **No flow-lsp touch** — `humanizeGaussian` is a runtime transform, not a compile-time / LSP-time concern. Phase 17 LSP architecture (parse-time-only analysis) is unaffected.
- **No new external dependency** — Box-Muller is implemented in plain `Math.Sqrt` / `Math.Log` / `Math.Cos`. Per CLAUDE.md "Guiding Principle: Minimal Dependencies", this phase adds zero NuGet packages.

</code_context>

<specifics>
## Specific Ideas

- The basic Box-Muller's `u1` near-zero guard (`Math.Max(u1, 1e-300)` per D-05) is a defensive hedge against the 1-in-2^53 chance that .NET's `Random.NextDouble()` returns exactly 0.0 (which it cannot per its contract — it returns `[0, 1)`, meaning 0 IS possible). The `Math.Log(0)` divergence would produce `NaN` velocity, propagate through `Math.Clamp`, and silently produce a clamped-to-0.05 note that's musically wrong. The 1e-300 floor produces a `Math.Log(1e-300) ≈ -690.776` clamped Gaussian sample of about ±37 (37 stddevs), which `* amount * 0.2` is at most 7.4, then `Math.Clamp(velocity + 7.4, 0.05, 1.0)` clamps to 1.0. So even the worst-case guarded sample is benign.
- The recommended showcase.flow placement (D-20: wrap `melody` around line 22) preserves the current melodic shape while adding subtle bell-curve velocity variation — composers hear a more naturalistic "human pianist" feel than uniform humanize gives. The seed should be a fixed literal (e.g., 42 or 314) to keep the byte-identical contract.
- The tutorial.flow chapter (D-22) should explicitly contrast uniform vs Gaussian distributions with a 1-line code example each:
  ```
  Sequence uniformFeel  = humanize(myMelody, 0.1)              // flat — every velocity equally likely within ±0.02
  Sequence naturalFeel  = humanizeGaussian(myMelody, 0.1, 42)  // bell — most velocities near base, occasional larger excursions
  ```
- For the deterministic-pin Fact (D-23), the test fixture should use a SMALL fixed Sequence (e.g., 4 quarter notes at base velocity 0.63), call `humanizeGaussian(seq, 0.1, 42)`, then assert the first note's velocity equals a specific double computed once via running the test, then frozen as a literal in the Fact. This is the canonical "snapshot-test for deterministic PRNG" pattern; mirror `flow-lang.Tests/Unit/Phase15/EuclideanSwingTests.cs:83-87` exactly.

</specifics>

<deferred>
## Deferred Ideas

### Out of Phase 25 Scope
- **Other distributions** (Cauchy, Laplace, exponential, triangular) — REQ scope is Gaussian only. Future phase if composer feedback requests; would belong in a "humanize variants" enhancement phase or v1.4.
- **Per-axis humanize** (separate Gaussian for velocity vs timing vs duration) — current scope is velocity-only, matching existing `humanize`. Future phase could add `humanizeTimingGaussian(seq, amount, seed)` etc.
- **`humanize(Sequence, Double, Int)` 3-arg seeded uniform overload** — not requested; would compete with `humanizeGaussian` for the same problem space and complicate the v1.2 baseline preservation. Composers wanting a seeded uniform humanize today already get it via `euclidean`'s 6-arg form.
- **Marsaglia polar method or Ziggurat algorithm** — D-05 chose basic Box-Muller (cos/sin) for simplicity + determinism. Switching to Marsaglia would buy ~10% faster generation at the cost of rejection-sampling state complexity; not worth it at compile-time velocity-jitter scale. Ziggurat would be even faster but is overkill for this use case.
- **Cached second Box-Muller sample** (the sin companion) — D-06 explicitly discards. Caching would create order-dependence between consecutive notes that complicates the deterministic-by-seed contract.
- **Statistical normality tests in xUnit** (Shapiro-Wilk, Anderson-Darling) — overkill for a 1000-note sample size. D-23's mean+stddev tolerance check is sufficient sanity.
- **SIMD / vectorized Box-Muller** — premature optimization; humanize runs at compile time, not in the audio hot path. No profiling has shown this as a bottleneck.
- **Modifying `humanize(Sequence, Double)` to be deterministic** — would break v1.2 byte-identity by definition (different output for the same input). Out of scope per D-04 / D-18.
- **Adding `humanizeGaussian` to `flow-lsp`'s linting / hover / completion** — LSP architecture is parse-time-only; no semantic insight into runtime velocity perturbation is possible at parse time. Hover/completion for the new function naturally appears via std.flow's `internal proc` declaration (Phase 17 hover machinery picks it up).

### Reviewed Todos (not folded)

None — no todos surfaced for Phase 25 (`gsd-sdk query todo.match-phase 25` returned 0 matches).

</deferred>

---

*Phase: 25-gaussian-humanize-last-prng-phase*
*Context gathered: 2026-05-04 via /gsd-discuss-phase 25 --auto (single-pass auto mode)*
*Auto-mode log: All 10 gray areas auto-selected; recommended option chosen for each per modes/auto.md.*
