# Project persistence — 2026-10-04

P8-03 adds a self-contained version-1 project file for the currently implemented
backend. The overall frontend-readiness goal remains active.

## Implemented

- Persist arrangement, project context, accepted Flow code and descriptor, source
  revision, detached generated contents, original build context and stable output
  binding IDs. Original build context may differ from current project context.
- Persist named tracks in bus order, selected graph binding and optional per-track
  instrument bindings. Source acceptance preserves routing. Project preparation
  now has an overload that resolves these saved selections directly.
- Loading executes no code. It validates schema, timing consistency, source IDs,
  output roles, binding uniqueness and exact availability against saved contents.
  Missing external routing references remain representable for repair and fail
  playback preparation rather than silently choosing a different device.
- Generated audio is self-contained inline PCM using the existing exact content
  codec; this is not yet an external asset registry/importer. Existing per-result
  16 MiB audio limit applies. Overall JSON is capped at 64 Mi characters; file reads
  additionally check a 192 MiB UTF-8 byte bound. Serialization still materializes
  JSON in memory; these limits are not a streaming memory guarantee.
- Save writes and flushes a sibling temporary file then renames over the target. The
  working document is marked saved only after success. `SaveRecovery` writes an
  immutable snapshot without marking the working document saved; the host must
  choose a distinct recovery path and schedule it. No automatic timer is installed.
- Reopened projects start with clean empty history and build revisions beyond
  accepted source revisions. Unknown future schema versions are rejected. This is
  a new format, not an implicit migration from the arrangement-only format.

## Verification

The initial focused persistence/acceptance run passed 11 tests, covering exact
serialized round-trip, exact prepared audio before/after load, preservation of
successful source and bindings, separate historical/current context, corrupt
bindings/future schema rejection, failed-save dirty state and routing retention
on regeneration. Log: `/tmp/flow-project-persistence.log`.

The final affected StudioModel/PlatformAudio/MusicModel/StaticBinding/Hosting run
passed **226 tests**, 0 failed, including recovery saves preserving working dirty
state. Log: `/tmp/flow-project-persistence-regression.log`. Existing analyzer
warnings remain. `git diff --check` passed; this was not a full-solution run.

## Next work

External asset identities/hashes/import/resolution, dependency pins, source/binding
repair operations, note editing, automation and stateful processors still need
implementation. Add host-level recovery discovery and live publication following
project commits, then end-to-end generated-project Flow export equivalence.
Current project files represent the implemented generated-source playback subset,
not every final DAW feature. No hardware capture, native frontend or Web publish
was performed. Historical callback-gap diagnosis remains deferred.
