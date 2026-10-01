# Underrun signal calibration — 2026-10-01

P6-08: callback-only starvation detection verified at 48 kHz / 256 frames;
process-wide/server/device reporting remains open. See the
[decision](../../../decisions/2026-10-01-underrun-calibration.md).

| Route | Baseline flags | Three injected runs | Calibration verdict |
| --- | ---: | --- | --- |
| ALSA/pipewire | 0 | 1, 1, 1 | Pass for callback starvation |
| ALSA/HDA Intel PCH: ALC897 Analog (hw:0,0) | 0 | 1, 1, 1 | Pass for callback starvation |
| PulseAudio/Default Sink | 0 | 0, 0, 0 | Expected negative-control failure |

Each directory contains an 8-second baseline, three 8-second trials with one
100 ms callback-only stall, and the harness assessment. The PipeWire route
reported its native underflow on callback 376, immediately after injected callback
375 in all three trials. All captures had zero measured callback allocations,
parent collections, faults and dropped records. Injected runs intentionally miss
deadlines and must not be treated as performance evidence.

`pipewire-isolated-60.json`: normal 60-second isolated-load run on ALSA/pipewire,
11,253 callbacks / 2,880,768 frames / 60.003261 seconds. Body median 0.103592 ms,
p99 0.209260 ms, max 2.048577 ms; maximum entry gap 5.623682 ms. No 70%/deadline
misses, callback flags, measured allocations, parent collections, faults or dropped
records. Worker: 12,744 evaluations, 53,452,210,176 cached bytes read, collections
[8300, 7056, 6710]. This short run does not replace the old 30-minute stress gate
or certify end-to-end glitch-free playback.

Additional raw exploratory reports:

- `stall-pulse.json`: one callback-only trial on ALSA/pulse, zero flags.
- `explore-23.json`: whole-process 250 ms SIGSTOP/SIGCONT on the actual recorded
  ALSA/pipewire route, zero flags despite a 253.068 ms maximum gap.
- `route-0.json`: same exploratory whole-process intervention on actual recorded
  direct HDA ALSA hardware, one flag and a 255.914 ms maximum gap.

The exploratory harness paused its own probe child about 3 seconds after launch,
resumed it after 250 ms and waited for completion. Those older probes lack the
new diagnosticStall field because the pause was external. Device enumeration
indexes changed during exploration; trust each report's actual host/name, not
its filename. Future calibrated probes resolve HOST/NAME inside native open.

Callback starvation and process-wide starvation have different visibility. Flags
can coalesce and cannot supply exact lost-period counts. Raw reports keep generic
underflowObservability unverified; the separate assessment certifies only the
specific injection scenario. No global device/server configuration was changed.

Validation: focused platform tests **13/13**; Python assessment tests **5/5**;
full **3,123 main + 21 MIDI passed, 19 skips**, zero failures/tracked mutations;
Web adapter smoke passed. Adjacent JSON records the full and Web gates. Raw logs
and TRX remain under `/tmp/flow-underruns/` and `/tmp/flow-underruns-verification/`.
