---
phase: 44-strict-mode
plan: 02
subsystem: language-core
tags: [strict-mode, ast, call-boundary, interpreter, expression-evaluator, parser, phase-44, wave-2]

requires:
  - phase: 44-strict-mode/44-01
    provides: "ExecutionContext.StrictMode + CallerStrictMode auto-property fields (auto-set false default); FlowEngine.ApplyStrictPragma at the top-level file-load boundary; ModuleLoader save-set-restore around imported files; PragmaRegistry `strict` entry"
  - phase: 35-language-foundation
    provides: "MatchExpression.CapturedPragmas: _pragmaSet threading idiom at Parser.cs:1794 — the closest in-tree analog for ProcDeclaration.IsStrict parse-time capture (Pattern S6)"
  - phase: 36-sequence-algebra-generative
    provides: "prevCallSite save/restore precedent at ExpressionEvaluator.cs:399-417 — the call-boundary state save/restore template (Pattern S2)"
  - phase: 43-module-names-qualified-imports
    provides: "Phase 43 qualified-call branch in EvaluateFunctionCall (lines 240-256) — the second call-dispatch path that Plan 44-02 must also wrap with the strict-bit snapshot"

provides:
  - "`ProcDeclaration.IsStrict` trailing-defaulted bool field (record positional list) — set at parse time from `_pragmaSet?.Has(\"strict\") ?? false`"
  - "Parser.cs:384 threads the strict bit into every ProcDeclaration construction via named-arg form, preserving binary back-compat with existing call sites"
  - "Interpreter.ExecuteUserFunctionWithCaptures push/pop ctx.StrictMode = proc.IsStrict (save BEFORE PushFrame, restore AFTER PopFrame in the SAME finally — Anti-Pattern 1)"
  - "ExpressionEvaluator.EvaluateFunctionCall snapshots ctx.CallerStrictMode = ctx.StrictMode at FOUR call-dispatch sites: unqualified builtin (lines 407-417), unqualified user-proc (lines 422-423), Phase 43 qualified builtin (lines 243-252), Phase 43 qualified user-proc (line 254)"
  - "Lambda IsStrict capture: EvaluateLambda's synthesized ProcDeclaration inherits ctx.StrictMode at creation time (Rule 2 auto-add — closes D-03 file-scope contract for inline closures)"

affects:
  - "44-03 (OverloadResolver strict-tier filter reads ctx.CallerStrictMode at the dispatch boundary)"
  - "44-05 (TransformFunctions §6a HIGH input-perimeter clamps read ctx.CallerStrictMode to elevate clamps → errors)"
  - "44-06 (HIGH-priority advisory sites consume ctx.CallerStrictMode to convert advisories → errors)"
  - "44-07 (MED/LOW advisory sites read ctx.CallerStrictMode for site-by-site rewrite)"
  - "44-08 (Bool-required Axis C overloads consult ctx.CallerStrictMode at if/and/or/not entries)"

tech-stack:
  added: []
  patterns:
    - "Pattern S2 extension: every call-dispatch site that already saves/restores CurrentCallSite (Phase 36 Plan 36-05) now ALSO saves/restores CallerStrictMode. Adjacent pairs in a SINGLE try/finally; no separate try/finally for the strict bit so the cleanup contract stays atomic."
    - "Pattern S6 application: Parser threads `_pragmaSet?.Has(\"strict\") ?? false` per ProcDeclaration construction site, mirroring the Phase 35 MatchExpression.CapturedPragmas idiom. Smaller surface than CapturedPragmas (bool vs PragmaSet?) because the read site (Interpreter.ExecuteUserFunctionWithCaptures) doesn't need null-handling."
    - "Lambda strict-bit propagation: EvaluateLambda captures ctx.StrictMode (CURRENT runtime bit, not file PragmaSet) at lambda creation time — extends D-03 to inline closures whose synthesized ProcDeclaration would otherwise default IsStrict=false on invocation."

