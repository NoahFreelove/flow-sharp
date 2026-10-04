# Backend readiness audit — current state, 2026-10-04

Status: **backend requirements verified for native frontend integration**. This is
not completion of the native DAW, hardware qualification or the release roadmap. This matrix supersedes the chronological
[initial audit and follow-ups](2026-10-04-backend-readiness-audit-history.md), which
retain historical missing-feature findings. The governing scope remains roadmap
sections 7–10 and 15, the MVP design brief, and the Flow DAW contracts. Native
frontend work and historical callback-gap diagnosis are owner-deferred; physical
hardware qualification and release packaging remain separate open gates.

## Current verification

The final Desktop core run passed **3,522 main tests + 21 MIDI tests**, with
14 skipped and zero failures. The separate long tier passed **34/34**, including
legacy corpus and audio determinism checks. Both solution builds passed and both
tracked-file mutation audits were empty. Compact authoritative reports are saved
in `../../baselines/backend-readiness/`; full logs/TRX remain at
`/tmp/flow-backend-readiness-core` and `/tmp/flow-backend-readiness-long`.

Additional verified gates:

- Affected backend/platform/hosting/API suite: 579 passed, no failures/skips.
- Web-target shared model/backend and Phase47/48 checks: 449 passed, 7 skipped.
- Actual WASM AppBundle publication and restored Desktop Release build: passed.
- Published language-only artifact: all seven exact-output examples passed;
  dependency libraries are only LanguageHost and flow-language, and process
  closure checks found no music/audio libraries.
- Real LSP completion/hover and clean shutdown passed under filesystem/device
  tracing, with no device or sample accesses.
- Verification-script tests: 37 passed.

The core suite includes simulated platform-host behavior; its name is not proof
of physical hardware testing. Physical device, native UI and installer gates stay
open as explicitly scoped below.

## Backend requirement matrix

Implementation and named tests below were checked against the roadmap/MVP/Flow
contracts and the current verification results. Rows cover backend requirements;
UI interaction and hardware results cannot be inferred from headless tests.

| Requirement | Current implementation / evidence | Remaining work or limit |
| --- | --- | --- |
| Language-only runtime and analysis | `flow-language`, `scripts/LanguageHost`, characterization and analysis suites; Phase 0–5 baseline artifacts | Standalone publish, seven example/closure checks and traced LSP smoke passed. |
| Independent sessions and bounded jobs | `SessionServices`, `MusicSession`, process workers, hosting tests | Workers are not an OS sandbox. |
| Detached notes and timing | `CompositionSnapshot`, project tempo/meter maps, arrangement playback tests | Step tempo and bar-boundary meter; no ramp/pickup policy. |
| Flow-first outputs | `FlowDawGenerator`, `flowDaw.flow`, mixed-output tests | Named notes/audio/graphs/instruments; no MIDI file needed. |
| Captured context and access | Generator context/tuning/assets, module/native policy tests | Bundled imports and explicit grants; legacy implicit sample lookup remains denied. |
| Sample-authoring deliverable | `examples/instruments/project-sample.flow`, `GrantedSampleExampleBuildsRoutesAndReopensWithIdenticalPlayback` | Complete backend example, including full-phrase reopened playback parity. |
| Flow plugin definitions and lifecycle | Manifest/build/parameter/instance/reload tests; public plugin examples | Native controls/editor UI remains frontend work. |
| Shared DSP and modulation | `AudioGraphDefinition`, prepared graphs; graph, oscillator, envelope, sample and signal tests | Supported primitives only; legacy full-buffer effects remain offline-only unless adapted. |
| Independent devices and ordered effects | Effect-chain compiler, instance commands, effect-chain and plugin tests | Advanced routing beyond exposed graph buses remains deferred MVP scope. |
| Safe publication and live controls | Playback coordinator/session, bounded queue, meter/control/monitor tests | Concurrent publication retirement, allocation, stale-worker and lifecycle tests passed; long compatibility tier passed. |
| Editable notes and transforms | Note commands, shared editing services, editable-source and note tests | Selection/zoom/shortcuts are frontend state. |
| Clipboard and grouped edits | `NoteClipboard`, `EditSequences`, clipboard tests | Backend complete; OS clipboard adapter deferred with UI. |
| Clip placement and windows | Clip commands, arrangement/clip tests, public Flow APIs | Split retriggers sustained notes; moving audio does not stretch it. |
| Clip gain/fades | Source-frame envelopes, processing/export tests | Schema 16; trims preserve original envelope anchors. |
| Track add/rename/reorder/remove | `ProjectTrackCommands`, track command tests | Add binds an explicitly exposed unused graph input; graph expansion is separate. |
| Saved track color | `ProjectTrack.ColorRgb`, `SetColor`, `dawTrackColor`; schema 17 and track command tests | Implemented; undo, migration, Flow reconstruction and unchanged audio verified. See track-colors handoff. |
| Mute/solo, gain/pan and automation | Track commands, mixer authoring/host, graph/public automation tests | Step/linear automation; smoothing uses shared kernels. |
| Transport and metering | Prepared/queued transport, output session, coherent meter snapshots | Physical recovery/latency still unverified. |
| Continuous click and count-in | `PreparedMetronome`, `MetronomePlayback`, queue/host tests | Host setting, not saved; toggle outside a take. Receipt/render timestamps, not DAC timestamps. |
| Live MIDI and audition | Shared native input owner, live voices and monitoring host tests | Hardware MIDI qualification remains open. |
| MIDI takes and tuning | Recording host/session, captured pitch map, project tuning tests | Unmapped keys silent; chromatic input uses sharp spelling. |
| MIDI files | Project import/export adapters and tests | Explicit 12-TET adapter; no MPE/bend/sustain/GM reconstruction. |
| Audio import/assets/waveforms | Audio asset files, packages, waveform preparation and tests | Bounded decoded assets; no streaming sampler/audio recording claim. |
| Smart undo/redo | `IUndoableAction`, history/composite/snapshot actions and tests | Captured redo, grouped gestures, bounded memory and dirty-state tracking. |
| Save/open/migration | Project JSON/file/package, fixed fixtures and migration tests | Fixtures are representative, not historical released files; schema 17 color defaults/version rejection are tested. |
| Autosave/recovery | Polled host autosave, recovery APIs and tests | Requires host polling; explicit recovery choice belongs in UI. |
| Missing assets/plugins | Preparation diagnostics, retained unavailable bindings, repair paths | Presentation/resolution dialogs remain frontend work. |
| Full/range/stem export | Bounce/stem writers and tests | Float32 WAV; range hard-crops after continuous preroll, nonlinear stems need not sum to mix. |
| Combined musical lifecycle | `BackendLifecycleTests.BuildEditAutomateUndoRecoverAndExportRetainOneMusicalProject` | Headless proof; native end-user walkthrough remains deferred. |
| JUI boundary | Package load/frame/dispose checkpoint, UI-independent backend APIs | Native integration and 10,000-note responsiveness gate remain deferred. |
| Current docs/CI | Architecture, integration handoff and current audit; core/platform/long CI tiers and verifier tests | Current Web/WASM, language artifact and analysis evidence recorded; hardware/release claims remain separate. |

