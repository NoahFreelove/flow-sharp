---
phase: 25-gaussian-humanize-last-prng-phase
plan: 01
subsystem: type-system
tags: [note-type, with-helper, velocity, phase-25, wave-1, precondition, defer-06]

# Dependency graph
requires:
  - phase: 22
    provides: "MusicalNoteData.With(...) builder helper (3-param version: onsetOffset, durationOverlap, portamentoMs) — the helper this plan extends"
  - phase: 25-00
    provides: "Phase 25 context, research (§Critical Pre-Existing Bug), patterns (§extend With(...) helper) and decisions D-17/D-18"
provides:
  - "MusicalNoteData.With(...) extended with `double? velocity = null` slot — 4-param overload"
  - "4 xUnit Facts (NoteTypeWithVelocityFacts) pinning field-preservation, null-coalesce, and override semantics for velocity"
  - "D-18 invariant verification gate: TransformFunctions.cs untouched (existing humanize FROZEN) — empty git diff confirms"
affects: [25-02-humanize-gaussian, future-velocity-transforms, transforms-wave]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "With(...) helper one-slot-at-a-time extension (Phase 22 DX-13/DX-14 → Phase 25 DEFER-06 continuation)"
    - "PHASE 25 (DEFER-06) marker comments anchor cross-plan changes inside a frozen file"

key-files:
  created:
    - flow-lang.Tests/Unit/Phase25/NoteTypeWithVelocityFacts.cs
  modified:
    - flow-lang/TypeSystem/SpecialTypes/NoteType.cs

key-decisions:
  - "D-17 anchored: With(...) helper extension is the testable seam; humanizeGaussian calls With(velocity: x) instead of repeating the 12-arg ctor."
  - "D-18 invariant enforced: existing TransformFunctions.cs:866-903 (humanize) NOT modified by this plan — git diff is empty."
  - "New velocity parameter placed at END of With() parameter list to preserve API stability for any positional callers; named-arg callers unaffected."

patterns-established:
  - "Helper one-slot extension: each plan that adds a defaulted-parameter field to MusicalNoteData also extends With(...) with a matching nullable optional parameter."
  - "Null-coalesce passthrough: `velocity ?? Velocity` preserves original value when caller omits the slot — same pattern as Phase 22 OnsetOffset/DurationOverlap/PortamentoMs."
  - "RED→GREEN gate: Task 1 writes Facts that fail with CS1739; Task 2's edit turns them GREEN — proves the contract is what the helper actually delivers."

requirements-completed: [DEFER-06]

# Metrics
duration: 3min
completed: 2026-05-04
---

# Phase 25 Plan 01: Wave 1 Precondition — `MusicalNoteData.With(velocity:)` slot Summary

**Extended Phase 22's `MusicalNoteData.With(...)` builder helper with a `double? velocity = null` slot so Plan 25-02 (humanizeGaussian) can rebuild perturbed notes via `note.With(velocity: x)` without inheriting the latent 12-arg ctor field-drop bug at `TransformFunctions.cs:896-898`.**

## Performance

- **Duration:** 3 min
- **Started:** 2026-05-04T23:04:14Z
- **Completed:** 2026-05-04T23:07:24Z
- **Tasks:** 2
- **Files modified:** 1 (created), 1 (modified)

## Accomplishments

- `MusicalNoteData.With(...)` now accepts an optional `double? velocity = null` parameter; the positional `Velocity` ctor argument is replaced with `velocity ?? Velocity` (null-coalesce passthrough).
- 4 xUnit Facts in `NoteTypeWithVelocityFacts` pin the contract:
  - `With_VelocityNull_PreservesOriginal` — both `note.With()` and `note.With(velocity: null)` keep the original Velocity.
  - `With_VelocitySet_ReturnsNewVelocity` — `note.With(velocity: 0.42)` returns a copy with Velocity=0.42; original unchanged (immutability).
  - `With_VelocityAndOnsetOffset_BothApply` — composition with the existing `onsetOffset` slot works; both fields override, others preserved.
  - `With_VelocitySet_PreservesAll16OtherFields` — bug-prevention regression: every non-velocity field on a richly-populated note is preserved.
- D-18 invariant verified empirically: `git diff flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` is **empty** — existing humanize is FROZEN.
- Phase 18 byte-identical regression: 19/19 GREEN. Phase 22 With() callers: 77/77 GREEN. Full suite: 678/678 GREEN.

## Task Commits

Each task was committed atomically following the plan's TDD RED→GREEN sequence:

1. **Task 1: Add 4 RED xUnit Facts pinning With(velocity:) semantics** — `5efb23f` (test)
   - Wrote `flow-lang.Tests/Unit/Phase25/NoteTypeWithVelocityFacts.cs` with all 4 Facts.
   - Verified RED state: 4 × CS1739 ("no overload accepts named parameter 'velocity'") build errors.
2. **Task 2: Extend MusicalNoteData.With(...) helper with velocity slot — turns Task 1 GREEN** — `b9017fc` (feat)
   - Added `double? velocity = null` parameter at end of `With()` signature.
   - Replaced positional `Velocity` arg with `velocity ?? Velocity`.
   - Two `PHASE 25 (DEFER-06)` marker comments anchor the additions.
   - All 4 NoteTypeWithVelocityFacts pass; full suite 678 GREEN.

_Note: this plan was declared `tdd="true"` per task; the plan-level RED→GREEN gate maps to Task 1 (test commit) → Task 2 (feat commit). No REFACTOR commit was needed — the helper extension is sub-10 lines and self-explanatory._

## Files Created/Modified

- `flow-lang.Tests/Unit/Phase25/NoteTypeWithVelocityFacts.cs` — **created**: 4 Facts (1 file, 106 insertions). Pins `With(velocity:)` semantics. Imports `FlowLang.Core` (SourceLocation), `FlowLang.TypeSystem` (Fraction), `FlowLang.TypeSystem.SpecialTypes` (MusicalNoteData, Articulation), and `Xunit`.
- `flow-lang/TypeSystem/SpecialTypes/NoteType.cs` — **modified** at `With(...)` helper (lines 317-330): +1 parameter, swap `Velocity` → `velocity ?? Velocity`. 5 insertions, 2 deletions.

## Decisions Made

None — followed plan as specified. The plan's `<action>` blocks specified the exact textual diff to apply for both tasks; no judgment calls were required.

## Deviations from Plan

None — plan executed exactly as written.

All acceptance criteria gates green:
- File created (Task 1)
- 4 [Fact] attributes (Task 1)
- 5 `With(velocity:` occurrences in test file (Task 1, ≥4 required)
- 4 CS1739 errors before Task 2, confirming RED state (Task 1)
- 1 `double? velocity = null` line added (Task 2, ≥1 required)
- 1 `velocity ?? Velocity` line added (Task 2, ≥1 required)
- 2 `PHASE 25 (DEFER-06)` marker comments (Task 2, exactly 2 required)
- `git diff TransformFunctions.cs` empty (Task 2, D-18 invariant)
- Phase 18 + Phase 22 + full suite all GREEN (Task 2)

## Issues Encountered

None.

The full test suite log surfaced 12 pre-existing warnings (CS8602/CS8604 nullable reference warnings in `ExpressionEvaluator.cs` and `PolyrhythmFunctions.cs`; xUnit2031/xUnit1051/VSTHRD200 stylistic warnings in Phase 17/19 tests). These are out-of-scope per the SCOPE BOUNDARY rule (not caused by this plan's changes).

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness

- **Plan 25-02 (humanizeGaussian) is unblocked.** It can now call `note.With(velocity: newVel)` to rebuild perturbed notes safely; field preservation across all 17 fields is contractually pinned by `With_VelocitySet_PreservesAll16OtherFields`.
- **D-18 invariant standing:** existing `humanize` (TransformFunctions.cs:866-903) remains FROZEN. Future plans must continue to leave that block untouched per Phase 25 CONTEXT.
- **Pattern continuity:** the With() helper now has 4 slots (onsetOffset, durationOverlap, portamentoMs, velocity). Future Phase 22-style "one-slot-at-a-time" plans should append additional nullable optional parameters at the end of the signature and add a matching null-coalesce in the ctor call.

## Self-Check: PASSED

Verified:
- `flow-lang.Tests/Unit/Phase25/NoteTypeWithVelocityFacts.cs` exists in worktree.
- `flow-lang/TypeSystem/SpecialTypes/NoteType.cs` modified at the `With()` helper.
- Commit `5efb23f` (Task 1 test) found in `git log`.
- Commit `b9017fc` (Task 2 feat) found in `git log`.
- `dotnet test flow-lang.Tests --filter "FullyQualifiedName~NoteTypeWithVelocityFacts"` → 4 Passed, 0 Failed.
- `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase18"` → 19 Passed, 0 Failed.
- `dotnet test flow-lang.Tests --filter "FullyQualifiedName~Phase22"` → 77 Passed, 0 Failed.
- Full suite → 678 Passed, 0 Failed.
- `git diff flow-lang/StandardLibrary/Transforms/TransformFunctions.cs` → empty (D-18 invariant).

---
*Phase: 25-gaussian-humanize-last-prng-phase*
*Plan: 01*
*Completed: 2026-05-04*