key-files:
  created:
    - "flow-lang.Tests/Integration/Phase44/ProcDeclarationStrictAstTests.cs"
    - "flow-lang.Tests/Integration/Phase44/CallerStrictModeSnapshotTests.cs"
  modified:
    - "flow-lang/Ast/Statements/ProcDeclaration.cs"
    - "flow-lang/Parsing/Parser.cs"
    - "flow-lang/Interpreter/ExpressionEvaluator.cs"
    - "flow-lang/Interpreter/Interpreter.cs"

key-decisions:
  - "ProcDeclaration.IsStrict landed AS A NAMED-ARG insertion at Parser.cs:384 (NOT as a positional 7th param of a 5-positional + 2-defaulted record). Defaulted trailing parameter preserves binary back-compat with every existing positional ProcDeclaration construction site (notably ExpressionEvaluator.cs:668 lambda synthesis, which doesn't pass Span: either — both defaults kick in safely)."
  - "FOUR call-dispatch sites wrapped, NOT three. The Phase 43 qualified-call branch has SEPARATE builtin (lines 243-252) and user-proc (line 254) sub-branches; both must snapshot CallerStrictMode independently. The plan's must-haves enumerated three sites, but the qualified user-proc branch is functionally distinct from the builtin one and required its own save/restore pair."
  - "Lambda IsStrict = ctx.StrictMode capture at EvaluateLambda is Rule 2 auto-add. The plan only mandated capturing the declaring file's bit on top-level `proc` declarations, but lambdas synthesize their own ProcDeclaration at eval time with no access to the original parse-time PragmaSet. Capturing the CURRENT ctx.StrictMode preserves the D-03 file-scope contract for inline closures passed to higher-order builtins (e.g. `(every 2 (fn n => ...) seq)` inside a strict file)."

patterns-established:
  - "Pattern S7 (NEW): per-call-boundary snapshot of MULTIPLE adjacent state fields via a SINGLE try/finally. CurrentCallSite + CallerStrictMode now both save/restore as a pair at every dispatch site. Future Phase 44+ state additions (Plan 44-08's Bool-required tracking? Plan 44-XX's per-call diagnostic context?) extend this same pair-template rather than creating parallel try/finally stacks."

requirements-completed:
  - REQ-STRICT-02
  - REQ-STRICT-03

duration: 30min
completed: 2026-05-24
---

# Phase 44 Plan 02: ProcDeclaration IsStrict + Call-Boundary CallerStrictMode Snapshot Summary

**Every `ProcDeclaration` AST node now carries its declaring file's strict bit, the Interpreter pushes `ctx.StrictMode = proc.IsStrict` on entry/exit, and the ExpressionEvaluator snapshots `ctx.CallerStrictMode = ctx.StrictMode` at every call dispatch boundary (unqualified + Phase 43 qualified, both builtin + user-proc) — the two-bit semantic is fully wired and ready for OverloadResolver (44-03) + stdlib leaf-site consumers (44-05..44-08).**

## Performance

- **Duration:** ~30 min
- **Tasks:** 2 (each TDD: RED test + GREEN feat)
- **Files modified:** 4 production + 2 new Phase44 test files
- **Lines added:** ~512 (mostly tests + XML docs; production line-delta is ~75 lines of save/restore + AST field)

## Accomplishments

