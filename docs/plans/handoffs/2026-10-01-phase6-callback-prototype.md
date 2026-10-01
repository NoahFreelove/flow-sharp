# Phase 6 — Linux callback prototype, 2026-10-01

Phases 0–5 complete; Phase 6 backend in progress. UI remains deferred. Finish
backend and Flow plugins first; action-owned Undo/Redo stays planned for Phase 8.
Use roadmap/progress directly, commit verified slices, do not push.

## Delivered

- `flow-platform-linux`: Flow.Platform.Linux references only Flow.Audio + BCL.
  Linux-only PortAudio v19 P/Invoke to system libportaudio.so.2; float32 stereo
  callback, explicit device selection, control-thread lifecycle and rooted callback
  lifetime through close. No automatic integration with legacy Flow playback.
- `CallbackRenderProbe`: bounded preallocated timing storage, native-buffer
  rendering through QueuedSinePlayback, optional post-render mute (default true),
  exception containment with silence/abort. Capture only after callback teardown.
- `scripts/AudioCallbackProbe`: Release CLI, native device enumeration, 32 voices /
  two sequences at 48 kHz and 128/256 frames, seek/pause/stop/replacement during
  capture. Idle, in-process Flow/file/hash/forced-GC, or isolated worker load.
  JSON records startup/steady body timings, entry gaps, flags, allocation, GC,
  generation/retirement, device/runtime metadata. Output is always muted here.

See docs/TESTING.md for commands. Native library and working audio session are
optional prerequisites, not installed by the repo. Tests do not open devices.

## Evidence and limits

104 focused cases pass. Full gate: **3,116 main + 21 MIDI**, **19 prerequisite
skips**, no failures or tracked-file changes; Web smoke passes. Six new tests
cover native buffer handling, closure, bounded storage and warmed allocation.

Four 20-second device runs on Ubuntu 26.04/x64, i7-11700K, .NET 10.0.12 workstation
GC, PortAudio 19.7.0 development version and PipeWire 1.6.2, using local device 31
(Default Sink, routed to MOONDROP Discdream 2 USB). Do not reuse that numeric index
without enumerating. All report zero underflows, body deadline misses and measured
body allocations. Steady 256-frame worst times: idle 0.316 ms, in-process 2.526 ms,
isolated 0.459 ms; entry gaps 5.640 / 9.919 / 5.753 ms respectively.

128-frame isolated: steady max 0.140 ms; startup max 1.883 ms crosses the 70% target
once, and max entry gap 5.681 ms. No sample loss/fault reported. Native reported
output latency zero is unavailable, not zero physical latency. These measurements
exclude native dispatch/GC before managed entry from body timing; gaps/underflows
are separate signals. No listening or 30-minute stress certification is claimed.

Evidence: docs/baselines/phase6/callback-*.json. Raw logs in
/tmp/flow-phase6-callback/; full verification in /tmp/flow-phase6-callback-verification/.
The invalid-device probe exits 1 with a structured error as expected.
Decision context: docs/decisions/2026-10-01-audio-callback-prototype.md.

## Next ready slice

Run sustained reference stress (30 minutes) and investigate startup/callback
spacing at 128 frames, then record the managed/native/isolation decision before
broad DSP migration. Add appropriate device failure/recovery/lifecycle evidence;
current adapter aborts on callback failure and requires explicit disposal/reopen.
Consider warmup on a throwaway prepared instance without violating active cursor
ownership; preserve cold-start evidence. Do not infer hardware latency from
requested block size. Further comparisons should keep workload/device fixed.

General processors, native resource tails/lifecycle, parameters/smoothing,
live-note recovery, sampler/effects and Phase 7 Flow graph authoring remain open.
The current publication policy intentionally installs stopped at zero.

Run tests serially with MSBUILDDISABLENODEREUSE=1; no tracked edits during verifier
hashing. Restore Desktop after Web publish. Timing probes must run without builds
or suites in parallel. Preserve browser JS and website bundles.
