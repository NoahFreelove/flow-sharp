# Queued count-in ownership and clock — 2026-10-04

QueuedSinePlayback.TryBeginRecording now has an overload accepting an optional
prepared lead-in. Success transfers exclusive cursor ownership; rejection retains
caller ownership. The cursor must be fresh, positive-length and match source format.
Accepted cursors must never be resubmitted. Existing immediate-start API remains.
Consumed/discarded command slots clear references before publishing head advancement.

PlaybackClockSnapshot includes coherent CountInRemainingFrames. Counted starts
publish RecordingStartTimestamp=0 until completed; then the timestamp is the render
entry timestamp plus the transport's intra-block completion offset at sample rate.
RecordingStartFrame remains the frozen project cursor and token retains lease
identity. These are render/receipt timestamps, not calibrated DAC times. A boundary
late in a fast-rendered callback may be later than the clock's receipt timestamp;
the MIDI host must treat pre-boundary stop/input as cancellation/excluded input.

Existing unconditional EndRecording mailbox cancels pending lead-in even with a
full FIFO, and stale lease release cannot cancel a newer lead-in. Stop discards
queued references and cancels active lead-in through the transport.

Validation: **33 passed**, zero failures/skips, `/tmp/flow-count-in-queue.log`.
New queue tests verify frozen cursor/progress, exact five-frame intra-block offset,
output prefix/suffix, delayed RecordingEnabled, full-queue cancellation, rejected
cursor ownership, stale tokens and stop. Related transport/recording/queue tests pass.

## Required next work

Wire count-in into ProjectMidiRecordingHost and provide a control API. Current Arm
still uses immediate start. Prepare MetronomeSchedule.CountIn/MetronomePlayback off
callback; transfer through the new queue overload. Adapt the two-second arming
deadline for legitimate count-ins and expose progress without reading DSP state.

Review early-input buffering carefully: pre-start MIDI must be excluded, but late
host polls must not discard input after an audio-acknowledged boundary. Long lead-ins
should not accumulate all early MIDI until bounded input overflow. A coherent
still-counting clock can supply a safe timestamp cutoff for discarding old packets;
retain packets newer than that observation until the true start is known. Stop
before a future intra-block start timestamp must cancel without negative elapsed
frames. Test these alongside device loss, cancellation and empty-project recording.

Continuous metronome playback beyond empty-project EOF, settings/Flow policy,
sample-authoring example, documentation reconciliation and final browser/WASM/core
requirements audit remain open. Native frontend and historical callback-gap work
remain deferred; physical hardware qualification is separate and open.
