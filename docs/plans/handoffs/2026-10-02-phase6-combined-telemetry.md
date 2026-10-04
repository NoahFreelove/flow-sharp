# Phase 6 — combined sustained telemetry and startup measurements, 2026-10-02

Phases 0–5 complete. Phase 6 remains open. The browser workspace is reviewed and the Flow DAW authoring contracts are documented; owner explicitly chose to resume the remaining Phase 6 work first.

## Result: sustained clean-playback gate failed

P6-10 adds `scripts/ci/audio_sustained_check.py`, using the calibrated ALSA/pipewire route at 48 kHz / 256 frames with isolated Flow/file/hash/GC load. A 300-second muted run recorded one native underflow flag at 244.671 seconds following a 120.984807 ms callback entry gap. Its body took 0.128049 ms. Worst body over the whole run was 1.857083 ms; no body headroom/deadline misses, measured body allocations, parent GC collections, dropped timing samples or callback faults. Replacement/retirement completed.

The independent observer had continuous data across the 296-second steady window plus half-second brackets. Client-node and driver scheduling counters both stayed at zero. Callback flags and server counters therefore must remain separate: neither overrides the other. The assessment correctly fails. The gap's cause is unknown; the evidence does not justify blaming managed DSP, GC, PipeWire, hardware or the load worker specifically. No deliberate stall was injected. No builds/tests ran alongside measurements.

The earlier 30-minute result used a different route with unavailable callback flags. This five-minute result is not an equivalent replacement or proof that the old run was clean.

The profiler parser initially rejected the short qualification because the driver changed to 2048 frames outside the active assessment window. Rate/quantum validation now applies inside the assessed window, with changes there still classified as unknown. The original failed qualification assessment remains in /tmp; a retrospective analysis of its valid window passed. The subsequent five-minute run used the corrected parser. Combined results now separately label server-only status so a callback flag cannot be misleadingly described as a server failure.

## Startup evidence

Reporting-only additions to AudioCallbackProbe measure preparation, open, start, first callback and close without adding work to the callback. After the sustained capture, the updated Release build was used for three fresh-process eight-second idle trials with external observations. All three had zero callback flags and zero assessed node/driver deltas.

- Preparation: 19.64–27.05 ms.
- Stream open: 303.64–369.06 ms.
- Start call: 0.248–0.388 ms.
- First callback after start request: 44.63–65.66 ms.
- Close: 2.43–4.08 ms.

These are measurements, not product acceptance limits. Fresh processes do not prove same-process reopen or device-loss recovery. The sustained backend reported 10.6667 ms output latency; its callback timestamps reported a median 31.8542 ms output lead. Neither is physical measured latency, and they must not be equated or advertised as such.

## Evidence and verification

Normalized evidence: `docs/baselines/phase6/combined-telemetry/`. Includes failed assessment, actual probe output, normalized compressed observer rows, window summary, and three startup reports. Raw profiler/registry/logs remain under `/tmp/flow-phase6-combined-qualification`, `/tmp/flow-phase6-combined-300s`, and `/tmp/flow-phase6-startup-trials`; do not commit registry files. The sustained assessment records the measured probe DLL hash. Its source differed from the current probe only by the subsequent lifecycle reporting additions.

Commands:

```sh
python3 scripts/ci/audio_sustained_check.py --probe scripts/AudioCallbackProbe/bin/Release/net10.0/AudioCallbackProbe --artifacts /tmp/NEW-DIRECTORY --seconds 300
python3 -m unittest discover -s scripts/ci -p 'test_*.py'
MSBUILDDISABLENODEREUSE=1 dotnet build scripts/AudioCallbackProbe/AudioCallbackProbe.csproj -c Release -p:FlowTarget=Desktop -m:1 -nr:false --ignore-failed-sources
MSBUILDDISABLENODEREUSE=1 dotnet test flow-lang.Tests/flow-lang.Tests.csproj -c Release --no-build --no-restore -m:1 -nr:false --filter FullyQualifiedName~PlatformAudio
```

24 Python tests and 13 targeted callback tests passed. Release build passed with existing warnings. Initial no-restore build hit stale Web restore assets; explicit Desktop restore/build resolved it. The sandbox blocked PipeWire and the test runner's local socket; approved escalated runs succeeded. No full .NET suite or browser test rerun was required for these diagnostic/reporting changes. No production DSP/native adapter changes; frozen browser adapter and site bundles untouched.

The sustained harness defaults to five minutes and can run 8–1800 seconds. Profiler JSON is parsed after capture and can consume substantial memory for long runs; add incremental parsing before making 30-minute combined telemetry routine. Cancellation terminates the owned probe process group, including its isolated worker.

## Next work

1. Investigate the un-injected 121 ms gap with time-correlated per-thread scheduling/wait evidence and native/backend state. Capture equivalent idle and isolated workloads; preserve failures rather than rerunning until green. Callback-body metrics alone cannot explain time before entry.
2. Establish a measured latency configuration and repeat sustained combined telemetry. Physical latency needs loopback equipment/a defined loopback route; requested block size is not latency. 128-frame operation needs separate calibration.
3. Implement/verify same-process output lifecycle, device disappearance/reconnection and bounded recovery with failure injection that does not disrupt unrelated desktop audio.
4. Exercise loop-end/EOF without the current frequent seeks, plus the full reference workload before choosing the final managed/native strategy or broadly porting DSP.

P6-10 completes the combined measurement harness and documents a failed reliability gate. It does not close Phase 6 or authorize calling the engine glitch-free.
