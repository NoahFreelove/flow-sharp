# Phase 8: Audio Production - Context

**Gathered:** 2026-04-04
**Status:** Ready for planning

<domain>
## Phase Boundary

Add `mix()` for layering audio buffers, per-section gain control via `gain` context block, and three new synthesizer presets (strings, organ, bell). All features follow the existing audio pipeline patterns.

</domain>

<decisions>
## Implementation Decisions

### Buffer Mixing
- **D-01:** API: `mix(Buffer, Buffer) -> Buffer` — sums samples from both buffers. Returns a new buffer (pure function).
- **D-02:** If buffers differ in length, output length matches the longer buffer. Shorter buffer is zero-padded.
- **D-03:** If buffers differ in channel count, mono is promoted to stereo (duplicate to both channels) before mixing.
- **D-04:** Composable via `->`: `track1 -> (mix track2)`. Also works as `(mix track1 track2)`.
- **D-05:** No automatic gain normalization on mix — user controls gain manually. This avoids surprising volume changes.

### Per-Section Gain
- **D-06:** `gain` as a musical context block: `gain 0.5 { ... }` sets gain multiplier for all voices rendered within.
- **D-07:** Follows the same pattern as `tempo`, `key`, `pan` context blocks — push/pop on the musical context stack.
- **D-08:** Gain value is a multiplier: 0.0 = silence, 1.0 = unity (default), 2.0 = double volume. Clamped to [0.0, 2.0].
- **D-09:** Gain multiplies with Voice.Gain during rendering (in SongRenderer/BarRenderer). Stacks with existing gain logic.
- **D-10:** Add `Gain` property to `MusicalContext` (nullable double, like Pan). Add to `GetMusicalContext()` stack walk.

### Synthesizer Presets
- **D-11:** Three new classes in `StandardLibrary/Audio/Synthesizers/`: `StringsSynthesizer.cs`, `OrganSynthesizer.cs`, `BellSynthesizer.cs`.
- **D-12:** All implement `INoteSynthesizer.RenderNote(MusicalNoteData, sampleRate, durationBeats, bpm)`.
- **D-13:** Strings: Two detuned sawtooth waves (~3-5 cent detune) with slow attack envelope (A=100ms, D=200ms, S=0.7, R=300ms). Warm, pad-like sound.
- **D-14:** Organ: Hammond-style additive synthesis. Drawbar harmonics at 1x, 2x, 3x, 4x, 6x, 8x fundamentals with preset amplitudes. Near-instant attack, no sustain decay. Classic organ tone.
- **D-15:** Bell: Risset-style inharmonic partials. Fundamental + partials at non-integer ratios (1.0, 2.2, 3.6, 4.1, 5.8). Exponential amplitude decay per partial. Metallic, bell-like timbre.
- **D-16:** Register in `SynthesizerFactory.Create()`: "strings" => StringsSynthesizer, "organ" => OrganSynthesizer, "bell" => BellSynthesizer.

### Claude's Discretion
- Exact detune amount for strings (3-5 cents)
- Hammond drawbar amplitude ratios
- Risset partial frequency ratios and decay rates
- Whether to add a `mix` varargs overload for 3+ buffers
- Whether gain context block needs a new TokenType or reuses existing gain function token

</decisions>

<canonical_refs>
## Canonical References

### Audio Pipeline
- `flow-lang/StandardLibrary/Audio/AudioCore.cs` — Existing buffer operations. `mix` may go here or in a new file.
- `flow-lang/StandardLibrary/Audio/SongRenderer.cs` — Gain integration in `MixVoicesToStereoBuffer`. Voice.Gain already applied here.
- `flow-lang/StandardLibrary/Audio/BarRenderer.cs` — Voice creation; gain context threading.
- `flow-lang/StandardLibrary/Audio/Voice.cs` — Voice.Gain property.

### Musical Context
- `flow-lang/Runtime/MusicalContext.cs` — Add `Gain` property alongside Pan.
- `flow-lang/Runtime/ExecutionContext.cs` — `GetMusicalContext()` must include Gain in stack walk.
- `flow-lang/Interpreter/Interpreter.cs` — `ExecuteMusicalContext` handler for gain blocks.

### Synthesizers
- `flow-lang/StandardLibrary/Audio/Synthesizers/PianoSynthesizer.cs` — Reference for complex synth implementation.
- `flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs` — `INoteSynthesizer` interface, `SynthesizerFactory.Create()`.
- `flow-lang/StandardLibrary/Audio/SynthUtils.cs` — Shared utilities (GenerateSine, GenerateADSR, etc.).

### Lexer/Parser (for gain context)
- `flow-lang/Lexing/SimpleLexer.cs` — Add `gain` keyword if needed (or reuse existing `gain` DSP function token).
- `flow-lang/Parsing/Parser.cs` — Add gain context block dispatch.
- `flow-lang/Ast/Statements/MusicalContextStatement.cs` — Add `Gain` to `MusicalContextType` enum.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `SynthUtils.GenerateSine/GenerateADSR` — Used by all synths. Strings/organ/bell reuse these.
- `PitchConversion.NoteToFrequency` — Converts note data to Hz for synths.
- `AudioBuffer` — Stereo/mono buffer with GetSample/SetSample.
- Pan context block implementation — exact pattern for gain context.

### Integration Points
- `SynthesizerFactory.Create()` — Add three new instrument names
- `MusicalContext.cs` — Add Gain property
- `ExecutionContext.GetMusicalContext()` — Add Gain to stack walk
- `BuiltInFunctions.cs` — Register `mix` function

</code_context>

<specifics>
## Specific Ideas

- `mix` should be the simplest possible operation — just sum samples
- Gain context enables dynamic compositions: quiet intro, loud chorus, soft bridge
- Strings preset should sound warm and pad-like — great for chord progressions
- Organ should sound classic Hammond — think jazz/rock organ
- Bell should sound metallic and ethereal — good for ambient textures

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 08-audio-production*
*Context gathered: 2026-04-04*
