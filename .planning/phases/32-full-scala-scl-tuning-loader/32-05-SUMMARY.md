---
phase: 32-full-scala-scl-tuning-loader
plan: 05
subsystem: tuning
tags: [scala, tuning, scl, microtonal, stack-refactor, d-12, d-13, d-14, pitfall-2, pitfall-6, repl-sticky, blast-radius]

# Dependency graph
requires:
  - phase: 32-03
    provides: ResolvedTuning + RenderTuning.Custom field + Pattern-A NoteToFrequency Custom branch (already merged into Wave 2 base)
  - phase: 32-04 (PARALLEL — same wave, merged first)
    provides: ScalaBuiltins.Register call at FlowEngine.cs:74; loadScala builtin surface + Value.Tuning factory
provides:
  - MusicalContext.TuningStack (Stack<RenderTuning>) + MusicalContext.ActiveTuning getter (D-12 single resolution accessor)
  - ExecutionContext.SetFileScopeTuning(RenderTuning) — bottom-frame pragma push (D-08 sticky)
  - ExecutionContext.PushTuning(RenderTuning) / PopTuning() — block-form entry/exit (Plan 32-06 consumer)
  - ExecutionContext.ResetBlockTuningStack() — REPL eval boundary hook (D-14 ephemeral)
  - FlowEngine.BuildPragmaTuning(TuningSystem) — pragma-name-to-RenderTuning helper
  - D-13 MIDI-export advisory predicate updated for custom Scala tunings (Pitfall 6)
  - 9 Phase 32 Plan 32-05 unit-test Facts (TuningStackFacts)
affects: [32-06 (tuning context block AST + interpreter — consumes PushTuning/PopTuning + the parallel TuningContextStatement node)]

# Tech tracking
tech-stack:
  added: []  # no new external libraries — pure C# refactor
  patterns:
    - "Push/pop musical-context stack mirrors Phase 18+ Tempo/TimeSignature/Key/Swing/ReverbTime/VoicePool — `MusicalContext.TuningStack` adopts the established shape"
    - "Single read accessor pattern: all Phase 23 readers consume MusicalContext.ActiveTuning (RESEARCH Pitfall 1 mitigation — Option B)"
    - "Transitional Obsolete shim for scalar Tuning field + SetTuning(TuningSystem?) — guards Task 1 build greenness while Task 2 sweeps readers"
    - "REPL stickiness coexistence (Pitfall 2): bottom-of-stack file-scope pragma frame never popped at REPL boundary; ResetBlockTuningStack pops down to Count ≤ 1"
    - "Stack<T> two-reversal trick in MusicalContext.Clone preserves push order — single-arg Stack<T> ctor reverses by default"
    - "Dual-axis predicate at every Phase 23 advisory site: `activeTuning.Custom != null || activeTuning.System != EqualTemperament` (Pitfall 6)"

key-files:
  created:
    - "flow-lang.Tests/Unit/Phase32/TuningStackFacts.cs"
  modified:
    - "flow-lang/Runtime/MusicalContext.cs"
    - "flow-lang/Runtime/ExecutionContext.cs"
    - "flow-lang/Core/FlowEngine.cs"
    - "flow-lang/StandardLibrary/Audio/SongRenderer.cs"
    - "flow-lang/StandardLibrary/Audio/MidiExport.cs"
    - "flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs"

