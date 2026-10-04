# Phase 6 — callback-gap scheduling investigation, 2026-10-02

Owner requested continued diagnosis of the un-injected 120.984807 ms callback gap and one native underflow in P6-10. This slice adds correlated thread observation and completes controlled and comparative hardware runs. **The original fault did not recur; its root cause is not established and no corrective engine change is claimed.**

## Instrumentation

- CallbackRenderProbe can opt into recording the native Linux thread ID once, on its first native callback. The default remains off; the diagnostic executable enables it. No per-callback procfs access or repeated identity syscall is added.
- The probe records a CLOCK_MONOTONIC/Stopwatch calibration bracket, first callback timestamp and native TID. Analysis preserves the calibration uncertainty instead of assuming Python, PortAudio and managed timestamps are interchangeable.
- `audio_scheduler_capture.py` reads only the owned probe's thread state, wait channel, CPU/scheduling counters, scheduling policy and context switches at nominal 20 ms intervals. It records actual collection intervals and its own CPU cost. No priority, sysctl, server or device configuration changes.
- `audio_gap_analysis.py` associates recorded callback gap neighborhoods and underflow events with the matching native thread. It rejects missing identity, reuse, counter resets, missing/slow samples and incomplete captures as unknown. Deltas cover bracketing samples, not exactly the gap. A short gap with no internal sample remains unknown.
- The existing hardware harness has opt-in `--scheduler` sampling, and the sustained runner now accepts explicit `--mode idle|isolated`. Sampler shutdown is included in probe cleanup.

Kernel `sched_schedstats` was disabled throughout. Consequently runqueue-wait values are null/unavailable, never interpreted as zero contention. R includes both running and runnable; sampled sleeping/wait-channel observations are not an exact scheduler trace. See the [kernel scheduler statistics documentation](https://www.kernel.org/doc/html/v6.12/scheduler/sched-stats.html) for counter semantics.

## Positive control and comparison

Baseline plus three 250 ms whole-process pause controls passed the independent server detector. The correlated samples identified the actual callback thread in `T (stopped)` at `do_signal_stop` for all three injected gaps. The first gap was 253.298422 ms with 12 interior stopped samples and 0.512 ms clock-bracket uncertainty. This validates identity/time alignment for a long known gap; it does not calibrate CPU starvation or every native blocking state.

Then ran serial five-minute captures through ALSA/pipewire, 48 kHz / 256 frames, with identical sampler configuration and no concurrent builds/tests:

| Mode | Native underflow flags | Node / driver deltas | Largest entry gap | Worst managed body | Sampler CPU time |
|---|---:|---:|---:|---:|---:|
| Idle | 0 | 0 / 0 | 11.659875 ms | 3.294627 ms | 28.87 s |
| Isolated Flow/file/hash/GC load | 0 | 0 / 0 | 7.890571 ms | 1.905117 ms | 30.09 s |

Both runs passed their measured callback/headroom and combined steady-window checks, including no body allocations, parent GC, dropped records or callback faults. They do not supersede the earlier failed run. One sequential pair cannot show that load is harmless or beneficial, and the observer adds roughly 10% of one CPU core. It can perturb scheduling. These are conditional diagnostic runs, not an uninstrumented performance baseline.

The idle maximum included one R-state sample; it does not distinguish execution from waiting to be scheduled. The isolated maximum had no interior sample, correctly reported unknown. Callback scheduling policy was recorded as 0 (ordinary scheduling) in the analyzed windows; priority changes have not been tested or justified as a fix.

The matching [PortAudio ALSA source at e1b70d33](https://raw.githubusercontent.com/PortAudio/portaudio/e1b70d33/src/hostapi/alsa/pa_linux_alsa.c) contains device-readiness polling and xrun handling before callback entry. This supports inspecting native wait/recovery paths next, but does not identify the historical gap's cause. Short managed-body times and zero parent collections cannot by themselves attribute the missing time.

## Evidence and verification

`docs/baselines/phase6/scheduler-gap/` contains actual probe reports, assessments, gap analyses, control summary, measured DLL hash and compressed callback-thread-only samples. Other thread data and registry snapshots stay local. Full raw directories:

- `/tmp/flow-phase6-scheduler-calibration`
- `/tmp/flow-phase6-scheduler-idle-300`
- `/tmp/flow-phase6-scheduler-isolated-300`

Example reproduction (new artifact directory required):

```sh
python3 scripts/ci/audio_sustained_check.py --probe scripts/AudioCallbackProbe/bin/Release/net10.0/AudioCallbackProbe --artifacts /tmp/NEW-RUN --seconds 300 --mode isolated --scheduler
python3 scripts/ci/audio_gap_analysis.py --probe /tmp/NEW-RUN/capture/probe.json --samples /tmp/NEW-RUN/capture/scheduler.raw.jsonl --output /tmp/NEW-RUN/gaps.json
```

Control calibration uses `audio_process_pause_check.py --probe ... --artifacts /tmp/NEW-CONTROL --scheduler`.

Release probe build passed. All 14 targeted PlatformAudio tests passed, including opt-in native identity and existing callback allocation checks; 29 Python tests passed, including correlation, missing samples, resets, TID reuse, disabled schedstats and sampler cleanup. Hardware commands and .NET tests used approved access outside the socket-blocking sandbox. No full suite, UI, website or frozen Web adapter changes were needed.

## Next decision

Retain the original failure as the open P6-10 gate. A longer observation with native poll/wakeup/recovery correlation is needed to localize a recurrence. Reduce observer overhead (for example publish the TID once off the callback thread and sample only that thread) before routine long runs. Runqueue wait is currently unavailable; any stronger CPU-starvation claim needs validated scheduler tracing or an explicitly configured diagnostic environment. Do not change priorities/buffering and label the issue fixed solely because a subsequent short run passes. Device recovery, physical latency and the complete reference workload remain separate open Phase 6 gates.
