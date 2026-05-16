---
phase: 33-sfz-orchestral-sampler
plan: 02
subsystem: audio
tags: [sfz, sampler, type-system, runtime-state, flow-config, value-types]

# Dependency graph
requires:
  - phase: 32-full-scala-scl-tuning-loader
    provides: TuningType / ScalaParseException / Value.Tuning analog patterns mirrored verbatim by SfzType / SfzParseException / Value.Sfz
  - phase: 30-flow-config
    provides: FlowConfigPoco snake_case auto-mapping (the new sfz_root TOML key needs zero FlowConfigLoader edit)
  - phase: 26
    provides: Symbol primitive type and per-context SymbolInternTable (the SFZ-surface fields cluster directly beneath it)
provides:
  - SfzData / SfzRegion / SfzLoopMode / SfzParseException — immutable parser-output shape consumed by Plans 33-04/05/06/07
  - SfzType — first-class music value type (specificity 150) consumed by Value.Sfz factory + the typed-variable binding surface
  - ExecutionContext.SfzEnabled / SfzInstruments / SfzPatchRegistry / SfzDiagnostics / ResolvedSfzRoot — runtime-state surface read by every other Phase 33 plan
  - FlowConfigPoco.SfzRoot — composer-configured VSCO-CE library root, consumed by Plan 33-05 loadSfz
  - Value.Sfz(SfzData) — the factory that wraps parser output for first-class language access
