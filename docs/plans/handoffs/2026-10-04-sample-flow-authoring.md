# Flow sampled instrument authoring/export — 2026-10-04

P7-36 connects sample graph kernels and immutable asset bindings to public Flow.

## Implemented

- `dawSample(id, frequency, gate, assetSlot, rootHz)` constructs the two-input
  reader with a stable resource slot. It does not open or decode files.
- `dawSampleInstrument(graph, voices, Dict<Int, DawAudio>)` validates/copies typed
  immutable slot bindings and constructs a graph instrument. It enforces exact
  referenced-slot coverage and existing inline resource limits.
- `dawProjectGraphSample(project, sourceId, layerId, slot)` retrieves a saved graph
  instrument asset for executable project reconstruction.
- FlowGraphExporter emits explicit reader construction instead of rejecting sample
  nodes. Standalone graph export reconstructs the graph definition, not its external
  PCM bindings. FlowProjectExporter retains bound sampler content in its resource
  seed, retrieves assets by stable slot, then emits explicit graph/asset-map/instrument
  construction. Other instrument placeholders retain previous behavior. No source
  code reexecution or external asset lookup is required for exported reconstruction.

`examples/instruments/sampled-instrument.flow` generates a short in-memory sample,
then returns a tuned, velocity-sensitive graph sampler with ADSR, demonstration
score and master mixer. It uses the ordinary generator worker. It is not a declared
plugin example: restricted plugin asset acquisition still requires a dedicated
capability. Existing one-shot, linear interpolation, no-loop/no-stream semantics
remain explicit.

## Verification

Three focused authoring/export/module tests passed (`/tmp/flow-sample-authoring.log`).
They verify typed asset dictionaries, exact project save/export reconstruction and
sample-identical voice playback. Module snapshots deliberately add three functions:
native 698→701, reachable 692→695; the same six unreachable legacy bindings remain.
The saved sampler example was added to actual worker → acceptance → save/reopen →
routed project compiler/nonzero-audio coverage.

All **387** affected backend/module tests passed with zero failures
(`/tmp/flow-sample-authoring-regression.log`). `git diff --check` passed.

## Next

Add restricted plugin asset capabilities and reusable sampler/drum plugin examples,
then MIDI input/basic recording, device instances/presets and remaining backend
workflows. Refresh full core/Web compatibility. Native JUI frontend and historical
callback-gap diagnosis remain deferred; hardware audio qualification remains open.
