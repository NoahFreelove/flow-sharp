# Phase 6 — prepared playback publication, 2026-10-01

Phases 0–5 complete; Phase 6 backend in progress. Finish backend and Flow plugins
before UI/DAW work. Roadmap/progress govern; commit verified slices, do not push.

## Delivered

`QueuedSinePlayback` now supports control-thread `TryReplace(preparedSource)` and
`TryTakeRetired(out transport)`. Existing SPSC command/stop rules still apply.
Only one control thread may submit, publish and collect; one audio thread reads.
The prepared source must match the host sample rate and block-frame limit.

Successful publication transfers source ownership and constructs/reset its new
transport on the control thread. False leaves candidate ownership/cursor unchanged.
Invalid format/null or re-publishing the active source throws before mutation.
One pending and one retired slot bound host retention. A new publication is
rejected during pending stop/replacement or until retirement has been collected.

At the next nonempty boundary observing a replacement (and no pending stop):

1. Discard old queued commands; their score-relative positions cannot carry over.
2. Install the new transport stopped at zero with no loop, update total frames
   and increment Generation (initial generation is 1).
3. Release the old transport into the retired slot; audio never accesses it again.
4. Render silence and publish position/state, then clear ReplacementPending.

Ordinary commands return false while replacement is pending. Seek/loop return
false before range validation in that state; after acknowledgment validation uses
the new score. This avoids validating against one generation and applying to
another. Normal commands submitted after acknowledgment wait for the next block.
Stop has priority when both are observed; it stops the old transport, leaving the
replacement pending for a later boundary. Replacement cannot resume playback.
A stop arriving during an already-started boundary may take the next block.

`TryTakeRetired` transfers old transport ownership back to the control thread.
It can return the old object as soon as audio releases it, even before replacement
acknowledgment. No disposal or user callback runs on the audio thread. The current
managed-only sine metadata needs no Dispose; native-resource lifecycle is not
implemented. To tear down, cease/join callback use before releasing the host on
the control thread. Status properties are separate atomic observations, not a
coherent multi-field snapshot; await replacement acknowledgment before resubmitting.

## Verification

Focused music-model tests: **98/98**. All-tier gate: **3,110 main + 21 MIDI passed,
19 prerequisite skips**, no failures or tracked-file mutations. Eight new cases
include exact sample parity after replacement, smaller/empty scores, ownership and
backpressure, stop ordering, validation, 2,000 concurrent swaps with exact-once
retirement, and zero warmed installation allocation on the callback thread.
Web JavaScript adapter smoke passes unchanged.

Summaries: `docs/baselines/phase6/publication-*.json`.
Raw logs/TRX: `/tmp/flow-phase6-publication/`.

## Next ready slice

A callback-capable Linux adapter and a headless timing/underrun harness using the
prepared sine/transport/queue/publication path. Test while background Flow work,
GC and asset work run; measure callback worst-case versus the roadmap's device
budget (48 kHz / 256 frames target). Record actual device/backend availability and
limitations; synthetic callback tests alone do not establish audible device safety.
Decide managed/native strategy from measurements before broadly migrating DSP.

This publication protocol is a dry-sine proof. General DSP lifecycle, live-note
recovery, smoothing, samplers/effects, tails/crossfades and seamless graph state
transfer remain open; Phase 7 Flow plugins are not started. UI stays deferred.
Action-owned Undo/Redo remains planned in roadmap §9.3 for Phase 8.

Run suites serially with `MSBUILDDISABLENODEREUSE=1`; do not edit tracked files
while the verifier hashes them. Restore Desktop after Web publish when needed.
Preserve frozen browser JS and the website bundle.
