---
phase: 32-full-scala-scl-tuning-loader
plan: 03
subsystem: tuning
tags: [scala, tuning, microtonal, scl, kbm, midi-to-hz, render-path, pattern-A]

# Dependency graph
requires:
  - phase: 32-01
    provides: 5 canonical .scl fixtures + 3 malformed + LICENSE.md attribution; Plan 32-03 references the partch_43 / slendro / carlos_alpha ratio contents inline (synthetic ParsedScala) since Plan 32-02 ships in parallel
  - phase: 32-02 (PARALLEL — same wave)
    provides: authoritative ParsedScala + ScalaKbm + ScalaParser; this plan forward-declares those types via Rule 3 to ship without blocking on a parallel worktree (see Deviations)
provides:
  - ResolvedTuning sealed class with eager 128-entry MIDI→Hz table at construction (D-02)
  - TuningType registered as the 15th SpecialType (specificity 137)
  - RenderTuning extended with ResolvedTuning? Custom = null (5th positional param per D-03)
  - PitchConversion.NoteToFrequency Custom branch + Pitfall 3 mutual-exclusion guard
  - 21 Phase 32 unit-test Facts (14 TuningType + 7 RenderTuningExtension)
affects: [32-04 (loadScala builtin), 32-05 (TuningStack), 32-06 (tuning context block), 32-07 (tutorial)]

# Tech tracking
tech-stack:
  added: []  # no new external libraries — hand-rolled C# only, per CLAUDE.md Guiding Principle
  patterns:
    - "Eager pre-compute static table at instance ctor (mirrors Phase 23 ChromaticRatioTable static-ctor pattern, scoped to instance per D-02)"
    - "Scale-step semantics (NOT 12-TET semitone semantics) for MIDI→Hz table walking — anchor by construction at kbm.ReferenceNote == kbm.ReferenceHz"
    - "Pattern A render-time entry point preserved — Custom branch at TOP of NoteToFrequency; all 13 synthesizer call sites untouched"
    - "Pitfall 3 mutual-exclusion: early Custom-branch return + defense-in-depth AND-with-null on the 12-TET short-circuit predicate"
    - "TDD per-task: RED commit (failing tests) → GREEN commit (impl) — 4 commits total"

key-files:
  created:
    - "flow-lang/StandardLibrary/Audio/Tuning/ResolvedTuning.cs"
    - "flow-lang/TypeSystem/SpecialTypes/TuningType.cs"
    - "flow-lang.Tests/Unit/Phase32/TuningTypeFacts.cs"
    - "flow-lang.Tests/Unit/Phase32/RenderTuningExtensionFacts.cs"
  modified:
    - "flow-lang/StandardLibrary/Audio/Tuning/RenderTuning.cs"
    - "flow-lang/StandardLibrary/Audio/PitchConversion.cs"

key-decisions:
  - "ParsedScala + ScalaKbm forward-declared inside ResolvedTuning.cs against Plan 32-02's documented contract — both plans run in parallel worktrees (Wave 1) and cannot see each other's authoritative ScalaParser.cs / ScalaKbm.cs. On wave merge the orchestrator must resolve the duplicate type names by removing these forward declarations (deviation Rule 3, blocking issue)"
  - "Scale-step (NOT 12-TET semitone) algorithm for MidiToHz population — the plan's <algorithm_semantics> block supersedes RESEARCH.md's incorrect '440 / 2^(9/12) ≈ 261.6256' simplification under non-12-TET fixtures"
  - "Cross-fixture anchor invariant: MidiToHz[kbm.ReferenceNote] == kbm.ReferenceHz EXACTLY by construction (default KBM: MidiToHz[69] ≈ 440.0 for partch_43, slendro, carlos_alpha — three Facts pin this)"
  - "Per-step ratio Facts: MidiToHz[61]/MidiToHz[60] == 81/80 exactly (Partch step 1) — internal-consistency invariant testable without pinning a 12-TET-derived value to MidiToHz[60]"
  - "Pitfall 3 mutual-exclusion guard at PitchConversion.cs:89 — `tuning.Custom is null && tuning.System == EqualTemperament` predicate ensures the byte-identical fast path NEVER silently swallows a Custom override"

