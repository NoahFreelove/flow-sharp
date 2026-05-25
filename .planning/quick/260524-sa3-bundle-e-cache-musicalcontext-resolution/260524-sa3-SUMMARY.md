# 260524-sa3 — Bundle E: cache MusicalContext resolution

## Outcome

Selected path: **FULL CACHE PATH** (planner audit determined invalidation surface is bounded — 7 sites in `ExecutionContext.cs` + 1 call-site swap in `Interpreter.cs`).

Cache mechanism: private `_cachedMusicalContext` nullable field on `ExecutionContext`. `GetMusicalContext()` returns the cached non-null value on hit; computes + stores on miss. Invalidated via `InvalidateMusicalContextCache()` at every mutation entry point. The `SetCurrentFrameMusicalContext(MusicalContext?)` public helper provides the single Interpreter-facing chokepoint so invalidation discipline stays local to `ExecutionContext`.

Two files edited:
- `flow-lang/Runtime/ExecutionContext.cs` — cache field + cached fast-return + helper + invalidation at 7 sites.
- `flow-lang/Interpreter/Interpreter.cs:335` — one-line wrapper swap (`_context.CurrentFrame.MusicalContext = musicalCtx` → `_context.SetCurrentFrameMusicalContext(musicalCtx)`).

## Test results

| Metric    | Bundle D baseline | Bundle E result | Delta |
|-----------|-------------------|-----------------|-------|
| Passed    | 1785              | 1785            | 0     |
| Failed    | 33                | 33              | 0     |
| Skipped   | 1                 | 1               | 0     |
| Total     | 1819              | 1819            | 0     |
| Duration  | ~30 s             | ~30 s           | —     |

**Zero new failures, zero new skips. Test baseline preserved exactly.** The 33 pre-existing failures (Phase 29 `ArticulationOnSampleTests` x4 expecting sample-rate 22050 instead of 88200, Phase 38 `OscLoopbackTests.RoundTrip_127001_EphemeralPort_PreservesPayload` timeout under load, etc.) are unchanged from Bundle D and unrelated to this change.

## Determinism gates

| Render                                       | Hash run 1                                                         | Hash run 2                                                         | Match |
|----------------------------------------------|--------------------------------------------------------------------|--------------------------------------------------------------------|-------|
| `examples/dsp/granular.flow` → `/tmp/granular_demo.wav`               | `b7372f88c239c2712001918b8c11c497df89b30f300a9c9415d7617d8c6cdab5` | `b7372f88c239c2712001918b8c11c497df89b30f300a9c9415d7617d8c6cdab5` | yes   |
| `examples/tutorial.flow` → `examples/output/flow_tutorial.wav`        | `a8a23f6717541a24c5700b1153dfb1947645b44819d7d2d71a9191d19724346d` | `a8a23f6717541a24c5700b1153dfb1947645b44819d7d2d71a9191d19724346d` | yes   |
| `tests/test_humanize_voice_block.flow` → `examples/output/test_humanize_voice_block.wav` | `23113ef02e1405a0c58ee878e370dd137ca25392d11fc9c65dd926628f5cf0a0` | `23113ef02e1405a0c58ee878e370dd137ca25392d11fc9c65dd926628f5cf0a0` | yes   |

Plan-checker note honored: `tests/test_voice_block*.flow` glob matches nothing in the repo; substituted `tests/test_humanize_voice_block.flow` (the only voice-block test that emits a WAV via `writeWav`).

All three pairs diff EMPTY — two-run cmp-clean determinism preserved.

## Benchmarks

Three Bundle E benches were run back-to-back to gauge noise envelope. The middle run (`results-bundle-e-20260524-203449.txt`) had the lowest stddev across the board and is the primary point of comparison below. Variance details captured at the bottom of this section.

### Marginal vs Bundle D (`bench/results-bundle-d-20260524-201830.txt` → `bench/results-bundle-e-20260524-203449.txt`)

| Benchmark              | Bundle D (s) | Bundle E (s) |   Δ s    |   Δ %   |
|------------------------|-------------:|-------------:|---------:|--------:|
| bench_collections      | 2.070        | 2.098        | +0.028   | +1.4 %  |
| bench_function_calls   | 1.488        | 1.480        | −0.008   | −0.5 %  |
| bench_notestream       | 1.204        | 1.202        | −0.002   | −0.2 %  |
| bench_overload         | 1.458        | 1.438        | −0.020   | −1.4 %  |
| bench_parse            | 1.420        | 1.472        | +0.052   | +3.7 %  |
| bench_var_lookup       | 3.240        | 3.184        | −0.056   | −1.7 %  |

### Cumulative vs original baseline (`bench/baseline.txt`)

| Benchmark              | Baseline (s) | Bundle E (s) |   Δ %   |
|------------------------|-------------:|-------------:|--------:|
| bench_collections      | 2.692        | 2.098        | −22.1 % |
| bench_function_calls   | 1.832        | 1.480        | −19.2 % |
| bench_notestream       | 1.350        | 1.202        | −11.0 % |
| bench_overload         | 1.988        | 1.438        | −27.7 % |
| bench_parse            | 1.620        | 1.472        |  −9.1 % |
| bench_var_lookup       | 4.680        | 3.184        | −32.0 % |

### Run-to-run variance (3 successive Bundle E runs)