affects: [33-03, 33-04, 33-05, 33-06, 33-07, 34-symphony-showcase]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Sealed-record immutable parser-output models (SfzData/SfzRegion mirror Phase 32's ParsedScala/ResolvedTuning shape)"
    - "Sealed-singleton FlowType subclass with strict reference-identity compatibility (SfzType mirrors Phase 32 TuningType verbatim)"
    - "Pre-computed (pitch, velocity) lookup grid storing region references — D-01 / D-02 last-declared-wins is structurally enforced by build write-order"
    - "ExecutionContext-clustered SFZ-surface fields under a section-header comment, mirroring the existing 'Random Number Generation State' block"
    - "Per-ExecutionContext sfz_root cache (ResolvedSfzRoot) prevents singleton-pollution test flakes per Pitfall 2"

key-files:
  created:
    - flow-lang/StandardLibrary/Audio/Sfz/SfzLoopMode.cs
    - flow-lang/StandardLibrary/Audio/Sfz/SfzParseException.cs
    - flow-lang/StandardLibrary/Audio/Sfz/SfzRegion.cs
    - flow-lang/StandardLibrary/Audio/Sfz/SfzData.cs
    - flow-lang/TypeSystem/SpecialTypes/SfzType.cs
    - flow-lang.Tests/Unit/Phase33/SfzTypeFacts.cs
  modified:
    - flow-lang/Runtime/Value.cs
    - flow-lang/Runtime/ExecutionContext.cs
    - flow-lang/Runtime/FlowConfig.cs

key-decisions:
  - "SfzParseException extends Flow's ParseException (TypeParser.cs:339), matching ScalaParseException's actual base — the plan's hint to 'verify the actual base' resolved against the contradictory 'derive from System.Exception' wording in the must_haves block. CONTEXT § 'Claude's Discretion / Error class hierarchy' explicitly requires extending ParseException."
  - "SfzPatchRegistry typed strongly as Dictionary<string, FlowLang.StandardLibrary.Audio.Sfz.SfzData> — Task 1 ships the type in the same plan, so no forward-reference workaround was needed; the merged-plan compile gate (Task 3) proves this composes cleanly."
  - "Value.Sfz factory placed immediately below Value.Tuning to keep music-type factories adjacent (mirrors the order of TuningType/SfzType in the SpecialTypes specificity slot map)."
  - "SFZ-surface ExecutionContext fields clustered under a '// ===== Phase 33 — SFZ surface =====' section header below SymbolInternTable, mirroring the existing '// ===== Random Number Generation State =====' pattern at lines 31-72."
  - "ResolvedSfzRoot is the 5th field per Pitfall 2 — first-read cache prevents test-isolation flakes when FlowConfig.Active gets reset between tests."

patterns-established:
  - "SFZ data-model files live under flow-lang/StandardLibrary/Audio/Sfz/ — parallel to flow-lang/StandardLibrary/Audio/Tuning/ which Phase 32 established"
  - "All SFZ records are sealed positional records with positional fields, XML-doc on the record (not per-field) describing the full field map + units/conversions/pitfalls — keeps the file scannable and the field semantics centralised"

requirements-completed: [SPEC-2, SPEC-3, SPEC-4, SPEC-5, SPEC-6]

# Metrics
duration: 12min
completed: 2026-05-15
---

# Phase 33 Plan 02: SFZ Data Model + Type System + Runtime State Foundation Summary

**Immutable SfzData/SfzRegion/SfzLoopMode/SfzParseException model + sealed-singleton SfzType (specificity 150) + Value.Sfz factory + 5 ExecutionContext SFZ-surface fields + FlowConfigPoco.SfzRoot — the merged Wave-1 type / runtime / config foundation every other Phase 33 plan compiles against.**

## Performance

- **Duration:** ~12 min
- **Started:** 2026-05-15
- **Completed:** 2026-05-15
- **Tasks:** 3 (Task 3 verification-only — no commit)
- **Files modified:** 9 (6 created, 3 patched)

## Accomplishments

- **Immutable SFZ data model** under `flow-lang/StandardLibrary/Audio/Sfz/`:
  - `SfzLoopMode` (4-member enum matching SFZ spec: NoLoop/OneShot/LoopContinuous/LoopSustain)
  - `SfzParseException` extending `ParseException` with the same `{file}:{line}:{col} — expected X got 'Y'` format ScalaParseException uses
  - `SfzRegion` 13-field sealed record with full unit / conversion documentation (Volume linear, Pan in [-1.0, +1.0], AmpegAttack/Release in seconds, LoopStart/End in source frames, header inheritance flattened at parse time)
  - `SfzData` top-level patch with Description, BasePath, Regions list, `SfzRegion?[128, 128]` Grid (D-01), and `int[] SortedByPitch` (D-03 nearest-pitch fallback index)
- **`SfzType`** — sealed singleton FlowType subclass at specificity 150, slotted above all existing music types (Tuning=137, Section=138, Beat=139, Song=140, Hertz=144). Strict compatibility — no numeric coercion, no cross-music-type equivalence.
- **`Value.Sfz(SfzData)` factory** in `flow-lang/Runtime/Value.cs` directly below `Value.Tuning`.
- **5 new `ExecutionContext` SFZ-surface fields** clustered under a `// ===== Phase 33 — SFZ surface =====` section header beneath `SymbolInternTable`: `SfzEnabled`, `SfzInstruments`, `SfzPatchRegistry`, `SfzDiagnostics`, `ResolvedSfzRoot`.
- **`FlowConfigPoco.SfzRoot`** — nullable init-only string property; the snake_case TOML key `sfz_root` auto-maps via the existing `JsonNamingPolicy.SnakeCaseLower` in `flow-cli/Config/FlowConfigLoader.cs:36` (zero edit needed there).
- **`SfzTypeFacts.cs`** — 7 facts pinning SfzType (specificity, name, strict compatibility against Tuning + String, identity against self), ExecutionContext fresh defaults, FlowConfigPoco.Defaults.SfzRoot null.
- **Compile-verify gate** (Task 3): `dotnet build flow-sharp.sln` exits 0; `dotnet test --filter "FullyQualifiedName~Phase33.SfzTypeFacts"` exits 0 — all 7 facts green.

## Task Commits

Each task was committed atomically:

1. **Task 1: SFZ data-model files** — `671254c` (feat)
2. **Task 2: SfzType + Value.Sfz factory + ExecutionContext SFZ surface + FlowConfigPoco.SfzRoot** — `0d619fb` (feat)
3. **Task 3: Compile-verify gate + SfzTypeFacts test run** — verification-only, no file edits, no commit (per the task `<action>` "This task has no file edits — it is a pure verification gate")

**Plan metadata commit:** _(orchestrator-managed in worktree mode)_

## Files Created/Modified

### Created
- `flow-lang/StandardLibrary/Audio/Sfz/SfzLoopMode.cs` — 4-member enum + SFZ-spec fallback documentation
- `flow-lang/StandardLibrary/Audio/Sfz/SfzParseException.cs` — extends `FlowLang.Parsing.ParseException`; `(filePath, line, column, expected, got)` constructor
- `flow-lang/StandardLibrary/Audio/Sfz/SfzRegion.cs` — sealed positional record with all 13 ordered opcode-derived fields
- `flow-lang/StandardLibrary/Audio/Sfz/SfzData.cs` — sealed positional record (Description, BasePath, Regions, Grid, SortedByPitch)
- `flow-lang/TypeSystem/SpecialTypes/SfzType.cs` — sealed singleton FlowType with specificity 150 + strict compatibility
- `flow-lang.Tests/Unit/Phase33/SfzTypeFacts.cs` — 7 facts (xUnit) pinning the type-system + runtime-state contract

### Modified
- `flow-lang/Runtime/Value.cs` — added `Value.Sfz(SfzData)` factory below `Value.Tuning`
- `flow-lang/Runtime/ExecutionContext.cs` — added 5 SFZ-surface properties under a new `// ===== Phase 33 — SFZ surface =====` section header beneath `SymbolInternTable`
- `flow-lang/Runtime/FlowConfig.cs` — added `string? SfzRoot { get; init; }` to `FlowConfigPoco`

## Decisions Made

- **Resolved the `SfzParseException` base-class ambiguity in favour of `ParseException`** (not `System.Exception`). The plan's must_haves block said "extends System.Exception (mirroring ScalaParseException's actual base)", but the parenthetical "verify by reading ScalaParseException.cs's actual derivation — copy that exact base" is the binding directive, ScalaParseException actually extends `ParseException`, and CONTEXT § "Claude's Discretion / Error class hierarchy" explicitly states "SfzParseException extends the existing flow-lang/Parsing/TypeParser.cs ParseException". Three signals to one — the parenthetical / CONTEXT / source-of-truth all line up against the plain-text wording. Chose to extend `ParseException`.
- **`SfzRegion` documentation centralised on the record, not per-field**, because all 13 fields share a small set of conversion/unit pitfalls (Volume/Pan/Loop-frames/Articulation hint precedence) that read more clearly as a single block than as 13 separate XML-doc tags.
- **SfzPatchRegistry typed `Dictionary<string, FlowLang.StandardLibrary.Audio.Sfz.SfzData>` directly** (no forward-reference workaround), as Task 1 ships `SfzData` in the same plan; the Task 3 build-gate proves this composes cleanly without using `object` or any other indirection.

