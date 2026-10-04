# Event-delimited graph voice rendering — 2026-10-04

P7-25 replaces per-sample graph dispatch with prepared block rendering between
note events, while retaining the existing note/voice semantics.

## Implemented

The graph voice path divides each requested block at the next onset, active
note-off or release retirement. Gates remain constant within each segment. Each
active voice processes the segment through its own graph; voices accumulate into
the output in the same slot order as before. Pitch and velocity are broadcast into
contiguous stereo input buses. Zero-input graphs are supported. Empty blocks/EOF
remain zero-filled without advancing state.

Shared scratch buffers are allocated at preparation and reused sequentially by
voices. Each graph prepares for the requested maximum block size. The existing
64 MiB aggregate graph-buffer budget now includes these scratch buffers. The
4,096 aggregate-node budget and bounded note/onset/polyphony limits remain.
No interpreter, allocations, file access or graph creation occur during Read.

## Evidence

Four graph voice tests passed (`/tmp/flow-voice-blocks.log`). The new regression
compares randomized block lengths against a one-frame reference for 250 overlapping
notes with eight voices, varied tuning/velocity/gain/pan, ADSR and delay, stealing,
release tails and multiple held-note seeks. Every stereo sample and steal count
matches exactly. Allocation-free Read remains verified.

A local offline smoke benchmark rendered 64 sustained graph voices for 16,800
frames at 48 kHz. After warmup, one-frame dispatch took **130.425 ms**, 256-frame
blocks **48.348 ms**, with identical accumulated sample checksum. Harness/log:
`/tmp/flow-voice-benchmark/Program.cs`, `/tmp/flow-voice-benchmark.log`. This single
run indicates reduced dispatch overhead; it is not a statistical benchmark or
physical callback deadline/underrun qualification. The timing harness's first-run
allocation counter includes formatting initialization; allocation assertions live
in the focused tests rather than this timing printout.

All **369** affected backend/module regressions passed with zero failures
(`/tmp/flow-voice-block-regression.log`). `git diff --check` passed. Full core, Web
and physical gates were not repeated in this slice.

## Next

Continue richer synth/sampler primitives and instrument plugin contracts, public
parameter automation/device instances/presets, asset capabilities, MIDI input and
recording, and integrated workflow checks. Full backend readiness remains open.
Native JUI frontend and historical callback-gap diagnosis remain deferred.
