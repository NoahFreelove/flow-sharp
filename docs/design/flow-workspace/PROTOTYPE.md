# Flow workspace browser prototype

This is the interaction prototype for review before implementing the workspace with JUI C# bindings. The original supplied `Flow Workspace.dc.html` and `support.js` are preserved beside this document as design references. The executable implementation is in `flow-workspace/` at the repository root. No CDN or npm install is needed to run it.

## Run

From the repository root:

```sh
python3 flow-workspace/serve.py
```

Open http://127.0.0.1:4174. Use `--port 4175` if needed. Opening the HTML directly as a file does not support the runtime worker.

Code generation needs the published Flow Web bundle. If missing, publish with the installed .NET 10 SDK and WASM workload:

```sh
dotnet publish flow-lang/flow-lang.csproj -p:FlowTarget=Web -c Release
```

The server maps `/runtime/` to `flow-lang/bin/Release/net10.0/browser-wasm/AppBundle`. It binds only to localhost and does not serve the repository root. The demo, editing and Web Audio preview work without publishing; Generate reports a runtime failure if the bundle is absent.

## Review walkthrough

1. Play the seeded demo. Select the MOTIF.flow clip on the FLOW track.
2. Edit its source and choose Generate (Ctrl+Enter). The actual Flow runtime emits MIDI; named voices appear as separate colored note layers. The example imports both `@std` and `@audio`.
3. Drag the clip to change its placement or track. Click the arrangement ruler and press S to split. Both pieces still reference the original source with independent source offsets.
4. Regenerate: linked clips update together, retaining placements and offsets. Failed, cancelled, stale or timed-out builds retain the previous successful note output.
5. Convert a clip to editable notes. It receives an independent, cropped copy, retains code provenance, and can now be drawn into, transposed, resized or edited without changing linked generated clips.
6. Undo/redo edits, try the three layouts, adjust layer instruments/filter/delay and track mute/solo/gain, save/reopen the project, and export a stereo WAV.

Space plays/pauses, Home rewinds, Ctrl+D duplicates the selected clip, Delete removes the selected note or clip, Ctrl+Z / Ctrl+Shift+Z undo/redo, Ctrl+S saves. Note drawing uses double-click. Panel dividers support dragging and arrow keys. Project state also autosaves in this browser's local storage.

## Model for the native implementation

A source owns Flow code, last successful code, revision, duration and note layers. A clip owns source reference, track, arrangement start, source offset and length. Moving or splitting changes placement only. Conversion is an explicit independent-content operation. Sound parameters belong to layers; mixing parameters belong to tracks.

Each document edit implements an undo/redo action. The prototype uses bounded snapshot actions (100 entries); JUI can use smaller typed actions with the same contract. Compiler work runs in a disposable worker with cancellation and a ten-second deadline, outside the UI thread. Revision acceptance and document mutation are performed together as an undoable action.

## Boundaries

This is a musical interaction prototype, not the native DAW audio engine or plugin host. MIDI carries pitch, duration and velocity into a 12-TET, 4/4, project-tempo preview. Flow tuning, tempo maps, custom synthesizers, effects graphs and native DSP are not reproduced by this MIDI bridge. Browser instruments, filter and delay are preview controls. The initial notes are explicitly seeded demonstration content until Generate succeeds.

There is no recording, audio-file clip import, plugin hosting, automation or native underrun telemetry here. The status reports underruns as unknown. Real-time browser preview caps voices at 128; export caps at three minutes and 10,000 notes. Export renders the full arrangement using browser synthesis, not the loop range. This prototype does not close the roadmap's backend telemetry/recovery work.

## Validation

```sh
node flow-workspace/tests/model.test.mjs
node flow-workspace/tests/audio.test.mjs
node flow-workspace/tests/runtime-smoke.mjs
```

The model tests cover split offsets, independent conversion, undo/redo branching, project validation and MIDI decoding. The audio test covers stop during pending context resume. The runtime integration test executes the actual example and checks three layers over 16 beats (48 notes).

The implementation environment blocked localhost socket creation and Chromium startup, so browser visual, worker integration and audible playback verification remain to be performed locally. The Node WASM integration passed; it does not substitute for that browser check.
