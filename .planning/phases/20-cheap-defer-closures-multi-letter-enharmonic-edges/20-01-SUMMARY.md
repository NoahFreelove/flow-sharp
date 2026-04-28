---
phase: 20-cheap-defer-closures-multi-letter-enharmonic-edges
plan: 01
subsystem: stdlib-collections
tags: [defer-01, range, stdlib, collections]
requires:
  - Phase 14 (slice precedent for clamp + Sequence/Array dual overload pattern)
  - Phase 18 (byte-identical regression gate must stay green — structural invariant since range touches no audio path)
provides:
  - Collections.Range method (2-arg + 3-arg dispatch via args.Count branch)
  - range(Int, Int) → Array[Int] stdlib overload (Pythonic semantics)
  - range(Int, Int, Int) → Array[Int] stdlib overload (with step, negative iterates backward)
  - tests/test_range.flow integration test
  - 8 RangeTests Facts pinning DEFER-01 acceptance cases
affects:
  - test_custom_oscillator.flow now runs to completion (Test 4 unblocked — was the only consumer of `range` in the existing .flow corpus)
  - FlowScriptData ExpectedErrorScripts shrunk by 1 entry (test_custom_oscillator stale pin removed; structurally forced by registration)
tech-stack:
  added: []
  patterns:
    - "Variable-arity dispatch via args.Count branch (mirrors no precedent in Collections.cs but cleanest pattern; alternative would be two separate methods)"
    - "Direct C# dispatch in unit Facts (Collections.Range with hand-built Value.Int args) — bypasses parser per Pitfall 4"
key-files:
  created:
    - flow-lang.Tests/Unit/Phase20/RangeTests.cs (8 Facts)
    - tests/test_range.flow (4 sentinels)
  modified:
    - flow-lang/StandardLibrary/Collections.cs (added Range method, ~30 lines)
    - flow-lang/StandardLibrary/BuiltInFunctions.cs (2 registrations in RegisterCollections)
    - flow-lang/collections.flow (2 internal proc range declarations)
    - flow-lang.Tests/FlowScriptData.cs (added test_range.flow RequiredSentinels entry; REMOVED test_custom_oscillator.flow ExpectedErrorScripts entry)
decisions:
  - "Param names s/e/step (NOT start/end) in collections.flow proc declarations: `end` is reserved EndProc keyword (parser produces `Expected parameter name. Got EndProc 'end'`). Plan literally specified `start`/`end` but that fails parse — Rule 1 deviation matching Phase 14 plan 14-01's identical lesson (slice's params are `s`/`e` for the same reason)."
  - "ExpectedErrorScripts entry for test_custom_oscillator.flow REMOVED in this plan despite plan's explicit DO NOT instruction. Reason: registering `range` structurally flips the script from error→clean-pass, so the pin's substring assertion (`Function 'range' not found`) fails because no such error appears in stderr. The plan's two instructions (register `range` + don't touch the pin) are mutually inconsistent given actual runtime behavior. Resolved per Rule 3 (blocking) — atomic-commit zero-regression contract takes priority. Plan 20-04 loses one item from its tracked migration list (the test_custom_oscillator pin removal) but its other migration items stay intact."
metrics:
  duration: ~8min
  tasks: 2 (Task 1 + Task 2 bundled in atomic commit per plan)
  files: 6 (2 created, 4 modified)
  completed: 2026-04-26
---

# Phase 20 Plan 01: range stdlib registration Summary

DEFER-01 closure shipped: `range(Int, Int)` and `range(Int, Int, Int)` registered as stdlib built-ins via Collections.Range with standard Pythonic semantics, atomically committed with 8 unit Facts + 1 .flow Theory row.

## What Was Built

**`Collections.Range(IReadOnlyList<Value> args)`** — single-method dispatch handling both arities via `args.Count` branch. Accepts Int/Int/(optional Int) only; throws `InvalidOperationException` with message containing "range step cannot be zero" if step==0. Returns `Value.Array(elements, IntType.Instance)` preserving element-type information.

**Two-arity registration** in `BuiltInFunctions.RegisterCollections`:
- `new FunctionSignature("range", [IntType, IntType])` → `Collections.Range`
- `new FunctionSignature("range", [IntType, IntType, IntType])` → `Collections.Range`

Overload resolver disambiguates by exact-arity match (per 20-RESEARCH Pitfall 3).

**Two `internal proc range` declarations** in `flow-lang/collections.flow`:
```
internal proc range (Int: s, Int: e)
internal proc range (Int: s, Int: e, Int: step)
```

(Param names `s`/`e` not `start`/`end` — see Decisions above.)

**8 RangeTests Facts** under `flow-lang.Tests/Unit/Phase20/RangeTests.cs`:
1. `TwoArg_DefaultStep` — `(range 0 5)` → `[0,1,2,3,4]`
2. `ThreeArg_PositiveStep` — `(range 0 10 2)` → `[0,2,4,6,8]`
3. `NegativeStep_IteratesBackward` — `(range 5 0 -1)` → `[5,4,3,2,1]`
4. `EmptyWhenStartEqualsEnd` — `(range 3 3)` → `[]`
5. `UnsatisfiableWithDefaultStepReturnsEmpty` — `(range 5 0)` → `[]`
6. `ZeroStepThrows` — InvalidOperationException with "range step cannot be zero"
7. `PreservesElementTypeIsInt` — result.Type is `ArrayType(IntType)`
8. `NegativeStep_DescendingPath` — `(range 10 0 -2)` → `[10,8,6,4,2]`

