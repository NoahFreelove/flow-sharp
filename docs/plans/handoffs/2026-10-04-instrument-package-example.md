# Saved instrument package and committed controls — 2026-10-04

P7-28 supplies a reusable instrument plugin example and explicit host control commits.

## Implemented

`examples/plugins/subtractive-instrument.flow` builds a 16-voice saw instrument
with amplitude ADSR, envelope-controlled one-pole cutoff and velocity response.
Its adjacent `.flowplugin` pins the exact source/hash and declares public level,
cutoff and release controls with stable IDs. All controls currently require
preparation. The separate generator example still includes a score/mixer; the
plugin returns only its declared instrument output.

ProjectPlaybackSession.SetPluginParameter and ResetPluginParameter commit one
public control action and request playback preparation. They return false for
unchanged overrides/already-reset controls and reject invalid values before document
mutation. The host calls these once per committed gesture. Existing preparation
coalescing and last-good playback behavior apply. This does not implement live
instrument knob previews or parameter automation.

## Verification

The new saved-example integration test uses the actual plugin host/isolated worker
and checks source/package byte agreement, acceptance, polyphonic audio, public
level scaling, changed cutoff/release values, project save/reopen, executable Flow
project export and rebuilding the same plugin ID with retained overrides. Audio
samples match exactly across persistence/export/rebuild; halving the public level
halves the output within float tolerance. The test also exercises host control
set/reset, no-op returns and invalid-value document preservation.

The first example parity run passed (`/tmp/flow-instrument-example-parity.log`).
Current full core/Web evidence remains the earlier instrument compatibility
checkpoint; no physical audio or frontend qualification is claimed.

All **375** affected backend/module regressions passed, zero failures
(`/tmp/flow-instrument-controls-regression.log`). `git diff --check` passed.

## Next

Implement coherent live instrument controls and host gestures, then sampler graph
primitives and further device/automation workflows. MIDI input/basic recording,
asset capabilities and integrated DAW workflow readiness remain open. Native JUI
frontend and historical callback-gap diagnosis remain deferred.
