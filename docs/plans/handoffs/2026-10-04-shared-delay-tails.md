# Shared delay and graph tails — 2026-10-04

P6-18 / P7-07 introduces the first shared stateful effect and graph-tail playback.
The full backend-readiness goal remains active.

## Implemented

- `flow.delay@1` is a finite stereo echo train. Parameters are delay time 1–2000 ms,
  repeat count 1–16, feedback multiplier 0–0.95 and wet mix 0–1. Echo k has amplitude
  feedback^(k-1). Wet/dry uses a linear crossfade. The explicit repeat count bounds
  callback cost and tail length; this is not an unbounded recursive feedback delay.
- Delay time rounds to the nearest frame, midpoint away from zero, minimum one
  frame. Time/repeat changes require graph preparation; wet/feedback use the shared
  five-millisecond ramps. Live feedback changes reweight stored input echoes.
- Prepared delay rings are included in the existing 64 MiB graph buffer budget.
  Graph tail length follows the longest connected input path, adds serial tails,
  ignores bypassed delay state, and is capped at 120 seconds. Each delay's maximum
  declared tail is 32 seconds. Scheduling latency remains zero.
- `PreparedGraphPlayback` includes the graph tail after source exhaustion, feeding
  zero input so echoes drain. Source cursors keep their own durations. Seek/stop/
  loop reset clears delay rings and transient state; pause preserves state by not
  processing. Bypassed delay passes its input and allocates no delay ring.
- Flow `dawDelay id input time repeats feedback wet` authors the same versioned
  graph device. Generic device authoring/export also supports it. `dawProcess`
  now returns source frames plus the graph tail under its existing output budget.
  Playback and Flow preview execute the same kernel, including tail samples.

## Verification

The focused delay/audio-graph/graph-playback run passed **16 tests**, including
known impulse echo amplitudes, EOF clearing, seek/reset tail clearing, serial-tail
length, bypass, buffer limits, invalid repeat counts, immutable structural controls,
block-size equivalence, allocation-free Read/Reset, Flow preview parity and graph
export/reconstruction. Log: `/tmp/flow-delay-tests-final.log`.

The affected StudioModel/PlatformAudio/MusicModel/StaticBinding/Hosting regression
run passed **243 tests**, 0 failed. Log: `/tmp/flow-delay-regression.log`. Existing
analyzer warnings remain. `git diff --check` passed; no full-solution run or Web
publish was performed.

## Remaining

This establishes finite stateful processing, not all final effect/plugin lifecycle
requirements. Reverb/filter ports, sampler, sample-accurate automation/modulation,
latency compensation and bounded retirement tails across graph replacement remain
open. Replacement currently uses existing whole-playback publication semantics;
old graph tails are not mixed into the new graph. Additional qualification under
simultaneous generation/edit/playback load is still required. Native frontend and
historical callback-gap diagnosis remain deferred; no hardware capture was run.