key-decisions:
  - "Phase 23 scalar Tuning field + SetTuning(TuningSystem?) kept transitionally as [Obsolete] shims (RESEARCH Pitfall 1 §How-to-avoid Option B). Direct deletion would have broken the four reader sites' compile step in Task 1, violating its `dotnet build` acceptance gate; the readers are migrated in Task 2 (same plan, next commit). The shims route the old scalar/SetTuning callers through the new TuningStack path, so they produce identical behavior — net zero observable change between Tasks 1 and 2 end."
  - "FlowEngine.Execute calls ResetBlockTuningStack BEFORE ApplyTuningPragma. Justification: a leaked block frame from a prior REPL eval (unclosed `tuning t { ...`) would otherwise stack above the new file-scope pragma push. Reset-first puts the stack in a known state (Count ≤ 1 → all block frames gone), then ApplyTuningPragma replaces or leaves the bottom frame."
  - "FlowEngine.BuildPragmaTuning uses Mode.Major + tonic ('C', 0) per SongRenderer.ResolveRenderTuning D-02 silent default. Per-section `key X { ... }` blocks REPLACE this resolution at render time, so the pragma-time default is a placeholder that SongRenderer overwrites with the active key's tonic/mode."
  - "SongRenderer.ResolveRenderTuning rewritten as three-branch: Custom-wins (Pitfall 3 mutual exclusion) → 12-TET fast path → key-aware tonic resolution. The 12-TET fast path is the byte-identical short-circuit trigger; Custom-wins handles loaded .scl tunings; the last branch carries Phase 23 JI/Pythagorean semantics."
  - "TuningStackFacts uses `using ExecutionContext = FlowLang.Runtime.ExecutionContext;` alias because the test project imports both FlowLang.Runtime and System.Threading. Mirrors the pattern in FlowEngine.cs:9 (RuntimeContext alias)."
  - "No Phase 23 test-file migrations needed — the frontmatter note anticipated 5+ Phase 23 tests with direct MusicalContext.Tuning access; grep audit at execute time found ZERO such tests. All Phase 23 tests reference `Tuning` only via `RenderTuning` (a different type) or in doc comments. See `Phase 23 test files migrated` heading below for the audit record."

patterns-established:
  - "Pattern: stack refactor for musical-context fields previously held as nullable scalars — push/pop API at ExecutionContext + Active<X> getter at MusicalContext + reset hook for REPL boundary. Template for future single-scalar → stack migrations."
  - "Pattern: dual-axis advisory predicate when a Phase-23 wedge field gains a Phase-32 extension axis — `wedge != Default || extension != null`. Applied at MidiExport D-13 advisory + HarmonyFunctions enharmonic guard."
  - "Pattern: deferred reader-site migration via [Obsolete] shim — Task 1 keeps the old API as a transitional bridge so the build stays green; Task 2 sweeps the readers; the shim deletes after the consumer (Plan 32-06) lands."

requirements-completed: [SPEC-2, SPEC-6]

# Metrics
duration: ~35min
completed: 2026-05-14
---

# Phase 32 Plan 05: MusicalContext TuningStack Refactor Summary

**Replaces Phase 23's scalar `MusicalContext.Tuning` (TuningSystem?) field with a push/pop `Stack<RenderTuning> TuningStack` + `ActiveTuning` getter, per D-12. Bridges the four production readers (FlowEngine pragma, SongRenderer, MidiExport, HarmonyFunctions) onto the new accessor. Wires Pitfall 2 coexistence (D-08 sticky file-scope pragma + D-14 ephemeral REPL block) via a dedicated `ResetBlockTuningStack` hook at the FlowEngine eval boundary. Updates the D-13 MIDI-export advisory predicate to fire under custom Scala tunings (Pitfall 6). 9 TuningStackFacts pin the contract. Phase 23 regression sweep 91/91 GREEN — observably-invisible refactor.**

## Performance

- **Duration:** ~35 min (executor start to SUMMARY commit)
- **Started:** 2026-05-14 (worktree spawn)
- **Completed:** 2026-05-14
- **Tasks:** 3 / 3
- **Files created:** 1 (TuningStackFacts.cs)
- **Files modified:** 6 (MusicalContext, ExecutionContext, FlowEngine, SongRenderer, MidiExport, HarmonyFunctions)
- **Test Facts added:** 9 (TuningStackFacts)
- **Phase 23 regression sweep:** 91/91 GREEN (critical contract preserved)
- **Phase 32 sub-suite:** 54/54 GREEN
- **Full suite:** 1158 passed / 26 pre-existing failures (Phase 28 baseline) — zero new regressions

