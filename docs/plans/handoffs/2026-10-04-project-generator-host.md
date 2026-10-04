# Isolated Flow generation in the DAW host — 2026-10-04

P7-15 connects the isolated generator boundary to the project/playback host.
The full backend-readiness objective remains active.

## Implemented

- `ProjectGeneratorHost` owns one `ProcessGeneratorWorker` and uses a single
  control-owner Poll loop for completion, document acceptance and playback updates.
  Worker tasks never mutate the document, history or audio queue.
- Requests capture opaque document build tickets. Pending requests coalesce per
  source, retaining FIFO order between sources. A new edit to the active source
  cancels that process; another source waits without cancelling independent work.
- There is at most one running build, 64 pending source requests and 16 Mi characters
  of pending code. No task is created for each queued edit. The existing per-source
  2 Mi character limit, worker deadline and process kill/join rules still apply.
- Successful current results commit source code, detached outputs and stable bindings
  as one undoable document action, then request playback preparation. Failed,
  cancelled, superseded or invalidated results do not overwrite accepted content.
  A later preparation failure remains distinct from successful source acceptance
  and retains the old prepared audio through the existing coordinator.
- `LastCompletion` exposes source/revision, job status, acceptance and error for the
  frontend. Poll also services preparation and output; no hidden event-loop thread.
- `ProjectDocument.DiscardBuild` releases abandoned tickets without invalidating a
  newer build. The host uses it after completion and on shutdown.
- Shutdown discards queued requests, cancels/joins active work, and closes the owned
  process worker. Failed worker cleanup remains retryable. Dispose the generator
  host before the playback session; the host does not own that session.
- The host library now references the Flow compatibility host. The process adapter
  is excluded under FLOW_WEB; platform-free project/audio/engine dependencies remain
  unchanged. This is desktop host composition, not browser worker support.

## Verification

The initial three real-process integration tests passed:
`/tmp/flow-generator-host.log`.

Coverage includes isolated source edit → one history action → offline publication,
save/reopen with exact playback samples, failed source preserving accepted content,
undo and re-preparation, 100 rapid same-source requests building only the first and
latest requests, pending-source capacity and joined shutdown. Additional coverage
checks undo invalidating an in-flight result and identity-safe draft cleanup.

All **289** affected StudioModel/PlatformAudio/MusicModel/StaticBinding/Hosting
regression tests passed, 0 failed (`/tmp/flow-generator-host-regression.log`).
Existing analyzer/package warnings remain. `git diff --check` passed. No full
solution build, Web publish or physical-device qualification was performed.

## Next

Provide a default new-project factory so callers need not hand-build the initial
routing/source graph. Expand frontend-facing diagnostics and transport access,
continue shared Flow device/plugin contracts and MIDI input/recording, then run
broader solution/Web compatibility and integrated workload checks. Hardware timing
and recovery remain unqualified; callback-gap investigation and native UI remain
explicitly deferred.
