# Flow mixer effect reordering — 2026-10-04

P8-18 adds undoable serial effect reordering through the bounded mixer host.
The backend-readiness objective remains active; native frontend work is deferred.

## Implemented

- `ProjectMixerAuthoring.BeginReorderEffects` takes the desired order of a
  contiguous chain of single-input processors. It reconnects the chain input,
  all consumers of its previous tail, and the graph output where necessary.
- Node identity, device versions, parameters, bypass and automation remain
  attached to their original devices. Canonical Flow code is prepared before
  the graph and routing are accepted as one captured undo/redo action.
- Intermediate fan-out is rejected: moving an effect shared by another branch
  requires an explicit routing edit. Tail fan-out is supported. Noncontiguous,
  duplicate, unknown and routing-node selections are rejected before mutation.
- `ProjectMixerHost.TryReorderEffects` copies the queued order. Later caller
  mutation cannot change the pending gesture. Existing queue bounds, stale
  rejection, completion handling and worker shutdown apply.

## Verification

All **19** focused mixer tests passed (`/tmp/flow-effect-reorder.log`). Three new
tests cover audible order changes against an independently constructed reference,
automation identity, executable Flow export parity, captured queue input, exact
undo/redo restoration, tail fan-out rewiring and invalid-edit atomicity.

All **318** affected backend/module regression tests passed, zero failures
(`/tmp/flow-effect-reorder-regression.log`). Full core/Web gates and hardware
qualification were not repeated for this host-authoring change.

## Next

Continuous live parameter preview and transport-preserving commits remain open.
Graph commits currently retain stopped-at-zero replacement semantics, without
seamless state/tail transfer. Continue public Flow plugin/device authoring and
MIDI input/recording requirements, then integrated host workflows and readiness
validation. Historical callback-gap investigation remains deferred by the owner;
physical qualification remains open.
