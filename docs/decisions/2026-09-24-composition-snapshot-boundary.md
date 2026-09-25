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
