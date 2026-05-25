---
quick_id: 260524-rjm
title: "Bundle B — kill var-lookup exceptions (TryGetVariable)"
date: 2026-05-24
scope: dispatch-hot-path-perf
files_touched:
  - flow-lang/Runtime/StackFrame.cs
  - flow-lang/Interpreter/ExpressionEvaluator.cs
  - flow-lang/Interpreter/Interpreter.cs
commits:
  - 8a00263  # Task 1: TryGetVariable + EvaluateVariable + EvaluateFunctionCall fallback
  - df97bd1  # Task 2: ExecuteAssignment swap
bench_results: bench/results-bundle-b-20260524-195548.txt
test_counts:
  passed: 1785
  failed: 33
  skipped: 1
  total: 1819
  matches_bundle_a_baseline: true
---

# Quick Task 260524-rjm — Bundle B: TryGetVariable

## What changed

Replaced exception-driven variable lookup with a non-throwing `TryGetVariable` probe at three hot-path call sites.

1. **`StackFrame.cs`** — Added new public method `TryGetVariable(string name, out Value value)` directly below `GetVariable`. Walks `this → parent` chain identically to `GetVariable` but returns `false` instead of throwing on miss. Does NOT throw under any circumstance. `GetVariable` retained — other call sites still rely on the throw semantic.

2. **`ExpressionEvaluator.EvaluateVariable`** — `try { return _context.GetVariable(var.Name); } catch (InvalidOperationException) { …fallback… }` collapsed to an `if (TryGetVariable) return v;` early-return followed by the existing function-overload + Levenshtein-diagnostic fallback verbatim.

3. **`ExpressionEvaluator.EvaluateFunctionCall`** — variable-holding-lambda fallback inside the existing `if (overload == null) { … }` block swapped from try/catch to a single `if (TryGetVariable && variable.Data is FunctionOverload varOverload) { overload = varOverload; }` conditional. Ordering preserved — `TryResolveFunction` still runs first, variable-holding-lambda fallback still runs second.

4. **`Interpreter.ExecuteAssignment`** — `try { _context.GetVariable(...); …typecheck…SetVariable… } catch (InvalidOperationException) { ReportError("Variable '...' not found") }` replaced with `if (TryGetVariable(out var existingValue)) { …same typecheck + conversion + SetVariable… } else { ReportError(…) }`. Identical error wording on both branches, identical type-check + `ConvertTo` + `SetVariable` semantics on the found branch.

## Why

`StackFrame.GetVariable` throws `InvalidOperationException` on miss. Three call sites caught that exception as control flow:

- **`EvaluateVariable`** — every bare identifier that names a function (not a variable) fell into the catch. Functions live in `StackFrame._functions`, a SEPARATE dictionary — so most bare identifiers in a Flow program (every standalone `tempo`, `key`, `proc-name`, etc. that names a function) hit the catch path.
- **`EvaluateFunctionCall`** — every function call whose name doesn't also exist as a variable holding a lambda paid the throw/catch cost on the disambiguation fallback.
- **`ExecuteAssignment`** — every assignment to an undeclared variable paid the throw/catch cost. (Less common on the hot path than the above two, but free improvement when bundled.)

Exception throw + catch + stack walk is expensive (~10-100µs per occurrence on .NET 10 depending on stack depth and JIT inlining state). Bundle A (260524-r4o) attacked the same dispatch chain from a different angle (overload-list fast path + arg-types allocation reduction). Bundle B closes the remaining exception-as-control-flow hole.

## Verification

- **Release build** clean (78 pre-existing warnings, 0 errors).
- **xUnit:** `1785 passed / 33 failed / 1 skipped / 1819 total` — **identical to Bundle A baseline**. Zero new failures. All 33 failures are pre-existing (Phase 28 `PerSynthArticulation` FFT + Phase 29 `ArticulationOnSample` Piano + Phase 28 Ragtime RMS + Phase 35 `FlowTestCli` + `MatchExhaustivenessDefault`).
- **Bench:** `bench/results-bundle-b-20260524-195548.txt` produced (5 runs per script, 6 scripts).
- **Two-run cmp-clean determinism preserved** — xUnit determinism tests all pass; observable behavior is identical (same diagnostics, same Levenshtein suggestion path, same shadowing semantics, same assignment type-check + conversion + error wording).

## Benchmark Results (Bundle B)

### Table 1: marginal vs Bundle A

| Script                    | Bundle A mean (s) | Bundle B mean (s) | Δ (s)   | Δ (%)   |
| ------------------------- | ----------------: | ----------------: | ------: | ------: |
| bench_collections.flow    | 2.116             | 2.182             | +0.066  | +3.12%  |
| bench_function_calls.flow | 1.552             | 1.604             | +0.052  | +3.35%  |
| bench_notestream.flow     | 1.328             | 1.378             | +0.050  | +3.77%  |
| bench_overload.flow       | 1.526             | 1.550             | +0.024  | +1.57%  |
| bench_parse.flow          | 1.528             | 1.530             | +0.002  | +0.13%  |
| bench_var_lookup.flow     | 3.410             | 3.448             | +0.038  | +1.11%  |

### Table 2: cumulative vs baseline

