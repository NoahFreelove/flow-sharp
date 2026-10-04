# Persistent source sample grants — 2026-10-04

Project schema 14 stores sorted, bounded `ProjectSource.AssetGrants` (up to 32
distinct nonempty GUIDs). Older sources default to no grants; an older schema tag
cannot carry nonempty new grants. Grants belong to ordinary generators, not editable
note sources or packaged plugins, whose dependency mechanism remains separate.
Saved missing asset references remain repairable; requesting a build validates that
the selected metadata exists and worker resolution verifies its hash/content.

`ProjectAssetGrantCommands.Set` changes a source's grants in one captured undo action
and ignores equal selections. `dawAssetGrants` exposes the same construction path to
Flow. Source acceptance and graph/note reconstruction preserve grants; Flow export
preserves them through its resource seed and reconstruction steps.

An explicit `RequestBuildWithAssets` stores its captured selection in the same undo
transaction as successful output acceptance. Subsequent ordinary `RequestBuild`
calls reuse the saved source selection. Failure/staleness never changes grants.
Changing grants while a build runs prevents its old selection from being accepted,
including builds that started without assets. Asset-backed builds also retain the
ChangeVersion ABA guard from the preceding handoff.

Focused host/API verification: **8 passed**, `/tmp/flow-generator-grants-focused.log`.
The host lifecycle test now covers persisted grants, Flow export/authoring, an
ordinary rebuild without resupplying IDs, grant revocation undo/redo, unknown asset
rejection, changed file failure and stale edit/undo rejection.

Affected backend/API regression: **537 passed**, zero failures/skips,
`/tmp/flow-generator-grants-regression.log`. Existing tests were expanded, so the
count is unchanged. `git diff --check` passed; full core/browser/hardware checks
remain pending.

Next: project tuning snapshots exposed/applied to generator evaluation and retained
through process/persistence, plus remaining legacy sample/style authoring integration.
Then integrated lifecycle and full core/browser qualification from the readiness
audit. Native UI and historical callback-gap diagnosis remain deferred.
