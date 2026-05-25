---
phase: 44-strict-mode
plan: 08
subsystem: language-core
tags: [strict-mode, overload-resolution, charitable-interpretation, print, if, not, and, or, truthy-coerce, void-wildcard]

# Dependency graph
requires:
  - phase: 44-strict-mode
    provides: "Plan 44-02 — ExecutionContext.StrictMode + CallerStrictMode fields + ExpressionEvaluator call-boundary snapshot; Plan 44-04 — explicit-conversion builtins available for composers refactoring TOWARD strict"
provides:
  - "Pre-strict bug fix per ROADMAP line 404 — (print 42) / (print 3.14) / (print -12dB) now work in default mode (was previously failing OverloadResolver)"
  - "(not) builtin FIRST registration per RESEARCH A6 (test.flow:39 previously commented on its absence)"
  - "D-12 non-strict last-truthy (and)/(or) semantics — (and 1 \"foo\") → \"foo\", (or false 42) → 42 — composer Area 4.2 choice"
  - "StdLib.AutoStr + TruthyCoerce + PrintAny + IfTruthy + NotCharitable + AndLastTruthy + OrLastTruthy helpers"
  - "ExecutionContext.ErrorReporter public accessor — context-dependent builtin registrations can route strict-mode errors through the accumulator the pipeline reads"
  - "InternalFunctionRegistry.TryGetImplementation two-pass exact-then-wildcard lookup — required when the same function name holds BOTH a typed overload AND a Void-wildcard overload"
  - "[strict] error wording landed for (print) / (if) / (not) — Plan 44-09's REQ-STRICT-09 test suite pins exact wording via strict-error-manifest.csv"
affects: [44-09 (strict tightening — Bool-required for and/or, Bool-only print return), 44-strict-mode wave 4 testing]

# Tech tracking
tech-stack:
  added: []  # zero new external packages per D-v1.5-03
  patterns:
    - "Two-pass exact-then-wildcard registry lookup — applies to any future bidirectional Void-wildcard overload registrations"
    - "Charitable Void-wildcard overload alongside typed overload at +1000/+500 specificity split — preserves byte-identical existing call sites"
    - "Surface decl in std.flow + C# impl in BuiltInFunctions.cs + helper in StdLib.cs — three-site registration matches existing equals/lt/gt/print pattern"

key-files:
  created:
    - flow-lang.Tests/Integration/Phase44/PrintCharitablyTests.cs
    - flow-lang.Tests/Integration/Phase44/IfTruthyCoerceTests.cs
    - flow-lang.Tests/Integration/Phase44/NotBuiltinTests.cs
    - flow-lang.Tests/Integration/Phase44/AndOrLastTruthyTests.cs
  modified:
    - flow-lang/Runtime/ExecutionContext.cs
    - flow-lang/StandardLibrary/StdLib.cs
    - flow-lang/StandardLibrary/BuiltInFunctions.cs
    - flow-lang/StandardLibrary/InternalFunctionRegistry.cs
    - flow-lang/std.flow

key-decisions:
  - "ExecutionContext.ErrorReporter exposed as public accessor (mirrors FlowEngine.ErrorReporter) instead of threading a separate er parameter through context-dependent registrations — minimizes API surface delta"
  - "InternalFunctionRegistry.TryGetImplementation two-pass lookup (exact match before wildcard fallback) — Rule 1 bug fix surfaced during Task 1 testing; required because both print(String) AND print(Void) now coexist"
  - "AutoStr falls through to Value.ToString for reference-identity types (Sequence/Chord/Song/Tuning/Sfz/etc.) instead of hand-rolling repr per type — leverages existing well-tested ToString impls"

