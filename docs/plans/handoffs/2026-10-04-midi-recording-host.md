# MIDI recording host — 2026-10-04

P9-05 connects the input ownership and take lifecycle slices. This is backend
recording during an existing, uninterrupted non-looping timeline, not completion
of the MIDI workflow gate.

## Implemented

ProjectPlaybackSession owns MidiRecording and polls it alongside output and playback
preparation. Start requires an existing target track, healthy playing output, an
acknowledged unchanged project, no pending transport commands, and no enabled loop.
It opens an exact native input name. Complete channel packets are validated and
converted to frame-stamped events; system messages are ignored. Recording retains
MIDI channel identity and supports the earlier sustain/retrigger/note pairing rules.
Stop joins input, checks its final error, drains the session and produces a captured
take. Commit adds editable notes/source/clip through one undo action and schedules
playback preparation. It never reruns input on redo.

QueuedSinePlayback publishes a coherent render-clock observation with frame, monotonic
timestamp, state, source generation and loop flag. Reads use a bounded sequence check
and allocate nothing. A producer-side change counter records every accepted transport
or replacement request, including pause/resume pairs between polls. Rejected commands
do not change it. Recordings fail on transport/source changes, project edits, output
loss, stale clocks, malformed packets, input errors or timed-out input shutdown.
A failed take cannot be committed. Timed-out shutdown retains native input ownership
for retry. Session shutdown closes input before output; an output-close failure leaves
the session available for retry rather than prematurely disposing its recorder.

## Timing and remaining work

Clock alignment uses the completed-render position plus elapsed Stopwatch time.
Packet timestamps are polling receipt time, not hardware timestamps or DAC time.
The explicit frame compensation is subtracted when constructing a take; hardware
latency calibration, long-run clock drift and native queue-loss detection remain
unqualified. The playback clock stale limit is two seconds. Stop's end time is taken
after input joins; a failed join faults rather than producing a partial take.

Recording currently starts while playback is already running and must finish before
that prepared timeline ends. Empty-project recording, pre-roll/count-in, extending
the timeline during recording, loop takes, live instrument monitoring, device/preset
workflows and integrated export/recovery remain open. Native JUI frontend work and
the historical callback-gap investigation remain deferred.

## Evidence

The focused input/host run passed 11 tests (`/tmp/flow-midi-host.log`). Host coverage
includes exact frame-to-musical placement, one captured undo/redo action, pause/resume
between polls, malformed packets, device faults, failure in the final joined read,
shutdown timeout/retry, project edits, pending commands and loop rejection.
A separate queue test checks clock fields, accepted/rejected command versions and
allocation-free clock publication/reading. The affected backend test selection passed
411 tests (`/tmp/flow-midi-host-regression.log`). Full core verification then passed
with the final post-join health recheck: desktop solution build, **3,343** language/
backend tests and **21** MIDI tests, zero failures and 14 skipped/unexecuted tests.
The tracked-content audit was empty. Evidence:
`/tmp/flow-midi-host-core/verification.json`. `git diff --check` passed.
