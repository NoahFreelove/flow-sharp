# Flow DAW authoring and playback contracts

Decision date: 2026-10-02. Establishes the architecture requested after review of the browser prototype. Backend implementation and verification status is now recorded in the [readiness audit](../../plans/handoffs/2026-10-04-backend-readiness-audit.md). Native JUI remains the target; the browser's MIDI/Web Audio bridge is temporary. This decision supplements roadmap sections 8–9 and Phases 6–9 without closing their native UI, hardware or release gates.

## 1. One musical engine

All bundled instruments, musical transformations, effects, modulation, mixing, sends and routing must have public Flow representations. Visual editing and Flow authoring use the same validated musical model and processor definitions. Live playback and offline export execute the same prepared graph and DSP kernels, with explicit sample-rate and initialization settings.

Track display order is independent of its saved graph input bus. Reordering does
not reroute audio; removing a track leaves its graph input silent. New tracks bind
to an explicitly exposed unused graph input. Bus identity is included in saved
projects and exported Flow construction code.

Track color is optional project metadata: an opaque sRGB integer `0xRRGGBB`,
with null meaning an automatic frontend-selected color. It changes neither routing
nor sound. `ProjectTrackCommands.SetColor` captures one undoable edit;
`dawTrackColor(track, rgb)` reconstructs it in Flow (use -1 for automatic color).
Project schema 17 stores this value; older projects default to automatic color.

The UI never supplies a second implementation of a musical effect. Native DSP kernels, scheduling, file/device access and UI code can remain C#/native implementation details. “Everything in Flow” means complete musical expressibility and shared execution semantics, not interpreting Flow on the audio callback.

Every device definition identifies its version, typed ports, stable parameter IDs, units, defaults, bounds, smoothing, automation behavior, latency, tail and state-reset behavior. Visual controls and generated source derive from that definition. Series order is preserved; parallel paths, buses, sends and sidechains are explicit connections. Bypass behavior is defined per processor and preserves declared latency. Unsupported operations fail validation; they are not silently replaced with approximate browser effects.

Existing full-buffer Flow effects need adapters or shared-kernel extraction before being exposed as real-time devices. An offline-only processor remains usable through render/freeze, with that capability visible. Each promoted processor needs equivalence tests across its Flow, visual, live and export paths; tolerances must be stated rather than assuming byte identity across platforms.

## 2. Authoring module and output boundary

Adopt `flowDaw` as the authoring library name, exposed as `use "@flowDaw"`. New generator/device templates insert that visible import automatically. Importing the library registers its API; it does not implicitly execute a generator, export a file or search for a variable named `song`.

A versioned declarative descriptor identifies the entry-point export, device/source identity, role, parameters and ports. The host invokes that entry point with an immutable generation/build context. A generator returns one structured result containing named outputs, rather than calling `writeMidi` or writing temporary files. Helpers can wrap a single composition into the result. Exact descriptor grammar and entry-point signature must be implemented against Flow's existing type/module system before publishing executable templates; no new grammar is assumed here.

Generation context supplies tempo/meter/tuning snapshots, explicit seed, parameter values, source identity/revision, asset resolver and execution budget. Workers produce detached values and diagnostics. A result is accepted only for the matching source, parameters and context revision. Failed, cancelled, timed-out or stale work leaves the last valid output playing. Accepted source/output revisions form one undoable document transaction.

| Role | Inputs | Outputs / execution |
|---|---|---|
| Composition generator | Context and parameters | Immutable compositions, named layers, optional instrument/routing bindings; worker |
| Note processor | Composition/events and parameters | Transformed musical data with provenance; worker or explicitly prepared bounded event processor |
| Instrument | Tuned note events and control signals | Prepared audio graph with per-voice/shared state |
| Audio effect | Audio buses and controls | Prepared audio graph |
| Modulator | Clock, controls and optional events | Typed control/audio-rate signal graph with explicit rate conversion |
| Offline audio producer/processor | Context, assets and optional buffers | Owned audio asset plus format, duration, time basis and provenance; worker |