patterns-established:
  - "Pattern: Void-wildcard charitable overload alongside typed overload. Register typed at +1000 in RegisterStdLib, wildcard at +500 in RegisterContextDependentFunctions (so ctx + ErrorReporter are available). Surface decl in std.flow with both arities. Composer-facing default = charitable; strict (CallerStrictMode==true) re-tightens via branch in wildcard impl."
  - "Pattern: TruthyCoerce as shared helper. One implementation reused by IfTruthy / NotCharitable / AndLastTruthy / OrLastTruthy keeps the falsy rules in one place: Bool returns underlying, Int/Long != 0, Float/Double != 0 && !NaN, Number !IsZero, String !empty, Symbol always truthy, Array/Tuple/Dict non-empty, music tagged-numerics always truthy by presence, reference-identity types always truthy."
  - "Pattern: Strict-error branching inside the wildcard impl. `if (ctx.CallerStrictMode && args[0].Type is not BoolType) { ctx.ErrorReporter.ReportError(\"[strict] (X) requires Bool — got <Type>\", ctx.CurrentCallSite); return Value.Void(); }` — Plan 44-09 then layers REQ-STRICT-09 verbatim-wording tests over the same error-text site."

requirements-completed: [REQ-STRICT-10]

# Metrics
duration: ~42min
completed: 2026-05-25
---

# Phase 44 Plan 08: Pre-strict Charitable Wildcards (print / if / not / and / or) Summary

**Pre-strict bug fix per ROADMAP line 404: (print 42) / if Int x / (not Int 0) now work charitably in default mode via Void-wildcard overloads; (not) gets its first registry registration; (and)/(or) ship D-12 last-truthy semantics; strict-mode error wording landed for Plan 44-09 to verbatim-pin.**

## Performance

- **Duration:** ~42 min (single-session execution, three task commits)
- **Started:** 2026-05-25T01:31:25Z (Phase 44 execution kickoff per STATE.md)
- **Completed:** 2026-05-25T02:13:43Z
- **Tasks:** 3
- **Files modified:** 5 (5 source files + 4 new test files)

## Accomplishments

- **Pre-strict bug fix** — `(print 42)` / `(print 3.14)` / `(print -12dB)` now auto-str via `StdLib.AutoStr` in non-strict mode. Was previously failing OverloadResolver with "No matching overload for function 'print' with argument types (Int)" (BuiltInFunctions.cs:165-169 only registered `print(String)`). Composer ergonomics restored to the default mode per Goals & Non-Goals "ergonomics first".
- **(not) builtin shipped** — first registration in InternalFunctionRegistry per RESEARCH A6 (flow-lang/test.flow:39 previously commented on its absence). Non-strict charitable: `(not 0)` → `true`, `(not 5)` → `false`, `(not "")` → `true`. Strict path emits `[strict] (not) requires Bool — got <Type>`.
- **D-12 non-strict (and)/(or) last-truthy** — composer Area 4.2 discuss-phase choice (RESOLVED per RESEARCH Open Question 2). `(and 1 "foo")` → `"foo"` (last truthy), `(or false 42)` → `42` (first truthy after short-circuit), `(and false 1)` → `false` (first falsy short-circuits), `(or "" "fallback")` → `"fallback"`. v1.5 breaking change vs prior Bool-only `AndBool`/`OrBool`; permitted under D-v1.5-01 pre-traction latitude.
- **(if Int x)** truthy-coerces in non-strict via `TruthyCoerce` (5 truthy, 0 falsy, empty-String falsy, non-empty-String truthy). Bool-typed overload (+1000) still wins for `(if true ...)` → byte-identical preservation.
- **Strict-mode error wording landed** for (print)/(if)/(not) — Plan 44-09's REQ-STRICT-09 test suite pins exact wording via strict-error-manifest.csv.

## Task Commits

Each task was committed atomically:

1. **Task 1: Void-wildcard print + AutoStr** — `a17cb8c` (feat)
   - StdLib.AutoStr dispatch table (Int/Long/Float/Double/Number/Bool/String/Symbol/Note/Decibel/Hertz/Cent/Millisecond/Second/Semitone/Void + fall-through to Value.ToString)
   - StdLib.PrintAny strict-aware wildcard impl
   - print(Void) registration in RegisterContextDependentFunctions + std.flow surface decl
   - ExecutionContext.ErrorReporter public accessor (used by all 3 tasks)
   - InternalFunctionRegistry two-pass exact-then-wildcard lookup (Rule 1 fix — required for all 3 tasks)
   - 8 PrintCharitablyTests Facts GREEN

2. **Task 2: Void-wildcard if + (not) first registration** — `cab742c` (feat)
   - StdLib.TruthyCoerce shared helper (Python/JS conventions + Flow ref-identity rule)
   - StdLib.IfTruthy + StdLib.NotCharitable strict-aware wildcard impls
   - if(Void, Void, Void) + not(Void) registrations + std.flow surface decls
   - 7 IfTruthyCoerceTests + 6 NotBuiltinTests = 13 Facts GREEN

3. **Task 3: D-12 last-truthy (and)/(or)** — `c53816b` (feat)
   - StdLib.AndLastTruthy + StdLib.OrLastTruthy (CPython-style short-circuit + last-truthy)
   - and(Void, Void) + or(Void, Void) registrations + std.flow surface decls
   - 6 AndOrLastTruthyTests Facts GREEN (incl. Bool-Bool +1000 regression pin)

**Plan metadata:** This SUMMARY commit + any STATE/ROADMAP updates owned by the orchestrator wave-merge step.

## Files Created/Modified

- `flow-lang/Runtime/ExecutionContext.cs` — added `public ErrorReporter ErrorReporter => _errorReporter;` accessor
- `flow-lang/StandardLibrary/StdLib.cs` — added AutoStr / PrintAny / TruthyCoerce / IfTruthy / NotCharitable / AndLastTruthy / OrLastTruthy
- `flow-lang/StandardLibrary/BuiltInFunctions.cs` — 5 new Void-wildcard registrations in RegisterContextDependentFunctions: print / if / not / and / or
- `flow-lang/StandardLibrary/InternalFunctionRegistry.cs` — two-pass exact-then-wildcard lookup in TryGetImplementation; new private SignaturesMatchExactly helper
- `flow-lang/std.flow` — 5 new surface decls: `print(Void)` / `if(Void,Void,Void)` / `not(Void)` / `and(Void,Void)` / `or(Void,Void)`
- `flow-lang.Tests/Integration/Phase44/PrintCharitablyTests.cs` — 8 Facts (NEW)
- `flow-lang.Tests/Integration/Phase44/IfTruthyCoerceTests.cs` — 7 Facts (NEW)
- `flow-lang.Tests/Integration/Phase44/NotBuiltinTests.cs` — 6 Facts (NEW)
- `flow-lang.Tests/Integration/Phase44/AndOrLastTruthyTests.cs` — 6 Facts (NEW)

## OverloadResolver Scoring Verification

Pitfall 3 confirmation per RESEARCH:

| Call                  | Argument types       | Selected overload          | Specificity | Path                                      |
| --------------------- | -------------------- | -------------------------- | ----------- | ----------------------------------------- |
| `(print "hello")`     | (String)             | `print(String)`            | +1000       | `StdLib.Print` — byte-identical preserve   |
| `(print 42)`          | (Int)                | `print(Void)`              | matches reverse-check (Void.IsCompatibleWith(Int)) | `StdLib.PrintAny` — auto-str via AutoStr   |
| `(if true X Y)`       | (Bool, ?, ?)         | `if(Bool, Void, Void)` or `if(Bool, Lazy, Lazy)` | +1000  | existing strict path — byte-identical      |
| `(if 5 X Y)`          | (Int, ?, ?)          | `if(Void, Void, Void)`     | matches via reverse-check | `StdLib.IfTruthy` — truthy-coerce  |
| `(not true)`          | (Bool)               | `not(Void)`                | matches via reverse-check | `StdLib.NotCharitable` — Bool path |
| `(not 5)`             | (Int)                | `not(Void)`                | matches via reverse-check | `StdLib.NotCharitable` — truthy-coerce |
| `(and true false)`    | (Bool, Bool)         | `and(Bool, Bool)`          | +1000       | `StdLib.AndBool` — Bool-shape preserved    |
| `(and 1 "foo")`       | (Int, String)        | `and(Void, Void)`          | matches via reverse-check | `StdLib.AndLastTruthy` — returns "foo" |

