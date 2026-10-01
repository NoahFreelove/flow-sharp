# Calibrating callback underrun detection

Date: 2026-10-01. Status: callback-starvation detection verified on two named
ALSA routes at 48 kHz / 256 frames. Full device reliability remains open.

## Why injection is required

The previous cadence audit found that the installed PortAudio PulseAudio adapter
passes zero callback flags. A clean counter cannot establish observability. Test
both normal playback and a known interruption before trusting a route's signal.

The diagnostic probe now offers an explicit muted-only `--starve` mode: after
375 callbacks at 256 frames, sleep for 100 ms inside one render callback. Only
that callback thread is delayed; the backend's other threads keep running.
The sample records the injection and its measured body duration. Native flags
are copied unchanged, never synthesized. These runs are not performance passes.
Normal probes leave injection disabled.

The hardware-only `scripts/ci/audio_underflow_check.py` runs a clean baseline and
three separate starvation trials. It requires a measured stall, a native
underflow flag on a later callback within one second, continued callbacks, matching
route/rate/block/version, and complete fault-free captures. Missing flags, dirty
baselines, timeouts and incomplete captures fail the check. It preserves raw
reports and a separate assessment; it does not globally promote a backend to
"reliable."

Device indexes changed during exploration. The adapter now resolves exact
`HOST/NAME` selectors during the same native initialization used to open the
stream; ambiguous or missing names fail instead of falling back to another device.
Each report retains the device actually opened.

## Results on this machine

PortAudio reports revision e1b70d33. All calibration runs were muted, 8 seconds,
48 kHz / 256 frames, serial, without concurrent builds or full test suites.

| Route | Baseline flags | Flags in each of three callback-stall trials | Detection |
| --- | ---: | --- | --- |
| ALSA/pipewire | 0 | 1, 1, 1 | Passed |
| ALSA/HDA Intel PCH: ALC897 Analog (hw:0,0) | 0 | 1, 1, 1 | Passed |
| PulseAudio/Default Sink | 0 | 0, 0, 0 | Failed, expected negative control |

The PipeWire route reported its flag on callback 376, immediately after the
injected callback 375, in all three trials. Playback callbacks continued after
recovery. A separate single ALSA/pulse callback-stall trial also reported zero.

## Limits and next decision

A whole-process SIGSTOP/SIGCONT exploration produced ~250 ms entry gaps with
no flags on ALSA/pipewire, while direct ALSA hardware reported a flag. These
exploratory runs used transient indexes; their actual opened device names are
retained in the raw reports. Callback-only calibration therefore cannot stand in
for process-wide pause detection. A managed runtime pause is not identical to
SIGSTOP, either. Do not infer complete GC-pause coverage from either experiment.

The matching [PortAudio ALSA source](https://github.com/PortAudio/portaudio/blob/e1b70d33/src/hostapi/alsa/pa_linux_alsa.c)
converts ALSA XRUN state into an output-underflow callback flag. The
[PipeWire ALSA plugin](https://github.com/PipeWire/pipewire/blob/1.6.2/pipewire-alsa/alsa-plugins/pcm_pipewire.c)
marks its local stream underrun when a processing request lacks enough frames.
This supports the observed callback-starvation path; it does not establish that
server-wide or hardware errors propagate through the same signal.

Use ALSA/pipewire for further callback-level experiments, with its calibration
attached. Keep generic report observability unverified; the assessment certifies
only the recorded scenario/configuration. Flag counts can coalesce and are not
exact lost-period counts. Direct hardware access is a comparison route, not a
silent change to the user's default output.

Next: obtain independent server/native telemetry for process-wide starvation and
validate it with controlled failures, then assess device latency, startup and
recovery. Keep the Phase 6 gate open. No broad DSP/plugin port is justified by
these short detection tests alone.

Evidence: `docs/baselines/phase6/underruns/`. Original cadence and 30-minute raw
reports remain unchanged. Logs and exploratory data: `/tmp/flow-underruns/`.