patterns-established:
  - "Pattern: Phase 23 Pattern A preserved — Custom branch is added at the TOP of NoteToFrequency, NOT as a parallel API; all 13 synthesizer call sites stay untouched"
  - "Pattern: TDD per-task with explicit RED commit (failing test) → GREEN commit (impl) — verifies tests fail BEFORE implementation lands"
  - "Pattern: forward-declared placeholder types for parallel-wave dependencies — clearly marked with merge-resolution comment so the orchestrator can delete on merge"

requirements-completed: [SPEC-1, SPEC-5]

# Metrics
duration: ~25min
completed: 2026-05-14
---

# Phase 32 Plan 03: ResolvedTuning + Render Path Custom Branch Summary

**Runtime tuning data path. ResolvedTuning eagerly precomputes the 128-entry MIDI→Hz table at construction (CONTEXT D-02). RenderTuning gains the optional Custom field (D-03). PitchConversion routes through Custom.MidiToHz when non-null, preserving Phase 23 Pattern A at the single entry point and the byte-identical 12-TET short-circuit when Custom == null. Pitfall 3 mutual-exclusion guard added as defense-in-depth.**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-05-14 (worktree spawn)
- **Completed:** 2026-05-14
- **Tasks:** 2 / 2
- **Files created:** 4 (2 source + 2 tests)
- **Files modified:** 2 (RenderTuning.cs + PitchConversion.cs)
- **Test Facts added:** 21 (14 TuningType + 7 RenderTuningExtension)
- **Phase 23 regression:** 91/91 GREEN (critical contract preserved)

## Accomplishments

### Task 1: ResolvedTuning + TuningType (RED commit `c9f12c6` + GREEN commit `cc54bc2`)

- **`ResolvedTuning` sealed class** eagerly populates `double[128] _midiToHz` at construction per D-02. Algorithm walks scale steps relative to `kbm.MiddleNote` and anchors at `kbm.ReferenceNote` so `MidiToHz[refNote] == kbm.ReferenceHz` EXACTLY by construction. Default KBM (refNote=69, refHz=440.0) thus pins `MidiToHz[69] = 440.0` for every loaded tuning — the cross-fixture anchor invariant.
- **All 6 interface fields exposed** per the plan's `<interfaces>` contract:
  - `Description` — verbatim first non-comment line from .scl (D-04)
  - `StepCents` (length N-1) + `PeriodCents` (separate field) per D-10
  - `Ratios` — original integer pair preserved for ratio-input steps (D-11)
  - `Kbm` — always present per D-05
  - `MidiToHz` — 128-entry eager lookup (D-02)
- **`ToString()` override** per D-04: `Tuning("<description>", N steps, period XXX.XX¢)` where N = `StepCents.Count + 1`.
- **`TuningType`** registered as the 15th SpecialType. Specificity 137 (slotted between Sequence=134 and Song=140 per RESEARCH). Singleton + reference-equality per Claude's Discretion (no Equals/GetHashCode override).
- **14 Facts in `TuningTypeFacts.cs`** covering:
  - TuningType singleton identity + Name=`"Tuning"` + Specificity=137 (5 Facts)
  - `IsCompatibleWith` self + NOT Song (2 Facts)
  - Cross-fixture anchor `MidiToHz[69]≈440.0` for partch_43, slendro, carlos_alpha (3 Facts)
  - Partch per-step ratio: step 1 = 81/80, step 2 = 33/32 (2 Facts) — these are the testable internal-consistency invariants that replace the broken 12-TET MidiToHz[60]=261.6256 assertion
  - **SPEC-5 acceptance**: carlos_alpha period wrap `MidiToHz[78]/MidiToHz[60] ≈ 2^(1404/1200) ≈ 3.2003` within ±0.1¢ (1 Fact)
  - Negative cents → descending pitch (D-09, 1 Fact)
  - `Description` + `ToString` format (2 Facts)

### Task 2: RenderTuning.Custom + PitchConversion branch (RED commit `bee9a03` + GREEN commit `283b556`)

