# Phase 12: Stability — Context

**Gathered:** 2026-04-19
**Status:** Ready for planning

<domain>
## Phase Boundary

Ship the v1.2 confirmed bug fixes (C1 body-skip → FIX-07a, C6 init([]) error → FIX-05, C7 Thunk caching → FIX-06), unblock the three failing test scripts (`test_custom_oscillator`, `test_while_loop`, `test_full_song`), and stand up an xUnit test framework that wraps the existing 70+ `.flow` integration tests as Theory cases. Each fix lands as a separate, bisectable commit with a regression test. The v1.1 soft-failure error model is preserved.

**In scope:**
- FIX-05: `init([])` raises an error matching `head([])` / `last([])` semantics
- FIX-06: `Thunk.Force()` caches evaluator failures and re-raises with original stack via `ExceptionDispatchInfo`
- FIX-07a: `ExecuteMusicalContext` body-skip — replace 7 early `return;` with `break;` so body runs under partial/default context
- New `flow-lang.Tests` xUnit project (first test framework in the repo)
- Wrap-as-Theory migration of all 70+ existing `.flow` test scripts (scripts unchanged; runner changes)
- Native C# xUnit unit tests for the three FIX-* internals
- `if(Bool, String, String)` overload registration (real cause of `test_custom_oscillator` failure)
- `exportWav` auto-creates parent directories via `Directory.CreateDirectory(Path.GetDirectoryName(path))`
- REQUIREMENTS.md edits closing TEST-01 / TEST-02 (already-implemented) and reframing TEST-03 (real failures: `if`-overload + dir creation)

**Out of scope:**
- C5 BREAKING CHANGE bundle (release notes, `augmentV1`/`diminishV1` aliases, `examples/*.flow` audit) — NOT triggered: C5 was Dismissed in Phase 11
- C2/C3/C4 work — Dismissed in Phase 11; closed by inline `AUDIT-VERIFIED 2026-04-18` markers
- TEST-04 Nyquist validation backfill — Phase 13
- Any DX work (DX-05/06/07/08/09) — Phases 14/15
- Tutorial refresh — Phase 16
- Rewriting `.flow` test logic into native C# Asserts — wrap-as-Theory only
- DryWetMidi / PulseAudio P/Invoke isolation in xUnit — defer until a concrete test needs it

</domain>

<decisions>
## Implementation Decisions

### Test scope reframing (empirical override of audit)

- **D-01:** REQUIREMENTS.md TEST-01/02/03 will be edited in plan 12-06 to reflect empirical reality. **TEST-01 closed:** `range(Int, Int)` is already implemented (verified via `test_custom_oscillator.flow:84` Test 4 running successfully). **TEST-02 closed:** `break`/`continue` are already interpreted at `Interpreter.cs:120-124,321-322,354-355` (verified via `test_while_loop.flow` passing with output `5,3,0,0,1,0,3`). **TEST-03 reframed:** `bpm()` / `createStereoTrack` / `renderBars` are all implemented; the actual failure of `test_full_song.flow` is missing `tests/output/` parent directory at the `exportWav` call. The actual failure of `test_custom_oscillator.flow:42` is a missing `if(Bool, String, String)` overload — not a `range` issue.

- **D-02:** `exportWav` auto-creates parent directories. One-line change in `flow-lang/StandardLibrary/Audio/FileIO.cs`: `Directory.CreateDirectory(Path.GetDirectoryName(path))` before writing. Broader DX win — matches `writeWav`, helps any user script writing to nested paths.

### FIX-07a — ExecuteMusicalContext body-skip

- **D-03:** Replace 7 early `return;` statements inside the `ExecuteMusicalContext` switch (`Interpreter.cs:151,164,178,224,240,255,263`) with `break;` so the body loop at `Interpreter.cs:271-285` executes under the partial/default musical context after a validation error. Each invalid context (tempo<=0, swing out of range, gain out of range, pan out of range, bad key, bad timesig) reports the error AND runs the body. Frame balance via `try/finally` at `Interpreter.cs:287-290` is correct and MUST NOT be altered.

- **D-04:** Soft-failure contract validation (ROADMAP success criterion 5) reuses existing tests — no new dedicated test required. `tests/spike/c1-musical-context-body.flow` flips RED→GREEN, AND `tests/test_musical_context_errors.flow` continues passing. Together they cover error accumulation, body-still-runs, and frame-stack balance.

### FIX-06 — Thunk caching

