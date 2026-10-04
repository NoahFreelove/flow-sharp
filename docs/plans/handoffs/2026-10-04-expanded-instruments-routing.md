# Expanded instrument and routing export — 2026-10-04

P7-13 expands instrument definitions and track routing in executable project code.
The overall backend-readiness goal remains active.

## Implemented

- Sine instruments reconstruct through `dawSine`, exposing voices and fractional
  attack/release values. Samplers reconstruct through `dawSampler`, exposing root
  frequency, voices and envelopes; immutable PCM remains resource data accessed
  through `dawProjectSample` without file IO or source execution.
- Seed instrument definitions use default settings with the same sample payload.
  `dawProjectInstrument` installs explicit reconstructed definitions into stable
  source/output slots while retaining historical code/context and binding IDs.
- `DawTrack` / `DawTracks`, `dawTrack` and `dawRouting` expose named track order,
  optional instrument bindings and the selected output graph. Empty strings denote
  absent bindings; an overload handles an empty track list. Routing uses the same
  validated project model as the host.
- Export uses separate variable names for each intermediate resource state.
  Generated source code remains historical data; reconstruction does not infer or
  rewrite the generator that originally produced an instrument.

## Verification

All nine focused project-export/sampler tests passed, including exact serialized
state after execution, independently bound sine/sampler tracks, fractional envelope
values and exact sampler PCM. Log: `/tmp/flow-instrument-export.log`.

The affected StudioModel/PlatformAudio/MusicModel/StaticBinding/Hosting regression
run passed **268 tests**, 0 failed. Log: `/tmp/flow-instrument-export-regression.log`.
Existing analyzer warnings remain; `git diff --check` passed. No full-solution run
or Web publish was performed.

## Remaining

Expanded score/note construction, source metadata/tempo construction, portable
asset packaging and dependency pins remain open. Audio sample bytes intentionally
remain data rather than thousands of numeric code statements. Large-resource
qualification and final live/offline/export qualification are still required.
Native frontend and historical callback-gap diagnosis remain deferred; no hardware
capture was performed.
