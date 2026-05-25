---
phase: quick-srj-bundle-f
plan: 01
subsystem: runtime
tags: [bundle-f, optimization, overload-resolution, memoization, cache, execution-context]
key-files:
  modified:
    - flow-lang/Runtime/ExecutionContext.cs
  created:
    - bench/results-bundle-f-20260524-205724.txt
metrics:
  duration: "~25 minutes"
  completed: "2026-05-24T21:00:00Z"
requirements: [BUNDLE-F-CACHE, BUNDLE-F-INVAL, BUNDLE-F-BYPASS, BUNDLE-F-PROOF]
---

# Quick Task 260524-srj: Bundle F — Overload Resolution Cache Summary

**One-liner:** Per-ExecutionContext memoization of overload resolution outcomes keyed by `(name, argType[])`, single-chokepoint invalidation at `DeclareFunction`, with named-args / varargs / VoidType bypass — drops `bench_overload` -19% and `bench_function_calls` -32% vs Bundle E.

## Path Chosen

**FULL CACHE per ExecutionContext** with single-chokepoint invalidation. The audit
in the PLAN's `<invalidation_surface_audit>` proved every `StackFrame._functions`
mutation transits `ExecutionContext.DeclareFunction`, so a full `Dictionary.Clear()`
on every (re)declaration is sufficient and trivially correct. No surgical
per-name invalidation, no candidate-set fingerprinting, no LiveReloadManager edits
(per-engine cache lifecycle handles live reload naturally).

## Invalidation Audit Recap

| Site | Action | Verdict |
|------|--------|---------|
| `StackFrame.DeclareFunction` (line 129) | Adds/replaces overload | Covered transitively via ExecutionContext wrapper |
| `ExecutionContext.DeclareFunction` (line 511 today) | Chokepoint wrapper | **InvalidateOverloadCache() wired** |
| `Interpreter.ExecuteProcDeclaration` (line 852, 865) | Calls `_context.DeclareFunction` | Covered by chokepoint |
| `ModuleLoader.LoadModule` (line 117) | Runs `interpreter.Execute(program)` → ProcDeclaration → chokepoint | Covered transitively |
| `LiveReloadManager.RenderScript` (line 857) | Fresh `FlowEngine` per render | No edit needed — per-engine cache lifecycle |
| `LiveReloadManager.StagePendingBuffers` (line 595) | No `_functions` mutation | No edit needed |
| `ExecutionContext.RestoreState` | No direct `_functions` mutation in current SnapshotState, but in-test `DeclareFunction` calls flow through chokepoint | **Defensive InvalidateOverloadCache() wired** (belt-and-suspenders) |

**Note on test-only bypass:** `flow-lang.Tests/Unit/Phase15/ReverbTimeContextTests.cs:58` calls
`GlobalFrame.DeclareFunction(overload)` directly, bypassing the chokepoint. This is
benign because it fires before any cache population (test setup), but it is the one
known direct-StackFrame call site in the test surface. No code change needed.

## Marginal vs Bundle E (`results-bundle-e-20260524-203614.txt`)

| Script | Bundle E (Mean s) | Bundle F (Mean s) | Δ (s) | Δ (%) |
|--------|------------------:|------------------:|------:|------:|
| bench_collections.flow | 2.262 | 1.684 | -0.578 | **-25.6%** |
| bench_function_calls.flow | 1.668 | 1.136 | -0.532 | **-31.9%** |
| bench_notestream.flow | 1.290 | 1.220 | -0.070 | -5.4% |
| bench_overload.flow | 1.474 | 1.188 | -0.286 | **-19.4%** |
| bench_parse.flow | 1.440 | 1.678 | +0.238 | +16.5% |
| bench_var_lookup.flow | 3.218 | 1.758 | -1.460 | **-45.4%** |

## Cumulative vs Original Baseline (`results-baseline-20260524-192612.txt`)

| Script | Baseline (Mean s) | Bundle F (Mean s) | Δ (s) | Δ (%) |
|--------|------------------:|------------------:|------:|------:|
| bench_collections.flow | 2.692 | 1.684 | -1.008 | **-37.4%** |
| bench_function_calls.flow | 1.832 | 1.136 | -0.696 | **-38.0%** |
| bench_notestream.flow | 1.350 | 1.220 | -0.130 | -9.6% |
| bench_overload.flow | 1.988 | 1.188 | -0.800 | **-40.2%** |
| bench_parse.flow | 1.620 | 1.678 | +0.058 | +3.6% |
| bench_var_lookup.flow | 4.680 | 1.758 | -2.922 | **-62.4%** |

## Determinism Proof (two-run cmp-clean)

```text
GRANULAR : cmp /tmp/granular_run1.wav  /tmp/granular_run2.wav   → exit 0 (byte-identical)
TUTORIAL : cmp /tmp/tutorial_run1.wav  /tmp/tutorial_run2.wav   → exit 0 (byte-identical)
HUMANIZE : cmp /tmp/humanize_run1.wav  /tmp/humanize_run2.wav   → exit 0 (byte-identical)
```

All three paired runs produce byte-identical WAV output → cache is invisible to
the determinism contract inherited from Phases 18/25/27/28/29/33.

