# Batched Flow arrangements and clip operations — 2026-10-04

P7-11 improves the executable project bridge and exposes shared clip transforms.
The overall backend-readiness goal remains active.

## Implemented

- `DawClip` / `DawClips` hold immutable score or audio clip definitions.
  `dawScoreClip` and `dawAudioClip` construct them using the same validated types as
  the DAW. Audio frame fields retain exact decimal-string Int64 representation.
- `dawArrange` replaces the complete arrangement clip list in one operation while
  preserving project resources, context, routing and automation. It validates the
  clip count and unique identities as one complete arrangement.
- Project export emits detached clip declarations followed by one `dawArrange`
  call, eliminating the previous repeated project/arrangement rebuild per clip.
  Legacy `dawPlaceScore` / `dawPlaceAudio` remain available for small direct edits.
- Flow `dawRelativeOffsetMs`, `dawAlignToBar`, `dawSplitScore` and `dawSplitAudio`
  call the existing shared clip kernels. Splits return the two clip values with
  explicit right-hand identity. Audio split uses a local source-frame cut and the
  project's full tempo map. Alignment clears the nudge as in the DAW API.

## Verification

The initial export tests passed, including execution/restoration of a 1,024-clip
project through one batch call, exact project/audio round-trip and full-width
frame counts. The 1,024-clip check verifies contents and emitted structure rather
than relying on a fragile timing threshold. Log: `/tmp/flow-batch-arrangement.log`.

The final affected StudioModel/PlatformAudio/MusicModel/StaticBinding/Hosting run
passed **266 tests**, 0 failed, including Flow score/audio splits and alignment/offset
parity with the host kernels. Log: `/tmp/flow-batch-arrangement-regression.log`.
Existing analyzer warnings remain; `git diff --check` passed. No full-solution run
or Web publish was performed.

## Remaining

Project resources still use the versioned snapshot restoration payload. Fully
expanded note/device/routing construction and portable asset packaging remain
required for the final authoring export. The 100,000-clip maximum has not been
qualified through Flow evaluation; this slice verifies 1,024 clips. Native frontend
and historical callback-gap diagnosis remain deferred; no hardware capture occurred.
