# Captured project sample example — 2026-10-04

`examples/instruments/project-sample.flow` demonstrates a granted project audio
asset used by `dawSampler`, with named note, instrument and mixer outputs. Its
adjacent README documents asset selection, source grants, routing, root frequency,
one-shot limitations and the distinction from legacy implicit sample lookup.

`GrantedSampleExampleBuildsRoutesAndReopensWithIdenticalPlayback` executes the
checked-in Flow file through the real project generator host and isolated worker.
It imports a WAV, captures the grant, routes the outputs, saves/reopens the project
and compares every block of prepared playback, also checking audible output.
Existing adjacent tests cover unavailable/changed assets, saved grant reuse,
revocation, stale acceptance, direct/isolated PCM and graph-instrument examples.

Verification: **13 passed**, zero failures/skips,
`/tmp/flow-sample-example-final.log` (Release). The first sandboxed runner could
not open its local IPC socket; the authorized rerun completed successfully.
The example is a backend host workflow, not a new interactive frontend.

Next required work: consolidate current-state documentation and readiness matrix,
refresh browser/WASM and full-core gates, and perform the final requirements audit.
No claim of backend completion or hardware qualification is made by this checkpoint.