A result may combine score layers, audio assets and graph definitions. The existing CompositionSnapshot is the score foundation: retain tuned frequencies, articulation, timing and provenance directly. Extend the host result with routing/device bindings; do not try to squeeze these into MIDI. Pure audio displays as a waveform; score output displays as notes. MIDI is an optional import/export adapter.

Arbitrary Flow runs only during bounded worker evaluation. Prepared DSP executes on the audio thread with preallocated state. General user note callbacks are not implicitly real-time safe. Parameters update through the engine's command/automation path; topology changes require a new validated build and safe publication.

## 3. Source ownership and generated code

The persisted project model is authoritative for visual arrangement and routing. Its Flow exporter produces explicit, valid construction operations for the supported project subset, including source identities, source windows, graph connections, parameters, seed and pinned dependency versions. Verbose output is acceptable. Exported construction code must reconstruct equivalent project semantics through the same public APIs; it need not recover the user's original formatting or abstractions.

Handwritten code remains a source-owned component with a declared interface. The DAW does not promise to convert arbitrary loops, procedures or branching back into visual device controls. Code preview of a visual graph is read-only until explicitly converted to source-owned code. Conversion is undoable and explains the loss of internal visual editing; exposed parameters remain editable. Do not use repeated text rewriting as the project model.

A generated source owns its code and successful revision, immutable results, layer identities and source provenance. A clip owns a reference to that source, destination track, arrangement anchor, source window and timing nudge. Duplicate/split share the source by default. Explicit conversion creates independent editable musical content and retains provenance. Regeneration updates linked clips without resetting their placements or source windows. A shortened source leaves out-of-range portions silent with a diagnostic; it does not silently stretch, move or delete clips. Newly added/removed output layers have explicit binding reconciliation; unmatched connections remain visible for repair.

## 4. Placement operations

The following names define placement semantics. Current executable overloads are
declared in `flow-lang/flowDaw.flow` and use explicit project/meter context where
required; the compact examples here describe semantics, not complete call sites.
Operations return new values; document actions install them atomically. All
positions must be finite; invalid input fails without partial edits.

| Operation | Contract |
|---|---|
| `(alignToBar clip barNumber)` | Set the clip's arrangement anchor to the start of the specified **one-based project bar**, resolved using the project meter map. Clear its millisecond nudge so its actual start is aligned. Preserve source offset and length. |
| `(split clip point)` | `point` is a typed musical duration from the **start of this clip's visible source window**, before its timing nudge. Return an ordered pair of clips. Require `0 < point < length`. Left retains its ID; right receives a new persistent ID. Both retain source/layer references. |
| `(relativeOffsetMs clip duration)` | Add a signed time duration to the clip's existing playback nudge. Require a typed time value such as `25ms`, not an untyped number. Preserve musical anchor, source window and length. |
| `(setOffsetMs clip duration)` | Set an absolute nudge; provided for idempotent generated project code and numeric inspector edits. |

Musical positions use quarter-note units internally; a beat value is not automatically a bar or a meter-denominator beat. Bar 1 begins at project quarter 0. For example bar 3 starts at quarter 8 in constant 4/4 and quarter 6 in constant 3/4. Bar alignment is resolved when the action runs: it is not a persistent “follow bar 3” constraint. The resolved anchor is saved; undo/redo restores that result rather than consulting a later meter map. Initial meter-map edits must occur at bar boundaries. Pickup/negative bar numbering is outside this first contract.

Generators may define their own tempo/meter while authoring, but project playback has one master tempo/meter map. In v1, placement of score output uses quarter positions against that project map; source tempo metadata remains available but does not silently change the project tempo. Importing it into the project map is an explicit undoable operation. Rendered audio preserves its rendered seconds and is labeled as such.

For a score clip with arrangement anchor A, source offset O, length L and nudge D seconds, split at P produces:

- Left: anchor A, source offset O, length P, nudge D.
- Right: anchor A + P, source offset O + P, length L − P, nudge D.

The playback time of a source event at quarter S is `projectTime(A + S − O) + D`. Thus moving preserves source content, splitting preserves event times on both sides, and a millisecond nudge stays constant through tempo changes. Split uses the musical P, not seconds divided by the current BPM; conversions from cursor seconds use the inverse project tempo map after subtracting D. The visible clip edges include the nudge. UI snapping and Flow operations use the same service.

At 120 BPM in 4/4, aligning to bar 3 gives quarter 8. Splitting four quarters into an eight-quarter clip gives anchors 8 and 12. Applying a 25ms nudge to both preserves their meeting point, now 25ms late. Aligning either clip afterward deliberately clears that nudge. Operation order matters and code preview preserves it.

Absolute timeline edits need a separately named operation, such as `splitAtProjectBeat`; do not overload `split` with indistinguishable local and project numbers. Likewise add a musical-relative move operation for grid movement rather than converting every drag into milliseconds. Generated project code uses absolute setters or constructors to avoid cumulative nudges when reconstructed repeatedly.

Audio source windows use source frames and sample-rate metadata, not fabricated note positions. An audio clip has an explicit seconds time basis: moving its musical anchor changes placement but does not time-stretch its samples. A typed time/frame split is a distinct overload. A musical ruler cut resolves through project time, then converts to the source frame boundary. Stretching and mixed score/audio clip editing need explicit later policies; do not reuse the score offset equation for audio samples.

Negative nudges are allowed. Material scheduled before project time zero is not emitted during normal playback starting at zero; sustained notes are reconstructed at the start boundary under the seek rules below. Clip/source bounds and host duration/resource limits are validated independently of accepting negative timing nudges.

## 5. Functional first-version cut, seek and tail rules

The owner accepts audible discontinuities for the first implementation. Define them consistently rather than promising seamless arbitrary DSP seeking:

- Cropping is non-destructive. Notes crossing a clip start are retriggered with their remaining duration; notes crossing its end receive note-off at that edge. A split through a sustained note therefore releases and retriggers it. Source data is unchanged.
- Clip-owned instrument/modulation state resets when starting a clip, seeking into it or wrapping a loop. Its local clock begins at the source offset, but delay buffers and other dynamic state start cleared: this is explicitly not equivalent to rendering the skipped prefix.
- Track/group/master effects are shared graph instances, not duplicated by splitting clips. Ordinary clip boundaries do not reset them. A transport seek/stop/loop jump resets their transient state in v1. This can truncate reverb/delay tails; both UI and Flow playback follow the same policy.
- During continuous playback, note-off allows declared bounded release/effect tails. Resource retirement waits for the declared tail budget; unbounded feedback must have a host-enforced maximum. Tail rendering is distinct from the visible clip/source length. Offline export includes those bounded tails.
- Stop cuts voices and clears transient DSP state; pause preserves state and freezes the playhead. Resume continues it. A failed rebuild changes neither document nor active graph.
- No automatic stretch, phase recovery, crossfade or full DSP state reconstruction is implied by split/move. Later preroll/checkpoint and seamless-cut work can add explicit modes without changing source-window semantics.

Offline full-song render and continuous live playback share these policies and deterministic initialization. Seeking into a stateful effect need not sound identical to playing from the beginning. The distinction must remain explicit in tests and UI expectations.

## 6. History, verification and implementation sequence

Each project mutation implements the established action interface with undo and redo. A drag is one action; split installs both pieces as one action; source acceptance installs a complete result as one action. Store resolved IDs, timing and generated content needed to redo without rerunning nondeterministic source. Audio publication follows document commit through the existing safe publication/retirement path.

Implementation sequence (backend slices 1–4 verified in the readiness audit;
native slice 5 is next):