The +1000 typed overloads always win when arg types match exactly, ensuring zero regression in existing Bool / String / Lazy<Bool> call sites.

## Decisions Made

- **Expose ExecutionContext.ErrorReporter publicly** (rather than threading a separate `errorReporter` argument through every context-dependent registration). Mirrors FlowEngine.ErrorReporter; minimizes API surface delta and makes future strict-mode wildcards trivially registerable. Rationale: ctx already carries CurrentCallSite + CallerStrictMode; adding ErrorReporter completes the trio of fields needed by strict-aware leaf sites.
- **TwO-pass exact-then-wildcard lookup in TryGetImplementation** (Rule 1 fix — see Deviations). Backwards-compatible: existing single-impl-per-name and existing wildcard-only impls behave identically; only the new BOTH-typed-AND-wildcard case benefits. Required for the entire Plan 44-08 surface to work.
- **AutoStr falls through to `Value.ToString`** for reference-identity types (Sequence/Chord/Song/Tuning/Sfz/MarkovModel/LsystemModel/etc.) instead of hand-rolling per-type repr. Rationale: Value.ToString already handles every type (Tuples as `<<...>>`, Dicts as `{k: v}`, Arrays as `[...]`, music-types as their canonical string). Maintaining a parallel dispatch table in AutoStr would invite drift.
- **Reused existing per-type `StrInt`/`StrDecibel`/etc. format conventions in AutoStr** — `-12dB` prints with sign-prefix matching `StrDecibel`, `100ms` without sign-prefix matching `StrMillisecond`, etc. Composers reading `(print -12dB)` output should see the literal source form back.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Two-pass lookup needed in InternalFunctionRegistry.TryGetImplementation**

- **Found during:** Task 1 (PrintCharitablyTests initial run — `(print 42)` returned empty stdout instead of "42\n")
- **Issue:** When parsing `internal proc print (Void: s)` in std.flow, the Interpreter calls `registry.TryGetImplementation("print", Sig[Void], ...)`. The legacy single-pass lookup iterated registered overloads linearly and matched the FIRST signature whose types are compatible. The Void-cross compatibility rule in `TypesEqual` (lines 95-98) makes Void match ANY non-Lazy type, so the lookup matched `(String, StdLib.Print)` FIRST — silently binding the new `(Void)` surface decl to the wrong impl. At call time `(print 42)` then routed to `StdLib.Print` which called `args[0].As<string>()` on an Int, throwing InvalidCastException that was silently swallowed by the function-call dispatcher.
- **Fix:** Added a prefer-exact first pass via new `SignaturesMatchExactly` helper (per-slot `Equals`, no Void cross-matching). Second pass keeps the legacy `SignaturesMatch` for the single-wildcard-impl case (preserves backwards compatibility for `equals(Void,Void)`, `lt(Void,Void)`, etc.). Single-impl-per-name cases unchanged.
- **Files modified:** `flow-lang/StandardLibrary/InternalFunctionRegistry.cs` (added Pass 1 + SignaturesMatchExactly; preserved Pass 2)
- **Verification:** `(print 42)` now correctly routes to PrintAny via the new prefer-exact pass. All 8 PrintCharitablyTests + 13 IfTruthyCoerceTests/NotBuiltinTests + 6 AndOrLastTruthyTests Facts GREEN. Phase 44 earlier-wave tests (32 Facts across PragmaRegistryStrictTests / ExecutionContextStrictModeTests / etc.) still GREEN — no regressions. All 123 `tests/test_*.flow` scripts pass.
- **Committed in:** a17cb8c (Task 1 commit — the fix landed alongside the print registration since both are needed together)

