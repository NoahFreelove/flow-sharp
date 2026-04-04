# Phase 9: Advanced Features - Context

**Gathered:** 2026-04-04
**Status:** Ready for planning

<domain>
## Phase Boundary

Add tempo ramp transform function for gradual BPM transitions, and an interactive tutorial script that teaches Flow from basics to full songs.

</domain>

<decisions>
## Implementation Decisions

### Tempo Ramps
- **D-01:** API: `tempoRamp(Sequence, Double startBPM, Double endBPM) -> Buffer`. Returns Buffer because tempo affects audio timing, not musical structure.
- **D-02:** Discrete-step approximation: divide the sequence into individual beats, render each beat at a linearly interpolated BPM between start and end. Concatenate the per-beat buffers.
- **D-03:** Linear interpolation between startBPM and endBPM. No curve parameter for v1.1 (can add exponential/logarithmic later).
- **D-04:** Uses existing `SequenceRenderer` and `BarRenderer` infrastructure. Each beat segment rendered with its own constant BPM.
- **D-05:** Register as built-in function in `BuiltInFunctions.cs`. Add `internal proc` in `audio.flow`.
- **D-06:** Optional instrument parameter: `tempoRamp(Sequence, startBPM, endBPM, "piano") -> Buffer`. Default to "piano" if not specified.

### Interactive Tutorial
- **D-07:** Single `.flow` script at `examples/tutorial.flow`. Self-contained, runs with `dotnet run --project flow-interpreter examples/tutorial.flow`.
- **D-08:** Structure: progressive sections from basic expressions → variables → functions → note streams → musical context → sections → songs → effects → export.
- **D-09:** Each section prints explanatory text, then demonstrates the concept with working code. User sees output alongside explanations.
- **D-10:** Uses `//` comments (from Phase 7) for code annotations. Uses `$"..."` interpolation for dynamic output.
- **D-11:** Ends with a complete mini-composition that uses multiple features together — a "graduation piece."
- **D-12:** No special runtime mode. Just a regular Flow script that teaches by example.

### Claude's Discretion
- Exact granularity of tempo ramp steps (per-beat vs per-half-beat)
- Tutorial content and pacing
- Whether tempoRamp needs a varargs overload for multiple BPM waypoints
- Tutorial graduation piece composition

</decisions>

<canonical_refs>
## Canonical References

### Tempo Ramps
- `flow-lang/StandardLibrary/Audio/SequenceRenderer.cs` — Renders sequences at a given BPM. Used per-beat in tempo ramp.
- `flow-lang/StandardLibrary/Audio/BarRenderer.cs` — Renders individual bars/notes.
- `flow-lang/StandardLibrary/Audio/SongRenderer.cs` — Pattern for buffer concatenation (`AppendBuffers`).
- `flow-lang/StandardLibrary/BuiltInFunctions.cs` — Registration point.
- `flow-lang/audio.flow` — Internal proc declaration.

### Tutorial
- `examples/showcase.flow` — Existing example script, reference for working Flow patterns.
- `tests/test_*.flow` — Working test scripts showing correct syntax patterns.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `SequenceRenderer.RenderSequenceToVoices` — Already renders at a given BPM
- `SongRenderer.MixVoicesToStereoBuffer` (internal) — Mixes voices to stereo buffer
- `SongRenderer.AppendBuffers` — Concatenates AudioBuffers
- `SynthUtils.BeatsToSeconds` — Beat/time conversion

### Integration Points
- `BuiltInFunctions.cs` — Register `tempoRamp`
- `audio.flow` — Internal proc declaration

</code_context>

<specifics>
## Specific Ideas

- Tempo ramp should feel smooth — each beat at a slightly different BPM creates a natural ritardando/accelerando
- Tutorial should be fun — use musical examples, not dry exercises
- Tutorial graduation piece could be a simple but complete song with melody, chords, and effects

</specifics>

<deferred>
## Deferred Ideas

None

</deferred>

---

*Phase: 09-advanced-features*
*Context gathered: 2026-04-04*
