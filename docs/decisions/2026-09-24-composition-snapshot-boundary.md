# Evaluated composition snapshot boundary

Status: initial Phase 5 boundary, 2026-09-24. Rendering/export migration is open.

`flow-music-model` builds `Flow.Music.Model` with only BCL references. It owns
immutable composition, section, sequence, bar and note data, with no AST, runtime
Value, evaluator, audio buffer or device types. Collection inputs are copied.
Placements share immutable sections and retain repeat counts without expanding
all events. A native host may construct these objects directly.

`FlowLang.Music.CompositionCompiler` is the compatibility bridge from evaluated
`SongData`. The caller must own the mutable input throughout conversion. Conversion
copies data and checks cancellation; it never evaluates section bodies or retains
runtime state. Legacy section definitions, rendering and export remain supported
and have not yet been rerouted through the snapshot.

## Timing, identity and pitch

Offsets and authored durations use quarter-note units; BPM means quarters per
minute regardless of meter. `Timing` converts quarters and seconds. The composition
timeline applies each section's tempo and lazily expands placements/repeats.
Tuplet fractions, onset offsets (including negative ones), rests, parallel voice
identities, meter and pickup spans survive conversion. Articulation, ties, overlap,
portamento and sustain are retained as data, not applied to authored duration.
Score duration therefore does **not** include audio tails or sample-frame rounding.

Compiler-generated GUIDs are deterministic for source identity and structural
path. They are not persistent edit identities: inserting/reordering notes can
change them. Authoring hosts should supply persistent IDs. Origins retain source
identity, line, column and note length where available.

Pitches retain spelling, MIDI key, cents and resolved frequency, including the
legacy tuning calculation. A reusable tuning description for future transforms,
whole-song tempo maps, explicit render options and instrument routing remain open.

## Linear buffer assembly

All four legacy song render paths now use `RenderedBufferSequence`. A rendered
section buffer is retained once with its repeat count. Building allocates the final
buffer once and copies each output sample once; cancellation is checked between
chunks of at most 65,536 samples. The final buffer owns its samples; callers must
not mutate retained input buffers during assembly. Unrepresentable contiguous
sizes are rejected. This removes quadratic prefix copying, but still retains
section buffers and the final contiguous allocation. Streaming output and explicit
render jobs/progress are subsequent work.

## MIDI audit and next adapter

The standalone `flow-midi/Midi/MidiParser.cs` reads SMF tick events and rejects
SMPTE time division. Its quantizer intentionally produces notation: it chooses a
primary meter by tick duration, warns about multiple tempo events, and the source
generator emits the first tempo rounded to integer BPM. This is not a lossless
composition import contract. Retain this parser and CLI behavior for now; a model
adapter must define tempo-map and quantization policy explicitly.

`flow-lang/StandardLibrary/Audio/MidiExport.cs` uses DryWetMidi, a 480 TPQN base
with tuplet-driven scaling capped at 9,600, and per-section tempo events. Serial
and parallel event branches must be characterized separately before migration,
particularly onset, overlap and portamento handling. Tick truncation is existing
behavior; do not silently change it while extracting shared timing/transforms.

The next slice should introduce explicit render context/options and a native-host
snapshot render/export proof, preserving existing byte/PCM baselines. Then route
Flow rendering through the same model, consolidate note transforms and add bounded
output/progress. The full Phase 5 gate remains open.

## Native rendering slice

`flow-audio` builds `Flow.Audio` with only model/BCL references. Its
`SineCompositionRenderer` consumes snapshots directly and has no current-session
lookup, asset cache, runtime value or device API. Options specify sample rate,
block size and a section note-count budget; cancellation and progress are supplied
per call. Synchronous sink callbacks receive borrowed stereo float blocks and
must copy anything they retain. Sink errors/cancellation propagate and previously
delivered output is not rolled back. Progress counts frames accepted by the sink.
Callbacks must cooperate; hard termination remains the host's responsibility.

