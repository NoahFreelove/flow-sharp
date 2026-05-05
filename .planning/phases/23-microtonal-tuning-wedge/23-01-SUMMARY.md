---
phase: 23-microtonal-tuning-wedge
plan: 01
subsystem: audio
tags: [microtonal, tuning, ratio-tables, just-intonation, pythagorean, render-time, closed-enum]

# Dependency graph
requires:
  - phase: 21-pragmas-h-as-b
    provides: PragmaRegistry closed-set growth pattern (analog for TuningSystem closed enum)
  - phase: 18-fractions-tuplets
    provides: xUnit Fact-per-canary template (FractionTests shape replicated for ratio facts)
provides:
  - flow-lang/StandardLibrary/Audio/Tuning/ namespace with 6 production types
  - TuningSystem closed enum (EqualTemperament default, JustIntonation, Pythagorean) — 3-value closed set
  - Mode closed enum (Major default + 6 church modes) — 7-value closed set
  - RenderTuning readonly record struct — Pattern A locked render-time payload
  - ChromaticRatioTable spelling-aware ratio map keyed on (Letter, Alteration)
  - TuningTables.Tables — 14-entry static dictionary keyed by (TuningSystem, Mode)
  - RatioMath helpers — TonicHzFromKey + CentOffsetMultiplier
  - 4 xUnit Fact suites pinning canonical Wikipedia + Mudcat ratios (36 Facts total)
affects: [23-02, 23-03, 23-04, 23-05, phase-24-scale-linting]

# Tech tracking
tech-stack:
  added: []  # Pure additions; no new external deps
  patterns:
    - "Pattern A render-time payload — RenderTuning value object threaded through synthesizers (analog to SongRenderer.RenderSection per-section bpm/pan/gain/rt60)"
    - "Closed-enum house style — TuningSystem and Mode mirror TokenType/PragmaRegistry shape"
    - "Spelling-aware ratio key — (char Letter, int Alteration) tuple distinguishes Eb (E,-1) from D# (D,+1) per D-09"
    - "Static-constructor dictionary build — defers Tables population until per-table fields fully initialized (avoids forward-ref nulls)"
    - "Canonical-source citation in doc comments — Wikipedia 'Five-limit tuning', Mudcat Olson 'Just Intonation Music Scales', Wikipedia 'Pythagorean tuning'"

key-files:
  created:
    - flow-lang/StandardLibrary/Audio/Tuning/TuningSystem.cs
    - flow-lang/StandardLibrary/Audio/Tuning/Mode.cs
    - flow-lang/StandardLibrary/Audio/Tuning/RenderTuning.cs
    - flow-lang/StandardLibrary/Audio/Tuning/ChromaticRatioTable.cs
    - flow-lang/StandardLibrary/Audio/Tuning/TuningTables.cs
    - flow-lang/StandardLibrary/Audio/Tuning/RatioMath.cs
    - flow-lang.Tests/Unit/Phase23/TuningRatioFacts.cs
    - flow-lang.Tests/Unit/Phase23/TuningModeShiftFacts.cs
    - flow-lang.Tests/Unit/Phase23/SpellingAwareTuningFacts.cs
    - flow-lang.Tests/Unit/Phase23/CentOffsetAdditivityFacts.cs
  modified: []  # Wave 1 is pure additions — no existing-code touch (byte-identical contract preserved)

key-decisions:
  - "Pattern A locked over Pattern B — RenderTuning value object threads through synthesizer signatures (RESEARCH §Architecture Patterns Pattern 1; Pattern B's MusicalContext.Current static accessor has zero codebase analogs and would introduce global mutable state)."
  - "Default enum members place EqualTemperament FIRST and Major FIRST so default(TuningSystem) and default(Mode) match the byte-identical 12-TET / silent C-major-default fallback semantics (D-08 + D-02)."
  - "ChromaticRatioTable is spelling-aware (D-09) — keyed on (Letter, Alteration), so Eb=6/5 and D#=75/64 are stored as distinct entries even though they collide in 12-TET."
  - "TuningTables.Tables built via static constructor (not field initializer) so the dictionary's value references resolve AFTER each per-table field initializer has run — surfaced by the RED suite as NullReferenceException, fixed inline (Rule 1 deviation)."
  - "EqualTemperament has NO entry in TuningTables.Tables — consumers MUST short-circuit on tuning.System == EqualTemperament before LookupRatio (Pitfall 6 byte-identical fast path)."

