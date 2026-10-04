# Coherent audio-to-UI meter snapshots — 2026-10-04

P6-21 adds a frontend-ready meter read boundary to the prepared audio graph.
The overall backend-readiness goal remains active.

## Implemented

- Prepared graphs expose immutable `MeterNodeIds` describing snapshot order.
  `TryReadMeters` copies completed-block peak/RMS values and the corresponding
  absolute frame position into a reader-owned buffer. Multiple readers are allowed.
- A preallocated publication array separates completed snapshots from in-progress
  processing meters. A single audio owner brackets publication with sequence/fence
  updates. Readers accept only a stable sequence; torn/mixed copies are discarded.
- Reads are bounded to three attempts and never block the audio owner. On failure,
  callers ignore their scratch-buffer contents and retain the last accepted UI
  snapshot. Read calls allocate nothing; the UI owns any retained display objects.
- Process publishes after the complete block. Reset publishes zero meters at the
  new transport frame. The old `GetMeter` remains audio-owner-only; cross-thread
  readers must use the new API.
- Publication adds one small prepared meter array plus metadata and a copy per
  completed block. It does not expose the graph's processing buffers or require
  locks, callback waits or callback allocation.

## Verification

All 19 focused meter/audio-graph/delay/automation tests passed. New coverage checks
node ordering, frame identity, reset clearing, no managed allocations and concurrent
reads across 20,000 changing blocks, accepting only internally consistent snapshots.
Log: `/tmp/flow-meter-snapshots.log`.

The affected StudioModel/PlatformAudio/MusicModel/StaticBinding/Hosting regression
run passed **273 tests**, 0 failed. Log: `/tmp/flow-meter-snapshot-regression.log`.
Existing analyzer warnings remain; `git diff --check` passed. No full-solution run
or Web publish was performed.

## Remaining

Frontend consumption, meter display smoothing/peak hold, unified transport/device
status snapshots and integrated publication under editor load remain open. This is
not new physical device timing qualification. Native frontend and historical
callback-gap diagnosis remain deferred.
