# Phase 3: Synthesis & MIDI Export - Context

**Gathered:** 2026-04-02
**Status:** Ready for planning

<domain>
## Phase Boundary

Add custom oscillator definitions (user-defined waveforms via wavetable procs) and MIDI file export to the Flow language. Custom oscillators integrate into the existing synthesizer pipeline (`INoteSynthesizer`, `SynthesizerFactory`). MIDI export uses DryWetMidi library to write standard MIDI files from Song structures.

</domain>

<decisions>
## Implementation Decisions

### Custom Oscillator API
- **D-01:** Users define a custom oscillator by writing a `proc` that takes an array size and returns a `Float[]` array representing one cycle of the waveform. Example: `proc myWave(Int size) { ... return samples }`.
- **D-02:** Register via `oscillator("name", waveProc)` built-in that creates a wavetable from the proc's output and registers it as a named synthesizer in `SynthesizerFactory`.
- **D-03:** Once registered, the custom oscillator is usable anywhere a built-in instrument name works: `renderSong(song, "myWave")`, `instrument "myWave" { ... }`.
- **D-04:** Default wavetable size: 2048 samples. Configurable via optional third argument: `oscillator("name", waveProc, 4096)`.
- **D-05:** Wavetable playback uses linear interpolation for frequency scaling — standard approach, avoids aliasing at low cost.
- **D-06:** The wavetable is computed ONCE at registration time (proc evaluated once, result cached). No per-sample interpreter overhead during playback.

### Custom Oscillator Integration
- **D-07:** Custom oscillators implement `INoteSynthesizer` via a new `WavetableSynthesizer` class that wraps the cached wavetable.
- **D-08:** `WavetableSynthesizer` uses the same ADSR envelope as `SineSynthesizer` (attack=5ms, decay=50ms, sustain=0.7, release=50ms) by default. Users can override with existing `adsr` function on the output buffer.
- **D-09:** Custom oscillators work with existing voice allocation, effects pipeline, and panning — no special-casing required. They produce standard `AudioBuffer` output via `INoteSynthesizer.RenderNote`.