patterns-established:
  - "Closed-enum + static-dict-of-tables pattern (parallels PragmaRegistry.KnownPragmas in Phase 21)"
  - "Spelling-aware lookup key tuple — (char Letter, int Alteration) replaces semitone offset for ratio tables"
  - "Static-constructor initialization for static readonly dictionaries that reference other static readonly fields in the same class"
  - "Canonical-source citation in XML doc comments for tampering detection (T-23-01-01 mitigation)"

requirements-completed: [MICR-01]

# Metrics
duration: 5m 22s
completed: 2026-05-04
---

# Phase 23 Plan 01: Microtonal Foundation Summary

**Closed-enum tuning identifiers + 14 mode-keyed ratio tables (7 JI + 7 Pythagorean) with canonical Wikipedia/Mudcat citations, plus the Pattern A `RenderTuning` value object that Wave 2 will thread through synthesizers — 36 xUnit Facts pin every canary ratio (5/4 JI third, 81/64 Pythagorean third, Eb≠D# spelling distinction, cent additivity).**

## Performance

- **Duration:** 5m 22s
- **Started:** 2026-05-04T00:35:07Z
- **Completed:** 2026-05-04T00:40:29Z
- **Tasks:** 2
- **Files created:** 10 (6 production + 4 test)
- **Files modified:** 0

## Accomplishments

- Six production types under `flow-lang/StandardLibrary/Audio/Tuning/` compile cleanly and ship the entire mathematical foundation for Phase 23 microtonal tuning.
- 14 chromatic ratio tables instantiated via `ChromaticRatioTable.Build` — every ratio sourced verbatim from Wikipedia (Five-limit tuning, Pythagorean tuning) and Mudcat Olson (Just Intonation Music Scales) with citations in XML doc comments.
- 36 xUnit Facts authored from canonical sources, all GREEN on first execution after a single inline static-constructor fix (Pass-1 RED → GREEN with one Rule 1 auto-fix).
- Pattern A locked: `RenderTuning` readonly record struct shipped; `MusicalContext.Current` static accessor explicitly NOT introduced (verified `grep -c "MusicalContext\.Current" flow-lang/` == 0).
- Byte-identical regression contract preserved: `ByteIdenticalTutorialTests` and `ByteIdenticalShowcaseTests` (4 of 4) still GREEN — Wave 1 made zero edits to existing code paths.

## Task Commits

Each task was committed atomically:

1. **Task 1: Closed enums + RenderTuning + ChromaticRatioTable scaffolding (Pattern A locked)** — `b6b916b` (feat)
2. **Task 2: TuningTables (14 ratio tables) + RatioMath + Pass-1 RED Facts** — `39ef570` (feat)

## Files Created

- `flow-lang/StandardLibrary/Audio/Tuning/TuningSystem.cs` — closed enum, 3 members, EqualTemperament first (default).
- `flow-lang/StandardLibrary/Audio/Tuning/Mode.cs` — closed enum, 7 church modes, Major first (default).
- `flow-lang/StandardLibrary/Audio/Tuning/RenderTuning.cs` — `readonly record struct` Pattern A payload + `Default` static property.
- `flow-lang/StandardLibrary/Audio/Tuning/ChromaticRatioTable.cs` — `sealed record` keyed on (Letter, Alteration); `Build(naturals, sharps, flats)` factory; throws `InvalidOperationException` on missing diatonic naturals.
- `flow-lang/StandardLibrary/Audio/Tuning/TuningTables.cs` — 14 static `ChromaticRatioTable` fields + static-constructor-built `Tables` dictionary + `LookupRatio` accessor with full Wikipedia + Mudcat citations.
- `flow-lang/StandardLibrary/Audio/Tuning/RatioMath.cs` — `TonicHzFromKey` (delegates to `PitchConversion.NoteToFrequency`) + `CentOffsetMultiplier` per D-10 cent-additive math.
- `flow-lang.Tests/Unit/Phase23/TuningRatioFacts.cs` — 14 Facts pinning canary ratios (5/4 JI third, 81/64 Pythagorean third, etc.).
- `flow-lang.Tests/Unit/Phase23/TuningModeShiftFacts.cs` — 14 Theory rows verifying canonical scale-degree shape per D-03.
- `flow-lang.Tests/Unit/Phase23/SpellingAwareTuningFacts.cs` — 4 Facts pinning Eb≠D# distinction (D-09) + EqualTemperament short-circuit invariant (Pitfall 6).
- `flow-lang.Tests/Unit/Phase23/CentOffsetAdditivityFacts.cs` — 4 Facts on cent-additive math (D-10) including the `JI fifth + 5c` composition canary.

## Decisions Made

- **Pattern A vs Pattern B**: Pattern A (RenderTuning value object) locked over Pattern B (static MusicalContext.Current accessor) because Pattern B has zero codebase analogs (`grep -c "MusicalContext\.Current" flow-lang/` == 0) while Pattern A mirrors the established `SongRenderer.RenderSection` per-section bpm/pan/gain/rt60 resolution. This was already locked in CONTEXT D-08 / RESEARCH §Pitfall 1; execution confirmed it's the only sane choice given the codebase shape.
- **Default ordering**: `EqualTemperament` first in `TuningSystem` and `Major` first in `Mode` so `default(TuningSystem)` and `default(Mode)` short-circuit to the byte-identical 12-TET path with C-major fallback (D-02 + D-08 + Pitfall 6 mitigation).
- **Spelling-aware key tuple**: `(char Letter, int Alteration)` instead of semitone offset — required for D-09 Eb (6/5 = 1.200) ≠ D# (75/64 = 1.171875) distinction in JI; the 12-TET equivalence breaks under non-equal tunings.
- **Static-constructor table build**: After Task 2 RED surfaced NullReferenceException at LookupRatio (forward-reference race in static readonly field initialization), moved the `Tables` dictionary build into a static constructor so all per-mode field initializers run first. Documented inline; Rule 1 deviation.
- **EqualTemperament absent from Tables**: No `(EqualTemperament, *)` entry exists by design — `LookupRatio` throws `KeyNotFoundException` when called with EqualTemperament, enforcing the Pitfall 6 invariant that callers MUST short-circuit before touching the ratio path.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 — Bug] Static initialization ordering in TuningTables**

