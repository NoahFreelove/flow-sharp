# MIDI recording session lifecycle — 2026-10-04

P9-03 composes bounded input admission, note pairing and captured takes. Native
input device ownership, transport observation and live monitoring remain open.

## Implemented

MidiRecordingSession captures one expected project snapshot and an explicit frame
clock mapping. One producer calls TryCapture; one control owner calls Poll,
RequestStop, ReportDiscontinuity or Cancel. Valid input admission is allocation-free.
Producer-side validation/overflow failures close admission and expose an error.

Stop closes admission atomically and marks Stopping. Poll processes a bounded
number of events per tick, waits for any already-entered producer without blocking,
then performs a second drain after the producer leaves. Completion closes held
notes at the supplied end frame and creates a MidiRecordingTake. Input that arrives
after admission closes returns false without entering the take. The session does
not implicitly mutate the document; its completed take commits through the existing
one-action editable-clip path.

Queue overflow, invalid/backward timestamps, pairing budgets and reported transport/
device discontinuities produce Faulted with no Take. Cancel closes admission and
produces no take/history. Straight-through timing remains the contract: the future
host must report pause/seek/loop or device loss rather than silently changing clocks.
Poll's event drain is bounded; final note sorting/take construction is control-side
work bounded by the note budget, not audio-callback work.

## Evidence

Four focused tests passed (`/tmp/flow-midi-session.log`): bounded multi-tick drain,
held-note completion and one-action commit; overflow/discontinuity/bad-time faults;
a stop racing an input producer with up to 10,000 retriggers retains every accepted
note; cancellation leaves history unchanged; valid admission allocates nothing.
The race test has an explicit deadline to avoid an unbounded test wait.

All **399** affected backend/module tests passed with zero failures
(`/tmp/flow-midi-session-regression.log`). `git diff --check` passed.

## Next

Integrate a host input adapter/clock and recording controls with project playback,
including device stop/join ownership, automatic discontinuity reporting and live
instrument monitoring. Then complete remaining MIDI/preset/device/recovery workflows.
Native JUI frontend and historical audio callback-gap diagnosis remain deferred.
Physical keyboard timing and hot-unplug reliability are not proven by fake-input
unit tests.
