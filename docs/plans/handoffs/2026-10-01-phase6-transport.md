# Phase 6 — frame transport, 2026-10-01

Phases 0–5 are complete. Phase 6 backend work is in progress. Finish backend and
Flow plugins before UI/DAW work, per owner direction. Use the roadmap and progress
ledger directly; commit verified slices without pushing.

## Delivered

`Flow.Audio.PreparedSineTransport` owns a `PreparedSinePlayback` cursor exclusively.
Construction resets it to zero, stopped. The host must serialize every operation;
there is no cross-thread command path yet. Sample rate/block limit are inherited.

- Play/resume preserves position; at EOF without a loop it stays stopped.
- Pause holds the cursor and outputs silence; pausing a stopped transport is a no-op.
- Stop rewinds to zero and retains loop settings. Seek preserves transport state.
- `SetLoop(start, end)` enables a nonempty half-open range within the score.
  Configuration does not move the cursor. Frames before the start are a lead-in;
  positions at/past the end wrap on the next nonempty playing read.
- `ClearLoop()` disables looping without changing state, position or stored range.
- `Read(Span<float>)` fills host-owned stereo memory and returns score frames
  rendered. Loop wraps happen within blocks and immediately at exact block ends.
  Exact/partial EOF stops at the end; unused output is zero. Empty reads do
  nothing, including after seeking to EOF; the next nonempty read resolves EOF.
- Invalid buffers, seeks and loop ranges leave state/output unchanged. Valid
  operations allocate no managed memory. No timer, IO, lock or interpreter work.

The prepared renderer remains a dry-sine prototype with source-order voice scans.
Loop wraps are exact seeks, without crossfade or effect tails. No generic node
lifecycle, device deadlines or cross-thread safety are claimed.

## Verification

Full all-tier gate: **3,093 main + 21 MIDI passed, 19 prerequisite skips**, zero
failures and zero tracked-content mutations. Fourteen transport cases include
reference-sample parity, state transitions, loop/EOF boundaries, one-frame loops,
64-bit positions, invalid calls and zero warmed allocation. The initial focused
music-model run passed 80 cases before the final long-position case was added;
the full gate includes that case. Web JavaScript adapter smoke passes unchanged.

Durable summaries: `docs/baselines/phase6/transport-*.json`.
Raw logs/TRX: `/tmp/flow-phase6-transport/`.

## Next ready slice

Add bounded host commands with explicit overflow behavior (protect stop/note-off),
then prepared graph publication and off-thread retirement. The audio thread owns
transport mutation; UI/control threads must not call this instance concurrently.
Follow with a callback-capable Linux adapter and measurements under Flow/GC load.
Choose managed/native strategy before broad DSP porting. General processors,
parameters/smoothing, samplers/effects and Phase 7 Flow graphs remain open.

UI stays deferred. The action-owned Undo/Redo contract is recorded in roadmap
§9.3 for the later UI-independent project model.

Run full suites serially with `MSBUILDDISABLENODEREUSE=1`, and do not edit tracked
files during verification. Restore `FlowTarget=Desktop` after Web publish before
Desktop builds/tests. Preserve frozen browser JS and the website bundle.
