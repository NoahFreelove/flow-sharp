# Phase 25: Gaussian Humanize (LAST PRNG phase) - Research

**Researched:** 2026-05-04
**Domain:** Gaussian-distributed velocity perturbation (Box-Muller) inside an existing static-typed music DSL — under a binding v1.2 byte-identical determinism contract
**Confidence:** HIGH (every recommendation is grounded in a concrete codebase line; CONTEXT.md is unusually detailed, so research scope is "fill the Claude's-Discretion gaps + supply Validation Architecture + surface one concrete bug-shaped finding," not re-derive locked decisions)

## Summary

CONTEXT.md (25 locked D-IDs) has already settled all major design questions for `humanizeGaussian(Sequence, Double, Int)`: signature, PRNG strategy (LOCAL `new Random(seed)`), Box-Muller variant (basic cos/sin, sin discarded), velocity scaling (`z * amount * 0.2`), clamps, rest passthrough, file location, std.flow declaration, test coverage shape, and the showcase additive call site. Phase 25 is the LAST PRNG-touching phase per `.planning/REQUIREMENTS.md` pre-ordering #5 and `.planning/research/PITFALLS.md:166` Pitfall 6.

Research surfaces five things the planner needs that CONTEXT.md leaves open or under-specified:

1. **One concrete bug-shaped finding the planner must NOT repeat.** The existing `Humanize` method at `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs:896-898` rebuilds notes via the OLD positional `MusicalNoteData` ctor that drops the new defaulted fields added in Phases 18 + 22 (`DurationFraction`, `OnsetOffset`, `DurationOverlap`, `PortamentoMs`, `IsChordTone`). It is FROZEN per D-18 so we can't fix it, but `humanizeGaussian` MUST avoid that pattern — use `note.With(...)` plus a velocity-aware override (extend the `With` helper at `NoteType.cs:317` with a `velocity` slot), or at minimum copy ALL fields including the new ones via the full ctor at `NoteType.cs:284`. See "Critical Pre-Existing Bug" below.
2. **Recommendations on the four Claude's-Discretion items** (helper extraction, guard idiom, theory matrix, base velocity).
3. **Validation Architecture** — exact xUnit Facts, file paths, run commands, Wave 0 gaps.
4. **Showcase placement confirmation** — `melody` Sequence at `examples/showcase.flow:20` is the right target; preserves the v1.2 baseline because melody is currently velocity-static.
5. **Determinism guard logic** — `Math.Max(u1, 1e-300)` is preferred over `if (u1 == 0.0)` because .NET's `Random.NextDouble()` can return exactly 0 (per its `[0, 1)` contract) and the `Math.Log(0) = -∞ → NaN` propagation through `Math.Clamp` would silently produce a clamped-to-0.05 ghost note.

**Primary recommendation:** Extract `private static double NextGaussianSample(Random rng)` at the bottom of `TransformFunctions`, use `Math.Max(u1, 1e-300)` (matches the math-utility idiom and is the universal Box-Muller textbook guard), and rebuild perturbed notes by calling the full `MusicalNoteData` ctor with EVERY field from the source note (do NOT mirror the existing `Humanize` field-list — that one is buggy/stale).

## User Constraints (from CONTEXT.md)

### Locked Decisions (D-01 through D-25)

Verbatim copy from `25-CONTEXT.md` `<decisions>` section. The planner MUST honor every D-ID; this research does not re-derive them.

**Function Signature (DA-1):**
- **D-01:** Signature is `humanizeGaussian(Sequence, Double, Int)` with parameter order `(seq, amount, seed)`. Single overload — no Sequence-only or Sequence+Double overloads.
- **D-02:** Function name is `humanizeGaussian` (camelCase) — locked by REQ DEFER-06 + PROJECT.md D-04.

**PRNG Strategy (DA-2):**
- **D-03:** Use LOCAL `new Random(seed)` per call. Mirrors `VariationFunctions.VarySeeded:71-77` and `BuiltInFunctions.cs:1258` (euclidean 6-arg D-17).
- **D-04:** Per-note PRNG advances internally — same `Random` instance consumed by all notes in iteration order over `Sequence.Bars` and `Bar.MusicalNotes`.

**Box-Muller Variant (DA-3):**
- **D-05:** BASIC Box-Muller (cos/sin form), not Marsaglia. Two `NextDouble` calls produce one Gaussian sample; `u1` guarded via `Math.Max(u1, 1e-300)`.
- **D-06:** sin companion DISCARDED — each non-rest note consumes a fresh (u1, u2) pair.

**Velocity Scaling + Clamping (DA-4):**
- **D-07:** `velJitter = z * amount * 0.2` matching existing humanize jitter scale.
- **D-08:** `amount` clamped to `[0.0, 1.0]` via `Math.Clamp` — silent clamp.
- **D-09:** Velocity clamped to `[0.05, 1.0]` via `Math.Clamp(note.Velocity + velJitter, 0.05, 1.0)`.
- **D-10:** Short-circuit when `amount == 0.0` post-clamp — return input unchanged.

**Rest + Edge Handling (DA-5):**
- **D-11:** Rests pass through unchanged. No PRNG consumption for rests.
- **D-12:** Sequences with zero non-rest notes return unchanged.
- **D-13:** Empty sequences return unchanged.
- **D-14:** Negative `amount` clamped to 0 via D-08.
- **D-15:** `seed` is `Int` — matches `Random(int)` ctor.

**File Location + Registration (DA-6):**
- **D-16:** Code lands in `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` adjacent to existing `// ===== Humanize =====` section at line 864. New `// ===== Humanize Gaussian =====` block; `RegisterHumanizeGaussian(registry)` called from `RegisterAll(registry)` at line 28 immediately after `RegisterHumanize(registry)`.
- **D-17:** Inline implementation OR `private static double NextGaussianSample(Random rng)` helper — planner decides. (Research recommends extract — see "Claude's Discretion Resolutions" below.)

**Existing `humanize` Invariance (DA-7):**
- **D-18:** Existing `humanize(Sequence, Double)` at `TransformFunctions.cs:866-903` is FROZEN. NOT modified, NOT renamed, NOT deprecated, NOT overloaded.
- **D-19:** Phase 18 byte-identical regression tests (`flow-lang.Tests/Integration/Phase18/ByteIdenticalTutorialTests.cs` + `ByteIdenticalShowcaseTests.cs`) MUST stay GREEN.

**Showcase + Tutorial Updates (DA-8):**
- **D-20:** ONE additive `humanizeGaussian(seq, amount, seed)` call site in `examples/showcase.flow`. Recommended placement: wrap `melody` Sequence (research confirms — see Showcase Confirmation below).
- **D-21:** Pre-emptively run two consecutive `dotnet run --project flow-interpreter examples/showcase.flow` invocations and `cmp` the outputs. Pin via `flow-lang.Tests/Integration/Phase25/ByteIdenticalShowcaseGaussianTests.cs`.
- **D-22:** Add `humanizeGaussian` chapter to `examples/tutorial.flow` after the existing `humanize` chapter (around line 567).

**Test Coverage (DA-9):**
- **D-23:** Seven required Facts in `flow-lang.Tests/Unit/Phase25/HumanizeGaussianFacts.cs`. (See Validation Architecture for the exact list.)
- **D-24:** Integration test in `flow-lang.Tests/Integration/Phase25/ByteIdenticalShowcaseGaussianTests.cs` mirroring `Phase18/ByteIdenticalShowcaseTests.cs`.

**Std.flow Public Declaration (DA-10):**
- **D-25:** Add `internal proc humanizeGaussian (Sequence: seq, Double: amount, Int: seed)` to `flow-lang/std.flow` immediately after `internal proc humanize` at line 136.

### Claude's Discretion (this research RESOLVES — see resolutions below)
- Extract Box-Muller helper vs inline (recommendation: extract)
- `Math.Max(u1, 1e-300)` vs `if (u1 == 0.0) u1 = 1e-300` (recommendation: `Math.Max`)
- Theory matrix size for cross-seed Facts (recommendation: 1 deterministic-pin Fact for seed=42 + 1 cross-seed-difference Fact for seed=42 vs 43)
- Exact base velocity in test fixtures (recommendation: 0.63)
- showcase.flow target Sequence (recommendation CONFIRMED: `melody`)

