# Atomic public plugin preview — 2026-10-04

P7-20 connects a public plugin parameter to atomic live updates of every mapped
graph target. The complete backend-readiness objective remains active.

## Implemented

- `PreparedAudioGraph.SetLatestParameters` validates every target/value before
  publishing an immutable batch. Duplicate targets and invalid/automated parameters
  reject the whole submission. Up to 4096 targets per submission, bounded by the
  prepared graph's parameter inventory.
- A single control producer merges pending updates by target through CAS. Audio
  takes the complete batch at a boundary and applies all smoothing targets before
  processing any node. Consumption allocates nothing and performs no waiting.
  Control submission allocates and may retry if audio consumes concurrently.
- Single-parameter latest updates use the same path. Pending cancellation restores
  are preserved when another gesture starts before audio consumes them. FIFO
  commands still run before latest batches at the boundary.
- `ProjectPlaybackCoordinator.TryBeginPluginParameterPreview` resolves a stable
  source/public parameter ID to its selected graph targets and current saved value.
  It rejects structural controls requiring preparation and respects automation
  ownership. Range/discrete validation occurs before any update. Existing stale
  document, frozen gesture, cancellation and retirement handling applies.
- `ProjectMixerHost.TryBeginPluginParameterPreview` exposes the gesture. Its usual
  update/cancel/commit APIs now support public plugin IDs. Commit saves one instance
  value action through `ProjectPluginCommands`, retains the pinned source/package,
  and requests preparation. It does not clone the plugin into a managed graph.
  The bounded completion queue records the result for the UI to drain.

## Verification

All **355** affected backend/module tests passed:
`/tmp/flow-plugin-preview-regression.log`.

New evidence includes 10,000 paired updates with concurrent audio proving no
half-applied batch, invalid-batch atomicity, captured caller data, merged restores,
zero allocation during consumption, public-ID commit/history without source
cloning, and a two-target preview/cancel producing the expected exact audio.

Full core gate passed: solution build, **3,287** language/backend tests and **21**
MIDI tests, zero failures, 14 unexecuted/skipped main tests, no tracked-file changes
inside verification. `/tmp/flow-plugin-preview-core/verification.json` and adjacent
TRX/logs contain the evidence. `git diff --check` passed.

## Next

Refresh Web compatibility verification, then advance composable oscillator,
envelope and filter processing plus required authored instrument examples.
Public-ID automation persistence, device instances/presets, voice/event lifecycle,
asset capabilities, state/tail continuity, MIDI recording and integrated workflows
remain open. Hardware qualification is not implied by managed concurrency tests.
Native JUI frontend and historical callback-gap diagnosis remain deferred.
