# Note clipboard and grouped gestures — 2026-10-04

Implemented the backend gap identified in the expanded MVP audit.

`NoteClipboard.Copy` captures a bounded, nonempty, unambiguous note selection,
normalizing offsets relative to its earliest note. Paste preserves relative timing,
resolved tuning, duration, articulation, ties/overlap, velocity and provenance while
assigning fresh note IDs and independent voice IDs. Voice relationships inside the
selection are preserved. Paste validates all notes against the target source window;
it does not silently extend source duration, quantize or retune them. This is
host-neutral musical data, not operating-system clipboard integration.

`ProjectNoteCommands.EditSequences` captures up to 1024 distinct sequence targets
as one document gesture across editable sources. All changes are constructed before
publication; any invalid target/edit leaves the document/history unchanged. Each
affected source revision advances once. Redo restores captured snapshots without
re-running callbacks or generating new IDs. The existing EditSequence delegates to
the grouped path. Cut/paste composes NoteEditing.Delete and NoteClipboard.Paste.

Validation: **33 passed**, zero failures/skips, `/tmp/flow-note-clipboard.log`.
Tests verify independent copies, preserved 432 Hz pitch and detailed note metadata,
relative timing, rejected duplicate/unknown selections and out-of-bounds paste,
cross-source cut/paste atomicity, one undo action, stable redo identity/callback
count, and persisted pasted notes. Existing editable-source, note-editing and
combined backend lifecycle tests remain green.

Native selection visuals, keyboard bindings and system clipboard adapters remain
frontend work. Generated source ownership still requires explicit conversion to
editable notes. Multi-source callbacks must operate on snapshots rather than
performing external side effects, as with the existing EditSequence API.

Next: implement per-audio-clip gain/fades through shared Flow processing, then
metronome/count-in. Retain final browser/WASM qualification, current support/docs
reconciliation and complete requirements audit. The captured-sample authoring
example still needs delivery. Native frontend and historical callback-gap work
remain deferred; physical hardware/latency qualification remains open.
