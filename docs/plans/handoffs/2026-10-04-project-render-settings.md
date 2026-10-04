# Saved project render preferences — 2026-10-04

Schema 13 adds immutable `ProjectRenderSettings` with sample rate (1–384000 Hz)
and offline block size (1–65536 frames, matching the graph preparation limit).
Older projects default to 48000 Hz / 256 frames. Schema 13 requires settings;
older tags reject nondefault new settings. Unsupported export formats are not
advertised: WAVE remains stereo float32, with existing RIFF size limits.

`ProjectRenderCommands.Set` is one undoable edit, with no history for equal values.
Snapshot reconstruction preserves settings across clip/note/track/asset edits,
source acceptance, processor transactions, plugin values/duplication/removal,
effects/automation, visual mixer authoring, graph reconstruction and packaging.
Flow export carries settings in its resource seed and all subsequent transforms;
`dawRenderSettings` also provides explicit pure Flow authoring. Native signatures
are now 716 registered / 710 reachable.

Mix bounce, stem export and MIDI export accept nullable rate/block arguments;
omitted values use the captured project's preferences and supplied values override
them individually. Overrides do not edit the project. Device playback/recording
sessions and explicit compiler preparation still use negotiated/caller-selected
formats; changing these preferences does not silently restart hardware. Plugin
rate/block constraints remain preparation errors, never silently clamped values.

Tests cover nondefault settings through undo/redo, source rebuild, clip gestures,
automation, mute, Flow reconstruction, saved files, recovery and packaging, plus
actual default/overridden WAV and stem exports. Existing audio/note processor and
plugin-chain tests now carry nondefault preferences. Fixed schema 1/6/11/12 files
assert migration defaults. Focused preferences/API/fixture tests: 9 passed in
`/tmp/flow-render-preferences-tests.log`.

Final affected backend and API-snapshot regression after the bounds correction:
**524 passed**, zero failures/skips, `/tmp/flow-render-preferences-final.log`.
`git diff --check` passed. These results do not replace pending full-core, browser
publish or hardware qualification.

Next: full generation-context and ordinary DAW-generator capability review from
the readiness audit, followed by integrated lifecycle and full core/browser checks.
Hardware qualification, native UI and historical callback-gap work remain separate.
