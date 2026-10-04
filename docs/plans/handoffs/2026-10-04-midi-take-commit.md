# Captured MIDI take to editable clip — 2026-10-04

P9-02 adds deterministic timeline conversion and atomic project commit for the
recording foundation. Device input/live monitoring is not yet connected.

## Implemented

MidiRecordingTake captures one expected project snapshot, recording start/end frame,
project start frame, sample rate and explicit input-latency compensation (0–10 s).
Straight-through mapping is projectStart + captureOffset - latency. Time before
project zero is trimmed; fully pre-zero notes disappear. Each note endpoint converts
independently through the captured tempo map, preserving duration across tempo
changes. No quantization, bar snapping or speculative latency estimation occurs.

Notes retain MIDI channel in VoiceId, velocity/127 and 12-TET MIDI pitch (A4=440),
with deterministic sharp spelling and captured note IDs. Take/source/clip identities
are allocated once. Invalid timing, notes, limits and overflowing frame arithmetic
reject. This mapping assumes an uninterrupted take; pause, seek and loop handling
must be explicit in the future recording session rather than inferred here.

ProjectNoteCommands.CreateFromNotes creates a detached editable source and clip in
one captured action. CreateBlank delegates to the same construction path. Notes are
copied/bounded and validated before document mutation. MidiRecordingTake.Commit
requires the original snapshot identity, an existing track and an uncommitted take.
An empty/fully trimmed take creates no action. Successful take commit is one-shot;
undo/redo restores the captured source/clip without re-pairing input or generating
new IDs. A changed project requires explicit take rebasing rather than silently
using a different tempo map.

## Evidence

Two focused tests passed (`/tmp/flow-midi-take.log`): a note crossing 120→60 BPM
converts to exact quarter bounds; commit creates one editable-source action;
save/reopen and undo/redo preserve serialized identity/content; latency trims before
zero; stale project commit rejects without mutation. This does not qualify physical
MIDI timestamp accuracy or live transport synchronization.

All **395** affected backend/module tests passed with zero failures
(`/tmp/flow-midi-take-regression.log`). `git diff --check` passed. Full core/Web
gates were not repeated for this slice.

## Next

Connect bounded input capture to a host recording session that owns its clock,
invalidates loss/discontinuity, drains events before stop and commits a completed
take safely. Then native MIDI device ownership and live instrument monitoring,
followed by remaining preset/device/recovery workflows. Native frontend and
historical callback-gap diagnosis remain deferred.
