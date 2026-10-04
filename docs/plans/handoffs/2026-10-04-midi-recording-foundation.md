# MIDI input queue and note recording foundation — 2026-10-04

P9-01 adds host-neutral capture primitives. It does not yet open a MIDI device,
monitor instruments or commit a take into a project clip.

## Implemented

MidiInputEvent carries a validated MIDI channel message and nonnegative host capture
frame. MidiInputQueue is a single-producer/single-consumer bounded ring (default
4,096; maximum 65,536 events), with monotonic timestamps and observable dropped-event
count. Valid enqueue/dequeue paths allocate nothing. A full queue returns false;
callers must invalidate the current take when loss is observed. New capture sessions
use new queues rather than resetting producer/consumer state concurrently.

MidiNoteRecording is control-owner pairing with a 100,000-note maximum and fixed
16×128 held-note slots. It preserves note number, channel, velocity and exact frame
bounds without quantization. Policies are explicit:

- Note-on velocity zero is note-off. Unmatched note-off is ignored.
- Repeating the same channel/pitch closes the previous note and retriggers; channels
  remain independent. Overlapping identical pitches do not form an ambiguous stack.
- Sustain (CC64 threshold 64) extends key releases until pedal-up; retrigger ends the
  previous sustained note. All Notes Off (123) honors sustain; All Sound Off (120)
  closes the channel immediately.
- Completion closes all held notes at stop. Zero-length notes are dropped and
  counted rather than inventing timing. Completion is one-shot.
- Input loss, backward capture time or note-budget overflow faults the take;
  completion cannot expose a partial recording as successful. Note pairing may
  allocate on the control owner; it is not an audio-callback processor.

Other channel messages are currently ignored. Running-status byte parsing, pitch
bend/controller recording, transport/latency mapping and MIDI device lifecycle are
not implemented by these primitives.

## Verification

Four focused tests passed (`/tmp/flow-midi-recording-kernel.log`): exact sustain/
retrigger/channel note pairing, stop closure, input-loss/budget faults, zero-duration
policy, all-notes/all-sound-off behavior, queue ordering/overflow and allocation-free
queue operations. No hardware MIDI qualification is implied.

All **393** affected backend/module tests passed with zero failures
(`/tmp/flow-midi-recording-regression.log`). `git diff --check` passed.

## Next

Add capture-clock/transport mapping and atomic captured note-to-editable-clip commit
with undo/redo, then bounded device input ownership and live instrument monitoring.
Retain raw timing; quantization is a separate explicit edit. Native frontend and
historical audio callback-gap diagnosis remain deferred.