### Deferred Ideas (OUT OF SCOPE)
Verbatim copy from `25-CONTEXT.md` `<deferred>` section — Cauchy/Laplace distributions, per-axis humanize (timing/duration), seeded uniform overload, Marsaglia/Ziggurat, cached sin companion, statistical normality tests (Shapiro-Wilk), SIMD, modifying existing `humanize`, LSP integration, all reviewed-not-folded todos. Planner MUST NOT pull any of these in.

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| DEFER-06 | `humanizeGaussian(seq, amount, seed)` Box-Muller. Same seed → deterministic velocity bytes pinned by Fact. Existing uniform `humanize` UNCHANGED — v1.2 byte-identical contract preserved. Two `showcase.flow` runs cmp-clean. | Standard Stack (no new deps), Architecture Pattern: "Sibling LOCAL-Random Transform", Test patterns at `Phase15/EuclideanSwingTests.cs:32-87` (deterministic-pin) + `Phase18/ByteIdenticalShowcaseTests.cs:32-89` (byte-identical two-runner). Validation Architecture lists all 7 Facts + 2 integration tests. |

## Project Constraints (from CLAUDE.md)

The planner MUST honor these — they have the same authority as locked D-IDs:

- **Runtime: .NET 10, C# 13** — `dotnet build` and `dotnet run --project flow-interpreter <script>.flow` are the canonical commands. `[VERIFIED: CLAUDE.md]`
- **No new external NuGet dependencies** — Box-Muller is pure `Math.Sqrt / Math.Log / Math.Cos`. The "Guiding Principle: Minimal Dependencies" line in CLAUDE.md technology stack section explicitly bans adding packages for features that can be hand-rolled. `[VERIFIED: CLAUDE.md "Libraries Explicitly NOT Recommended"]`
- **File-scoped namespaces, AST records, switch-expression dispatch** — match existing `TransformFunctions` style (file-scoped namespace at `TransformFunctions.cs:6`). `[VERIFIED]`
- **`Random.Shared` is FORBIDDEN for any byte-identity-critical PRNG draw** — Pitfall 6 at `.planning/research/PITFALLS.md:172`: *"Determinism contracts only hold if every PRNG in the chain is seeded."* Use `new Random(seed)` LOCAL only. `[VERIFIED: PITFALLS.md:166-195]`
- **Charitable interpretation memory** — silent-and-documented over errors. Negative amount silently clamps to 0; weird inputs get reasonable defaults, not exceptions. `[VERIFIED: ~/.claude/projects/.../feedback_charitable_interpretation.md]`
- **Functional S-expression style** — calls are `(humanizeGaussian seq 0.1 42)`, not `seq.humanizeGaussian(0.1, 42)`. The std.flow declaration at D-25 is positional. `[VERIFIED: feedback_language_philosophy.md]`
- **GSD workflow enforcement** — direct repo edits outside a GSD command are prohibited. `[VERIFIED: CLAUDE.md "GSD Workflow Enforcement"]`

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| `humanizeGaussian` registration | C# Built-in (`StandardLibrary/Transforms/`) | — | Sibling pattern with existing `Humanize`; transforms live under `StandardLibrary/Transforms/`, registered via `InternalFunctionRegistry`. No interpreter / parser / lexer touch. |
| `.flow`-side type signature | Standard library `.flow` module (`flow-lang/std.flow`) | — | D-25 — the `internal proc` declaration in std.flow is what makes the registered C# function callable from user scripts. Without this declaration, registration is invisible. |
| Box-Muller math | C# helper (`TransformFunctions.NextGaussianSample`) | — | Self-contained; consumes only `Random.NextDouble()`. No interaction with `MusicalContext`, `ExecutionContext.GetRand`, or any other tier. |
| Velocity perturbation | Compile-time transform (runs once when `humanizeGaussian(...)` is evaluated) | — | NOT in the audio hot path. The transform produces a new `SequenceData`; subsequent `renderSong` reads the perturbed velocities. No audio-renderer touch. |
| Determinism contract | Integration tests (`Tests/Integration/Phase25/`) + showcase regression test | Existing Phase 18 byte-identical tests (must remain green) | Two-runner cmp pattern at `Phase18/ByteIdenticalShowcaseTests.cs:32-89` is canonical. |
| Tutorial chapter | `examples/tutorial.flow` chapter section | — | Append after existing humanize chapter at `:567` per D-22. |

**Why this matters:** No flow-lsp, parser, lexer, AST, or interpreter changes. The phase is a localized addition to two files (`TransformFunctions.cs`, `std.flow`) plus tests + example updates. Anything else in a plan is a smell.

## Standard Stack

### Core (existing — no changes)
| Library / Component | Version | Purpose | Why Standard |
|---------------------|---------|---------|--------------|
| .NET 10 BCL | net10.0 | `System.Random`, `System.Math` | Phase uses only `Random.NextDouble`, `Math.Sqrt/Log/Cos/PI/Clamp/Max`. All in BCL. `[VERIFIED: existing TransformFunctions.cs imports]` |
| `FlowLang.Runtime.MusicalNoteData` | (in-repo) | Note record with `Velocity` field | Existing data structure used by every transform. Perturbation produces new `MusicalNoteData` instances via the full ctor (NOT the buggy positional list at `Humanize:896-898`). `[VERIFIED: NoteType.cs:284]` |
| `FlowLang.Runtime.SequenceData` / `BarData` | (in-repo) | Container for transformed bars | Existing — `Humanize` already iterates `seq.Bars` → `bar.MusicalNotes`. New code mirrors. `[VERIFIED: TransformFunctions.cs:881-901]` |
| `FlowLang.StandardLibrary.InternalFunctionRegistry` | (in-repo) | Function registration | Existing dispatch — `registry.Register(name, signature, lambda)`. `[VERIFIED: TransformFunctions.cs:870, 49-60]` |
| xUnit + `Xunit.Sdk` | (existing) | Test framework | Existing tests use `[Fact]` and `[Theory]` + `[InlineData]`. `[VERIFIED: Phase15/EuclideanSwingTests.cs:1-3]` |

### Supporting (no new packages)
None. Per CLAUDE.md "Guiding Principle: Minimal Dependencies", Box-Muller is implementable in ~6 lines of pure C# math. `[VERIFIED: CLAUDE.md technology stack section]`

### Alternatives Considered
| Instead of | Could Use | Tradeoff | Decision |
|------------|-----------|----------|----------|
| Basic Box-Muller (cos/sin) | Marsaglia polar method | Marsaglia is ~10% faster but uses rejection sampling — variable PRNG draw count per sample. **VIOLATES Pitfall 6** ("draw count must be fixed"). | Locked to basic Box-Muller per D-05. |
| Basic Box-Muller | Ziggurat algorithm | Ziggurat is ~3x faster but requires precomputed tables and is non-trivial to verify. Overkill for compile-time velocity jitter. | Locked to basic Box-Muller per D-05. |
| Hand-rolled `MathUtils.NextGaussian` | Third-party stats library (e.g., MathNet.Numerics) | Adds heavy NuGet dep for 6 lines of math. Violates "Minimal Dependencies" principle. | Hand-roll — locked by CLAUDE.md. |
| Cache sin companion (paired draws) | Discard sin (D-06) | Caching wastes 50% fewer normals BUT makes determinism sensitive to rest-density (a single rest changes whether subsequent notes consume fresh or cached samples). | Locked discard per D-06. |

**Installation:** None — no `dotnet add package` step. `[VERIFIED]`

**Version verification:** N/A — zero new external packages.

## Critical Pre-Existing Bug (planner MUST NOT repeat)

The existing `Humanize` method at `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs:896-898` was authored before Phases 18 and 22 added new defaulted fields to `MusicalNoteData`:

