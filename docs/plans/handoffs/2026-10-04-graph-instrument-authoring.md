# Graph instrument authoring and persistence — 2026-10-04

P7-24 connects the prepared graph voice pool to generated instrument results,
project persistence, routing/compiler selection and executable Flow export.

## Implemented

`(dawGraphInstrument graph voices)` returns a DawInstrument using the fixed voice
input contract: bus 0 pitch Hz, bus 1 gate, bus 2 velocity. The graph supplies its
own amplitude/envelope response; placement gain/pan remain external. Construction
validates polyphony, aggregate node budget and input bus indices. Legacy envelope
and root-pitch fields must retain defaults for graph instruments because they have
no effect; authored envelope/tuning belongs in the graph itself.

GeneratedContentJson schema **3** writes `flow.graphInstrument` version 1 with an
embedded versioned graph. Readers retain schema 1/2 support; graph instruments
require schema 3. Missing graph, unexpected graph on legacy instrument, invalid
ports and mixed sampler/graph definitions reject. Existing project schema 9 embeds
this independently versioned content. Worker interchange uses the same serializer.

ProjectCompiler already passes selected instrument settings through routing to the
note scheduler; it now receives graph instruments through those existing bindings.
FlowProjectExporter emits explicit graph construction followed by dawGraphInstrument,
with stable node IDs and parameter values. Source acceptance/undo/redo continues
capturing detached generated results rather than reexecuting saved code.

`examples/instruments/enveloped-sine.flow` constructs a velocity-sensitive, ADSR
sine instrument, score and mixer entirely through Flow. This is a generator example,
not an instrument plugin package: instrument-kind plugin host acceptance remains open.

## Verification

- Save/reopen and executable export restore identical project data and rendered
  voice audio; old content schema rejects graph payloads.
- Real isolated worker builds the saved example; acceptance, save/reopen, routed
  ProjectCompiler preparation and nonzero audio pass.
- **367** affected backend/module tests passed before the additional example test
  (`/tmp/flow-instrument-regression.log`). Both instrument integration tests then
  passed (`/tmp/flow-instrument-example.log`). No failure was observed.
- Module snapshots deliberately add one public signature: native 694→695,
  reachable 688→689; the same six legacy unreachable signatures remain.
- Full core/Web/hardware gates have not been repeated for this slice.

## Next

Optimize event-delimited graph voice processing, then add richer synth and sampler
primitives/examples and instrument plugin contracts. Continue public automation,
device instances/presets, asset capabilities, MIDI input/recording and integrated
workflows. Large-polyphony physical performance is not yet qualified. Native JUI
frontend and historical callback-gap investigation remain deferred.
