---
phase: 15-composer-dx-part-2
plan: 02
subsystem: language-grammar
tags: [dx-07, grammar, runtime, musical-context, reverb-time, parser, interpreter]

# Dependency graph
requires:
  - phase: 15-composer-dx-part-2
    provides: Wave 0 test subtree scaffolding (Plan 15-01) — Unit/Phase15 + Integration/Phase15 directories
provides:
  - "`reverbTime <seconds> { ... }` musical-context grammar end-to-end through AST, lexer, parser, runtime, and interpreter"
  - "Silent-clamp-at-30s interpret-time behavior (D-03) with parse-time negative rejection"
  - "0.0 preserved as dry sentinel on MusicalContext.ReverbTime (D-02) — consumer (Plan 15-03) short-circuits Reverb.Apply"
  - "8-field GetMusicalContext walk + early-break predicate (RESEARCH Pitfall 1 regression pin)"
  - "7 Facts GREEN (F-01, F-03, F-04, F-05, F-22, F-23 + Parse_Zero_ProducesDry)"
affects:
  - 15-03 (SongRenderer + Reverb.Apply wiring — consumes MusicalContext.ReverbTime)
  - 15-06 (identifier audit + doc deliverable — references the grammar landed here)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Nullable-field inheritance rail: field + Clone + ToString + ExecutionContext walk + early-break predicate (all four touchpoints in one commit)"
    - "Parse-time vs interpret-time validation split (parse-time errors, interpret-time silent clamps)"
    - "Test-only C# probe registered on engine global frame + internal registry pre-Execute (no std.flow mutation required)"

key-files:
  created:
    - flow-lang.Tests/Unit/Phase15/ReverbTimeContextTests.cs (7 Facts)
  modified:
    - flow-lang/Ast/Statements/MusicalContextStatement.cs (enum entry)
    - flow-lang/Runtime/MusicalContext.cs (field + Clone + ToString)
    - flow-lang/Runtime/ExecutionContext.cs (walk + early-break)
    - flow-lang/Lexing/TokenType.cs (enum member)
    - flow-lang/Lexing/SimpleLexer.cs (keyword map)
    - flow-lang/Parsing/Parser.cs (dispatch gate + case body)
    - flow-lang/Interpreter/Interpreter.cs (context-switch case)

key-decisions:
  - "Probe pattern: register `probeMusicalContext` directly on engine global frame + internal registry in the test harness — avoided std.flow mutation because Plan 15-04 is concurrently editing it"
  - "Probe function name uses camelCase (no underscore) because `_` is a reserved Underscore token (rest in note streams)"
  - "Did NOT add a default for ReverbTime at the bottom of GetMusicalContext — null-is-sentinel, matching Pan/Gain treatment (defaults live at SongRenderer consumer site per 15-PATTERNS.md)"

patterns-established:
  - "Probe function via global-frame direct declaration: tests that need to observe runtime context state at specific frames can pre-register a C# impl (FunctionOverload.Internal) on engine.Context.GlobalFrame without requiring an std.flow declaration"

requirements-completed: [DX-07]

# Metrics
duration: 35min
completed: 2026-04-21
---

# Phase 15 Plan 02: DX-07 reverbTime Grammar & Runtime Summary

**`reverbTime <seconds> { ... }` musical-context grammar lands end-to-end: AST enum, nullable runtime field, 8-field ExecutionContext walk with updated early-break predicate, lexer keyword, parser dispatch + case body (parse-time negative rejection per D-03), and interpreter case body (silent clamp at 30s per D-03, 0.0 preserved as dry sentinel per D-02). 7 Facts GREEN covering F-01/F-03/F-04/F-05/F-22/F-23 plus Parse_Zero_ProducesDry.**

## Performance

- **Duration:** ~35 min
- **Started:** 2026-04-21T03:24:20Z (worktree reset to base 54c8b27)
- **Completed:** 2026-04-21T03:59:50Z
- **Tasks:** 2 (atomic commits)
- **Files modified:** 7 (existing) + 1 (new Facts file)

## Accomplishments

