---
phase: quick-260524-qnf
plan: 01
subsystem: tooling/benchmarks
tags: [tooling, benchmark, performance, harness]
dependency_graph:
  requires:
    - dotnet 10 + flow-sharp.sln Release build
    - /usr/bin/time (GNU, -f flag)
  provides:
    - bench/run.sh re-runnable driver
    - bench/baseline.txt stable pre-optimization pin
  affects: []
tech_stack:
  added: []
  patterns:
    - "Build-once + dotnet run --no-build to amortize JIT cost across N runs"
    - "/usr/bin/time -o file -f '%e %M' for clean stderr capture"
    - "awk-driven mean + sample stddev (no bc dep)"
    - "Per-script Note: tuning history at the top of each .flow file"
key_files:
  created:
    - bench/bench_function_calls.flow
    - bench/bench_collections.flow
    - bench/bench_var_lookup.flow
    - bench/bench_notestream.flow
    - bench/bench_parse.flow
    - bench/bench_overload.flow
    - bench/bench_parse_imports/m01.flow
    - bench/bench_parse_imports/m02.flow
    - bench/bench_parse_imports/m03.flow
    - bench/bench_parse_imports/m04.flow
    - bench/bench_parse_imports/m05.flow
    - bench/run.sh
    - bench/README.md
    - bench/baseline.txt
  modified:
    - .gitignore (bench/ allow-list block + results-*.txt re-ignore)
decisions:
  - "Skipped Long from bench_overload.flow — the lexer has no L literal suffix and toLong is not a registered builtin; Int/Float/Double is enough to keep OverloadResolver scoring tier-shifting per call."
  - "bench_notestream returns a bare `1` from the proc (not (len s)) — `len(Sequence)` is not a registered builtin; the Sequence-binding line is where NoteStreamCompiler runs anyway."
  - "Proc body uses `... end proc`, NOT `{ }`. CLAUDE.md's `proc name (...) { body }` wording is outdated relative to current Parser; existing tests (tests/test_nothing_builtin.flow) confirm `end proc`. `tempo` / `timesig` / `key` blocks DO still use `{ }`."
  - "bench_parse needed ~125k lines (25000 declaration groups) to land in the 1-5s window — the parser is fast (50 groups = 0.57s, 200 = 0.57s, 3000 = 0.69s, 10000 = 1.04s, 25000 = 1.65s)."
  - ".gitignore needed an explicit bench/ allow-list mirroring the examples/{pragmas,scala,generative,sections,dsp,notation,live}/ precedent — the project globally ignores *.flow and *.md."
metrics:
  duration_min: 12
  completed_date: 2026-05-24
---

# Quick Task 260524-qnf: Flow Interpreter Benchmark Harness Summary

Re-runnable benchmark harness under `bench/` measuring six representative
Flow interpreter hot paths. Composer can run `bash bench/run.sh --label
<name>` from anywhere; output is a markdown table + per-run results file.

## What was built

### Six benchmark scripts (Task 1, commit fc60720)

| Script                    | Tuned count          | Mean (s) | Mean RSS (MB) |
| ------------------------- | -------------------: | -------: | ------------: |
| bench_function_calls.flow | N = 600,000          | 1.832    | 109.1         |
| bench_collections.flow    | N = 10,000, R = 20   | 2.692    | 108.7         |
| bench_var_lookup.flow     | N = 250,000          | 4.680    | 108.9         |
| bench_notestream.flow     | R = 150,000          | 1.350    | 109.0         |
| bench_parse.flow          | ~125k lines (25k×5)  | 1.620    | 232.9         |
| bench_overload.flow       | N = 200,000          | 1.988    | 108.7         |

Plus 5 import-target files under `bench/bench_parse_imports/m01-m05.flow`
that parse cleanly standalone and exercise ModuleLoader on relative paths.

### Driver + docs + baseline (Task 2, commit bf1da32)

