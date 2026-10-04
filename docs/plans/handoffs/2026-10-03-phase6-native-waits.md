# Phase 6 — lower-overhead sampling and native waits, 2026-10-03

Owner asked to continue the gap investigation. P6-12 adds callback-only sampling and optional owned-process native poll tracing, then runs a ten-minute isolated-load diagnostic. **The original 120.98 ms gap did not recur. The new traced run nevertheless failed combined telemetry, and no root cause or corrective engine fix is claimed.**

## Changes

The probe publishes PID/TID once through an atomic sidecar rename on the control thread. The sampler validates ownership and reads only that callback thread. It never falls back to scanning all threads when identity is missing. Sampling remains 20 ms, with measured intervals and CPU usage. No callback file I/O was introduced.

Read-only `perf stat -e task-clock -- true` failed under the host's `perf_event_paranoid=4` restriction even outside the socket-blocking sandbox. No kernel settings were changed. Tracing a newly launched owned child with strace works. `--native-trace` uses `strace -D -f` with poll/ppoll filtering, preserving the probe's PID/parent relationship for node ownership. It traces only that process tree. It changes scheduling and is explicitly diagnostic, not an untraced performance baseline. Cleanup now terminates the owned group on failure even if the probe exited before its worker/tracer.

The probe records bracketed monotonic/realtime clock samples at both ends. Native trace analysis pairs unfinished/resumed calls, requires a callback-thread exit marker, and marks clock drift/parse problems unknown. The new server-event analyzer validates the observation window and correlates counter updates with nearby polls and retained callback neighborhoods. Proximity is not causation.

## Qualification

- All three controlled 250 ms pauses were detected by the callback-only sampler, with 12–13 interior stopped-thread samples and matching server counter changes. Baseline was clean.
- Short sampler CPU cost was 0.173 CPU-seconds, versus 0.589 in the previous all-thread control. These are observed diagnostic costs, not a product performance guarantee.
- Eight-second native poll tracing worked on the selected ALSA/pipewire route.
- Twelve-second integrated isolated-load qualification passed: 4,508 completed callback-thread polls, no parse errors, 0.798 ms clock bracket uncertainty and 0.030 ms endpoint offset drift.

## Ten-minute result

Muted 32-voice / two-sequence workload, isolated Flow/file/hash/GC worker, 48 kHz / 256 frames, ALSA/pipewire, same targeted output. No builds/tests ran during measurement. No scheduling priority, device or server settings were changed.

| Metric | Result |
|---|---:|
| Native callback underflow flags | 0 |
| PipeWire client-node counter delta | 5 |
| PipeWire driver counter delta | 0 |
| Largest callback entry gap | 15.413416 ms |
| Worst measured managed callback body | 2.393791 ms |
| Callback count | 112,503 |
| Completed Flow worker evaluations | 106,282 |
| Callback-thread sampler CPU time | 11.873845 s over the 600 s capture |
| Completed callback-thread polls | 225,065 |
| Poll timeouts | 113 |
| Longest poll / interval between polls | 10.583 ms / 5.788 ms |

No body deadline/headroom misses, measured body allocations, parent collections, dropped timing records or callback faults. Replacement/retirement completed. The sampler used roughly 2% of one core, versus roughly 10% for the earlier all-thread loaded run; this excludes strace and pw-profiler overhead. The loaded work rate and timing must not be treated as a controlled untraced comparison across days.

Five node counter increments appeared in three clusters, approximately 31.726 s (+1), 500.004 s (+2) and 563.410 s (+2) after the first callback. Nearby native waits were 7.497 ms, 10.583 ms and 9.434 ms respectively. The latter two calls requested a 6 ms timeout and returned timeout after the longer interval. These are delayed waits at the native boundary in this traced run. They do not distinguish kernel wakeup/runqueue delay from tracer overhead. Most of the 113 timeouts were outside these event windows; timeout alone is not evidence of an xrun.

Callback samples and poll timestamps were aligned with a 0.746 ms bracket uncertainty and 0.030 ms endpoint offset drift. Event proximity uses a stated +/-50 ms window, and server counter updates are not exact failure onset timestamps. Only retained longest-gap callback neighborhoods are available; missing neighborhood data does not mean no callbacks occurred.

**The combined check correctly fails despite zero native flags.** This is the converse of P6-10, where a native underflow flag occurred with zero server counter deltas. Neither signal can substitute for the other, and neither counter is an exact physical output-underrun count. The original untraced 121 ms anomaly remains open.

## Evidence and reproduction

`docs/baselines/phase6/native-waits/` contains actual probe output, combined assessment, callback-only compressed samples, native summaries/exact outlier excerpts, counter-window correlations, positive controls and source-data/DLL hashes. Raw profiler/registry/native trace remains at `/tmp/flow-phase6-traced-isolated-600`; registry data must not be committed. Qualification directories are `/tmp/flow-phase6-single-thread-calibration` and `/tmp/flow-phase6-traced-qualification`.

```sh
python3 scripts/ci/audio_sustained_check.py --probe scripts/AudioCallbackProbe/bin/Release/net10.0/AudioCallbackProbe --artifacts /tmp/NEW-TRACE --seconds 600 --mode isolated --scheduler --native-trace
python3 scripts/ci/audio_gap_analysis.py --probe /tmp/NEW-TRACE/capture/probe.json --samples /tmp/NEW-TRACE/capture/scheduler.raw.jsonl --output /tmp/NEW-TRACE/gaps.json
python3 scripts/ci/audio_poll_analysis.py --probe /tmp/NEW-TRACE/capture/probe.json --trace /tmp/NEW-TRACE/capture/native-poll.raw.log --output /tmp/NEW-TRACE/polls.json
python3 scripts/ci/audio_server_event_analysis.py --capture /tmp/NEW-TRACE/capture --output /tmp/NEW-TRACE/server-events.json
```

Release probe build passed; all 37 Python tests passed after capture. Hardware runs verified identity publication and clock metadata. No full .NET or UI suite rerun; production DSP/native adapter behavior and the frozen browser adapter remain unchanged. The current turn changes diagnostic control-thread/reporting code, not audio processing.

## Next bounded experiment

Run the same ten-minute isolated workload with callback-only sampling but without strace, before changing priority or buffering. Keep node/driver/flag counters separate and compare load throughput as well as timing. This can test whether the observed node events recur without ptrace, though one clean run cannot prove its absence. If they recur, collect scheduler/wakeup evidence in a deliberately configured diagnostic environment or add a narrowly scoped low-overhead native wait recorder. Runqueue statistics remain disabled; do not invent CPU-wait totals.

Do not extend replay testing indefinitely or call a clean repeat a fix. Retain both failures, and keep physical latency, device-loss/reconnection, loop/EOF and full-workload gates explicit while proceeding through Phase 6.
