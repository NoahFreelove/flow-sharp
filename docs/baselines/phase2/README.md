# Phase 2 record: session state and evaluation jobs

Recorded 2026-09-22 on Linux (Ubuntu 26.04, SDK 10.0.112, runtime 10.0.12) against
`dev`. Restructuring roadmap Phase 2 (section 10). Design and implementation notes:
[session lifetime decision](../../decisions/2026-09-22-session-lifetime.md).

## Gate

| Gate item | Evidence |
| --- | --- |
| Concurrent independent engines produce isolated output and results | `SessionIsolationTests`: 12 parallel runs of two programs (different tempo, strictness, seeded randomness, proc definitions, advisories, rendered instruments) each equal their serial baseline, including rendered samples; nothing reaches the process console. |
| Cancellation disposes resources | `CancellationTests`: time limits and host cancellation stop loops, recursion and long renders; the engine stays usable; engine disposal releases tracked resources, and a script-opened OSC listener's UDP port is free again. Cancellation stops playback started by the evaluation. |
| Timed-out and superseded work does not remain active | `CoordinatorTests`, `WatchModeEditTests`: a superseded runaway evaluation stops and `ActiveJobs` returns to 0; a job that ignores cancellation is reported as still active, and its late result is rejected. `ProcessWorkerTests`: an unresponsive or busy worker process is killed within its deadline and replaced. |
| Previous valid playback survives a failed edit | `WatchModeEditTests.FailedEditKeepsThePreviousVersion` and `NewerEditCancelsARunawayRender`: only the newest successful render is staged for playback; `CoordinatorTests.FailedEditKeepsThePreviousGoodResult`. |

## What changed

| Work item (roadmap) | Result |
| --- | --- |
| Inject output/diagnostics, configuration snapshots, random services, module sources, caches, optional music state | `EngineOptions` → `SessionServices` (sinks, `AdvisoryLog`, config snapshot, budgets, tracked resources, service bag) and `RenderServices` (caches, RNGs, wavetables, timeline BPM, MIDI sink). Module sources still resolve through `ModuleLoader` paths (source providers are Phase 3). |
| Remove static current-engine accessors; explicit services for the renderer | `FlowEngine.CurrentSampleCache`, `CurrentSfzSampleCache`, `CurrentExecutionContext` removed. The renderer reads the session's `RenderServices`. `SynthUtils.Rng`, the dither RNG, custom wavetables, `setBPM` tempo, the MIDI sink, OSC queues and rate limits and the TTS command are per session. |
| Cooperative cancellation and host budgets | `FlowEngine.Evaluate` + `EvaluationOptions` (token, time limit) → `EvaluationResult`. Deadline-based checkpoints in loops, calls, builtins, section rendering, voice mixing and DSP loops. Iteration ceiling for `setMaxIterations`. |
| Extract live job coordination; latest-request-wins; stale-result rejection | `FlowLang.Hosting.LatestRequestCoordinator<T>`; watch mode renders through it (the orphaning `Task.Run` + `Wait` pattern is gone); REPL Ctrl+C cancels the running evaluation. |
| Process-worker option for hard termination | `FlowLang.Hosting.ProcessEvaluationWorker` + `flow-interpreter --worker` (JSON lines). Desktop only. |
| Hosts stop redirecting the console | `WasmEntry`, `flow check`, `flow doc` example runner, `FlowEngineRunner` test fixture and the minimal host use engine sinks. `flow doc` examples now stop cooperatively at their time limit instead of leaking a background thread. |

Also fixed along the way:

- **Language:** unit values (`s`, `ms`, `Hz`, `dB`, cents, beats) formatted full
  double precision (`0.6666666666666666s`) while plain doubles used 10
  significant digits. All value formatters now share one rule (`music.units`
  contract).
- **Browser:** the D-48-10 30 s cap was documented as unenforceable in
  single-threaded WASM. It is now enforced through deadline checkpoints and
  reports the `cancel` error kind.
- **Built-in wavetable variants** ("warm", "bright", "buzz") moved to an immutable
  process-wide table, so every engine sees them while user wavetables stay
  per session.

## Behavior changes to know

- One-shot advisories are once per engine (or per shared `AdvisoryLog`), not once
  per process. Watch mode shares a log across reloads, so its behavior is unchanged
  for composers. Five tests that asserted cross-engine dedup now pin the shared-log
  contract.
- `print` output from a pure-Flow test body goes to the engine's output sink (the
  console by default).

## Minimal host

[`minimal-host.json`](minimal-host.json) (same probe as Phase 1): the program runs
with engine sinks and no console redirection. No session or render state is
reachable outside the engine after construction or evaluation. The remaining
language-only blockers (static assembly closure, eager music module loading,
musical builtin registration) are Phase 3 work, unchanged from the Phase 1 record.

## Verification

See the progress ledger's Phase 2 section for commands and results.
