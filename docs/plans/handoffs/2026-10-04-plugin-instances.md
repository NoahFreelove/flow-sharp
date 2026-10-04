# Independent plugin instances — 2026-10-04

P7-39 adds duplication and unused-instance removal to the existing source-based
device model. Creation/reload still use ProjectGeneratorHost.RequestPluginBuild;
there is no second representation of plugin code or prepared DSP.

## Implemented

ProjectPluginInstanceCommands.Duplicate copies the accepted instrument/effect with
new source and output-binding IDs. Immutable package and generated layer contents
can be shared safely, while public-value dictionaries and bindings are independent.
The copied result retains the accepted generation context and revision and receives
the new source ID. Duplication does not rerun Flow or generate different random data.

Automation is copied by default with new lane IDs and remapped output bindings;
the caller can explicitly omit it. Both public parameter and graph-node targets use
this mapping. Existing clips, tracks and master selection stay attached to their
current sources. Instrument duplication can optionally assign the new instance to
an existing track in the same history action. Effects are copied unassigned.

Removal rejects devices referenced by the mixer, tracks or clips. Once detached,
removal captures the source and its automation in one undoable action. Existing
document invalidation prevents a pending build from resurrecting the removed source,
including after undo. Undo/redo preserve copied identities without new evaluations.

ProjectPlaybackSession exposes DuplicatePlugin and RemovePlugin and requests normal
bounded playback preparation. Existing SetInstrument supports assigning accepted
bindings without duplication. The frontend must distinguish deliberate sharing of
one source binding from independent duplication; sharing also shares saved values.

## Verification

- Initial focused parameter/build tests: **24 passed**, zero failures.
  `/tmp/flow-plugin-instances-focused.log`.
- Final affected backend/platform/music/hosting/module tests: **456 passed**, zero
  failures or skips. `/tmp/flow-plugin-instances-regression.log`.
- Tests cover independent values and rebuild acceptance, remapped automation,
  optional automation omission, JSON identity preservation, rendered Flow export
  parity after routing the copied effect, instrument duplication/assignment,
  captured undo/redo, invalid assignment, referenced-device removal rejection and
  pending-build invalidation after removal/undo.
- `git diff --check` passed. Full core/Web/hardware qualifications were not rerun
  for this slice; earlier checkpoints remain documented in their handoffs.

## Remaining integration

The compiler currently selects one mixer graph and instrument bindings per track.
Independent effect sources exist but there is no persisted chain of plugin-instance
references per track/bus. ProjectMixerAuthoring edits graph nodes and can lower a
selected plugin graph into managed Flow construction code; that does not preserve
the live identity of multiple packaged effects in a chain. Address this gap before
claiming the instrument/effect instance workflow complete. Composition must preserve
public parameters, automation, ordering, bypass, Flow export and instance ownership.

Also continue safe reload compatibility, remaining plugin categories and integrated
bounce/export/recovery. Native JUI frontend and historical callback-gap investigation
remain deferred. The overall backend goal is active.