```csharp
// TransformFunctions.cs:896-898 — BUGGY (FROZEN per D-18, do NOT fix):
newNotes.Add(new MusicalNoteData(note.NoteName, note.Octave, note.Alteration,
    note.DurationValue, note.IsRest, note.CentOffset, note.IsTied,
    newVelocity, note.Articulation, note.IsDotted, note.SourceLocation, note.SourceLength));
```

The `MusicalNoteData` ctor at `flow-lang/TypeSystem/SpecialTypes/NoteType.cs:284` accepts SEVENTEEN parameters:

```csharp
public MusicalNoteData(char noteName, int octave, int alteration, int? durationValue,
    bool isRest, double? centOffset = null, bool isTied = false, double velocity = 0.63,
    Articulation articulation = Articulation.Normal, bool isDotted = false,
    FlowLang.Core.SourceLocation? sourceLocation = null, int sourceLength = 0,
    FlowLang.TypeSystem.Fraction? durationFraction = null,  // ← Phase 18 (FRAC-02)
    double onsetOffset = 0.0,                                // ← Phase 22 (DX-13)
    double durationOverlap = 0.0,                            // ← Phase 22 (DX-14 legato)
    double portamentoMs = 0.0,                               // ← Phase 22 (DX-14 portamento)
    bool isChordTone = false)                                // ← Phase 19 (chord literals)
```