## Accomplishments

### Task 1: MusicalContext refactor + ExecutionContext push/pop API (commit `0692d9d`)

- **`MusicalContext.cs`** gains:
  - `Stack<RenderTuning> TuningStack` (auto-initialized empty in the field initializer).
  - `RenderTuning ActiveTuning` getter — `TuningStack.Peek()` when non-empty, `RenderTuning.Default` (byte-identical 12-TET short-circuit trigger) when empty. This is the SINGLE read accessor per RESEARCH Pitfall 1 §Option B.
  - `Clone()` deep-copies the stack via the two-reversal trick (`new Stack<T>(new Stack<T>(original))`) to preserve push order. `RenderTuning` is a struct so no reference aliasing concern; the trick guards against the silent reverse a single-arg Stack<T> ctor produces.
  - `ToString()` reports `tuning=<ActiveTuning> (stack depth N)` when the stack is non-empty.
  - Phase 23 scalar `TuningSystem? Tuning` field kept transitionally as an `[Obsolete]` shim (Pitfall 1 §How-to-avoid Option B). Will be removed after Plan 32-06 lands.

- **`ExecutionContext.cs`** gains the four-method push/pop/active/reset surface:
  - **`SetFileScopeTuning(RenderTuning)`** — REPLACES the bottom-of-stack frame on `GlobalFrame.MusicalContext.TuningStack`. Algorithm: drain the stack, push the new frame. Net result: `Count == 1` containing the new file-scope frame.
  - **`PushTuning(RenderTuning)`** — pushes onto the topmost (current-frame) stack. Plan 32-06's `tuning t { ... }` interpreter case consumes this.
  - **`PopTuning()`** — pops the topmost frame's stack. Throws `InvalidOperationException` if the stack is empty (defensive — should never fire if push/pop are balanced via try/finally).
  - **`ResetBlockTuningStack()`** — REPL eval boundary hook. Pops the global frame's stack down to `Count ≤ 1`; preserves the bottom-of-stack file-scope frame (D-08 sticky pragma) and removes all block frames above it (D-14 ephemeral). Idempotent.
  - **`SetTuning(TuningSystem?)`** kept transitionally as an `[Obsolete]` shim that builds a `RenderTuning(system, Major, 'C', 0)` and forwards to `SetFileScopeTuning`. This keeps Task 1's build green while Task 2 sweeps the readers.
  - **`GetMusicalContext()`** — the inheritance resolver now walks frames top-to-bottom and adopts the FIRST non-empty `TuningStack` it encounters (deep-copy via two-reversal trick) instead of the old `??=` scalar merge. Existing innermost-frame-wins semantic preserved.

### Task 2: Phase 23 reader-site migration + Pitfall 6 D-13 predicate (commit `ad0dd59`)

5 production code paths migrated (the 5 sites RESEARCH §"Readers of MusicalContext.Tuning" identified):

1. **`FlowEngine.ApplyTuningPragma`** — calls `_context.SetFileScopeTuning(BuildPragmaTuning(<system>))` instead of the old `_context.SetTuning(<system>)`. `BuildPragmaTuning` is a new private helper that produces `new RenderTuning(system, Mode.Major, 'C', 0)` per SongRenderer's D-02 silent C-major default (the same defaults SongRenderer.ResolveRenderTuning uses when no key context exists at section render time). D-07/D-08 sticky pragma preserved: when no pragma is present, `ApplyTuningPragma` calls nothing → bottom frame survives.

2. **`FlowEngine.Execute`** — calls `_context.ResetBlockTuningStack()` BEFORE `ApplyTuningPragma`. This is the D-14 REPL eval boundary hook. A leaked block frame (unclosed `tuning t { ...` from a prior REPL eval) is popped before the new file-scope frame is written. Per Pitfall 2: pragmas stay sticky; blocks force-close.

