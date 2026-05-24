---
phase: 43-module-names-qualified-imports
plan: 02
subsystem: runtime
tags: [phase-43, runtime, registry, module-registry, concurrent-dictionary]

# Dependency graph
requires:
  - phase: 36-sequence-algebra-generative
    provides: "PrngRegistry per-context registry shape (singleton-per-ExecutionContext, FNV-1a keyed)"
  - phase: 38-live-coding-2
    provides: "LiveBlockRegistry per-context registry shape (ConcurrentDictionary-backed, Snapshot/Clear ceremony)"
provides:
  - "ModuleRegistry sealed class (Runtime/ModuleRegistry.cs) — Contains / Register / TryGetProc / Snapshot / Clear public API"
  - "ExecutionContext.ModuleRegistry property — singleton-per-context, ConcurrentDictionary-backed"
  - "6-Fact unit-test scaffold pinning per-context isolation + last-write-wins semantics"
  - "Lookup target for Plan 43-03's ExpressionEvaluator registry-first dispatch branch (D-02)"
  - "Write target for Plan 43-03's ModuleLoader use-time registration hook (D-05 / D-06)"
affects: [phase-43-plan-03-dispatch, phase-43-plan-04-stdlib-migration, phase-44-strict-mode]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Pattern 4 (per-ExecutionContext registry field) extended: 4th registry alongside PrngRegistry / LiveBlockRegistry / StyleRegistry"
    - "ConcurrentDictionary<string, IReadOnlyDictionary<string, Value>> as outer keying, with inner-dict immutability conveyed at the type level"

key-files:
  created:
    - "flow-lang/Runtime/ModuleRegistry.cs (117 lines, 5 public methods)"
    - "flow-lang.Tests/Integration/Phase43/ModuleRegistryTests.cs (171 lines, 6 Facts)"
  modified:
    - "flow-lang/Runtime/ExecutionContext.cs (insert ModuleRegistry property after LiveBlockRegistry at line 156)"

key-decisions:
  - "Per-ExecutionContext (NOT static singleton) — matches RESEARCH A1 + Phase 35 TEST-02 hermetic-isolation contract"
  - "ConcurrentDictionary backing — matches LiveBlockRegistry/PrngRegistry two-actor (background render + audio thread) pattern even though current writer is single-threaded"
  - "Last-write-wins on duplicate Register; advisory wiring lives in Plan 43-03 (ModuleLoader caller), NOT in the registry itself"
  - "FunctionOverload.Internal chosen over FunctionOverload.UserDefined for the test stub Value factory — simpler path, no ProcDeclaration AST tree required"
  - "Inner-dict type IReadOnlyDictionary<string, Value> communicates immutability at the API surface (callers may NOT mutate the registered proc set in place)"

patterns-established:
  - "Pattern: Plan 43-03 will call registry.Contains BEFORE registry.Register so the duplicate-name advisory fires on collision rather than after"
  - "Pattern: TryGetProc returns reference-equal Value instances (no copying) — registry is a lookup table, not a clone source"

requirements-completed: [REQ-MOD-02]

# Metrics
duration: 12min
completed: 2026-05-24
---

# Phase 43 Plan 02: ModuleRegistry Runtime Data Structure Summary

**Per-ExecutionContext ModuleRegistry sealed class (ConcurrentDictionary-backed) exposed on ExecutionContext, plus 6-Fact unit-test scaffold pinning isolation + last-write-wins — pure infrastructure with zero behavior-visible changes.**

## Performance

- **Duration:** 12 min
- **Started:** 2026-05-24T16:18:00Z (approx — base-correction reset)
- **Completed:** 2026-05-24T16:30:11Z
- **Tasks:** 1 (TDD: RED + GREEN)
- **Files modified:** 3 (1 created in Runtime/, 1 modified in Runtime/, 1 created in Tests/Integration/Phase43/)

## Accomplishments

- **`ModuleRegistry` sealed class** at `flow-lang/Runtime/ModuleRegistry.cs` (117 lines) with the locked 5-method public API: `Contains` / `Register` / `TryGetProc` / `Snapshot` / `Clear`. Backed by `ConcurrentDictionary<string, IReadOnlyDictionary<string, Value>>`. XML doc cites Phase 43 D-05 + D-02 + D-06 and the Pattern-4 mirror-shape rationale.
- **`ExecutionContext.ModuleRegistry` property** wired at `flow-lang/Runtime/ExecutionContext.cs` line 158 (immediately after `LiveBlockRegistry`), declared `public ModuleRegistry ModuleRegistry { get; } = new();` with multi-paragraph XML doc following the established Pattern-4 cadence (PrngRegistry → LiveBlockRegistry → ModuleRegistry).
- **6 unit-test Facts** at `flow-lang.Tests/Integration/Phase43/ModuleRegistryTests.cs` covering: fresh-registry empty, Register+Contains round-trip, TryGetProc hit (reference identity preserved), TryGetProc miss (both unknown-module + unknown-proc paths), last-write-wins on duplicate Register (D-06 semantics), per-ExecutionContext isolation (A1 — two distinct `FlowEngine` instances expose distinct registries with non-leaking state).
- **All 6 Facts GREEN** in 130 ms; adjacent registry suites (Phase 36 PrngRegistry, Phase 36 StyleRegistry, Phase 38 LiveBlockParser) continue to pass in 208 ms total (24 tests).

## Task Commits

Each task was committed atomically per the TDD RED/GREEN cycle:

1. **Task 1 RED — failing tests** — `2bc2905` (`test(43-02): add failing ModuleRegistry unit tests (RED)`)
2. **Task 1 GREEN — implementation** — `f8f338f` (`feat(43-02): implement ModuleRegistry + ExecutionContext property (GREEN)`)