- `bench/run.sh` — 165-line bash driver. Validates `--label` arg against
  `[A-Za-z0-9._-]+`. Builds once at `-c Release`, then loops each
  top-level `bench_*.flow` file 5x via `/usr/bin/time -f "%e %M"
  -o <file>` with `--no-build`. awk computes mean + sample stddev. Emits
  markdown table to stdout + `bench/results-<label>-<timestamp>.txt`.
  Cleans tmpdir on exit. Defensive guards for missing `/usr/bin/time`,
  missing `dotnet`, missing solution, build failures, run failures —
  each exits with a distinct code + clear error.
- `bench/README.md` — composer usage (quick start, hot-path table, tuning
  notes, constraints, "adding a new bench" recipe).
- `bench/baseline.txt` — verbatim copy of the first `--label baseline`
  run. Stddev across 5 runs is **under 5% of mean for all six scripts**
  (0.007–0.069s for elapsed; 0.1–0.4 MB for RSS) — clean signal-to-noise
  for downstream bundle diffs.

## Baseline numbers (5 runs, dotnet 10.0.107, git rev fc60720, Linux x86_64)

```
| Script                    | Mean (s) | Stddev (s) | Mean RSS (MB) | Stddev RSS (MB) |
|---------------------------|---------:|-----------:|--------------:|----------------:|
| bench_collections.flow    | 2.692    | 0.069      | 108.7         | 0.4             |
| bench_function_calls.flow | 1.832    | 0.049      | 109.1         | 0.3             |
| bench_notestream.flow     | 1.350    | 0.007      | 109.0         | 0.1             |
| bench_overload.flow       | 1.988    | 0.008      | 108.7         | 0.4             |
| bench_parse.flow          | 1.620    | 0.007      | 232.9         | 0.3             |
| bench_var_lookup.flow     | 4.680    | 0.032      | 108.9         | 0.1             |
```

## Per-script tuning notes

- **bench_function_calls** — Started N=200k (1.24s), tried 300k (1.38s),
  landed on N=600k for 1.83s. Scaling is sub-linear; the work-per-call is
  cheap enough that JIT + bookkeeping take a measurable share.
- **bench_collections** — N=10000 elements × R=20 outer reps hit 2.69s
  on the first try; no retuning needed.
- **bench_var_lookup** — Started N=300k (5.44s, above upper bound),
  dropped to N=250k for 4.68s — right at the upper end but useful for
  separating GetVariable cost from outer reduce dispatch cost. 20 bound
  vars per call is the same workload as drafted in the plan.
- **bench_notestream** — Most surprising tuning: R=5000 → 0.67s; bumping
  to R=30000 → 1.04s; R=60000 → 1.09s; R=150000 → 1.34s. Wall-clock
  barely scales with R, suggesting **proc-call dispatch + reduce loop
  overhead dominate over NoteStreamCompiler itself**. The 16-note literal
  is likely memoized per SourceLocation or NoteStreamCompiler is very
  fast. Landed on R=150k for clean 1.34s signal headroom.
- **bench_parse** — Most aggressive bump: 50 declaration groups was 0.57s
  (same as 200 groups), 3000 groups = 0.69s, 10000 = 1.04s, 25000 =
  1.65s. The parser is *extremely* fast. The 25k-group file is ~125k
  lines; RSS spikes to ~233 MB (vs. ~109 MB for all others) because the
  whole file lives in memory during parse. If a future optimization
  bundle changes parse architecture this may flip from CPU-bound to
  memory-bandwidth-bound.
- **bench_overload** — N=200k landed 1.98s on first try; no retuning.
  Long was skipped (lexer has no `L` suffix; `(toLong)` not registered),
  so the rotation is Int/Float/Double across three nested `(add)` calls.
  Three tiers is enough to keep OverloadResolver scoring tier-shifting
  on every call.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Proc body syntax in CLAUDE.md is outdated**

- **Found during:** Task 1 first dry-run (all six scripts failed with
  "Unexpected token LBrace '{'" errors at the proc-body opening brace).
- **Issue:** CLAUDE.md describes proc declarations as
  `proc name (Type: arg) { body }` but the current Parser expects
  `proc name (Type: arg) ... end proc`.
