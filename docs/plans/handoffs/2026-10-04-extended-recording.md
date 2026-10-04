# Empty-project and extended MIDI recording — 2026-10-04

P9-06 removes the existing-timeline restriction of P9-05. Recording into the clean
Flow default project is now a backend path; live instrument monitoring is still open.

## Implemented

ProjectMidiRecordingHost.Start now uses the same asynchronous path as Arm. With
connected, acknowledged, non-looping output and an existing target track, it opens
input and submits one bounded command to start recording playback. State is Arming
until the audio boundary publishes the take's token, starting project frame and
Stopwatch timestamp. This works from stopped, paused or playing, including a zero-
length arrangement. Callers must observe Arming/Recording instead of assuming Start
synchronously creates a take. Poll remains part of the session's ordinary update.

A bounded SPSC queue retains complete MIDI messages while waiting for acknowledgment.
The control owner discards packets received before the acknowledged start, converts
remaining timestamps to frames and drains them into the existing recording session.
A full queue faults the take rather than hiding missing notes. No packet arrays are
allocated by this admission path. The same bounded drain continues after Stop; the
captured end timestamp is fixed after input joins, independent of later UI polls.

PreparedSineTransport can advance through silence beyond its prepared source while
recording. Existing source audio and effect tails are rendered normally before EOF.
This mode changes neither TotalFrames nor the document. EndRecording returns to the
finite timeline: before EOF playback continues; at/beyond EOF the cursor clamps and
stops. Stop clears the extension. Source replacement does not inherit recording mode.
Loop takes remain unsupported and are rejected by the recording host.

The queue publishes an activation clock and uses a token-scoped release mailbox, so
ending recording cannot be blocked by a full transport FIFO and an obsolete release
cannot end a newer take. Release is applied to the old transport before a preserving
source replacement. Otherwise committing a longer captured clip into an initially
empty project could accidentally inherit playing state and autoplay that new clip.

Stop, cancel, fault and disposal release recording mode. Input shutdown retains its
previous join-before-free contract. A failed join keeps input ownership for retry.
If stopped before any activation is acknowledged, no take is produced. Acknowledgment
or playback clocks missing for more than two seconds fault the recording.

## Evidence

- All **418** affected desktop backend, platform, hosting and module tests passed,
  zero failures (`/tmp/flow-extended-recording-final.log`).
- Empty Flow default project: buffered notes received before the first control tick,
  explicit exclusion of pre-activation input, exact take timing, no placeholder clips,
  one captured commit action and undo/redo.
- Full 4096-message drain: Stop releases audio ownership before completion; a later
  drain retains the original end timestamp (`/tmp/flow-recording-drain.log`).
- Arming overflow and cancellation release input/transport without history changes.
- Transport preserves original samples through EOF, continues with zero output,
  retains finite source length, allocates nothing in valid callback operations,
  honors full-FIFO release and rejects obsolete ownership tokens. Committing a longer
  source immediately after release does not autoplay it.

- Web-target shared backend/music/static-binding/native-policy/Phase47/48 selection:
  **328 passed, 7 skipped**, zero failures (`/tmp/flow-recording-web.log`). Actual
  WebAssembly publish succeeded (`/tmp/flow-recording-wasm.log`), generating the
  AppBundle. Desktop solution Release build restored afterward: zero errors,
  314 warnings (`/tmp/flow-recording-desktop-restore.log`).
- Final host/recording/queued-transport selection passed **26 tests**, including the
  additional stale-clock case and bounded coherent-clock observation deadline
  (`/tmp/flow-recording-clock-final.log`). This final change is host-only; the shared
  code tested and published for Web is unchanged. `git diff --check` passed.

## Next and qualification

Add live instrument monitoring through the same Flow instrument graph and track/mixer
routing, with bounded note admission, held-note release and device-loss behavior.
Device instances/presets and integrated bounce/export/recovery workflows remain open.
Count-in/pre-roll and loop takes are not implemented. Captured takes contain notes
and sustain-derived note lengths; pitch bend, program changes and other controllers
do not yet create recorded automation lanes. Receipt/render clock timing still
requires physical latency/drift qualification; native MIDI queue loss and hot unplug
have not been proven by fake-input tests. Extended transport silence does not itself
supply live monitoring. Native JUI frontend and historical callback-gap work remain
deferred. The overall backend goal is still active.
