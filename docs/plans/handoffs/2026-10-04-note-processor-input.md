# Note-transform processor input — 2026-10-04

Continuation update: captured audio and note processing transactions are now
implemented in [P7-44](2026-10-04-clip-processing-transactions.md). Earlier next-step
sections below describe this checkpoint's historical state; use that handoff for
the current continuation and verification.

P7-43 adds explicit note-window input and detached result support alongside offline
audio execution. Both processor categories now execute through bounded isolated
workers; selected-clip capture and transactional application remain to implement.

## Input and public Flow API

PluginNoteInput contains an explicit duration in quarter notes and immutable note
events relative to that window. It retains identity, voice, spelling, resolved Hz,
cent offset, velocity, articulation, ties, overlap, portamento, exact duration and
source provenance. It validates finite controls, unique nonempty IDs, note bounds
and a 100,000-note limit. Empty windows are valid and retain their declared duration.
Zero-duration events/rests are supported. This is a flat window, not an implicit
flattening of an arrangement, section repeats or an arbitrary Song.

GeneratorBuildRequest.NoteInput is exposed as the new DawNoteSequence type. A
NoteTransform builder receives `(Dict<String, Double>: context, DawNoteSequence:
input)`. Audio and note inputs are mutually exclusive and must match the manifest.
Declared typed parameters use `parameter:<id>` context values/defaults, as in the
offline-audio path. Processor controls must require reprocessing and advertise no
live smoothing; no graph parameter targets are permitted.

The public helpers are:

- dawNotes: obtain an evaluation-local array of immutable DawNote values.
- dawNoteSequence: build a validated window from that array and explicit duration.
- dawNoteDuration: read the window duration.
- dawTransposeNotes: use the shared Transposition.Apply implementation; note IDs,
  timing and other controls remain intact. Resolved Hz scales by the applied
  equal-tempered interval, preserving the original tuning offset rather than
  resolving an unrelated tuning table. Existing shared range/clamp rules apply.
- dawNoteVelocity: get velocity, or return a copy with a validated velocity.
- dawResult accepts a named DawNoteSequence, enabling ordinary map/filter and
  detached reconstruction without MIDI-file output.

The result must contain exactly the declared note layer. It lowers to one ordinary
detached score sequence with deterministic wrapper identities derived from source
and layer, retaining event IDs and context tempo. Existing generated-score playback,
persistence and Flow project export consume that result. Multiple result roles,
arbitrary Song output from a note processor, and out-of-window notes are rejected.

## Worker and budgets

Protocol **4** carries optional note input with exact metadata. Notes have a bounded
16 Mi-character interchange envelope; the existing 16 Mi-character limit on the
entire request still applies after combining source/package/input. The parent
validates returned processor output as well as the child. Native policy includes
only the reviewed pure helpers; transpose checks evaluation cancellation per note.
Other generator kinds may also return detached note windows, subject to the shared
aggregate authored-note budget.

The prior live-parameter guards apply to both processor kinds: changing saved
parameters requires a new processing operation, not a cosmetic edit to captured
output. Actual executed parameter values remain in the accepted result context.

## Example and verified checkpoint

`examples/plugins/octave-velocity.flow` and its `.flowplugin` transpose input notes
one octave and use a declared velocity parameter through the public API.

- Full core: Desktop solution build passed; **3,417 language/backend + 21 MIDI
  tests passed**, zero failures, 14 skipped. Tracked mutation audit empty.
  `/tmp/flow-note-processors-core/verification.json`.
- Browser-target compatibility: **382 passed, 7 skipped, zero failures**.
  `/tmp/flow-note-processors-web.log`.
- Actual WebAssembly AppBundle publish passed: `/tmp/flow-note-processors-wasm.log`.
  Desktop Release build restored successfully afterward:
  `/tmp/flow-note-processors-desktop-restore.log`.
- Initial focused note/audio/worker suite passed 16 tests. Final core includes the
  packaged example and rejection of misleading live-automation metadata.
