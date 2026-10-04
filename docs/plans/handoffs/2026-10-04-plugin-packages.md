# Pinned plugin packages and isolated effect builds — 2026-10-04

P7-15 connects the static manifest contract to isolated effect builds and saved
project sources. General plugin authoring and backend readiness remain incomplete.

## Implemented

- `PluginPackage` captures an immutable manifest, exact source, output layer,
  stable public-parameter targets and dependency content snapshots. Source and
  every declared dependency must match their hash/version. Missing, duplicate or
  undeclared contents fail before any build. Package JSON is versioned and bounded
  to 16 Mi characters; base64 dependency contents total at most 12 Mi characters.
  Deserialize rejects unknown/duplicate fields and never reads files or runs code.
- `ProjectGeneratorHost.RequestPluginBuild` uses the existing one-process build
  worker, cancellation, coalescing, queue budgets and control-owner acceptance.
  Processing constraints and supported kind are checked before ticket creation;
  detached output must match its declared single effect layer and parameter/port
  contract before acceptance. Failed builds preserve the previous document/audio.
- Queue memory accounting includes package serialization as well as source text.
  No document mutations occur in worker tasks.
- Accepted `ProjectSource.Plugin` retains the package in the same captured action
  as source/result/bindings. Ordinary source acceptance clears the declaration;
  visual mixer edits clone into their existing managed source ownership path.
- Project JSON schema **8** stores optional plugin package data per source and
  reads schemas 1–8. Earlier schemas cannot claim new plugin fields. Restore
  validates source/package identity without executing code. History estimates
  include retained package data. Undo/redo restore captured definitions.
- Graph/note reconstruction preserves package metadata, so full Flow project
  export and subsequent evaluation retain the exact package too.
- `ProjectCompiler` revalidates a selected saved plugin's graph and host processing
  limits before preparing playback. Mismatched saved graphs cannot be published.

## Verification

All **339** affected backend/module tests passed
(`/tmp/flow-plugin-package-regression.log`). After adding selected-plugin validation
at playback preparation, all three focused package/build tests passed
(`/tmp/flow-plugin-package-final.log`). These include a real child-process build,
exact save/reopen and executable Flow export, undo/redo, rejected port mismatch,
ordinary source ownership transition, saved-graph mismatch rejection, hash
corruption, missing dependency contents and rejection before worker launch when
pinned dependency execution is unavailable. `git diff --check` passed.

No full core/Web/hardware gate was repeated in this step.

## Remaining limitations and next work

Only dependency-free declared audio-effect builds are currently executable through
this adapter. Packages can save pinned dependency contents, but builds containing
any are explicitly rejected until a worker resolver consumes only those snapshots.
The existing worker is process-isolated, not an OS filesystem/network sandbox;
ordinary Flow imports/IO are not yet restricted by this package wrapper. Do not
claim fully hermetic plugin builds or complete portable dependency execution.

Next connect pinned module/asset resolution and isolate access to undeclared
resources; extend the graph authoring layer with parameter/signal handles and DSP
primitives. Required public Flow synth/sampler/composed-effect examples, instrument
and event plugin contracts, runtime parameter mapping, device instances/presets,
state/tail continuity, MIDI recording and integrated workflows remain open.

Native JUI frontend and historical callback-gap diagnosis remain deferred.
