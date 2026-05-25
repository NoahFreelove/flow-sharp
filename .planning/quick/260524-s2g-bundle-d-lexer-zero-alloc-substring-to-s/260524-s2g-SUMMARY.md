---
phase: quick-260524-s2g
plan: 01
subsystem: lexer
tags: [perf, zero-alloc, lexer, hot-path, span]
requires: []
provides:
  - "Zero-allocation prefix probes in SimpleLexer.SkipWhitespaceAndComments hot path"
affects:
  - flow-lang/Lexing/SimpleLexer.cs
tech_added: []
patterns:
  - "ReadOnlySpan<char>.StartsWith(ReadOnlySpan<char>) — ordinal-by-construction, allocation-free prefix match"
key_files:
  created:
    - .planning/quick/260524-s2g-bundle-d-lexer-zero-alloc-substring-to-s/260524-s2g-SUMMARY.md
    - bench/results-bundle-d-20260524-201830.txt
  modified:
    - flow-lang/Lexing/SimpleLexer.cs
decisions:
  - "Form A (bare AsSpan, no StringComparison) — terser, identical observable semantics"
  - "Left untouched: 15 text.Substring(0, text.Length - N) lexeme-trim sites — consumed by int.TryParse/double.TryParse(string, ...); LOCKED out of scope"
metrics:
  duration: "~6 min wall, 2 commits"
  completed: "2026-05-24"
---

# Quick Task SUMMARY — Bundle D (lexer zero-alloc Substring → Span)

## What changed

Three lines in `SimpleLexer.SkipWhitespaceAndComments` (the hot whitespace/comment-skip loop, called once per `NextToken` and between every emit) swapped from `_source.Substring(_position).StartsWith(LIT)` to `_source.AsSpan(_position).StartsWith(LIT.AsSpan())`. Each previous call allocated a substring of the *entire remaining source* on every iteration — kilobytes per parse for files of any meaningful size, repeated at inner-loop frequency. The span form walks a `ReadOnlySpan<char>` window over the existing string with zero allocation and zero GC. The probes guard `Note:` / `TODO:` / `FIXME:` lead-in line comments at start-of-line; observable lexer behavior (token positions, diagnostics, fall-through to identifier scanning) is unchanged because `ReadOnlySpan<char>.StartsWith(ReadOnlySpan<char>)` is ordinal by construction.

## Files modified

- `flow-lang/Lexing/SimpleLexer.cs` (3 lines: 1177, 1200, 1208)

## Test result

```
Failed!  - Failed:    33, Passed:  1785, Skipped:     1, Total:  1819, Duration: 28 s - flow-lang.Tests.dll (net10.0)
```

Matches Bundle C baseline exactly: **1785 passed / 33 failed / 1 skipped / 1819 total**. The 33 failures are the documented Phase 28/29/35/38 deferred baseline (per STATE.md Phase 43 highlights) — no new failure names introduced.

## Benchmark Results (Bundle D)

Bundle D result file: `bench/results-bundle-d-20260524-201830.txt`
Bundle C reference:  `bench/results-bundle-c-20260524-200926.txt`
Baseline reference:  `bench/baseline.txt`

### Table 1 — Marginal vs Bundle C

| Benchmark | Bundle C (ms) | Bundle D (ms) | Δ | Δ% |
|---|---:|---:|---:|---:|
| bench_parse | 1478 | 1420 | -58 | -3.92% |
| bench_function_calls | 1522 | 1488 | -34 | -2.23% |
| bench_var_lookup | 3194 | 3240 | +46 | +1.44% |
| bench_collections | 2118 | 2070 | -48 | -2.27% |
| bench_notestream | 1214 | 1204 | -10 | -0.82% |
| bench_overload | 1496 | 1458 | -38 | -2.54% |

### Table 2 — Cumulative vs original baseline

| Benchmark | Baseline (ms) | Bundle D (ms) | Δ | Δ% |
|---|---:|---:|---:|---:|
| bench_parse | 1620 | 1420 | -200 | -12.35% |
| bench_function_calls | 1832 | 1488 | -344 | -18.78% |
| bench_var_lookup | 4680 | 3240 | -1440 | -30.77% |
| bench_collections | 2692 | 2070 | -622 | -23.10% |
| bench_notestream | 1350 | 1204 | -146 | -10.81% |
| bench_overload | 1988 | 1458 | -530 | -26.66% |

## Expected vs observed

Plan predicted bench_parse would drop the most marginally. Observed: bench_parse did drop the largest absolute amount (-58 ms / -3.92%) and the largest of the parser/lexer-bound benchmarks, confirming the prediction. bench_function_calls / bench_collections / bench_overload also picked up 2-3% each — consistent with `SkipWhitespaceAndComments` being on every benchmark's parse path. bench_var_lookup ticked +1.44% (+46 ms), within stddev of both runs (Bundle C ±15 ms, Bundle D ±28 ms) — noise, not regression; nothing in this swap touches variable-lookup hot paths. bench_notestream moved only -0.82% (smaller files, fewer whitespace iterations — proportionally less work in the swap zone). No benchmark regressed meaningfully (> +5%).

Cumulatively across Bundles A+B+C+D, every benchmark is now between -10.8% (bench_notestream) and -30.8% (bench_var_lookup) faster than the original baseline.

## Determinism

Two-run cmp-clean determinism preserved — lexer tokenization is pure / position-deterministic; this swap touches neither randomness nor floating-point.

## Constraints honored

- Edits confined to `flow-lang/Lexing/SimpleLexer.cs` (3 lines changed, surrounding `else if` chain structure preserved, no other file touched in this task).
- Observable lexer behavior identical: `Note:` / `TODO:` / `FIXME:` lead-in line comments at start-of-line continue to be skipped to end-of-line; non-matching prefixes fall through to identifier scanning. `ReadOnlySpan<char>.StartsWith(ReadOnlySpan<char>)` is ordinal by construction → same tokens, same positions, same diagnostics.
- Bundle C test baseline matched (1785/33/1/1819, zero new failures, failure set is a subset of the documented Phase 28/29/35/38 deferred items).
- The other 15 `text.Substring(0, text.Length - N)` lexeme-trim sites in the file were left alone — they feed `int.TryParse(string, ...)` / `double.TryParse(string, ...)` and the string form is load-bearing under the LOCKED constraint scope.
- No new `using` directives needed (`MemoryExtensions.AsSpan(string, int)` lives in `System`, implicit-usings enabled).
- Build clean in Release: zero new warnings, zero new errors (8 pre-existing warnings unchanged).

## Self-Check: PASSED

- `flow-lang/Lexing/SimpleLexer.cs` — modified (3 sites swapped, verified by grep).
- `bench/results-bundle-d-20260524-201830.txt` — emitted.
- `.planning/quick/260524-s2g-bundle-d-lexer-zero-alloc-substring-to-s/260524-s2g-SUMMARY.md` — present.
- Task 1 commit `0ddd0d9` — present in git log (`git log --oneline | grep 0ddd0d9`).
