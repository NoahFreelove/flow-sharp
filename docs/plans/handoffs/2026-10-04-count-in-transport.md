# Audio-owner count-in transport — 2026-10-04

PreparedSineTransport now accepts an exclusively owned, fresh, matching-format
positive-length lead-in through BeginRecording(IPreparedAudioPlayback). It renders
lead-in audio while keeping project position frozen and sums existing live monitoring
with a constructor-preallocated scratch buffer. On the exact final lead-in frame it
enables recording, then uses the remainder of that same block for project playback.

CountInRemainingFrames exposes audio-owner progress. RecordingStartOffset reports
the boundary within the last nonempty block (including a boundary at block end).
Read still returns project/recording frames advanced, excluding lead-in frames;
the output span nevertheless contains the lead-in audio. Pause, successful seek,
stop, loop change or EndRecording cancels the lead-in. Replacement inheritance
stops rather than silently inheriting a pending counted recording.

Validation: **35 passed**, zero failures/skips, `/tmp/flow-count-in-transport.log`.
Tests exercise exact-block and mid-block starts, frozen nonzero cursor, cancellation
paths, empty-project continuation and zero measured callback allocations. Existing
recording, preserved-transport, monitoring and queue regressions remain green.

## Required next work

This overload is not yet exposed through QueuedSinePlayback or the project host.
Continue the integration described in `2026-10-04-metronome-rendering.md`:

- Transfer the prepared lead-in through a bounded queue command with clear ownership
  on rejection/discard/release. Clear retained command references after consumption.
- Publish coherent count-in progress and acknowledge RecordingStartTimestamp using
  the actual intra-block completion offset, not the command-application time.
- Extend ProjectMidiRecordingHost arming timeout/packet filtering for count-in;
  reject early input and preserve late polling, cancellation and device-loss behavior.
- Integrate continuous metronome monitoring, including playback seek/loop and
  recording beyond an empty project's EOF; default exports must exclude monitoring.
- Provide settings/control and Flow parity policy and test actual host composition.

Still required: sample-authoring example, documentation reconciliation, browser/WASM
qualification and final requirements/core audit. Native frontend and historical
callback-gap work remain deferred. Physical hardware qualification remains open.
