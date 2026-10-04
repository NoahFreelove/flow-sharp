# Gate-driven envelopes — 2026-10-04

P7-22 adds the envelope primitive needed by prepared Flow instruments. This does
not yet connect custom graphs to the note voice pool or complete backend readiness.

## Implemented

`(dawAdsr id gate attack decay sustain release)` constructs `flow.adsr`. Durations
are milliseconds (0–10,000); sustain is linear amplitude (0–1). All four settings
require graph preparation in this version. Each stereo channel independently
interprets a finite positive input as gate-on; zero, negative and nonfinite input
are gate-off. The output is an envelope, intended to multiply an audio signal.

A rising edge attacks from the current level toward one; decay then reaches
sustain. A falling edge releases from the current level to zero, including during
attack or decay. Retrigger during release starts from the current level. Nonzero
stages use ceil(milliseconds * sampleRate / 1000) frames and reach their target on
frame N. Zero-time stages collapse immediately; zero attack and decay therefore
emit sustain immediately. Nonzero attack emits its peak before a zero decay moves
to sustain on the following frame.

Reset/seek clears both channel envelopes. Release contributes its actual prepared
frame length to the graph tail budget. That budget assumes the upstream gate goes
low; a constant positive source intentionally sustains indefinitely while processed.
Construction, JSON and executable generic device export share the same catalog.

## Verification

Four focused tests passed (`/tmp/flow-envelope-tests.log`): exact stage targets,
partial-stage release/retrigger, zero durations, independent stereo gates, invalid
gates, reset, partition equivalence, zero callback allocation, release tail and
public Flow construction/export parity. Module snapshots deliberately add exactly
one signature: native 693→694, reachable 687→688, with the same six legacy
unreachable signatures (`/tmp/flow-envelope-snapshots.log`).

All **363** affected backend/module regressions passed with zero failures
(`/tmp/flow-envelope-regression.log`). `git diff --check` passed. Full core, Web
and physical audio gates were not repeated for this slice.

## Next

Prepare per-voice graph instruments with pitch, gate and velocity inputs, bounded
voice ownership and release retirement. Add public Flow synth/sampler examples.
Continue public automation/device instances/presets, asset capabilities, MIDI
input/recording and integrated workflows. Native JUI frontend and historical
callback-gap diagnosis remain deferred. The JUI package smoke evidence is in the
[JUI handoff](2026-10-04-jui-package.md); no native window verification is implied.
