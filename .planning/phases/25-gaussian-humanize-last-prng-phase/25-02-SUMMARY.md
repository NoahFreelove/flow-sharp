---
phase: 25-gaussian-humanize-last-prng-phase
plan: 02
subsystem: standard-library/transforms
tags: [humanize-gaussian, box-muller, prng, transforms, phase-25, wave-2, implementation, defer-06]

# Dependency graph
requires:
  - phase: 25-00
    provides: "Phase 25 context, research, patterns; D-01..D-25 decisions; Skip-marked HumanizeGaussianFacts skeleton"
  - phase: 25-01
    provides: "MusicalNoteData.With(velocity:) slot — the field-preserving builder this plan calls"
  - phase: 22
    provides: "Pattern: With(...) helper one-slot extension"
provides:
  - "humanizeGaussian(Sequence, Double, Int) registered built-in (Box-Muller cos branch, deterministic by seed)"
  - "TransformFunctions.NextGaussianSample helper (private static, testable)"
  - "std.flow public proc declaration making humanizeGaussian visible to .flow user scripts"
  - "7 GREEN HumanizeGaussianFacts pinning all D-23 invariants + frozen pin value 0.6413705509099572 for seed=42 first-note velocity"
affects: [25-03-showcase-tutorial, 25-04-integration-validation, future-velocity-transforms]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "LOCAL new Random(seed) per call (mirrors VariationFunctions.VarySeeded :71-77 / BuiltInFunctions.cs:1258) — does NOT touch ExecutionContext.GetRand"
    - "Box-Muller cos branch with Math.Max(u1, 1e-300) log(0) guard for Gaussian sampling from uniform PRNG"
    - "Frozen-pin testing: discover actual deterministic output value once, then freeze as IEEE 754 literal constant — canary for .NET Random algorithm drift"
    - "FROZEN block extension: new code lands ADJACENT to existing implementation (D-16); existing block byte-identical (D-18) verified by `git diff | grep -E '^-[^-]' = 0`"

key-files:
  created:
    - .planning/phases/25-gaussian-humanize-last-prng-phase/25-02-SUMMARY.md
  modified:
    - flow-lang/StandardLibrary/Transforms/TransformFunctions.cs
    - flow-lang/std.flow
    - flow-lang.Tests/Unit/Phase25/HumanizeGaussianFacts.cs

key-decisions:
  - "D-03 enforced: humanizeGaussian uses LOCAL new Random(seed) — no global PRNG state. Citation comment cites VariationFunctions:71-77 and BuiltInFunctions.cs:1258 verbatim."
  - "D-18 enforced: existing humanize at TransformFunctions.cs:866-903 is byte-identical — git diff shows zero deletions inside the frozen block."
  - "RESEARCH §Critical Pre-Existing Bug avoided: humanizeGaussian uses note.With(velocity: newVelocity) (Plan 25-01 helper) instead of the 12-arg ctor at :896-898 that drops 5 fields added by Phases 18 & 22."
  - "Frozen pin value 0.6413705509099572 captures seed=42 + amount=0.1 + baseVelocity=0.63 first-note output. Acts as a canary if .NET's Random algorithm shifts in a future patch."
  - "D-08 amount clamp + D-09 velocity clamp + D-10 amount==0 short-circuit + D-11 rest passthrough — all four edge contracts encoded as named-arg D- citations in the implementation."

patterns-established:
  - "Box-Muller in TransformFunctions: NextGaussianSample(Random) → double helper, two NextDouble draws → one N(0,1) sample, sin companion discarded (D-06), u1 floored at 1e-300."
  - "Sibling-of-frozen pattern: when extending a FROZEN module, add a parallel function adjacent to it (RegisterHumanize → RegisterHumanizeGaussian) and wire it from the central Register(...) entry point on the next line."
  - "Pin discovery protocol: write Fact with placeholder 0.0 → run → read actual from failure diff → freeze as IEEE 754 literal → re-run GREEN. Encodes the algorithm output rather than re-deriving it."

requirements-completed: [DEFER-06]

# Metrics
duration: 5min
completed: 2026-05-04
---

# Phase 25 Plan 02: humanizeGaussian Box-Muller Implementation Summary

