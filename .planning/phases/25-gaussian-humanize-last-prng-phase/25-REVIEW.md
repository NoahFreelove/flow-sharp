---
phase: 25-gaussian-humanize-last-prng-phase
reviewed: 2026-05-04T00:00:00Z
depth: standard
files_reviewed: 9
files_reviewed_list:
  - flow-lang/StandardLibrary/Transforms/TransformFunctions.cs
  - flow-lang/TypeSystem/SpecialTypes/NoteType.cs
  - flow-lang/std.flow
  - examples/showcase.flow
  - examples/tutorial.flow
  - tests/test_humanize_gaussian.flow
  - flow-lang.Tests/Unit/Phase25/HumanizeGaussianFacts.cs
  - flow-lang.Tests/Unit/Phase25/NoteTypeWithVelocityFacts.cs
  - flow-lang.Tests/Integration/Phase25/ByteIdenticalShowcaseGaussianTests.cs
findings:
  critical: 0
  warning: 1
  info: 4
  total: 5
status: warnings
---

# Phase 25: Code Review Report

**Reviewed:** 2026-05-04
**Depth:** standard
**Files Reviewed:** 9 (3 production + 3 examples/smoke + 3 tests)
**Status:** warnings (1 minor warning, 4 info; no blockers)

## Summary

Phase 25 ships `humanizeGaussian(Sequence, Double, Int)` as a Box-Muller velocity-perturbation
transform with LOCAL `new Random(seed)` per call. The implementation is small (sub-50 LOC), well-commented,
and faithfully follows the 25 locked decisions D-01..D-25 in 25-CONTEXT.md.

**Adversarial verification stance — PRIMARY CONCERNS CHECKED:**

- **D-18 invariant (existing `Humanize` FROZEN at TransformFunctions.cs:866-903):** VERIFIED via
  `git diff 9c3553e^..9c3553e -- flow-lang/StandardLibrary/Transforms/TransformFunctions.cs |
  grep -E '^-[^-]' | wc -l` returns `0`. Only insertions (the new `RegisterHumanizeGaussian` /
  `HumanizeGaussian` / `NextGaussianSample` block) — zero deletions inside the frozen block. The
  static `HumanizeRng = new()` field (line 874) and the buggy 12-arg ctor at lines 897-899 are
  preserved verbatim, so the v1.2 byte-identical determinism contract holds.
- **PRNG correctness (D-03 LOCAL Random):** VERIFIED. `var rng = new Random(seed);` at line 941
  creates a stack-local instance per call. No static field, no global state, no
  `ExecutionContext.GetRand` access. Same `(seq, amount, seed)` reseeds identically across calls
  (pinned by `Seeded42_TwoConsecutiveCalls_ProduceIdenticalOutput` Fact).
- **Box-Muller numerical correctness (D-05/D-06):** VERIFIED. Cos branch only:
  `Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2)`. Sin companion discarded.
  `u1` floored at `1e-300` via `Math.Max(u1, 1e-300)` at line 973 — guards `Math.Log(0)` divergence
  (Random.NextDouble contract is `[0, 1)` so 0.0 IS a possible output; floor produces a worst-case
  ~37-stddev sample which subsequent velocity clamp `[0.05, 1.0]` neutralizes). Pinned by
  `Seeded42_FirstNoteVelocity_PinnedExactly` (= 0.6413705509099572) and
  `LargeSequence_DistributionIsApproximatelyNormal` (n=1000, mean ~ 0, stddev ~ 0.1 within ±20%).
- **Field preservation (RESEARCH §Critical Pre-Existing Bug):** VERIFIED. Line 957 calls
  `note.With(velocity: newVelocity)`, NOT the latent 12-arg ctor at TransformFunctions.cs:896-898
  that drops 5 fields (DurationFraction, OnsetOffset, DurationOverlap, PortamentoMs, IsChordTone).
  Plan 25-01's `MusicalNoteData.With(...)` extension at NoteType.cs:317-333 routes through the
  full 17-arg ctor, preserving all fields. Pinned by `With_VelocitySet_PreservesAll16OtherFields`.