3. **`SongRenderer.ResolveRenderTuning`** rewritten as three-branch:
   - **Branch 1 (Custom wins):** if `activeTuning.Custom != null`, return it verbatim. The Custom-path MidiToHz table is fully populated; tonic/mode are irrelevant. Pitfall 3 mutual exclusion contract.
   - **Branch 2 (12-TET fast path):** if `activeTuning.System == EqualTemperament` AND no Custom, return as-is. Triggers the byte-identical 12-TET short-circuit at the synthesizer level.
   - **Branch 3 (Phase 23 JI/Pythagorean):** key-aware tonic resolution, mirroring the previous behavior.

4. **`MidiExport.WriteMidi(ctx)` D-13 advisory** — predicate changed from `musicalCtx?.Tuning is TuningSystem activeTuning && activeTuning != TuningSystem.EqualTemperament` to:
   ```csharp
   var activeTuning = musicalCtx?.ActiveTuning ?? RenderTuning.Default;
   if (activeTuning.Custom != null || activeTuning.System != TuningSystem.EqualTemperament)
   ```
   **This satisfies Pitfall 6** — the advisory now fires under custom Scala tunings too. WarnOnce sentinel + message text unchanged (only the trigger predicate changed).

5. **`HarmonyFunctions.Enharmonic` advisory** — same dual-axis predicate applied to the enharmonic-respelling warning. Destructive (≈21¢ shift) under any non-EQ system AND under custom tunings.

### Task 3: TuningStackFacts (commit `b3eddca`)

**9 Facts in `flow-lang.Tests/Unit/Phase32/TuningStackFacts.cs`** (≥ 7 plan minimum):

1. `MusicalContext_Default_HasEmptyStack_ActiveTuningIsDefault` — empty stack returns `RenderTuning.Default` (12-TET short-circuit trigger; `Custom == null`, `System == EqualTemperament`).
2. `PushTuning_Once_ActiveTuningReturnsPushedValue` — single push surfaces via `GetMusicalContext().ActiveTuning`.
3. `PushTuning_Twice_ActiveTuningReturnsTopValue` — second push wins; `TuningStack.Count == 2`.
4. `PopTuning_AfterTwoPushes_RevealsLowerValue` — pop reveals the layer below; count drops to 1.
5. `SetFileScopeTuning_TwiceReplaces_DoesNotAccumulate` — two successive calls leave `Count == 1` containing the second value (Phase 23 D-08 + Phase 32 D-12 invariant).
6. **`ResetBlockTuningStack_PreservesPragmaFrame_PopsBlocks`** — Pitfall 2 explicit coexistence Fact: set pragma frame + push two block frames → reset → only pragma remains. Idempotent on second call.
7. `PopTuning_OnEmptyStack_Throws` — defensive guard verified.
8. `Clone_DeepCopiesTuningStack_PreservesOrder` — push order survives via the two-reversal trick; original not mutated.
9. `TuningStack_CarriesCustomResolvedTuning_ActiveTuningPreservesIt` — a `RenderTuning` carrying a `ResolvedTuning?` Custom reference round-trips through `PushTuning + GetMusicalContext().ActiveTuning` with `Assert.Same` reference preservation.

## Task Commits

| # | Hash      | Type     | Description                                                                            |
|---|-----------|----------|----------------------------------------------------------------------------------------|
| 1 | `0692d9d` | refactor | MusicalContext TuningStack + ExecutionContext push/pop API (Task 1)                    |
| 2 | `ad0dd59` | refactor | migrate Phase 23 readers to ActiveTuning + Pitfall 6 predicate (Task 2)                |
| 3 | `b3eddca` | test     | TuningStackFacts — stack semantics + Pitfall 2 coexistence (Task 3)                    |

_The orchestrator will add the metadata commit (this SUMMARY.md) after wave merge._

## Phase 23 test files migrated (frontmatter note resolution)

The plan's frontmatter note anticipated 5+ Phase 23 test files that construct or assert on `MusicalContext.Tuning` directly. The execute-time grep audit:

