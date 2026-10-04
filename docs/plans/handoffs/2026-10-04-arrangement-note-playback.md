# Arrangement note playback and Flow sine instrument — 2026-10-04

P6-16 / P7-04 connects linked score clip windows to prepared note voices, the shared
mixing graph, transport and callback path. The previous goal turn added the shared
graph; this turn adds actual arrangement lowering and note lifecycle behavior.
The full frontend-readiness goal remains active.

## Implemented

- Added `Flow.Studio.Engine` (references only Studio.Model and Audio) to the solution
  and test project. Arrangement preparation is separate from document data and has
  no interpreter, UI or physical device dependency.
- `ArrangementCompiler.Prepare` resolves source/layer bindings and track-to-input-bus
  bindings, applies clip source offsets/windows, project tempo and constant signed
  millisecond nudges, then creates detached frame events and prepared graph playback.
- Source tempo remains metadata; project tempo owns event placement. Articulation,
  tied following rests, overlap and pedal durations are lowered explicitly. Fixed
  tie/pedal seconds remain seconds through tempo changes via
  `NoteDuration.ProjectDuration`; legacy renderer arithmetic is unchanged.
- Crossing a window start retriggers the remaining note; crossing its end sends
  note-off at that edge. Splitting a held note releases the left and retriggers the
  right. Negative nudges reconstruct held notes at zero and omit already-ended
  material. Source objects are never rewritten.
- Shortened/missing source layers keep the visible clip extent as silence with
  diagnostics. Notes retain clip/note/placement IDs, repeat index, tuned frequency,
  velocity and source gain/pan. Repeats outside a visible window are skipped
  arithmetically instead of expanding the entire composition.
- `PreparedNotePlayback` implements the prepared cursor contract with preallocated
  voice state, bounded polyphony, deterministic oldest-voice stealing, phase-zero
  starts, attack and release. Release tails contribute to offline/live duration;
  visible empty clip extent is also retained.
- Seek/loop/stop clear voices and reconstruct held notes at phase zero. An interval
  maximum tree finds at most the voice limit of held notes, pruning expired notes
  without a full linear scan on the callback. Already-released tails are not
  reconstructed. Pause freezes state through the existing transport.
- `NoteInstrumentCatalog.Sine` exposes `flow.sine@1`, tuned-note/stereo port metadata,
  voice/attack/release controls, zero latency and reset policy. The same
  `SineVoiceSettings` is exposed as Flow's `DawInstrument` through
  `(dawSine 64 5ms 20ms)`; these configuration edits require preparation.
- Preparation limits include authored/scheduled notes (100,000 default), selected
  occurrences (100,000), note/window intersection work (16 × event budget),
  simultaneous onsets (1,024 per frame), voices (1–256), track buses (1–64), and
  attack/release ranges. Existing graph buffer limits still apply. Preparation
  checks cancellation and publishes nothing on failure.

## Verification

The affected StudioModel/PlatformAudio/MusicModel/StaticBinding regression run
passed **181 tests**, 0 failed. After adding the final duration test/work-budget
check, the targeted arrangement suite passed **9 tests**, 0 failed. Coverage:

- source-window split through a sustained note across a project tempo change;
- custom frequency preservation, release length, source provenance;
- negative nudge and shortened-source silent remainder;
- a late window in a two-million-repeat source without expanding earlier repeats;
- missing source diagnostics with stable clip duration;
- deterministic voice stealing and output across different block partitions;
- held-note retrigger on seek and no reconstruction of expired release tails;
- no managed allocation during prepared Read/held-note Seek after warmup;
- Flow generator → Flow instrument definition → arrangement → mixer → callback
  equality with offline playback through EOF;
- cancellation/resource rejection and explicit rejection of unsupported source
  reverb; fixed 100 ms tie extension across a tempo change.

Logs: `/tmp/flow-arrangement-playback-regression.log` and
`/tmp/flow-arrangement-playback-final.log`. Existing package/analyzer warnings
remain. Callback parity is in-memory, not a new hardware/listening/stress result.

## Remaining backend work

Audio asset clips are currently rejected, not silently ignored. Source-local
reverb/voice-pool settings and note portamento need explicit device bindings and
supported processors; these are also rejected rather than rendered approximately.
The sine instrument is the first note source, not a general user-defined instrument
graph. Instrument definitions are available to Flow/native hosts but are not yet
part of structured generator results or persisted project source transactions.

Next: audio asset/sample playback and clip windows, expanded instrument/effect
state and bounded tails, graph/audio output through the worker protocol, source
acceptance/binding reconciliation and project persistence, automation/modulation,
then full end-to-end editing/export evidence. Keep native frontend work deferred
and do not resume the historical callback-gap investigation.
