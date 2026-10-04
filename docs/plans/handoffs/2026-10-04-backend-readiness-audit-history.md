# Backend readiness audit — 2026-10-04

Status: **not ready to declare complete**. This is the first evidence pass, not a
replacement roadmap or a claim that all requirements have been inspected. Preserve
the original goal and close the gaps below before the full completion audit.

Sample-authoring follow-up: the checked-in project sample example now runs through
asset import/grant, isolated build, routing, save/reopen and full-phrase playback
parity. **13 related tests passed**; see `2026-10-04-project-sample-example.md`.
The example deliverable is complete. Documentation consolidation, refreshed broad
qualification and the final requirements audit remain open.

Current follow-up: continuous metronome monitoring is implemented alongside counted
recording. The latest affected regression passed **576 tests**, zero failures/skips;
see `2026-10-04-continuous-metronome.md`. Range export, clip envelopes and grouped
note clipboard operations are also implemented. The entries below are chronological
audit history, including superseded missing-feature findings. Remaining work is the
captured-sample example, consolidated current-state documentation, refreshed full
core/browser/WASM qualification and final requirement-by-requirement audit.

Count-in follow-up: project host, queue and audio transport now support counted MIDI
recording with exact boundary admission and early-input draining (34 related tests
passed). See `2026-10-04-counted-recording-host.md`. Continuous metronome monitoring
remains required, along with authoring example and final documentation/qualification.

Clip-envelope follow-up: per-clip gain/fades now persist, survive editing/history,
export to Flow and use shared playback. Offline processing applies the envelope
exactly once. 558 affected tests passed; see `2026-10-04-project-clip-envelopes.md`.
Metronome/count-in, authoring example and final documentation/qualification remain.

Clipboard follow-up: detached note copy/paste and atomic cross-sequence/source
gestures are now implemented, with captured undo/redo and persistence evidence.
33 focused/related tests passed. See `2026-10-04-note-clipboard.md`. Native clipboard
UI remains frontend work; clip gain/fades and metronome/count-in remain backend gaps.

Range-export follow-up: exact selected WAV export now preserves continuously
rendered effect history, cancellation and atomic publication (10 focused tests
passed). The pre-change full core checkpoint passed 3515 tests, 14 skipped, no
failures or tracked mutations. See `2026-10-04-range-export.md`; metronome/count-in,
clip gain/fades and clipboard/grouped-edit audit remain required.

**Expanded MVP audit:** metronome/count-in, per-audio-clip gain/fades and selected
WAV export ranges are still missing. Note clipboard/grouped gesture APIs also need
explicit verification. See `2026-10-04-authoring-and-mvp-audit.md`. These stay in the
backend goal; earlier summaries of the remaining work were incomplete. Formant
generator support is now implemented (7 focused native-policy tests passed).

Style follow-up: ordinary generators support bundled and inline improvisation
styles without implicit style-file discovery. Backend plus Phase36 regression:
742 passed, zero failures/skips. See `2026-10-04-generator-styles.md`; remaining
authoring/sample review and full core/browser qualification are still required.

Project MIDI tuning follow-up: monitoring preparation and recording now resolve
captured project tuning using Flow pitch conversion. Actual host tests and the
affected regression suite passed **548 tests** with no failures/skips. See
`2026-10-04-project-midi-tuning.md` for sharp-spelling policy, unmapped keys and
remaining sample/style/final-qualification work. Earlier tuning-gap rows are historical.

Latest qualification: browser checks passed 424 tests (7 skipped), actual WASM
publication and restored Desktop build succeeded, and the combined backend
lifecycle test passed. Live MIDI now supports a captured immutable pitch map at
the audio boundary (11 focused tests passed); project tuning resolution and
monitoring/recording wiring remain required. See `2026-10-04-live-pitch-map.md`.

Follow-up: MIDI note import and arrangement export are implemented in
`2026-10-04-project-midi-import.md` and `2026-10-04-project-midi-export.md`.
Their host paths, captured import history and scheduler-derived export have focused
coverage; the latest affected suite passed 510 tests. The initial missing-adapter
finding below is historical; keep its explicit controller/tuning limitations and
the remaining integrated qualification in scope.

Follow-up: explicit clip trim/resize and linked repeat now have project commands,
shared Flow APIs and timing/history coverage; see `2026-10-04-clip-trim-repeat.md`.
The affected backend suite plus module-surface checks passed 514 tests. The original
missing-operation row below is historical. Continue work item 2 with persistent
track mute/solo; remaining audit items are still required.

Follow-up: persistent track mute/solo is implemented in schema 12, lowered through
Flow gain nodes after track inserts, and preserved by Flow export and undo/redo.
See `2026-10-04-track-mute-solo.md` for monitoring, shared-master and stem semantics.
Affected backend/API checks: 517 passed. Work item 2 is implemented; continue with
work item 3 (saved render preferences and committed migration fixtures).

