# Saved public plugin parameters — 2026-10-04

P7-19 adds source-instance parameter values, separate from pinned definitions and
accepted default graphs. Backend readiness remains active.

## Implemented

- `ProjectSource.PluginValues` is a detached typed-value override dictionary keyed
  by stable public parameter ID. Missing entries use manifest defaults. Values
  validate against declared ranges and discrete mappings; undeclared values fail.
- `ProjectPluginCommands.SetParameter` and `ResetParameter` capture one document
  action. They preserve source code, package, default graph and output identities.
  If any mapped target has an automation lane, the manual gesture is rejected
  before mutation. Repeated identical overrides/reset of an absent override are
  no-ops. Call the session's preparation request after a committed control edit.
- `PluginEffectContract.ApplyValues` lowers all target bindings into one prepared
  graph. One public control may target multiple nodes without partially publishing
  them. This occurs on the worker; no Flow runs in the callback. Structural device
  settings are prepared anew as part of the same captured edit.
- Project compiler applies the values only after validating the pinned default
  graph. JSON schema **9** stores per-source overrides and reads schemas 1–9.
  Earlier saved sources without values use defaults. Full Flow export preserves
  overrides while reconstructing the original default graph.
- Rebuilding the same plugin ID retains stable overrides. Incompatible/removed
  public parameters reject acceptance rather than silently dropping saved values;
  callers can reset overrides explicitly before attempting that update. Replacing
  the plugin identity or accepting ordinary source clears plugin instance values.
- Preview cancellation restores the effective saved value, not the manifest's
  default. Visual mixer edits clone the effective graph, so a prior knob adjustment
  is retained when the graph becomes a canonical managed source. Original pinned
  source/default output remain intact. History accounting includes override data.

## Verification

All **352** affected backend/module tests passed, zero failures:
`/tmp/flow-plugin-parameters-regression.log`.

Five new cases cover exact audible gain changes, two-target mapping, atomic
history, save/reopen, executable Flow export, undo/redo/reset, automation rejection,
stable values across rebuild, invalid-edit atomicity, preview cancellation and
visual graph cloning. `git diff --check` passed. Full core/Web/hardware gates were
not repeated for this step.

## Next

These are committed public-control edits. Continuous public-ID preview across
multiple targets still needs an atomic batch path; the existing preview token
addresses one graph parameter. Public-ID automation lanes and independently
instanced plugin devices/presets remain open (current values belong to a generated
source instance).

Continue oscillator/envelope/filter primitives and authored instrument examples,
voice/event lifecycle, tail transitions, asset capabilities, MIDI input/recording
and integrated host workflows. Native JUI frontend and historical callback-gap
investigation remain deferred.
