# Shared live instrument kernel — 2026-10-04

P9-07 supplies the live instrument bus needed for MIDI monitoring. It does not yet
connect that bus to project routing or a native input; monitoring remains unfinished.

## Implemented

PreparedLiveInstrument reuses PreparedNotePlayback's existing voice creation and
rendering kernels. There is no parallel synth implementation: legacy sine/sample,
Flow instrument graphs, graph sample slots, note-off release, phase/reset behavior,
voice stealing and public parameter validation/smoothing remain shared with clips.
Live MIDI pitch is 12-TET A4=440, consistent with captured MIDI notes.

One MIDI producer writes complete validated channel messages into a bounded SPSC
queue (default 4096, maximum 65536). The audio owner applies at most 256 messages per
nonempty block by default (configurable 1–4096) and retains the remainder for later
blocks. Messages apply at block boundaries; this is not intra-block scheduling.
Overflow latches a fault, closes admission and silences held/queued voices at the
next boundary. Explicit CloseAdmission also permanently closes that prepared
instance, including an in-flight writer. A new instance is required to resume after
close/fault; later integration must own preparation/replacement rather than clearing
this latch while producer and consumer are active.

Note-on/off and velocity-zero note-on are supported. CC64 sustain, CC123 all notes
off (honoring sustain), and CC120 channel all-sound-off are handled with fixed storage.
Same-key retrigger releases the prior voice and starts a new one. An off for a stolen
key cannot release its replacement. Pitch bend, pressure, program changes and other
CCs currently have no implicit parameter mappings.

RequestPanic is independent of the FIFO: it kills voices and discards commands seen
at the next boundary while leaving admission open. Later messages may sound. Device
loss/detachment must close admission, not merely request panic. All valid admission
and render operations allocate nothing; graphs and sample assets are prepared first.

Public parameter batches are consumed once before any live note starts/resets. Newly
started voices snap to the latest targets, as scheduled voices do; updates arriving
after that boundary wait until the next block. This avoids starting a fresh live voice
on an obsolete value and then ramping it to a target that scheduled notes already use.

## Evidence

All **429** affected backend, platform, music, hosting and module tests passed,
zero failures (`/tmp/flow-live-instrument-regression.log`). The new live tests cover:

- Exact sample-array equality with scheduled sine, legacy sample, Flow oscillator/
  envelope and graph sampler voices at the same note boundaries.
- Sustain, channel panic, retrigger, all-notes-off and release tails.
- Overflow closes admission and leaves no held or queued notes sounding.
- Bounded per-block command work and panic even with a full FIFO.
- Coherent latest controls on newly started and stolen voices.
- Concurrent producer/close safety and zero allocation during valid input/rendering.

Level-only assertions use the renderer's established float precision; the four live/
scheduled comparisons use exact array equality. `git diff --check` passed. No physical
input or audio device was opened for these tests. The earlier Web publication predates
this kernel; repeat Web compatibility at the next shared-pipeline checkpoint.

## Next: route and own monitoring

Attach prepared live instrument buses before the existing track/master Flow graph,
so nonlinear effects see clip audio and monitored notes together. Summing a second
independently effected output would change device-chain behavior and is not equivalent.
PreparedGraphPlayback currently reads only finite arrangement sources. Transport skips
source rendering when stopped/paused and extends recording with silence after EOF.
Those paths need explicit monitoring support, including graph tails and coherent
instrument parameter groups, without changing finite export duration or autoplay.

Keep DSP elapsed time separate from the frozen project automation clock during stopped/
paused monitoring. Handle seek/stop/replacement and stale input ownership explicitly.
Then connect native packet admission to both monitoring and recording, preserving the
single-producer contracts and join-before-free input lifecycle. Instrument/control
preparation remains outside the callback.

Device instances/presets, integrated export/recovery and hardware latency/device-loss
qualification remain open. Native JUI frontend and historical callback-gap work are
deferred. The overall backend goal remains active.