Follow-up: fixed representative schema 1/6/11/12 project fixtures now live under
`flow-lang.Tests/fixtures/projects`; four migration/Flow-export/history checks pass.
See `2026-10-04-project-migration-fixtures.md` for coverage and limits. Saved render
preferences remain unimplemented and are the next action in work item 3.

Follow-up: schema 13 now persists render sample-rate/block preferences, with
captured undo, Flow authoring and default export use. Snapshot transforms preserve
the settings; old fixed fixtures assert migration defaults. See
`2026-10-04-project-render-settings.md`. Continue with work item 4, generation
context/capabilities; full integrated and cross-target qualification remains open.

Follow-up: full tempo/meter maps and exact source/context revisions are now exposed
to Flow builds; see `2026-10-04-generator-context-maps.md`. Affected backend/API
checks: 525 passed. Work item 4 remains open for tuning snapshots, host-owned asset
access and ordinary-generator capability restrictions.

Follow-up: ordinary-generator imports now use a bundled-only resolver; packaged
plugins retain pinned dependencies. See `2026-10-04-generator-module-policy.md`.
This does not restrict native calls yet; native/indirect IO review, tuning and host
asset resolution remain open under work item 4.

Follow-up: ordinary generators now use reviewed default-deny native signatures and
disable implicit sampled-instrument disk access. See `2026-10-04-generator-native-policy.md`.
Host-owned sample/style assets, tuning and remaining authoring-surface coverage are
still required; this policy does not make the worker an OS sandbox.

Follow-up: selected project audio assets can now be hash-checked by the host and
supplied as captured PCM to direct/isolated generators through `dawAssetSample`.
See `2026-10-04-generator-host-assets.md` for limits and remaining per-source grant,
legacy sample/style and tuning work. Work item 4 remains open.

Follow-up: schema 14 persists ordinary-source sample grants, with undoable changes,
Flow authoring and reuse by default rebuilds. See `2026-10-04-generator-asset-grants.md`.
Tuning context and remaining sample/style authoring integration still require work.

Follow-up: generator tuning is now captured/applied/exposed and persisted in schema
15 (including historical source contexts); process protocol 6 transports it. See
`2026-10-04-project-tuning-context.md`. Live MIDI/recording still use 12-TET and need
contract review; sample/style authoring integration and full qualification remain.

Full-core checkpoint: 3503 passed, 14 skips, zero failures and no tracked mutations
after restoring the legacy SampleCache constructor. See
`2026-10-04-backend-core-checkpoint.md`. The audit also confirmed missing dedicated
tempo/meter edit commands and typed Flow clip-nudge/setter APIs; implement these
before final integrated qualification. This passing checkpoint does not close
the remaining contract items, browser/WASM or hardware gates.

Follow-up: dedicated undoable tempo/meter commands, matching Flow construction,
typed Millisecond/Second relative offsets and absolute clip-offset setters are now
implemented. See `2026-10-04-project-timing-commands.md`. Browser/WASM, integrated
lifecycle, tuning/live-MIDI and sample/style review remain open.

Sources: roadmap sections 7–10 and 15 in
`../2026-09-20-flow-restructuring-roadmap.md`, and
`../../design/flow-workspace/FLOW-DAW-CONTRACTS.md`. Native JUI/frontend work and
historical callback-gap diagnosis remain deferred by owner direction. Physical
device/latency qualification and release packaging must remain documented separately;
unit tests do not establish those gates.

## Requirements inspected

