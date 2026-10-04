# Captured live MIDI pitch map — 2026-10-04

Backend goal remains active. Native frontend and historical callback-gap diagnosis
remain deferred.

## Verified checkpoints

- Browser compatibility: 424 passed, 7 skipped, zero failures
  (`/tmp/flow-backend-web-current.log`).
- Actual WASM publication succeeded (`/tmp/flow-backend-wasm-current.log`), then
  Desktop solution build succeeded (`/tmp/flow-backend-desktop-restored.log`).
- Combined backend lifecycle test passed: isolated plugin build, editable notes,
  clip/timing edits, automation, undo/redo, save/recovery, Flow reconstruction,
  mix/stem/MIDI export (`/tmp/flow-backend-lifecycle.log`).

## Current change

`Flow.Music.Model.MidiPitchMap` captures exactly 128 finite, nonnegative frequencies.
Zero explicitly represents an unmapped silent key. The constructor copies input;
the map exposes no mutable storage. The existing default is 12-TET A4=440.

`PreparedLiveInstrument` now accepts a captured map through an additional constructor;
the original constructor signature remains. The callback performs a table lookup
and ignores unmapped note-ons. No parsing, filesystem access or Flow evaluation
is introduced into the callback.

Focused live instrument suite: **11 passed**, zero failures/skips
(`/tmp/flow-live-pitch-map.log`). Added evidence compares a 432 Hz live voice with
scheduled playback, verifies unmapped silence and immutable capture, and rejects
invalid maps. Existing waveform/graph/sample, sustain, overflow and control tests
remain green.

## Next required work

This is the lower-level transport for tuning, not completed project integration.
Resolve project tuning through the existing Flow pitch conversion on the control
side; pass the same captured map to prepared monitoring and recording sessions.
Recording must preserve the resolved note frequencies and handle unmapped keys
consistently. Test Scala/keyboard maps and non-equal-temperament project keys through
the actual project hosts. Then finish sample/style authoring review and final
requirement-by-requirement qualification/documentation reconciliation. Hardware
qualification is still separate and open.
