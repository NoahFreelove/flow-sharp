# Immutable note-editing kernel — 2026-10-04

P8-07 begins piano-roll backend operations in the shared Music.Model layer.
Editable project-source ownership and document commands are the next required
step; this is not yet a complete project piano-roll editing feature.

## Implemented

- `NoteEditing` adds, deletes, moves and resizes notes, changes velocity and assigns
  resolved pitch/rest values. Pitch assignment takes explicit frequency so custom
  tuning is preserved instead of implicitly converting through MIDI.
- Edits return immutable sequences. Touched notes are validated before any new
  result can be installed. Invalid selections, duplicate IDs, nonfinite/out-of-range
  values and edits outside the fixed sequence duration are rejected atomically.
- Identity, provenance, articulation and other unaffected controls are retained.
  Resizing clears the old exact rational duration, avoiding stale tuplet metadata.
  Notes are stably ordered by onset; sequence/bar duration stays fixed.
- `ScoreEditing.EditSequence` replaces the selected sequence in its shared section,
  preserving section/placement identities and repetition. Every placement that
  shares that section gets the same immutable replacement. Ambiguous IDs fail.
  Following placements cannot move because duration changes are rejected.
- Work/collection limits are bounded to 100,000 notes/selections for these APIs.
  These operations run on the control/preparation side, not the callback.

## Verification

The final StudioModel/MusicModel regression selection passed **206 tests**, 0 failed.
New coverage verifies immutable move/resize/velocity/pitch/rest edits, tuning and
provenance preservation, duplicate/missing IDs, atomic multi-note rejection, and
shared repeated-section updates without timeline shifts. Log:
`/tmp/flow-note-editing-regression.log`. Existing analyzer warnings remain;
`git diff --check` passed. No full-solution run, hardware capture or Web publish.

## Next work

Add an explicit generated-to-editable source conversion with provenance, persisted
ownership and undoable note commands. Generated sources must remain code-owned;
these pure edit functions do not authorize silently rewriting generated outputs.
Then expose corresponding Flow construction/edit operations and qualify full
save/reopen/playback/export parity. Native frontend and historical callback-gap
investigation remain deferred.
