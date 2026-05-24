---
phase: quick-260524-r4o
plan: 01
subsystem: interpreter/runtime/typesystem
tags: [perf, dispatch-hot-path, allocation-reduction, bundle-a]
dependency_graph:
  requires:
    - flow-lang/Runtime/StackFrame.cs (GetFunctionOverloads, HasFunction, FunctionOverload)
    - flow-lang/Runtime/ExecutionContext.cs (_overloadResolver field, ResolveFunction, TryResolveFunction)
    - flow-lang/TypeSystem/OverloadResolver.cs (Resolve, named-arg dispatch body)
    - flow-lang/Interpreter/ExpressionEvaluator.cs (EvaluateFunctionCall)
    - flow-lang/Diagnostics/ErrorReporter.cs (ReportError surface)
  provides:
    - StackFrame.GetFunctionOverloads fast-path read-only contract
    - OverloadResolver.Resolve(IReadOnlyList<FunctionOverload>, ..., bool silent) FunctionOverload-direct overload
    - OverloadResolver.ResolveCore private helper with parameterized ErrorReporter
    - OverloadResolver.SilentReporter shared lazy-allocated fire-and-forget reporter
    - ExecutionContext.TryResolveFunction 3-line reuse of _overloadResolver in silent mode
    - ExpressionEvaluator.EvaluateFunctionCall single-pass pre-sized argValues + argTypes build
  affects:
    - Every function-call dispatch in the interpreter (every (fn ...) S-expr)
    - Phase 36 PrngRegistry two-run cmp-clean determinism (preserved — verified)
    - Phase 28 articulation render path (preserved — no audio render touched)
    - Phase 39 notation export byte-stability (preserved — no notation path touched)
tech_stack:
  added: []
  patterns:
    - "Pre-sized fixed-capacity collection construction (avoid LINQ + boxed enumerator + growable List)"
    - "Lazy-allocated shared single-instance error reporter for silent fire-and-forget probes"
    - "Reference-equality scan to recover FunctionOverload from FunctionSignature lookup result"
    - "Extract-method refactor: shared body in private helper taking the differing dependency (ErrorReporter) as parameter"
key_files:
  created: []
  modified:
    - flow-lang/Runtime/StackFrame.cs
    - flow-lang/Runtime/ExecutionContext.cs
    - flow-lang/TypeSystem/OverloadResolver.cs
    - flow-lang/Interpreter/ExpressionEvaluator.cs
decisions:
  - "Task 3 Part B (lazy localReporter in named-arg branch) landed inside the Task 2 commit, because it lives in the ResolveCore helper that Task 2 extracts — splitting it across two commits would either (a) leave the helper inconsistent or (b) require touching the same lines twice. Task 3's actual grep gates (live entirely in ExecutionContext.cs) are unaffected and all pass."
  - "Added `using FlowLang.Runtime;` to OverloadResolver.cs (Rule 3 blocking fix — FunctionOverload lives in FlowLang.Runtime namespace, not FlowLang.TypeSystem)."
  - "Adjusted one inline comment in TryResolveFunction so the `new OverloadResolver` grep gate returns exactly 1 (constructor field init) and not 2 (comment + constructor). Behavior unchanged."
  - "Kept the legacy Resolve(IReadOnlyList<FunctionSignature>, ...) public method as a one-liner forwarder to ResolveCore — preserves backward compat per LOW-risk scope discipline."
metrics:
  duration: ~25min
  completed: 2026-05-24
---

# Quick Task 260524-r4o: Bundle A — Function Dispatch Hot Path Summary

Four LOW-risk allocation-reduction micro-optimizations to the function-call dispatch hot path. Every `.flow` script that calls a function (i.e. every `.flow` script) executes through this path, so even tiny per-call wins compound. All behavior preserved — Phase 36 PrngRegistry two-run cmp-clean determinism contract, Phase 28 articulation render path, and Phase 39 notation export byte-stability all preserved unchanged. No FP arithmetic touched, no PRNG ordering touched, no audio render path touched.

## Task Narratives