- **D-05:** On evaluator failure, capture the exception via `ExceptionDispatchInfo.Capture(ex)`, store on the thunk, and call `.Throw()` on subsequent `Force()` calls. Preserves original stack trace across re-throws — critical for debugging lazy-expression failures. Standard .NET pattern.

- **D-06:** Implement thread-safety via `Lazy<T>`-style refactor with `LazyThreadSafetyMode.ExecutionAndPublication`. Replace `Thunk` internal state (`_isEvaluated`, `_cachedValue`, `_evaluator`) with a `Lazy<Value>` wrapping the evaluator call; the failure-cache mechanic from D-05 plugs into the Lazy's value-factory. Future-proof even though the Flow interpreter is single-threaded today.

### FIX-05 — init([]) error

- **D-07:** `init([])` raises `InvalidOperationException("Cannot get init of empty array")` matching the existing `head`/`last` error format at `Collections.cs:78,79`. Replace the silent `Take(elements.Count - 1)` LINQ at `Collections.cs:84-92` with an empty-check that throws.

### Test framework adoption (xUnit)

- **D-08:** New `flow-lang.Tests` xUnit project added to the .NET solution. First test framework in the repo. Plan 12-01 scaffolds it as the foundation for all subsequent FIX-* regression tests.

- **D-09:** All 70+ existing `.flow` test scripts migrated via wrap-as-Theory: each `tests/test_*.flow` (and `tests/spike/c*.flow`) becomes a `[Theory]` data row that xUnit invokes by running `FlowEngine`, capturing stdout/stderr, and asserting on exit code + stdout substrings. The `.flow` scripts themselves are NOT rewritten — only the runner changes from `for test in tests/test_*.flow; do dotnet run --project flow-interpreter "$test"; done` to xUnit.

- **D-10:** New FIX-05/06/07a regression tests authored as native C# xUnit unit tests (against `Collections.Init`, `Thunk.Force`, `Interpreter.ExecuteMusicalContext` directly). Existing `.flow` integration coverage continues alongside via the wrap-as-Theory layer.

- **D-11:** `tests/spike/c1-musical-context-body.flow` registered as a Theory case expecting GREEN (success). Initially RED in the test report (committed in plan 12-01); the FIX-07a commit in plan 12-04 (returns→breaks) flips it GREEN within the same plan. Bisectability preserved — `git bisect` still pins the FIX-07a commit as the green-flip.

### Plan structure (6 plans, atomic commits per fix)

- **D-12:** **12-01** — xUnit harness scaffold (`flow-lang.Tests` csproj, solution wiring, FlowEngine test fixture) + wrap all 70+ `.flow` tests as Theory cases. Foundation; runs first. Spike c1 expected GREEN but lands RED until 12-04.

- **D-13:** **12-02** — FIX-05 `init([])` error + native xUnit unit test in `flow-lang.Tests`.

- **D-14:** **12-03** — FIX-06 Thunk caching: `Lazy<Value>` refactor with `ExecutionAndPublication` safety mode + `ExceptionDispatchInfo` failure-cache + native xUnit unit test.

- **D-15:** **12-04** — FIX-07a body-skip: 7 `return;`→`break;` in `Interpreter.cs` ExecuteMusicalContext switch. Same plan flips spike c1 Theory case from RED to GREEN. Adds `// AUDIT-VERIFIED 2026-04-19: C1 — Fixed (returns→breaks)` marker per Phase 11 D-02 convention.

- **D-16:** **12-05** — `if(Bool, String, String)` overload registration in StdLib + `exportWav` auto-mkdir in FileIO.cs. Both surface as part of the test-suite-green push; bundled because they're both small and have no interdependency with the FIX-* fixes.

- **D-17:** **12-06** — REQUIREMENTS.md edits closing TEST-01 / TEST-02 as already-implemented, reframing TEST-03 around the actual failure modes, plus a 12-VERIFICATION.md rollup pointing to the FIX-* commits. Lands last.

- **D-18:** Each plan produces atomic commits — multiple commits per plan when a plan covers multiple distinct concerns (e.g., 12-05 has two commits: `if`-overload, then `exportWav`). Bisectability preserved per ROADMAP success criterion 3 across both fix and test commits.

### Empirical findings (Phase 11 → 12 surprise)

- **F-01:** TEST-01/02/03 audit wording overshoots reality. The audit was authored from code reading; the actual failures are different from what was claimed. Phase 12's "test unblock" workload shrinks from 3 missing-built-in implementations to 1 missing `if`-overload + 1 directory-creation hardening.