## Scope disposition and next stage

The original backend goal is satisfied by the shared language/model/audio/host
implementation and the checks above. No confirmed in-scope backend feature gap
remains. The MVP's interaction requirements have callable backend paths; their
native presentation and interaction gates are the next stage, not silently
claimed complete here.

| Roadmap completion checklist | Disposition |
| --- | --- |
| General-language behaviors and executable docs | Characterization/contracts and all seven standalone examples verified. |
| Language-only closure | Published artifact and process closure verified. |
| Musical compatibility | Core music tests plus 34 long compatibility/determinism checks passed. |
| Session isolation | Concurrent engine/configuration/advisory tests passed. |
| Non-executing analysis | Analysis tests and traced LSP smoke passed. |
| Cancellation and stale rejection | Worker termination, publication, source acceptance and edit/undo ABA tests passed. |
| Detached music/shared transforms | Snapshot, editing, clipboard, processor and Flow reconstruction tests passed. |
| Offline rendering/memory/progress | Existing linear-assembly baseline retained; streaming, cancellation, range/tail and export tests passed. |
| Real-time ownership and measured deadlines | Ownership/queue/allocation tests passed. Prior measured evidence retained; physical sustained/latency qualification remains open and historical gap investigation is owner-deferred. |
| Public Flow instruments/effects | Public synth/sampler/composed-effect examples, isolated build, independent instances and shared-kernel tests passed. |
| Persisted instances/automation/presets/source | Project/plugin/history/Flow reconstruction tests passed. |
| One usable piano-roll/record/mix/export workflow | Combined headless lifecycle and MIDI hosts passed. Native visual walkthrough is explicitly the next stage. |
| Undo/recovery/assets/migrations | Command/history, recovery, package/hash validation and fixed migration fixtures passed. |
| Current docs/CI | Current architecture, this audit and native integration handoff describe actual APIs/limits; historical handoffs remain marked as history. |
| Linux packaging/device configurations | Release gate remains open; not a claim of backend-to-frontend readiness. |

Next: implement the approved workspace in JUI using
`../../design/flow-workspace/NATIVE-INTEGRATION.md`. The native stage must verify
10,000-note rendering/editing responsiveness, keyboard/selection/zoom behavior,
editor/control integration, window/graphics/accessibility and the complete user
walkthrough. Recent-project lists and editor text/selection state belong there.

Do not treat readiness as hardware certification: physical MIDI/output latency,
unplug/replug, supported-device qualification and installable Linux releases remain
open. Multitrack audio recording, external native plugins and advanced time
stretching remain explicitly deferred product scope. The historical callback-gap
investigation remains deferred by the owner, with its evidence preserved.