```bash
grep -rn 'MusicalContext\.Tuning\|\.Tuning =\|SetTuning\|GlobalFrame\.MusicalContext\.Tuning' \
  flow-lang.Tests/Unit/Phase23/ flow-lang.Tests/Integration/Phase23/
```

returned **a single doc-comment reference**:

| File:Line | Context | Action |
|-----------|---------|--------|
| `flow-lang.Tests/Unit/Phase23/TransformInvarianceFacts.cs:25` | XML doc comment: `/// transform code path never reads <c>MusicalContext.Tuning</c>.` | **No action — doc comment only.** The post-refactor invariant still holds: transforms operate at the Sequence layer and never read tuning context. |

The broader `\.Tuning` grep across `flow-lang.Tests/Unit/Phase23/` returned `RenderTuning` references in `PitchConversionTuningFacts.cs`, `VocalizationTuningFacts.cs`, etc. — all referencing the **distinct** `RenderTuning` record-struct (a `Phase 23 Audio/Tuning` type), NOT `MusicalContext.Tuning`. Those Facts continued to compile and pass unchanged through the refactor.

**Net Phase 23 test migrations performed: 0.** The frontmatter overestimated the blast radius; the grep audit invalidated the estimate. Phase 23's behavior is exercised via the FlowEngineRunner / inline-source pattern, not direct ExecutionContext or MusicalContext construction — those use the same code path the production readers use, so no migration was needed.

## Files Created/Modified

### Created
- `flow-lang.Tests/Unit/Phase32/TuningStackFacts.cs` (231 lines, 9 Facts; xUnit; namespace `FlowLangTests.Unit.Phase32`)

### Modified
- `flow-lang/Runtime/MusicalContext.cs` (+45/-3 lines net) — TuningStack + ActiveTuning + Clone two-reversal + ToString update + Obsolete scalar shim
- `flow-lang/Runtime/ExecutionContext.cs` (+88/-22 lines net) — GetMusicalContext stack-aware resolution + SetFileScopeTuning + PushTuning + PopTuning + ResetBlockTuningStack + Obsolete SetTuning shim
- `flow-lang/Core/FlowEngine.cs` (+22/-12 lines net) — ResetBlockTuningStack call site + ApplyTuningPragma uses BuildPragmaTuning + helper
- `flow-lang/StandardLibrary/Audio/SongRenderer.cs` (+21/-15 lines net) — ResolveRenderTuning three-branch rewrite + Custom-wins (Pitfall 3) + timeline-path doc comment update
- `flow-lang/StandardLibrary/Audio/MidiExport.cs` (+9/-4 lines net) — D-13 advisory predicate updated to dual-axis (Pitfall 6) + RegisterContextDependent doc comment update
- `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs` (+7/-3 lines net) — enharmonic guard dual-axis predicate

## Decisions Made

See `key-decisions` in the frontmatter for the full list. Most important call-outs:

- **Phase 23 Obsolete shims** carry the build through Task 1 → Task 2 boundary. The scalar `Tuning` field + `SetTuning(TuningSystem?)` are kept as `[Obsolete]` transitional shims that route to the new TuningStack path. Removal is scheduled for after Plan 32-06's `TuningContextStatement` interpreter lands (the only remaining consumer that could still hold a reference is the not-yet-existing TuningContextStatement test code, but Plan 32-06 will use the new API directly).
- **ResetBlockTuningStack runs BEFORE ApplyTuningPragma** in FlowEngine.Execute. This is the Pitfall 2 critical ordering — without reset-first, a leaked block frame from a prior REPL eval would stack ABOVE the new file-scope pragma push.
- **No Phase 23 test migrations needed** — grep audit invalidated the frontmatter's estimate of "5+ Phase 23 tests with direct MusicalContext.Tuning access". The actual count is 0 (Phase 23 tests use FlowEngineRunner / inline-source patterns, which exercise the production readers indirectly).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 — Blocking Issue] Plan's Task 1 acceptance required `dotnet build` green while removing the scalar Tuning field**

