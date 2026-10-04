# Project tuning for monitoring and recording — 2026-10-04

Implemented the project wiring following `2026-10-04-live-pitch-map.md`.

`GeneratorTuning.ResolveMidi` uses the existing SongRenderer key/mode resolution
and PitchConversion, including captured Scala/KBM, to build the immutable map.
ProjectPlaybackSession resolves it on its preparation worker and passes it through
the coordinator/compiler to the prepared monitor. ProjectMidiRecordingHost captures
it when arming and passes it to the recording session/take. Zero-frequency keys are
silent in monitoring and omitted from takes; recorded notes retain resolved Hz.
Lower-level project monitoring/recording rejects nondefault tuning without an
explicit resolved map instead of silently playing/recording 440 Hz equal temperament.
The engine stays independent of the Flow interpreter assembly.

MIDI input uses sharp spelling for chromatic keys, consistently with recorded note
spelling. This is explicit because MIDI does not carry enharmonic spelling and
JI/Pythagorean distinguish it. Flow-authored spellings and source tuning overrides
remain independent. Repreparing monitoring resets voices under existing semantics;
no seamless held-note retuning is promised. MIDI file import/export remains the
documented 12-TET adapter, with no MPE/pitch-bend tuning export.

## Evidence

Affected backend/API regression: **548 passed**, zero failures/skips,
`/tmp/flow-midi-tuning-regression.log`.

- Actual recording host captures 432-based Scala pitches, commits one editable take
  and preserves undo/redo behavior using a fake MIDI/output device.
- Actual playback session prepares/publishes a tuned monitor; its rendered samples
  match independently prepared playback with the resolved project map.
- Just intonation D minor and Pythagorean E-flat major MIDI A4 match Flow-generated
  A4 through the existing generator template.
- KBM unmapped notes are omitted and mapped 432 Hz notes survive recording-session
  completion. Explicit KBM mappings are required to test restricted range: size-zero
  linear KBM intentionally maps all keys in the existing Flow tuning implementation.
- Existing model, hosting, audio, lifecycle and module-surface regressions passed.

## Continue

Finish sample/style authoring compatibility review, then final integrated/core and
browser qualification and requirement-by-requirement documentation reconciliation.
`@improv` is importable but its natives are currently excluded from ordinary-generator
policy because StyleRegistry lazy loading evaluates shipped/user filesystem packs
in another engine. Preserve useful in-memory/shipped musical capabilities without
restoring implicit user-file execution. Sample synthesis/grants exist; assess legacy
named sampled instrument integration against the actual MVP contract.

Native frontend and historical callback-gap diagnosis remain deferred. Physical
device/latency qualification remains open; fake-device tests do not establish it.
