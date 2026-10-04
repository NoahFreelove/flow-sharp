# Sampler/automation compatibility checkpoint — 2026-10-04

Current-state verification after P7-27–37: instrument packages, immutable public
values, coherent live controls, public automation, sample graph kernels/assets,
Flow sampler export and restricted packaged-WAVE access.

## Evidence

- Full core verifier passed: Desktop solution build; **3,321 language/backend tests
  + 21 MIDI tests**, zero failures. Fourteen language tests were unexecuted/skipped.
  Tracked-file mutation audit was empty. Evidence:
  `/tmp/flow-sampler-core/verification.json`, logs and TRX files in that directory.
- Web-target shared backend/music/static-binding/native-policy/Phase47/48 selection:
  **313 passed, 7 skipped**, zero failures (`/tmp/flow-sampler-web.log`). Platform and
  long-running tests were excluded.
- Actual WebAssembly publish passed (`/tmp/flow-sampler-wasm.log`) and generated
  `flow-lang/bin/Release/net10.0/browser-wasm/AppBundle`.
- Desktop solution Release build restored after Web-target assets: zero errors,
  309 warnings (`/tmp/flow-sampler-desktop-restore.log`).

This verifies compilation, deterministic tests and publication, not browser UI,
native JUI window behavior, physical audio callback timing or device reliability.

## Next: MIDI input and captured recording

Existing `flow-lang/Audio/MidiClock.cs` has an internal librtmidi polling input
bridge used for MIDI clock. It is not a DAW recording service: it dispatches byte
arrays from a polling thread and does not provide note recording, project transport
mapping or clip actions. Reuse/extract the native adapter carefully rather than
opening devices from the language/audio callback.

Required next backend work: bounded input event capture, explicit ownership and
shutdown, timestamp-to-transport mapping, channel/note pairing and held-note policy,
live instrument monitoring, and captured recording commit/undo into editable clips.
Fake-input tests should precede physical keyboard qualification. Preserve raw input
and avoid implicit quantization; source timing remains explicit.

Device instances/presets and integrated bounce/export/recovery workflows also need
completion against the roadmap. Native frontend and historical callback-gap
investigation remain deferred; the goal is still active and not frontend-ready.