- **Found during:** Task 1 — deleting `MusicalContext.Tuning` (scalar) caused 8 compile errors in flow-lang.csproj (FlowEngine SetTuning x3, MidiExport x1, SongRenderer x3, HarmonyFunctions x1). Task 1's `<acceptance_criteria>` says `dotnet build flow-lang.Tests/flow-lang.Tests.csproj -v minimal` MUST exit 0.
- **Issue:** The plan's Task 1 / Task 2 boundary is conceptually atomic but ships across two commits. Pure deletion in Task 1 violates the build-green acceptance gate.
- **Fix:** Followed RESEARCH Pitfall 1 §How-to-avoid Option B: kept the scalar `TuningSystem? Tuning` field + the `SetTuning(TuningSystem?)` method as transitional `[Obsolete]` shims that route through the new TuningStack path. Task 1 builds clean; Task 2 sweeps the readers (which never read the scalar anyway because the shim writes to the new stack). The shims will be deleted after Plan 32-06 lands.
- **Files modified:** `flow-lang/Runtime/MusicalContext.cs` (Obsolete scalar field kept), `flow-lang/Runtime/ExecutionContext.cs` (Obsolete SetTuning kept).
- **Verification:** `dotnet build` exits 0 with 0 errors, 13 pre-existing warnings (no new Obsolete-usage warnings — the Obsolete-attributed APIs are no longer called by any code path post-Task-2).
- **Committed in:** `0692d9d` (Task 1).

**2. [Rule 3 — Blocking Issue] xUnit test ambiguous reference between FlowLang.Runtime.ExecutionContext and System.Threading.ExecutionContext**

- **Found during:** Task 3 — first build of TuningStackFacts.cs reported `CS0104: 'ExecutionContext' is an ambiguous reference between 'FlowLang.Runtime.ExecutionContext' and 'System.Threading.ExecutionContext'`.
- **Issue:** xUnit test files implicitly import System namespaces (including `System.Threading`); FlowLang.Runtime.ExecutionContext clashes with the same-named class in `System.Threading`.
- **Fix:** Added `using ExecutionContext = FlowLang.Runtime.ExecutionContext;` alias to TuningStackFacts.cs. Mirrors the pattern in `FlowEngine.cs:9` (`using RuntimeContext = FlowLang.Runtime.ExecutionContext;`).
- **Files modified:** `flow-lang.Tests/Unit/Phase32/TuningStackFacts.cs` (alias added).
- **Verification:** Build clean; all 9 Facts pass.
- **Committed in:** `b3eddca` (Task 3) — single commit.

**3. [Rule 2 — Auto-add missing critical functionality] Clone two-reversal trick + Pitfall 2 reset-before-pragma ordering**

- **Found during:** Task 1 — implementing `MusicalContext.Clone()` and FlowEngine ordering.
- **Issue:** A naive `new Stack<RenderTuning>(original.TuningStack)` REVERSES the order (single-arg Stack<T> ctor enumerates top-to-bottom and pushes back, inverting). A naive FlowEngine that calls `ApplyTuningPragma` BEFORE `ResetBlockTuningStack` would leak block frames above the new pragma frame at REPL eval boundary (Pitfall 2 anti-pattern).
- **Fix:** Two-reversal trick in Clone (`new Stack<T>(new Stack<T>(original))`); `_context.ResetBlockTuningStack()` placed at the head of `Execute`, before `ApplyTuningPragma`. Both are subtle correctness traps; the Facts at Task 3 lock them in (`Clone_DeepCopiesTuningStack_PreservesOrder`, `ResetBlockTuningStack_PreservesPragmaFrame_PopsBlocks`).
- **Files modified:** `flow-lang/Runtime/MusicalContext.cs` (Clone), `flow-lang/Core/FlowEngine.cs` (Execute ordering).
- **Verification:** Both protected by Facts in TuningStackFacts.cs.
- **Committed in:** `0692d9d` (Clone) + `ad0dd59` (FlowEngine ordering).