`Humanize:896-898` passes only 12 arguments. The remaining FIVE fields silently fall through to defaults — meaning a sequence with tuplet durations (`DurationFraction != null`), quantize onsets (`OnsetOffset != 0`), legato (`DurationOverlap != 0`), portamento (`PortamentoMs != 0`), or chord-tone polyphony (`IsChordTone == true`) **gets these fields silently zeroed when humanized**. This is a real, latent bug. It is FROZEN per D-18 (don't touch existing `humanize` to preserve byte-identity).

**`humanizeGaussian` MUST NOT repeat this pattern.** Two acceptable approaches:

**(A) Use the `With(...)` helper at `NoteType.cs:317`** — extend it with a `velocity` slot. The helper currently accepts `onsetOffset`, `durationOverlap`, `portamentoMs`. Adding `double? velocity = null` follows the same null-coalesce pattern the helper already uses. Then `humanizeGaussian` calls `note.With(velocity: newVelocity)`. **RECOMMENDED** — it preserves rollback-independence (Phase 22 CONTEXT line 18 pattern) and avoids enumerating fields the transform doesn't own.

**(B) Use the full ctor with EVERY field passed through.** Requires 17-argument call site mentioning every field by name. Brittle when future phases add fields.

Recommend (A). The `With(...)` helper extension is one new optional parameter + one new line in the body — sub-5-line change. The pattern matches Phase 22's "transforms call `With(...)` naming ONLY the field they own" convention documented at `TransformFunctions.cs:39-47`.

`[VERIFIED: NoteType.cs:284-330, TransformFunctions.cs:39-47, TransformFunctions.cs:896-898]`

## Architecture Patterns

### System Architecture Diagram

```
.flow source: (humanizeGaussian melody 0.1 42)
        │
        │  (parse-time)
        ▼
FunctionCallExpression { name = "humanizeGaussian", args = [VarRef("melody"), Literal(0.1), Literal(42)] }
        │
        │  (evaluation in Interpreter)
        ▼
ExpressionEvaluator → Args evaluated to Values [Sequence, Double, Int]
        │
        │  (overload resolution via FunctionSignature)
        ▼
InternalFunctionRegistry["humanizeGaussian"](args)
        │
        │  (THIS PHASE)
        ▼
TransformFunctions.HumanizeGaussian(args) [in TransformFunctions.cs, ~line 905+]
        │
        │  ┌─ amount = Math.Clamp(args[1], 0.0, 1.0)            (D-08)
        │  ├─ if (amount == 0.0) return Value.Sequence(seq)     (D-10 short-circuit)
        │  ├─ seed = args[2].As<int>()                          (D-15)
        │  ├─ var rng = new Random(seed)                        (D-03 LOCAL)
        │  └─ foreach bar.MusicalNotes:
        │       if note.IsRest → passthrough                    (D-11)
        │       else:
        │         z = NextGaussianSample(rng)                   (D-05)
        │         velJitter = z * amount * 0.2                  (D-07)
        │         newVel = Math.Clamp(note.Velocity + velJitter, 0.05, 1.0)  (D-09)
        │         newNote = note.With(velocity: newVel)         (recommended: extend With helper)
        │
        ▼
Value.Sequence(perturbedSequence)
```

### Recommended Project Structure (delta only)

```
flow-lang/
├── StandardLibrary/Transforms/
│   └── TransformFunctions.cs              ← MODIFIED: +RegisterHumanizeGaussian, +HumanizeGaussian, +NextGaussianSample
├── TypeSystem/SpecialTypes/
│   └── NoteType.cs                        ← MODIFIED: extend With(...) with `velocity` slot
└── std.flow                               ← MODIFIED: +internal proc humanizeGaussian
flow-lang.Tests/
├── Unit/Phase25/
│   └── HumanizeGaussianFacts.cs           ← NEW: 7 Facts per D-23
└── Integration/Phase25/
    └── ByteIdenticalShowcaseGaussianTests.cs  ← NEW: 2 Facts (WAV + MIDI two-runner) per D-24
examples/
├── showcase.flow                          ← MODIFIED: ONE additive humanizeGaussian on melody (line ~20)
└── tutorial.flow                          ← MODIFIED: +chapter after :567 humanize chapter
tests/
└── test_humanize_gaussian.flow            ← NEW: smoke-test sentinel for FlowScriptData two-pass infrastructure
```

### Pattern 1: Sibling LOCAL-Random Transform
**What:** A transform that mirrors a non-deterministic uniform sibling but uses LOCAL `new Random(seed)` per call, isolating it from `ExecutionContext.GetRand` and any global PRNG state.
**When to use:** Any new transform that needs reproducible per-call randomness while a non-deterministic sibling exists.
**Example:**
```csharp
// Source: flow-lang/StandardLibrary/Composition/VariationFunctions.cs:71-77
private static Value VarySeeded(IReadOnlyList<Value> args)
{
    var seq = args[0].As<SequenceData>();
    double probability = args[1].As<double>();
    int seed = args[2].As<int>();
    return Value.Sequence(ApplyVariation(seq, probability, null, new Random(seed), null));
}
```
And the prescribed comment style:
```csharp
// Source: flow-lang/StandardLibrary/BuiltInFunctions.cs:1256-1258
// D-17: LOCAL new Random(seed) scoped to THIS call; does NOT read or mutate
// ExecutionContext.GetRand. Mirrors VariationFunctions.VarySeeded at :71-77.
var rng = new Random(seed);
```
Phase 25 mirrors verbatim with a parallel comment citing both VariationFunctions:71-77 AND BuiltInFunctions:1258 as precedent.

### Pattern 2: Box-Muller (Basic, cos branch only)
**What:** Standard Box-Muller transform producing one `N(0, 1)` sample per call. Two `NextDouble` draws → one Gaussian sample.
**When to use:** When you need Gaussian-distributed pseudo-random samples and the determinism contract requires a fixed draw count per output.
**Example (recommended helper):**
```csharp
// Source: standard textbook formulation; mirrors widely-cited patterns
// (e.g., Wikipedia Box–Muller transform, Numerical Recipes §7.3.4)
// [CITED: en.wikipedia.org/wiki/Box-Muller_transform — basic form]
//
// AUDIT-VERIFIED 2026-05-04: Gaussian humanize uses basic Box-Muller (2 uniform
// draws per sample, no rejection); local Random(seed) per call; bytes pinned by
// flow-lang.Tests/Integration/Phase25/ByteIdenticalShowcaseGaussianTests.cs.
private static double NextGaussianSample(Random rng)
{
    double u1 = rng.NextDouble();
    double u2 = rng.NextDouble();
    // Guard against u1 == 0.0 (legal per Random.NextDouble [0, 1) contract).
    // Math.Log(0) = -infinity → NaN propagation through subsequent Math.Clamp
    // would silently produce a clamped-to-0.05 ghost note. The 1e-300 floor is
    // ~37 stddevs out — clamped at the velocity boundary, no audible artifact.
    u1 = Math.Max(u1, 1e-300);
    return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
}
```

### Pattern 3: Two-Runner Byte-Identical Integration Test
**What:** Run a `.flow` script twice through fresh `FlowEngineRunner` instances with output paths substituted to distinct files; assert `bytes1.SequenceEqual(bytes2)`.
**When to use:** Every PRNG-touching phase that the v1.2 byte-identical contract covers.
**Example:** See `flow-lang.Tests/Integration/Phase18/ByteIdenticalShowcaseTests.cs:32-89`. Mirror verbatim, only changing the test class name, output filenames (`phase25_showcase_run1.{wav,mid}`), and namespace.

### Anti-Patterns to Avoid
- **`Random.Shared.NextDouble()` for the second uniform draw.** Breaks Pitfall 6 by skipping the seed. `[VERIFIED: PITFALLS.md:172]`
- **Caching the sin companion to halve PRNG draws.** Creates rest-density-sensitive determinism (D-06 forbids).
- **Reflecting velocity overflow (`if v > 1: v = 2 - v`) instead of clamping.** Existing humanize clamps; keep parity (D-09).
- **Modifying the existing `Humanize` method.** D-18 + D-19 forbid — would break v1.2 byte-identity.
- **Repeating the `Humanize:896-898` 12-arg ctor pattern.** See "Critical Pre-Existing Bug" — use `With(...)` helper extension instead.
- **Adding `humanizeGaussian` as a 3-arg overload of `humanize`.** D-04 + CONTEXT.md `<deferred>` forbid — must be a separate function name.
- **Per-note independent seeding** (e.g., `seed_for_note_i = base_seed + i`). CONTEXT `<domain>` line 29 explicitly excludes — overcomplicated, single LOCAL `Random(seed)` advancing internally is sufficient.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Gaussian distribution | Custom rejection-sampling Marsaglia | Basic Box-Muller (D-05) | Variable draw count breaks determinism |
| PRNG | Custom LCG / Xorshift | `System.Random(seed)` | BCL implementation has stable per-`int`-seed sequence; matches euclidean precedent |
| Numeric clamping | Manual `if (v < lo) v = lo;` | `Math.Clamp(v, lo, hi)` | Matches existing humanize style at `TransformFunctions.cs:878,894` |
| Note rebuild | Full 17-arg ctor enumeration in transform | `note.With(velocity: newVel)` extended helper | Avoids the bug at `Humanize:896-898` and matches Phase 22 convention `[VERIFIED: TransformFunctions.cs:39-47]` |

**Key insight:** The phase is a sub-100-line addition. Every function it needs exists. The danger is reaching for "improvements" — caching, vectorization, alternative distributions — that violate locked decisions or break the determinism contract.

## Common Pitfalls

### Pitfall 1: Determinism contract broken by extra unseeded PRNG draw
**What goes wrong:** A draw from `Random.Shared` slips into the implementation (e.g., during a "guard against u1==0" branch), making the Gaussian output environment-dependent.
**Why it happens:** Quick fix temptation: `if (u1 == 0) u1 = Random.Shared.NextDouble();` looks innocent.
**How to avoid:** Use `Math.Max(u1, 1e-300)` — pure deterministic clamp, no PRNG draw. Code review checklist: `grep -n "Random\." flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` should produce ZERO matches outside `new Random(seed)` and `rng.NextDouble()`.
**Warning signs:** `cmp examples/output/showcase_run1.wav examples/output/showcase_run2.wav` returns non-empty diff after the phase ships.
`[VERIFIED: PITFALLS.md:166-195 Pitfall 6]`

### Pitfall 2: NaN propagation through Math.Clamp on log(0)
**What goes wrong:** .NET `Random.NextDouble()` documents return range `[0, 1)`, meaning 0.0 IS possible (1-in-2^53 probability). `Math.Log(0) = -∞`, `Math.Sqrt(-2 * -∞) = +∞`, `+∞ * cos(2πu2)` is `±∞` or `NaN` if u2 hits the exact zero of cosine, and `Math.Clamp(NaN, 0.05, 1.0)` returns NaN — which silently encodes to MIDI velocity 0 (ghost note) or produces audio NaN clicks.
**Why it happens:** Box-Muller textbook form omits the guard.
**How to avoid:** `u1 = Math.Max(u1, 1e-300)` — produces a worst-case Gaussian sample of ±37 stddevs, clamped to velocity boundary. Always benign.
**Warning signs:** Smoke test for NaN: a stress Fact runs `humanizeGaussian` over a 10000-note sequence with ~1000 different seeds; assertion checks `!double.IsNaN(note.Velocity) && !double.IsInfinity(note.Velocity)` for every note. (Optional — D-23 doesn't require this Fact, but it's cheap insurance.)
`[VERIFIED: Microsoft .NET docs Random.NextDouble — returns [0, 1); CITED: standard Box-Muller pitfall]`

### Pitfall 3: Showcase byte-identity fails because the new call site changes downstream PRNG state
**What goes wrong:** showcase.flow uses `(euclidean 5 16 A2 0.18 0.12 7)` at `examples/showcase.flow:17`. Adding `humanizeGaussian` to a DIFFERENT Sequence (`melody`) should NOT affect euclidean output — they use independent LOCAL `Random(seed)` instances per D-03. But if humanizeGaussian were ever to use `ExecutionContext.GetRand` instead, it would couple to euclidean.
**Why it happens:** Copy-paste from a non-seeded transform.
**How to avoid:** The mandatory comment block at the top of `HumanizeGaussian` cites D-03 + VariationFunctions:71-77 + BuiltInFunctions:1258 verbatim. Code review checks the comment is present BEFORE the `var rng = new Random(seed);` line.
**Warning signs:** ByteIdenticalShowcaseGaussianTests fails on the WAV byte but passes the MIDI byte (or vice versa) — indicates partial coupling.
`[VERIFIED: TransformFunctions.cs existing patterns; PITFALLS.md:172]`

### Pitfall 4: Test fixture reads `MusicalContext.Velocity` and gets unexpected default
**What goes wrong:** A test builds a Sequence via the .flow language path (using a `tempo X { timesig 4/4 { ... } }` block) and assumes base velocity is 0.63. But if the `MusicalContext.Velocity` is set elsewhere (e.g., a `mp`/`mf` dynamic in the test fixture), the actual base velocity differs.
**Why it happens:** `BuiltInFunctions.cs:1280` shows euclidean reads `context.GetMusicalContext().Velocity ?? 0.63`. A test fixture that uses dynamics in the Sequence literal produces non-0.63 baseline velocities.
**How to avoid:** Build test fixtures via direct `MusicalNoteData` construction in C# (NOT via parsing .flow source) when the deterministic-pin Fact requires exact baseline. See `Phase15/EuclideanSwingTests.cs:32-86` for the canonical pattern (assert `Tol = 1e-9`, baseline `0.63` constant).
**Warning signs:** Pin Fact passes locally but fails after a tutorial.flow rewrite that reorders dynamic markers.
`[VERIFIED: BuiltInFunctions.cs:1280; Phase15/EuclideanSwingTests.cs:32-87]`

### Pitfall 5: `note.With(...)` overload doesn't accept `velocity` yet
**What goes wrong:** Planner discovers the existing `With(...)` helper at `NoteType.cs:317-330` accepts only `onsetOffset / durationOverlap / portamentoMs` — no `velocity` slot. Plan task says "use `note.With(velocity: newVel)`" but the call doesn't compile.
**Why it happens:** The helper was authored in Phase 22 for fields THAT phase introduced; velocity wasn't a use case.
**How to avoid:** First plan task in Phase 25 extends the `With(...)` helper with `double? velocity = null` (one new parameter + one new line in the body). Document the extension as a Phase 22 Pattern continuation. Alternatively, a SECOND task can add a velocity-aware `With` overload `WithVelocity(double v)` if extending the existing signature is judged risky. Either is fine — the planner picks.
**Warning signs:** Task fails compile with "no overload accepts named parameter 'velocity'".
`[VERIFIED: NoteType.cs:317-330]`

## Showcase Confirmation (Claude's Discretion #5)

D-20 recommended wrapping `melody` Sequence at `examples/showcase.flow:20`. Confirmed by reading the file:

```flow
// examples/showcase.flow:13-20 [VERIFIED]
Sequence padBase = | A3w | F3w | D3w | E3w |
Sequence pad = padBase -> crescendo 0.18 0.6
Sequence pulse = (euclidean 5 16 A2 0.18 0.12 7)   // already PRNG-touching, fixed seed
Sequence melody = | mp _ _ E5q G5q | A5h E5h | _ _ G5q B5q | A5w |  // ← TARGET
```

`melody` is currently velocity-static (only `mp` dynamic). Wrapping it as `Sequence melody = (humanizeGaussian (| mp _ _ E5q G5q | A5h E5h | _ _ G5q B5q | A5w |) 0.08 314)` adds bell-curve velocity perturbation while preserving every other property of the showcase output. Seed `314` is recommended (a fixed literal that's not 42 — preserves "different seed" sanity while staying memorable). Amount `0.08` is conservative — enough to be musically present, gentle enough to fit the ambient texture.

The other Sequences are LESS appropriate:
- `pad` already passes through `crescendo` — adding humanizeGaussian would introduce new state coupling.
- `pulse` already uses seeded euclidean humanize at `(... 0.12 7)` — adding humanizeGaussian on top is double-perturbation, musically muddy.
- `padBase` is referenced by `pad` — wrapping it propagates indirectly to a transformed Sequence, harder to reason about.

`melody` is the right target. `[VERIFIED: examples/showcase.flow:1-45]`

## Claude's Discretion Resolutions

| # | Question (CONTEXT.md `<decisions>` Discretion section) | Recommendation | Rationale |
|---|------|----------------|-----------|
| 1 | Inline Box-Muller vs extract `private static double NextGaussianSample(Random rng)` | **Extract** | Three benefits: (a) reusable by any future Gaussian-needing transform without copy-paste, (b) the 6-line helper is independently unit-testable (cross-seed determinism, no-NaN stress), (c) keeps the `HumanizeGaussian` method body at ~25 lines — easy to review. Cost: zero — the helper compiles to inline anyway under release JIT. |
| 2 | `Math.Max(u1, 1e-300)` vs `if (u1 == 0.0) u1 = 1e-300` | **`Math.Max`** | Branchless, single line, matches the textbook idiom. The `if` form is functionally equivalent but reads as "guarding against an unlikely case" rather than "establishing an invariant." `Math.Max` matches existing TransformFunctions style at line 1312 (`v = Math.Max(0.0, Math.Min(1.0, v));`). `[VERIFIED: BuiltInFunctions.cs:1312]` |
| 3 | Theory matrix for cross-seed Facts (1 seed vs 3 in `[InlineData]`) | **1 deterministic-pin Fact (seed=42) + 1 cross-seed-difference Fact (42 vs 43)** | The deterministic contract is "same seed → same output," not "every seed produces a unique output." A 3-seed Theory adds maintenance cost (three pin values to update if `Random` algorithm shifts in a future .NET patch) without adding semantic coverage. Two Facts cover both directions. |
| 4 | Exact base velocity in test fixtures | **0.63** | Matches `BuildEuclideanSequence` baseline at `BuiltInFunctions.cs:1280` ("Base velocity: MusicalContext.Velocity ?? 0.63"). Matches `Phase15/EuclideanSwingTests.cs:32` (`private const double BaseVelocity = 0.63;`). De-facto codebase convention. `[VERIFIED]` |
| 5 | showcase.flow target Sequence | **`melody` at line 20** | See "Showcase Confirmation" above. Confirmed by reading the file. `[VERIFIED]` |

## Code Examples

Verified patterns from official sources (paths into THIS repo):

### Sibling registration in RegisterAll
```csharp
// Source: flow-lang/StandardLibrary/Transforms/TransformFunctions.cs:17-30 [VERIFIED]
public static void Register(InternalFunctionRegistry registry)
{
    RegisterTranspose(registry);
    RegisterInvert(registry);
    RegisterRetrograde(registry);
    RegisterAugmentDiminish(registry);
    RegisterOctaveShift(registry);
    RegisterRepeat(registry);
    RegisterConcat(registry);
    RegisterDynamicTransforms(registry);
    RegisterTempoTransforms(registry);
    RegisterHumanize(registry);
    // PHASE 25: insert here →   RegisterHumanizeGaussian(registry);
    RegisterOrnamentTransforms(registry);
}
```

### Existing humanize sibling (FROZEN — sole reference)
```csharp
// Source: flow-lang/StandardLibrary/Transforms/TransformFunctions.cs:864-903 [VERIFIED]
// ===== Humanize =====

private static void RegisterHumanize(InternalFunctionRegistry registry)
{
    var humanizeSig = new FunctionSignature("humanize",
        [SequenceType.Instance, DoubleType.Instance]);
    registry.Register("humanize", humanizeSig, Humanize);
}

private static readonly Random HumanizeRng = new();

private static Value Humanize(IReadOnlyList<Value> args)
{
    var seq = args[0].As<SequenceData>();
    double amount = Math.Clamp(args[1].As<double>(), 0.0, 1.0);

    var result = new SequenceData();
    foreach (var bar in seq.Bars)
    {
        var newNotes = new List<MusicalNoteData>();
        foreach (var note in bar.MusicalNotes)
        {
            if (note.IsRest) { newNotes.Add(note); continue; }
            double velJitter = (HumanizeRng.NextDouble() * 2.0 - 1.0) * amount * 0.2;
            double newVelocity = Math.Clamp(note.Velocity + velJitter, 0.05, 1.0);

            // ⚠ NOTE: this 12-arg form drops 5 fields added in Phases 18 & 22.
            // FROZEN per D-18 — do NOT fix; humanizeGaussian must use With(...) instead.
            newNotes.Add(new MusicalNoteData(note.NoteName, note.Octave, note.Alteration,
                note.DurationValue, note.IsRest, note.CentOffset, note.IsTied,
                newVelocity, note.Articulation, note.IsDotted, note.SourceLocation, note.SourceLength));
        }
        result.AddBar(new BarData(newNotes, bar.TimeSignature!));
    }
    return Value.Sequence(result);
}
```

### Recommended HumanizeGaussian skeleton
```csharp
// PHASE 25: lands at flow-lang/StandardLibrary/Transforms/TransformFunctions.cs ~line 905
// ===== Humanize Gaussian =====

private static void RegisterHumanizeGaussian(InternalFunctionRegistry registry)
{
    var sig = new FunctionSignature("humanizeGaussian",
        [SequenceType.Instance, DoubleType.Instance, IntType.Instance]);
    registry.Register("humanizeGaussian", sig, HumanizeGaussian);
}

// CONTEXT D-01..D-25 anchor decisions:
//   D-01  signature (Sequence, Double, Int) order (seq, amount, seed)
//   D-03  LOCAL new Random(seed) per call; does NOT touch ExecutionContext.GetRand.
//         Mirrors VariationFunctions.VarySeeded:71-77 and BuiltInFunctions.cs:1258.
//   D-05  basic Box-Muller (cos branch); D-06 sin discarded
//   D-07  velJitter = z * amount * 0.2 (matches uniform humanize jitter range)
//   D-08  amount clamped to [0, 1]; D-09 velocity clamped to [0.05, 1.0]
//   D-10  amount==0 short-circuit returns input unchanged
//   D-11  rests pass through; D-12/D-13 empty/all-rest sequences pass through
//
// AUDIT-VERIFIED 2026-XX-XX: Gaussian humanize uses basic Box-Muller (2 uniform
// draws per sample, no rejection); local Random(seed) per call; bytes pinned by
// flow-lang.Tests/Integration/Phase25/ByteIdenticalShowcaseGaussianTests.cs.
private static Value HumanizeGaussian(IReadOnlyList<Value> args)
{
    var seq = args[0].As<SequenceData>();
    double amount = Math.Clamp(args[1].As<double>(), 0.0, 1.0);  // D-08
    int seed = args[2].As<int>();                                // D-15

    if (amount == 0.0) return Value.Sequence(seq);                // D-10 short-circuit

    var rng = new Random(seed);                                   // D-03 LOCAL
    var result = new SequenceData();
    foreach (var bar in seq.Bars)
    {
        var newNotes = new List<MusicalNoteData>();
        foreach (var note in bar.MusicalNotes)
        {
            if (note.IsRest) { newNotes.Add(note); continue; }    // D-11

            double z = NextGaussianSample(rng);                   // D-05/D-06
            double velJitter = z * amount * 0.2;                  // D-07
            double newVelocity = Math.Clamp(note.Velocity + velJitter, 0.05, 1.0);  // D-09

            newNotes.Add(note.With(velocity: newVelocity));       // see "Critical Pre-Existing Bug"
        }
        result.AddBar(new BarData(newNotes, bar.TimeSignature!));
    }
    return Value.Sequence(result);
}

private static double NextGaussianSample(Random rng)
{
    double u1 = rng.NextDouble();
    double u2 = rng.NextDouble();
    u1 = Math.Max(u1, 1e-300);  // guard log(0); see Pitfall 2
    return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);  // D-06: cos branch only
}
```

### std.flow declaration (D-25)
```flow
// Source: flow-lang/std.flow:135-136 [VERIFIED — append humanizeGaussian directly after :136]
Note: Humanize
internal proc humanize (Sequence: seq, Double: amount)
internal proc humanizeGaussian (Sequence: seq, Double: amount, Int: seed)   // ← PHASE 25
```

### Showcase additive call site (D-20)
```flow
// Source: examples/showcase.flow:20 [VERIFIED — wrap melody]
//   BEFORE:
Sequence melody = | mp _ _ E5q G5q | A5h E5h | _ _ G5q B5q | A5w |
//   AFTER:
Sequence melody = (humanizeGaussian | mp _ _ E5q G5q | A5h E5h | _ _ G5q B5q | A5w | 0.08 314)
```

## Runtime State Inventory

Phase 25 is a code-only phase. No data migration, no live-service config, no OS-registered state.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | None — humanizeGaussian produces in-memory `SequenceData`, not persisted | None |
| Live service config | None — no daemons, no databases, no n8n workflows | None |
| OS-registered state | None — no Task Scheduler / launchd / systemd entries reference this code | None |
| Secrets/env vars | None — no API keys, no PRNG seeds in env | None |
| Build artifacts | After Phase 25 ships, `flow-lang.Tests` rebuild + `examples/output/flow_showcase.{wav,mid}` regenerate. Stale `tests/output/phase18_showcase_run*.{wav,mid}` from prior Phase 18 test runs are overwritten on first test run — no manual cleanup needed. | None |

**Nothing found in any category.** Verified by: scan of CLAUDE.md (no daemons mentioned), scan of `.planning/STATE.md` references (no service configs), `grep -rn "humanize" /home/noah/Desktop/projects/flow-sharp/` confined to source / tests / examples / planning docs.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK | `dotnet build`, `dotnet run`, xUnit test runs | ✓ | net10.0 (existing project target) | — |
| xUnit | All Phase 25 Facts | ✓ | existing | — |
| `flow-interpreter` console app | Integration two-runner via `dotnet run --project flow-interpreter examples/showcase.flow` | ✓ | existing | — |
| `cmp` (POSIX) | Manual two-run smoke (optional; xUnit byte comparison via `SequenceEqual` covers automated path) | ✓ (Linux primary platform per CLAUDE.md) | — | — |
| PulseAudio | Audio playback — NOT exercised by Phase 25 tests (tests write WAV+MIDI to disk; no playback) | ✓ but unused | — | Tests do not call `play`/`preview` |

**Missing dependencies with no fallback:** None. Phase 25 needs nothing beyond what the existing repo already requires.

## Validation Architecture

> **Nyquist validation:** ENABLED. `.planning/config.json` `workflow.nyquist_validation = true`. `[VERIFIED]`

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.x (existing) `[VERIFIED: flow-lang.Tests/Phase15/EuclideanSwingTests.cs:1-3]` |
| Config file | `flow-lang.Tests/flow-lang.Tests.csproj` (existing) `[VERIFIED]` |
| Quick run command | `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase25"` |
| Full suite command | `dotnet test flow-lang.Tests` |
| Smoke (manual) | `dotnet run --project flow-interpreter tests/test_humanize_gaussian.flow` (sentinel: `"humanizeGaussian seed=42: PASSED"`) |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| DEFER-06 | `humanizeGaussian(seq, 0.1, 42)` produces deterministic velocity bytes | unit | `dotnet test flow-lang.Tests --filter "Seeded42_FirstNoteVelocity_PinnedExactly"` | ❌ Wave 0 |
| DEFER-06 | Same input + same seed produces byte-identical output | unit | `dotnet test flow-lang.Tests --filter "Seeded42_TwoConsecutiveCalls_ProduceIdenticalOutput"` | ❌ Wave 0 |
| DEFER-06 | Different seeds produce different output | unit | `dotnet test flow-lang.Tests --filter "DifferentSeeds_ProduceDifferentOutput"` | ❌ Wave 0 |
| DEFER-06 | `amount = 0.0` returns input unchanged (D-10 short-circuit) | unit | `dotnet test flow-lang.Tests --filter "AmountZero_ReturnsInputUnchanged"` | ❌ Wave 0 |
| DEFER-06 | Rests pass through unchanged (D-11) | unit | `dotnet test flow-lang.Tests --filter "Rests_PassThroughUnchanged"` | ❌ Wave 0 |
| DEFER-06 | Velocity clamped to [0.05, 1.0] (D-09) | unit | `dotnet test flow-lang.Tests --filter "Velocity_ClampedTo_005_to_10"` | ❌ Wave 0 |
| DEFER-06 | 1000-note sequence has approximately Gaussian distribution | unit (statistical sanity) | `dotnet test flow-lang.Tests --filter "LargeSequence_DistributionIsApproximatelyNormal"` | ❌ Wave 0 |
| DEFER-06 | `examples/showcase.flow` two consecutive runs produce byte-identical WAV | integration | `dotnet test flow-lang.Tests --filter "Showcase_TwoRunsProduceIdenticalWav"` (Phase25 namespace) | ❌ Wave 0 |
| DEFER-06 | `examples/showcase.flow` two consecutive runs produce byte-identical MIDI | integration | `dotnet test flow-lang.Tests --filter "Showcase_TwoRunsProduceIdenticalMidi"` (Phase25 namespace) | ❌ Wave 0 |
| DEFER-06 | Existing `examples/tutorial.flow` byte-identical contract still holds (regression) | integration | `dotnet test flow-lang.Tests --filter "Tutorial_TwoRunsProduceIdenticalWav"` (Phase18 namespace — must stay GREEN) | ✅ exists at `Phase18/ByteIdenticalTutorialTests.cs` |
| DEFER-06 | Existing `examples/showcase.flow` byte-identical contract still holds (regression — uniform humanize unchanged) | integration | `dotnet test flow-lang.Tests --filter "Showcase_TwoRunsProduceIdenticalWav"` (Phase18 namespace — must stay GREEN, but the showcase.flow content is changing — see note below) | ⚠ exists at `Phase18/ByteIdenticalShowcaseTests.cs` BUT showcase.flow content is changing per D-20 — this Fact will re-pin under new bytes |
| DEFER-06 | Smoke `.flow` script `tests/test_humanize_gaussian.flow` passes through `dotnet run` with sentinel | smoke | `dotnet run --project flow-interpreter tests/test_humanize_gaussian.flow` + sentinel match in `flow-lang.Tests/FlowScriptData.cs` registration | ❌ Wave 0 |

### Critical Note on Phase 18 Showcase Test
After D-20 lands (additive `humanizeGaussian` call site in showcase.flow), the **existing** `Phase18/ByteIdenticalShowcaseTests.cs` two-runner Facts (`Showcase_TwoRunsProduceIdenticalWav` + `Showcase_TwoRunsProduceIdenticalMidi`) will continue to pass — they assert `bytes1.SequenceEqual(bytes2)`, NOT `bytes == frozen_v1.2_bytes`. The byte-identical contract is "two consecutive runs produce identical output," not "output never changes." So Phase 18's tests are SELF-RE-PINNING by design. The new Phase 25 integration tests duplicate this assertion against the post-Phase-25 showcase.flow — they'll be byte-identical to Phase 18's results once Phase 25 ships, but that's coincidental. Plan must NOT remove Phase 18's showcase test.

`[VERIFIED: Phase18/ByteIdenticalShowcaseTests.cs:78-83 — the assertion is `bytes1.SequenceEqual(bytes2)`, no frozen-byte pin]`

### Sampling Rate
- **Per task commit:** `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase25"` (~7 unit Facts + 2 integration Facts; <30s)
- **Per wave merge:** `dotnet test flow-lang.Tests` (full suite — ~287 Facts at last v1.2 count + new Phase 23-25 additions; <2min)
- **Phase gate:** Full suite green AND `dotnet run --project flow-interpreter examples/showcase.flow` runs to exit 0 producing non-empty `examples/output/flow_showcase.{wav,mid}` AND `dotnet run --project flow-interpreter examples/tutorial.flow` likewise AND `cmp` between two consecutive showcase + tutorial invocations is clean before invoking `/gsd-verify-work`.

### Wave 0 Gaps

The planner MUST schedule Wave 0 tasks BEFORE implementation:

- [ ] Create `flow-lang.Tests/Unit/Phase25/` directory
- [ ] Create `flow-lang.Tests/Unit/Phase25/HumanizeGaussianFacts.cs` skeleton (xUnit collection `[Collection("FlowScripts")]`, `private const double Tol = 1e-9;`, `private const double BaseVelocity = 0.63;` matching Phase15/EuclideanSwingTests.cs:32-33)
- [ ] Create `flow-lang.Tests/Integration/Phase25/` directory
- [ ] Create `flow-lang.Tests/Integration/Phase25/ByteIdenticalShowcaseGaussianTests.cs` skeleton mirroring `Phase18/ByteIdenticalShowcaseTests.cs:1-89` (only changes: namespace `FlowLang.Tests.Integration.Phase25`, run-file basenames `phase25_showcase_run1.{ext}` / `phase25_showcase_run2.{ext}`)
- [ ] Create `tests/test_humanize_gaussian.flow` smoke script (sentinel pattern matching `Phase15` test_euclidean_humanize.flow style: `(print "humanizeGaussian seed=42: PASSED")` + `(print "two runs byte-identical: PASSED")`)
- [ ] Register sentinels in `flow-lang.Tests/FlowScriptData.cs` (mirror lines 225-231 entry style)
- [ ] Extend `MusicalNoteData.With(...)` helper at `NoteType.cs:317` with `double? velocity = null` parameter — required before HumanizeGaussian body can compile
- [ ] Framework install: NONE — xUnit + .NET 10 already present `[VERIFIED]`

### Test Specifics (D-23 Facts)

For each Fact, the planner picks one of two construction strategies. The deterministic-pin Fact (#1) MUST use direct C# `MusicalNoteData` construction to avoid `MusicalContext.Velocity` interference (Pitfall 4). Other Facts can use either strategy.

| Fact | Construction | Pin / Assertion |
|------|--------------|-----------------|
| `Seeded42_FirstNoteVelocity_PinnedExactly` | Direct C# `MusicalNoteData(NoteName='C', octave=4, alteration=0, durationValue=4, isRest=false, velocity=0.63)` × 4 (quarter-note Cs at base 0.63) wrapped in `BarData` + `SequenceData` | Call `humanizeGaussian(seq, 0.1, 42)`, assert `result.Bars[0].MusicalNotes[0].Velocity == <computed-once-then-frozen>` with `Tol = 1e-9`. Pin via test-first run, then freeze the computed double in source as a literal. Pattern matches `Phase15/EuclideanSwingTests.cs:83-87`. |
| `Seeded42_TwoConsecutiveCalls_ProduceIdenticalOutput` | Same fixture | Call `humanizeGaussian(seq, 0.1, 42)` twice; assert all 4 velocities identical between calls (`Assert.Equal(seq1[i].Velocity, seq2[i].Velocity, Tol)`) |
| `DifferentSeeds_ProduceDifferentOutput` | Same fixture | Call with seed=42 and seed=43; assert at least one velocity differs by > Tol. Sanity check that `humanizeGaussian` is not a no-op. |
| `AmountZero_ReturnsInputUnchanged` | Same fixture | Call `humanizeGaussian(seq, 0.0, 42)`; assert all velocities equal to base (D-10 short-circuit verification). |
| `Rests_PassThroughUnchanged` | Mixed fixture: 2 notes + 2 rests (`MusicalNoteData(' ', 0, 0, 4, isRest: true)` × 2 interleaved) | Call `humanizeGaussian(seq, 0.5, 42)`; assert rest entries unchanged (`Assert.Equal(originalRest.IsRest, result.IsRest)` + `Assert.Equal(originalRest.DurationValue, result.DurationValue)`); only non-rests have changed velocity. |
| `Velocity_ClampedTo_005_to_10` | Fixture with extreme baseline (`velocity=0.99`) | Call `humanizeGaussian(seq, 1.0, 42)` over 100 notes; assert `Min(velocities) >= 0.05` AND `Max(velocities) <= 1.0` for every note (D-09). |
| `LargeSequence_DistributionIsApproximatelyNormal` | 1000 quarter notes at `velocity=0.5` baseline | Call `humanizeGaussian(seq, 0.5, 42)`; compute sample mean and stddev of velocity perturbations (`note.Velocity - 0.5`); assert `Math.Abs(mean) < 0.02` AND `Math.Abs(stddev - 0.1) / 0.1 < 0.20` (within ±20% of expected `amount * 0.2 = 0.1`). Looser tolerances given finite n=1000. |

`[VERIFIED: D-23 in CONTEXT.md, Phase15/EuclideanSwingTests.cs:32-87 patterns]`

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `MusicalNoteData.With(...)` extension with a `velocity` slot is non-controversial and can land in Phase 25 Wave 0 | Critical Pre-Existing Bug | LOW — if planner judges the helper extension too risky to do in Phase 25, fallback is the full 17-arg ctor. Either compiles. |
| A2 | `1e-300` is a sufficient floor for the `u1 == 0` guard | Pattern 2 + Pitfall 2 | LOW — value is industry-standard; produces clamped Gaussian sample `~Math.Sqrt(2 * 690.776) ≈ 37.16`, multiplied by `amount * 0.2 ≤ 0.2` is `≤ 7.43`, then `Math.Clamp(velocity + 7.43, 0.05, 1.0)` clamps to 1.0. Always benign. |
| A3 | xUnit tolerance `Tol = 1e-9` is sufficient for the deterministic-pin Fact | Validation Architecture (Test Specifics) | LOW — matches existing `Phase15/EuclideanSwingTests.cs:33` precedent. Box-Muller is exact-arithmetic deterministic per seed at IEEE 754 double precision; 1e-9 tolerance has 10 orders of magnitude of headroom. |
| A4 | Seed `314` is appropriate for the showcase additive call site (Showcase Confirmation) | Showcase Confirmation | LOW — any fixed integer literal works. `314` distinguishes from `42` (the deterministic-pin Fact seed) and `7` (the existing euclidean seed in showcase.flow:17), reducing accidental cross-contamination if those seeds are ever changed. |
| A5 | Phase 18's existing `ByteIdenticalShowcaseTests.cs` will continue to pass after D-20 changes showcase.flow content | Critical Note on Phase 18 Showcase Test | MEDIUM — the test asserts `bytes1.SequenceEqual(bytes2)` (run-to-run identity), not against a frozen byte set. As long as `humanizeGaussian` is itself deterministic (D-03 + Pitfall 6), the assertion holds. If a regression bug causes humanizeGaussian to consume `Random.Shared` somewhere, BOTH Phase 18 and Phase 25 showcase tests RED simultaneously — which is the right failure signal. Plan must verify Phase 18 tests stay green after the D-20 showcase edit lands. |

**If this table feels short:** That's because CONTEXT.md already locked 25 D-IDs. The few open items above are operational (helper extension, seed choice) not architectural.

## Open Questions

None blocking the planner. Three minor operational notes:

1. **Where exactly to add `velocity` to `With(...)`** — alphabetical order of optional parameters? After `portamentoMs`? Style choice for the planner. The signature change is one parameter, no breaking ripple.
2. **Whether to add a "no-NaN stress" Fact (1000 random seeds × 1000 notes, assert no NaN/Inf)** — D-23 doesn't list it, but it's cheap insurance for Pitfall 2. Recommend yes; it goes RED if the `Math.Max(u1, 1e-300)` guard regresses.
3. **Tutorial chapter content** — D-22 recommended uniform vs Gaussian contrast. Specific code sample at CONTEXT.md `<specifics>` lines 191-194 is appropriate; planner can lift verbatim.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Hand-rolled Box-Muller everywhere | Same — no library equivalent in .NET BCL for `NextGaussian(int seed)` | Stable since .NET 1.0 | Library status unchanged; Box-Muller IS the standard idiom in C# `[VERIFIED: Microsoft .NET docs Random class — no Gaussian method]` |
| `Random.NextDouble()` returns `[0, 1]` | Returns `[0, 1)` per documented contract — 0.0 is a possible output, 1.0 is not | .NET 1.0 onward | Confirms the `Math.Max(u1, 1e-300)` guard is necessary, not paranoid `[VERIFIED: Microsoft .NET docs Random.NextDouble]` |
| `System.Random` was non-deterministic across .NET versions (pre-.NET 6) | .NET 6+: `Random(int)` ctor is documented to produce a STABLE sequence per seed across patches; `Random.Shared` (.NET 6+) is per-thread non-seedable | .NET 6 (Nov 2021) | Phase 25 `new Random(seed)` is byte-stable across .NET 10 patches (the contract Phase 18+ relies on) `[CITED: docs.microsoft.com .NET 6 release notes — Random determinism guarantees]` |

**Deprecated/outdated:** None affects this phase.

## Security Domain

> Required when `security_enforcement` is enabled. Not explicitly configured — treat as enabled.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | — (no auth surface) |
| V3 Session Management | no | — (no sessions) |
| V4 Access Control | no | — (no privilege boundary) |
| V5 Input Validation | yes | `Math.Clamp(amount, 0.0, 1.0)` (D-08) and `Math.Clamp(velocity, 0.05, 1.0)` (D-09) handle out-of-range inputs charitably (per CLAUDE.md memory). The `seed` parameter is `Int` (constructor accepts any 32-bit value) — no validation needed (`Random(int)` accepts the full range including negatives and 0). |
| V6 Cryptography | no | — `Random` is NOT cryptographic; never use `humanizeGaussian` for security purposes. Comment in source MUST clarify (existing `BuiltInFunctions.cs:1257` style). |

### Known Threat Patterns for `humanizeGaussian`

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| User-supplied `amount` outside `[0, 1]` | Tampering / DoS | `Math.Clamp` silent-and-documented — D-08 |
| User-supplied very-large sequence triggering O(n) PRNG draws | DoS | None needed — sequences are bounded by `MusicalContext` size limits already enforced by `NoteStreamCompiler` (and `BuildEuclideanSequence` already rejects `steps > 1024`). humanizeGaussian inherits the upstream bound. |
| `seed` from untrusted source affecting downstream determinism | Information Disclosure | None — `humanizeGaussian` is a pure function of `(seq, amount, seed)`; seed leaks no data (it's an INPUT). |
| Misuse for crypto / authentication | Spoofing | Source comment: `// NOTE: System.Random is NOT cryptographically secure. humanizeGaussian is for musical jitter only.` |
| `Math.Log(0) = -∞` NaN propagation (already covered as Pitfall 2) | DoS (silent corruption of audio output) | `Math.Max(u1, 1e-300)` guard |

`[VERIFIED: existing BuiltInFunctions.cs:1229-1232 step-bound precedent]`

## Sources

### Primary (HIGH confidence — in-repo, VERIFIED)
- `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs:17-30` — `RegisterAll` call site
- `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs:864-903` — existing `Humanize` (FROZEN per D-18)
- `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs:228-245` — `TransformNotes` helper (NOT used by Humanize, but available)
- `flow-lang/StandardLibrary/Composition/VariationFunctions.cs:71-77` — `VarySeeded` LOCAL Random precedent
- `flow-lang/StandardLibrary/BuiltInFunctions.cs:1207-1260` — euclidean 6-arg D-17 LOCAL Random (full precedent block + comment style)
- `flow-lang/TypeSystem/SpecialTypes/NoteType.cs:211-330` — `MusicalNoteData` class, ctor, `With(...)` helper, `Velocity` clamp at line 294
- `flow-lang/std.flow:130-154` — humanize + euclidean declarations format
- `examples/showcase.flow:1-45` — confirms `melody` Sequence at line 20 is correct target
- `examples/tutorial.flow:555-594` — confirms humanize chapter at line 567 area
- `flow-lang.Tests/Integration/Phase18/ByteIdenticalShowcaseTests.cs:1-90` — two-runner integration pattern
- `flow-lang.Tests/Integration/Phase15/EuclideanByteIdenticalTests.cs:1-60` — seeded-PRNG byte-identical precedent
- `flow-lang.Tests/Unit/Phase15/EuclideanSwingTests.cs:32-87` — deterministic-pin `Tol = 1e-9` pattern, `BaseVelocity = 0.63`
- `flow-lang.Tests/FlowScriptData.cs:225-231` — euclidean humanize sentinel format
- `.planning/REQUIREMENTS.md:84,109,154,170` — DEFER-06 + binding pre-ordering #5
- `.planning/ROADMAP.md:71,192-200` — Phase 25 entry + dependencies
- `.planning/research/PITFALLS.md:166-195` — Pitfall 6 full text
- `.planning/phases/25-gaussian-humanize-last-prng-phase/25-CONTEXT.md` — 25 locked D-IDs
- `.planning/config.json` — `nyquist_validation: true`
- `CLAUDE.md` — full project instructions; "Guiding Principle: Minimal Dependencies"; .NET 10 + C# 13; functional S-expression style; charitable interpretation memory

### Secondary (MEDIUM — official docs)
- Microsoft .NET docs: `System.Random.NextDouble()` returns `[0, 1)` — confirms the 0-guard is necessary
- Microsoft .NET 6 release notes: `Random(int)` seeded ctor produces stable sequence per seed across patches

### Tertiary (LOW — none cited)
None. Every recommendation is grounded in primary sources.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — zero new packages; everything in BCL or in-repo already.
- Architecture: HIGH — sibling pattern verified against three precedents (`Humanize`, `VarySeeded`, `BuildEuclideanSequence`).
- Pitfalls: HIGH for Pitfalls 1-3 (all directly cited in `.planning/research/PITFALLS.md` and the existing codebase); MEDIUM for Pitfalls 4-5 (operational, not architectural).
- Validation Architecture: HIGH — every test path follows an existing pattern with cited precedent.
- The Critical Pre-Existing Bug finding: HIGH — verified by reading the Humanize ctor call (12 args) and the MusicalNoteData ctor signature (17 params).

**Research date:** 2026-05-04
**Valid until:** 2026-06-04 (30 days for stable; .NET 10 BCL is stable, in-repo patterns are mature)
