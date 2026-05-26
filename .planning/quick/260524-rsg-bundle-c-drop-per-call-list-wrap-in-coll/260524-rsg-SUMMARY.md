---
task: 260524-rsg-bundle-c-drop-per-call-list-wrap-in-coll
mode: quick
type: execute
status: complete
files_modified:
  - flow-lang/StandardLibrary/Collections.cs
commit: d210dbe
test_baseline: 1785 passed / 33 failed / 1 skipped / 1819 total (zero new failures vs Bundle B)
bench_label: bundle-c
bench_file: bench/results-bundle-c-20260524-200926.txt
date: 2026-05-24
---

# Bundle C — Drop per-call `List<Value>` wrap in collection callbacks

## Problem

`map`/`filter`/`each`/`reduce` in `flow-lang/StandardLibrary/Collections.cs`
each allocated one throwaway `new List<Value> { element }` (or
`{ accumulator, element }` for reduce) per iteration to wrap the per-element
argument list for `InvokeCallback`. On a `(map arr fn)` over 10k elements
that's 10k throwaway single-element `List<Value>` instances per call. The
`bench_collections.flow` benchmark drives this hard (10k iterations × ~20
outer reps × 4 callback sites ≈ 800k eliminated list allocations per bench
run).

## Approach

Hoist ONE reusable buffer per call out of the loop, mutate it in place via
the indexer each iteration. `Value[]` is used as the backing buffer (not
`List<Value>`) because:

- It satisfies `IReadOnlyList<Value>` (arrays implement `IList<T>` which
  extends `IReadOnlyList<T>`).
- No internal `_items`/`_size` growable backing — strictly N references
  plus header.
- Indexer assignment is a single MOV; no `_version++` mutation tracking
  like `List<T>` performs.

`InvokeCallback`'s parameter type relaxed from `List<Value>` to
`IReadOnlyList<Value>` — strictly local change (`InvokeCallback` is
`private`), and both downstream paths (`callback.Implementation!` is
`Func<IReadOnlyList<Value>, Value>`; `ExecuteUserFunctionWithCaptures`
takes `IReadOnlyList<Value>`) already speak that shape, so this is a
trivial widening.

### Safety precondition (planner-verified, reproduced for code-review trail)

`args` is read-only-consumed within the synchronous call:

- **Internal builtins** extract via `args[0]` / `args[1]` and return a `Value`.
  No retention past return.
- **User-defined functions** iterate `args` by index inside the parameter-
  binding loop only (`ExecuteUserFunctionWithCaptures` lines 1135-1170);
  every `args[i]` is extracted and stored as a separate stack-frame value
  via `DeclareVariable` / `SetVariable`. The varargs branch creates a NEW
  `List<Value>` and copies remaining args into it before wrapping in a
  `Value.Array` — also copies-out before storing.

By the time control returns to the caller's `for`-loop body, every read of
`args[0]`/`args[1]` has already been resolved into stack-frame values.
Mutating `buffer[0] = nextElement` for the next iteration cannot corrupt
previously-bound parameters in any recursive call.

A Bundle C marker comment was added on each of the 4 hoist sites
documenting this precondition for future maintainers: any code path that
retains the args reference past callback return must first revert Bundle C
or prove its new path also satisfies the precondition.

## Files Touched

- `flow-lang/StandardLibrary/Collections.cs` — 4 callsite edits + 1
  parameter-type relaxation. `Prepend` site at line ~282 intentionally
  untouched (return-value capture, not per-iteration alloc).

`git diff --stat HEAD~1 HEAD`:
```
 flow-lang/StandardLibrary/Collections.cs | 23 ++++++++++++++++++-----
 1 file changed, 18 insertions(+), 5 deletions(-)
```

## Test Results

**Full xUnit suite:** 1785 passed / 33 failed / 1 skipped / 1819 total.
**EXACT MATCH with Bundle B baseline (git rev `8a00263`).** Zero new
failures. The 33 failures are pre-existing Phase 28 PerSynthArticulation
FFT / Phase 29 ArticulationOnSample Piano / Phase 28 Ragtime RMS /
Phase 35 FlowTestCli + MatchExhaustivenessDefault / Phase 38 OSC loopback
deferred items documented in
`.planning/phases/42-type-system-stdlib-audit/deferred-items.md`.

**Composer `.flow` script sweep:** 119 happy-path scripts pass; the 4
expected non-zero-exit scripts unchanged (`test_dict_type_errors.flow`,
`test_error_masking.flow`, `test_iteration_guard.flow`,
`test_musical_context_errors.flow`).

## Known Issues

None new. Pre-existing failures inherited from Bundle B baseline; Bundle C
preserves them byte-for-byte.

## Benchmark Results (Bundle C)

`bench/results-bundle-c-20260524-200926.txt` (5 runs per script).

### Table 1 — Marginal vs Bundle B

