# Packaged effect chains — 2026-10-04

P7-40 closes the source-based effect-chain gap identified in the instance handoff.
Ordered track/master inserts now preserve package identity, public values,
automation, bypass, undo, persistence and executable Flow restoration.

## Model and host

ProjectRouting.Effects is an immutable ordered list of ProjectEffect references:
Binding identifies the accepted packaged graph output, TrackId selects a track or
null for master, and Bypassed controls processing. Relative order within each track
or master is processing order. The routing limit is 256 unique effect bindings.
The same binding cannot occupy multiple inserts or also be the master mixer;
duplicate the source for independently controllable instances.

ProjectEffectCommands.Set validates available single-stereo-input effect packages
and captures the complete chain change as one action. Reordering, bypassing, moving
between track/master, insertion and removal use this same operation. No-op lists
create no history. Removing an insert keeps its source and automation for reuse.
Source removal now rejects inserted devices. Track removal detaches its inserts;
rename/reorder/assignment, instrument duplication and mixer authoring preserve them.
ProjectPlaybackSession.SetEffects requests normal bounded preparation after an edit.

Project schema **11** saves insert order/identity/bypass; schemas 1–10 still load
without inserts. Older schema tags cannot silently carry nonempty effect lists.
Snapshot loading retains missing bindings for repair, while interactive insertion
requires an available device. Missing selected effects fail preparation and leave
the last acknowledged playback active.

## Shared processing and controls

BoundEffect.FromSource applies saved public values through the package contract.
EffectChainCompiler lowers inserts into the ordinary AudioGraphDefinition:

- Each track's raw bus passes through its chain once before feeding every branch
  of the original mixer. Multiple input nodes for a bus share that processed signal.
- Master inserts follow the original mixer output in order.
- Original mixer node IDs remain addressable. Inserted processors receive distinct
  generated IDs, with an immutable (binding, source-node) mapping returned alongside
  the graph. Local node names can collide across packages without sharing state.
- Public/direct effect automation and public live gestures use the mapping. Playback
  preparation owns it; IDs are not persisted and handles are generation-scoped.
- Bypass omits the processor and its tails from the prepared graph and offers no
  active live preview. Changing chains uses the existing reset-on-replacement policy;
  running DSP state is not migrated or crossfaded.

ProjectCompiler uses this graph for the existing prepared arrangement, including
monitoring, offline rendering, meters and tail calculation. There is no parallel
effect callback. The combined graph retains the 1,024-node limit and existing
buffer/tail/automation budgets. Composition is worker-side and cancellation-aware.
Tests verify allocation-free processing of the resulting ordinary graph.

Serial inserts accept one stereo input bus. Multi-input sidechains need a separate
explicit routing contract; they are rejected here. Existing manually authored mixer
graphs can still express their ordinary multi-bus routing.

## Flow contract

`dawInsertEffect(project, trackId, effectBinding, bypassed)` appends an insert;
the empty track string selects master. FlowProjectExporter emits the saved package
resources and explicit graph definitions, then these ordered calls. Flow construction
preserves unresolved binding references like dawRouting, allowing repairable projects
to export/reopen; preparation still refuses to activate unresolved devices.
Native module snapshots now show **703 registered / 697 reachable**, with the same
six legacy unreachable registrations.

## Verified checkpoint

- Final full core: Desktop solution build passed, **3,398 language/backend + 21
  MIDI tests passed**, zero failures, 14 skipped; tracked mutation audit empty.
  `/tmp/flow-effect-chains-core-final/verification.json`.
- Final browser-target compatibility suite: **366 passed, 7 skipped, zero failures**.
  `/tmp/flow-effect-chains-web-final.log`.
- Actual WebAssembly AppBundle publish passed: `/tmp/flow-effect-chains-wasm.log`.
  Final core verification then restored the Desktop target.
- Earlier affected suite passed 465 tests before the final missing-reference
  recovery assertion/test was included in the full checkpoint.
- Coverage includes noncommutative ordering, track/master placement, shared bus
  processing, node-name collisions, mapped live/automated controls, bypass/tails,
  budgets, captured undo/redo, schema rejection, save/reopen, Flow audio parity,
  track edit preservation, and last-good playback after a missing effect.
- `git diff --check` passed. No physical audio/MIDI qualification was performed.

## Continue

Audit safe reload compatibility next, especially parameter units/schema changes
under an unchanged plugin ID, and preserve pinned old sound on incompatible builds.
Remaining plugin categories (note transform/offline audio), integrated bounce/stems,
autosave/recovery workflows and final backend readiness documentation remain open.
Do not call the whole roadmap complete from this checkpoint. Native JUI frontend
and historical callback-gap investigation remain deferred as directed.
