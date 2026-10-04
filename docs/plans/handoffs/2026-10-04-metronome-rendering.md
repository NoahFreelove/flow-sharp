# Shared Flow metronome rendering — 2026-10-04

`MetronomePlayback` prepares scheduled clicks using the existing sine-oscillator,
ADSR, multiply and pitch/gate/velocity input graph devices. It introduces no new
DSP kernel. InstrumentGraph returns the exact graph used for rendering and can be
exported/rebuilt with FlowGraphExporter. The default click has a 1 ms attack, 20 ms
decay to zero, zero release, 25 ms gate, 1500 Hz bar accent and 1000 Hz other beats.
Volume is 0..1 (default .25), polyphony is bounded to 16 voices and schedule capture
is bounded to 100000 beats. A caller can supply another compatible Flow graph.

Validation: **3 passed**, `/tmp/flow-metronome-playback.log` (schedule and renderer).
A three-beat count-in renders the expected duration, silence between clicks, accent
pitch difference, seek-at-click and mute behavior. Rebuilding its exported Flow
graph gives sample-identical audio.

## Required continuation

Metronome/count-in still need transport/session integration; do not mark complete.

`PreparedSineTransport` currently starts recording immediately. Add a prepared
lead-in cursor owned by the audio transport that renders while project position
is frozen, completes at an exact sample boundary (including inside a block), then
starts recording/advances project time. Stop/cancel/device loss must release it.
Preserve monitoring and define pause/seek/replacement behavior explicitly.

`QueuedSinePlayback` currently has a BeginRecording FIFO command with a lease token;
it captures RecordingStartFrame/Timestamp on command application. Extend ownership
and clock publication so a counted start is acknowledged at the actual completion
boundary, not the command boundary. Its stop/release mailbox must cancel a lead-in
even with a full command FIFO. Avoid retaining discarded prepared cursors forever.

`ProjectMidiRecordingHost` currently waits for RecordingEnabled/token and rejects
unacknowledged arming after two seconds. Adapt this for a legitimate longer count-in,
exclude all buffered pre-start packets, retain late-poll behavior and cancellation,
and test blocks straddling the boundary with fake audio/MIDI devices. Add readable
count-in progress and controls. Continuous metronome monitoring beyond empty-project
EOF/recording also needs integration, with exports off by default.

Still required: capture/settings/Flow policy, captured-sample authoring example,
historical roadmap/docs reconciliation and final compatibility/readiness audit.
Native frontend and callback-gap investigation remain deferred. Hardware/latency
qualification stays separately open.