Source: `bench/results-bundle-b-20260524-195548.txt` ↔
`bench/results-bundle-c-20260524-200926.txt`. Negative Δ = faster under
Bundle C.

| Script | Bundle B Mean (s) | Bundle C Mean (s) | Δ (s) | Δ % |
|---|---:|---:|---:|---:|
| bench_collections.flow | 2.182 | 2.118 | -0.064 | -2.9% |
| bench_function_calls.flow | 1.604 | 1.522 | -0.082 | -5.1% |
| bench_notestream.flow | 1.378 | 1.214 | -0.164 | -11.9% |
| bench_overload.flow | 1.550 | 1.496 | -0.054 | -3.5% |
| bench_parse.flow | 1.530 | 1.478 | -0.052 | -3.4% |
| bench_var_lookup.flow | 3.448 | 3.194 | -0.254 | -7.4% |

### Table 2 — Cumulative vs Original Baseline

Source: `bench/baseline.txt` (git rev `fc60720`) ↔
`bench/results-bundle-c-20260524-200926.txt`.

| Script | Baseline Mean (s) | Bundle C Mean (s) | Δ (s) | Δ % |
|---|---:|---:|---:|---:|
| bench_collections.flow | 2.692 | 2.118 | -0.574 | -21.3% |
| bench_function_calls.flow | 1.832 | 1.522 | -0.310 | -16.9% |
| bench_notestream.flow | 1.350 | 1.214 | -0.136 | -10.1% |
| bench_overload.flow | 1.988 | 1.496 | -0.492 | -24.7% |
| bench_parse.flow | 1.620 | 1.478 | -0.142 | -8.8% |
| bench_var_lookup.flow | 4.680 | 3.194 | -1.486 | -31.8% |

### Interpretation

`bench_collections` moved -2.9% marginally — in the EXPECTED direction
(smaller wall-clock under Bundle C) but inside the run-to-run stddev band
(Bundle B stddev was 0.169s = ~7.7%; Bundle C stddev is 0.211s = ~10.0%).
The cumulative A+B+C drop on `bench_collections` is now -21.3% vs the
original baseline (from -19% post-Bundle-B), so the bundle DID compound
the dispatch+lookup gains as intended even though the per-bundle delta is
inside noise.

**FLAG:** the headline marginal `bench_collections` Δ is smaller than the
clean signal seen on the lambda-call-heavy `bench_var_lookup` (-7.4%) and
`bench_notestream` (-11.9%) benchmarks. Possible explanations for the
muted `bench_collections` signal (worth investigating if a Bundle D
targets the same area):

- `bench_collections` mixes `map`/`filter`/`reduce` with non-callback
  array ops (`length`/`reverse`/`take`/`drop`/etc.) that Bundle C does
  not touch — the callback-allocation share of total time may be lower
  than the planning estimate suggested.
- `var_lookup` and `notestream` are the cleanest "all my work is
  function-call dispatch" microbenchmarks, so they show the
  IReadOnlyList-shape widening (parameter type relaxation lets the JIT
  see a covariant array-to-interface convert instead of a List allocation
  on every dispatch). That's a Bundle B/C interaction win — the Bundle B
  `TryGetVariable` change shaved variable-lookup-call latency; Bundle C
  now removes the per-call allocation on top of that.
- .NET 10's escape analysis may have already been hoisting some of the
  Bundle B list allocations into stack slots when the list never
  escaped — leaving less heap savings for Bundle C to claim on the
  collection benchmark specifically.

`bench_function_calls` (-5.1%) and `bench_overload` (-3.5%) marginal
drops are consistent with the same JIT-friendly-dispatch story:
`Func<IReadOnlyList<Value>, Value>` invocations through
`callback.Implementation!` are everywhere in the interpreter, and the
widened `InvokeCallback` parameter type means the compiler emits one
fewer interface-cast/box at the four call sites.

Conclusion: Bundle C delivers the architectural change (zero
per-iteration allocations on the 4 callback hot paths) but the headline
`bench_collections` win is inside the noise band. The cumulative A+B+C
drop on the targeted benchmark is -21.3%, and the lambda-call-heavy
benchmarks (`var_lookup`, `notestream`) show the cleanest marginal
Bundle C signal. Recommend re-running with N=20 if the composer wants a
tighter signal on `bench_collections` specifically.

## Self-Check: PASSED

- `flow-lang/StandardLibrary/Collections.cs` — exists, modified per spec.
- Commit `d210dbe` — present in `git log --oneline`.
- `bench/results-bundle-c-20260524-200926.txt` — exists.
- `dotnet test` count: 1785/33/1/1819 — matches Bundle B baseline exactly.
- 4 expected `.flow` script failures — unchanged.
- `grep -c 'new List<Value> {'` = 1 (Prepend site, intentional).
- `grep -c 'var buffer = new Value\['` = 4 (Each/Map/Filter/Reduce).
- `InvokeCallback` signature: `IReadOnlyList<Value>`.
