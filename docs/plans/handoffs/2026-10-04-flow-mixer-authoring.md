# Flow-authored track creation — 2026-10-04

P8-14 adds a prepare/commit operation for creating a track and expanding its Flow
routing together. The full backend-readiness goal remains active.

## Implemented

- `ProjectMixerAuthoring.BeginAddTrack` captures the current project and an explicit
  destination node/input edge. It chooses an unused bus, adds input/gain/sum nodes,
  and inserts the sum before that edge. Existing downstream processors retain their
  order and affect both old and new inputs. The default project's `masterGain`
  input is the normal insertion point.
- The expanded definition is exported as canonical Flow generator code. `Prepare`
  evaluates that fixed construction code through the normal Flow generator adapter
  on a worker, returning detached graph output. Arbitrary user source edits still
  belong to the isolated generator host.
- `Commit` accepts the prepared source and adds the track, changes the selected
  graph and remaps active graph automation in one captured history action. The
  original generator source/results are preserved; its code is never rewritten.
  Existing node IDs and automation point data are retained.
- The resulting mixer is a separate saved canonical source. Subsequent regeneration
  of the original graph source does not implicitly rewrite this captured graph.
  This is explicit graph construction ownership, not bidirectional rewriting of
  an arbitrary user program.
- Preparation does not mutate the document. Any intervening document edit makes
  the request stale; commit refuses it without a partial source or track addition.
  Undo/redo uses the captured project snapshots and does not rerun Flow.
- `ProjectDocument.Accept` gains a dependent-transform overload. The candidate
  source and transform are fully built before the single history commit; transform
  errors leave the document/history untouched.

## Host contract

Begin and Commit run on the document's control owner; Prepare runs on a bounded
worker and accepts cancellation. A host that abandons/cancels a request discards
its exposed ticket via `Document.DiscardBuild`. After successful commit, request
playback preparation through the session. Automatic scheduling of these mixer
requests alongside code-edit requests is still a host integration task.

Insertion points are explicit; arbitrary custom graph topology is not guessed.
Bus indices already referenced by the graph are reserved even if a track was
removed. The existing track-add command can reuse a deliberately exposed unused
input; expansion does not silently overwrite that route. All 64 graph input slots
remain the engine limit.

## Verification

Initial focused mixer/source-acceptance tests passed (10 tests):
`/tmp/flow-mixer-authoring.log`.

New coverage checks one-action source/routing acceptance, exact undo/redo snapshots,
master automation remapping, old source preservation, audible two-track summing,
save/reopen audio parity, stale/cancelled preparation, invalid insertion points and
failure inside the dependent transform without partial acceptance.

All **303** affected backend and module-characterization tests passed, 0 failed
(`/tmp/flow-mixer-authoring-regression.log`). Existing analyzer/package warnings
remain. `git diff --check` passed. The full core gate and Web publish were not
repeated in this step.

## Next

Integrate bounded mixer-gesture scheduling and parameter/effect edits into the host,
continue remaining Flow device/plugin and MIDI input/recording work, and preserve
broader compatibility gates. This is backend authoring, not a native UI implementation
or new physical-device qualification. Historical callback-gap diagnosis remains
deferred.
