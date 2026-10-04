# Captured project tuning context — 2026-10-04

`GenerationContext.Tuning` now carries an immutable `ProjectTuning`: named system
(EqualTemperament, JustIntonation, Pythagorean), musical key/mode, optional captured
Scala text and optional keyboard-map text. These are definitions, never file paths.
Text is bounded to 256 Ki characters each; the existing Scala parsers perform
semantic validation on the host before edits/builds. Existing parser limitations,
including its keyboard-map formal-octave policy, are unchanged.

The generator installs tuning/key into its fresh engine before user evaluation.
Source tuning declarations retain their existing override semantics. Full captured
definitions are exposed through dawContextInfo tuningSystem/tuningKey/tuningScala/
tuningKeyboardMap entries. `dawProjectTuning` and `GeneratorTuning.Set` share
validated construction; edits increment context revision and are undoable. Old
accepted output remains unchanged until explicit regeneration.

Project schema 15 persists both current project tuning and each source result's
historical tuning context. Earlier schemas default to equal temperament / C major;
older tags cannot carry nondefault new tuning. Schema 15 requires definitions.
Processor parameter-context copies preserve tuning. Process protocol is now 6 and
transports the definition; version mismatches fail explicitly.

Tests establish A4=432 from captured Scala/KBM in direct and isolated builds, source
equal-temperament override restoring 440, historical/current tuning separation after
save/reopen, undo, Flow reconstruction and malformed-scale rejection before edit.
Focused tuning checks: **2 passed**, `/tmp/flow-project-tuning-focused.log`.
An initial fixture used unsupported formal octave 12; it was corrected to the
parser's documented supported value 0. No parser checks were weakened.

Final affected backend/API regression: **539 passed**, zero failures/skips,
`/tmp/flow-project-tuning-regression.log`. `git diff --check` passed; full-core,
browser/publish and physical-device qualification remain pending.

This establishes generator tuning, not all possible DAW tuning paths. Live MIDI
monitoring (`PreparedLiveInstrument`) and recorded note capture (`MidiRecordingTake`)
still use 12-TET A4=440; review their intended project-tuning behavior before final
readiness. MIDI file import/export already document 12-TET limitations. Host-owned
legacy sample/style integration and complete authoring-surface review also remain.

Next: close those contract gaps, then integrated musical lifecycle, full core/browser
tests, actual WASM publish, restored Desktop build and documentation reconciliation.
Native UI and historical callback-gap diagnosis remain deferred.