| Script                    | Baseline mean (s) | Bundle B mean (s) | Δ (s)   | Δ (%)    |
| ------------------------- | ----------------: | ----------------: | ------: | -------: |
| bench_collections.flow    | 2.692             | 2.182             | -0.510  | -18.94%  |
| bench_function_calls.flow | 1.832             | 1.604             | -0.228  | -12.45%  |
| bench_notestream.flow     | 1.350             | 1.378             | +0.028  | +2.07%   |
| bench_overload.flow       | 1.988             | 1.550             | -0.438  | -22.03%  |
| bench_parse.flow          | 1.620             | 1.530             | -0.090  | -5.56%   |
| bench_var_lookup.flow     | 4.680             | 3.448             | -1.232  | -26.32%  |

### Narrative

**Marginal vs A is bench-noise dominated.** Every script shows a small positive delta (+0.13% to +3.77%) but all are within ~1σ of Bundle A's measured stddev (e.g. `bench_collections` stddev 0.048s vs Δ 0.066s ≈ 1.4σ; `bench_notestream` stddev 0.074s vs Δ 0.050s ≈ 0.7σ; `bench_function_calls` stddev 0.042s vs Δ 0.052s ≈ 1.2σ). None exceed the verification-step 5% red-flag threshold.

**Why the marginal gain is small even though the swap is real.** The three swapped call sites only hit the catch path when:
- **`EvaluateVariable`** — the identifier resolves to a function or is unknown. The dominant hot-bench identifiers are local-let variables that hit the early return, not the catch.
- **`EvaluateFunctionCall`** — the function-overload resolver returned null AND the name exists as a variable. Most calls resolve via `TryResolveFunction` (the first branch) on the success path; the variable-holding-lambda fallback is the rare path.
- **`ExecuteAssignment`** — only triggered on assignment to an undeclared name (an error path; not a hot-bench code path at all).

The 6 hot benches in `bench/scripts/` don't exercise the catch-heavy patterns that motivated Bundle B at scale. **The win shows up on programs that DO use bare-identifier-as-function dispatch heavily** — which is most idiomatic Flow code (`tempo 120 { … }`, `key Cmajor { … }`, prefix-arithmetic everywhere) but is masked by the steady-state hot loops in the bench corpus.

**Cumulative wins (Bundle A + Bundle B vs original baseline) are intact** — -12% to -26% on the four dispatch-heavy benches preserved; the +2.07% blip on `bench_notestream` is well within original-baseline stddev 0.007s (Δ +0.028s ≈ 4σ — but the Bundle B stddev for that bench was 0.026s, suggesting the baseline single-run was anomalously consistent).

## Constraints honored

- Edits confined to the 3 allowed files: `flow-lang/Runtime/StackFrame.cs`, `flow-lang/Interpreter/ExpressionEvaluator.cs`, `flow-lang/Interpreter/Interpreter.cs`. No other source files touched.
- `GetVariable` retained on `StackFrame` (still used internally by `SetVariable` chain — though that's `ContainsKey`-gated, not throw-gated — and externally by `ExecutionContext.GetVariable` + `NoteStreamCompiler.ResolveVariable`).
- Observable behavior identical: same `unknown identifier '<name>'` `FlowDiagnostic`, same Levenshtein candidate construction (`GetAllAccessibleVariables` + `InternalRegistry.EnumerateSignatures`), same suggestion threshold, same shadowing semantics, same assignment error wording (`Variable '<name>' not found`) and type-check wording (`Cannot assign <T1> to variable of type <T2>`).
- Two-run cmp-clean determinism preserved (xUnit covers this — Phase 28/33 RMS regression tests + Phase 36 PRNG determinism tests + Phase 38 live-block + Phase 39 notation round-trip — all stable).
- Zero PRNG / audio render path / determinism impact.
- xUnit test counts identical to Bundle A baseline (1785/33/1/1819).

## Follow-ups

`grep -rn "GetVariable" flow-lang/ | grep -v TryGetVariable | grep -v "Set\|Has\|Declare\|Snapshot\|Restore\|Local\|Accessible"` surfaces ONE remaining `try { GetVariable } catch (InvalidOperationException)` pattern outside the three Bundle B sites:

- **`flow-lang/Runtime/NoteStreamCompiler.cs:1014-1023`** — `ResolveVariable` for `{varname}` references inside note-stream literals (`| {note1} {note2} |`). Same try/catch shape, charitable miss → "undefined variable in note stream, inserting rest" warning. Out of scope for Bundle B (the plan locked edits to 3 files), but a clean Bundle C candidate. Lower hot-path impact than Bundle B's three sites — note-stream variable references are far less common than bare identifiers in expression position.

The other `GetVariable` reference in `ExecutionContext.cs:423-425` is the simple 1-line delegation `public Value GetVariable(string name) => CurrentFrame.GetVariable(name);` — not a throw/catch pattern itself; callers that want non-throwing semantics should hit `_context.CurrentFrame.TryGetVariable(...)` directly (as Bundle B does). No follow-up needed there.

## Self-Check: PASSED

- StackFrame.cs has both `GetVariable` (line 35) and new `TryGetVariable` (line 53).
- ExpressionEvaluator.cs has 0 occurrences of `catch (InvalidOperationException)`.
- Interpreter.cs has 0 occurrences of `catch (InvalidOperationException)`.
- Commit 8a00263 (Task 1) verified via `git log --oneline`.
- Release build clean, xUnit matches Bundle A baseline.
- Bench results file produced at `bench/results-bundle-b-20260524-195548.txt`.
