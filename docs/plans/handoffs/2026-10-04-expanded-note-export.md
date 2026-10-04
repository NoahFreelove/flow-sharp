# Expanded note-event export — 2026-10-04

P7-14 expands score events into explicit Flow construction while retaining score
structure metadata. The overall backend-readiness goal remains active.

## Implemented

- `DawNote` / `DawNotes` and `dawNote` construct detached events with stable identity,
  voice, source offset/duration, spelling, resolved Hz, velocity, articulation,
  tie, overlap and portamento. Rest values use empty spelling/zero Hz. Optional
  rational-duration and provenance metadata remain explicit string arguments.
- Export emits per-event constructors, retaining custom tuning directly rather
  than converting through MIDI. Negative numeric values use Flow's `neg` form;
  optional cent metadata uses invariant round-trip text.
- `dawProjectNotes` installs the complete sequence event list in a pure project
  construction step. Shared section occurrences retain one replacement section;
  all IDs, durations, bars and section settings remain unchanged. Restored authored
  metadata passes the existing composition interchange validation.
- The resource seed contains empty note lists for sequences reconstructed by the
  export. Actual musical events therefore appear in code, not just snapshot data.
  Source historical code/context and editable ownership remain preserved, with no
  generator execution or reverse rewriting of original source code.

## Verification

All six focused project-export tests passed after correcting reserved-word
parameter names in the library declaration. New coverage preserves tuned frequency,
negative offsets, accidentals/cents, rests, articulation/ties/overlap/portamento,
exact tuplets, provenance and shared repeated sections through exact serialized
project equality. Log: `/tmp/flow-note-export-repaired.log`.

The affected StudioModel/PlatformAudio/MusicModel/StaticBinding/Hosting regression
run passed **271 tests**, 0 failed. Log: `/tmp/flow-note-export-regression.log`.
Existing analyzer warnings remain; `git diff --check` passed. No full-solution run,
hardware capture or Web publish was performed.

## Remaining

Section/bar/source metadata and tempo maps still use the snapshot seed. Note
constructors are verbose; convenient authoring forms can layer onto them. Large
scores with many sequences still need reconstruction performance qualification,
since each sequence currently installs a fresh immutable project result. Full
live/offline/export qualification and remaining audio/plugin requirements stay
open. Native frontend and historical callback-gap diagnosis remain deferred.