No REFACTOR commit — the implementation matched the recommended skeleton on first pass; no cleanup needed.

## Files Created/Modified

- `flow-lang/Runtime/ModuleRegistry.cs` (NEW, 117 lines) — sealed class with `ConcurrentDictionary<string, IReadOnlyDictionary<string, Value>>` backing; public surface = `Contains` / `Register` / `TryGetProc` / `Snapshot` / `Clear`; XML doc cites D-05 / D-02 / D-06 + Pattern-4 mirror-shape rationale.
- `flow-lang/Runtime/ExecutionContext.cs` (MODIFIED, +27 lines) — new `public ModuleRegistry ModuleRegistry { get; } = new();` property inserted after `LiveBlockRegistry` (line 156); multi-paragraph XML doc citing Phase 43 + the per-context-vs-static rationale + the Plan 43-03 read/write hook destinations.
- `flow-lang.Tests/Integration/Phase43/ModuleRegistryTests.cs` (NEW, 171 lines) — namespace `FlowLang.Tests.Integration.Phase43`, `[Collection("FlowScripts")]` + `IDisposable` with `RenderingDiagnostics.ResetForTesting()` ceremony, 6 `[Fact]` methods covering the must_haves truth-set; helper `StubFunction(string name)` builds a `Value.Function(FunctionOverload.Internal(...))` for the proc-Value test fixture.

## Decisions Made

- **Per-ExecutionContext (not static singleton).** Per RESEARCH §"Alternatives Considered" line 147 + §Pattern 4 at line 350. Mirrors PrngRegistry / LiveBlockRegistry / StyleRegistry. A static singleton would leak module registrations across `FlowEngine` instances and break Phase 35 TEST-02's hermetic snapshot/restore round-trip. Pinned by Test 6 (`DistinctExecutionContextsExposeDistinctRegistries`).
- **`ConcurrentDictionary` backing.** Matches the two-actor pattern documented at LiveBlockRegistry (background `Task.Run` re-render + audio playback thread). Plan 43-02 itself ships a single-threaded writer, but `ModuleLoader` integration in Plan 43-03 may load modules from background contexts in future live-coding paths — picking the concurrent backing now avoids a retrofit.
- **Inner-dict type as `IReadOnlyDictionary<string, Value>`.** Communicates the immutability contract at the public API surface — callers MUST NOT mutate the registered proc set in place (a new Register call is the only way to update it, preserving last-write-wins semantics). The concrete `Dictionary<string, Value>` builder lives at the caller site (Plan 43-03 `ModuleLoader`).
- **Last-write-wins on duplicate Register; advisory at caller site.** Per D-06. The registry stays a dumb data structure — no diagnostic firing. `ModuleLoader` in Plan 43-03 will check `Contains` BEFORE the matching `Register` call and fire `[module] duplicate module name '<name>' — last load wins` via `RenderingDiagnostics.WarnOnce` at that hook point. Pinned by Test 5 (`DuplicateRegisterKeepsLastWriteWins`).
- **`FunctionOverload.Internal` chosen over `UserDefined` for the test stub.** Simpler factory path — only needs name + `FunctionSignature` + `Func<IReadOnlyList<Value>, Value>` lambda. The `UserDefined` factory requires a `ProcDeclaration` AST tree which is overkill for unit tests that never invoke the proc.

## Deviations from Plan

None — plan executed exactly as written.

- The plan's `read_first` list mentions `43-PATTERNS.md` (lines around 131-160 and "ExecutionContext.cs — ModuleRegistry field add" section), but no `43-PATTERNS.md` file exists on disk under `.planning/phases/43-module-names-qualified-imports/`. RESEARCH.md §Pattern 4 (lines 350-367) is the canonical source for the recommended skeleton and I followed it directly. Not a deviation — the missing-file reference was inert because the equivalent content lives in RESEARCH.md.

## Issues Encountered

None.

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness

- **Plan 43-03 (Wave 2)** can immediately wire `ModuleLoader.LoadModule` to call `context.ModuleRegistry.Register(...)` and `ExpressionEvaluator.EvaluateMemberAccess` to call `context.ModuleRegistry.TryGetProc(...)` against a real type rather than a sketched interface.
- The 5-method public API is intentionally minimal — Plan 43-03 will not need to extend it. If it does, that's a deviation signal that Plan 43-02's shape was wrong.
- Wave 1 Plan 43-01 (token + AST + parser surface) lands in a disjoint file set and merges independently. No coupling concerns.

## Self-Check: PASSED

Verified before SUMMARY commit:

- `flow-lang/Runtime/ModuleRegistry.cs` FOUND (117 lines)
- `flow-lang/Runtime/ExecutionContext.cs` modified (new `public ModuleRegistry ModuleRegistry { get; } = new();` at line 158, XML doc lines 132-156)
- `flow-lang.Tests/Integration/Phase43/ModuleRegistryTests.cs` FOUND (171 lines, 6 Facts)
- Commit `2bc2905` (RED) FOUND in git log
- Commit `f8f338f` (GREEN) FOUND in git log
- `dotnet build flow-lang/flow-lang.csproj` exits 0 with zero new warnings
- `dotnet test ... --filter Phase43.ModuleRegistryTests`: 6 passed / 0 failed
- Adjacent registry suites (PrngRegistry / StyleRegistry / LiveBlockParser): 24 passed / 0 failed
- Per CLAUDE.md determinism contract: no `Random` / no FP arithmetic introduced — registry is pure dict lookup. Two-run cmp-clean trivially preserved.
- No new NuGet packages added.
- No modifications to STATE.md / ROADMAP.md (orchestrator owns those writes).

---
*Phase: 43-module-names-qualified-imports*
*Completed: 2026-05-24*