**1 integration test** `tests/test_range.flow` exercising all overloads end-to-end through the parser; binds `Int negOne = (sub 0 1)` to satisfy the negative-literal-as-binary-subtraction parser quirk per 20-RESEARCH Pitfall 4.

## Verification

- **Phase 20.RangeTests:** 8/8 GREEN
- **Phase 18 byte-identical regression gate:** 19/19 GREEN (no audio path touched — structural invariant)
- **Full xUnit suite:** 349/349 GREEN (340 baseline + 8 new RangeTests + 1 new test_range.flow Theory row)
- **Atomic commit:** d0d17db

## Deviations from Plan

### Rule 1 — `end` reserved keyword (proc parameter rename)

**Found during:** Task 1, Step 3 (collections.flow proc declarations)

**Issue:** Plan literally specified:
```
internal proc range (Int: start, Int: end)
internal proc range (Int: start, Int: end, Int: step)
```

But `end` is reserved as `TokenType.EndProc` (the proc-body terminator keyword). Parser emitted:
```
collections.flow:15:39: error: Expected parameter name. Got EndProc 'end'
```

This caused a cascade — collections.flow failed to parse, every test importing `@collections` (transitively, `@std`) failed, full suite went 254/348 RED briefly during local iteration.

**Fix:** Renamed proc parameters from `start`/`end` to `s`/`e` (mirrors `slice (Voids: arr, Int: s, Int: e)` at line 17 — same precedent applied in Phase 14 plan 14-01 for the same reason). C# `Collections.Range` keeps `start`/`end` locals (those are inert internal labels, not parser-visible).

**Files modified:** flow-lang/collections.flow lines 15-16

**Commit:** d0d17db (rolled into atomic commit; iteration localized to working tree)

### Rule 3 — `test_custom_oscillator.flow` ExpectedErrorScripts pin (architectural conflict)

**Found during:** Task 1 verification (full-suite run)

**Issue:** Plan explicitly instructed (CRITICAL — DO NOT in this plan):
> Edit FlowScriptData.cs:57 (the `["test_custom_oscillator.flow"] = "Function 'range' not found"` ExpectedErrorScripts entry). That removal is plan 20-04's job.

But the pin asserts that the script's stderr contains `"Function 'range' not found"`. After registering `range`, the script runs to completion with zero errors — stderr is empty — pin's substring assertion fails. Suite went 347/348 with the pin in place.

The plan's two instructions are mutually inconsistent given runtime reality:
- Register `range` → script no longer errors
- Keep pin → pin requires script to error

Resolved by removing the pin. The plan's `<success_criteria>` "Existing 340 xUnit Facts + ~70 .flow tests pass post-plan (zero regression)" takes priority — the explicit non-negotiable contract beats the discretionary "do not touch" hint.

**Files modified:** flow-lang.Tests/FlowScriptData.cs (removed entry at the original line 57; replaced enclosing comment block with a status update referencing this summary)

**Impact on Plan 20-04:** Plan 20-04's tracked migration list loses ONE item (the test_custom_oscillator pin removal). Other 20-04 closure items (audit-trail docs, Traceability marker flips, REQUIREMENTS.md row updates) are unaffected.

**Commit:** d0d17db (bundled atomically; same commit registers `range` AND removes the now-stale pin so HEAD never has the inconsistent state)

## Auth Gates

None.

## Hand-off Notes

**Plan 20-02 (DEFER-04 multi-letter enharmonic edges):** Independent file (NoteType.cs / EnharmonicTests.cs in Phase14 territory). Can start immediately. No dependency on this plan beyond the Phase 18 invariant which remains green.

**Plan 20-03 (DEFER-05 slice negative-from-end):** Independent file (Collections.SliceArray / SliceSequence). Can start immediately or run in parallel with 20-02. No dependency on `range`.

**Plan 20-04 (closure migration):** Loses the test_custom_oscillator pin-removal item (already removed in this commit). Remaining 20-04 work: audit-trail docs, REQUIREMENTS.md DEFER-01 row flip from Pending → Shipped d0d17db, ROADMAP.md progress update, deferred-items.md DEFER-01 strikethrough closure note (mirrors Phase 14 14-04 pattern).

## Phase 18 Byte-Identical Gate

19/19 Phase18 Facts GREEN. `range` does not interact with `MusicalNoteData.DurationFraction`, `Fraction`, `GetBeats`, `SongRenderer`, `MidiExport`, or any audio path — the gate is a structural invariant rather than something defended by test changes here.

## Self-Check: PASSED

- [x] flow-lang/StandardLibrary/Collections.cs — Range method present (`grep -c "public static Value Range" flow-lang/StandardLibrary/Collections.cs` returns 1)
- [x] flow-lang/StandardLibrary/BuiltInFunctions.cs — both range2Signature and range3Signature registrations present
- [x] flow-lang/collections.flow — both `internal proc range` lines present (with corrected param names s/e/step)
- [x] flow-lang.Tests/Unit/Phase20/RangeTests.cs — file exists, 8 [Fact] methods
- [x] tests/test_range.flow — file exists, 4 sentinels + PASSED marker
- [x] flow-lang.Tests/FlowScriptData.cs — RequiredSentinels entry for test_range.flow added; ExpectedErrorScripts entry for test_custom_oscillator.flow removed
- [x] git log --oneline | grep d0d17db → commit `feat(20-01): DEFER-01 register range...` exists
- [x] dotnet test flow-sharp.sln → 349/349 GREEN
- [x] dotnet test --filter "FullyQualifiedName~Phase18" → 19/19 GREEN
- [x] dotnet test --filter "FullyQualifiedName~Phase20.RangeTests" → 8/8 GREEN
