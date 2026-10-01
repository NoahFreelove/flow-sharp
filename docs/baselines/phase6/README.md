# Phase 6 verification — prepared playback

Date: 2026-10-01. This verifies backend slices, not the full Phase 6 gate.

**Observability correction (cadence audit):** historical `outputUnderflows: 0`
values below are callback-flag counts. The matching PortAudio PulseAudio backend
passes zero flags even when its private underrun counter increments. Actual device
underruns are unknown; no zero-underrun gate is established by those reports.
Raw evidence is preserved. Managed-body duration/allocation results remain valid.

- Focused music-model tests: 67 passed.
- Desktop solution build and all-tier tests: 3,079 main + 21 MIDI passed,
  19 prerequisite skips, zero failures and zero tracked-content mutations.
- Generated Web bundle: fresh sessions, located parse errors and music arithmetic
  pass through the unchanged JavaScript adapter.
- Legacy sine bit parity includes tuning, ties, pedal, parallel voices and stealing.
  Prepared read/seek/reset and boundary crossings allocate zero bytes after warmup.
- Device timing, underruns, managed/native decision, full DSP and UI remain open.

Reproduce:

```sh
MSBUILDDISABLENODEREUSE=1 python3 scripts/ci/verify.py --tier all --artifacts /tmp/flow-phase6-prepared
node scripts/ci/wasm-session-smoke.mjs "$PWD/flow-lang/bin/Release/net10.0/browser-wasm/AppBundle/flow-runtime.js" /tmp/flow-phase6-wasm.json
```

Summaries are adjacent JSON files; raw logs/TRX are in the temporary artifact path.
Local allocation tests do not establish hard real-time safety or immunity to GC.

## Frame transport slice

2026-10-01: `PreparedSineTransport` adds single-owner play/pause/stop/seek/loop.
Fourteen new cases preserve reference samples and require zero warmed allocation.
The all-tier gate passed **3,093 main + 21 MIDI**, with 19 prerequisite skips,
zero failures and zero tracked-file mutations. Web adapter smoke also passed.
The adjacent `transport-all-verification.json` and `transport-wasm-verification.json`
record the results; raw logs/TRX are at `/tmp/flow-phase6-transport/`.

Reproduce with the commands above using `--artifacts /tmp/flow-phase6-transport`
and `/tmp/flow-phase6-transport-wasm.json` for the smoke output. This slice does
not establish device deadlines or cross-thread transport safety.

## Bounded host command slice

2026-10-01: `QueuedSinePlayback` adds a bounded FIFO and stop recovery independent
of FIFO capacity. Focused music-model tests passed 90/90. Nine new cases cover
sample parity, saturation, wraparound, concurrent commands/stop acknowledgment,
validation and zero warmed allocation. All-tier verification passed **3,102 main
+ 21 MIDI**, with 19 prerequisite skips, no failures or tracked-file mutations.
The Web adapter smoke passed. Summaries: `queue-all-verification.json` and
`queue-wasm-verification.json`; logs/TRX: `/tmp/flow-phase6-queue/`.

Reproduce using the commands above with `--artifacts /tmp/flow-phase6-queue`
and `/tmp/flow-phase6-queue-wasm.json` as the smoke output. Concurrency tests
exercise the single-producer/single-consumer protocol, not hard real-time safety.

## Prepared playback publication slice

2026-10-01: bounded replacement and control-thread retirement in `QueuedSinePlayback`.
Focused tests: **98/98**. All-tier verification: **3,110 main + 21 MIDI passed,
19 prerequisite skips**, no failures or tracked-file mutations. Web adapter smoke
passes. Eight new cases cover smaller/empty scores, stale-command handling,
stop precedence, ownership/backpressure, 2,000 concurrent swaps and zero warmed
callback installation allocation. Preparation/allocation is off the measured path.

Summaries: `publication-all-verification.json`, `publication-wasm-verification.json`.
Logs/TRX: `/tmp/flow-phase6-publication/`. Reproduce using the commands above with
`--artifacts /tmp/flow-phase6-publication` and `/tmp/flow-phase6-publication-wasm.json`
as the smoke output. General DSP/native-resource lifecycle and device timing remain
open; this is a dry-sine protocol proof, not seamless plugin hot reload.

