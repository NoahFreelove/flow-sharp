# Managed Flow mixer sources and parameter gestures — 2026-10-04

P8-16 establishes canonical graph ownership and adds committed parameter gestures.
The full backend-readiness objective remains active.

## Implemented

- `ProjectSource.IsManagedGraph` explicitly identifies a dedicated canonical mixer
  source. Project JSON v7 persists it; v1–v6 files remain readable and missing
  ownership defaults conservatively to user-owned. Managed sources require one
  `mixer` graph, no score/audio/instrument layers, a `generate` entry and saved code.
- A visual edit to a user-owned graph creates a managed copy, preserving the original
  code and outputs. Further visual edits reuse that source ID and output binding,
  avoiding a new retained source on every knob gesture or track addition.
- Ordinary source-code acceptance clears managed ownership. A subsequent visual
  edit copies that user-owned source again, rather than rewriting the accepted user
  program. Undo/redo restores ownership with the rest of the captured snapshot.
- The authoring request/result types are generalized to `MixerEditRequest` and
  `PreparedMixerEdit`. Track creation and parameter changes share the same worker
  evaluation and atomic commit path. Pure graph construction used by project Flow
  exports preserves the managed-ownership marker.
- `BeginSetParameter` validates node/parameter identity, catalog range/integer rules,
  and automation ownership before building canonical Flow source. Input-bus changes
  remain routing operations; direct edits to an automated parameter are rejected
  until its automation is edited or removed.
- `ProjectMixerHost.TrySetParameter` queues one completed parameter gesture using
  the existing bounded FIFO/result queue. Parameter completions have no track ID.
  One successful gesture is one captured action followed by playback preparation.

## Behavioral limits

These are committed graph edits, not a continuous live knob-preview path. They use
current replacement behavior: the rebuilt graph installs stopped at frame zero.
The frontend should submit once per completed gesture. Continuous preview, effect
insertion/reordering and automation recording still need their host workflows.
Default instrument/source behavior and original user graph code are preserved.

## Verification

All 12 focused mixer tests passed (`/tmp/flow-managed-mixer-tests.log`). Four new
checks cover queued parameter changes with exact gain-scaled audio, source reuse,
undo/redo binding stability, v7 save/reopen, executable Flow export, ownership
transfer after normal code acceptance, invalid/automated parameters and conservative
legacy migration. Malformed managed-source shapes are rejected.

All **311** affected backend/module regression tests passed, 0 failed
(`/tmp/flow-managed-mixer-regression.log`). Existing analyzer/package warnings
remain. `git diff --check` passed. Full core/Web publish gates were not repeated
in this step.

## Next

Add captured effect insertion/bypass/removal and live parameter preview semantics,
then continue broader plugin/device and MIDI input/recording requirements. Native
frontend, historical callback-gap diagnosis and physical-device qualification are
not completed by this change.