## Deviations from Plan

None — plan executed exactly as written. The `SfzParseException` base-class ambiguity above is documented as a decision (not a deviation) because both readings of the plan resolved to the same answer once verification was applied.

## Issues Encountered

None.

## User Setup Required

None — no external service configuration introduced by this plan. (`FlowConfigPoco.SfzRoot` is the surface for the future composer-configured VSCO-CE root, but the actual config-file edit is a Plan 33-05+ concern; this plan only ships the POCO field.)

## Next Phase Readiness

- **Plan 33-04 (SfzParser)** can now construct `SfzData`/`SfzRegion`/`SfzLoopMode` values and throw `SfzParseException` with the canonical format.
- **Plan 33-05 (loadSfz builtins)** can now wrap parser output via `Value.Sfz(SfzData)`, read `FlowConfig.Active.SfzRoot` and cache into `ExecutionContext.ResolvedSfzRoot`, and write into `ExecutionContext.SfzInstruments` from the `__enableSfzModule` marker.
- **Plan 33-06 (SfzRenderer)** can now read `SfzData.Grid` for `(pitch, velocity)` lookups and use `SfzData.SortedByPitch` for nearest-pitch fallback.
- **Plan 33-07 (variable-declaration handler + sampler:NAME dispatch)** can now write into `ExecutionContext.SfzPatchRegistry` on `Sfz`-typed variable declarations and dispatch via that map from `SongRenderer`.
- No blockers.

## Self-Check: PASSED

Verification of claims:

**Files created (6):**
- `FOUND: flow-lang/StandardLibrary/Audio/Sfz/SfzLoopMode.cs`
- `FOUND: flow-lang/StandardLibrary/Audio/Sfz/SfzParseException.cs`
- `FOUND: flow-lang/StandardLibrary/Audio/Sfz/SfzRegion.cs`
- `FOUND: flow-lang/StandardLibrary/Audio/Sfz/SfzData.cs`
- `FOUND: flow-lang/TypeSystem/SpecialTypes/SfzType.cs`
- `FOUND: flow-lang.Tests/Unit/Phase33/SfzTypeFacts.cs`

**Commits (2):**
- `FOUND: 671254c` — Task 1 (SFZ data model)
- `FOUND: 0d619fb` — Task 2 (SfzType + Value.Sfz + ExecutionContext + FlowConfig)

**Build:** `dotnet build flow-sharp.sln` exits 0 (14 pre-existing warnings, 0 errors).
**Tests:** `dotnet test --filter "FullyQualifiedName~Phase33.SfzTypeFacts"` — Passed 7 / 0 Failed / 0 Skipped.

---
*Phase: 33-sfz-orchestral-sampler*
*Completed: 2026-05-15*
