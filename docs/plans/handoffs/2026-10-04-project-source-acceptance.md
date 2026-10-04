# Project source acceptance — 2026-10-04

P8-02 introduces the control-thread project document and atomic acceptance of
successful generated sources. The full backend goal remains active.

## Implemented

- `ProjectSnapshot` owns arrangement, generation context and accepted sources.
  `ProjectSource` keeps successful code, descriptor, result and stable output
  bindings. Draft code/build tickets are transient rather than durable mutations.
- `BeginBuild` issues an opaque request ticket with monotonically increasing source
  revision and the actual generation context. `Accept` rejects foreign, superseded,
  wrong-source, wrong-revision and wrong-context results without changing history.
  Worker results must carry the request's context object (the existing worker host
  already reconstructs results with that context).
- Successful source code, detached output and reconciled bindings install in one
  `IUndoableAction`. Undo/redo restores captured snapshots without executing Flow.
  History-budget failures leave the document untouched. Accounting uses serialized
  content sizes and structural estimates, not a runtime heap measurement.
- Outputs have stable binding IDs per source/role/name. Removed outputs remain
  unavailable for repair, and returning names reuse their IDs. Added outputs get
  new IDs. Binding history is capped at 4096 per source; explicit unused-binding
  repair remains to be implemented. No implicit routing is guessed.
- Arrangement edits preserve clip identities, placement, windows and nudges during
  acceptance. Independent source builds and clip-only edits keep valid tickets;
  relevant source/context changes invalidate them. Undo/redo invalidates pending
  tickets so returning to an earlier snapshot cannot revive stale work.
- `ProjectCompiler` resolves accepted outputs for arrangement preparation. Score
  clips use source/layer IDs; generated audio clips use their output binding ID as
  the audio source ID. Selected graph and track instruments use binding IDs. Missing
  score/audio outputs produce existing silence diagnostics; missing selected devices
  fail preparation so the host can retain last-good playback.

## Verification

Seven focused tests passed: captured source/content undo/redo, removed/reappearing
bindings, foreign/superseded/context rejection, independent builds and clip moves,
context-change/undo stale rejection, atomic history-budget rejection, and actual
prepared audio samples across regeneration/undo/redo/removal. Logs:
`/tmp/flow-project-acceptance.log`, `/tmp/flow-project-acceptance-playback.log`.

The affected StudioModel, PlatformAudio, MusicModel, StaticBinding and Hosting
regression run passed **221 tests**, 0 failed. Log:
`/tmp/flow-project-acceptance-regression.log`. Existing analyzer warnings remain.
This was not a full-solution run.

## Next work / limits

Persist full project sources, output binding IDs, routing/track selections and
assets, then prove save/reopen playback equivalence. Currently graph/track device
selection is passed to `ProjectCompiler`; it is not yet in the durable project.
The existing arrangement-only file format is unchanged. Add explicit binding
repair and source removal commands, failed-build diagnostics and draft editor state.
Successful document commit and prepared playback publication are separate stages;
this slice does not automatically publish to a live audio session.

Sampler/stateful effects, automation/modulation, full context, large file-backed
assets and final export quality remain open. No hardware qualification or native
frontend implementation was performed; historical callback-gap diagnosis remains
deferred.
