# Compatible plugin reload — 2026-10-04

P7-41 protects accepted device instances against silent reinterpretation when a
new package is built under the same project source ID.

## Acceptance contract

ProjectDocument.Accept calls PluginReloadContract before constructing or committing
a replacement when both old and new sources are packaged plugins. Stale ticket and
generation-context checks still run first. A rejection does not change the snapshot,
history, bindings, accepted package or values. ProjectGeneratorHost surfaces it as
a failed completion and does not schedule replacement playback.

In-place reload requires the same plugin ID, kind, state schema/policy, audio/note
ports and declared output layer. Every existing public parameter must remain with
the same unit, scale, range, enumeration labels, smoothing and rebuild behavior.
Display metadata, source/package version, internal target mapping and defaults can
change; new parameters can be added. This is conservative compatibility, not an
automatic schema migration system.

Reload captures all old effective public values, including parameters which used
implicit defaults. A changed manifest default therefore does not silently change
that instance's setting. Newly introduced controls use their new defaults. Explicit
ResetParameter adopts the new default. Undo restores the previous package and values;
redo restores the captured replacement. Changed DSP code can intentionally change
sound—the guarantee concerns instance settings and interpretation, not identical
audio from arbitrary edited code.

Public automation retains stable public IDs and follows validated new target
mappings. Direct graph-node automation opts into implementation IDs: its target node,
device ID/version and parameter must remain. Removing or changing those targets
rejects reload before document mutation. Existing preparation validation still
handles graph/resources, format limits and processing budgets.

Breaking revisions can be built under a new source ID and assigned explicitly,
retaining the old instance. Automatic conversion of units, ranges or state schemas
is not implemented. Ordinary non-package RequestBuild remains the existing explicit
path to remove a plugin declaration and edit that source as ordinary Flow code.

## Evidence

- Focused parameter/reload tests: **22 passed**, zero failures.
  `/tmp/flow-plugin-reload-focused.log`.
- Final affected backend/platform/music/hosting/module tests: **474 passed**, zero
  failures or skips. `/tmp/flow-plugin-reload-regression.log`.
- Tests reject changed units, ranges, scales, identity and state schema without
  replacing sound/document/history. They verify default preservation through
  save/reopen and undo/redo, explicit default reset, successful public automation
  target changes, and rejection of direct automation retargeting.
- An isolated host test verifies failed completion/retained acceptance for an
  incompatible schema, followed by successful creation as a separate instance.
- `git diff --check` passed. Full core/Web checkpoints in the effect-chain handoff
  predate this slice; module signatures and project schema did not change here.

## Continue

Implement the remaining declared plugin categories: note-transform and offline-audio
input/result contracts, using bounded isolated evaluation and captured acceptance.
Generic generators already emit notes/audio but are not a substitute for processors
that consume selected input. Then integrate bounce/stems and autosave/recovery into
the host workflow and audit backend readiness against the roadmap. Hardware
qualification remains distinct. Native JUI frontend and callback-gap diagnosis remain
deferred; the overall goal is active.
