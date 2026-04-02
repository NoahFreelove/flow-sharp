# Phase 2: Audio Pipeline - Context

**Gathered:** 2026-04-01
**Status:** Ready for planning

<domain>
## Phase Boundary

Add WAV file loading, stereo panning, sidechain compression, and polyphonic voice allocation to the Flow audio pipeline. All changes are in the audio subsystem (StandardLibrary/Audio/, DSP/). No language/parser changes. All new features follow Flow's functional style — pure functions that return new values, composable via `->` operator.

</domain>

<decisions>
## Implementation Decisions

### WAV Loading
- **D-01:** API: `loadWav(String) -> Buffer` — takes a file path, returns an AudioBuffer. Follows `writeWav` naming convention.
- **D-02:** Read RIFF/WAV headers, parse fmt/data chunks — inverse of existing `FileIO.ExportWavInternal`. Support 16/24/32-bit PCM.
- **D-03:** Automatic sample rate conversion if loaded WAV doesn't match the default 44100 Hz (linear interpolation is sufficient).
- **D-04:** Return a proper `Buffer` value that works with all existing audio functions (effects, mix, play, writeWav).

### Stereo Panning
- **D-05:** Use constant-power pan law: `left = cos(angle) * sample`, `right = sin(angle) * sample` where angle maps from -1.0..+1.0 to 0..PI/2.
- **D-06:** Expose as both a function `pan(Buffer, Float) -> Buffer` AND integrate into `MixVoicesToStereoBuffer` so `Voice.Pan` property actually works.
- **D-07:** Also support `pan` as a musical context block: `pan -0.5 { ... }` that sets panning for all voices rendered within.
- **D-08:** Fix the existing bug: `Voice.Pan` property exists but is completely ignored in `SongRenderer.MixVoicesToStereoBuffer` — wire it in.

### Sidechain Compression
- **D-09:** API: `sidechain(Buffer trigger, Buffer source, Float threshold, Float ratio) -> Buffer` — pure function, returns new buffer.
- **D-10:** Uses the trigger buffer's envelope to control gain reduction on the source buffer. Reuse existing `Compressor.cs` envelope follower logic.
- **D-11:** Attack/release default to 10ms/100ms (same as existing compressor) but allow optional args: `sidechain(trigger, source, threshold, ratio, attackMs, releaseMs)`.

### Polyphonic Voice Allocation
- **D-12:** Default max voices: 32. Configurable via `setMaxVoices(Int)` built-in (mirrors `setMaxIterations` pattern from Phase 1).
- **D-13:** Voice stealing policy: drop-quietest (the voice with lowest current amplitude is released when limit is hit).
- **D-14:** Voice allocator is a new class `VoiceAllocator` in `StandardLibrary/Audio/` that wraps voice pool management.
- **D-15:** Crossfade on steal: 5ms fade-out on the stolen voice to avoid clicks/pops.

### Functional Style (from user feedback)
- **D-16:** All new audio functions are pure — they return new buffers, never mutate inputs. This matches Flow's functional-first philosophy.
- **D-17:** All functions composable via `->` operator: `kick -> pan(-1.0) -> sidechain(bass, -12dB, 4.0)`.

### Claude's Discretion
- Internal implementation of WAV header parsing (endianness handling, chunk validation)
- Voice allocator data structure (priority queue vs sorted list)
- Whether `pan` context block uses a new `PanContextStatement` AST node or piggybacks on existing `MusicalContextStatement`

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Audio Pipeline
- `flow-lang/StandardLibrary/Audio/FileIO.cs` — Existing WAV writer; `loadWav` is the inverse. Study header format and bit depth handling.
- `flow-lang/StandardLibrary/Audio/SongRenderer.cs` — `MixVoicesToStereoBuffer` is where Voice.Pan must be wired in. Study the mixing loop.
- `flow-lang/StandardLibrary/Audio/Voice.cs` — `Voice.Pan` property exists but is unused in mixer.
- `flow-lang/StandardLibrary/Audio/DSP/Compressor.cs` — Existing compressor with envelope follower. Sidechain reuses this logic.
- `flow-lang/StandardLibrary/Audio/BarRenderer.cs` — Creates Voice instances per note; voice allocation integrates here.
- `flow-lang/StandardLibrary/Audio/SequenceRenderer.cs` — Renders sequences to voices; allocation limit applies here.

### Registration
- `flow-lang/StandardLibrary/BuiltInFunctions.cs` — Register `loadWav`, `pan`, `sidechain`, `setMaxVoices`
- `flow-lang/StandardLibrary/Audio/PlaybackFunctions.cs` — Pattern for storing runtime references (`_manager` static)

### Existing Patterns
- `flow-lang/StandardLibrary/Audio/DSP/Delay.cs` — Example of pure DSP function (returns new buffer, never modifies input)
- `flow-lang/StandardLibrary/Audio/EffectsFunctions.cs` — Registration pattern for DSP built-ins

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `FileIO.ExportWavInternal` — full WAV format knowledge (RIFF headers, fmt chunk, data chunk). `loadWav` reverses this.
- `Compressor.Apply` — envelope follower with attack/release smoothing. Sidechain splits this into separate trigger/source paths.
- `Voice.Pan` property — already exists, just needs wiring in `MixVoicesToStereoBuffer`.
- `Timeline.SetVoicePan` / `Timeline.SetTrackPan` — pan setting functions exist in the timeline system.
- `AudioBuffer` — immutable-style buffer with `GetSample`/`SetSample`. All DSP returns new buffers.

### Established Patterns
- DSP functions in `Audio/DSP/` are static, take `AudioBuffer` input, return new `AudioBuffer` (never modify input)
- Built-in functions registered in `BuiltInFunctions.cs` via `FunctionSignature` + lambda
- Effects registered in `EffectsFunctions.cs` with `RegisterEffects` method

### Integration Points
- `BuiltInFunctions.cs`: Register `loadWav`, `pan`, `sidechain`, `setMaxVoices`
- `SongRenderer.MixVoicesToStereoBuffer`: Wire `Voice.Pan` into the mixing loop
- `BarRenderer.RenderBarToVoices`: Apply voice allocation limit

</code_context>

<specifics>
## Specific Ideas

- `loadWav` should work with the flow operator: `Buffer kick = "kick.wav" -> loadWav`
- Panning should feel natural: `melody -> pan(-0.3)` for slightly left
- Sidechain is the classic EDM pumping effect: `bass -> sidechain(kick, -12dB, 4.0)` — bass ducks under kick
- Voice allocation should be transparent — users don't manage voices, the system handles it

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 02-audio-pipeline*
*Context gathered: 2026-04-01*