The renderer prepares note metadata one section at a time and sweeps active voice
intervals in bounded output blocks. Repeats reuse metadata/block storage and reset
sine sample positions. It preserves legacy sine sample math, note versus onset
rounding, source-order additive mixing, pan/gain, per-sequence pool stealing and
5 ms fades. Sections clip to notated length, including all-rest sections and
per-section tempo/frame truncation. `NoteDuration` centralizes articulation,
tied-rest extension, overlap and pedal tails; legacy `BarRenderer` uses it too.

This is deliberately a dry sine implementation: nonzero reverb is rejected.
Resolved tuning frequency is used directly. Portamento is retained in snapshots
but, as in the legacy sine synth, has no audible effect here. Sample instruments,
Flow lambdas, SFZ, per-instrument release and effect routing still use the legacy
path. This does not claim the complete Phase 5 render gate.

`scripts/MusicHost` is a non-Flow host that builds a two-tempo score, renders it
and writes PCM16 WAV blocks. Its small host-owned encoder saturates without
dither and limits output to RIFF's 32-bit size; it does not replace the existing
WAV export byte contract. Full MIDI/notation adapters and general-purpose audio
codec extraction remain open.

## Snapshot MIDI export

`flow-music-io` builds `Flow.Music.IO`, referencing only the model, DryWetMidi
8.0.3 and the BCL. `MidiCompositionExporter` writes a conductor track plus one
track per sequence name (case-insensitive on the raw name, first-occurrence
order) with the shared `InstrumentRouting` program/channel and prefix-stripped
track name. It owns the GM routing and key-signature tables; legacy `writeMidi`,
MusicXML, LilyPond and `midiOut` delegate to them. The file is built and
validated in memory; validation, tuplet-resolution and cancellation failures
write nothing to the caller's stream. MIDI keys outside 0–127, meters that SMF
cannot encode and positions beyond the 28-bit delta-time range are rejected.

Timing policy: note-ons and note-offs round (half away from zero) from absolute
score positions, so a repeated key never ends its successor early; section
starts accumulate rounded section durations, so repeats do not drift.
Resolution is 480 TPQN, raised to LCM(480, 2×d) for exact tuplet denominators
of placed sections, capped at 9,600. Onsets before the song start clamp to 0
while the note keeps its authored end.
Ties do not merge notes, and pitches stay 12-TET MIDI keys (no pitch bend), as
in legacy export. Tempo, meter and key events are emitted where they change.

Flow `writeMidi` now compiles the evaluated song and writes through this exporter
(owner decision, 2026-09-27); the tuning advisory, in-memory Web capture and
no-partial-file behavior are retained. For scores whose ticks are exact
integers the bytes equal the previous implementation's (three corpora pinned by
recorded SHA-256 in `SnapshotMidiExportTests`). Output changed where the previous
exporter disagreed with the score/audio timeline:

- The previous exporter advanced bars and sections by time-signature capacity, so an overfull
  monophonic bar (for example nine quarters in 4/4) overlaps the next section;
  the next section is now placed where the audio renderer does.
- It applied legato overlap and portamento only to serial notes; voice-block
  notes now receive them as well.
- It truncated each note/rest/bar/offset step separately; positions now round
  from absolute score time. They differ only for non-integer tick positions,
  such as swing/humanize offsets or floating-point residue.
- It emitted only the first section's meter and key, taking the meter from the
  section context; meters now come from the first sequence's bars (these differ
  when a sequence was built under another meter) and later changes are emitted.
  A zero-repeat first section (not producible from Flow syntax) no longer
  supplies tick-0 settings.
- It computed tuplet resolution from unplaced sections and ignored tuplets inside
  voice blocks; resolution now uses placed notes, including voice blocks.
- Onsets before the song start clamp to 0 (previously negative event times
  reached DryWetMidi), and a song
  referencing a section missing from its registry is an error instead of a
  silent skip.

MusicXML keeps the previous tuplet-resolution helper for its `divisions`, and
`midiOut` keeps its own event walk (sharing only the routing table).
