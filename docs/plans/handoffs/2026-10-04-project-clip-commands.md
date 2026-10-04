# Blank note clips and project clip commands — 2026-10-04

P8-09 adds blank editable content and captured arrangement gestures. The overall
backend-readiness goal remains active.

## Implemented

- `CreateBlank` installs a fixed-length empty score, editable source and linked clip
  in one action. Track identity and source/clip IDs are validated. Section/sequence
  IDs are captured in the result, so redo never creates different identities.
- Blank content has editable ownership without invented generated-source provenance:
  `EditableSourceOrigin.SourceId` is nullable, with null meaning newly authored
  content and provenance revision zero. Converted content still retains its actual
  original source/revision. Project schema version 5 records this distinction;
  earlier versions remain readable.
- Project-level move, duplicate and delete validate the complete score/audio
  selection, preserve source references and install one captured snapshot per
  gesture. Duplicate assigns explicit new clip IDs and shares source windows.
- Score and source-frame audio split wrap the existing timing-aware split kernels
  as project commands. All placement/source-window/nudge policies remain shared.
- Source/device/asset/automation state survives arrangement edits. Deletion removes
  clips but retains sources/assets for undo and future reuse. Invalid multi-clip
  operations leave both the project and its history unchanged.

## Verification

The StudioModel/MusicModel regression run passed **210 tests**, 0 failed. New
coverage creates blank content, draws a note, duplicates/moves/splits/deletes clips,
checks captured undo/redo and persisted ownership, rejects invalid gestures atomically,
and verifies mixed score/audio moves plus exact audio-frame splits. Log:
`/tmp/flow-project-clip-commands.log`. Existing analyzer warnings remain;
`git diff --check` passed. No full-solution run or Web publish was performed.

## Remaining

Clipboard/cross-project transfer, explicit source-duration resizing, instance edits
inside repeated sections, complete Flow project construction/export and frontend
integration remain open. Automatic unused-source/asset cleanup must account for
history and saved projects. Native frontend and historical callback-gap diagnosis
remain deferred; no hardware test was performed.
