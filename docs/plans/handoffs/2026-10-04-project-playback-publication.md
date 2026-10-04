# Project-to-playback publication — 2026-10-04

P6-22 connects committed project snapshots to prepared live playback. The overall
backend-readiness goal remains active; native frontend work has not started.

## Implemented

- `ProjectPlaybackCoordinator` captures immutable preparation tickets on the
  document/control thread. A host can run static `Prepare` on a bounded worker,
  resolving external assets and compiling the arrangement without touching the
  document or active audio cursor.
- Completion accepts only the latest ticket for the current snapshot. Failed,
  superseded, cancelled or stale work does not replace working audio. Prepared
  builds are checked again against the document immediately before publication.
- Ready playback is retained when the audio queue rejects replacement because a
  stop or previous replacement is pending. Control-thread polling retries later;
  publication neither changes history nor silently drops the ready result.
- `ActiveSnapshot` changes only after the audio owner's generation acknowledgement.
  An unacknowledged submission cannot be overwritten by a later submission. Retired
  transports are reclaimed by control-thread polling, outside the callback.
- Replacement uses the existing explicit behavior: install at a block boundary,
  stopped at frame zero, with no loop. This is not seamless live recompilation or
  preservation of transport position and effect tails.
- The exposed queue supports transport/device attachment, but replacement and
  retired-source collection belong exclusively to this coordinator. Hosts must
  marshal Begin/Complete/Fail/TryPublish calls to one control owner.

## Verification

All **278** affected StudioModel/PlatformAudio/MusicModel/StaticBinding/Hosting
regression tests passed, 0 failed. Log:
`/tmp/flow-project-publication-regression.log`.

Five new tests cover acknowledgement/history behavior, latest-result selection,
stale/failed work retaining audible old output, cancellation, stop/replacement
backpressure, and 100 consecutive publications while another thread renders.
Existing analyzer warnings remain. No physical-device or full-solution run was
performed. `git diff --check` passed.

## Next

The coordinator is a publication boundary, not a worker scheduler or device owner.
Connect bounded preparation scheduling, isolated Flow generation/acceptance and
output-session lifecycle in a host-level backend service; expose preparation,
asset and active graph diagnostics/meters without crossing thread ownership.
Continue integrated editing/save/export/playback verification and remaining Flow
plugin/MIDI backend work. Historical callback-gap diagnosis remains deferred.