- **`RenderTuning` extended** with `ResolvedTuning? Custom = null` as the 5th positional parameter at the END of the record-struct's parameter list. Existing 4-arg call sites (SongRenderer:184, `RenderTuning.Default` factory, ≥ 4 Phase 23 test sites) compile unchanged because the default value `null` triggers the byte-identical 12-TET short-circuit.
- **`PitchConversion.NoteToFrequency` extended** with:
  - NEW first branch at top of function body: `if (tuning.Custom is not null) { ... read tuning.Custom.MidiToHz[midi] ... }`. Bounds-clamps midi < 0 || > 127 to 0.0; applies cent offset via `RatioMath.CentOffsetMultiplier` when `note.CentOffset.HasValue`.
  - The existing 12-TET short-circuit predicate at `:89` hardened with `tuning.Custom is null && ...` — **Pitfall 3 mutual-exclusion guard**. The early Custom-branch return already handles the override case correctly; the AND-with-null is defense-in-depth so a future refactor (e.g. dropping the early return, restructuring dispatch) doesn't reintroduce the silent-swallow regression.
- **7 Facts in `RenderTuningExtensionFacts.cs`** covering:
  - `RenderTuning.Default.Custom == null` (default-Custom API contract)
  - 4-arg RenderTuning compiles unchanged with the new 5th param (binary-compat check)
  - Null-Custom path: byte-identical 12-TET (MIDI 60 ≈ 261.6256 Hz — the 12-TET answer, correctly asserted here on the 12-TET path)
  - Non-null Custom reads `resolved.MidiToHz[midi]` exactly (array equality, NOT a hardcoded number)
  - Cent offset applied through the new branch (5¢ on top of `MidiToHz[60]`)
  - Out-of-range MIDI returns 0.0 (bounds clamp)
  - **Pitfall 3 mutual-exclusion guard** Fact `PitchConversion_CustomOverridesSystem_PitfallGuard`: a hand-constructed `new RenderTuning(EqualTemperament, Major, 'C', 0, customNonNull)` MUST take the Custom branch, NOT the 12-TET short-circuit

## Task Commits

| # | Phase  | Hash      | Type | Description                                                  |
|---|--------|-----------|------|--------------------------------------------------------------|
| 1 | RED    | `c9f12c6` | test | failing Facts for ResolvedTuning + TuningType                |
| 2 | GREEN  | `cc54bc2` | feat | ResolvedTuning + TuningType — eager MIDI→Hz table (Task 1)   |
| 3 | RED    | `bee9a03` | test | failing Facts for RenderTuning.Custom + PitchConversion      |
| 4 | GREEN  | `283b556` | feat | RenderTuning.Custom + PitchConversion branch — Pitfall 3 guard (Task 2) |

_The orchestrator will add the metadata commit (this SUMMARY.md) after wave merge._

## Files Created/Modified

### Created
- `flow-lang/StandardLibrary/Audio/Tuning/ResolvedTuning.cs` (225 lines) — sealed class + the Wave-1 parallel forward declarations of `ParsedScala` and `ScalaKbm`
- `flow-lang/TypeSystem/SpecialTypes/TuningType.cs` (27 lines) — 15th SpecialType, specificity 137
- `flow-lang.Tests/Unit/Phase32/TuningTypeFacts.cs` (263 lines) — 14 Facts
- `flow-lang.Tests/Unit/Phase32/RenderTuningExtensionFacts.cs` (168 lines) — 7 Facts

### Modified
- `flow-lang/StandardLibrary/Audio/Tuning/RenderTuning.cs` (+12 lines) — Custom field add, Default factory unchanged
- `flow-lang/StandardLibrary/Audio/PitchConversion.cs` (+24/-1 lines) — Custom branch at top + Pitfall 3 guard on 12-TET short-circuit

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 — Blocking Issue] Forward-declared `ParsedScala` and `ScalaKbm` inside `ResolvedTuning.cs`**