- **Edge cases (D-08..D-15):** VERIFIED. `amount` clamped to `[0, 1]` (D-08, line 934);
  velocity clamped to `[0.05, 1.0]` (D-09, line 953); `amount == 0.0` short-circuit (D-10,
  line 937); rest passthrough (D-11, line 949); empty/all-rest sequences pass through naturally
  (D-12/D-13); negative amounts coerced to 0 by Math.Clamp lower bound (D-14); seed is `int`
  matching `Random(int)` ctor (D-15).
- **Thread safety (System.Random is NOT thread-safe):** VERIFIED. The local `rng` instance is
  created and consumed on a single execution thread (the interpreter is single-threaded; no
  goroutine/Task/parallel iteration). `Random` is never shared across threads. The static
  `HumanizeRng` in the OLD `Humanize` is technically a thread-unsafety risk if Flow ever
  multi-threads the interpreter, but that's pre-existing and FROZEN.
- **Determinism (Phase 18 binding contract):** VERIFIED. 19/19 Phase 18 byte-identical Facts
  GREEN; 2/2 Phase 25 `ByteIdenticalShowcaseGaussianTests` GREEN; 4/4 manual two-run cmp on
  showcase.flow + tutorial.flow exit 0.

The implementation is solid. The 5 findings below are minor observations, mostly informational —
none block release.

## Warnings

### WR-01: `humanizeGaussian` short-circuit returns input `SequenceData` reference (aliasing)

**File:** `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs:937`
**Issue:** When `amount == 0.0`, the function returns `Value.Sequence(seq)` — the *same*
`SequenceData` reference passed in by the caller. Since `SequenceData.Bars` is a public
mutable `List<BarData>` (SequenceType.cs:15) and `SequenceData.AddBar` mutates it
(SequenceType.cs:32-41), a downstream caller that performs in-place mutation on the returned
value would also mutate the input. This is a sharing/aliasing hazard that diverges from the
existing `Humanize` (line 876-903), which always allocates a fresh `SequenceData` even though
that case is also a logical no-op.

**Mitigating factors:** (a) The pattern already exists in the codebase — line 108
(`if (strength == 0.0 && swing == 0.0) return Value.Sequence(seq);`), line 654, line 768,
line 804 all return the input sequence reference. Phase 25 follows established convention.
(b) Flow's interpreter is single-threaded and Sequence values are typically used as
"snapshots" then discarded — in-place mutation post-transform is not idiomatic in user code.
(c) The byte-identical regression tests pass, so no current call path triggers the alias.

**Severity:** WARNING (not BLOCKER) — convention-matching latent hazard, not an active bug.

**Fix (optional, conservative):**
```csharp
// Build a defensive shallow copy even on the short-circuit path:
if (amount == 0.0)
{
    var copy = new SequenceData();
    foreach (var bar in seq.Bars) copy.AddBar(bar);  // BarData itself is shareable
    return Value.Sequence(copy);
}
```
Or, accept the codebase convention and document the aliasing contract at the function header.
Tracking this against the codebase's "Sequence values are immutable from the user-script
perspective" implicit contract would clarify whether the convention is safe.

## Info

### IN-01: Box-Muller `1e-300` floor is mathematically defensible but the rationale is buried in a comment