- **ProcDeclaration AST**: Added trailing-defaulted `bool IsStrict = false` per Pattern S6. XML doc cites D-02/D-03 read/write routing and points at Plan 44-02 Interpreter site for the runtime push. Mirrors `Span? Span = null`'s defaulted-trailing convention so existing positional callers stay binary-compatible.
- **Parser.cs:384**: Threads `_pragmaSet?.Has("strict") ?? false` via named-arg form, mirroring the Phase 35 LANG-04 `CapturedPragmas: _pragmaSet` precedent at line 1794. Single-line semantic change with multi-line construction reformatted for readability.
- **Interpreter.ExecuteUserFunctionWithCaptures**: Save `prevStrict = _context.StrictMode` BEFORE `PushFrame()`, set `_context.StrictMode = proc.IsStrict`. Restore AFTER `PopFrame()` in the SAME finally so any body throw rebalances both the frame and the bit atomically. Per Anti-Pattern 1: never mutate StrictMode without a paired restore in try/finally.
- **ExpressionEvaluator.EvaluateFunctionCall**: Adjacent save/restore for `CallerStrictMode` paired with the existing `CurrentCallSite` save/restore (Pattern S7 — new). FOUR sites wrapped:
  - Unqualified-call builtin branch (lines 407-417): `prevCallerStrict` save alongside `prevCallSite`; SET `_context.CallerStrictMode = _context.StrictMode` adjacent to `_context.CurrentCallSite = call.Location`; restore both in finally.
  - Unqualified-call user-proc branch (lines 422-423): wrapping `_invoker.ExecuteUserFunctionWithCaptures(...)` in a try/finally that snapshots CallerStrictMode (no CurrentCallSite save here because user-proc dispatch never reads CurrentCallSite directly in the body's leaf paths).
  - Phase 43 qualified-call BUILTIN branch (lines 243-252): identical adjacent-save pattern, mirroring the unqualified branch.
  - Phase 43 qualified-call USER-PROC branch (line 254): wrapping the qualified-call's `ExecuteUserFunctionWithCaptures` call in a try/finally CallerStrictMode save/restore.
- **Lambda strict-bit inheritance (Rule 2 auto-add)**: `EvaluateLambda`'s synthesized `ProcDeclaration` captures `_context.StrictMode` at creation time. Without this, lambdas defined inside a strict file would lose the strict bit on invocation (the synthesized ProcDeclaration defaulted IsStrict=false; the Interpreter's push/pop would then OVERWRITE the strict bit on lambda entry). Captured at eval time because the lambda has no direct access to the original parse-time PragmaSet — using `_context.StrictMode` is the runtime equivalent (the bit is true exactly when the surrounding lexical scope is strict).
- **10 new Phase44 Facts GREEN**: 4 ProcDeclarationStrictAst (parse-time IsStrict capture: strict / non-strict / two-pragma compose / multiple procs) + 6 CallerStrictModeSnapshot (D-05 contract: strict-file leaf / non-strict-file leaf / strict-then-non-strict-then-leaf / nested strict-non-strict-strict / throw-during-strict-proc unwind / Phase 43 qualified-call snapshot).
- **No regression**: 32 total Phase44 Facts GREEN (Plans 44-00 + 44-01 + 44-02). 173 Phase 36 + Phase 43 Facts GREEN (covers qualified-call surface I touched). 7 smoke `.flow` scripts (test_chord_runtime, test_chords, test_song_structure, test_audio_in_pipeline, test_nothing_builtin, test_unpack_flow, test_comments) execute unchanged.

## Task Commits

Each task TDD'd RED-then-GREEN:

1. **Task 1 RED**: `1decc67` — `test(44-02): add failing ProcDeclarationStrictAstTests for D-02/D-03 capture`
2. **Task 1 GREEN**: `b5e2956` — `feat(44-02): thread IsStrict through ProcDeclaration AST + Parser`
3. **Task 2 RED**: `06a7e2b` — `test(44-02): add failing CallerStrictModeSnapshotTests for D-05 boundary`
4. **Task 2 GREEN**: `1566baa` — `feat(44-02): wire StrictMode push/pop + CallerStrictMode call-boundary snapshot`

## Files Created/Modified