**Task 1 — StackFrame.GetFunctionOverloads fast path (commit 73e2cc5).** The legacy implementation unconditionally allocated `new List<FunctionOverload>()` + `AddRange` per call, even when overloads lived entirely in the current frame (the common case for builtins called from the global frame). The fast path now returns the internal `_functions[name]` list directly when no parent shadow exists, walking the parent chain via the existing cheap `HasFunction` ContainsKey probe (zero allocs). Multi-frame shadow case still copies via the merge path. All 5 in-tree callers were audited at planning time and confirmed read-only — XML-doc-comment documents the contract so future callers either honor it or fix themselves (not this method). Public return type stays `List<FunctionOverload>` per LOW-risk scope (changing to `IReadOnlyList<FunctionOverload>` is a separate refactor).

**Task 2 — OverloadResolver FunctionOverload-direct overload + ResolveFunction caller update (commit 1086fad).** The legacy `ExecutionContext.ResolveFunction` flow allocated three things per call beyond the actual resolution work: an `overloads.Select(o => o.Signature).ToList()` projection (3 allocs: LINQ iterator + boxed enumerator + growable List), the resolver's internal candidate iteration overhead, and an `overloads.FirstOrDefault(o => o.Signature == sig)` reverse-lookup. The new `OverloadResolver.Resolve(IReadOnlyList<FunctionOverload>, ..., bool silent)` overload collapses these into one fixed-size `FunctionSignature[]` allocation followed by a reference-equality scan (each `FunctionOverload` owns its own `FunctionSignature` instance, so reference equality is correct). The legacy `Resolve(IReadOnlyList<FunctionSignature>, ...)` public method is preserved as a one-liner forwarder for backward compat — the shared scoring/named-arg body extracted into a private `ResolveCore(..., ErrorReporter reporter)` helper that takes the reporter as a parameter (decouples error emission from the field so silent probes — Task 3 — can route to a shared reporter). One small Rule 3 blocking fix: `OverloadResolver.cs` needed a `using FlowLang.Runtime;` directive since `FunctionOverload` lives in the Runtime namespace, not TypeSystem.

