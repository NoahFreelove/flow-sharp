# Prepared sample graph kernel — 2026-10-04

P7-34 adds an audio graph sample primitive. Instrument asset persistence and public
Flow authoring/export are still required before this becomes a usable saved sampler.

## Implemented

`flow.sample` has two stereo inputs: pitch Hz and gate. Structural parameters select
an integer asset slot 0–255 and root frequency 1–100,000 Hz. AudioGraphDefinition.Sample
constructs it. PreparedAudioGraph receives an explicit immutable PCM asset map;
missing/empty assets reject before playback. It retains asset references rather
than copying PCM per voice. Unique referenced PCM is capped at 64 MiB per prepared
graph. Slot identity is serialized in existing graph JSON; PCM is not embedded there.

Each stereo channel independently starts at sample frame zero on a finite positive
gate's rising edge. A held gate does not loop; a subsequent rising edge retriggers.
Gate-off lets the one-shot reader continue. Pitch advances source position by
frequency/rootHz * sourceRate/outputRate, using linear interpolation and zero beyond
the sample boundary. Finite pitch clamps to 0–100,000 Hz; negative/nonfinite pitch
becomes zero and freezes source position. Reset clears gate/playhead/start state.
The primitive does not apply velocity or an amplitude envelope; those are ordinary
graph connections. It adds no independent tail: note lifetime/release is supplied
by the surrounding instrument envelope, just as with oscillator nodes. Without an
envelope tail, voice retirement at note-off cuts the reader.

There is no reverse playback, looping, zones, streaming or bandlimited resampling.
Graph export explicitly rejects sample nodes until asset-binding construction is
available, rather than emitting incorrect unary-device code. Existing sine/sampler
and graph instrument paths remain unchanged; unbound sample instruments reject.

## Verification

Two focused tests passed (`/tmp/flow-sample-graph.log`): exact stereo half-speed
interpolation, sign-preserving channels, held-gate completion, rising-edge retrigger,
reset silence, callback allocation, missing assets, integer slot validation and
graph identity serialization. No physical audio qualification is claimed.

All **384** affected backend/module tests passed with zero failures
(`/tmp/flow-sample-graph-regression.log`). `git diff --check` passed. Full core/Web
gates were not repeated for this slice.

## Next — complete this sampler feature

Bind immutable assets into graph instrument settings, persist them with generated
contents, count them in project resource budgets, and expose Flow construction and
executable export. Add reusable sampler/drum examples through the isolated worker
and project compiler. Continue recording/device workflow work; native JUI frontend
and historical callback-gap diagnosis remain deferred.
