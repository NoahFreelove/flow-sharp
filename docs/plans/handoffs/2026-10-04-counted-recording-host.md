# Counted MIDI recording host — 2026-10-04

ProjectMidiRecordingHost.ArmCounted prepares one to eight bars through the shared
MetronomeSchedule/MetronomePlayback path and transfers the cursor through the queue.
The cursor tempo/meter is frozen during the count-in. CountInRemainingFrames exposes
coherent progress (or the prepared duration before command acknowledgement).
Existing Start/Arm remain immediate. Count-in is a host monitoring/recording option,
not project musical content; it is neither saved in takes nor included in exports.

Arming deadline now includes the prepared lead-in plus the existing two-second
acknowledgement allowance. Health checks still reject stale audio clocks, device
loss, project edits and transport changes. A consumer-only MidiInputQueue.TryPeek
lets Poll discard packets no newer than a coherent still-counting clock timestamp.
Newer packets remain buffered until start is known, preventing a completion racing
Poll from losing valid input. Continuous early input can be drained during a long
count-in; an actual unserviced producer overflow remains an explicit failure.

The existing activated capture filter excludes pre-start packets. Stopping at or
before a future intra-block start timestamp now cancels instead of subtracting a
negative elapsed interval. No project mutation occurs until explicit take commit.

Validation: **34 passed**, zero failures/skips,
`/tmp/flow-count-in-host-final.log` (host/session/shared MIDI/count-in tests).
New fake-device integration runs an entire count-in with >4096 early packets while
polling, confirms project position stays frozen, delays the final host activation
poll, and retains only the intended post-boundary note at the correct musical time.
The alternate scenario stops before the intra-block boundary and cancels cleanly.
Device ownership and extension release are checked. Prior immediate recording and
session tests remain green.

## Continue

Count-in is wired end-to-end; continuous metronome monitoring during ordinary
playback and recording beyond the finite project still needs implementation.
Use shared Flow graph primitives, handle pause/seek/loop/empty-project recording,
provide host controls/progress and keep exports off by default. Native frontend
integration remains deferred, as does historical callback-gap investigation.

Then deliver the captured-sample authoring example, reconcile historical roadmap
and readiness rows/current architecture docs, and run browser/WASM/core and final
requirement-by-requirement audit. Physical device/latency qualification remains open.