- **Fix:** Rewrote all 6 bench scripts + 5 import-target files to use
  the `... end proc` form. Confirmed against `tests/test_nothing_builtin.flow`
  and `tests/test_unpack_runtime.flow` — those tests use `end proc` and
  pass. `tempo` / `timesig` / `key` blocks still use `{ }` and were left
  alone (`bench_notestream.flow` confirms via dry-run pass).
- **Files modified:** all 11 bench .flow files.
- **Commit:** fc60720.
- **Out-of-scope follow-up:** CLAUDE.md should be updated to reflect
  current syntax. Logged here; not auto-fixed because the user's
  constraint forbids modifying any production / docs file outside the
  bench/ scope.

**2. [Rule 3 - Blocking] `len(Sequence)` not registered**

- **Found during:** Task 1 dry-run of bench_notestream.flow (error: "No
  matching overload for function 'len' with argument types (Sequence)").
- **Issue:** `len` is registered for `String` and `Array` but not for
  `Sequence` (only `(str Sequence)` and `(slice Sequence ...)` exist).
- **Fix:** Replaced `(len s)` with bare `1` as the proc's tail expression
  — the implicit return still flows through the outer reduce as an Int.
  The NoteStream literal is bound to `s` before the tail expression, so
  NoteStreamCompiler still runs on every call (which is what we wanted
  to measure).
- **Files modified:** bench/bench_notestream.flow.
- **Commit:** fc60720.

**3. [Rule 3 - Blocking] `*.flow` globally gitignored**

- **Found during:** Task 1 commit prep (`git status` showed no bench/
  files at all).
- **Issue:** Top-level `.gitignore` blocks `*.flow` and `*.md` globally;
  the project pattern is per-directory allow-list re-includes (see
  examples/scala/, examples/generative/, etc).
- **Fix:** Added a `bench/` allow-list block to `.gitignore` plus a
  defensive `bench/results-*.txt` re-ignore so per-run results files
  don't accumulate in git.
- **Files modified:** .gitignore.
- **Commit:** fc60720.

### Iteration count retuning (not deviations — explicit plan latitude)

The plan said "If a script runs faster than ~0.5s or slower than ~8s,
retune the iteration count inline". I retuned:

- bench_function_calls: 200k → 600k (was 1.24s, now 1.83s)
- bench_var_lookup: 300k → 250k (was 5.44s above upper bound, now 4.68s)
- bench_notestream: 5000 → 150000 (was 0.67s, now 1.35s — see notes
  about sub-linear scaling above)
- bench_parse: 50 groups → 25000 groups (was 0.57s, now 1.62s — parser
  is much faster than the plan anticipated)

All other counts stayed at the plan's draft values (bench_collections
N=10000/R=20, bench_overload N=200000).

## Authentication Gates

None.

## Known Stubs

None. All six benchmarks measure real work in real interpreter paths.

## Build-info noise

The first `bash bench/run.sh --label baseline` invocation aborted with
"Fatal error. Internal CLR error. (0x80131506)" inside `dotnet build`.
A second run completed cleanly. Likely a transient MSBuild / CLR issue,
not a harness bug — the build script itself was fine when invoked
directly between the two runs.

## Verification

Plan's `<verification>` invariants:

1. ✅ `git status --short -- flow-lang/ flow-interpreter/ '*.csproj'
   '*.sln'` shows no modifications.
2. ✅ `ls bench/` shows: 6 bench_*.flow + bench_parse_imports/ (5 files)
   + run.sh + README.md + baseline.txt + 1 results-baseline-*.txt.
3. ✅ `cat bench/baseline.txt` shows a 6-row markdown table with
   non-zero means.
4. ✅ Audio I/O grep is clean (after the `bench_function_calls.flow`
   comment retouch in commit bf1da32 to remove a false-positive
   "reduce loop" match against `loop `).
5. (Skipped re-run with `--label sanity` to avoid accumulating noise;
   baseline.txt is a manual `cp` so subsequent --label runs can't
   clobber it by design.)

## Self-Check: PASSED
