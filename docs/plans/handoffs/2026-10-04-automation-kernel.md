# Sample-accurate automation kernel — 2026-10-04

P6-20 implements the runtime curve foundation for DAW/Flow automation. Project and
Flow authoring integration is the next required step; this is not a claim of a
complete automation feature or backend readiness.

## Implemented

- Immutable frame-domain lanes with strict ordered finite points, step/linear
  interpolation, authored-device value before the first point and final-value hold.
- Prepared graphs validate targets, device parameter ranges and automatable flags.
  Limits are 256 lanes and 100,000 points in aggregate; a parameter has at most one
  lane. Structural controls such as input bus and delay repeat count cannot animate.
- Curves apply at exact frames through gain, pan, drive and delay parameters, using
  the same processor for all graph consumers. Authored curves bypass manual-control
  smoothing; otherwise smoothing would alter the requested curve shape.
- An active lane owns its parameter. Manual queued writes to it are rejected until
  a changed graph/lane is prepared. Touch/latch/write recording modes remain future
  host work rather than silently competing with automation.
- Per-lane cursors advance during processing; seeks use binary search. Graph reset
  now accepts the absolute transport frame, restores the corresponding curve value
  and clears effect state. Playback seek/loop resets forward that position. No
  callback allocation or prefix rendering is introduced.

## Verification

The affected backend regression run passed **250 tests**, 0 failed. After the final
frame-overflow guard and direct playback-seek test, all **11 automation/delay/graph
playback tests** passed. Coverage checks exact per-frame values, step/linear behavior,
block partition independence, backward/forward resets, device validation and no
managed allocations in processing/reset. Logs: `/tmp/flow-automation-regression.log`
and `/tmp/flow-automation-final.log`. Existing analyzer warnings remain;
`git diff --check` passed. No full-solution run, hardware capture or Web publish.

## Remaining

Persist musical-time automation in the project, lower through the full tempo map,
add undoable editing and Flow authoring/export, then verify live/offline/project
round-trip parity. Current lanes are an explicit preparation API and are not yet
saved by project JSON or embedded in Flow graph results. Automation does not extend
arrangement duration by itself. Modulator ports and recording modes remain open.
Native frontend and historical callback-gap diagnosis remain deferred.
