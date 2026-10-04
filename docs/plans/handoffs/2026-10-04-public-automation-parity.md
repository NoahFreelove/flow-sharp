# Public automation end-to-end parity — 2026-10-04

Verification checkpoint extending P7-33 across effects, tempo changes and executable
project export. No new runtime behavior is claimed in this checkpoint.

## Coverage added

A declared effect maps one public gain parameter to two series gain nodes. A
constant PCM clip feeds it while its public curve rises from 0 to 1 over four
quarters; tempo changes from 120 to 60 BPM at quarter two. Rendered checkpoints
verify both targets follow musical time: amplitude .25 at one second, .5625 at
two seconds and 1 at three seconds. Whole-project Flow export reconstructs the
same audio sample-for-sample. Seeking into the curve matches the corresponding
continuous-render slice.

The test rejects a manual public-value edit owned by automation and rejects a
second raw-node lane targeting an already-expanded public target. This verifies
that public mapping does not bypass existing target ownership checks.

The instrument gesture/automation integration test now also exports the saved
automated project as executable Flow and verifies identical rendered samples after
reconstruction, in addition to its existing save/reopen checks.

## Evidence

The focused multi-target effect test passed (`/tmp/flow-public-effect-automation.log`).
Its first compile used the wrong ProjectDocument.Accept overload; required action
arguments were supplied before running it. Runtime assertions passed unchanged.
These deterministic checks do not measure physical devices or callback deadlines.

All **382** affected backend/module tests passed with zero failures
(`/tmp/flow-public-automation-parity-regression.log`). `git diff --check` passed.

## Next

Continue sampler graph primitives and reusable sampler/drum examples, then MIDI
input/basic recording, device instances/presets, asset capabilities and integrated
workflow readiness. Full core/Web refresh remains due after related changes.
Native JUI frontend and historical callback-gap diagnosis remain deferred.
