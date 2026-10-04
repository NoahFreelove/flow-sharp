# Live voice-pool control mailbox — 2026-10-04

P7-29 implements the audio-layer mechanism for coherent live graph instrument
controls. Project/plugin gesture wiring remains open; plugin metadata still
requires rebuild for instrument controls until that wiring is complete.

## Implemented

PreparedNotePlayback.SetLatestParameters validates 1–4,096 graph targets on the
single control producer. A failed batch changes neither pending state nor accepted
targets. Latest complete target snapshots coalesce through one atomic mailbox;
changes to different targets cannot discard a pending restoration. No prepared
voice graph is individually published from the control thread.

At the next nonempty Read (or Seek), the audio owner atomically consumes one
snapshot and applies it to every prepared voice before rendering. Target validation
and command-index compilation occur on the producer. Application is bounded loops
without allocation, locks or producer retries on the audio owner. Identical graph
instances share compiled node/parameter indices. Unchanged targets do not restart
an in-flight ramp when another control changes.

Active voices smooth with catalog timing. Inactive voices retain target values;
starting, stealing, seeking or resetting snaps those targets before resetting DSP,
so new voices inherit current controls. Empty Read does not consume a pending
update. Legacy sine/sampler kernels reject graph-control calls. Structural and
automation-owned targets retain existing validation restrictions.

## Evidence

Focused voice tests passed before the concurrency addition:
`/tmp/flow-voice-live-controls.log` (five tests). New coverage checks whole-pool
updates, invalid duplicate batch preserving the prior update, allocation-free
consumption, voice stealing, held seek, reset and latest-value coalescing.

The concurrency test repeatedly publishes 10,000 paired targets while audio reads
a graph whose branches cancel exactly only when values agree. It checks that no
partially applied batch reaches rendered output across two independent voices.

All **377** affected backend/module tests passed, including concurrency, with
zero failures (`/tmp/flow-live-voice-regression.log`). `git diff --check` passed.
Full core/Web and hardware gates were not repeated for this slice.

## Next

Expose prepared instrument instances through arrangement/project playback, route
public plugin controls to the correct voice pool(s), and extend begin/update/cancel/
commit gestures without losing restoration or publishing across retired graphs.
Only then permit live instrument parameter metadata. Continue sampler graph
primitives, public automation/device instances/presets, MIDI input/basic recording
and integrated workflows. Native frontend/historical callback diagnosis remain
deferred; physical performance is not qualified by mailbox unit tests.
