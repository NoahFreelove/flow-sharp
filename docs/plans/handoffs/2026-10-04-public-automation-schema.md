# Public plugin automation persistence — 2026-10-04

P7-32 establishes the persisted/Flow contract for public plugin automation. Playback
lowering is deliberately incomplete and explicitly rejects these lanes for now.

## Implemented

ProjectAutomationLane now distinguishes GraphNode and PluginParameter targets.
For public plugin lanes, GraphBinding identifies the stable output binding and
ParameterId identifies the declared public control. NodeId must be empty; internal
node identity is resolved through package metadata rather than persisted in this
lane. Existing musical quarter positions, step/linear shapes and point budgets apply.

Project schema 10 serializes TargetKind. Schemas 1–9 remain readable with default
GraphNode semantics; older versions containing public plugin targets reject.
Duplicate lane-target checking includes target kind. Captured automation commands
preserve undo/redo. Public parameter edits reject when a lane owns that public ID.

`dawPluginCurve` constructs public lanes from stable IDs and musical point maps.
FlowAutomationExporter and whole-project export preserve target kind. A public
lane cannot be passed directly to MusicalAutomationCompiler: it must first resolve
to concrete prepared targets. ProjectCompiler currently rejects projects containing
public lanes instead of silently omitting their audible effect. Existing graph-node
automation playback is unchanged.

## Verification

Focused persistence/export/history and module-surface tests passed (three tests,
`/tmp/flow-public-automation.log`). They check exact project export reconstruction,
schema version rejection, target preservation and captured undo/redo. Module
snapshots deliberately add one function: native 697→698, reachable 691→692, with
the same six legacy unreachable signatures. `git diff --check` passed.

All **380** affected backend/module regressions passed with zero failures
(`/tmp/flow-public-automation-regression.log`). Full core/Web gates were not
repeated in this slice.

## Next — required to finish this feature

Resolve public IDs against selected plugin bindings, validate all authored values
and automate prepared voice targets on the project clock (not restarted note-local
clock). Preserve tempo-aware interpolation, seek/loop behavior, ownership conflicts
and no callback allocations. Replace the temporary explicit preparation rejection
only after audible playback parity is tested. Then continue sampler/recording and
remaining DAW backend work. Native frontend and historical gap diagnosis remain
deferred.