**Implemented `humanizeGaussian(Sequence, Double, Int)` as a Box-Muller cos-branch velocity-perturbation transform with a LOCAL `new Random(seed)` per call. Lives adjacent to the FROZEN `Humanize` at `TransformFunctions.cs:866-903`; uses the Plan 25-01 `note.With(velocity:)` helper to avoid the latent 12-arg ctor field-drop bug. After this plan ships, composers can call `(humanizeGaussian seq 0.1 42)` from .flow source and get deterministic Gaussian-distributed velocity jitter.**

## Performance

- **Duration:** ~5 min
- **Started:** 2026-05-04T23:14:00Z
- **Completed:** 2026-05-04T23:21:11Z
- **Tasks:** 3
- **Files modified:** 3 (TransformFunctions.cs, std.flow, HumanizeGaussianFacts.cs)
- **Files created:** 0 (all three target files pre-existed)

## Accomplishments

- `humanizeGaussian(Sequence, Double, Int)` is registered, callable from .flow user code, and produces deterministic Gaussian-distributed velocity perturbation per seed.
- LOCAL `new Random(seed)` invariant (D-03) preserved — no global PRNG state is read or written. Verified by inspection of the cited comment block above `var rng = new Random(seed)` and by the determinism Fact `Seeded42_TwoConsecutiveCalls_ProduceIdenticalOutput`.
- Frozen pin value **`0.6413705509099572`** captured for `seed=42, amount=0.1, baseVelocity=0.63` — first non-rest note Velocity. Acts as a canary if .NET's Random algorithm changes.
- All 7 D-23 Facts GREEN (frozen-pin + determinism + seed-sensitivity + amount=0 short-circuit + rest passthrough + velocity clamp engagement + 1000-sample distribution sanity).
- D-18 invariant verified empirically: `git diff flow-lang/StandardLibrary/Transforms/TransformFunctions.cs | grep -E "^-[^-]" | wc -l` returned `0` — existing `Humanize` block at lines 866-903 is byte-identical.
- Phase 18 byte-identical regression: 19/19 GREEN. Plan 25-01 `NoteTypeWithVelocityFacts`: 4/4 GREEN. Full `flow-lang.Tests` suite: **686 passed, 0 failed, 2 skipped** (the 2 skips are Plan 25-03 byte-identical-showcase placeholders, expected).
- Smoke test: `dotnet run --project flow-interpreter -- -e 'use "@std" / Sequence s = | C4q D4q | / Sequence h = (humanizeGaussian s 0.1 42) / (print "ok")'` prints `ok` with no errors.

## Task Commits

Each task was committed atomically:

1. **Task 1: Implement HumanizeGaussian + RegisterHumanizeGaussian + NextGaussianSample (D-01..D-17)** — `9c3553e` (feat)
   - Inserted `RegisterHumanizeGaussian(registry);` between `RegisterHumanize(registry);` and `RegisterOrnamentTransforms(registry);` in the Register(...) method.
   - Added `===== Humanize Gaussian =====` block (3 methods: `RegisterHumanizeGaussian`, `HumanizeGaussian`, `NextGaussianSample`) BETWEEN the FROZEN Humanize block and the existing Ornament Transforms section.
   - All D-anchor decisions (D-01, D-03, D-05..D-11, D-15, D-17) carry inline citations.
   - Build green, zero deletions in `git diff`.
2. **Task 2: Add internal proc humanizeGaussian declaration to std.flow (D-25)** — `a928628` (feat)
   - Inserted exactly one line (`internal proc humanizeGaussian (Sequence: seq, Double: amount, Int: seed)`) immediately after the existing humanize line at `flow-lang/std.flow:136`.
   - Smoke test confirms .flow user code can resolve and call humanizeGaussian.
3. **Task 3: Flip 7 HumanizeGaussianFacts from Skip to live + freeze pin (D-23)** — `3cc3a11` (test)
   - Removed 7 `Skip = "..."` markers; replaced 7 `Assert.True(false, "skeleton...")` bodies with real implementations.
   - Added `BuildBaseSequence`, `BuildMixedSequence`, `CallHumanizeGaussian`, `NonRestNotes` helpers + frozen pin constant `Seeded42_FirstNote_PinnedVelocity = 0.6413705509099572`.
   - 7/7 GREEN; full suite GREEN.

## Frozen Pin Value (Noteworthy Artifact)

```csharp
private const double Seeded42_FirstNote_PinnedVelocity = 0.6413705509099572;
```