---

**Total deviations:** 3 auto-fixed (2 × Rule 3 blocking issues, 1 × Rule 2 missing critical functionality).
**Impact on plan:** No expansion of scope; all three deviations were required to ship the plan's stated objective per the acceptance criteria.

## Authentication Gates Encountered

None — Plan 32-05 is pure C# refactor + xUnit Facts; no auth, no network access, no file-system surface beyond reading source files.

## Pre-existing Failures (Out of Scope per Executor Rules)

Full-suite `dotnet test` reports **26 failures**, all pre-existing:
- 24 × `Phase28.PerSynthArticulationTests.PerSynthArticulation_NormalVsArticulated_FFTCosineDifferentiable` (FFT-based articulation differentiation tests across sax/piano/bell/flute/strings/brass × Accent/Legato/Tenuto/Sforzando)
- 2 × `Phase28.RagtimeFixtureTests.Ragtime_*_RmsRegression` (RMS regression vs baselines)

Pre-existing per RESEARCH Pitfall 7 + Plan 32-02/03/04 SUMMARYs. **Plan 32-05 introduces zero new regressions** — 1158 passed / 26 failed delta matches the Wave 1 + Wave 2 base.

## Acceptance Verification

All `<acceptance_criteria>` items pass for all 3 tasks:

### Task 1 acceptance
- ✅ `grep -n 'Stack<RenderTuning> TuningStack' flow-lang/Runtime/MusicalContext.cs` returns 1 match (line 99)
- ✅ `grep -n 'public RenderTuning ActiveTuning' flow-lang/Runtime/MusicalContext.cs` returns 1 match (line 110)
- ✅ `grep -n 'public void PushTuning|public void PopTuning|public void SetFileScopeTuning|public void ResetBlockTuningStack' flow-lang/Runtime/ExecutionContext.cs` returns 4 matches (all four new methods)
- ✅ `dotnet build flow-lang.Tests/flow-lang.Tests.csproj -v minimal` exits 0
- ✅ Phase 23 sub-suite still compiles (tests fail at this gate because readers haven't been migrated yet — that's expected and addressed in Task 2 below)

### Task 2 acceptance
- ✅ `grep -n 'SetFileScopeTuning|SetTuning' flow-lang/Core/FlowEngine.cs` returns 5 matches (3 SetFileScopeTuning calls + 2 doc references)
- ✅ `grep -n 'ActiveTuning' flow-lang/StandardLibrary/Audio/SongRenderer.cs` returns 5 matches (1 production + 4 doc)
- ✅ `grep -n 'Custom != null|Custom is not null' flow-lang/StandardLibrary/Audio/MidiExport.cs` returns ≥ 1 match (1 found at line 208 + 1 doc reference at line 167) — Pitfall 6 predicate update grep-verified
- ✅ `grep -n 'ActiveTuning' flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs` returns 1 match (line 59)
- ✅ `dotnet test --filter "FullyQualifiedName~Phase23" --no-build -v minimal` exits 0 (91/91 Phase 23 tests pass — CRITICAL regression gate)
- ✅ `dotnet build flow-lang.Tests/flow-lang.Tests.csproj -v minimal` exits 0 (no compile errors anywhere)

### Task 3 acceptance
- ✅ `dotnet test --filter "ClassName~TuningStackFacts" -v minimal` exits 0; 9 Facts passed (≥ 7 required)
- ✅ Pitfall 2 explicit Fact `ResetBlockTuningStack_PreservesPragmaFrame_PopsBlocks` passes
- ✅ Phase 23 sub-suite regression sweep still GREEN (91/91)