| Requirement | Current source evidence | Audit result / remaining proof |
| --- | --- | --- |
| Detached musical data and prepared playback | `flow-music-model/CompositionSnapshot.cs`, `flow-studio-engine/ArrangementCompiler.cs`, `flow-audio/Graph/PreparedGraphPlayback.cs` | Implemented, with affected regression coverage. Recheck full gates before final signoff. |
| Public Flow generator, instrument, effect and processor APIs | `flow-lang/Hosting/FlowDawGenerator.cs`, `flow-lang/flowDaw.flow`, plugin packages/examples, P7-44 handoff | Implemented core paths. Audit the complete authoring context and default worker capabilities below. |
| Captured source acceptance, stale rejection and undo/redo | `ProjectDocument.cs`, `ActionHistory.cs`, `CompositeAction.cs`, clip-processing operations | Implemented, including edit/undo ABA protection for clip processors. |
| Editable note gestures and shared transforms | `ProjectNoteCommands.cs`, `flow-music-model/Editing/NoteEditing.cs`, `Quantization.cs`, `Transposition.cs` | Add/delete/move/resize/velocity/pitch and transformations exist. Clipboard/selection adapters and per-gesture batch semantics still need explicit coverage review. |
| Clip placement, splitting and linked duplication | `Clips.cs`, `ProjectClipCommands.cs` | Implemented. **Explicit trim/resize and repeat/loop authoring operations are absent from the project command API.** Constructors/split/delete combinations are not proof of the required complete workflow. |
| Tempo/meter | `ProjectTiming.cs` | Piecewise-constant tempo and bar-boundary meter are explicit. Review host editing and document the supported step-tempo policy against ramp expectations. |
| Mixer gain/pan, routing, effects, automation | `ProjectMixerAuthoring.cs`, `ProjectMixerHost.cs`, `ProjectCompiler.cs`, `EffectChainCompiler.cs` | Implemented graph controls. **Persistent track mute/solo is not represented by `ProjectTrack` or dedicated commands.** Offline solo preparation for stems is not the DAW mixer mute/solo workflow. |
| MIDI keyboard/recording/monitoring | `ProjectMidiRecordingHost.cs`, `ProjectMidiMonitoringHost.cs`, shared native-input owner | Backend paths and fake-device/worker tests exist. Hardware qualification remains open. |
| MIDI file import/export in the project | `flow-midi/Midi/MidiParser.cs` is internal to a CLI; `flow-music-io/MidiCompositionExporter.cs` exports a composition | **Missing project adapter.** No host/project MIDI file import command or arrangement MIDI export path was found. CLI conversion and composition export alone do not satisfy the DAW requirement. |
| Full mix and selected stems | `ProjectBounceFile.cs`, `ProjectStemFiles.cs`, `PlaybackWaveWriter.cs` | Bounce and aligned processed stems implemented. This pass added explicit captured track selection; focused suite **5 passed**, `/tmp/flow-selected-stems-tests.log`. Whole-project preflight and common duration remain the selected-export policy. |
| Saved sample-rate/render preferences | `ProjectSnapshot` and schema 11 `ProjectJson.Data` | **Missing.** Export/session call arguments exist but are not persisted project preferences. Add versioned data and preserve it through every snapshot transformation and Flow export. |
| Save/open, assets, autosave/recovery | `ProjectFile.cs`, `ProjectPackage.cs`, `AudioAssetFiles.cs`, `ProjectAutosaveHost.cs`, `ProjectRecovery.cs` | Implemented backend APIs with failure/cancellation/history tests. Verify combined open/recover/edit/save/export lifecycle, not only isolated helpers. |
| Old project migration fixtures | `ProjectJson.cs`, persistence tests | Versions 1–11 are accepted; tests exercise compatibility branches. **No committed `.flowproject`/versioned project fixture files were found.** Roadmap explicitly requires small committed migration fixtures. |
| Full generator context | `GenerationContext`, `FlowDawGenerator.ContextValue` | **Incomplete against authoring contract:** Flow receives initial BPM/meter, seed and numeric parameters. Source identity/revision, complete maps and tuning context need review/implementation, rather than treating preserved note Hz as a full project tuning snapshot. |
| Default worker access | `FlowDawGenerator` uses reviewed policy for packaged plugins and unrestricted policy for ordinary generators | **Needs contract review and tests.** Isolated process execution is not an OS sandbox. Determine and implement the intended default capabilities for DAW generators without breaking the explicitly general-purpose language host. |
| Analysis/editor and JUI boundary | Phase 0–5 artifacts and JUI package handoff | Prior milestones have evidence; rerun current language/browser/analysis gates. Native UI stays deferred. |
| Integrated stability and documentation | Recent affected regressions and handoffs | Not yet a complete integrated qualification. The original phase narratives contain stale “future work” claims; reconcile after implementation gaps close. |

## Work order

1. Project MIDI file import/export over the detached model: bounded parsing,
   channel/track and tempo/meter policy, captured import action, cancellation,
   source-window/nudge/tuning limitations made explicit, shared export scheduling.
2. Track mute/solo with persisted state and public Flow equivalents, and explicit
   clip trim/repeat commands with captured identities and timing tests.
3. Versioned project render preferences, committed old-schema fixtures and tests
   proving preservation through editing, processing, Flow export and recovery.
4. Finish the generation-context/capability review and outstanding contract checks;
   add required APIs/tests rather than silently narrowing the contract.
5. Exercise an integrated headless musical lifecycle including plugin build,
   edits/automation, undo/redo, save/reopen/recovery and mix/stem/MIDI export.
6. Reconcile docs and run full core, affected platform suite, browser compatibility,
   actual WASM publish and restored Desktop build. Audit remaining explicit gates
   and limitations before declaring backend readiness.

The last broad checkpoint before this audit was **499 affected backend tests**;
it is not a new full-core or hardware qualification. This audit closes only the
selected-stem API omission so far. The rest is required work, not waived scope.
