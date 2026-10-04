# Native DAW frontend integration

The browser workspace is the approved visual prototype. The native application
should consume `Flow.Studio.Host` and the owner's JUI 0.10.0 C# package. Do not
carry the browser's temporary MIDI/Web Audio synthesis bridge into native playback.
Backend qualification status is recorded in the current readiness audit; this
handoff does not claim a shipped native UI or physical-device certification.

## Ownership and application loop

Create the project with `ProjectFactory.Create`, or load it with `ProjectFile.Load`.
Create `ProjectPlaybackSession` with the project directory and the saved render
sample rate/block size. Initial preparation is synchronous: do it before opening
output and before entering a latency-sensitive UI loop. Opening output is explicit
through `Connect` and never automatically starts playback.

Use one control owner for the document and host objects. Run `Poll` from the
application loop; there are no hidden host timers. Poll generation/mixer/processor
hosts when present, and autosave separately. These hosts may themselves poll the
playback session; avoid concurrent polls or background document mutations. Workers
build/prepare detached results; the control owner accepts them and audio owns
prepared rendering state. Marshal UI requests to that control owner.

Use `Playback.Queue` for transport requests and observe its coherent clock, not a
UI timer, for playhead position. Queue rejection is backpressure: retain or retry
the user's intended control action according to its contract. Stop has a protected
path. Show output faults and unknown underrun information honestly. Reconnection
starts stopped. If shutdown cannot join a device callback, retain the owner for
retry; do not force-dispose resources still used by audio.

## Workspace wiring

| UI interaction | Backend boundary |
| --- | --- |
| Notes/velocity/quantize/transpose | `ProjectNoteCommands` and shared `NoteEditing` services; commit one grouped gesture. |
| Copy/paste across sequences | `NoteClipboard` and `EditSequences`; IDs are regenerated for copied material. |
| Clip move/split/trim/repeat/nudge | `ProjectClipCommands`; preserve linked source windows and provenance. |
| Clip gain/fades | `ProjectClipEnvelopeCommands`; source-frame anchors survive trim/split. |
| Track metadata/mute/solo/instrument | `ProjectTrackCommands`; color is nullable 24-bit sRGB, null means automatic. |
| Add a mixer track | `ProjectMixerHost.TryAddTrack` prepares the Flow graph expansion; direct track add requires an already exposed unused bus. |
| Knob/fader drag | Mixer preview begin/update/commit; one committed action, cancel restores prior value. |
| Instrument/effect controls | Stable manifest parameter IDs; parameter edits/presets through the session and public plugin commands. |
| Ordered devices | Session `SetEffects` / `ProjectEffectCommands`; retain saved instance IDs. |
| Build/apply Flow | `ProjectGeneratorHost`; use asset grants for sample access and packaged dependencies for plugins. |
| Process selected notes/audio | `ProjectClipProcessorHost`; asynchronous, revision-checked, one accepted undo action. |
| Meter strips | `TryReadActiveMeters` with a reused buffer; retain the last coherent read on contention. |
| Piano-roll audition/MIDI keyboard | Select monitored track, wait for acknowledged monitor activation, then use monitoring host/endpoint. |
| Record/count-in | `MidiRecording.Arm` / `ArmCounted`, observe state, stop, then commit the captured take. |
| Metronome | Session `SetMetronome`, before arming or after a take; click is monitoring-only. |
| Save/recover | `ProjectFile`, `ProjectPackage`, `ProjectAutosaveHost`, explicit `ProjectRecovery` choice. |
| Export | `ProjectBounceFile`, `ProjectStemFiles`, `ProjectMidiExport`; use async progress/cancellation. |
| Editor intelligence | Non-executing language analysis/LSP; source text undo is separate from accepted project history. |

After direct document commands or history undo/redo, call `RequestPreparation`.
Host operations that already schedule preparation need no second request. Keep
render caches and UI selection out of undo history. The immutable document is the
saved truth; code preview comes from `FlowProjectExporter`, never repeated text
rewriting on each drag. Failed builds preserve the previous accepted sound.

Surface retained missing bindings/assets with repair choices. Keep edited source,
build status and the accepted source revision visibly distinct. Generators return
notes, audio and devices directly; they do not need `writeMidi`. Generated-note
conversion creates independent editable material while retaining provenance.

## First native walkthrough

Start with transport and one routed track. Add a piano roll, grouped note edits and
meter display. Connect source editor/build diagnostics and a second mixer track.
Then exercise record/count-in, automation, undo/redo, save/reopen and WAV export.
Use the existing headless lifecycle and host tests as behavioral references, not
as substitutes for native interaction testing.

The native gate still requires the 10,000-note piano-roll/input/responsiveness
exercise while audio and background builds run, window/graphics/accessibility
checks and a real application walkthrough. JUI package loading and one headless
frame were verified separately; that does not establish those interaction gates.

## Supported limits to show accurately

- Clip splitting/seek can retrigger notes and reset transient DSP; seamless state
  recovery is not promised. Continuous exports include declared bounded tails.
- Step tempo and bar-boundary meter are supported. Audio moves without stretching.
- Samplers are one-shot, use linear resampling, and have no disk streaming or loop
  editor. Captured project sample grants are distinct from legacy implicit lookup.
- MIDI file interchange is a 12-TET adapter; live tuning uses captured frequencies.
  MIDI receipt/render timestamps are not calibrated hardware timestamps.
- Range WAV export hard-crops its selected interval after rendering the prefix.
  Processed stems with nonlinear shared master effects may not sum to the full mix.
- Flow process workers are bounded and capability-restricted, not OS sandboxes.
- Native third-party plugin hosting, multitrack audio recording, advanced time
  stretching, installer qualification and physical latency/recovery remain outside
  this backend-to-frontend handoff.