1. Host-neutral arrangement types and shared operations with tests for meter changes, nonuniform tempo, negative nudges, local splits, audio-frame cuts, serialization and exact undo/redo restoration.
2. `@flowDaw` module/descriptor and direct generator-result API, with a minimal composition example requiring no MIDI write. Reject stale context/results and preserve last-good output.
3. Versioned device catalog and prepared graph contract: instrument, effect, modulation and routing primitives with independent instance state. Retain Phase 6 measured callback/telemetry gates before broad DSP porting.
4. One vertical musical slice: generated tuned notes → Flow instrument → ordered Flow effects → group/master → shared live/offline output; visual edits export equivalent Flow construction code.
5. Native JUI workspace consumes these APIs; remove browser synthesis assumptions rather than porting its DSP.

Acceptance includes rebuilding generated project code into equivalent musical data/graph, processor parity, order-sensitive effect chains, modulation automation without graph rebuild, split/move timing across tempo changes, documented cut/retrigger/tail behavior, independent instances, source provenance, cancellation, stale rejection and failed-build playback continuity. The browser prototype's passing MIDI/model tests do not satisfy these native-engine gates.

### Live MIDI tuning policy

Live monitoring and new MIDI takes resolve the captured project tuning on the
control/preparation side through Flow's pitch conversion. Audio callbacks consume
an immutable 128-key frequency map. Scala/KBM unmapped keys are silent and omitted
from recorded takes. MIDI carries no enharmonic spelling: chromatic input keys
use sharp spellings consistently for monitoring and recorded notes, which matters
for just intonation and Pythagorean tuning. Flow-authored notes retain their explicit
spellings and source tuning overrides.

A take retains its resolved frequencies and the existing captured-project commit
check; subsequent tuning edits do not retune historical notes. Monitoring tuning
changes require preparation/publication of a new monitor, with the existing
voice-reset semantics. MIDI file import/export remains the documented 12-TET note
adapter; custom pitch bends/MPE tuning export is not implemented.

### Clip gain and fade policy

Audio clip gain/fades are source-frame-anchored envelopes, interpreted by the same
playback kernel for project playback and Flow-authored clips (`dawClipEnvelope`).
Linear fade-in starts at zero; linear fade-out reaches zero on the last frame of
its captured window. Overlapping fades multiply. Null envelope means unity gain
and no fades. Source assets remain immutable.

Move, nudge, align, duplicate, repeat, split and trim preserve the captured envelope.
Splitting does not insert new edge fades. Trimming reveals/cuts the existing
source-anchored envelope rather than stretching it; extensions beyond an active
fade's edge hold that edge's clamped gain. Setting gain/fades again anchors a new
envelope to the current visible window. Frame counts serialize as exact integers
and use string arguments in Flow to avoid floating-point identity loss.

Offline audio processing receives the audible window with envelope applied. Its
accepted replacement uses the processed PCM with no additional envelope, preventing
double processing; undo restores the original clip and envelope. Project schema 16
and arrangement schema 2 store the envelope. Older files load with unity/no fades.

### Metronome and counted recording

Metronome and count-in are host monitoring options, not project musical data or
undoable edits. They default off for a new session and are excluded from offline
mix/stem export. The click instrument uses ordinary Flow graph primitives and can
be reconstructed from its exported Flow graph. Bar accents use 1500 Hz, other beats
1000 Hz, with a short decaying envelope. Beats use the active meter denominator;
6/8 therefore clicks six eighth notes rather than inferring compound grouping.

Continuous metronome follows captured tempo/meter maps, seek and loop resets, and
continues during recording beyond finite/empty projects. It is silent while paused
or stopped. It is mixed after the project output graph so track mute/solo and master
effects do not change the reference click. The current toggle publishes a newly
prepared playback instance and must be set before arming or after completing a take.

Count-in uses one to eight bars at the recording cursor's tempo/meter while project
time stays fixed. MIDI before its audio-acknowledged boundary is excluded. Late
control polling retains valid post-boundary input; stopping before the boundary
cancels. The published timestamps are render/receipt timestamps, not calibrated DAC
or hardware MIDI timestamps; device latency qualification remains separate.
