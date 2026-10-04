# DAW backend — arrangement foundation, 2026-10-04

The owner authorized continuing autonomously until the backend is ready for the
DAW frontend. No product decision is currently required. Follow the Flow-first
contracts, native JUI target after the approved browser prototype, action-based
undo/redo, and deferred callback-gap investigation. Flow integration is backend
work, not something to postpone until the UI exists.

## Completed slice (P8-01, pulled forward as a playback dependency)

Added the UI/interpreter-independent `Flow.Studio.Model` project to the solution
and test project. It references Music.Model only; no UI or interpreter dependency.

- Immutable score clips retain stable clip/track/source/layer IDs, source windows,
  musical anchors and signed typed millisecond nudges.
- Audio clips retain sample rate and exact frame windows. Moving their musical
  anchor does not stretch their samples.
- Piecewise-constant project tempo with inverse seconds conversion; meter changes
  indexed by one-based bars and constrained to bar boundaries.
- Shared alignment, musical movement, absolute/relative time nudges, local score
  splits, exact audio-frame splits and seconds-ruler cuts. Audio ruler cuts snap
  to the nearest source frame with ties away from zero. Score splits keep source
  timing across tempo changes and both pieces keep their nudge.
- Detached arrangement snapshots validate clip IDs, copy collections and preserve
  missing source/track references for later binding repair. A split installs both
  pieces atomically. Right-hand IDs are supplied once by the caller and retained.
- `IUndoableAction` with Undo/Redo, history branching, saved-revision dirty state,
  capacity and conservative retained-byte accounting. Failed actions do not move
  stacks; composite actions roll back completed children on failure. Custom child
  actions must be atomic and support inverse operations after success. A rollback
  failure is surfaced as an aggregate error, not claimed to be recoverable.
- Document edits prepare one complete immutable result, then install it as one
  action. Redo reuses the result without reevaluating a generator. One edit per
  completed gesture provides drag coalescing at the host boundary.
- Strict versioned arrangement JSON and sibling-temp save/reopen. The file is
  flushed before replacement; dirty state changes only after successful save.
  Unsupported versions/fields fail instead of silently dropping future data.

## Validation

Release targeted run: **67 passed, 0 failed, 0 skipped**, including 14 new model
cases plus the existing 53 output-session/playback cases. Tests cover tempo inverse
conversion, meter changes, negative nudges, score/audio splits across tempo changes,
exact undo/redo snapshots, redo-branch invalidation, saved revisions, history budget
limits, compound failure rollback, JSON preservation, save/reopen, failed-save
preservation and invalid input/schema rejection. Existing compiler/analyzer
warnings remain. No hardware stress run was performed.

```sh
dotnet test flow-lang.Tests/flow-lang.Tests.csproj -c Release --no-restore -m:1 /nodeReuse:false --filter 'FullyQualifiedName~StudioModel|FullyQualifiedName~PlatformAudio|FullyQualifiedName~PreparedSineTransportTests|FullyQualifiedName~QueuedSinePlaybackTests|FullyQualifiedName~PlaybackPublicationTests'
```

## Limits and next sequence

This is arrangement interchange, not a complete project file: source bodies,
track definitions, device instances, assets, automation, dependency pinning,
autosave and migration beyond rejecting unknown versions remain to be added.
History is control-thread owned, uses estimates rather than exact CLR heap
accounting, and is not serialized. Physical file durability across power loss is
not certified by flushing the file alone. Clip operations do not yet schedule
notes or render audio, and Flow builtins exposing them are still pending.

Continue with the established authoring contract:

1. `@flowDaw` descriptor and structured generator results using bounded evaluation;
   last-good output survives failure, cancellation and stale results.
2. Versioned shared device catalog and prepared graph, then one generated tuned
   score → instrument → ordered effects → group/master live/offline slice.
3. Connect arrangement windows and edit publication to that common playback path;
   implement the specified cut/retrigger/reset/bounded-tail rules.
4. Expand project source/track/device/asset persistence, note/parameter editing,
   automation and reconstruction through equivalent Flow construction operations.
5. Headless end-to-end editing/export verification before native frontend work.

Do not treat phases 6–8 as complete or call the backend frontend-ready yet. Keep
hardware qualification limitations visible, without resuming the deferred gap
investigation or waiting on the owner for routine implementation choices.
