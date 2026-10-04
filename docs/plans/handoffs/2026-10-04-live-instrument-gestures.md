# Live instrument project gestures — 2026-10-04

P7-31 connects public instrument parameters to acknowledged project playback and
existing preview/cancel/commit gesture handling.

## Implemented

Arrangement preparation retains graph voice pools by track and groups pools by
instrument output binding. ProjectCompiler supplies the track-to-instrument binding
map; PreparedArrangement exposes read-only control groups by binding. The parent
playback owns the update boundary before any track renders. Legacy instruments
have no graph controls; unused/unrouted instruments cannot begin a preview.

ProjectPlaybackCoordinator resolves public instrument controls to the active
binding's group. Effects retain master-graph parameter publication. Both paths use
the existing single-gesture guard, acknowledged snapshot identity, frozen commit,
cancellation restoration and retirement detection. A preview changes sound without
changing the document. ProjectMixerHost commits one captured public-value action,
requests preparation, and keeps the preview until replacement acknowledges.

Instrument metadata can now declare live parameters when all targets support
catalog automation/smoothing. Structural parameters still require rebuild. The
saved subtractive plugin declares level/cutoff live with 5 ms smoothing, and release
structural. Newly started/stolen voices inherit current targets; active voices ramp.
The pinned source hash is unchanged because only discovery metadata changed.

This implements live host gestures, not persisted musical automation lanes for
public instrument parameter IDs. Those remain separate work.

## Evidence

All twelve PluginBuildTests passed (`/tmp/flow-instrument-preview.log`). New coverage
builds the actual saved plugin in the isolated worker, assigns it to a track with a
note, prepares project playback, verifies nonzero sound, previews half level without
history, cancels to exact original audio, commits one action, verifies prepared
replacement audio, undoes/restores original audio, and rejects live release preview.
Prior group concurrency tests cover shared-track block coherence. `git diff --check`
passed. This does not qualify physical callback timing.

All **379** affected backend/module tests passed with zero failures
(`/tmp/flow-instrument-preview-regression.log`). Full core/Web gates were not
repeated for this slice.

## Next

Sampler graph primitives/examples, public instrument automation, device instances/
presets, MIDI input/basic recording and integrated workflow readiness remain open.
Refresh full core/Web evidence after further related changes. Native JUI frontend
and historical callback-gap diagnosis remain deferred.