- **Found during:** Task 1 RED (test file references `ParsedScala` + `ScalaKbm` types that ship in Plan 32-02; both plans are in Wave 1 and run in parallel worktrees, so neither sees the other's authoritative files at build time)
- **Issue:** Plan 32-03's `ResolvedTuning` ctor signature is `(ParsedScala scl, ScalaKbm kbm)`. Plan 32-02 owns both types in its `files_modified` list (`ScalaParser.cs` and `ScalaKbm.cs`). Running in parallel worktrees, my agent cannot see Plan 32-02's work; without forward declarations, my code does not compile and my Facts cannot run.
- **Fix:** Added `public sealed record ParsedScala(...)` and `public sealed record ScalaKbm(...)` co-located inside `ResolvedTuning.cs` against the documented contract from `32-02-PLAN.md` §`<interfaces>`. Field shapes match Plan 32-02's contract EXACTLY (verified by re-reading the plan).
- **Merge resolution:** On wave merge, the orchestrator will see two declarations of each type (mine in `ResolvedTuning.cs`, Plan 32-02's in `ScalaKbm.cs` / `ScalaParser.cs`). Resolution: delete the forward declarations in `ResolvedTuning.cs` (lines 22-58) and let `ResolvedTuning` consume Plan 32-02's authoritative versions via the existing `using FlowLang.StandardLibrary.Audio.Tuning;` namespace. The contract shapes are identical so no public API breakage results.
- **Files modified:** `flow-lang/StandardLibrary/Audio/Tuning/ResolvedTuning.cs` (lines 22-58 added)
- **Commit:** `cc54bc2`

### Plan-Spec Adherence

- All 13 acceptance-criteria grep audits pass (verified inline):
  - `public sealed class ResolvedTuning` present
  - `public sealed class TuningType` + `public static TuningType Instance` present
  - All 6 ResolvedTuning interface fields exposed (Description, MidiToHz, PeriodCents, StepCents, Ratios, Kbm)
  - `ResolvedTuning? Custom` field in RenderTuning
  - `tuning.Custom is not null` branch in PitchConversion (line 66)
  - `Custom is null && tuning.System` Pitfall 3 guard in PitchConversion (line 89)
- 21 Phase 32 Facts pass (plan minimum: 13 + 7 = 20)
- Phase 23 regression sweep: 91/91 GREEN (critical contract)
- NO Fact asserts a 12-TET-derived value for MidiToHz[60] under a non-12-TET fixture (grep audit clean — the only `261.6256` reference in a positive Assert is on the 12-TET path, where the value is correct; the only other reference is a `Assert.NotEqual(261.6256, ...)` Pitfall-3-guard assertion)

## Authentication Gates Encountered

None. Plan 32-03 is pure C# implementation + xUnit Facts; no auth required.

## Acceptance Verification

### Task 1 acceptance (`<acceptance_criteria>`)
- ✅ `flow-lang/StandardLibrary/Audio/Tuning/ResolvedTuning.cs` contains `public sealed class ResolvedTuning`
- ✅ `flow-lang/TypeSystem/SpecialTypes/TuningType.cs` contains `public sealed class TuningType` AND `public static TuningType Instance`
- ✅ `dotnet test --filter "FullyQualifiedName~TuningTypeFacts" -v minimal` exits 0; 14 Facts passed (≥ 13 required)
- ✅ SPEC-5 acceptance: `ResolvedTuning_CarlosAlpha_NonOctaveWrap` Fact asserts cents ratio within ±0.1¢
- ✅ Cross-fixture anchor: 3 Facts asserting `MidiToHz[69] ≈ 440.0` (Partch, slendro, carlos_alpha)
- ✅ Per-step ratio Facts: 2 Facts (81/80 and 33/32 for Partch)
- ✅ NO 12-TET-derived assertion for MidiToHz[60] under non-12-TET fixture (grep audit clean)
- ✅ All 6 ResolvedTuning interface fields grep-verified

### Task 2 acceptance (`<acceptance_criteria>`)
- ✅ `grep -n 'ResolvedTuning? Custom' flow-lang/StandardLibrary/Audio/Tuning/RenderTuning.cs` returns ≥ 1 match (1 found)
- ✅ `grep -n 'tuning.Custom is not null' flow-lang/StandardLibrary/Audio/PitchConversion.cs` returns ≥ 1 match (1 found at line 66)
- ✅ `grep -n 'Custom is null && tuning.System' flow-lang/StandardLibrary/Audio/PitchConversion.cs` returns ≥ 1 match (1 found at line 89)
- ✅ `dotnet test --filter "FullyQualifiedName~RenderTuningExtensionFacts"` exits 0; 7 Facts passed (≥ 7 required)
- ✅ `dotnet test --filter "FullyQualifiedName~Phase23"` exits 0; 91/91 passing (critical regression gate)
- ✅ Pitfall 3 explicit Fact `PitchConversion_CustomOverridesSystem_PitfallGuard` passes

### Overall plan verification (`<verification>`)
- ✅ `dotnet build` clean (0 errors, 13 pre-existing warnings)
- ✅ TuningTypeFacts ≥ 13 Facts GREEN (14 ran)
- ✅ RenderTuningExtensionFacts ≥ 7 Facts GREEN (7 ran)
- ✅ Phase 23 sub-suite 100% GREEN
- ✅ All 6 ResolvedTuning interface fields exposed
- ✅ PitchConversion's new branch reads `tuning.Custom.MidiToHz`; bounds clamp present; cent offset applied
- ✅ NO 12-TET-derived MidiToHz[60] assertion appears anywhere in Phase 32 unit tests under non-12-TET fixtures

## Threat Model Adherence

This plan's PLAN.md does not declare an explicit `<threat_model>` block — the runtime data path is internal to the .NET process and consumes types built by upstream plans (32-01 fixtures, 32-02 parsers). No new trust boundary introduced. Custom branch bounds-clamps MIDI to 0..127 (defense against out-of-range degree calculation overflow). No information disclosure surface added.

## Threat Flags

| Flag | File | Description |
|------|------|-------------|
| (none) | — | No new network endpoints, auth paths, file access patterns, or schema changes at trust boundaries. The Custom branch reads from an in-memory `double[128]` populated at construction; bounds-clamped 0..127. |

## Known Stubs

**1. `ParsedScala` + `ScalaKbm` forward declarations in `ResolvedTuning.cs`** (lines 22-58)

These types ship in their final shape (matching Plan 32-02's `<interfaces>` contract) but are placeholders for parallel-wave coordination. Plan 32-02's authoritative `ScalaKbm.cs` will land in the same wave merge; resolution is to delete the forward declarations in `ResolvedTuning.cs` and route through 32-02's files. Plan 32-04 (`(loadScala)` builtin) is the consumer that will exercise this end-to-end via `ScalaParser.Parse` + `ScalaKbmParser.Default`.

No other stubs. The MIDI→Hz table is fully populated; the type system registration is complete; the render-path branch is functional and exercised by 7 Facts.

## TDD Gate Compliance

Plan 32-03 has `tdd="true"` on both tasks. Gate sequence verified:

- **Task 1**: RED commit `c9f12c6` (test only — builds RED with CS0246) → GREEN commit `cc54bc2` (impl makes tests pass)
- **Task 2**: RED commit `bee9a03` (test only — builds RED with CS1061 + CS1729) → GREEN commit `283b556` (impl makes tests pass)

Each RED commit introduces failing tests; each GREEN commit makes them pass. No skipped-RED gates.

## Pre-existing Failures (Out of Scope per Executor Rules)

The full `dotnet test` suite has 26 pre-existing failures unrelated to Plan 32-03:
- 24 × `Phase28.PerSynthArticulationTests.PerSynthArticulation_NormalVsArticulated_FFTCosineDifferentiable` (FFT-based articulation differentiation tests across sax/piano/bell/flute/strings/brass × Accent/Legato/Tenuto/Sforzando)
- 2 × `Phase28.RagtimeFixtureTests.Ragtime_*_RmsRegression` (RMS regression vs baselines)

Verified pre-existing by `git stash` + retest cycle: both runs (with and without Plan 32-03 changes) show identical 26-failure count and identical failing-test names. These are out of scope per executor rules (logged here for the deferred-items pass, not fixed).

## Self-Check: PASSED

All 6 claimed file paths exist on disk:
- `flow-lang/StandardLibrary/Audio/Tuning/ResolvedTuning.cs` — FOUND
- `flow-lang/TypeSystem/SpecialTypes/TuningType.cs` — FOUND
- `flow-lang.Tests/Unit/Phase32/TuningTypeFacts.cs` — FOUND
- `flow-lang.Tests/Unit/Phase32/RenderTuningExtensionFacts.cs` — FOUND
- `flow-lang/StandardLibrary/Audio/Tuning/RenderTuning.cs` — FOUND (modified)
- `flow-lang/StandardLibrary/Audio/PitchConversion.cs` — FOUND (modified)
- `.planning/phases/32-full-scala-scl-tuning-loader/32-03-SUMMARY.md` — FOUND (this file)

All 4 task commits exist in git log:
- `c9f12c6` (Task 1 RED) — FOUND
- `cc54bc2` (Task 1 GREEN) — FOUND
- `bee9a03` (Task 2 RED) — FOUND
- `283b556` (Task 2 GREEN) — FOUND
