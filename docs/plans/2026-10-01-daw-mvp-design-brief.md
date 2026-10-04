# Flow DAW MVP design brief

Date: 2026-10-01.

This brief describes the first usable Linux desktop DAW for the design agent.
It follows the [restructuring roadmap](2026-09-20-flow-restructuring-roadmap.md)
and uses the owner's custom UI kit. These are design requirements, not claims
that every backend capability is implemented. UI implementation remains deferred
while the audio backend and Flow plugin support are completed.

## MVP outcome

Compose a short song visually, generate or transform parts with Flow, use Flow
instruments and effects, record MIDI, save and reopen the project, and export the
mix. Writing code is optional for ordinary composition.

## Essential features

| Area | What the MVP needs |
|---|---|
| Project management | New/open/save, recent projects, unsaved indicator, autosave/recovery, missing asset/plugin resolution. |
| Transport | Play/pause/stop, record, seek, loop region, song position, tempo, time signature, metronome and count-in. |
| Arrangement | Horizontal timeline, track headers, note/audio clips, move/resize/split/duplicate/delete, snapping, zoom, scrolling and loop boundaries. |
| Tracks | Add/rename/reorder/color tracks; mute, solo, volume, pan and MIDI record-arm. Instrument and audio tracks feeding a master output. |
| Piano roll | Draw/delete/move/resize notes, multiple selection, copy/paste, audition, velocity lane, quantize, transpose, snap resolution and clip length. |
| Audio clips | Import audio, waveform display, trim, position, clip gain and basic fades. |
| MIDI | Keyboard input, instrument audition, basic recording into note clips, MIDI import/export and all-notes-off. |
| Device chain | Instrument slot followed by ordered effects; add/remove/reorder/bypass devices, parameter controls and presets. |
| Mixer | Track/master strips with meters, volume, pan, mute/solo and clipping indicators. |
| Automation | Lanes for volume, pan and exposed plugin parameters; draw/move/delete points with step or linear interpolation. |
| Flow editor | Source tabs, highlighting, completion, inline diagnostics, search, documentation/hover help, build/apply, cancellation and output/problems panel. |
| Plugin authoring | Instrument/effect/generator templates, source editing, parameter preview and audition. Clearly distinguish edited source from the currently running version. Failed builds preserve the working sound. |
| Generated clips | Run Flow to produce notes, preview the result, regenerate, retain source/settings/seed, and explicitly convert into editable notes. |
| Undo/redo | Named actions, keyboard shortcuts and history panel. One drag or grouped edit becomes one undo step. Actions implement an interface with undo and redo functions, owning their reversal and reapplication behavior. |
| Export | Stereo WAV export with range selection, progress/cancel and effect-tail handling. |
| Audio settings/status | Device, sample rate, buffer settings, connection/recovery state and readable performance status. Unknown underrun counts must appear as unknown. |

## Flow features to explore in the design

These are UI opportunities around existing language capabilities. Exposing them
in the DAW still requires integration; the entire list is not an MVP commitment.

| Flow capability | Possible DAW interaction |
|---|---|
| Note streams and musical durations | Type a short musical phrase in a clip's source tab and preview its notes immediately. |
| Chords, scales and arpeggiation | Select a chord or progression, preview voicings, then insert notes or generate an arpeggio. |
| Shared musical transforms | Apply transpose, inversion, reversal or rhythmic expansion from a piano-roll menu or Flow script. |
| Euclidean rhythms and pattern generation | A generator panel with steps, pulses, rotation and a live note preview, particularly useful for drums. |
| Seeded randomness | “New variation” plus a seed lock so a generated part survives reopening unchanged. |
| Musical context | Show inherited tempo/key/tuning and any supported local overrides in the selected item's inspector. |
| Custom tuning / Scala | Tuning selection and meaningful pitch labels in the piano roll. Reserve space now; richer microtonal editing can follow. |
| Flow-authored devices | Generate knobs, sliders and menus from declared plugin parameters; an “Edit source” action opens the corresponding code. |
| Offline audio processing | Select a clip, apply a Flow processor, preview and accept the result as an undoable edit. |
| Reusable generators | Save a useful rhythm, melody or transformation script as a reusable device with exposed controls. |

For generated clips, make **Source / Preview / Convert to notes** explicit.
Visual note edits must not imply that the DAW can rewrite arbitrary Flow code.
Keep the distinction between editable notes and generated source clear.

## Suggested workspace

- **Top:** transport, tempo/meter, loop and project status.
- **Left:** searchable browser for instruments, effects, generators, presets and assets.
- **Center:** arrangement timeline.
- **Bottom, resizable:** piano roll, waveform editor, device chain or mixer.
- **Right, collapsible:** selected track/clip/note inspector.
- **Expandable editor workspace:** Flow source beside a musical/device preview, with diagnostics below.

## Design states to include

- Empty project.
- Selected notes and velocity editing.
- Generated clip with source, preview and conversion controls.
- Plugin compiling.
- Failed build with the previous working version still playing.
- Missing asset or plugin.
- Audio-device disconnect and recovery.
- Recovery after an unsaved session.

## Deferred scope

Defer multitrack audio recording, advanced routing, third-party plugin formats,
a session-launcher view, notation engraving, advanced time stretching and
collaborative editing. Basic MIDI recording remains part of the MVP.

## First complete design walkthrough

Create track → add instrument → draw notes → arrange clips → generate a variation
→ edit a Flow effect → automate it → undo/redo → save/reopen → export.

## Engineering continuation

Current sequencing follows the owner's 2026-10-04 direction: finish backend
readiness, then integrate the approved design with JUI. Historical callback-gap
diagnosis is deferred. See the [current readiness audit](handoffs/2026-10-04-backend-readiness-audit.md)
for implemented features, remaining backend work and separate hardware gates.
