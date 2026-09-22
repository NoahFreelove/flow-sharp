# Session lifetime and ownership

Status: accepted in Phase 1; **implemented in Phase 2 (2026-09-22)**. The
implementation notes below record what exists now.

## Context (as found in Phase 1)

`FlowEngine` was the only embedding unit, and its lifetime leaked into process state:

- Construction published `FlowEngine.CurrentSampleCache`, `CurrentSfzSampleCache`
  and `CurrentExecutionContext`, so the latest engine won for static renderers.
- Printing went to `Console.Out`, so every host (CLI, tests, WASM, the minimal host)
  redirected process-global console streams to capture output.
- Advisory deduplication (`RenderingDiagnostics`), `FlowConfig.Active`,
  `SynthUtils.Rng`, the WAV dither RNG, custom wavetables, the `setBPM` tempo, the
  in-memory MIDI sink and the OSC handler queue were process-wide.
- `WasmEntry` kept a shared engine whose lifetime was implicit.
- Background resources a script opened (OSC listeners, MIDI clocks and ports)
  outlived their engine.
- Watch mode enforced its 30 s render cap with `Task.Run` + `Wait` and leaked the
  timed-out worker.

## Decision

1. **The application owns sessions.** A session owns lexical scopes, module state,
   the module/type catalog view for its profile, output sinks, diagnostics,
   advisory deduplication, random state and optional music evaluation state.
2. **A session executes serially.** Concurrent calls into one session are a usage
   error unless an API documents otherwise. Independent sessions run concurrently
   without sharing mutable state.
3. **Render jobs own render state.** Render caches, synthesis and dither RNGs and
   render options belong to the session's render services, not to statics.
4. **Output and diagnostics are injected.** `print` writes to the session's output
   sink and advisories to its diagnostic sink. No component redirects `Console`.
   Deduplication is per session.
5. **`Dispose` ends everything the session started,** including workers, timers,
   OSC listeners, MIDI clocks and ports, and the audio context (WASM).
6. **`FlowEngine` is the compatibility facade** that constructs one session.

## Implementation (Phase 2)

| Concern | Now |
| --- | --- |
| Session object | `FlowLang.Runtime.SessionServices`, owned and disposed by `FlowEngine` (`engine.Session`). Configured through `EngineOptions` (output, diagnostics, config snapshot, advisory log, iteration ceiling). |
| Output | `print`, visualization, test runner, transforms and backend warnings write through the session (`FlowConsole.Out`/`Error`). Default sinks follow `Console` at write time, so existing CLI behavior is unchanged. |
| Advisories | `RenderingDiagnostics.WarnOnce` writes to the current session and dedups in its `AdvisoryLog`. Engines get their own log by default; watch mode shares one log across its per-reload engines, so advisories stay once per watch session. |
| Render state | `FlowLang.StandardLibrary.Audio.RenderServices` (sample caches, SFZ cache, evaluation context, noise and dither RNGs with the original seeds, custom wavetables, timeline BPM, MIDI sink). The `FlowEngine.Current*` statics are removed. |
| Configuration | `SessionServices.Config` snapshot, taken from `FlowConfig.Active` when the engine is created (or supplied by the host). `FlowConfig.Active` remains the process default that CLI hosts load before creating engines. |
| OSC | Rate-limit timestamps and the pending-handler queue are per session; one engine's `(oscPump)` never runs another engine's handlers. |
| Resources | `SessionServices.Track(release)` registers script-opened resources (OSC listeners, MIDI clocks, MIDI output ports); session disposal releases them in reverse order. |
| Cancellation | `FlowEngine.Evaluate(source, file, EvaluationOptions)` → `EvaluationResult` with outcome `Succeeded`, `Failed`, `Cancelled` or `TimedOut`. Checkpoints at loop iterations, proc calls, builtin calls, section repeats, voice mixing and DSP frame/grain loops. Time budgets are deadlines checked at checkpoints (they work in single-threaded WASM) plus a token for blocking waits. Cancellation is never cached by lazy thunks and is not swallowed by module-loading or test-runner catches. A cancelled evaluation stops playback it started. |
| Budgets | `EngineOptions.MaxIterationsCeiling`; `(setMaxIterations N)` above it is capped with a `[budget]` advisory. |
| Jobs | `FlowLang.Hosting.LatestRequestCoordinator<T>`: latest-request-wins, generation IDs, stale-result rejection, cancel-and-await of superseded work, last-good retention. Watch mode renders through it (the orphaning `Task.Run` + `Wait` is gone); the REPL cancels the running evaluation on Ctrl+C. |
| Hard stop | `FlowLang.Hosting.ProcessEvaluationWorker` + `flow-interpreter --worker` (JSON-lines protocol). On timeout or cancellation the worker process tree is killed and awaited; the next request starts a fresh worker. Desktop only. |
| Browser | `WasmEntry.RunFromJs` gives each run's engine its own sinks (no console redirection) and enforces the D-48-10 30 s budget cooperatively, reporting the `cancel` error kind. |

### Transitional adapter

Legacy static code that cannot yet take the session as a parameter (renderer
internals, advisory helpers, DSP loops) finds it through `SessionServices.Current`.
The engine sets that for the duration of each entry point (`Evaluate`, construction,
`TestRunner.Run`, `engine.EnterScope()` for hosts that call renderers directly). The
value flows into tasks and threads started inside the scope, and is null
everywhere else. It is a lookup mechanism, not ownership: nothing is found through
"the latest engine". It should disappear when the renderer takes explicit render
contexts (Phase 5).

### Process-wide state that remains (by design)

- `FlowConfig.Active`: the host's default configuration, snapshotted per session.
- Immutable tables and one-time registrations: built-in wavetable variants, scale
  and chord tables, the librtmidi availability probe.
- Test seams (`InputFunctions.CaptureOverride`, `MidiFunctions.BackendOverride`,
  `JackFunctions.TransportQueryOverride`, `OscFunctions.HandlerInvokeOverride`,
  `VoiceAllocator` test pool size): process hooks for tests only.
- `PianoSynthesizer.CurrentReleaseSec`: an `AsyncLocal` render option, already
  isolated per async flow; it moves into explicit render options in Phase 5.
- `AbcImport`/`MmlImport` strict context: thread-static around one synchronous parse.

## Verification (Phase 2 gate)

- `flow-lang.Tests/Hosting/SessionIsolationTests`: two programs differing in tempo,
  strictness, seeded randomness, proc definitions, advisories and rendered
  instruments run 12 times on parallel threads. Each run reproduces its serial
  baseline exactly (stdout, diagnostics, error count, rendered samples), and the
  process console receives nothing. Also covered: per-engine vs shared advisory
  logs, config snapshots, no engine state reachable outside entry points.
- `CancellationTests`: time limits and host cancellation stop loops, recursion and
  long renders within bounds; the engine stays usable; cancelled thunks are not
  cached; the iteration ceiling holds; disposal releases tracked resources and a
  script-opened OSC listener's port.
- `CoordinatorTests` and `WatchModeEditTests`: superseded runaway work stops and
  `ActiveJobs` returns to 0; late results are rejected; failed and timed-out edits
  keep the previous version.
- `ProcessWorkerTests`: an unresponsive worker is killed within the deadline and
  replaced; host cancellation kills a busy worker.
- `WasmDiagnosticsTests.RunFromJsStopsARunawayScriptWithACancelError`.
