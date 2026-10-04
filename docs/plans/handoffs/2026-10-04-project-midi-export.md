# Arrangement MIDI export — 2026-10-04

`ProjectMidiExport.ExportAsync` adds captured-project SMF format 1 output. It resolves
assets and prepares the project with `ProjectCompiler`, then consumes its scheduled
note gates. Clip windows, repeated sections, tempo-map conversion, millisecond
nudges, articulation/tie/overlap and section pedal duration policies therefore use
the same lowering as audio playback. It does not introduce a second note scheduler,
evaluate saved Flow source, mutate document history or use the live playback cursor.

## Encoding and limits

The file contains a conductor track with project tempo/meter changes followed by
one track per DAW track in saved display order. Source section tempo does not replace
the master project map. Scheduled note frame positions are converted back through
that map and rounded to 9,600 ticks per quarter. A positive gate that rounds to zero
ticks becomes one tick. MIDI tempo is rounded to integer microseconds per quarter;
this representation can introduce small clock differences for fractional BPM.
The end marker retains the prepared project duration, including silent time where
audio/DSP tails would play. DSP tails themselves are not encoded as MIDI notes.

Each DAW track gets its own MIDI-port meta-event. Per-key channel allocation separates
up to 16 overlapping same-pitch notes on that port; greater overlap fails explicitly.
Note-offs sort before new note-ons at equal ticks. Readers that ignore MIDI ports
can merge independent tracks. No GM program/drum assignment is inferred from a Flow
instrument, and no claim is made about external synth sound equivalence.

Resolved frequencies are rounded to nearest 12-TET MIDI keys, with a diagnostic for
tuned pitches; no pitch bends/MPE are emitted. Out-of-range pitches fail. Velocities
are rounded to 1–127; zero-velocity scheduled notes are omitted and counted. Section
gain/pan, Flow instruments, mixer processing, device automation, sampled audio and
voice stealing are not translated into MIDI sound engines. The result always
returns diagnostics describing that boundary.

Existing project preparation/note budgets bound output construction. Absolute ticks
are limited to the four-byte MIDI delta range, leaving room for a one-tick gate.
Unrepresentable tempo/meter fails before file publication. Unavailable score clips
fail; other playback/asset diagnostics are returned. The current adapter requires
the project's graph/instruments to prepare successfully even though it exports notes.

## File ownership

The caller owns/awaits the task and cancellation token; progress reports completed
tracks on the worker. Source data stays captured throughout export. Output is staged
in a unique sibling file, flushed, cancellation-checked and renamed into place.
Overwrite is opt-in; normalized project asset paths cannot be direct destinations.
Failures leave existing destination data intact and clean up temporary output.

## Verification and next step

Focused export tests: **3 passed**, `/tmp/flow-project-midi-export-tests.log`.
An independent DryWetMidi reader checks the written files. Tests compare note gates
with shared playback across tempo changes, repeated split windows and nudge; verify
tempo/meter maps, exact repeated-export bytes, same-key channel separation, tuning
diagnostics, cancellation and overwrite refusal. Final affected backend suite:
**510 passed**, zero failures/skips, `/tmp/flow-project-midi-export-regression.log`.
`git diff --check` passed. No physical MIDI device was qualified.

MIDI note import and arrangement export now have host APIs; preserve their explicit
controller/tuning/port limitations rather than calling them lossless MIDI roundtrips.
Next: persistent track mute/solo and explicit clip trim/repeat commands from the
backend readiness audit, followed by render preferences, migration fixtures,
generation-context/capability work and integrated full verification. Native JUI and
historical callback-gap diagnosis remain deferred; the goal is active.
