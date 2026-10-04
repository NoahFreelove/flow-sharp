# Live mixer parameter preview — 2026-10-04

P8-19 connects continuous parameter preview to the active audio graph and captures
its final value as one Flow-backed document action. Backend readiness remains open.

## Implemented

- `PreparedAudioGraph.SetLatestParameter` validates through the same catalog and
  automation ownership rules as queued edits. One latest-value mailbox per
  prepared parameter coalesces bursts, including cancellation restoration.
  Audio consumes these after FIFO commands at a nonempty block boundary and uses
  the existing smoothing. Control calls allocate; consumption does not allocate,
  block, run user code, or depend on available FIFO capacity.
- `ProjectPlaybackCoordinator` owns at most one opaque parameter preview against
  the acknowledged project. It rejects a new preview while publication or
  preparation is pending, or the document differs from the active snapshot.
  Structural and automation-owned parameters cannot be overridden.
- Preview changes neither document nor history. Cancellation restores the saved
  value through the mailbox. An unrelated document edit invalidates preview;
  subsequent updates fail. Hosts must continue their normal poll loop.
- `ProjectMixerHost` exposes begin/update/cancel/commit. Commit freezes the final
  value and passes it through canonical Flow preparation and a captured action.
  Failed/stale gestures restore saved sound. Success keeps the preview on the old
  graph until the prepared replacement is acknowledged; failed playback
  preparation restores the old audible project while retaining the saved edit.
- Shutdown restores active/queued previews before joining workers. All host and
  coordinator methods retain the single control-owner requirement. The preview
  is exclusive; automation recording and multiple simultaneous control gestures
  are not implemented here.

## Verification

Six added tests cover a 10,000-update burst, per-parameter coalescing, full FIFO
independence, zero callback allocations while consuming changes, audible preview
and cancellation, one-action commit/undo, worker failure, shutdown restoration,
unrelated edits, automation/structural rejection and playback preparation failure.

All **324** affected backend/module tests passed, zero failures:
`/tmp/flow-mixer-preview-regression-final.log`. Package/analyzer warnings remain.
Physical qualification and Web publishing were not repeated in this step.

## Next

Graph publication still replaces playback stopped at zero. Transport preservation,
state/tail continuity, public user-authored Flow plugin primitives, MIDI input and
recording, and integrated host workflows remain required backend work. Preview
alone does not complete the mixer lifecycle or full frontend-readiness gate.

JUI 0.10.0 is now available and its packaged managed/native headless smoke passed;
see [the package integration note](2026-10-04-jui-package.md). Native UI implementation
and historical callback-gap investigation remain deferred as agreed.
