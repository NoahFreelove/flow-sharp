# Audio clip playback — 2026-10-04

P6-17 / P7-05 adds decoded audio source windows to the arrangement playback path.
The previous turn implemented note windows; this turn adds first-class immutable
PCM assets, scheduling/mixing and a Flow value for rendered audio. The full backend
readiness goal remains active.

## Implemented

- `PcmAsset` owns a copied, finite, stereo float buffer and sample-rate metadata.
  Defaults limit an individual decoded asset to 256 MiB. Multiple clips share one
  immutable asset; file decoding remains outside the callback.
- `ScheduledAudioClip` retains clip identity, source-frame offset/length and project
  start/end frames. Moving a musical anchor does not stretch its sample duration.
- `PreparedPcmPlayback` supports overlap, silence outside shortened source data,
  negative placement, seek and exact same-rate sample copying. A bounded active
  set and interval tree avoid scanning every project clip in callback Read/Seek.
- Different sample rates use explicit linear interpolation, with zero outside the
  visible source window. This is the functional conversion policy for this slice,
  not a claim of band-limited/high-quality export resampling. Preparation reports
  `linear-rate-conversion` for affected clips.
- `PreparedMixedPlayback` combines independent note and audio cursors on a track
  bus before the shared graph. The existing graph transport/publication/callback
  path remains the playback boundary.
- `ArrangementCompiler` now lowers both score and audio clips. It validates saved
  sample-rate metadata against resolved assets, retains missing-asset duration as
  silence, reports missing/shortened assets, and enforces a default 512 MiB budget
  over distinct referenced decoded assets. This check does not itself bound memory
  a caller allocated before passing assets to the compiler.
- Audio start placement is rounded once to the nearest output frame, midpoint away
  from zero. Duration is calculated independently, using the exact source frame
  count at matching sample rates. No duration drift is introduced by separately
  rounding an absolute end timestamp.
- Flow `DawAudio` and `(dawAudio buffer)` convert an already processed stereo buffer
  into the same immutable PCM asset used by the host. Non-stereo buffers require
  explicit channel conversion rather than an implicit downmix.

## Verification

The affected StudioModel/PlatformAudio/MusicModel/StaticBinding run passed **190
 tests**, 0 failed. After the final placement/duration rounding edits, score/audio
arrangement tests passed **17 tests**, then the exact-integer same-rate path passed
all **8 audio-clip tests**. Coverage includes input ownership, exact source windows,
negative crop, shortened/missing assets, defined interpolation/window boundaries,
split continuity across a tempo change, overlap limits, allocation-free Read/Seek,
Flow asset creation and mixed note/audio playback.

Logs: `/tmp/flow-audio-clips-regression.log`, `/tmp/flow-audio-clips-final.log`,
`/tmp/flow-audio-clips-exact.log`. Existing package/analyzer warnings remain. No
physical device capture/listening, full solution suite or Web publish was run.

## Required follow-up

Extend structured generator results and their worker protocol to carry audio and
graph/instrument definitions; the current composition-generator result still
accepts score layers only. Add asset identity/hash/path persistence, bounded
file import/decoding and waveform access, and a band-limited conversion path before
claiming final export-quality rate conversion. Sampling an asset from note events
is separate from the fixed-duration audio clip implementation.

Continue source-result acceptance and binding reconciliation, full project
persistence, sampler/additional effects with bounded tails, automation/modulation,
and end-to-end editing/export qualification. Keep the native frontend and the
historical callback-gap investigation deferred. This is not a completion claim for
Phases 6–8 or the overall frontend-readiness goal.
