# Selected-clip processing transactions — 2026-10-04

P7-44 connects bounded processor execution to captured project edits and playback
preparation. The previous handoff is `2026-10-04-note-processor-input.md`.

## Operations and ownership

`AudioClipProcessingOperation` captures one audio clip, its exact sample window,
package and declared parameter defaults/overrides. `NoteClipProcessingOperation`
captures one score clip's authored note window with distinct occurrence identities.
Both expose `IClipProcessingOperation`, own an independent generation context and
accept only their matching source identity/revision/context. They leave global
project parameters unchanged. Captures are transient and control-owner confined.

`ProjectDocument.ChangeVersion` increments for every installed edit, undo and redo.
Operations reject completion after any intervening document change, including
edit/undo returning to the same snapshot. No-op edits do not invalidate captures.
Acceptance adds a new source and replaces only the selected clip in one captured
history action. Original sources and other clips remain available. Redo reuses
accepted contents and identities without running Flow. Invalid/empty results fail
before document mutation; zero notes in a positive-duration window are allowed.

`ProjectClipProcessorHost.ProcessAudio` and `ProcessNotes` use an owned isolated
worker, one operation at a time. Poll applies completed results on the control
thread and requests playback preparation. Cancellation, stale captures and disposal
cancel/join the worker. Dispose the processor host before its playback session.
Ordinary generator builds still do not guess a selected processor input.

## Note settings and source windows

The worker's flat note envelope does not encode source section playback settings.
Acceptance adapts that envelope back into a score with the original section
settings, preserving gain/pan/pedal and source-window placement/nudge. Only affected
repeat occurrences are expanded; unaffected prefix/suffix repeats remain shared.
Captured note removal is occurrence-specific. Returned notes with unchanged onset
and duration retain their original boundary onset/duration/exact-duration annotation
when returning to their original section, so a trimmed staccato/pedal note does not
change sound merely because its processor input was clipped. Unchanged voice IDs
are restored inside their original sequence. Notes are ordered by onset on rebuild.

Moved/new notes use destination-section settings. New source extension has default
section settings at the project context's initial BPM; project timing remains the
playback authority. Output duration becomes clip duration; source offset is retained.
Audio replacements instead start at frame zero and use output frame duration,
including tails. Notes beyond the old clip window are not silently exposed when a
processor extends it. Section-local reverb/voice-pool and portamento retain existing
playback limitations; processing does not add support for them.

Stored note processor contents are the host-adapted score, while worker transport
validation still requires a single flat window. Saved package, actual parameter
values, contents and clip references survive ordinary project persistence and Flow
project export. Reconstruction enforces existing score interchange budgets before
acceptance (10,000 sections/placements, 100,000 sequences, 1,000,000 notes/bars,
32 Mi-character score envelope), with an early bound on expanded copies.

## Remaining scope

External audio passed to the capture API must already be decoded and hash-verified
by the project asset resolver. Single selected audio clips supply one bus; multiple
input buses remain available through the worker API, without a selected-clip mapping
workflow yet. Inline audio remains limited to 16 MiB, and worker request limits can
be tighter after encoding. Long streaming audio is not implemented by these APIs.

Next: integrated project bounce/stems, autosave/recovery, and an explicit audit of
remaining backend requirements before JUI integration. Hardware qualification is
still separate; native frontend work and historical callback-gap diagnosis remain
deferred. Do not mark the overall backend goal complete from this checkpoint.

## Verification

- Initial note/host focused suite: **7 passed**, `/tmp/flow-note-transaction-tests.log`.
- Final affected backend suite: **486 passed**, zero failures/skips,
  `/tmp/flow-note-transaction-regression-final.log`.
- Browser-target compatibility: **392 passed, 7 skipped**, zero failures,
  `/tmp/flow-note-transaction-web.log`. This is the browser test build, not a new
  WebAssembly AppBundle publish or physical-device qualification.
- Desktop Release solution restore/build succeeded afterward,
  `/tmp/flow-note-transaction-desktop-restore.log`; `git diff --check` passed.
- An earlier affected run passed 485 and failed the existing native callback
  zero-allocation assertion (3,992 measured bytes). Its isolated suite subsequently
  passed all 14 tests (`/tmp/flow-note-transaction-callback-check.log`), followed by
  the clean complete affected rerun above. Cause is unconfirmed; no callback code
  or allocation assertion was changed. Keep this observation visible for hardening.
- Tests cover playback equality of identity processing with repeated/split sections,
  staccato/pedal/gain/pan and nudge, save/reopen and executable Flow export, source
  extension, note removal/movement across settings, stale edit/undo rejection,
  atomic invalid-result failure, exact history, real worker application/publication,
  cancellation and joined disposal. No physical device was qualified.