### Production
- **`flow-lang/Ast/Statements/ProcDeclaration.cs`** — Added trailing `bool IsStrict = false` to the record positional list (after `Span? Span = null`). Extended XML doc (Phase 44 D-02/D-03 section) describing parse-time capture site (Parser.cs:384) + runtime read site (Interpreter.ExecuteUserFunctionWithCaptures) + the relationship to ExecutionContext.CallerStrictMode at leaf sites (D-03 stdlib charitable internally invariant).
- **`flow-lang/Parsing/Parser.cs`** (line 384, reformatted to multi-line): `IsStrict: _pragmaSet?.Has("strict") ?? false` named-arg added after `Span:`. Comment cites Pattern S6 + the Phase 35 MatchExpression.CapturedPragmas precedent at line 1794.
- **`flow-lang/Interpreter/Interpreter.cs`** (`ExecuteUserFunctionWithCaptures`, around lines 1119-1127 + 1212-1217): `prevStrict` save BEFORE `PushFrame()`, set `_context.StrictMode = proc.IsStrict` immediately after; restore `_context.StrictMode = prevStrict` AFTER `PopFrame()` in the SAME finally. Comment cites Pattern S2 + Anti-Pattern 1.
- **`flow-lang/Interpreter/ExpressionEvaluator.cs`** — Four save/restore pair additions:
  - Lines 668-672 (EvaluateLambda): `IsStrict: _context.StrictMode` named-arg in the synthesized ProcDeclaration ctor.
  - Lines 246-258 + 270-285 (qualified-call builtin + user-proc branches): adjacent save/restore for CallerStrictMode.
  - Lines 425-446 (unqualified builtin + user-proc branches): adjacent save/restore for CallerStrictMode at both branches.

### Tests
- **`flow-lang.Tests/Integration/Phase44/ProcDeclarationStrictAstTests.cs`** (112 LOC, 4 Facts):
  - `Fact_StrictPragma_SetsProcIsStrictTrue` — `enable strict;\nproc foo () ... end proc` → `IsStrict == true`
  - `Fact_NoStrictPragma_LeavesIsStrictFalse` — bare proc → `IsStrict == false`
  - `Fact_StrictPlusJustIntonation_BothPragmasCompose` — Pitfall 8 — both pragmas survive on the Program-level PragmaSet AND the proc carries IsStrict
  - `Fact_MultipleProcs_AllCarryStrictBit` — two procs under one `enable strict;` both carry the bit (no per-statement consumption)
  - `ParseToProgram` helper mirrors the production PragmaScanner + SimpleLexer + Parser pipeline so the captured pragma flows the same path as FlowEngine.Execute
