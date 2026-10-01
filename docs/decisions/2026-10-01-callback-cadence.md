# Callback cadence and underflow observability

Status: diagnostic findings recorded; backend/latency strategy remains open.
Date: 2026-10-01.

The previous 30-minute managed-body target remains met. Its 15.686 ms maximum
entry gap cannot be assigned an exact cause retrospectively because gap-event
native timestamps were not captured. New 60-second runs show late wakeups with
buffered catch-up; they do not prove the historical event had the identical cause.

The installed PortAudio identifies revision e1b70d33. Matching upstream sources:
[callback processing](https://github.com/PortAudio/portaudio/blob/e1b70d33/src/hostapi/pulseaudio/pa_linux_pulseaudio_cb.c),
[stream setup/underflow handling](https://github.com/PortAudio/portaudio/blob/e1b70d33/src/hostapi/pulseaudio/pa_linux_pulseaudio.c),
[public extension header](https://github.com/PortAudio/portaudio/blob/e1b70d33/include/pa_linux_pulseaudio.h).
The callback-processing loop services variable server requests in user-sized
blocks and supplies zero callback flags. A private counter receives underflow
events but has no public accessor. StreamInfo latency accounts only for the
PortAudio buffer processor. Therefore zero callback flags do not prove zero
underruns, and zero StreamInfo latency does not describe the output pipeline.

Runtime snapshots on this setup show a 256-frame PipeWire quantum even when
128-frame user callbacks are requested; Pulse requests at least 256 frames and
uses a 768-frame target buffer. The observed paired 128-frame callbacks follow
that arrangement. Native timestamp lead estimates were around 20–23 ms.

Long gaps were followed by rapid catch-up without a long managed body: at one
22.327 ms entry gap, native current time also advanced ~22.322 ms, body execution
was 0.081 ms, DAC progression remained one block, and estimated queued lead fell
to 4.288 ms. This supports native/backend scheduling delay cushioned by buffering,
not a DSP overrun at that event. Exact OS wakeup/preemption attribution requires
further tracing. Backend timestamps are estimates, not physical latency measures.

Decision: keep the prototype available but stop interpreting its zero underflow
flags as a device pass. Reports identify the host, observability limitation,
full flags and native time/gap neighborhoods. Preserve historical raw reports and
correct their derived assessment. Next validate a route with trustworthy underrun
telemetry (or a supported backend fix/direct adapter) before tuning low latency
or selecting the final managed/native strategy. Do not manipulate private native
struct offsets, suppress the evidence or change global PipeWire settings silently.

Measurements and verification are in docs/baselines/phase6/cadence-*.json.
