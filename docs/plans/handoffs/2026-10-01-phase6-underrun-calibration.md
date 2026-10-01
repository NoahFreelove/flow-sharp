# Phase 6 — underrun calibration, 2026-10-01

Phases 0–5 complete; Phase 6 backend in progress. UI implementation remains deferred;
finish backend and Flow plugins first. The owner now has an MVP design brief at
`docs/plans/2026-10-01-daw-mvp-design-brief.md`. Commit verified slices, do not push.

## Completed slice

P6-08 adds muted-only callback starvation to CallbackRenderProbe: explicit opt-in,
one 100 ms sleep after 375 callbacks at 256 frames. No flags are invented; ordinary
measurements leave it disabled. Reports include injection records and up to 100
native underflow callback records, with explicit truncation status.

The probe accepts an exact HOST/NAME selector, resolved under the native lifecycle
lock in the same initialization as open. Indexes shifted during exploration, so
use names for reproducible captures. Missing/ambiguous names fail, without fallback.

`scripts/ci/audio_underflow_check.py` runs an 8-second baseline plus three 8-second
starvation trials. It requires a clean baseline, measured injection, correlated
native flag, continued callbacks and matching complete captures. It returns failure
for undetected starvation; no hardware availability is silently treated as a pass.

At 48 kHz / 256 frames, ALSA/pipewire and direct HDA ALSA each passed: baseline
zero, then one flag in each trial. PulseAudio/Default Sink failed all three, the
expected negative control. ALSA/pulse also failed a single exploratory trial.
PipeWire flags occurred immediately after the stalled callback in all trials.

A separate 60-second isolated-load ALSA/pipewire run captured 11,253 callbacks:
body median/p99/max 0.104/0.209/2.049 ms, gap max 5.624 ms. Zero flags, allocations,
parent GC, faults, dropped samples or body-budget misses. No settings changed.

## Remaining observability gap

Whole-process 250 ms SIGSTOP/SIGCONT on ALSA/pipewire produced no flag, whereas
direct ALSA hardware produced one. That is a different failure mode from blocking
only the rendering thread. Neither scenario alone certifies managed GC pause or
server/hardware coverage. Generic reports still label backend observability
unverified; separate assessments certify only the exact recorded scenario/config.
Counts are flags, not exact lost periods. Keep the full Phase 6 gate open.

## Next ready slice

Validate independent server/native telemetry for whole-process starvation. Local
`pw-profiler` supports `-J` JSON and `-n` sample bounds. PipeWire 1.6.2's profiler
source exports driver/follower xrun counters and timestamps:
https://github.com/PipeWire/pipewire/blob/1.6.2/src/modules/module-profiler.c

Investigate collecting that stream in a separate process while pausing only the
probe. Correlate the actual node/driver, distinguish counters and their resets,
prove the observer stayed live, and retain missing/truncated data as unknown.
An idle schema query was attempted but yielded no active profile data; runtime
coverage has NOT been validated. Do not change global server settings silently.
If profiler counters lack the needed coverage, evaluate a direct supported API.

Then assess latency, startup and recovery before broader DSP/plugin work. The
128-frame mode and full reference workload still need their own measurements.

## Verification and evidence

Focused platform tests 13/13; Python assessment tests 5/5; full 3,123 main + 21 MIDI
passed, 19 skips, no failures/tracked mutations. Web smoke passed. New tests cover
muted-only one-shot injection, unchanged flags and negative assessment cases.
Existing warmed native callback allocation test remains zero.

Evidence: `docs/baselines/phase6/underruns/`; decision:
`docs/decisions/2026-10-01-underrun-calibration.md`. Raw logs and TRX are in
`/tmp/flow-underruns/` and `/tmp/flow-underruns-verification/`. Original 30-minute
and cadence reports remain unchanged. Do not commit raw server snapshots with
machine/user metadata.

Run tests serially with MSBUILDDISABLENODEREUSE=1; no tracked edits during verifier
hashing. Restore Desktop after Web publish. Separate builds/full suites from
hardware captures. Preserve the frozen Web adapter and website bundles.