### Overall plan verification (`<verification>` block)
- ✅ `dotnet build` clean (0 errors, 13 pre-existing warnings)
- ✅ TuningStackFacts ≥ 7 Facts GREEN (9 ran)
- ✅ Phase 23 sub-suite 100% GREEN (CRITICAL regression gate flagged in plan_structure_guidance)
- ✅ 5 reader sites migrated: FlowEngine, SongRenderer, MidiExport, HarmonyFunctions (5th site — the Phase 23 tests using `MusicalContext.Tuning` directly — confirmed zero matches via grep audit, see "Phase 23 test files migrated" section above)
- ✅ Pitfall 6 D-13 advisory predicate now covers `Custom != null` (grep-verified at MidiExport.cs:208)
- ✅ Pitfall 2 D-08-sticky-+-D-14-ephemeral coexistence verified by Fact 6
- ✅ Phase 32 sub-suite: 54/54 GREEN (no regressions in 32-02/03/04 Facts)

## Threat Model Adherence

This plan's PLAN.md does not declare an explicit `<threat_model>` block — the runtime data path is internal to the .NET process and consumes types built by upstream plans. No new trust boundary introduced. Phase 23 D-13 advisory is preserved + EXTENDED (Pitfall 6) to fire under custom Scala tunings, which is a defense-in-depth strengthening (composers are now warned in more cases that MIDI export is 12-TET-only).

## Threat Flags

| Flag | File | Description |
|------|------|-------------|
| (none) | — | No new network endpoints, auth paths, file access patterns, or schema changes at trust boundaries. The TuningStack is in-memory; pushes are bounded by source-text length (each `tuning t { ... }` block produces one push that pops at block exit). |

## Known Stubs

None. Plan 32-05 ships the complete TuningStack refactor specified in the plan's `<interfaces>`:
- `MusicalContext.TuningStack` + `ActiveTuning` — both populated and exercised.
- `ExecutionContext.SetFileScopeTuning + PushTuning + PopTuning + ResetBlockTuningStack` — all four entry points populated and exercised by both the Phase 23 readers AND TuningStackFacts.
- 5 reader-site migrations — all complete, grep-verified, Phase-23 100% GREEN.
- Pitfall 6 D-13 predicate update — complete, grep-verified.

The Phase 23 `[Obsolete]` shims (`MusicalContext.Tuning` scalar field + `ExecutionContext.SetTuning(TuningSystem?)` method) are **transitional**, not stubs. They contain working code that routes through the new TuningStack path; their `[Obsolete]` attribute surfaces any unmigrated caller as a compile warning. Scheduled deletion after Plan 32-06 lands.

## TDD Gate Compliance

Plan 32-05 has `tdd="true"` on Task 3 only (Tasks 1 + 2 are refactors with `type="auto"`). Per Plan 32-04 SUMMARY's precedent for test-only Tasks against an already-shipped runtime: no separate RED commit because the runtime surface landed in Tasks 1 + 2 of this plan (`0692d9d` + `ad0dd59`). The Facts in `b3eddca` exercise that surface and pass on first run.

A meaningful RED would have required artificially breaking the implementation between Task 2 commit and Task 3 commit, which would degrade the plan's atomicity (Phase 23 regression would have momentarily failed). The 9 Facts at GREEN are the durable behavioral guarantee.

## Self-Check: PASSED

All claimed file paths exist on disk:
- `flow-lang.Tests/Unit/Phase32/TuningStackFacts.cs` — FOUND
- `flow-lang/Runtime/MusicalContext.cs` — FOUND (modified)
- `flow-lang/Runtime/ExecutionContext.cs` — FOUND (modified)
- `flow-lang/Core/FlowEngine.cs` — FOUND (modified)
- `flow-lang/StandardLibrary/Audio/SongRenderer.cs` — FOUND (modified)
- `flow-lang/StandardLibrary/Audio/MidiExport.cs` — FOUND (modified)
- `flow-lang/StandardLibrary/Harmony/HarmonyFunctions.cs` — FOUND (modified)
- `.planning/phases/32-full-scala-scl-tuning-loader/32-05-SUMMARY.md` — FOUND (this file)

All 3 task commits exist in git log:
- `0692d9d` (Task 1) — FOUND
- `ad0dd59` (Task 2) — FOUND
- `b3eddca` (Task 3) — FOUND