## Linux callback prototype measurements

2026-10-01, four independent 20-second muted real-device runs. Reference machine:
Ubuntu 26.04 x64, Intel i7-11700K (8 cores/16 threads), .NET 10.0.12 workstation GC;
libportaudio2 19.7.0+git20260206.e1b70d33-0ubuntu1, PipeWire/PulseAudio 1.6.2,
Default Sink routed to MOONDROP Discdream 2 USB. No device configuration changed.
32 sine voices in two sequences at 48 kHz; control/replacement operations enabled.
Builds and test suites were not run concurrently with the measurement windows.

| Load / frames | Callbacks | Steady p99 ms | Steady max ms | All max ms | Max entry gap ms | Reported underflows | Body deadline misses |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Idle / 256 | 3753 | 0.141 | 0.316 | 1.679 | 5.640 | 0 | 0 |
| In-process / 256 | 3753 | 0.320 | 2.526 | 2.526 | 9.919 | 0 | 0 |
| Isolated / 256 | 3752 | 0.190 | 0.459 | 1.705 | 5.753 | 0 | 0 |
| Isolated / 128 | 7504 | 0.092 | 0.140 | 1.883 | 5.681 | 0 | 0 |

Steady excludes the first second; full startup samples remain summarized in JSON.
All measured callback-body allocations and dropped timing samples were zero.
The 128-frame startup maximum exceeded 70% of its 2.667 ms deadline once. Native
reported latency was zero (unavailable); entry gaps can reflect batching/scheduling
and must not be equated directly to body deadline misses or hardware latency.

In-process load evaluated Flow 4,474 times, read/hashed 18.8 GB through a cached
4 MiB temporary file, and forced full collections every eight iterations. Parent
GC counts were 2,272/1,713/1,713. Isolated runs kept parent GC counts zero; child
counters include its slightly longer lifetime around the capture window. Muting
happens after DSP, so these runs exercise rendering without claiming audible UAT.

Raw summaries: callback-idle-256.json, callback-inprocess-256.json,
callback-isolated-256.json and callback-isolated-128.json. Invalid-device behavior
is recorded in callback-unavailable-device.json (expected exit 1). Reproduction
commands are in docs/TESTING.md; indices are local and must be enumerated.
Raw logs: /tmp/flow-phase6-callback/.

Full gate: **3,116 main + 21 MIDI passed, 19 skips**, no failures or tracked-file
mutations; Web smoke passes. Focused **104/104** includes six hardware-free
callback tests. Verification artifacts: callback-all-verification.json and
callback-wasm-verification.json; logs/TRX in /tmp/flow-phase6-callback-verification/.

These short runs support further isolation experiments. They do not close the
30-minute stress gate or settle managed/native strategy. No UI load, native DSP
baseline, device-loss recovery or listening certification was measured.

## Thirty-minute isolated-load stress

2026-10-01: uninterrupted 1800-second capture on the same reference device and
machine, at 48 kHz/256 frames, with isolated Flow/asset/forced-GC load. No builds
or suites ran concurrently. Device output was muted after rendering. The Release
build passed; the probe exited 0 and both parent/worker terminated normally.

| Measurement (all callbacks, including startup) | Result |
| --- | ---: |
| Callbacks / frames | 337,508 / 86,402,048 |
| First-to-last callback span | 1799.998879 s |
| Median / p99 body duration | 0.113212 / 0.242357 ms |
| Worst body duration | 2.085725 ms |
| Block deadline / 70% target | 5.333333 / 3.733333 ms |
| Worst fraction of deadline | 39.11% |
| Body deadline misses / 70% overruns | 0 / 0 |
| Reported output underflows | 0 |
| Measured body allocations / parent collections | 0 bytes / 0 |
| Dropped timing samples / callback faults | 0 / false |
| Maximum entry gap | **15.686382 ms** |

The worst body occurred at 147.463 seconds, not during startup. Generation 2 and
one retired transport confirm replacement. One ordinary command was rejected;
the reason is not recorded separately. The worker performed 254,147 Flow
evaluations and read/hashed 1.066 TB of cached file data, with GC counts
189,352/188,107/157,586. These are cached reads, not physical disk traffic, and
worker counts include its slightly longer lifetime.

