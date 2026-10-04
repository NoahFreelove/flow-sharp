# Managed assets and waveform preparation — 2026-10-04

P8-05 adds project-owned imports, undoable relinking and waveform preparation.
The backend-readiness goal remains active.

## Implemented

- `ManagedAudioImport.Prepare` copies a selected WAVE into a flushed temporary
  file beneath `audio-assets`, validates/hashes the copy, and moves it to a
  content-addressed name without overwriting an existing file. Identical imports
  reuse verified contents. Cancellation and file/decoded limits apply; failed
  preparation cleans its temporary file.
- Import preparation is worker-side. `ProjectAssetCommands.Place` commits the
  detached reference and initial clip together on the control thread. Existing
  routing, source results, context and other assets are preserved. Reusing a known
  matching asset creates another clip without duplicating the reference.
- `Relink` replaces an asset reference as one undoable edit, keeping all clip
  instances, windows, placement and nudges. Shorter replacements leave missing
  portions silent through the existing compiler. Different sample rates require
  explicit conversion; relink does not reinterpret stored frame offsets.
- Managed files survive undo/redo and failed document commits. Unreferenced copies
  may therefore remain on disk. Future cleanup must account for saved projects and
  history; this slice deliberately does not delete reusable source data.
- `WaveformPyramid` prepares immutable per-channel min/max envelopes at progressively
  doubled bucket sizes. It preserves partial final buckets and does not inject zero
  into extrema. Default base is 256 frames, with a 32 MiB peak-payload budget and
  cooperative cancellation. Small object/array overhead is outside that payload
  budget. The pyramid retains no PCM and performs no callback work.

## Verification

All 13 focused import/waveform tests passed: WAVE decoding, persisted resolution,
managed deduplication, failed-import cleanup, original-file independence, relink
undo/redo with unchanged clip identity, different-rate rejection, exact waveform
extrema/partial buckets, cancellation and memory limits.
Log: `/tmp/flow-managed-assets.log`.

The affected StudioModel/PlatformAudio/MusicModel/StaticBinding/Hosting regression
run passed **239 tests**, 0 failed. Log: `/tmp/flow-managed-assets-regression.log`.
Existing analyzer warnings remain; `git diff --check` passed. No full-solution run
or Web publish was performed.

## Remaining

No automatic asset garbage collection, waveform disk cache or viewport renderer is
implemented. Project waveform preparation is an available worker API, not a UI.
External file relocation and dependency packaging remain host workflows. Next major
backend work includes sampler instruments, stateful shared effects/tails, automation,
note editing and full Flow project export/reconstruction. Native frontend and
historical callback-gap diagnosis remain deferred. No hardware capture was run.
