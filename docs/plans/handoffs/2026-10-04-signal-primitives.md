# Composable signal primitives — 2026-10-04

P7-18 adds a first public effect composed from sample operations rather than a
matching native effect. Backend/plugin readiness is still incomplete.

## Implemented

- `flow.value` / `(dawValue id value)` explicitly broadcasts a numeric value as
  a stereo sample stream. Its stable `value` parameter supports existing 5 ms
  smoothing, latest-value preview and absolute-frame automation. Range ±1,000,000.
- `flow.multiply` / `(dawMultiply id left right)` multiplies corresponding stereo
  samples, preserving channels. Intermediate multiplication uses double precision
  and saturates to finite float range. Shared-node merging retains conflict checks.
- `flow.tanh` / `(dawTanh id input)` applies the waveshaper per sample. Existing
  sum/mix supplies signal addition. These primitive nodes do not expose bypass.
- The prepared executor handles zero-input value nodes and parameterless processors
  without interpreting Flow or allocating on the callback. JSON and executable
  Flow export reconstruct all three node kinds through public APIs.
- `examples/plugins/composed-soft-clip.flow` implements
  `tanh(input * drive + bias) * level`. Its adjacent `.flowplugin` package contains
  exact source/hash, ports and three stable public controls targeting value nodes.
  The tested example builds through the restricted, isolated plugin path.

All streams here are explicitly stereo/sample-rate graphs. Numeric-to-stream
conversion is `dawValue`; no scalar is silently treated as an audio signal. A
separate control-rate optimization/type layer is not implemented. This is not yet
an instrument graph, oscillator/envelope/filter API or general DSP compiler.

## Verification

All **347** affected backend/module tests passed
(`/tmp/flow-signal-primitives-regression.log`). Four focused tests passed after
saving the final example package (`/tmp/flow-composed-example-final.log`). Coverage
includes sample-formula parity, signal parameter smoothing, zero render allocation,
automation ownership, absolute-frame steps, finite multiply overflow behavior,
graph JSON, executable Flow export and a real isolated example build. The example
package test verifies its source is byte-for-byte the adjacent editable Flow file.

The two module snapshots were deliberately updated/reviewed for exactly three
new public signatures: native totals 688→691, reachable 682→685; `@flowDaw`
surface 417→420. Existing unreachable native signatures remain unchanged.
Full core/Web/hardware gates were not repeated in this step.

## Next

Add runtime public parameter mapping and composable oscillator/envelope/filter
primitives, then required subtractive synth and sampler examples. Continue plugin
voice/event contracts and asset capabilities, state/tail transitions, MIDI input
and recording, and integrated backend workflows. No native frontend is started;
historical callback-gap diagnosis remains deferred.
