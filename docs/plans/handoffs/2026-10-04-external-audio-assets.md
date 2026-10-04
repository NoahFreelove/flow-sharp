# External audio assets — 2026-10-04

P8-04 adds bounded WAVE import and external audio references to project playback.
The full backend-readiness goal remains active.

## Implemented

- Worker-side RIFF/WAVE decoder supports PCM 8/16/24/32-bit and IEEE float32,
  mono/stereo, rates through 384 kHz. Mono is duplicated into stereo. It validates
  chunk extents, alignment, format consistency and finite samples, handles odd-byte
  chunk padding, limits chunk traversal and checks cancellation between blocks.
- Decoded output defaults to 256 MiB per asset. Temporary decoding and immutable
  ownership copies mean peak memory exceeds that retained-output limit. File
  hashing is streamed with a default 512 MiB encoded-file cap.
- `AudioAssetReference` stores stable identity, normalized project-relative path,
  SHA-256, sample rate and frame count. Inspect and resolve read on the preparation
  worker, never on the audio callback. Hash/format mismatch requires explicit relink.
  Path validation rejects absolute and parent-traversal paths; it is not an OS
  sandbox or a symlink-containment guarantee.
- Project format version 2 persists external references. Version 1 migrates to an
  empty external asset list. Generated inline audio remains supported. Asset IDs
  cannot collide with generated output binding IDs; source acceptance retains assets.
- `ProjectAudioAssets.Resolve` supplies decoded assets and diagnostics, with a
  default 512 MiB aggregate retained-audio budget. Missing/unreadable/changed files
  remain absent. Project compilation then retains their clip windows as silence
  with missing-asset diagnostics. Existing explicit rate-conversion policy applies.
- Imported audio registration/clip placement can commit through the existing
  project action interface and undo/redo. The caller copies/locates audio inside
  the project directory before inspection; automatic managed copying is not yet
  supplied. File decoding is eager, not disk-streamed playback.

## Verification

The initial seven importer tests passed, covering integer widths, stereo float,
mono duplication, malformed/truncated frames, nonfinite values, decoded budget,
cancellation, content change/missing files and path validation.
Log: `/tmp/flow-audio-import.log`.

The final affected StudioModel/PlatformAudio/MusicModel/StaticBinding/Hosting run
passed **235 tests**, 0 failed, including persisted external-asset playback, missing
file silence, import undo/redo and version-1 migration. Log:
`/tmp/flow-audio-import-regression.log`. Existing analyzer warnings remain.
`git diff --check` passed; no full-solution run or Web publish was performed.

## Remaining

Managed copying/relink UI commands, lossless/compressed import formats, waveform
pyramids, high-quality resampling, note-driven sampler and generated large-asset
transfer remain open. Current WAVE import deliberately rejects WAVE extensible,
compressed and multichannel encodings. Add source/binding repair operations,
stateful processors, automation and end-to-end project Flow export. Native frontend
and historical callback-gap diagnosis remain deferred; no hardware test was run.