- Native snapshots: **710 registered / 704 reachable**, same six legacy unreachable
  registrations. Seven new note-helper/result signatures are exposed.
- Tests cover tuned-frequency transposition, identity/control/provenance preservation,
  input immutability, exact input and real-worker output interchange, deterministic
  wrappers, empty windows, wrong/mixed/missing input, bad result roles/layers and
  invalid note windows. `git diff --check` passed. No hardware was qualified.

## Continue with captured processing transactions

Implement a host operation that captures a selected clip's exact note/audio window,
processing parameters and package, runs the worker, rejects stale completion and
applies the result with one captured undo action. Expanded repeats require unique
occurrence IDs; source offsets, nudges and clipping must be handled explicitly.
Do not flatten sections/repeats implicitly inside the note input contract.

Processor contexts contain per-operation values and may differ from the project
generation context. Do not mutate global project parameters merely to satisfy the
ordinary ProjectDocument.Accept build-ticket path. Add an explicit transaction with
the captured snapshot/input and clear ownership. ProjectGeneratorHost still rejects
these categories through its ordinary build API; do not present clip processing as
already integrated.

Then continue bounce/stems, autosave/recovery and a requirement-by-requirement backend
readiness audit. Native JUI frontend and historical callback-gap investigation remain
deferred. The overall goal is active.

## Selected-window capture follow-up

`ProcessorClipCapture` now captures source-relative authored note windows and exact
stereo audio frames. Note capture intersects only visible repeats, assigns stable
distinct occurrence IDs, isolates voices by occurrence/sequence, preserves note
controls/provenance, and clears exact-duration annotations when trimming. It has
100,000-note/occurrence and 1,600,000-candidate bounds plus cancellation. Placement
and nudge stay on the clip. Audio capture checks matching sample rates, limits the
window to 16 MiB, and pads shortened sources with silence. `PcmAsset.CopyFramesTo`
copies the selected region without allocating a copy of the entire source asset.

Focused capture tests: **3 passed**, `/tmp/flow-processor-capture-tests.log`.
Project/music model regression: **356 passed**, zero failures/skips,
`/tmp/flow-processor-capture-regression.log`. `git diff --check` passed.
This is capture plumbing, not the completed host transaction. No worker dispatch,
acceptance or UI was added in this follow-up. Before applying transformed notes,
resolve preservation of section-level playback settings (gain/pan/pedal/etc.);
these are intentionally not baked into the authored-note input. Snapshot identity
alone is insufficient for stale acceptance after edit/undo (ABA): use a monotonic
document change token or an explicitly invalidated operation capability.

## Audio transaction and host follow-up

`AudioClipProcessingOperation` captures a selected one-bus audio clip, pinned package
and declared parameter defaults/overrides. Generated audio resolves by stable binding;
external PCM must be decoded/hash-verified by the caller. Operation context remains
separate from global project parameters. Successful acceptance adds the captured
processor source and replaces only the selected clip in one history action, retaining
anchor/nudge and adopting output duration (including tails). Empty replacement audio
is rejected. Other linked clips and original sources remain unchanged. Saved output
and actual parameter values survive project reopen; redo does not rerun code.

`ProjectDocument.ChangeVersion` increments on every installed edit/undo/redo, preventing
stale capture acceptance after edit/undo even when snapshot identity returns to the
original. `ProjectClipProcessorHost` owns one isolated worker, rejects concurrent
requests, applies results on Poll, requests playback preparation, and cancels/joins
on cancellation, stale capture and disposal. Ordinary generator API is unchanged.

Initial focused transaction/real-worker tests passed 4 tests. Expanded project,
music, audio-host and worker regression: **481 passed**, zero failures/skips,
`/tmp/flow-clip-processing-regression.log`. `git diff --check` passed.
Note-transform transactional application and preservation of section settings remain
next. Multi-bus offline execution exists at the worker API; this selected-clip host
currently supplies one bus. Long audio streaming, integrated bounce/stems,
autosave/recovery and the complete readiness audit remain open.
