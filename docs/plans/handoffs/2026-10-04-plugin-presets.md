# Public plugin presets — 2026-10-04

P7-38 supplies portable presets for existing source-based plugin instances. This
extends saved public values; it does not introduce a second device/DSP model.

## Contract

PluginPreset captures every public parameter in typed units, including defaults.
The immutable snapshot contains a display name, plugin ID, exact serialized package
SHA-256 and values. Versioned JSON rejects duplicate/unknown fields, malformed
identity, nonfinite values and excessive input. Capture and parsing do not execute
Flow or open resources. Applying also validates the complete parameter set and
typed ranges, including discrete values.

The package fingerprint includes source, manifest, target mapping and dependencies.
A package revision needs explicit preset migration; there is no automatic migration
or best-effort matching. Even metadata/order-only package changes can require
migration under this intentionally exact v1 contract. Presets hold neither live DSP
state nor project binding IDs, so they transfer between independent instances of
the same package. Loading a preset does not load a plugin.

ProjectPluginCommands.ApplyPreset validates the entire change before applying one
captured project action. Changing an automation-owned public parameter or its direct
graph target rejects the whole preset. Unchanged values are allowed, including a
fully unchanged preset that creates no history. Default values need no explicit
override. The source, package, output and binding identities remain intact.

ProjectPlaybackSession.ApplyPluginPreset schedules normal bounded preparation after
a successful edit; it does not write parameters one at a time into a running graph.
Project persistence and executable Flow export retain the resulting values through
their existing paths. The reusable preset itself is a separate JSON artifact; no
library browser, preset filesystem manager or project preset-name tracking is added.

## Evidence

- Focused public-parameter/preset tests: **7 passed**, zero failures.
  `/tmp/flow-plugin-presets-focused.log`.
- Affected backend/platform/music/hosting/module suite: **453 passed**, zero
  failures or skips. `/tmp/flow-plugin-presets-regression.log`.
- Effect audio parity across instances, project JSON and executable Flow export;
  one-action undo/redo; defaults restoration; no-op history; atomic automation
  rejection; bad values, incomplete/extra controls, duplicate fields and package
  revision rejection are covered. The instrument build test also uses the host
  preset API and checks saved graph values and undo/redo.
- `git diff --check` passed. The full core checkpoint in the preceding shared MIDI
  handoff predates this slice; no physical device qualification was performed.

## Continue

Audit device-instance creation/duplication/assignment and safe reload workflows next.
Values and presets currently belong to a generated plugin source; independently
routed device instances need explicit ownership rather than sharing mutable values.
Remaining plugin categories and integrated bounce/export/recovery also remain open.
Use the completed JUI package only when native frontend work begins. The backend
goal is still active, and historical callback-gap investigation remains deferred.
