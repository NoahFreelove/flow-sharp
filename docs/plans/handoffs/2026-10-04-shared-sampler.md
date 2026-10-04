# Shared sampler instrument — 2026-10-04

P6-19 / P7-08 adds a bounded one-shot sampler to the shared note playback path.
The backend-readiness goal remains active.

## Implemented

- `flow.sampler@1` uses immutable stereo PCM, explicit root frequency, voice count,
  attack and release. Flow `dawSampler audio rootHz voices attack release` returns
  a `DawInstrument` through the same generator result and binding paths as sine.
- Tuned note frequency determines sample advancement relative to root pitch and
  source/output rates. Linear interpolation supplies fractional positions, with
  zero outside the sample. Playback stops naturally at sample exhaustion or after
  the note's envelope release. There is no sample looping or zone selection yet.
- The existing bounded voice pool, onset budget, oldest-voice stealing and held-note
  seek tree are shared. Seeking retriggers held samples from their beginning;
  expired releases are cleared. Naturally finished samples free their voice before
  the next onset. Valid Read/Seek/Reset remain allocation-free.
- Center pan preserves the sample's stereo image; moving pan attenuates the opposite
  side using capped constant-power gains. Note velocity/gain scale amplitude.
  Unlike sine's built-in oscillator scaling, samples retain their authored level.
- The historical public `SineVoiceSettings` name is retained for source compatibility;
  optional immutable sample/root fields select the sampler kernel. Both instruments
  still share its envelope/voice settings. No arbitrary DSP executes on callback.
- Generated-content schema version 2 carries sampler PCM and root frequency;
  version 1 remains readable. Worker protocol remains version 2. Inline sampler
  audio counts toward the combined 16 MiB result budget, including repeated content.
  Project saving/reopening and stable instrument bindings use the same codec.
- Arrangement preparation counts referenced sampler PCM in its decoded asset budget
  and reports linear interpolation. Generated sampler payloads are inline snapshots,
  not external file references; large sampler transfer remains future work.

## Verification

Initial sampler/mixed-generator tests passed **7 tests**, including pitch ratios,
stereo preservation, zero outside sample, seek retriggering, allocation-free callback,
real isolated Flow generation and exact playback after project round-trip.
Log: `/tmp/flow-sampler-tests.log`.

The final affected StudioModel/PlatformAudio/MusicModel/StaticBinding/Hosting
regression run passed **247 tests**, 0 failed, including natural sample completion
without false voice stealing. Log: `/tmp/flow-sampler-regression.log`. Existing
analyzer warnings remain; `git diff --check` passed. No full-solution run or Web
publish was performed.

## Remaining

Band-limited pitch/rate conversion, looping/multisample zones, external sampler asset
references, user-authored instrument DSP, automation/modulation and additional
shared effects remain open. Full project export and editor note operations still
need implementation. No hardware qualification or native frontend work was performed;
historical callback-gap diagnosis remains deferred.