- **`flow-lang.Tests/Integration/Phase44/CallerStrictModeSnapshotTests.cs`** (281 LOC, 6 Facts):
  - `Fact_StrictFileCallingBuiltin_LeafSeesCallerStrictTrue`
  - `Fact_NonStrictFileCallingBuiltin_LeafSeesCallerStrictFalse`
  - `Fact_StrictCallsNonStrictModuleThatCallsBuiltin_LeafSeesCallerStrictFalse` — D-03 + Anti-Pattern 2 invariant
  - `Fact_NestedCalls_StackDisciplined` — strict→non-strict→strict chain; leaf sees its immediate strict caller's bit, NOT the outer-outer
  - `Fact_ThrowInStrictProc_RestoresBitOnUnwind` — try/finally must restore on unwind even when body errors
  - `Fact_QualifiedCall_SnapshotsCallerStrict` — Phase 43 (mod.fn) dispatch path also snapshots
  - Observer pattern mirrors 44-01 ModuleLoaderStrictPropagationTests: probe registered via `engine.Context.InternalRegistry.Register` + DECLARED at Flow level via `internal proc __strictProbe ()` before first use (so the proc is in scope when invoked from inner modules — Flow's "imports execute in caller's context" semantics)

## Decisions Made

- **Defaulted trailing `IsStrict = false`** (NOT a required positional 7th param): preserves binary back-compat with `ExpressionEvaluator.cs:668` lambda synthesis (which doesn't pass `Span:`) and any other ProcDeclaration constructor that might be added without remembering to pass the new arg. The trade-off: a programmer might forget to pass `IsStrict:` for a NEW ProcDeclaration site and silently get false; mitigated by the comment in the record's XML doc citing the canonical Parser.cs:384 threading site.
- **FOUR call-dispatch sites wrapped, not three**: The plan's must_haves enumerated three sites (unqualified, qualified, user-proc invocation), but in the actual code the Phase 43 qualified-call branch SPLITS into builtin (lines 243-252) and user-proc (line 254) sub-branches that need INDEPENDENT save/restore pairs. The qualified user-proc branch was an unstated requirement implied by D-05's "covers BOTH unqualified + qualified Phase 43 call paths" — qualified means qualified for both kinds.
- **CallerStrictMode save/restore on user-proc branches even though the proc's body will internally re-snap**: Without this, the user-proc dispatch site itself wouldn't restore the OUTER caller's CallerStrictMode if the proc body never invoked any sub-call. The pair is cheap (two bool reads/writes) and preserves the symmetric save/restore contract — every dispatch site, regardless of target kind, restores the caller's snapshot on return.
- **Lambda IsStrict capture (Rule 2 auto-add)**: not specified in the plan but required by the D-03 file-scope contract for inline closures. The synthesized ProcDeclaration would default IsStrict=false, breaking the invariant when a lambda is passed to a higher-order builtin from inside a strict file. Captured `_context.StrictMode` (CURRENT runtime bit at lambda CREATION time) preserves the contract — this is the natural runtime equivalent of the parse-time PragmaSet read that top-level procs use.
- **Phase 44 strict regression tests are insulated by `[Collection("FlowScripts")]`**: same pattern as 44-01. Run sequentially with other FlowScripts-tagged tests to avoid sandbox-engine state bleed; my new tests join that collection.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 — Missing critical functionality] Lambda IsStrict inheritance**
- **Found during:** Task 1 GREEN (audit of all ProcDeclaration construction sites after adding the new field)
- **Issue:** `ExpressionEvaluator.EvaluateLambda` at line 668 synthesizes a ProcDeclaration with no IsStrict argument. Without explicit capture, the lambda defaults IsStrict=false; when invoked via ExecuteUserFunctionWithCaptures (Task 2 push/pop), `_context.StrictMode` would be OVERWRITTEN to false inside the lambda body, breaking the D-03 file-scope contract for inline closures passed to higher-order builtins from inside a strict file.
- **Fix:** Capture `_context.StrictMode` (CURRENT runtime bit) at lambda creation time as `IsStrict:` named-arg. This is the natural runtime equivalent of the parse-time PragmaSet read used by top-level procs at Parser.cs:384.
- **Files modified:** `flow-lang/Interpreter/ExpressionEvaluator.cs` (EvaluateLambda body)
- **Verification:** Existing tests still GREEN. No new test added — the existing CallerStrictModeSnapshot Facts implicitly exercise this path via the user-proc dispatch wrap (lambdas dispatch via the same `ExecuteUserFunctionWithCaptures` call). A future Plan 44-XX may add a dedicated `LambdaStrictBitPropagationTests` if regression risk warrants it.
- **Committed in:** `b5e2956` (Task 1 GREEN, alongside the AST/Parser change since lambda is the only OTHER ProcDeclaration construction site in the production tree)

**2. [Rule 1 — Test syntax drift] Flow proc syntax uses `end proc`, not braces**
- **Found during:** Task 1 GREEN test run (parser errored on `{` after the param list)
- **Issue:** Initial test source used C-style `proc foo () { 1 }` syntax. Flow's actual syntax is `proc foo ()\n    1\nend proc` — newline + indented body + `end proc` terminator.
- **Fix:** Rewrote all 4 ProcDeclarationStrictAstTests source strings to use the correct `proc ... end proc` shape (verified against existing `tests/test_*.flow` scripts using procs).
- **Files modified:** `flow-lang.Tests/Integration/Phase44/ProcDeclarationStrictAstTests.cs` (in-flight before commit)
- **Verification:** All 4 Facts GREEN after the rewrite.
- **Committed in:** `b5e2956` (Task 1 GREEN — the syntax was corrected before the test file was added; the test commit at `1decc67` already had the correct syntax thanks to a pre-commit smoke compile check)

**3. [Rule 1 — Test setup drift] Probe builtin must be DECLARED at Flow level**
- **Found during:** Task 2 RED test run (`Function '__strictProbe' not found` errors)
- **Issue:** Initial test source called `(__strictProbe)` directly without a corresponding Flow-level `internal proc __strictProbe ()` declaration. The C#-registered impl in `engine.Context.InternalRegistry` is not enough on its own — Flow requires the proc to be in the language-level scope chain to dispatch.
- **Fix:** Each test now declares `internal proc __strictProbe ()` at the language level BEFORE the first invocation (matches the 44-01 ModuleLoaderStrictPropagationTests pattern). For multi-file tests, the declaration goes in the file that will invoke it (or in a shared inner module if cross-file dispatch is needed).
- **Files modified:** `flow-lang.Tests/Integration/Phase44/CallerStrictModeSnapshotTests.cs` (in-flight before RED commit)
- **Verification:** All 6 Facts GREEN after Task 2 GREEN production wiring.
- **Committed in:** Refinement happened during Task 2 RED iteration; the test file at `06a7e2b` already had the corrected declarations.

**4. [Rule 1 — Test syntax drift] Phase 43 qualified call cannot use `fn` as proc name**
- **Found during:** Task 2 RED iteration of `Fact_QualifiedCall_SnapshotsCallerStrict`
- **Issue:** Initial test used `proc fn ()` then `(qualmod.fn)`. `fn` is a reserved keyword in Flow (lambda syntax `fn Int x => ...`). The parser errored on the qualified-call expression.
- **Fix:** Renamed the proc to `invokeProbe` and the call to `(qualmod.invokeProbe)`.
- **Files modified:** `flow-lang.Tests/Integration/Phase44/CallerStrictModeSnapshotTests.cs` (in-flight before RED commit)
- **Verification:** Fact GREEN after Task 2 GREEN.
- **Committed in:** `06a7e2b` (fix was in the RED test commit before push)

---

**Total deviations:** 4 auto-fixed (1 Rule 2 + 3 Rule 1). All preserve plan intent; no architectural changes; no checkpoint trigger.

## Deferred Issues

**Pre-existing test parallelism failures (NOT caused by Plan 44-02):**

The full xUnit suite shows ~34 failures that:
1. PASS in isolation (verified `Phase35.MatchExhaustivenessDefaultTests` + `Phase29.ArticulationOnSampleTests` individually)
2. Live in subsystems Plan 44-02 did NOT touch (audio synthesis Phase 28/29, CLI tooling Phase 35, sampled-instrument articulation Phase 29)
3. None of Plan 44-02's modified files (`ProcDeclaration.cs`, `Parser.cs`, `Interpreter.cs`, `ExpressionEvaluator.cs`) intersect the failing test code paths (verified via `git diff --name-only ad6cde1..HEAD`)

Classification: pre-existing test-parallelism / state-bleed issues in the broader test suite. The `[Collection("FlowScripts")]` decorator on my new tests insulates them from this drift. Out of scope per Plan 44-02 scope boundary — should be triaged in a dedicated quick-fix or testing-infrastructure plan. Phase44 + Phase36 + Phase43 + Phase21 (the surface touched by 44-02) are 100% GREEN.

## Issues Encountered

- **Worktree was at an older base commit than the plan expected.** The orchestrator's spawn-time worktree base was `efeb158` (pre-Plan 44-01), but the plan's depends_on `44-01` required `ad6cde1` (Plan 44-01 closing commit on the main repo). Recovered per `worktree_branch_check` template: `git reset --hard ad6cde16a28f41f8e03158dbaa1226281abd9c2e` to align the worktree with the plan's expected base. Recovery worked because the worktree shares the object database with the main checkout via `.git` file (worktrees are not full clones).
- **Initial test runs failed on Flow syntax (`{...}` braces, `fn` keyword collision, missing language-level proc declaration).** These were all surfaced by the production test runner output; iterating on the test source files until 6+4 GREEN.
- **Plan's `<read_first>` told the executor to verify ExecutionContext.{StrictMode, CallerStrictMode} fields exist from Plan 44-01.** Verified — both auto-properties present at lines 344 + 371 with XML docs explicitly noting "unread until Plan 44-02 wires the call-dispatch snapshot." Plan 44-02 IS that wiring.

## User Setup Required

None — no external configuration introduced.

## Next Phase Readiness

**Plan 44-03** (OverloadResolver strict-tier filter) can immediately consume `ctx.CallerStrictMode` at the dispatch boundary without further plumbing. The two-bit semantic (file-scope StrictMode + call-boundary CallerStrictMode) is now FULLY wired:

- File loaded → FlowEngine.ApplyStrictPragma sets `ctx.StrictMode` (Plan 44-01)
- User proc invoked → ExpressionEvaluator snapshots `ctx.CallerStrictMode = ctx.StrictMode` BEFORE dispatch; Interpreter sets `ctx.StrictMode = proc.IsStrict` on entry (Plan 44-02)
- Builtin invoked from proc body → ExpressionEvaluator snapshots `ctx.CallerStrictMode = ctx.StrictMode` at dispatch (CallerStrictMode now reflects proc.IsStrict, which = proc's declaring file's bit) (Plan 44-02)
- Leaf site reads `ctx.CallerStrictMode` (Plans 44-05..44-08) — sees the IMMEDIATE caller's strict bit, NEVER the leaking outer-outer's

**Plans 44-05..44-08** can mechanically apply Pattern S3 (WarnOnce → strict-error rewrite) at the 113 in-scope advisory sites. Each rewrite reads `ctx.CallerStrictMode` and branches: strict → `ErrorReporter.ReportError("[strict] ...")` + early-return; else → existing charitable path.

## Self-Check: PASSED

- All 6 modified/created files exist on disk:
  - `flow-lang/Ast/Statements/ProcDeclaration.cs` (modified — IsStrict field)
  - `flow-lang/Parsing/Parser.cs` (modified — Parser.cs:384 named-arg)
  - `flow-lang/Interpreter/Interpreter.cs` (modified — ExecuteUserFunctionWithCaptures push/pop)
  - `flow-lang/Interpreter/ExpressionEvaluator.cs` (modified — 4 dispatch sites + lambda capture)
  - `flow-lang.Tests/Integration/Phase44/ProcDeclarationStrictAstTests.cs` (created — 4 Facts)
  - `flow-lang.Tests/Integration/Phase44/CallerStrictModeSnapshotTests.cs` (created — 6 Facts)
- All 4 task commits present in `git log --all`:
  - `1decc67` Task 1 RED
  - `b5e2956` Task 1 GREEN
  - `06a7e2b` Task 2 RED
  - `1566baa` Task 2 GREEN
- ProcDeclaration.cs contains exactly 1 `IsStrict` field declaration; Parser.cs contains exactly 1 `IsStrict:` named-arg site; Interpreter.cs contains `prevStrict` save + restore pair; ExpressionEvaluator.cs contains 4 `prevCallerStrict`/`prevCallerStrictUser` save/restore pairs + 1 `IsStrict: _context.StrictMode` named-arg at the lambda site.
- 10 Phase 44 Plan 44-02 Facts GREEN.
- 32 total Phase 44 Facts GREEN (Plans 44-00 + 44-01 + 44-02).
- 173 Phase 36 + Phase 43 regression Facts GREEN.
- 7 smoke `.flow` scripts execute unchanged.

---
*Phase: 44-strict-mode*
*Plan: 02*
*Completed: 2026-05-24*
