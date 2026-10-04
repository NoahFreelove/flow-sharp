# Flow automation authoring and preview — 2026-10-04

P7-09 exposes the persisted musical automation model through Flow construction,
export and shared preview processing. The overall backend-readiness goal is active.

## Implemented

- Flow `DawAutomation` / `DawAutomations` types and `dawCurve` construct the same
  immutable project lane: stable lane/graph binding IDs, node/parameter, a
  quarter-to-value dictionary and linear/step selection. Points sort by quarter and
  pass the same finite/ordered/budget validation as project curves.
- `FlowAutomationExporter` emits executable construction code preserving IDs,
  targets, values and shapes. Explicit IDs are intentional for reconstructed project
  code; user-facing template convenience and generated named-target binding remain
  future authoring work. The exporter currently requires at least one lane.
- `dawProcess` overloads accept one or multiple stereo buffers, musical curves and
  an explicit quarter-to-BPM dictionary. They use exactly the project tempo-aware
  lowering and prepared audio graph, including effect tails and output budgets.
  Explicit preview curves apply to the supplied graph; their saved graph binding
  identity is retained but is not resolved by the buffer preview function.
- Musical lowering now lives in Studio.Model so Flow and Studio.Engine share it
  without adding an interpreter-to-engine dependency. The existing engine API
  forwards to the shared implementation.

## Verification

All five focused Flow/project automation tests passed. They execute exported
curves, compare identities/points/shapes, cover tiny/negative values and step curves,
and compare every single/multiple-bus preview sample against shared project
lowering across a tempo change. Log: `/tmp/flow-curve-authoring-final.log`.

The affected StudioModel/PlatformAudio/MusicModel/StaticBinding/Hosting regression
run passed **256 tests**, 0 failed. Log: `/tmp/flow-curve-authoring-regression.log`.
Existing analyzer warnings remain; `git diff --check` passed. No full-solution run
or Web publish was performed.

## Remaining

Full project export/reconstruction, generated curve-result transport/named bindings,
recorded automation modes and modulation ports remain open. `dawResult` still
accepts score/audio/graph/instrument roles; a `DawAutomation` value alone is not a
new worker-result role. Hosts can evaluate exported curves and commit them through
project automation commands. Native frontend and historical callback-gap diagnosis
remain deferred. No hardware qualification was performed.
