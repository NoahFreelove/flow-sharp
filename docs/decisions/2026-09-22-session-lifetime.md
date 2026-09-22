# Session lifetime and ownership

Status: accepted for restructuring Phase 1 (2026-09-22). Implemented in Phase 2.

## Context

`FlowEngine` is the only embedding unit. Its lifetime leaks into process state:

- Construction publishes `FlowEngine.CurrentSampleCache`, `CurrentSfzSampleCache`
  and `CurrentExecutionContext`, so the latest engine wins for static renderers.
- Printing goes to `Console.Out`, so every host (CLI, tests, WASM, the minimal host)
  redirects process-global console streams to capture output.
- Advisory deduplication (`RenderingDiagnostics`), `FlowConfig.Active`,
  `SynthUtils.Rng` and `PianoSynthesizer.CurrentReleaseSec` are process-wide.
- `WasmEntry` keeps a shared engine whose lifetime is implicit.
- Diagnostics accumulate in two lists (`ErrorReporter.Errors` and
  `ErrorReporter.Diagnostics`). Hosts now read them through
  `ErrorReporter.ErrorCount`/`FormatAll`; the session diagnostic sink should
  replace both lists with one.

The full static-field inventory is in `docs/baselines/phase0/static-fields.json`.

## Decision

1. **The application owns sessions.** A session owns lexical scopes, module state,
   the module/type catalog view for its profile, output sinks, diagnostics,
   advisory deduplication, random state and optional music evaluation state.
2. **A session executes serially.** Concurrent calls into one session are a usage
   error unless an API documents otherwise. Independent sessions run concurrently
   without sharing mutable state.
3. **Render jobs own render state.** A render job owns its prepared graph, DSP
   state, synthesis RNG and render options (for example release time). The device
   host owns the audio device. Shared asset caches are immutable-data services with
   explicit lifetime, passed in rather than found through statics.
4. **Output and diagnostics are injected.** `print` writes to the session's output
   sink and advisories to its diagnostic sink. No component redirects `Console`.
   Deduplication is per session (or per job), not per process.
5. **`Dispose` ends everything the session started.** This covers workers, timers,
   OSC listeners, MIDI/JACK handles and the audio context (WASM). After disposal
   no static reference to the session remains.
6. **`FlowEngine` becomes a compatibility facade.** It constructs one `legacy`
   session. Existing members (`Execute`, `Context`, `ErrorReporter`, `SourceMap`)
   delegate while callers migrate.

## Verification to add (Phase 2 gate)

- Two sessions created in one process run different programs concurrently. Each
  captures only its own output and diagnostics, and repeated advisories appear once
  per session.
- Creating and disposing sessions in a loop leaves no `FlowEngine.Current*`-style
  static pointing at a disposed session. The minimal-host `processStatics` fields
  all become false.
- Hosts no longer call `Console.SetOut`/`SetError` to capture evaluation output.
  `WasmEntry` and the test fixtures use sinks.
