# Phase 6 — bounded host commands, 2026-10-01

Phases 0–5 are complete; Phase 6 backend work is in progress. Finish backend and
Flow plugins before UI/DAW work. Use roadmap/progress directly; commit verified
slices without pushing.

## Delivered

`Flow.Audio.QueuedSinePlayback(source, capacity = 256)` takes exclusive ownership
of a prepared sine source and constructs its transport off-thread. Exactly one
control thread calls `TryPlay`, `TryPause`, `TrySeek`, `TrySetLoop`, `TryClearLoop`
and `RequestStop`. Exactly one audio thread calls `Read`. Multiple UI/worker
producers must be serialized outside this object. Do not access the source again.

The preallocated FIFO has the requested capacity (1–65,536) plus a spare slot.
Release/acquire cursor accesses publish commands and protect slot reuse. Commands
are applied in FIFO order from a tail snapshot taken at the block boundary,
limiting each callback to at most Capacity commands even if the producer refills.
Validation occurs on the control thread against the fixed score. Ordinary full
or stop-pending queues reject a command with false; no accepted entry is replaced.

`RequestStop` cannot be blocked by FIFO saturation. Repeated requests coalesce in
a separate flag. At a boundary observing it, the consumer discards queued commands,
stops/rewinds and acknowledges by clearing `StopPending`. Normal submissions are
rejected while pending; after acknowledgment fresh commands wait for a subsequent
block. Stop during an already-started block may take the next block. This is
block-boundary control, not sample-offset event scheduling.

Read validates output before consuming anything; empty reads do not drain or
acknowledge. All valid operations allocate nothing and take no locks. Published
State and PositionFrames are independent atomic observations, not a coherent
multi-field snapshot. RejectedCommands counts full/pending-stop rejections;
DiscardedCommands counts accepted commands superseded by stop. Polling emits no IO.

This remains the dry-sine prototype. No live notes, note-off recovery, parameter
coalescing, prepared graph swapping or device timing certification is delivered.

## Verification

Focused music-model tests: **90/90 passed**. Full all-tier gate: **3,102 main +
21 MIDI passed, 19 prerequisite skips**, zero failures or tracked-file mutations.
Nine queue cases include sample parity, saturation/wraparound, stop recovery,
validation, concurrent FIFO/stop stress and zero warmed operation allocation.
Generated Web adapter smoke also passes unchanged.

Summaries: `docs/baselines/phase6/queue-*.json`.
Raw logs/TRX: `/tmp/flow-phase6-queue/`.

## Next ready slice

Prepared graph publication at block boundaries and off-thread retirement. Define
ownership, bounded pending/retired storage, replacement/backpressure and failure
behavior before wiring it in. Keep old resources alive until audio releases them.
Queued score-relative commands are validated against the current fixed source:
replacement needs an explicit generation/stale-command policy, especially for
seek/loop ranges when score length changes. Preserve stop precedence.

Then build a callback-capable Linux adapter and measure deadlines/underruns under
Flow/GC load before selecting managed/native strategy or broadly porting DSP.
UI stays deferred; action-owned Undo/Redo is recorded in roadmap §9.3 for Phase 8.

Run suites serially with `MSBUILDDISABLENODEREUSE=1`; do not edit tracked files
while the verifier hashes them. Restore Desktop after Web publish when necessary.
Preserve frozen browser JS and do not refresh the website bundle incidentally.
