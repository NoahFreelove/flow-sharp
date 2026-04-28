# Phase 12 Deferred Items

## DEFER-01: `range` stdlib function missing

> ~~**Discovered during:** Plan 12-05 execution (Test 4 of tests/test_custom_oscillator.flow)~~
>
> ~~**Symptom:** `(range 0 sz)` at `tests/test_custom_oscillator.flow:86` reports~~
> ~~`Function 'range' not found` with underlying `Type cast failure ... from Flow value of type 'Void'`.~~
>
> ~~**Root cause:** `range` is documented in CLAUDE.md under "Built-in Function Categories > Collections"~~
> ~~but is NOT registered in `flow-lang/StandardLibrary/BuiltInFunctions.cs` (searched name~~
> ~~pattern; zero hits). No `.flow` stdlib file declares it either (collections.flow has~~
> ~~head/tail/map/filter/etc. but no `range`).~~
>
> ~~**Why deferred:** Plan 12-05 scope is two atomic commits (if-overload + exportWav-mkdir).~~
> ~~Adding `range` is a new stdlib feature, not in the plan's scope. The plan's SCOPE BOUNDARY~~
> ~~rule restricts auto-fixes to issues DIRECTLY caused by the current task's changes — `range`~~
> ~~was missing before plan 12-05 began.~~
>
> ~~**Interim handling:** `flow-lang.Tests/FlowScriptData.cs` updated so the test_custom_oscillator~~
> ~~Theory row still passes by asserting the NEW pre-fix error substring (`"Function 'range' not found"`).~~
> ~~When plan 12-06 adds `range`, this entry should be removed so the row flips to the~~
> ~~`errorCount == 0` branch.~~
>
> ~~**Proposed fix (plan 12-06 candidate):**~~
> ~~1. Add `public static Value Range(IReadOnlyList<Value> args)` to Collections.cs — 3-arg~~
> ~~   forms: `(range start end)` inclusive of start, exclusive of end; optionally~~
> ~~   `(range start end step)`. Return `Value.Array(list, IntType.Instance)`.~~
> ~~2. Register `FunctionSignature("range", [IntType.Instance, IntType.Instance])` and~~
> ~~   `FunctionSignature("range", [IntType.Instance, IntType.Instance, IntType.Instance])`~~
> ~~   in `BuiltInFunctions.RegisterCollections`.~~
> ~~3. Add `internal proc range (Int: start, Int: end)` and 3-arg variant to collections.flow.~~
> ~~4. Remove the pre-fix baseline entry from FlowScriptData.cs so test_custom_oscillator flips~~
> ~~   to the GREEN default branch.~~

**CLOSED 2026-04-26 by Phase 20 plan 20-01 (DEFER-01).** Implementation: registered
`range(Int, Int)` and `range(Int, Int, Int)` overloads in
`BuiltInFunctions.RegisterCollections` mapping to new `Collections.Range` method;
added 2 `internal proc range` declarations in `collections.flow` per Phase 14 plan
14-02 stdlib-registration contract (param names `s`/`e`/`step` not `start`/`end`
because `end` is reserved as `EndProc` keyword — Phase 14 plan 14-01 lesson
re-applied as Rule 1 deviation). Pythonic semantics — start inclusive, end
exclusive, default step=1, negative step iterates backward, empty array when
unsatisfiable, step==0 throws InvalidOperationException("range step cannot be zero").
8 Phase20/RangeTests unit Facts + 1 tests/test_range.flow Theory row pin
acceptance. FlowScriptData.cs:57 stale ExpectedErrorScripts entry
(`test_custom_oscillator.flow = "Function 'range' not found"`) REMOVED in the same
atomic commit as a Rule 3 deviation (registering `range` structurally flips the
script from error → clean-pass; keeping the pin would have invalidated the
substring assertion) — test_custom_oscillator.flow Theory row now flows through
default `errorCount == 0` GREEN gate. Commit hash: `d0d17db`. See
`.planning/phases/20-cheap-defer-closures-multi-letter-enharmonic-edges/20-01-SUMMARY.md`.
