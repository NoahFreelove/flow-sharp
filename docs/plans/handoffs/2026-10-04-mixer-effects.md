# Flow mixer effect gestures — 2026-10-04

P8-17 adds committed effect insertion, bypass and removal to the Flow mixer host.
The full backend-readiness objective remains active.

## Implemented

- `BeginInsertEffect` inserts a versioned single-input catalog processor on an
  explicit destination edge. Node identity, device/version, parameter bounds,
  structural integer rules and graph connectivity use the shared graph validation.
  Input/sum routing nodes cannot masquerade as serial effects.
- `BeginSetBypass` retains the node, parameters and automation. Only catalog devices
  supporting bypass accept the gesture. Prepared graph behavior determines whether
  tails are active, using the existing shared live/offline kernels.
- `BeginRemoveEffect` removes a single-input processor, reconnects every consumer
  to its predecessor, and updates the graph output when necessary. This handles
  shared effect nodes without leaving disconnected references.
- Automation for the removed node on the selected graph is removed in the same
  captured action. Other automation remains. Undo restores the original graph,
  automation and canonical source together.
- The bounded mixer host exposes all three gestures. Insertion parameters are
  copied and catalog-validated when queued, so later changes to a caller's mutable
  dictionary cannot alter the pending action. Each gesture still prepares saved
  Flow code before document acceptance and playback preparation.
- Managed source IDs/bindings remain stable across these visual edits. Original
  user source remains intact, and exports reconstruct the changed graph through
  the same public Flow device definitions.

## Verification

All 16 focused mixer/effect tests passed (`/tmp/flow-mixer-effects.log`). Four new
checks cover queued captured parameters, exact audible gain changes, bypass/removal
and undo, executable Flow export, finite delay tails, automation retention/removal,
shared-node fan-out rewiring, graph-output removal and invalid-gesture atomicity.

All **315** affected backend/module regression tests passed, 0 failed
(`/tmp/flow-mixer-effects-regression.log`). Existing analyzer/package warnings
remain; `git diff --check` passed. Full core and Web publish gates were not repeated
for this host-authoring change.

## Remaining

Effect movement/reordering and continuous live parameter preview remain to be
connected. Committed edits retain the established stopped-at-zero replacement
semantics; this is not seamless tail-preserving hot graph replacement. Continue
plugin/device and MIDI input/recording requirements. Native frontend and historical
callback-gap investigation remain deferred; physical qualification is still open.