The managed-body target is met for this workload/configuration. The full device
and Phase 6 gates remain open: the 15.686 ms entry gap needs scheduling/buffering
analysis; native reported latency zero is unavailable; muted output is not
listening certification. No UI load or native DSP comparison was included, and
frequent mid-score seeks do not stress loop-end/EOF transitions. Callback body
measurement excludes native entry/dispatch and telemetry append overhead.

Evidence: `callback-isolated-256-30min.json` (unaltered probe output, including the
20 worst body samples) and `callback-stress-assessment.json` (explicit checks).
Raw logs/device enumeration: `/tmp/flow-phase6-stress/`. Reporting-only changes
added span/frame counts/outliers and removed the hard-coded short-run limitation;
no callback or workload changes were made for this capture. Base commit: c43278b.

Reproduce after enumerating the current device index:

```sh
scripts/AudioCallbackProbe/bin/Release/net10.0/AudioCallbackProbe 1800 isolated /tmp/callback-stress.json DEVICE_INDEX 256
```

The full regression suite was not repeated for this report-only change; the
preceding callback-all-verification.json remains the latest suite evidence.

## Cadence follow-up and underflow observability

Two 60-second isolated-load runs, same device/configuration. Read-only `pactl` and
`pw-top` snapshots were taken during capture. No settings were changed; no
builds or test suites overlapped capture. This is a diagnostic follow-up, not a repeat 30-minute certification.

| Callback frames | Callbacks | Gap median / p99 ms | Largest gap ms | Native lead median ms | Gaps < half period / > 1.5 periods |
| --- | ---: | ---: | ---: | ---: | ---: |
| 256 | 11,253 | 5.324 / 5.708 | 8.756 | 20.488 | 1 / 1 |
| 128 | 22,504 | 0.333 / 5.598 | 22.327 | 22.521 | 11,256 / 11,212 |

PipeWire used 256 frames at 48 kHz in both snapshots. Pulse target length was
6144 bytes (768 stereo float frames, 16 ms); minimum request was 2048 bytes
(256 frames). Thus requesting 128 user frames did not halve the driver quantum:
callbacks were normally delivered in pairs. These settings and native lead
estimates do not measure end-to-end hardware latency.

At the 256-frame longest gap, managed/native current-time deltas were 8.756/8.768
ms, body 0.294 ms, and the next gap 1.829 ms. Output DAC time advanced one block;
estimated lead fell from 20.643 to 17.209 ms, then recovered. At the 128-frame
longest gap, managed/native deltas were 22.327/22.322 ms, body 0.081 ms, followed
by sub-millisecond catch-up calls. Estimated lead bottomed at 4.288 ms. These
observations locate delay before managed processing and show buffering/catch-up;
they do not identify the exact scheduler event behind the old 15.686 ms maximum.

Both runs: zero body allocations, parent collections, callback faults and dropped
records. Raw flags zero; actual underruns unknown. Body maxima 2.266/2.032 ms;
128-frame startup exceeded the 70% target once, with no body deadline misses.

Source audit at PortAudio revision e1b70d33:
- `pa_linux_pulseaudio_cb.c`: request-processing loop; BeginBufferProcessing flags
  argument zero; native current/DAC timestamp construction.
- `pa_linux_pulseaudio.c`: private outputUnderflows increment; StreamInfo latency
  derived only from buffer-processor frames.
- `pa_linux_pulseaudio.h`: no public accessor for the private underrun counter.

See [the investigation decision](../../decisions/2026-10-01-callback-cadence.md)
for pinned upstream links. The older raw JSONs remain untouched; the derived
30-minute assessment records this observability correction.

Evidence: cadence-pulse-256.json, cadence-pulse-128.json, cadence-server-settings.json
(sanitized server properties, no machine/user identifiers). Raw snapshots/logs and
source downloads: /tmp/flow-cadence/. Full gate: **3,117 main + 21 MIDI passed,
19 skips**, no failures/mutations; Web smoke passed. Focused **105/105** includes
native timestamp copying and warmed allocation. Gate summaries are adjacent
cadence-all-verification.json and cadence-wasm-verification.json.