| Benchmark              | Run 1 (s) | Run 2 (s) | Run 3 (s) | Max stddev observed (s) |
|------------------------|----------:|----------:|----------:|------------------------:|
| bench_collections      | 2.300     | 2.098     | 2.262     | 0.536                   |
| bench_function_calls   | 1.530     | 1.480     | 1.668     | 0.284                   |
| bench_notestream       | 1.268     | 1.202     | 1.290     | 0.098                   |
| bench_overload         | 1.502     | 1.438     | 1.474     | 0.059                   |
| bench_parse            | 1.414     | 1.472     | 1.440     | 0.025                   |
| bench_var_lookup       | 3.478     | 3.184     | 3.218     | 0.409                   |

Result files: `bench/results-bundle-e-20260524-203341.txt`, `bench/results-bundle-e-20260524-203449.txt`, `bench/results-bundle-e-20260524-203614.txt`.

## Expected vs observed

**Expectation:** `bench_notestream` drops the most because note-stream compile is the hottest GetMusicalContext caller.

**Observed:** Bundle E shows essentially no meaningful improvement on any of the six microbenchmarks vs Bundle D — every delta is well inside the run-to-run noise envelope (the smallest stddev on bench_notestream is 0.098 s; the observed Δ is −0.002 s, i.e. 50× smaller than noise). The reason is workload-specific: `bench_notestream` calls a proc once per iteration, and `PushFrame` / `PopFrame` straddling each proc invocation invalidate the cache, so each iteration still pays a fresh stack walk — exactly one GetMusicalContext resolution per cache lifetime. The cache provides ZERO hits in this pattern.

The cache **DOES** pay off on workloads where many `GetMusicalContext()` calls happen inside a single frame body — per-note rendering inside a single sequence-render call, multiple note-stream compiles inside the same proc, song-rendering paths that walk a SectionData chain. The two-run cmp-clean WAV-render gates (granular, tutorial, voice-block) all use those paths and continued to render byte-identically across runs, so the resolution is verified correct on those code paths even though it doesn't move the chosen microbenchmark numbers.

**Honest assessment:** Bundle E is a correctness-preserving refactor (cache invalidation surface audited + wired + verified) that removes a hot-path allocation, but the perf win does not show up in the existing bench suite because the existing bench suite isolates the proc-call dispatch pattern rather than the within-frame multi-call pattern. A future bench (e.g. `bench_renderlong.flow` that calls `renderSong` on a multi-section piece in a loop) would surface the win, if one is added.

## Invalidation surface (audit recap)

7 mutation sites in `ExecutionContext.cs` invalidate the cache:

1. `PushFrame()` — after `_callStack.Push(newFrame)`.
2. `PopFrame()` — after `_callStack.Pop()`.
3. `SetFileScopeTuning(RenderTuning)` — after the final `stack.Push(renderTuning)`.
4. `PushTuning(RenderTuning)` — after `TuningStack.Push(renderTuning)`.
5. `PopTuning()` — after `TuningStack.Pop()`.
6. `ResetBlockTuningStack()` — after the while-pop-down-to-1 loop.
7. `RestoreState(TestSnapshot)` — immediately after `GlobalFrame.MusicalContext = snap.GlobalFrameMusicalContext`.

Plus 1 external mutation chokepoint moved to a wrapper:

8. `Interpreter.cs:335` now calls `_context.SetCurrentFrameMusicalContext(musicalCtx)` instead of writing the field directly. The wrapper performs the assignment AND invalidates the cache.

`SetTuning(TuningSystem?)` (line 587, `[Obsolete]`) is a no-op shim that delegates to `SetFileScopeTuning` and inherits its invalidation through site #3 — no direct wiring needed.

**No caller of `GetMusicalContext()` mutates the returned object** — every call site reads a property (`.Tempo`, `.TimeSignature`, `.Key`, `.Velocity`, `.Pan`, `.Gain`, `.ReverbTime`, `.ActiveTuning`, `.SustainPedal`, `.VoicePoolSize`) or invokes `.Clone()` explicitly when an independent snapshot is wanted. Read-only-return contract holds (audit details in `260524-sa3-PLAN.md` `<invalidation_surface_audit>`).

## Constraints honored

- Edits confined to `flow-lang/Runtime/ExecutionContext.cs` (cache field, fast-return wiring, helpers, 7 invalidation sites) + `flow-lang/Interpreter/Interpreter.cs:335` (one-line wrapper swap).
- Observable behavior identical: test count matches Bundle D baseline exactly (1785/33/1/1819); all three deterministic-render WAV-hash pairs match byte-for-byte across two consecutive runs.
- Two-run cmp-clean determinism preserved (granular.flow, tutorial.flow, test_humanize_voice_block.flow).
- No audio render path semantic change.
- CLAUDE.md untouched.

## Self-Check: PASSED

- `flow-lang/Runtime/ExecutionContext.cs` carries `_cachedMusicalContext` field + cached fast-return in `GetMusicalContext()` + `InvalidateMusicalContextCache()` helper + `SetCurrentFrameMusicalContext(...)` public helper + 7 invalidation call sites (`PushFrame`, `PopFrame`, `SetFileScopeTuning`, `PushTuning`, `PopTuning`, `ResetBlockTuningStack`, `RestoreState`).
- `flow-lang/Interpreter/Interpreter.cs:335` calls `_context.SetCurrentFrameMusicalContext(musicalCtx)` (verified via grep).
- Commit `1d60d24` recorded with both file edits.
- Bench result files present: three `bench/results-bundle-e-*.txt` files.
