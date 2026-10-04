# Stable track buses and captured track gestures — 2026-10-04

P8-13 separates track display order from audio-bus identity and adds track commands.
The full backend readiness goal remains active.

## Implemented

- Each normalized `ProjectTrack` now has a stable input-bus index (0–63). Track list
  order is display order; reordering preserves bindings and does not rewrite user
  generator code or route another track through the wrong effect chain.
- Project JSON version 6 requires explicit bus indices. Versions 1–5 remain readable;
  missing indices migrate from original list order, preserving their original sound.
  Routing validates unique track IDs and bus indices.
- Default project compilation uses the explicit mapping. Unassigned graph buses get
  prepared silence, so removing a track does not compact surviving buses. Empty
  track lists can still prepare against the retained graph. The lower-level legacy
  compiler overload without a mapping retains its positional behavior.
- `ProjectTrackCommands` provides rename, reorder, instrument assignment, add and
  remove as captured actions. Removing tracks removes their score/audio clips in
  the same action while retaining reusable sources, assets and automation. Undo
  restores the captured project; no regeneration is involved.
- Add requires an unused explicit input exposed by the selected Flow graph. It
  validates instrument availability and graph input existence. Expanding a graph
  to expose another input is still a separate graph-authoring operation; the current
  one-track default does not automatically expand when the user adds a track.
- Flow's three-argument `dawTrack` keeps positional migration semantics; a new
  four-argument overload supplies a stable bus explicitly. Project construction
  exports always include that bus, including sparse/reordered routing.

## Verification

Five new tests cover distinct gain paths surviving reorder/removal, sparse bus
save/reopen and Flow export, empty routing silence, undo/redo, explicit bus reuse,
instrument assignment, invalid gesture atomicity and v5→v6 migration.

All **299** affected backend and module-characterization tests passed:
`/tmp/flow-track-bus-regression.log`. The two module snapshots were deliberately
updated and reviewed: exactly one new `dawTrack(String, String, String, Int)` native
signature, with registration/reachability counts each increasing by one. Normal
snapshot comparisons passed in the regression run.

All **16** focused track/persistence/Flow-export tests also passed against the Web
runtime (`/tmp/flow-track-bus-web.log`). The Desktop solution was then restored and
built successfully with zero errors (`/tmp/flow-track-bus-desktop-build.log`).
Existing analyzer/package warnings remain. `git diff --check` passed. The full
core suite was not repeated after this feature; the preceding full core gate is
recorded in the Web test boundary handoff.

## Next

Add coordinated Flow graph-authoring gestures for convenient track creation and
mixing without changing generated source ownership. Continue device/plugin and MIDI
input/recording requirements, integrated host UX contracts and qualification. Native
frontend and historical callback-gap investigation remain deferred. These tests do
not establish real-device timing or physical recovery.