**Task 3 — TryResolveFunction reuses _overloadResolver in silent mode (commit 7538b19).** The legacy `TryResolveFunction` body allocated a fresh `new OverloadResolver(new ErrorReporter())` per probe — the entire dispatch-fallback path through `ExpressionEvaluator.EvaluateFunctionCall` calls this on every function call before considering lambdas. The replacement is 3 lines: reuse the existing `_overloadResolver` field via the silent flag added in Task 2. The silent flag routes rejection diagnostics into the resolver's shared lazy-allocated `SilentReporter` instance — the reporter's accumulated errors are never read or flushed (callers don't consume them) so the shared-instance reuse is safe. Task 3 Part B (lazy `localReporter` in named-arg branch) landed inside Task 2's commit because it lives in the `ResolveCore` helper that Task 2 extracts — splitting it across two commits would either leave the helper inconsistent or require touching the same lines twice. Task 3's actual grep gates (live entirely in ExecutionContext.cs) all pass. Adjusted one inline comment in `TryResolveFunction` so the `new OverloadResolver` grep gate returns exactly 1 (constructor field init only); behavior unchanged.

**Task 4 — ExpressionEvaluator single-pass argValues + argTypes build (commit 7c00f68).** The legacy `EvaluateFunctionCall` body evaluated arguments via `call.Arguments.Select(Evaluate).ToList()` (3 allocs) and then re-traversed the resulting list with `argValues.Select(v => v.Type).ToList()` (3 more allocs) — 6 allocations per dispatched call where 2 (a pre-sized `List<Value>` + a fixed-size `FlowType[]`) would do. The replacement is a single pre-sized loop. `FlowType[]` satisfies `IReadOnlyList<FlowType>` at every downstream consumer (`TryResolveFunction` and `ResolveFunction` fallback), so no signature changes propagate. The `qArgValues` module-prefix dispatch branch at the top of `EvaluateFunctionCall` (Phase 43 qualified-call routing) is left untouched — different code path, Bundle F scope.

## Benchmark Results (Bundle A vs Baseline)

| Script | Baseline mean (s) | Bundle A mean (s) | Delta (s) | Delta (%) |
|--------|---:|---:|---:|---:|
| bench_collections.flow | 2.692 | 2.116 | -0.576 | -21.4% |
| bench_function_calls.flow | 1.832 | 1.552 | -0.280 | -15.3% |
| bench_notestream.flow | 1.350 | 1.328 | -0.022 | -1.6% |
| bench_overload.flow | 1.988 | 1.526 | -0.462 | -23.2% |
| bench_parse.flow | 1.620 | 1.528 | -0.092 | -5.7% |
| bench_var_lookup.flow | 4.680 | 3.410 | -1.270 | -27.1% |

Every script faster than baseline. The two most-relevant scripts to this bundle — `bench_function_calls.flow` (-15.3%) and `bench_overload.flow` (-23.2%) — show the largest expected wins. `bench_var_lookup.flow` (-27.1%) was unexpectedly the biggest beneficiary; the variable-lookup hot path apparently runs through `TryResolveFunction` as part of its fallback flow (variables-holding-lambdas branch in `EvaluateFunctionCall`), so Task 3's per-probe resolver-allocation elimination compounds there. `bench_notestream.flow` (-1.6%) flat within noise is expected — note-stream rendering is dominated by audio synthesis, not dispatch.

Baseline source: `bench/baseline.txt` (git rev fc60720, dotnet 10.0.107, 5 runs/script).
Bundle A source: `bench/results-bundle-a-20260524-194547.txt` (git rev 7c00f68, same harness).

## Test Suite Results

**Pre-Bundle-A baseline** (per STATE.md highlights from Phase 43 closure): 1779 passed / 36 failed / 1 skipped / 1816 total. All 36 failures pre-existing per `.planning/phases/42-type-system-stdlib-audit/deferred-items.md` (Phase 28 PerSynthArticulation FFT, Phase 29 ArticulationOnSample Piano, Phase 28 Ragtime RMS, Phase 35 FlowTestCli + MatchExhaustivenessDefault, Phase 38 OscLoopback flake).

**Post-Bundle-A**: 1785 passed / 33 failed / 1 skipped / 1819 total.

- Total grew by 3 (new tests added to the suite since the STATE.md snapshot).
- Passed grew by 6 (4 new + 2 previously-failing tests that have since been fixed in the interim).
- Failed dropped by 3 (subset of the baseline 36).
- **Zero new failures introduced by Bundle A.** The 33 failing tests are all in the pre-existing baseline set (Phase 28 PerSynthArticulation × 24, Phase 29 ArticulationOnSample × 6, Phase 35 MatchExhaustivenessDefault × 2, Phase 38 OscLoopback × 1).

## Determinism Gate

`examples/dsp/granular.flow` two-run cmp-clean: **PASS** (byte-identical). The Phase 36 PrngRegistry two-run determinism contract is preserved end-to-end through the new dispatch hot path.

## Composer-Script Smoke Gate (REQ-MOD-11)

- `examples/showcase.flow` — runs clean, emits `examples/output/flow_showcase.{wav,mid}`, zero `[module]` advisories.
- `examples/tutorial.flow` — runs clean, emits `examples/output/flow_tutorial.{wav,mid}` with full graduation-piece narrative.
- `examples/dsp/granular.flow` — runs clean, emits `/tmp/granular_demo.wav`.

## Commits

| Task | Commit | Files modified | Subject |
|---|---|---|---|
| 1 | 73e2cc5 | flow-lang/Runtime/StackFrame.cs | StackFrame.GetFunctionOverloads fast path — return local list directly |
| 2 | 1086fad | flow-lang/TypeSystem/OverloadResolver.cs, flow-lang/Runtime/ExecutionContext.cs | OverloadResolver FunctionOverload-direct overload + ResolveFunction caller update |
| 3 | 7538b19 | flow-lang/Runtime/ExecutionContext.cs | TryResolveFunction reuses _overloadResolver in silent mode |
| 4 | 7c00f68 | flow-lang/Interpreter/ExpressionEvaluator.cs | EvaluateFunctionCall single-pass argValues + argTypes build |

## Deviations from Plan

**1. [Rule 3 - Blocking issue] Added `using FlowLang.Runtime;` to OverloadResolver.cs**
- **Found during:** Task 2 build verification.
- **Issue:** `FunctionOverload` lives in `FlowLang.Runtime` namespace; the new `Resolve(IReadOnlyList<FunctionOverload>, ...)` overload referenced it without an import. Build error CS0246.
- **Fix:** Added `using FlowLang.Runtime;` to the file header alongside the existing `using FlowLang.Diagnostics;`.
- **Files modified:** flow-lang/TypeSystem/OverloadResolver.cs (1 line added)
- **Commit:** Included in 1086fad (Task 2).

**2. [Sequencing — Task 3 Part B landed in Task 2 commit]**
- **Found during:** Task 2 implementation when extracting `ResolveCore`.
- **Issue:** The plan's Task 3 Part B (lazy-allocate `localReporter` only on first rejection in the named-arg branch) lives inside the `ResolveCore` helper that Task 2 extracts. Splitting it across two commits would either (a) leave the helper inconsistent between commits or (b) require touching the same lines twice.
- **Fix:** Landed Part B in the same Task 2 commit. Task 3 then becomes a pure ExecutionContext.cs edit. Task 3's actual grep gates (live entirely in ExecutionContext.cs) all pass with this sequencing.
- **Files modified:** flow-lang/TypeSystem/OverloadResolver.cs (covered by 1086fad).
- **Commit:** 1086fad (Task 2 commit).

**3. [Cosmetic — comment reword in TryResolveFunction]**
- **Found during:** Task 3 grep-gate verification.
- **Issue:** First-draft inline comment in the new `TryResolveFunction` body said "No per-probe `new OverloadResolver(new ErrorReporter())` allocation". That literal substring caused the `new OverloadResolver` grep gate to return 2 (comment + constructor field init), not the expected 1.
- **Fix:** Reworded the comment to "No per-probe resolver-allocation". Behavior identical; grep gate clean.
- **Files modified:** flow-lang/Runtime/ExecutionContext.cs (covered by 7538b19).
- **Commit:** 7538b19 (Task 3 commit).

No other deviations. No FlowType[] → List<FlowType> Task 4 fallback was needed (arrays satisfy IReadOnlyList<T> in .NET as expected).

## Scope Boundary Adherence

- Edits confined to exactly the 4 named files: `flow-lang/Runtime/StackFrame.cs`, `flow-lang/Runtime/ExecutionContext.cs`, `flow-lang/TypeSystem/OverloadResolver.cs`, `flow-lang/Interpreter/ExpressionEvaluator.cs`.
- Zero NuGet additions, zero `.csproj` edits, zero CLAUDE.md edits.
- Zero behavior change. Zero determinism change. Zero FP arithmetic touched, zero PRNG ordering touched, zero audio render path touched.
- No caching keyed by `(name, arg-types)` introduced (reserved for Bundle F).

## Known Stubs

None.

## Threat Flags

None — Bundle A is a hot-path allocation-reduction pass that introduces zero new security-relevant surface. No new network endpoints, no auth paths, no file access patterns, no schema changes at trust boundaries.

## Self-Check: PASSED

- StackFrame.cs modified — confirmed `git log --oneline -5` shows 73e2cc5 as Task 1 commit.
- OverloadResolver.cs + ExecutionContext.cs modified — confirmed 1086fad as Task 2 commit.
- ExecutionContext.cs further modified — confirmed 7538b19 as Task 3 commit.
- ExpressionEvaluator.cs modified — confirmed 7c00f68 as Task 4 commit.
- `bench/results-bundle-a-20260524-194547.txt` exists — confirmed.
- All four commit hashes (73e2cc5, 1086fad, 7538b19, 7c00f68) present in `git log`.
- `dotnet build flow-lang/ -c Release` clean (0 Error, 8 pre-existing Warnings) after every task.
- Test suite shows 33 failed (≤ baseline 36, no new failures).
- 3 composer-script smoke runs clean.
- granular.flow two-run cmp-clean PASS.
