# Whole-process pause detection — 2026-10-01

P6-09 closes the specific whole-client starvation reporting gap on the tested
ALSA/pipewire route at 48 kHz / 256 frames. It does not close the full Phase 6 gate.

A separate `pw-profiler -J` process observes the probe's PipeWire output node.
The harness resolves ownership through the child's PID → client ID → output node,
then records node and driver object serials before and after each observation.
Only the owned, muted probe is paused; the observer and server remain running.
No global server/device settings are changed.

| Run | Confirmed pause ms | Max callback gap ms | Callback flags | Node xrun delta | Driver xrun delta |
| --- | ---: | ---: | ---: | ---: | ---: |
| Baseline | 0 | 5.557 | 0 | 0 | 0 |
| Pause 1 | 250.152 | 251.280 | 0 | 92 | 0 |
| Pause 2 | 250.149 | 254.736 | 0 | 92 | 0 |
| Pause 3 | 250.159 | 252.974 | 0 | 92 | 0 |

Each run lasts 8 seconds. The assessment window brackets the intervention with
half a second on either side, checking contiguous profiler sequence numbers,
nondecreasing driver timestamps, no gaps over 30 ms, stable counters, and at least
ten distinct server timestamps during the pause window. The baseline has 237
window records; injected runs have 282, 282 and 283. There are 92 records during
each pause, including legitimate complete/incomplete notification pairs.

The value 92 is a PipeWire node scheduling counter delta, **not** an exact number
of lost periods or physical hardware underruns. Device-driver counters are
reported separately. Counter increments must fall inside the intervention or
its 100 ms recovery allowance; unrelated increments fail attribution.

Evidence includes normalized observer windows and untouched probe JSON. Raw
profiler/registry snapshots remain in `/tmp/flow-server-observer/calibration-v2/`;
registry snapshots contain local user/machine identifiers and are not committed.
The first calibration attempt was marked unknown because an overly strict parser
rejected legitimate equal timestamps. It remains under `calibration/`; the parser
was corrected using the source contract and a regression test, then all trials
were rerun. Exploratory data is under `/tmp/flow-server-observer/`.

Validation: all **17 Python CI-tool tests passed**, including ten observer tests.
No C# runtime, callback or browser files changed, so the unchanged .NET/Web gates
were not rerun for this tooling-only slice. The previous full gate remains
3,144 passed / 19 skipped, recorded in the adjacent underrun-calibration evidence.

Reproduce using a built Release AudioCallbackProbe:

```sh
python3 -B scripts/ci/audio_process_pause_check.py \
  --probe scripts/AudioCallbackProbe/bin/Release/net10.0/AudioCallbackProbe \
  --artifacts /tmp/flow-process-pause-check
python3 -B -m unittest discover -s scripts/ci
```

Use a new output directory. Missing profiler/node data, truncated streams,
sequence gaps, counter resets/wraps, identity changes and capture failures are
unknown/failure, never zero errors. Observer and probe processes have bounded
waits and cleanup; interruption after SIGSTOP resumes and reaps the owned child.

Next backend work: combine callback and external observer evidence in sustained
runs, then latency/startup/device-recovery validation. Physical output reliability,
128-frame operation and the full reference workload remain separately gated.
The owner is ready to share the DAW design for review before UI implementation.