- **Found during:** Task 2 (initial test run after writing all 6 files)
- **Issue:** The `Tables` static readonly field was declared and initialized FIRST in textual order, then `JustIonian`...`PythLocrian` were declared after. Per C# semantics, static readonly fields initialize in textual declaration order; when the `Tables` dictionary literal evaluated `JustIonian` / `JustAeolian` / etc., those fields were still at their default `null` value. The dictionary held 14 entries with null values. `Tables_HasExactly14Entries` passed (count == 14) but every `LookupRatio` call threw `NullReferenceException` because `table.Lookup(...)` was being invoked on a null `ChromaticRatioTable`.
- **Fix:** Removed the field initializer on `Tables`, declared it without an initializer (still `static readonly`), and added a `static TuningTables()` constructor that builds the dictionary. Static constructors run AFTER all field initializers, so by the time the constructor populates `Tables`, every per-mode field has been fully initialized. Added an XML doc comment on the field documenting why the static constructor is necessary.
- **Files modified:** `flow-lang/StandardLibrary/Audio/Tuning/TuningTables.cs`
- **Verification:** All 36 Phase23 Facts GREEN after the fix. Build clean (0 errors, no new warnings vs baseline). Test re-run: 36 passed, 0 failed.
- **Committed in:** `39ef570` (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** Single inline correction. No scope creep. The original plan's intent (static dictionary keyed by (TuningSystem, Mode)) is preserved — only the initialization mechanics changed.

## Issues Encountered

None besides the static-init ordering bug documented above. The plan's 14 ratio tables transcribed verbatim from canonical sources matched Pass-1 reality exactly — once the null reference was resolved, all 36 Facts passed on the first re-run. Zero divergence between authored Facts and table values, confirming Phase 18-22 RED-then-GREEN precedent.

## TDD Gate Compliance

The plan's `<task type="auto" tdd="true">` markers indicate per-task TDD intent, but both tasks bundle test + production code in a single commit (the test files reference public API that only exists once production code lands). This is the established Phase 18-22 pattern for "Pass-1 RED authored from canonical sources" — the Facts encode the contract, production code satisfies it, and they ship together. Two `feat(...)` commits cover both production + test additions. No separate `test(...)` RED commit was created since the canonical-source-driven authoring approach makes RED→GREEN nearly atomic (and a separate RED commit would not build because the production types don't yet exist at that point).

