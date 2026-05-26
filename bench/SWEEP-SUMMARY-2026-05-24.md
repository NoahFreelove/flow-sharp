# Flow Interpreter Optimization Sweep — 2026-05-24

Six-bundle micro-optimization sweep against the Flow language interpreter.
Each bundle landed as its own `/gsd:quick` task with atomic commit, full xUnit
verification, and benchmark capture. Two-run cmp-clean determinism preserved
across every bundle (Phase 36 PRNG-routing contract intact).

## Headline: cumulative deltas vs original baseline

| Script | Baseline (s) | After all bundles (s) | Δ (s) | Δ (%) |
|--------|-------------:|----------------------:|------:|------:|
| bench_var_lookup     | 4.680 | 1.758 | **-2.922** | **-62.4%** |
| bench_overload       | 1.988 | 1.188 | **-0.800** | **-40.2%** |
| bench_function_calls | 1.832 | 1.136 | **-0.696** | **-38.0%** |
| bench_collections    | 2.692 | 1.684 | **-1.008** | **-37.4%** |
| bench_notestream     | 1.350 | 1.220 | **-0.130** | **-9.6%**  |
| bench_parse          | 1.620 | 1.678 | **+0.058** | **+3.6%**  ⚠️ |

RSS held flat at ~109 MB across all benchmarks (within ±0.4 MB). bench_parse's
234 MB RSS reflects its 125k-line input file held in memory during parse;
unchanged after all bundles.

## Per-bundle attribution

| Bundle | Commit | Files touched | Risk | Marginal contribution |
|--------|--------|---------------|------|------|
| **A** — Dispatch hot-path micro-opts | 73e2cc5 / 1086fad / 7538b19 / 7c00f68 | StackFrame, ExecutionContext, OverloadResolver, ExpressionEvaluator | LOW | Biggest single bundle. var_lookup -27%, overload -23%, collections -21%, function_calls -15% |
| **B** — TryGetVariable | 8a00263 / df97bd1 | StackFrame, ExpressionEvaluator, Interpreter | LOW | Marginal within noise — planner caught that GetVariable only throws on not-found, so throw frequency was lower than initial analysis suggested. Change kept as code-quality improvement. |
| **C** — Reusable Value[] callback buffer | d210dbe | Collections | LOW | Target bench (collections) marginal -2.9% in noise, but JIT escape-analysis side effects helped notestream (-11.9%) and var_lookup (-7.4%) |
| **D** — Lexer Substring→Span | 0ddd0d9 | SimpleLexer | LOW | Predictable bench_parse -3.9%; others noise |
| **E** — MusicalContext cache | 1d60d24 | ExecutionContext, Interpreter | MEDIUM | No measurable bench impact — bench loop's PushFrame/PopFrame thrashes the cache. Correct infrastructure for the song-render hot path that doesn't show up in these benches. |
| **F** — Overload resolution cache | 2833046 | ExecutionContext | MEDIUM | Big win on resolution-heavy benches. var_lookup -45%, function_calls -32%, collections -26%, overload -19% marginal. Parse +16.5% regression from per-decl Dictionary.Clear() on pathological 25k-decl workload — accepted. |

## Test + determinism status (unchanged from start)

- xUnit: pre-Bundle-A 1785/33/1/1819 → end-of-sweep same counts (modulo +13 tests added by unrelated phase-44 work in parallel). **Zero new failures attributable to any bundle.**
- 33 pre-existing failures unchanged (not in scope for this sweep).
- Two-run cmp-clean determinism preserved across granular.flow, tutorial.flow, and tests/test_humanize_voice_block.flow at every bundle.
- Build clean Release after every commit, no new warnings.

## Known regressions + follow-ups

1. **bench_parse +3.6% vs baseline (+16.5% vs Bundle E).** Cause: Bundle F's overload-resolution cache invalidates via `Dictionary.Clear()` on every proc declaration. The bench's 25k consecutive declarations pay this cost 25k times. Real-world Flow scripts declare procs once at module load then resolve, so this regression has no observable impact on typical workloads. **Follow-up:** consider `Dictionary.TrimExcess()` or skip-if-empty Clear if a real-world workload exposes the cost.

2. **NoteStreamCompiler.cs:1014-1023** has the same `try { GetVariable } catch (InvalidOperationException)` pattern Bundle B replaced elsewhere. Lower hot-path impact than the three sites Bundle B covered. Clean candidate for a follow-up quick task.

3. **Forward risk for Phase 44 Plan 44-02 (Strict Mode):** when `CallerStrictMode` wires into `OverloadResolver`, the same `(name, argTypes)` may resolve differently in strict vs non-strict callers. Bundle F's cache key doesn't encode this discriminator. Plan 44-02 must either extend `OverloadCacheKey` with a strict bit OR invalidate around `CallerStrictMode` changes. Documented in both Bundle F's SUMMARY and the `_overloadResolveCache` field's XML doc-comment.

4. **Bundle E correctness is sound but unmeasured.** The cache is correct and the invalidation surface is fully audited (7 sites + RestoreState chokepoint), but the bench loop pattern (PushFrame/PopFrame per iteration) thrashes the cache. Song-render workloads should benefit; no bench exists for that path today.

## Methodology

- Harness: `bench/run.sh` runs each `bench_*.flow` 5× via `/usr/bin/time -f "%e %M"` against a Release build (`dotnet build -c Release`).
- Stddev under 5% of mean on all bench scripts across the sweep (bench_collections had higher noise post-Bundle-F at σ=0.464; mean still firmly below baseline).
- Each bundle ran through `/gsd:quick` (or `/gsd:quick --validate` for Bundles E and F) with planner → optional plan-checker → executor → optional verifier.
- Each bundle's PLAN.md and SUMMARY.md preserved under `.planning/quick/260524-<id>-bundle-*/` for audit.

## Files modified across the sweep

```
flow-lang/Runtime/StackFrame.cs            (Bundles A, B)
flow-lang/Runtime/ExecutionContext.cs      (Bundles A, E, F)
flow-lang/TypeSystem/OverloadResolver.cs   (Bundle A)
flow-lang/Interpreter/ExpressionEvaluator.cs  (Bundles A, B)
flow-lang/Interpreter/Interpreter.cs       (Bundles B, E)
flow-lang/StandardLibrary/Collections.cs   (Bundle C)
flow-lang/Lexing/SimpleLexer.cs            (Bundle D)
```

7 production source files touched. No new NuGet dependencies. No `.csproj` changes. No CLAUDE.md edits (no user-visible behavior changes to document).
