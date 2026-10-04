# Prepared graph voice pool — 2026-10-04

P7-23 connects prepared graphs to the existing bounded note scheduler. This is the
playback kernel; saved instrument definitions and the public Flow instrument API
still need integration before users can select these instruments in projects.

## Implemented

`SineVoiceSettings.VoiceGraph` selects an immutable graph instead of the legacy
sine/sampler kernel. Sample and graph selection are mutually exclusive. Each voice
owns an independently prepared graph; no interpreter or file access runs in Read.
Input buses are fixed: 0 = tuned frequency in Hz, 1 = gate, 2 = velocity. Each value
is broadcast to stereo. Graphs may omit unused buses. Higher bus indices reject.
Velocity is not automatically multiplied into the output: the graph decides its
response. Placement gain and stereo-preserving pan apply after the graph.

Every note-on resets the chosen voice graph, including deterministic oldest-voice
stealing. Gate drops on the scheduled note-off frame. The voice remains active for
the prepared graph's bounded tail, then retires even if a graph ignores gate and
continues sounding. Legacy attack/release settings do not add a second envelope
to graph voices. `TailSeconds` remains a legacy sine/sampler helper and explicitly
rejects graph settings; prepared playback duration includes the actual graph tail.

Seek clears all graph state and reconstructs held notes with remaining duration;
released tails are not reconstructed. Pause preserves state through the existing
transport. Pool preparation caps aggregate node count at 4,096 and graph buffers
at 64 MiB, in addition to existing note, onset and polyphony budgets.

Processing currently evaluates each active graph one frame at a time, preserving
sample-accurate event edges and deterministic output across caller block sizes.
This is functionally bounded and allocation-free but has not been qualified for
large polyphony on physical hardware; segment/block optimization remains needed
before claiming efficient high-polyphony custom graphs.

## Verification

Three focused tests passed (`/tmp/flow-graph-voices.log`): overlapping independent
envelopes/velocities, bounded note-off tails, allocation-free Read, oscillator
pitch input, deterministic steal/reset/held seek, block partition equivalence and
invalid-port/aggregate-node budget rejection.

All **366** affected backend/module regressions passed, zero failures
(`/tmp/flow-graph-voice-regression.log`). `git diff --check` passed. Full core, Web
and physical audio gates were not repeated for this slice.

## Next

Add versioned graph instrument definitions to project persistence, compilation,
generated results and executable Flow construction/export, with synth examples.
Then optimize event-delimited graph blocks and test representative polyphony.
Sampler graph primitives, instrument plugin packages, public automation/device
instances/presets, asset capabilities, MIDI input/recording and integrated workflows
remain open. Native JUI frontend and historical callback-gap diagnosis are deferred.