**Inputs:** `seed=42`, `amount=0.1`, `baseVelocity=0.63`, single 4-note `BuildBaseSequence`, first non-rest note.
**Algorithm:** `z = sqrt(-2 ln u1) * cos(2π u2); newVelocity = clamp(0.63 + z * 0.1 * 0.2, 0.05, 1.0)`.
**Discovery method:** Wrote the Fact with placeholder `0.0`, ran via `dotnet test`, read the actual velocity from the failure diff, replaced placeholder with the IEEE 754 literal, re-ran GREEN.
**What it pins:** Combined invariants of (a) .NET Random's algorithm, (b) the iteration order over `Sequence.Bars` × `Bar.MusicalNotes` (D-04), and (c) the Box-Muller scale formula `velJitter = z * amount * 0.2` (D-07). Any change to any of these three breaks this pin — that is the intended canary behavior.

## Verification Results

| Check | Expected | Actual |
|-------|----------|--------|
| `dotnet build` | green | **green** (0 errors, 19 warnings — all pre-existing) |
| HumanizeGaussianFacts | 7 passed | **7 passed** |
| Phase 18 byte-identical regression | 19 passed | **19 passed** |
| NoteTypeWithVelocityFacts (Plan 25-01) | 4 passed | **4 passed** |
| Full flow-lang.Tests suite | 0 failed | **686 passed, 0 failed, 2 skipped (Plan 25-03 placeholders)** |
| `git diff TransformFunctions.cs grep ^-[^-] wc -l` (D-18) | 0 | **0** |
| .flow smoke `(humanizeGaussian seq 0.1 42)` | "ok" | **"ok"** |

## Deviations from Plan

**One minor deviation, no functional impact:**

**[Doc - command syntax] Smoke test command needed `--` separator for `dotnet run` arg forwarding**
- **Found during:** Task 2 verification.
- **Issue:** The plan's smoke command `dotnet run --project flow-interpreter -e '...'` causes `dotnet run` to interpret `-e` as a `dotnet` flag, not as an argument to flow-interpreter. The interpreter starts the REPL header and the eval code is silently dropped.
- **Fix:** Run with `--` separator: `dotnet run --project flow-interpreter -- -e '...'`. Output: `ok` (no errors).
- **Files modified:** None (no source change — only the smoke command wrapper).
- **Commit:** None (verification-only deviation).
- **Acceptance criteria adjustment:** The Task 2 acceptance criterion `dotnet run --project flow-interpreter -e '...' | grep -c "^ok$"` returns 1 was satisfied via the `--`-separated form.

Otherwise the plan executed exactly as written. All D-01..D-25 decisions, all 7 D-23 Facts, the D-18 byte-identical FROZEN invariant, and the D-19 Phase 18 regression gate all pass.

## Threat Flags

None. The threat model in 25-02-PLAN.md (T-25-02-01..T-25-02-06) is fully covered:

- T-25-02-01 (log(0) NaN propagation): mitigated by `Math.Max(u1, 1e-300)` guard; verified by `Velocity_ClampedTo_005_to_10` and `LargeSequence_DistributionIsApproximatelyNormal`.
- T-25-02-02 (LOCAL Random vs global PRNG): mitigated by `var rng = new Random(seed)` with citation comment; no `Random.Shared` reference anywhere in HumanizeGaussian.
- T-25-02-03 (FROZEN existing Humanize): mitigated by `git diff` zero-deletion check + Phase 18 regression GREEN.
- T-25-02-04 (seed information disclosure): accept — seed is input, no PII surface.
- T-25-02-05 (unbounded Sequence DoS): accept — sequence size upstream-bounded; humanizeGaussian is O(n) sequential.
- T-25-02-06 (System.Random crypto misuse): mitigated by source comment "NOTE: System.Random is NOT cryptographically secure. humanizeGaussian is for musical jitter only — never use for security purposes."

No new threat surface introduced.

## Self-Check: PASSED

**Files claimed in summary:**
- `flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` (modified) — confirmed via `git log --name-only 9c3553e`.
- `flow-lang/std.flow` (modified) — confirmed via `git log --name-only a928628`.
- `flow-lang.Tests/Unit/Phase25/HumanizeGaussianFacts.cs` (modified) — confirmed via `git log --name-only 3cc3a11`.

**Commits claimed in summary:**
- `9c3553e` (Task 1) — present in `git log`.
- `a928628` (Task 2) — present in `git log`.
- `3cc3a11` (Task 3) — present in `git log`.

All claims verified.
