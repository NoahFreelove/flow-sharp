# Phase 6 — callback cadence audit, 2026-10-01

Phases 0–5 complete; Phase 6 backend in progress; UI deferred. Finish backend and
Flow plugins before DAW UI. Commit verified slices without pushing.

## Findings

The exact old 15.686 ms gap is not retrospectively diagnosable. Two new 60-second
isolated-load runs reproduced long-gap/catch-up behavior with native timestamps:
256 frames: gap 8.756 ms then 1.829 ms, body 0.294 ms. 128 frames: gap 22.327 ms,
body 0.081 ms, then rapid catch-up. Native current-time deltas match entry gaps;
DAC timestamps progress one block, consuming then replenishing queued output lead.
This points before managed processing, but does not identify a specific OS event.

PipeWire snapshots: 256-frame driver quantum at 48 kHz for BOTH user block sizes.
Pulse target length 6144 bytes = 768 frames = 16 ms; minimum request 2048 bytes =
256 frames. Roughly half the 128-frame callbacks are back-to-back. Native median
output lead is ~20.488/22.521 ms, not zero; these are backend estimates, not measured
hardware latency. Minimum lead at the 22.327 ms gap was 4.288 ms.

**Important correction:** the installed PortAudio reports e1b70d33. Matching
PulseAudio source passes zero application callback flags and increments a private
underflow counter without public access. Earlier zero flag counts cannot certify
zero device underruns. Its StreamInfo latency is buffer-processor-only. The
30-minute managed-body result stands; device reliability remains unverified.
See docs/decisions/2026-10-01-callback-cadence.md for pinned primary-source links.

## Changes and verification

- Host API identity and explicit underflow observability in reports. Unknown
  backends remain unverified; none are implicitly declared reliable.
- Native current/DAC timestamp copies and full flags in bounded callback records;
  nonfinite timestamps become missing. Longest-gap neighborhoods and cadence
  distributions are computed off-thread after native teardown.
- Current JSON calls the raw counter outputUnderflowFlagCount. Historical raw
  reports are untouched; callback-stress-assessment.json records the correction.
- Focused **105/105**; full **3,117 main + 21 MIDI passed, 19 skips**, no failures
  or tracked-file mutations. Web smoke passes. Warmed timestamp-enabled callback
  still allocates zero. A new fixture verifies native time copying/nonfinite data.
- Both device runs had zero parent GC, allocations, faults or dropped samples.
  128-frame startup still crossed the 70% body target once; no body deadline miss.

Evidence: docs/baselines/phase6/cadence-*.json. Raw logs/server snapshots/source
in /tmp/flow-cadence/ (server raw files contain local identifiers; only sanitized
properties are committed). Full verification in /tmp/flow-cadence-verification/.
No global device/server settings changed; no builds/suites overlapped captures.

## Next ready slice

Obtain trustworthy underrun/device timing telemetry. Evaluate another native host
route, a supported upstream fix, or a direct platform adapter; validate its error
signals before any zero-underrun gate. Then measure configurable low-latency
buffering and startup/recovery. Smaller user callback size alone does not reduce
driver quantum or total queued latency. Do not port broad DSP or close Phase 6
based solely on the managed-body pass. General DSP/plugin work remains open.

Run tests serially with MSBUILDDISABLENODEREUSE=1 and avoid tracked edits during
verifier hashing. Restore Desktop after Web publish. Keep timing captures separate
from builds/suites; preserve browser JS and website bundles.