- **F-02:** ROADMAP success criterion 4 (C5 BREAKING CHANGE bundle: release notes, `augmentV1`/`diminishV1` transitional aliases, `examples/*.flow` audit) is **not triggered**. C5 was Dismissed in Phase 11; the existing semantics are correct. No migration story shipped.

### Claude's Discretion

- Exact xUnit Theory case naming convention (e.g., `Tests.FlowScripts.test_while_loop` vs `Tests.Integration.WhileLoop`)
- Internal field names in the Lazy-refactored Thunk (e.g., `_lazy`, `_dispatchInfo`)
- Wording of REQUIREMENTS.md status-update lines for closed TEST-01/02
- Whether to add Theory `[InlineData]` per `.flow` script vs `[ClassData]` source generator
- Substring-assert expected-stdout fragments for each Theory case (which lines to pin)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Milestone planning
- `.planning/ROADMAP.md` "Phase 12: Stability" — 5 success criteria (esp. #3 bisectable commits, #5 soft-failure preservation; #4 NOT triggered per F-02)
- `.planning/REQUIREMENTS.md` §"Stability — Confirmed Bugs" — FIX-05, FIX-06 spec
- `.planning/REQUIREMENTS.md` §"Stability — Contingent on Spike Outcome" — FIX-07a spec (FIX-07b/c/d/e do not exist; C2-C5 dismissed)
- `.planning/REQUIREMENTS.md` §"Test Unblocking" — TEST-01/02/03 wording being reframed in plan 12-06

### Phase 11 handoff
- `.planning/phases/11-audit-spike/11-VERIFICATION.md` — Spike verdicts (C1 Confirmed, C2-C5 Dismissed); §"BREAKING CHANGE Trigger" confirms F-02
- `.planning/phases/11-audit-spike/11-CONTEXT.md` — D-02 marker convention (`// AUDIT-VERIFIED YYYY-MM-DD: …`); D-08 RED-test convention reused for 12-04
- `.planning/phases/11-audit-spike/11-01-SUMMARY.md` — Detailed C1 mechanism (7 early returns, lines 151,164,178,224,240,255,263)

### Audit (source of truth for C6, C7)
- `.planning/CODEBASE-AUDIT-2026-04-18.md` §1 row C6 — `init([])` returning `[]` silently
- `.planning/CODEBASE-AUDIT-2026-04-18.md` §1 row C7 — Thunk failure cache silent corruption

### Code targets
- `flow-lang/Interpreter/Interpreter.cs` — FIX-07a target; ExecuteMusicalContext switch at 131-291; current AUDIT-VERIFIED marker at line 292
- `flow-lang/Runtime/Thunk.cs` — FIX-06 target (full Lazy-refactor)
- `flow-lang/StandardLibrary/Collections.cs:84-92` — FIX-05 target (Init function); reference impls at lines 73-81 (Head/Last)
- `flow-lang/StandardLibrary/Audio/FileIO.cs` — `exportWav` auto-mkdir target
- `flow-lang/StandardLibrary/StdLib.cs` — `if` overload registration site (locate existing `if(Bool,T,T)` overloads to mirror)
- `flow-lang/StandardLibrary/BuiltInFunctions.cs:330-353` — Collections.Init registration (no signature change for FIX-05)

### Test targets
- `tests/spike/c1-musical-context-body.flow` — committed RED in Phase 11; flips GREEN via FIX-07a in plan 12-04
- `tests/test_musical_context_errors.flow` — soft-failure contract guard (must continue passing post-FIX-07a)
- `tests/test_custom_oscillator.flow:42` — failure point: `(if (gt frames1 0) "PASS" "FAIL")` needs `if(Bool,String,String)` overload
- `tests/test_full_song.flow:158-159` — failure point: `(exportWav mixed "tests/output/test_full_song.wav")` blocks on missing dir
- `tests/test_while_loop.flow` — already passing; xUnit Theory case asserts current behavior

### Solution / build
- `flow-lang.sln` — gains `flow-lang.Tests` project reference in plan 12-01
- `CLAUDE.md` "Build & Run Commands" — bash for-loop test runner currently documented; plan 12-06 may update to describe `dotnet test`

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **70+ existing `.flow` test scripts:** kept verbatim, wrapped by xUnit Theory layer in plan 12-01
- **`ExceptionDispatchInfo` (System.Runtime.ExceptionServices):** standard .NET primitive for D-05 stack-preserving re-throw
- **`Lazy<T>` with `LazyThreadSafetyMode` (System):** standard .NET primitive for D-06 Thunk refactor
- **`Directory.CreateDirectory` + `Path.GetDirectoryName` (System.IO):** standard primitives for D-02 exportWav auto-mkdir
- **`AUDIT-VERIFIED` marker convention** (Phase 11 D-02): extends with new dates; FIX-07a commit adds `2026-04-19` markers
- **Existing Head/Last error format** (`Collections.cs:73-81`): direct template for FIX-05 Init error message

### Established Patterns
- **Soft-failure error model:** validation errors accumulate in `ErrorReporter`; execution continues. FIX-07a preserves this by running body under partial/default context after error report.
- **Bisectable atomic commits per fix** (ROADMAP success criterion 3): inherited from Phase 11 D-08; each FIX-* lands as its own commit even when bundled into the same plan.
- **`.flow` stdout convention for tests:** kept; xUnit consumes via wrap-as-Theory rather than replacing.
- **Phase-numbered `VERIFICATION.md` rollup** (Phase 11 pattern): plan 12-06 produces `12-VERIFICATION.md` with one row per requirement and atomic commit hashes.
- **Switch-case validation in Interpreter** (`ExecuteMusicalContext`): each context type validates inline; FIX-07a turns each early-exit into a fall-through to the body loop.

### Integration Points
- **12-01 xUnit scaffold is upstream of 12-02..12-05** — plan ordering matters; planner should not parallelize 12-01 with FIX plans
- **12-02..12-04 can wave-parallelize** with each other (FIX-05 / FIX-06 / FIX-07a touch independent files: Collections.cs / Thunk.cs / Interpreter.cs)
- **12-05 (`if`-overload + exportWav) parallelizable** with 12-02..12-04
- **12-06 (REQUIREMENTS edits + verification rollup) lands last** — depends on all prior commits existing
- **`flow-lang.sln`:** gains `flow-lang.Tests` reference in 12-01
- **CLAUDE.md "Build & Run Commands" section:** may update in 12-06 to describe `dotnet test` invocation; existing `dotnet run --project flow-interpreter` instructions remain valid for ad-hoc script runs
- **Spike c1 RED→GREEN flip lives entirely inside plan 12-04** — single plan owns both the test expectation and the fix that satisfies it

</code_context>

<specifics>
## Specific Ideas

- **The audit was wrong about TEST-01/02/03 in three different ways.** Phase 12's "test unblock" workload was overestimated by REQUIREMENTS.md as written. Always run the failing test before scoping the fix — code review alone missed `if`-overload and dir-creation as the real causes.
- **Wrap-as-Theory keeps `.flow` tests as living spec.** The scripts ARE the language usage examples. Rewriting them in C# Asserts would lose that property. xUnit becomes the harness, not the assertion language.
- **The FIX-07a commit message should call out the body-skip mechanism explicitly** so that future audits reading the commit understand the fix preserves frame balance (which was correct) and changes only the early-exit behavior (which was the real bug).
- **Lazy<Value> with ExecutionAndPublication** is the .NET-canonical answer for memoize-with-failure-caching. Don't hand-roll the locking — use the primitive and plug the dispatch-info into the value factory.

</specifics>

<deferred>
## Deferred Ideas

- **CI integration of `dotnet test`** — runs locally fine after plan 12-01, but wiring it into a CI workflow (GitHub Actions, etc.) is a separate concern. Defer to a later DX phase.
- **Native C# rewrites of selected `.flow` tests** — wrap-as-Theory captures behavior; if a particular test deserves richer assertions (numeric tolerances, structured output diffing), promote case-by-case in future phases.
- **Lock-granularity tuning for Thunk** — Lazy<Value> ExecutionAndPublication is correct for the single-threaded interpreter; revisit if Flow gains concurrent evaluation.
- **DryWetMidi / PulseAudio P/Invoke isolation in xUnit** — these layers are not exercised by FIX-* tests; defer to a later phase that needs platform-isolated tests.
- **Migrating `.flow` test intent to native C# Asserts (full rewrite)** — out of scope; explicitly rejected during discussion as a 5x scope inflation.
- **REQUIREMENTS.md TEST-01/02 retained as audit-trail entries** — closed-as-already-done, but kept in the file with status update so the v1.2 paper trail shows what the audit claimed vs what reality was.

</deferred>

---

*Phase: 12-stability*
*Context gathered: 2026-04-19*