---

**Total deviations:** 1 auto-fixed (Rule 1 bug)
**Impact on plan:** The auto-fix was load-bearing for Plan 44-08 — without prefer-exact lookup, none of the 5 new wildcard registrations would have correctly bound to their intended impls. Zero scope creep; the fix is narrowly scoped to dispatch correctness.

## Issues Encountered

- **Initial test failure on empty stdout** for `(print 42)` (Task 1). Diagnosed via verbose mode (`--verbose`) showing "Resolving 'print' with args (Int) — 1 candidate(s) checked, none matched, candidate: print(String)". Realized two issues simultaneously: (a) Flow surface decls in std.flow are required for new overloads (added the `internal proc print (Void: s)` line), and (b) the registry lookup needed prefer-exact to disambiguate when the surface decl uses a Void parameter type but a typed impl also exists. Fixed via the two-pass lookup pattern (see Deviations).
- **Flow `if` syntax confusion** in Task 2 initial tests — wrote `if cond { ... } else { ... }` (block syntax) instead of `(if cond then else)` (prefix syntax). Caught by the parser's `Unexpected token LBrace '{'` error on first test run. Rewrote all 7 IfTruthyCoerceTests Facts using prefix syntax + variable binding for result inspection.

## User Setup Required

None — no external service configuration; the new charitable wildcards work out-of-the-box in any FlowEngine.

## Next Phase Readiness

- **Plan 44-09 (Wave 5) is ready to layer strict tightening on top** — the `(print)/(if)/(not)/(and)/(or)` wildcard impls already branch on `ctx.CallerStrictMode` and emit `[strict] ...` errors with canonical wording. Plan 44-09 should:
  - Add the `[strict] (print) requires String — got <Type>` / `[strict] (if) requires Bool — got <Type>` / `[strict] (not) requires Bool — got <Type>` rows to `strict-error-manifest.csv`.
  - Tighten `(and)`/`(or)` strict path: when `CallerStrictMode == true`, the wildcard should emit `[strict] (and) requires Bool — got <Type>` / `[strict] (or) requires Bool — got <Type>` BEFORE the last-truthy short-circuit logic runs.
  - Pin all 5 error strings verbatim in `AxisCBoolRequiredTests` (REQ-STRICT-09 test suite).
- **No blockers** for downstream waves; Plan 44-08 is self-contained and does not depend on Plan 44-05/44-06/44-07 (Axis B advisory-to-error wiring).

## Self-Check: PASSED

**Files verified:**
```
FOUND: flow-lang.Tests/Integration/Phase44/PrintCharitablyTests.cs
FOUND: flow-lang.Tests/Integration/Phase44/IfTruthyCoerceTests.cs
FOUND: flow-lang.Tests/Integration/Phase44/NotBuiltinTests.cs
FOUND: flow-lang.Tests/Integration/Phase44/AndOrLastTruthyTests.cs
FOUND: flow-lang/Runtime/ExecutionContext.cs (modified: ErrorReporter accessor)
FOUND: flow-lang/StandardLibrary/StdLib.cs (modified: 7 new helpers)
FOUND: flow-lang/StandardLibrary/BuiltInFunctions.cs (modified: 5 new wildcard registrations)
FOUND: flow-lang/StandardLibrary/InternalFunctionRegistry.cs (modified: two-pass lookup)
FOUND: flow-lang/std.flow (modified: 5 new surface decls)
```

**Commits verified:**
```
FOUND: a17cb8c — Task 1 (Void-wildcard print + AutoStr)
FOUND: cab742c — Task 2 (Void-wildcard if + first (not) registration)
FOUND: c53816b — Task 3 (D-12 last-truthy and/or)
```

---
*Phase: 44-strict-mode*
*Completed: 2026-05-25*