## Next Phase Readiness

**Ready for Wave 2 (Plan 23-02):**
- `RenderTuning` shape is locked. Wave 2 can mechanically update synthesizer signatures from `(MusicalNoteData note, ...)` → `(MusicalNoteData note, RenderTuning tuning, ...)`.
- `TuningTables.LookupRatio(system, mode, letter, alteration)` is the single entry point — Wave 2's `PitchConversion.NoteToFrequency` overload calls it after the EqualTemperament short-circuit.
- `RatioMath.TonicHzFromKey` and `RatioMath.CentOffsetMultiplier` are reusable building blocks for Wave 2's ratio composition: `freq = TonicHzFromKey(...) * LookupRatio(...) * CentOffsetMultiplier(cents)`.
- ByteIdentical regression suite GREEN — Wave 2 can rely on the contract being intact at the start of its work.

**Open questions deferred to Wave 2:**
- `PitchConversion.NoteToFrequency(MusicalNoteData note, RenderTuning tuning)` overload signature: should it be a NEW overload alongside the existing 1-arg / 3-arg forms (preserves byte-identical fast path when `RenderTuning.Default` is passed) or replace the 1-arg form (cleaner but risks accidental missed call sites)? Plan 23-02's research recommends NEW overload + EqualTemperament short-circuit at the top.
- Octave-displacement math for tonic-relative ratios: when a note's octave differs from the tonic's, does the ratio multiplier still anchor at `TonicHzFromKey(tonicLetter, tonicAlteration, noteOctave)` (octave-relative anchor) or `TonicHzFromKey(tonicLetter, tonicAlteration, fixedReferenceOctave) * 2^(noteOctave - refOctave)` (fixed anchor with octave doubling)? Both produce the same Hz given exact octave doubling but the former is cleaner per RESEARCH; should be confirmed in Wave 2's implementation.
- Whether Wave 2 should ship a `TuningSystem`/`Mode` `Parser` (e.g., for `enable justIntonation;` pragma resolution into the enum) or whether that lands in the pragma-bridge wave (23-03 / 23-04).

**MICR-01 status caveat:** The plan's frontmatter claims `requirements: [MICR-01]`, but MICR-01's acceptance criterion ("`enable justIntonation; ...` followed by `play(C4 E4)` produces frequency ratio 5:4") is only fully satisfiable once Wave 2 wires `TuningTables.LookupRatio` through `PitchConversion` and Wave 3 routes it via the pragma bridge. This SUMMARY marks MICR-01 per the plan's instruction; downstream waves (23-02..23-05) also claim it and will re-mark idempotently. The verifier should treat MICR-01 as "foundation-laid in 23-01, end-to-end-acceptance pending later waves."

## Self-Check

Verifying claims before finalizing:

**Files exist:**
- FOUND: flow-lang/StandardLibrary/Audio/Tuning/TuningSystem.cs
- FOUND: flow-lang/StandardLibrary/Audio/Tuning/Mode.cs
- FOUND: flow-lang/StandardLibrary/Audio/Tuning/RenderTuning.cs
- FOUND: flow-lang/StandardLibrary/Audio/Tuning/ChromaticRatioTable.cs
- FOUND: flow-lang/StandardLibrary/Audio/Tuning/TuningTables.cs
- FOUND: flow-lang/StandardLibrary/Audio/Tuning/RatioMath.cs
- FOUND: flow-lang.Tests/Unit/Phase23/TuningRatioFacts.cs
- FOUND: flow-lang.Tests/Unit/Phase23/TuningModeShiftFacts.cs
- FOUND: flow-lang.Tests/Unit/Phase23/SpellingAwareTuningFacts.cs
- FOUND: flow-lang.Tests/Unit/Phase23/CentOffsetAdditivityFacts.cs

**Commits exist:**
- FOUND: b6b916b (Task 1 — Tuning scaffolding)
- FOUND: 39ef570 (Task 2 — 14 ratio tables + 36 Facts GREEN)

## Self-Check: PASSED

---
*Phase: 23-microtonal-tuning-wedge*
*Completed: 2026-05-04*
