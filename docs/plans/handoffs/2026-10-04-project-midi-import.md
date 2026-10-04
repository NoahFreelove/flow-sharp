# Project MIDI note import — 2026-10-04

This closes the note-import half of the MIDI adapter gap from the backend readiness
audit. Arrangement MIDI export remains next; composition export alone is still
not proof of a complete project MIDI workflow.

`Flow.Music.IO.MidiNoteImporter` reads bounded SMF format 0/1 files into detached
track/channel parts. It supports channel running status, note-on velocity zero as
note-off, FIFO pairing for overlapping identical keys, explicit PPQN timing and
12-TET pitch spelling/frequency. Track-end duration includes trailing silence.
Exact note duration is retained as ticks/PPQN when representable by the model's
integer rational. Parts, notes, tempo/meter metadata and diagnostics use read-only
collections. It is independent of interpreter objects and project state.

Limits: 16 MiB encoded file, 256 tracks, 100,000 note-ons, 1,000,000 events and
4,096 entries per tempo/meter map. Headers, chunks, event data, end-of-track and
four-byte variable integers are validated; truncated/malformed files fail. SMPTE
division, format 2 and zero PPQN are rejected explicitly. Cancellation is checked
during parsing and completion of unfinished notes. The legacy CLI parser is not
changed by this addition.

This is a **note import**, not a lossless MIDI sequencer import. Controller changes
(including sustain), pitch bends, programs, SysEx and other unrepresented metadata
are counted in diagnostics and not silently interpreted as Flow instruments or
automation. Unmatched note-offs are diagnosed; held notes without an off are closed
at that track's end with a diagnostic. Tempo/meter events are returned separately
without modifying project timing. More complete controller mapping remains a
documented capability limit rather than claimed playback equivalence.

`ProjectMidiImport.ReadAsync` reads/parses off the control thread with the same file
budget. The caller awaits it, presents diagnostics, and invokes Apply on the document
owner when choosing the destination. Apply creates one independent editable clip per
note-bearing track/channel part on the selected existing DAW track at the requested
quarter anchor. Channel provenance remains in each note's VoiceId. All parts use the
selected track's Flow instrument; GM programs are not auto-assigned. Zero-length
parts receive a one-tick visible window; zero-duration events remain zero-duration.

`ProjectNoteCommands.Import` validates and installs all imported sources/clips in a
single captured document action. IDs/results are reused by undo/redo, and global
tempo/meter/context remain unchanged. Unknown destination tracks, invalid identities
or exceeded budgets fail without a partial edit. Saved project data uses existing
editable-source semantics; imported notes can immediately use shared note editing.

## Verification / continuation

Focused reader suite: **6 passed**, `/tmp/flow-midi-note-import-tests.log`, including
all three committed MIDI fixtures, same-key overlaps/running status, tempo/meter,
unsupported-control diagnostics, truncated chunks, invalid encodings and cancellation.
Final affected backend regression: **507 passed**, zero failures/skips,
`/tmp/flow-midi-import-regression.log`. `git diff --check` passed.
The project test covers file read → captured import →
save/reopen → undo/redo, with unchanged project timing and atomic invalid-target failure.

Next implement arrangement MIDI export using project tempo/meter, source windows,
clip anchors and millisecond nudges. Define its tuning/articulation/controller
limitations explicitly and test event times against the shared playback scheduling.
Then continue the readiness audit's track mute/solo, clip trim/repeat, render
preferences, migration fixtures and generation-context/capability work. Native JUI
and historical callback-gap diagnosis remain deferred; the goal is active.
