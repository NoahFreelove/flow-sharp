# Phase 6 — ten-minute untraced comparison, 2026-10-03

P6-13 completes the requested matching isolated-load run without native tracing.
**The combined sustained check passed. The original 120.98 ms callback gap remains
unexplained, and the full Phase 6 gate remains open.** No engine or diagnostic code
was changed for this run.

## Measurement

The muted 32-voice / two-sequence workload ran for 600 seconds with the isolated
Flow/file/hash/GC worker, callback-only scheduler sampling and external PipeWire
telemetry. Native tracing was disabled. No builds or tests ran during capture.
Device, buffering and scheduling settings were not changed.

| Metric | Previous traced run | This untraced run |
|---|---:|---:|
| Native callback underflow flags | 0 | 0 |
| PipeWire client-node counter delta | 5 | 0 |
| PipeWire driver counter delta | 0 | 0 |
| Largest callback entry gap | 15.413416 ms | 9.192330 ms |
| Worst measured managed callback body | 2.393791 ms | 2.013491 ms |
| Callback count | 112,503 | 112,504 |
| Completed Flow worker evaluations | 106,282 | 151,019 |
| Callback-only sampler CPU time | 11.873845 s | 9.793242 s |
| Combined sustained assessment | Failed | Passed |

All capture, calibrated-route, duration, frame-count, observer-coverage, body
headroom/deadline, allocation, parent-collection, retirement and load-mode checks
passed. Captured callback span was 599.996419 seconds; p99 managed body time was
0.174215 ms. There were no callback faults or dropped timing records. The backend
reported 10.667 ms output latency; this is an estimate, not physical latency proof.
Callback metrics cover the full capture; external telemetry covers its bracketed
steady window. Server scheduling counters are not exact physical underrun counts.

## Comparison limits

The probe DLL hash matched before and after this run and matched the traced run:
`541a6cd080c274a807bd67e4f2242e309c50a7f92bf9dbd310154969a1eba8b6`.
Both used ALSA/pipewire, PortAudio V19.7.0-devel revision e1b70d33, 48 kHz / 256
frames, the same runtime and isolated mode, and the same output driver name and
serial (63). The driver was
`alsa_output.usb-MOONDROP_Discdream_2_Discdream_2-00.analog-stereo`.

Worker throughput increased by 42.09%, and the five node events did not recur.
This is consistent with tracing affecting the earlier measurement, but a single
sequential observational pair cannot distinguish tracer overhead from other
scheduling variation or establish causality. Scheduler sampling and the server
observer remained active. The sampler used about 1.63% of one core; this excludes
the external observer. The earlier **untraced** 121 ms failure remains valid.

## Evidence and reproduction

Evidence is in [untraced-comparison](../../baselines/phase6/untraced-comparison/):
actual probe and assessment JSON, normalized compressed callback-thread samples,
normalized compressed observer rows, validated observer-window boundaries, gap
analysis and a comparison with explicit comparability checks. Registry dumps are
excluded. Raw capture is at `/tmp/flow-phase6-untraced-isolated-600`.
The previous result is documented in the
[native-wait handoff](2026-10-03-phase6-native-waits.md).

```sh
python3 scripts/ci/audio_sustained_check.py --probe scripts/AudioCallbackProbe/bin/Release/net10.0/AudioCallbackProbe --artifacts /tmp/NEW-UNTRACED --seconds 600 --mode isolated --scheduler
python3 scripts/ci/audio_gap_analysis.py --probe /tmp/NEW-UNTRACED/capture/probe.json --samples /tmp/NEW-UNTRACED/capture/scheduler.raw.jsonl --output /tmp/NEW-UNTRACED/gaps.json
```

The hardware command exited successfully and all sustained checks passed. No code
changed, so the previous build and 37 passing Python tests were not rerun. Evidence
comparison asserts matching hash, driver identity, route/format, duration, runtime
and mode.

## Next work

Proceed to the remaining Phase 6 evidence: device-loss/reconnection lifecycle,
physical latency, loop/EOF behavior without frequent seeks, and the full reference
workload. Keep the unresolved gap visible in the final managed/native decision.
If it recurs, use a bounded low-overhead native-wait or scheduler investigation;
do not extend replay testing indefinitely or describe this clean repeat as a fix.
