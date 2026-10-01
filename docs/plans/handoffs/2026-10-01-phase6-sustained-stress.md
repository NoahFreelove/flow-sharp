# Phase 6 — sustained isolated-load stress, 2026-10-01

Phases 0–5 complete; Phase 6 backend in progress. Backend/Flow plugins precede UI.
Commit verified slices, do not push. Previous callback prototype handoff contains
adapter ownership/build details; this handoff supersedes its next-step status.

## Completed

Owner requested the 30-minute stress test. Ran the existing muted 32-voice/two-
sequence reference workload at 48 kHz/256 frames with isolated Flow/file/hash/
forced-GC load. Same reference machine and PortAudio/PipeWire Default Sink routed
to MOONDROP USB as P6-05 (local index 31; enumerate afresh before reusing).
No builds or suites ran alongside capture. The probe and worker exited normally.

- 337,508 callbacks; 86,402,048 frames; first/last entry span 1799.998879 seconds.
- All-callback median 0.113212 ms; p99 0.242357 ms; worst 2.085725 ms at 147.463 s.
- Worst is 39.11% of the 5.333333 ms deadline, below the 70%/3.733333 ms target.
- Zero body deadline/headroom overruns, reported underflows, body allocations,
  parent GC collections, dropped timing records and callback faults.
- Generation 2; one playback retired. One ordinary command rejection (reason not
  individually logged), no discards. Worker performed 254,147 Flow evaluations,
  1.066 TB cached reads/hashing and 189,352/188,107/157,586 collections.

**Managed-body sustained target met; full device/Phase 6 gate remains open.**
Max callback entry gap was 15.686382 ms. The body measurement excludes native
entry/dispatch pauses and telemetry append; no reported underflow does not prove
low hardware latency. Native latency zero is unavailable. No UI load, listening,
recovery or native DSP baseline. Frequent mid-score seeks avoid loop-end/EOF stress.

## Artifacts and verification

- docs/baselines/phase6/callback-isolated-256-30min.json: exact output.
- docs/baselines/phase6/callback-stress-assessment.json: explicit checks/limits.
- /tmp/flow-phase6-stress/: raw logs and device enumeration.
- Reporting now includes span/frame count/top 20 body outliers and a duration-
  neutral limitation statement. Callback/workload code is unchanged from c43278b.
- Release build and the actual 30-minute run passed. Full suites were not rerun
  for reporting/docs only; previous suite gate remains 3,116 main + 21 MIDI passed,
  19 skips, Web smoke passed. Do not present that as a newly executed suite.

## Next ready slice

Investigate callback entry spacing and native buffering/latency: preserve times
of long gaps and use native stream time information where reliable. Determine
whether 128-frame callbacks are batched at a larger backend quantum. Keep body
and entry-gap metrics separate, and do not assume requested block size is device
latency. Address startup/preparation and device failure/recovery evidence before
final managed/native/isolation selection and broad DSP migration.

Existing processor/plugin, live-note, smoothing, sampler/effect and tail lifecycle
work remains open. UI is deferred; action-owned Undo/Redo remains Phase 8.

Run measurements without builds/tests in parallel. Tests stay serial with
MSBUILDDISABLENODEREUSE=1. Do not edit tracked files during verifier hashing;
restore Desktop after Web publish when needed. Preserve browser JS/site bundles.
