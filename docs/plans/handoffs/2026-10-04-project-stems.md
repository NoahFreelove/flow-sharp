# Project stems — 2026-10-04

`ProjectStemFiles.ExportAsync` exports every routed track from one immutable
snapshot to a new directory. The host resolves assets once, validates the complete
project, and renders tracks sequentially using fresh `ProjectCompiler` playback.
The readiness audit added optional `selectedTracks`: omitted means all; explicit
selection is captured before worker dispatch, validated for 1–64 unique existing
IDs, and rendered in project order. Empty/duplicate/unknown selections fail without
output. Selected stems retain the complete project's common duration and preflight.
It uses the same float32 streaming writer as project bounce and never modifies the
document or live playback state. No saved Flow source is evaluated.

## Processing and timing contract

This export mode solos each track's note/audio sources and track inserts through
the unchanged shared mixer and master chain. Other tracks' inserts are omitted,
including devices that could generate sound from silent input. Shared/master
autonomous generators remain active. These are processed solo stems; their sum is
not guaranteed to equal the full mix when shared processing is nonlinear or
generates its own signal. `stems.json` records this explicit mode along with project
identity, sample rate, frame count, stable track IDs, names, filenames and diagnostics.

`ProjectCompiler`/`ArrangementCompiler` accept an optional solo track for offline
preparation. Clips on other tracks retain the project timeline extent but contribute
no events/audio. Unselected track inserts and their automation are omitted; selected
inserts, instrument controls and shared/master automation use existing lowering.
Solo preparation rejects an unknown track or simultaneous live monitoring.

`PreparedGraphPlayback.minimumFrames` provides a common export endpoint without
prematurely stopping graph processing. Every stem starts at project frame zero and
has the full mix's prepared duration, including the longest release/effect tails.
All stems therefore align on import. Normal playback callers retain default
behavior. The exporter rejects an unexpected solo duration exceeding that endpoint.

## Publication and limits

Track filenames use indices and GUIDs; display names remain manifest data. The
complete WAV/manifest set is staged in an owned sibling directory and published
with a final directory rename after file flushes and cancellation checks. Existing
destinations are never overwritten. Cancellation/failure removes the unpublished
set, including already completed stems. The parent directory must exist. This is
atomic visibility, not a new guarantee against power loss or filesystem failure.

The caller owns/awaits the task, supplies cancellation and marshals optional worker
progress to the UI. Existing limits remain: 64 tracks, preparation/asset budgets,
32-bit RIFF output size. No integer PCM/dither, RF64, selected-range export, raw
pre-mixer stems, or group/sidechain-preserving stem mode is introduced here.

## Verification and continuation

Focused bounce/stem suite passed **7 tests** before the final tail case was added:
`/tmp/flow-stem-tests-final.log`. Final affected backend suite: **495 passed**,
zero failures/skips, `/tmp/flow-stems-regression.log`. `git diff --check` passed.
Tests cover common frame lengths, exact solo-render equality, linear reconstruction,
nonlinear differences, safe track filenames, overwrite refusal, cancellation during
the second stem, inactive autonomous-insert isolation, automation and insert tails.
No physical audio device is required or qualified by these tests.

Continue with integrated host autosave/recovery, then audit the backend roadmap
requirement by requirement. Full core/browser verification should precede declaring
frontend readiness. Native JUI frontend and historical callback-gap diagnosis remain
deferred. The overall goal is still active.
