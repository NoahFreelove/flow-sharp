# Editable project sources — 2026-10-04

P8-08 adds explicit ownership conversion and undoable note editing to the project.
The overall backend-readiness goal remains active.

## Implemented

- `ProjectNoteCommands.MakeEditable` detaches one score clip into a new source with
  a copied immutable score layer and provenance (source ID, layer, accepted revision).
  Other linked clips stay on the original source. Full source content is retained
  so existing offsets, lengths, placement and nudges remain unchanged.
- Editable sources keep note/section/placement identities and resolved tuning. They
  have no executable code, and `BeginBuild` rejects regeneration of an editable
  source. Generated sources reject direct project note edits until conversion.
- `EditSequence` applies the shared note-editing kernel and installs a complete
  captured result in one document action. All clips linked to that editable source
  see the edit. Undo/redo restores exact captured snapshots without running code.
- Project format version 4 persists editable provenance/ownership and accepts older
  project versions as generated sources. Restore validates editable content is a
  single score layer with no audio/graph/instrument outputs or executable code.
- Existing routing, assets and automation survive conversion and note edits.
  Copying a source is logical immutable sharing until an edit replaces the affected
  structures; the original source content is never changed.

## Verification

The StudioModel/MusicModel regression run passed **207 tests**, 0 failed. New
coverage verifies generated-source edit rejection, per-clip detachment, unchanged
linked clips and geometry, provenance/ownership save/reopen, regeneration rejection,
and exact conversion/edit undo/redo. Log: `/tmp/flow-editable-source.log`. Existing
analyzer warnings remain; `git diff --check` passed. No full-solution run, hardware
capture or Web publish was performed.

## Remaining

Creating blank editable clips, exposing full editable-score construction in Flow,
instance-specific edits inside repeated source sections, complete project export,
selection/clipboard commands and UI integration remain open. The existing source
container retains its historical generator-descriptor field; editable ownership
explicitly prevents that descriptor from being executed by project builds.
Native frontend and historical callback-gap diagnosis remain deferred.
