# Phase 6 — external process-pause observer, 2026-10-01

Phases 0–5 complete; Phase 6 backend in progress. Owner says the DAW design is ready
and asked to handle the reporting gap first. That specific gap is now covered on
the measured ALSA/pipewire route. Next user interaction: review their design against
`docs/plans/2026-10-01-daw-mvp-design-brief.md`. This is design review, not a change
to the backend/plugin-first implementation sequence. Commit verified slices, no push.

## New evidence

`scripts/ci/audio_process_pause_check.py` starts an independent `pw-profiler -J`
observer and an 8-second muted probe. It finds the owned output node via PID/client
relationships, records node and driver serials, then optionally pauses only the
probe for 250 ms using SIGSTOP/SIGCONT. The stopped state is confirmed through
/proc; all owned processes are resumed/reaped on failure or interruption.

One baseline plus three paused trials passed. Baseline node/driver deltas zero;
each pause: node delta 92, driver delta zero, native callback flags zero. Actual
pause durations ~250.15 ms, callback gaps 251.28–254.74 ms. Counts are server
scheduling events, not exact lost periods or physical output underruns.

Assessment checks a window from 0.5 seconds before to 0.5 seconds after intervention:
contiguous profiler sequence numbers, nondecreasing timestamps, no gap >30 ms,
at least ten distinct timestamps during observation, follower presence, monotonic
counters and unchanged node/driver identity. Increments must correlate to the
pause/recovery window. Missing/reset/truncated evidence is unknown, never clean.

PipeWire sends complete and incomplete notifications with equal driver timestamps;
sequence numbers still advance. The first attempt rejected those pairs and was
preserved as unknown. The parser was fixed with a regression test; the full set
of hardware trials was rerun successfully. No global audio settings changed.

## Validation and artifacts

All 17 Python CI-tool tests passed (ten observer tests). No C#/browser changes:
the previous 3,144-passed/19-skipped full gate remains the latest runtime gate,
not a newly rerun result. Hardware capture and assessment both succeeded.

Evidence: `docs/baselines/phase6/process-pause/`; raw files/logs:
`/tmp/flow-server-observer/calibration-v2/`. Raw registry snapshots contain private
machine/user metadata: do not commit them. Normalized counter windows and actual
probe JSON are committed. Matching source contract:
https://github.com/PipeWire/pipewire/blob/1.6.2/src/modules/module-profiler.c
https://github.com/PipeWire/pipewire/blob/1.6.2/src/tools/pw-profiler.c

## Backend continuation after design review

Use callback flags plus independent server/node telemetry for sustained tests;
record the two counters separately. The one-off diagnostic proves detection of
whole-client pauses, not all GC/server/hardware failure modes or physical latency.
Then address latency configuration, startup, device loss/reconnection and the
full reference workload before broad DSP/plugin migration. 128-frame operation
still needs its own calibration. The full Phase 6 gate remains open.

Run tests serially with MSBUILDDISABLENODEREUSE=1. No builds/full suites alongside
hardware timing captures; no tracked edits during verifier hashing. Preserve the
frozen Web adapter, website bundles and historical fixtures. New profiler tools
are Linux diagnostic harnesses, not production DAW lifecycle code.