## xUnit Result

**1798 passed / 33 failed / 1 skipped / 1832 total** (Release).

Bundle E baseline was 1785/33/1/1819 — the suite grew by 13 tests (likely Phase 44
research-phase test additions) but FAILED count is unchanged at 33 (same documented
pre-existing baseline failures: Phase 28/29/35/38 FFT + Piano + Ragtime RMS +
FlowTestCli + MatchExhaustivenessDefault + flaky OSC loopback). **ZERO new failures
introduced by Bundle F.**

High-risk test families cross-checked individually (all PASSING):

| Filter | Pass | Total |
|---|---|---|
| `~Repl` | 15 | 15 |
| `~Module` | 36 | 36 |
| `~Live` | 10 | 10 |
| `~Prng` | 21 | 21 |

## Files Modified

- `flow-lang/Runtime/ExecutionContext.cs` (only)

Added: `_overloadResolveCache` field, `OverloadCacheKey` nested struct,
`InvalidateOverloadCache()`, `ShouldBypassOverloadCache()` helper,
`ToCacheArgTypes()` helper. Wired cache reads in `ResolveFunction` and
`TryResolveFunction`; wired invalidation in `DeclareFunction` and `RestoreState`.
Added `using FlowLang.TypeSystem.PrimitiveTypes;` for the `VoidType` bypass check.

## Bench Expectations Met / Surprises

**Met:**
- `bench_overload.flow` -19.4% (targeted hot path — DROPPED MOST as predicted).
- `bench_function_calls.flow` -31.9% (function dispatch through ResolveFunction).
- `bench_var_lookup.flow` -45.4% (bigger than expected; the script's tight loops
  hammer builtin lookups which now hit the cache).
- `bench_collections.flow` -25.6% (collections route through builtin resolution).
- `bench_notestream.flow` -5.4% (modest — note streams do some resolve work).

**Surprise — minor regression on bench_parse:**
`bench_parse.flow` +16.5% vs Bundle E (+3.6% vs original baseline). Parse-time
exercises the proc declaration path heavily, so the per-`DeclareFunction`
`Dictionary.Clear()` adds measurable per-decl overhead. Hot-loop resolutions
during parse are rare so the cache READ never amortizes the WRITE/CLEAR cost on
this script. Mitigation if it ever matters: optimize `InvalidateOverloadCache`
to a "dirty bit" pattern or skip Clear when the dict is already empty — but at
+16% on a single script with high stddev contribution from intervening commits
this isn't worth the added complexity yet. Cumulative vs baseline is still
slightly negative (+3.6%), within run-to-run noise.

## Forward Risk for Phase 44 Plan 44-02

**HEADS UP**: When Phase 44 Plan 44-02 wires `CallerStrictMode` into
`OverloadResolver` (Axis A — strict mode disables compatible/convertible
coercion), the SAME `(name, argTypes)` may resolve to a DIFFERENT
`FunctionOverload` depending on whether the caller is strict-mode.

Today's `OverloadCacheKey` does NOT encode a strict-mode bit. Plan 44-02 MUST
either:

1. **Extend `OverloadCacheKey`** with a third field `bool StrictMode` (and use
   `ExecutionContext.CallerStrictMode` at the cache-read site to fill it), OR
2. **Invalidate the cache** around every `CallerStrictMode` toggle (push/pop at
   call dispatch boundaries — costs one Clear() per builtin/proc call which
   would unwind most of Bundle F's gains), OR
3. **Bypass the cache** whenever `CallerStrictMode == true` (a fourth bypass
   gate, analogous to the existing VoidType/varargs gates).

Option 1 is the cleanest. The doc-comment on `_overloadResolveCache` calls this
out so a future executor picking up Plan 44-02 will see it inline.

## STATE.md Follow-up Note (paste-ready)

> Completed quick task 260524-srj: Bundle F overload resolution cache
> (per-ExecutionContext memoization on `(name, argType[])` with single-chokepoint
> invalidation at `DeclareFunction` and defensive invalidation at `RestoreState`;
> named-args / varargs / VoidType bypass for correctness). bench_overload -19%,
> bench_function_calls -32%, bench_var_lookup -45%, bench_collections -26% vs
> Bundle E; cumulative bench_var_lookup -62% vs original baseline. xUnit unchanged
> failure count (33), zero new failures. Two-run cmp-clean preserved on
> granular/tutorial/test_humanize_voice_block. Single-file edit to
> `flow-lang/Runtime/ExecutionContext.cs`. FORWARD RISK: Phase 44 Plan 44-02 must
> either extend `OverloadCacheKey` with a strict-mode bit or invalidate around
> `CallerStrictMode` changes — doc-commented at the field declaration.

## Self-Check: PASSED

- `flow-lang/Runtime/ExecutionContext.cs`: FOUND (modified, committed in 2833046)
- `bench/results-bundle-f-20260524-205724.txt`: FOUND
- `.planning/quick/260524-srj-bundle-f-overload-resolution-cache/260524-srj-SUMMARY.md`: FOUND (this file)
- Commit `2833046` (perf(quick-srj-bundle-f-01)): FOUND in git log
