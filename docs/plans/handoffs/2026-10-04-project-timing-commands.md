# Project timing commands and typed clip offsets — 2026-10-04

`ProjectTimingCommands.Set` installs step tempo and bar-boundary meter maps as one
captured undo action. Arrangement/current generation context share the exact new
map objects; context revision increments. Source windows, quarter anchors, audio
frame lengths, nudges, historical source contexts, tuning, routing, automation and
render settings are preserved. Equal maps add no history. This does not time-stretch
audio, re-align clips previously aligned to bars, or introduce tempo ramps.

`dawProjectTiming` exposes the same operation with a quarter-to-BPM dictionary and
matching one-based bar-to-numerator/denominator dictionaries. Invalid maps fail before
constructing a replacement project. Accepted source tempo remains historical until
explicit regeneration; changing current project tempo controls arrangement playback.

Typed Millisecond/Second overloads now exist for `dawRelativeOffsetMs`, plus an
absolute `dawSetOffsetMs` in both units. Both use existing ClipOperations for score
and audio. The old Double milliseconds overload remains compatible. Document
`RelativeOffset` and `SetOffset` commands validate a mixed clip selection atomically,
commit one action and ignore unchanged output. Absolute setters are idempotent.

Focused command/API checks: 4 passed, `/tmp/flow-timing-commands-tests.log`.
Additional regression coverage exercises both note/audio typed offsets, failed meter
keys, no-op history and invalid selection atomicity. Native surface now has 728
registered signatures / 722 reachable.

Final affected backend/API regression: **541 passed**, zero failures/skips,
`/tmp/flow-timing-commands-regression.log`. `git diff --check` passed.

Next: browser compatibility and actual WASM publish, then restore the Desktop build.
Use those results alongside an integrated backend lifecycle and the remaining
tuning/live-MIDI and sample/style contract review. Full core's preceding checkpoint
was 3503 passed/14 skips; it predates these commands. Native UI and historical
callback-gap diagnosis remain deferred.
