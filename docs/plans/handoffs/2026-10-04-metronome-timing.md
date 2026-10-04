# Metronome and count-in timing foundation — 2026-10-04

`ProjectMeterMap.PositionAt` resolves a project quarter to its one-based bar,
bar-start quarter and active meter. `MetronomeSchedule.Between` schedules a bounded
half-open project interval through all tempo/meter changes; frame times are absolute
project sample frames. The meter denominator defines the click unit: 6/8 uses six
eighth-note clicks, with beat one accented. Compound grouping is not inferred.

`CountIn` creates one to eight frozen bars at the cursor's active tempo/meter,
with click frames relative to the lead-in start and an exact completion frame.
Project time must remain stationary during this lead-in. It does not extrapolate
earlier tempo changes or require negative project positions. Beat lists are captured
and bounded to 100000 entries; bad ranges, settings and unrepresentable frame times
fail before publication.

Validation: **13 passed**, zero failures/skips, `/tmp/flow-metronome-schedule.log`.
Includes tempo-changing 4/4 -> 6/8 click frames, half-open selection, bar accents,
frozen 3/8 count-in at a non-bar cursor, duration/limits, existing arrangement and
project timing tests.

## Required continuation

This is only the shared timing foundation. Metronome/count-in are NOT yet wired
to playback or recording. Next:

1. Render clicks with the existing Flow graph/instrument primitives and expose the
   equivalent Flow authoring path; no separate callback oscillator implementation.
2. Compose metronome monitoring with prepared playback, preserving graph controls,
   pause/seek/loop semantics, empty-project recording and default-off export behavior.
3. Extend audio-owner recording activation with the lead-in boundary: project cursor
   stays fixed, input before completion is excluded, state/progress is readable,
   cancel/device loss closes admission, and recording begins at the acknowledged
   exact boundary. Test split audio blocks crossing the boundary and late host polls.
4. Capture settings and update persistence/Flow export if project-owned; verify
   undo/save parity and document the supported click/count-in policy.

Then deliver the captured-sample authoring example, reconcile historical roadmap
and readiness rows, and complete browser/WASM/core and full requirements auditing.
Native frontend and historical callback-gap diagnosis remain deferred; physical
device/latency qualification remains separately open.
