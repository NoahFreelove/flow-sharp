# Musical project automation — 2026-10-04

P8-06 connects the automation kernel to musical project data and playback.
Flow authoring/export remains the next required integration step; the backend goal
is active and automation is not yet complete end to end.

## Implemented

- Immutable project lanes target a stable graph binding, node and parameter, with
  identity, step/linear shape and strictly ordered quarter-position/value points.
  Projects enforce unique lane IDs and targets, 256 lanes and 100,000 total points.
- `ProjectAutomationCommands.Set/Remove` use captured project actions, supporting
  one edit per gesture and undo/redo without recalculating source code. Source
  acceptance and asset import/relink preserve automation.
- Project format version 3 persists lanes. Versions 1 and 2 remain readable with
  empty automation. Missing/unknown graph bindings remain representable for repair;
  only lanes for the selected graph are active. Invalid active node/parameter
  targets fail preparation rather than silently applying elsewhere.
- Lowering uses the full project tempo map. Linear musical curves insert tempo
  boundaries so a slope in beats does not incorrectly become one slope in seconds.
  Frame positions round to nearest, midpoint away from zero. When sub-frame points
  collapse onto one frame, the later point wins. Lowered point counts are bounded.
- Project compilation passes lowered curves into the existing prepared graph.
  Seek uses absolute frames; saved curves therefore restore the same playback
  values after reopen. Automation does not add duration beyond the arrangement.

## Verification

All 19 focused project-automation/persistence/import tests passed, including tempo
boundary values, deterministic sub-frame collapse, stored curve playback after
reopen, undo/redo/removal, older format migration and duplicate-target rejection.
Log: `/tmp/flow-project-automation.log`.

The final affected StudioModel/PlatformAudio/MusicModel/StaticBinding/Hosting run
passed **254 tests**, 0 failed, including automation retention during source
regeneration. Log: `/tmp/flow-project-automation-regression.log`. Existing analyzer
warnings remain; `git diff --check` passed. No full-solution run or Web publish.

## Next work

Expose authoring and reconstruction of these curves in Flow, verify exported
curves against project playback, and include them in full project export. Add
recorded automation modes and modulation ports, plus frontend repair controls for
inactive/unavailable targets. Note editing, additional effects and final quality
qualification remain open. Native frontend and historical callback-gap diagnosis
remain deferred; no hardware test was run.