**File:** `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs:973`
**Issue:** `u1 = Math.Max(u1, 1e-300);` — the floor value `1e-300` is chosen to be
"small enough that `Math.Log(1e-300) ≈ -690.776` produces a worst-case ~37-stddev sample,
which the velocity clamp `[0.05, 1.0]` truncates harmlessly." The comment at lines 966-968
captures this, but a reader who doesn't trust the comment would benefit from a unit Fact
that explicitly drives the guard branch (`u1 == 0.0`). The probability of `Random.NextDouble()
returning exactly 0.0 is ~1/2^53 (per the API contract of `[0, 1)`) so a Fact would have to
mock the RNG or use a seed-search; not practical.

**Severity:** INFO — mathematically correct, well-documented; no fix required.

**Fix (optional):** Add a comment-only note pointing at the source of the worst-case
Gaussian magnitude calculation, or extract `1e-300` into a named constant
`private const double LogZeroGuard = 1e-300;` for readability.

### IN-02: `tests/test_humanize_gaussian.flow` prints "two runs byte-identical: PASSED" without comparing bytes

**File:** `tests/test_humanize_gaussian.flow:24`
**Issue:** The Flow-side smoke script writes two MIDI files (run A + run B) with identical
seed, then prints `"two runs byte-identical: PASSED"` unconditionally — without any
`cmp`-equivalent in Flow. The sentinel string suggests an assertion happened, but it didn't.
The actual byte-comparison lives in
`flow-lang.Tests/Integration/Phase25/ByteIdenticalShowcaseGaussianTests.cs` (running
showcase.flow, not this test). The Flow `RequiredSentinels` table at FlowScriptData.cs:237
will only catch failures if the script crashes (which would prevent the print).

**Mitigating factors:** This pattern exactly mirrors the precedent at
`tests/test_euclidean_humanize.flow:24` (Phase 15 DX-09). The convention is that `.flow`
smoke scripts assert "the script ran without throwing"; the byte-identical contract is
authoritatively pinned by the integration test. The Flow language itself doesn't currently
expose a file-comparison built-in, so an in-script `cmp` is impossible.

**Severity:** INFO — convention-matching; cosmetic naming concern only.

**Fix (optional):** Rename the sentinel to "two runs completed: PASSED" to avoid suggesting
an assertion that wasn't performed. Updates would need to land in tandem with FlowScriptData.cs
and the matching Phase 15 precedent file (cross-phase change — out of Phase 25 scope).

### IN-03: `Velocity_ClampedTo_005_to_10` Fact relies on probabilistic clamp engagement, but is actually deterministic

**File:** `flow-lang.Tests/Unit/Phase25/HumanizeGaussianFacts.cs:185-189`
**Issue:** The assertion comment claims clamp engagement is "almost certain" with baseline
0.99 + amount=1.0 + seed=42. Strictly speaking, with a fixed seed the outcome is
deterministically true OR false — not probabilistic. Currently it's true (test GREEN), but
the wording could mislead a future maintainer who changes the seed thinking "almost certain
will hold for any seed" — it might not for a pathological seed.

**Severity:** INFO — wording precision, not a bug.

**Fix (optional):** Rephrase the comment to "with seed=42, deterministically engages the
clamp on at least one of 100 samples (verified empirically — pinned by GREEN test result)."

### IN-04: Test class `HumanizeGaussianFacts` uses `[Collection("FlowScripts")]` but doesn't run .flow scripts

**File:** `flow-lang.Tests/Unit/Phase25/HumanizeGaussianFacts.cs:29`
**Issue:** The class is annotated `[Collection("FlowScripts")]` but its tests do not invoke
the FlowEngine, do not write files, and do not interact with shared global state — they call
`TransformFunctions.Register` directly into a fresh local `InternalFunctionRegistry` per test.
The `FlowScripts` collection serializes test execution; using it here unnecessarily reduces
parallelism with no correctness benefit.

**Mitigating factors:** Same convention is used at `flow-lang.Tests/Unit/Phase22/VoicingFacts.cs`
and `ArpeggioFacts.cs`. Project-wide convention.

**Severity:** INFO — performance / parallelism micro-impact only (~13 tests serialize
unnecessarily).

**Fix (optional):** Remove `[Collection("FlowScripts")]` from `HumanizeGaussianFacts` and
`NoteTypeWithVelocityFacts` so they can run in parallel. Verify post-removal that no shared
state (e.g., `TransformFunctions.HumanizeRng` static field) is touched — `humanizeGaussian`
specifically avoids `HumanizeRng` per D-03, so the local-PRNG path is parallel-safe.

### IN-05: Examples chapter at `tutorial.flow:540-547` re-evaluates `humanize` (uses static non-deterministic `HumanizeRng`)

**File:** `examples/tutorial.flow:540-547`
**Issue:** Section 18.5 ("Gaussian Humanize") demonstrates uniform vs Gaussian humanize:
```flow
Sequence uniformFeel = (humanize myMelody 0.1)
Sequence naturalFeel = (humanizeGaussian myMelody 0.1 42)
```
The `(humanize myMelody 0.1)` call uses the static `HumanizeRng` field
(TransformFunctions.cs:874), which is NON-deterministic across runs (different velocity bytes
each run). The chapter then `(print $"uniform humanize: {(str uniformFeel)}")` which writes
non-deterministic output to stdout.

**Mitigating factors:** This is INTENTIONAL per the chapter's rhetorical goal — contrasting
"non-deterministic uniform" vs "deterministic Gaussian-with-seed". Tutorial output is to
stdout (not WAV/MIDI); the Phase 18 byte-identity contract is `cmp` on the WAV/MIDI files
written at line 643-644, not on stdout. The chapter does NOT call writeWav/writeMidi inside
its tempo/timesig block, so it cannot break the byte-identity gate.

**Severity:** INFO — intentional pedagogy; behavior is correct.

**Fix:** None required. The contrast is deliberate. If a future maintainer worries about
stdout determinism, the comment at line 541 ("non-deterministic (uses static Random)")
already discloses this.

---

## Verification Checklist (per success_criteria)

- [x] **D-18 invariant explicitly verified.** `git diff 9c3553e^..9c3553e --
  flow-lang/StandardLibrary/Transforms/TransformFunctions.cs | grep -E '^-[^-]' | wc -l`
  returns `0` — zero deletions in the frozen block 866-903 (only insertions adjacent to it).
- [x] **Box-Muller correctness assessed.** Cos branch: `√(-2 ln u1) · cos(2π u2)` is the
  textbook standard form; `1e-300` floor mitigates the `[0, 1)` API edge case correctly;
  pinned by `Seeded42_FirstNoteVelocity_PinnedExactly = 0.6413705509099572` and
  `LargeSequence_DistributionIsApproximatelyNormal` (n=1000, mean tolerance 0.02, stddev
  tolerance ±20%).
- [x] **Field-preservation pattern verified.** humanizeGaussian uses `note.With(velocity:
  newVelocity)` (line 957), routing through the full 17-arg ctor in `MusicalNoteData.With`
  (NoteType.cs:317-333) — preserves all fields including the 5 the buggy 12-arg ctor at
  lines 897-899 silently drops. Pinned by `With_VelocitySet_PreservesAll16OtherFields`.
- [x] **Each finding has file:line, severity, description, recommendation.**

## Closing Assessment

Phase 25 is shipping clean. The single WARNING (WR-01: aliasing on amount=0 short-circuit) is
not a bug — it's a latent sharing concern that matches existing codebase convention, and there
is no current call path that triggers it. The 4 INFO items are cosmetic (comment wording, test
collection assignment, sentinel naming).

The implementation faithfully translates 25 locked decisions into ~50 lines of well-commented
C#. Box-Muller is implemented correctly with the appropriate `log(0)` guard. PRNG isolation
(D-03) is correct — no global state, no thread-safety concerns. The frozen-block invariant
(D-18) is preserved exactly. The pre-existing 12-arg ctor field-drop bug at TransformFunctions.cs:896-898
is correctly avoided via the `With(velocity:)` helper (Plan 25-01 precondition).

The phase ships.

---

_Reviewed: 2026-05-04_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
