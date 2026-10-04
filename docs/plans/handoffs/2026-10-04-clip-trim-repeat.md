# Clip trim and repeat — 2026-10-04

`ClipOperations` and `ProjectClipCommands` now expose trim/resize and linked
repeat for score and audio clips. Each project edit is one captured undo action;
repeat IDs are supplied once and remain stable through redo. Invalid selection,
ID collisions and bounds fail before publication. Unchanged trims add no history.

Score trim uses a signed source-quarter left-edge delta and a new length. Anchor
and source offset move together, retaining the project time of surviving events.
Audio trim uses exact signed source frames and resolves the new anchor through
the full tempo map. Right-only resize preserves the anchor exactly. Both retain
nudge; negative deltas may extend left only within nonnegative source/project bounds.
Extending beyond available source content follows the existing silent-window policy.

Repeat returns additional linked clips, excluding the original, using distinct
caller-supplied IDs. Score copies are spaced by visible quarter length; audio
copies by sample duration across tempo changes. This is explicit repeated
placement, not a destructive source render or an implicit infinite loop.

Flow equivalents use the same helpers: `dawTrimScore`, `dawTrimAudio`,
`dawRepeatScore`, `dawRepeatAudio`. Frame counts remain decimal strings to preserve
64-bit values. Repeat accepts a Strings list of IDs. The native surface now has
714 registered signatures, 708 reachable through internal procedures.

Focused command, Flow evaluation/export and module-surface checks: 13 passed,
`/tmp/flow-trim-repeat-surface.log`. Coverage includes surviving event times,
tempo-crossing audio adjacency, extension bounds, overflow, identity capture,
atomic collision rejection, no-op history and undo/redo.

Affected backend suite plus module-surface verification without snapshot-update
mode: **514 passed**, zero failures/skips,
`/tmp/flow-trim-repeat-regression.log`. `git diff --check` passed. This is not a
full-core, browser or physical-device qualification.

Next: persistent track mute/solo, then the remaining readiness-audit work.
Native frontend and historical callback-gap diagnosis remain deferred.