### MIDI Export
- **D-10:** API: `writeMidi(String path, Song song)` — takes a file path and a Song value, writes a Standard MIDI File (.mid).
- **D-11:** Uses Melanchall.DryWetMidi library (v8.0.3+) for correct SMF encoding — handles variable-length encoding, delta times, track chunks, tempo maps.
- **D-12:** Exports full song structure: one MIDI track per section/instrument combination. Tempo, time signature, and key signature from MusicalContext are written as meta events.
- **D-13:** Per-note velocities from Flow (`note.Velocity`) map to MIDI velocity (0-127 range, scaled from Flow's 0.0-1.0).
- **D-14:** Note durations from Flow (beats) convert to MIDI ticks using standard resolution (480 ticks per quarter note).

### MIDI Instrument Mapping
- **D-15:** Built-in mapping table for Flow instrument names to General MIDI program numbers: piano→0, brass→56 (trumpet), sax→65 (alto sax), flute→73, drums→channel 10.
- **D-16:** Default instrument (when no instrument specified in section): piano (program 0).
- **D-17:** Custom oscillators have no MIDI equivalent — `writeMidi` uses the default piano program for sections using custom oscillators. This is expected; MIDI can't represent arbitrary waveforms.

### Functional Style (carrying forward from Phase 2)
- **D-18:** `writeMidi` is a side-effect function (writes to disk), similar to `writeWav`. Returns Void.
- **D-19:** `oscillator` is a registration function (side effect on the synthesizer registry). Returns Void.
- **D-20:** `WavetableSynthesizer.RenderNote` produces new AudioBuffers (pure), consistent with all other synthesizers.

### Claude's Discretion
- Internal wavetable interpolation algorithm details (linear vs cubic — linear is fine for v1)
- DryWetMidi API usage patterns (low-level events vs high-level note/pattern API)
- Whether to add a `getMidiProgram(String) -> Int` utility function for users to query the mapping
- ADSR envelope defaults for custom oscillators (can adjust based on what sounds good)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Synthesizer Pipeline
- `flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs` — `INoteSynthesizer` interface, `SynthesizerFactory.Create()`, existing basic synth implementations (sine, saw, square, triangle)
- `flow-lang/StandardLibrary/Audio/Synthesizers/PianoSynthesizer.cs` — Reference for complex synth implementation: partials, ADSR, filtering
- `flow-lang/StandardLibrary/Audio/SynthUtils.cs` — Shared synthesis utilities (GenerateSine, GenerateADSR, CreateSilence, BeatsToSeconds)
- `flow-lang/StandardLibrary/Audio/OscillatorState.cs` — Existing `OscillatorState` with phase tracking — may be useful for wavetable playback
- `flow-lang/StandardLibrary/Audio/SignalGeneration.cs` — Low-level signal generation built-ins

### Audio Pipeline
- `flow-lang/StandardLibrary/Audio/SongRenderer.cs` — Song rendering pipeline; integration point for custom oscillators via `SynthesizerFactory`
- `flow-lang/StandardLibrary/Audio/BarRenderer.cs` — Renders bars to voices using `INoteSynthesizer`
- `flow-lang/StandardLibrary/Audio/Voice.cs` — Voice type with Pan, Gain, OffsetBeats

### MIDI (existing import infrastructure)
- `flow-midi/Midi/MidiParser.cs` — Existing MIDI import parser; reference for MIDI data structures
- `flow-midi/Midi/MidiTypes.cs` — Existing MIDI type definitions; may be reusable or referential
- `flow-midi/Conversion/FlowGenerator.cs` — Converts MIDI to Flow code; inverse operation to writeMidi

### Registration & Types
- `flow-lang/StandardLibrary/BuiltInFunctions.cs` — Registration point for `oscillator` and `writeMidi`
- `flow-lang/StandardLibrary/Audio/FileIO.cs` — Existing `writeWav` pattern; `writeMidi` follows same style
- `flow-lang/Runtime/Value.cs` — Value wrapper; `Value.Void()` for side-effect functions
- `flow-lang/TypeSystem/PrimitiveTypes/OscillatorStateType.cs` — Existing oscillator type
- `flow-lang/TypeSystem/SpecialTypes/SongType.cs` — Song type for writeMidi signature

### Project Dependencies
- `flow-lang/flow-lang.csproj` — Add DryWetMidi NuGet reference here

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `INoteSynthesizer` interface: custom oscillators implement this, get all pipeline integration for free
- `SynthesizerFactory.Create()`: string-based synth lookup; extend with runtime-registered custom oscillators
- `SynthUtils`: ADSR generation, sine generation, silence creation — reuse for wavetable playback
- `OscillatorState`: phase tracking for seamless waveform generation — useful for wavetable phase advancement
- `PitchConversion.NoteToFrequency()`: already converts Flow notes to Hz — used by wavetable playback
- `flow-midi/Midi/MidiTypes.cs`: MIDI data structures already defined for import; may inform export structure
- `FileIO.cs`: `writeWav` pattern for file-writing built-in registration and error handling

### Established Patterns
- Synthesizers are classes implementing `INoteSynthesizer.RenderNote(MusicalNoteData, sampleRate, durationBeats, bpm)`
- `SynthesizerFactory` maps string names to synthesizer instances via switch expression
- Built-in functions registered in `BuiltInFunctions.cs` via `FunctionSignature` + lambda
- File-writing functions return `Value.Void()` and throw on I/O errors

### Integration Points
- `SynthesizerFactory.Create()`: Add runtime-registered custom oscillator lookup (before the switch expression)
- `BuiltInFunctions.cs`: Register `oscillator` and `writeMidi`
- `flow-lang.csproj`: Add `Melanchall.DryWetMidi` package reference
- `SongRenderer.cs`: Already calls `SynthesizerFactory.Create(synthType)` — custom oscillators work automatically once factory is extended

</code_context>

<specifics>
## Specific Ideas

- Custom oscillator registration should feel natural: `oscillator("wobble", myWobbleProc)` then immediately usable in songs
- Wavetable approach avoids per-sample interpreter overhead — the proc runs once, the result is cached as a float array
- MIDI export enables users to take Flow compositions into any DAW for further production
- The existing `flow-midi` project already has MIDI import — export is the complementary operation

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 03-synthesis-midi-export*
*Context gathered: 2026-04-02*