- DX-07 grammar ships through the full language pipeline up to but not including audio application (Plan 15-03 picks up MusicalContext.ReverbTime and wires Reverb.Apply in SongRenderer).
- MusicalContext now has 10 fields total: 2 typed defaults at resolution time (TimeSignature, Tempo, Swing) + 8 nullable inheriting fields (Key, Velocity, Pan, Gain, **ReverbTime** added) — ExecutionContext walk + 8-clause early-break predicate updated atomically per RESEARCH Pitfall 1.
- Parser rejects `reverbTime -N` at parse time with error text "reverbTime cannot be negative (RT60 is a time in seconds); got '-' at {location}" — error anchored at `-` sign, not at `{` (RESEARCH Pitfall 4).
- Interpreter silently clamps positive values > 30.0 via `Math.Min(rt60, 30.0)`; 0.0 preserved as sentinel (no error, no clamp-up — D-02 overrides ROADMAP #3 wording; Plan 15-06 will reframe the ROADMAP doc).
- 7 Facts GREEN (F-01, F-03, F-04, F-05, F-22, F-23 + Parse_Zero_ProducesDry). Full suite 267/267 (baseline was 260; +7 new, zero regressions).

## Task Commits

Each task committed atomically:

1. **Task 1: AST + Runtime field + Lexer keyword + ExecutionContext walk** — `ad3a0f9` (feat)
   - MusicalContextStatement.cs: `ReverbTime` as 10th enum member
   - MusicalContext.cs: nullable double field + Clone + ToString
   - ExecutionContext.cs: walk ReverbTime + 8-clause early-break predicate
   - TokenType.cs: `ReverbTime` enum member
   - SimpleLexer.cs: `"reverbTime" => TokenType.ReverbTime` keyword map

2. **Task 2: Parser dispatch + case + Interpreter case + 7 Facts** — `8a0a868` (feat)
   - Parser.cs: dispatch lookahead + case body with parse-time negative rejection
   - Interpreter.cs: `MusicalContextType.ReverbTime` case with `Math.Min(rt60, 30.0)` silent clamp
   - Tests/Unit/Phase15/ReverbTimeContextTests.cs: 7 new Facts (all GREEN)

## Files Created/Modified

- **flow-lang.Tests/Unit/Phase15/ReverbTimeContextTests.cs** (NEW) — 7 Facts with probe-function helper
- **flow-lang/Ast/Statements/MusicalContextStatement.cs** — `MusicalContextType.ReverbTime` enum member
- **flow-lang/Runtime/MusicalContext.cs** — nullable `ReverbTime` field, Clone + ToString extended
- **flow-lang/Runtime/ExecutionContext.cs** — GetMusicalContext walk + 8-clause early-break
- **flow-lang/Lexing/TokenType.cs** — `ReverbTime` enum member
- **flow-lang/Lexing/SimpleLexer.cs** — `"reverbTime"` keyword map entry
- **flow-lang/Parsing/Parser.cs** — dispatch gate + ReverbTime case body (parse-time negative rejection)
- **flow-lang/Interpreter/Interpreter.cs** — MusicalContextType.ReverbTime case (silent clamp at 30.0, preserve 0.0)

## Probe Hook Approach

**Decision:** Used direct `InternalRegistry.Register` + `GlobalFrame.DeclareFunction(FunctionOverload.Internal(...))` instead of the plan's two alternatives (Option A: FlowEngine.GetMusicalContextProbe hook; Option B: stdout-sentinel parsing).

**Rationale:**
- Option A would have required modifying FlowEngine.cs — adds production API surface just for tests, smells off.
- Option B (print-sentinel strings) cannot expose the full `MusicalContext` object; we'd only see whatever we chose to stringify, making F-22 (ReverbTime at outermost frame with all other fields set at inner frames) awkward to assert.
- Chosen approach: test constructs `FlowEngine` directly, registers `probeMusicalContext` as an internal function on the global frame, and the script invokes `(probeMusicalContext)` at its innermost scope. The C# impl snapshots `engine.Context.GetMusicalContext().Clone()` into a test-scoped `List<MusicalContext>`. Clean, observable, requires zero production-code changes, and does NOT touch `std.flow` (which Plan 15-04 is editing concurrently).

**Initial probe name was `__probeMusicalContext`; renamed to `probeMusicalContext`** after the first test run surfaced that `_` is lexed as `TokenType.Underscore` (the rest marker in note streams), producing `Unexpected token Underscore '_'` parse errors. See Deviations §1.

## Decisions Made

- **Probe approach:** direct global-frame function registration (see above).
- **Probe naming:** camelCase without underscores (see Deviations §1).
- **Full-suite baseline:** 260 (Wave 0 scaffolding already merged +3 on top of the 257 cited in the prompt); final 267 (+7 Facts).
- **No modifications to STATE.md or ROADMAP.md** — the orchestrator owns those writes per worktree-agent contract.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Probe function name cannot contain underscores**
- **Found during:** Task 2 (first test run after wiring Facts)
- **Issue:** Initially named the probe `__probeMusicalContext`. The SimpleLexer treats `_` as `TokenType.Underscore` (reserved as a rest marker in note streams), so `(__probeMusicalContext)` tokenizes as `( _ _probeMusicalContext )` and the parser emits "Unexpected token Underscore '_' at col 10". Six of the seven Facts failed with this error on first run.
- **Fix:** Renamed the probe to `probeMusicalContext` (camelCase, no underscore prefix). Tested all 7 Facts pass after the rename.
- **Files modified:** flow-lang.Tests/Unit/Phase15/ReverbTimeContextTests.cs
- **Verification:** All 7 Facts GREEN after the rename. Full suite 267/267.
- **Committed in:** `8a0a868` (rolled into Task 2 commit — the rename happened before Task 2 committed, so the broken intermediate state never landed in git history)

---

**Total deviations:** 1 auto-fixed (Rule 1 — bug in test author's identifier choice).
**Impact on plan:** Minor — identifier naming discovery, no scope creep, no production behavior affected.

## Issues Encountered

- None beyond the deviation above.

## Self-Check

**Files created/modified verified:**

- `flow-lang.Tests/Unit/Phase15/ReverbTimeContextTests.cs` — FOUND
- `flow-lang/Ast/Statements/MusicalContextStatement.cs` — FOUND
- `flow-lang/Runtime/MusicalContext.cs` — FOUND
- `flow-lang/Runtime/ExecutionContext.cs` — FOUND
- `flow-lang/Lexing/TokenType.cs` — FOUND
- `flow-lang/Lexing/SimpleLexer.cs` — FOUND
- `flow-lang/Parsing/Parser.cs` — FOUND
- `flow-lang/Interpreter/Interpreter.cs` — FOUND

**Commits verified:**

- `ad3a0f9` (Task 1: scaffold reverbTime grammar infrastructure) — FOUND
- `8a0a868` (Task 2: wire reverbTime parse + interpret + 7 Facts) — FOUND

**Test results:**

- `dotnet test flow-lang.Tests/flow-lang.Tests.csproj --filter "FullyQualifiedName~ReverbTimeContextTests" --nologo` → 7 passed, 0 failed
- `dotnet test flow-sharp.sln --nologo` → 267 passed, 0 failed

**Acceptance criteria (from PLAN.md §verification):**

- [x] `dotnet build flow-sharp.sln --nologo` exits 0 with zero errors
- [x] `dotnet test flow-lang.Tests/flow-lang.Tests.csproj --filter "FullyQualifiedName~ReverbTimeContextTests" --nologo` exits 0 with 7 Facts GREEN
- [x] `dotnet test flow-sharp.sln --nologo` exits 0 (zero Phase 1-14 regressions)
- [x] `grep -rn "reverbTime" examples/ tests/ flow-lang/*.flow` → only Wave 0 placeholder `tests/test_reverb_time.flow` (expected; Plan 15-03 replaces its body)
- [x] `grep -c "ReverbTime" flow-lang/Ast/Statements/MusicalContextStatement.cs flow-lang/Runtime/MusicalContext.cs flow-lang/Runtime/ExecutionContext.cs flow-lang/Lexing/TokenType.cs flow-lang/Parsing/Parser.cs flow-lang/Interpreter/Interpreter.cs` returns ≥1 for each
- [x] `grep -c "case MusicalContextType.ReverbTime" flow-lang/Parsing/Parser.cs` returns 1
- [x] `grep -c "case MusicalContextType.ReverbTime" flow-lang/Interpreter/Interpreter.cs` returns 1
- [x] `grep -Fc "reverbTime cannot be negative" flow-lang/Parsing/Parser.cs` returns 1
- [x] `grep -c "Math.Min(rt60, 30.0)" flow-lang/Interpreter/Interpreter.cs` returns 1
- [x] `grep -c "\[Fact\]" flow-lang.Tests/Unit/Phase15/ReverbTimeContextTests.cs` returns 7
- [x] All individual named-Fact filter commands from 15-VALIDATION.md pass

## Self-Check: PASSED

## Next Phase Readiness

- **Plan 15-03 (DX-07 audio application)** can now consume `MusicalContext.ReverbTime` in SongRenderer. The field is nullable; Plan 15-03 handles the null-as-inherit + 0.0-as-dry-sentinel + positive-as-RT60 dispatch (D-02, D-14, D-15, D-16).
- **Plan 15-06 (phase closure docs + identifier audit)** should fold the Wave-0 placeholder `tests/test_reverb_time.flow` into its closure checklist — that script remains a placeholder until Plan 15-03 replaces its body, at which point Wave 0's FlowScriptData sentinel rows become meaningful.
- No blockers for downstream DX-07 work. Zero regressions elsewhere in the test suite.

## Fact Wiring Status (from 15-VALIDATION.md)

| Fact ID | Status After This Plan |
|---------|------------------------|
| F-01 `Parse_Positive_StoresInContext` | GREEN |
| F-02 `Zero_ShortCircuitsReverb` (integration) | pending (Plan 15-03) |
| F-03 `Parse_Negative_ParseError` | GREEN |
| F-04 `Parse_AboveMax_ClampsTo30` | GREEN |
| F-05 `Nested_WithGain_Independent` | GREEN |
| F-22 `GetMusicalContext_AllFieldsResolvedSearchesReverbTime` | GREEN |
| F-23 `Nested_InsideTempoAndKey_Resolves` | GREEN |
| extra `Parse_Zero_ProducesDry` | GREEN (supporting, honors D-02) |

Phase 15 Facts so far: **7 GREEN** (6 from VALIDATION map + 1 supporting sentinel). Plan 03 unblocks F-02, F-06, F-07, F-08. Plan 04 brings DX-09 Facts (F-09..F-21) separately.

---
*Phase: 15-composer-dx-part-2*
*Plan: 02*
*Completed: 2026-04-21*
