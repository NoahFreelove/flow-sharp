# Phase 2: Audio Pipeline - Research

**Researched:** 2026-03-29
**Domain:** Audio DSP, WAV I/O, stereo panning, sidechain compression, polyphonic voice allocation
**Confidence:** HIGH

## Summary

Phase 2 adds four audio capabilities to Flow: WAV file loading, stereo panning, sidechain compression, and polyphonic voice allocation. All four are well-understood audio engineering problems with established algorithms. The existing codebase already contains most of the infrastructure needed -- WAV writing (inverse for loading), a compressor with envelope follower (reusable for sidechain), a `Voice.Pan` property (currently ignored in the mixer), and a voice creation pipeline through `BarRenderer`/`SequenceRenderer`.

The implementation requires zero new external dependencies. All four features are pure C# additions in `StandardLibrary/Audio/` following the existing pattern: static DSP classes that return new `AudioBuffer` instances, registered as built-in functions via `FunctionSignature` + lambda in `BuiltInFunctions.cs`. The `pan` context block (D-07) is the only feature touching the parser/interpreter -- it can piggyback on the existing `MusicalContextStatement` by adding a `Pan` enum value to `MusicalContextType` and a `Pan` property to `MusicalContext`.

**Primary recommendation:** Implement in dependency order: WAV loading first (standalone, no dependencies), then panning (needed by voice mixing fix), then sidechain (uses buffer operations), then voice allocation (integrates into renderer pipeline).

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** API: `loadWav(String) -> Buffer` -- takes a file path, returns an AudioBuffer. Follows `writeWav` naming convention.
- **D-02:** Read RIFF/WAV headers, parse fmt/data chunks -- inverse of existing `FileIO.ExportWavInternal`. Support 16/24/32-bit PCM.
- **D-03:** Automatic sample rate conversion if loaded WAV doesn't match the default 44100 Hz (linear interpolation is sufficient).
- **D-04:** Return a proper `Buffer` value that works with all existing audio functions (effects, mix, play, writeWav).
- **D-05:** Use constant-power pan law: `left = cos(angle) * sample`, `right = sin(angle) * sample` where angle maps from -1.0..+1.0 to 0..PI/2.
- **D-06:** Expose as both a function `pan(Buffer, Float) -> Buffer` AND integrate into `MixVoicesToStereoBuffer` so `Voice.Pan` property actually works.
- **D-07:** Also support `pan` as a musical context block: `pan -0.5 { ... }` that sets panning for all voices rendered within.
- **D-08:** Fix the existing bug: `Voice.Pan` property exists but is completely ignored in `SongRenderer.MixVoicesToStereoBuffer` -- wire it in.
- **D-09:** API: `sidechain(Buffer trigger, Buffer source, Float threshold, Float ratio) -> Buffer` -- pure function, returns new buffer.
- **D-10:** Uses the trigger buffer's envelope to control gain reduction on the source buffer. Reuse existing `Compressor.cs` envelope follower logic.
- **D-11:** Attack/release default to 10ms/100ms (same as existing compressor) but allow optional args: `sidechain(trigger, source, threshold, ratio, attackMs, releaseMs)`.
- **D-12:** Default max voices: 32. Configurable via `setMaxVoices(Int)` built-in (mirrors `setMaxIterations` pattern from Phase 1).
- **D-13:** Voice stealing policy: drop-quietest (the voice with lowest current amplitude is released when limit is hit).
- **D-14:** Voice allocator is a new class `VoiceAllocator` in `StandardLibrary/Audio/` that wraps voice pool management.
- **D-15:** Crossfade on steal: 5ms fade-out on the stolen voice to avoid clicks/pops.
- **D-16:** All new audio functions are pure -- they return new buffers, never mutate inputs. This matches Flow's functional-first philosophy.
- **D-17:** All functions composable via `->` operator: `kick -> pan(-1.0) -> sidechain(bass, -12dB, 4.0)`.

### Claude's Discretion
- Internal implementation of WAV header parsing (endianness handling, chunk validation)
- Voice allocator data structure (priority queue vs sorted list)
- Whether `pan` context block uses a new `PanContextStatement` AST node or piggybacks on existing `MusicalContextStatement`

### Deferred Ideas (OUT OF SCOPE)
None -- discussion stayed within phase scope
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| AUDIO-01 | User can load WAV files as audio buffers via `loadWav` function | D-01 through D-04; inverse of existing `FileIO.ExportWavInternal`; WAV format fully understood from write side |
| AUDIO-02 | User can control stereo panning per voice/buffer with `pan` function | D-05 through D-08; constant-power pan law; `Voice.Pan` property already exists but is ignored in mixer |
| AUDIO-03 | User can apply sidechain compression driven by a trigger buffer | D-09 through D-11; `Compressor.cs` envelope follower logic directly reusable |
| AUDIO-04 | User can allocate polyphonic voices with configurable voice limits and stealing | D-12 through D-15; `VoiceAllocator` class integrates into `BarRenderer`/`SequenceRenderer` pipeline |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

- **Runtime**: .NET 9 (net9.0) -- all code must target this
- **Platform**: Linux primary (PulseAudio dependency)
- **Dependencies**: Minimal -- no new packages needed for Phase 2
- **Performance**: Real-time audio playback requires efficient buffer operations; no GC pressure in hot paths
- **Compatibility**: Existing .flow scripts and test suite must continue to work
- **Testing**: No unit test framework -- tests are `.flow` scripts executed directly, verified by console output
- **Functional style**: All new audio functions must be pure (return new buffers, never mutate inputs) and composable via `->` flow operator
- **Registration pattern**: Built-in functions registered via `FunctionSignature` + lambda in `BuiltInFunctions.cs`
- **DSP pattern**: Static classes in `Audio/DSP/` with `Apply` methods returning new `AudioBuffer`

## Standard Stack

### Core (No Changes)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET 9 | net9.0 | Runtime | Already in use |
| C# 13 | Latest | Language | Record types, pattern matching already used |
| System.IO.BinaryReader | Built-in | WAV file reading | Inverse of existing BinaryWriter usage in FileIO.cs |

### No New Dependencies
All four features are pure C# implementations. No NuGet packages needed.

## Architecture Patterns

### Recommended File Structure
```
flow-lang/StandardLibrary/Audio/
  FileIO.cs                    # Add LoadWavInternal (inverse of ExportWavInternal)
  DSP/
    Panner.cs                  # NEW: Constant-power stereo panning
    SidechainCompressor.cs     # NEW: Sidechain compression (reuses Compressor envelope logic)
  VoiceAllocator.cs            # NEW: Polyphonic voice pool management
  EffectsFunctions.cs          # Add pan, sidechain registration
  SongRenderer.cs              # Fix MixVoicesToStereoBuffer to use Voice.Pan

flow-lang/Runtime/
  MusicalContext.cs             # Add Pan property

flow-lang/Ast/Statements/
  MusicalContextStatement.cs   # Add Pan to MusicalContextType enum

flow-lang/Interpreter/
  Interpreter.cs               # Handle MusicalContextType.Pan in ExecuteMusicalContext

flow-lang/StandardLibrary/
  BuiltInFunctions.cs          # Register loadWav, pan, sidechain, setMaxVoices
```

### Pattern 1: DSP Function Pattern (Established)
**What:** Static class with `Apply` method that takes `AudioBuffer` input and returns new `AudioBuffer`.
**When to use:** All signal processing operations.
**Example (from existing `Compressor.cs`):**
```csharp
// Source: flow-lang/StandardLibrary/Audio/DSP/Compressor.cs
public static class Compressor
{
    public static AudioBuffer Apply(AudioBuffer input, float thresholdDb, float ratio,
        float attackMs = 10f, float releaseMs = 100f)
    {
        // ... validate inputs ...
        var result = new AudioBuffer(input.Frames, input.Channels, input.SampleRate);
        // ... process samples ...
        return result;
    }
}
```

### Pattern 2: Built-in Function Registration (Established)
**What:** Create `FunctionSignature`, register with lambda that extracts args and calls static method.
**When to use:** Exposing any C# function to Flow.
**Example (from existing `EffectsFunctions.cs`):**
```csharp
// Source: flow-lang/StandardLibrary/Audio/EffectsFunctions.cs
var delaySig = new FunctionSignature("delay",
    [BufferType.Instance, DoubleType.Instance, DoubleType.Instance, DoubleType.Instance]);
registry.Register("delay", delaySig, DelayEffect);
```

### Pattern 3: Musical Context Extension (Established)
**What:** Add property to `MusicalContext`, enum value to `MusicalContextType`, handler in `Interpreter.ExecuteMusicalContext`.
**When to use:** `pan` context block (D-07).
**Key files:** `MusicalContext.cs` (add `Pan` property), `MusicalContextStatement.cs` (add `Pan` to enum), `Interpreter.cs` (add case), `Parser.cs` (recognize `pan` keyword).

### Pattern 4: Overload Registration (Established)
**What:** Register multiple `FunctionSignature` entries for the same function name with different parameter lists.
**When to use:** `sidechain` simple (4 args) and full (6 args) overloads (D-11), `loadWav` single overload.
**Example (from existing compressor registration):**
```csharp
// Simple overload
var compressSimpleSig = new FunctionSignature("compress",
    [BufferType.Instance, DoubleType.Instance, DoubleType.Instance]);
registry.Register("compress", compressSimpleSig, CompressSimple);

// Full overload
var compressFullSig = new FunctionSignature("compress",
    [BufferType.Instance, DoubleType.Instance, DoubleType.Instance,
     DoubleType.Instance, DoubleType.Instance]);
registry.Register("compress", compressFullSig, CompressFull);
```

### Anti-Patterns to Avoid
- **Mutating input buffers:** All DSP must return NEW `AudioBuffer` instances. Never modify `input.Data` in-place.
- **Ignoring channel count mismatches:** When operating on two buffers (sidechain), handle mono vs stereo gracefully. Check existing `MixBuffers` for the pattern.
- **Forgetting empty buffer guard:** Every DSP function checks `if (buffer.Frames == 0)` and returns empty buffer immediately (see all functions in `EffectsFunctions.cs`).
- **Hard-coding sample rate:** Always use `buffer.SampleRate`, never assume 44100.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| WAV format parsing | Custom binary format | Inverse of existing `FileIO.ExportWavInternal` | The project already has full WAV format knowledge (RIFF, fmt, data chunks). Just reverse the write path. |
| Envelope follower | New envelope algorithm | Extract/reuse from `Compressor.cs` lines 35-40 | Attack/release coefficients already computed correctly: `Math.Exp(-1.0 / (ms * 0.001 * sampleRate))` |
| Constant-power panning | Custom pan law | Standard `cos/sin` formula (D-05) | Two lines of math; well-established in audio engineering |

## Common Pitfalls

### Pitfall 1: WAV Chunk Ordering Assumptions
**What goes wrong:** Assuming `fmt` chunk immediately follows RIFF header and `data` immediately follows `fmt`. Real-world WAV files may have extra chunks (LIST, JUNK, bext, etc.) between them.
**Why it happens:** The write side (FileIO.cs) always writes in RIFF/fmt/data order, so the developer assumes all WAVs look the same.
**How to avoid:** Read chunk IDs and sizes, skip unknown chunks until finding `fmt` and `data`. Use a simple loop: read 4-byte ID + 4-byte size, if not the chunk you want, seek forward by size.
**Warning signs:** `loadWav` works on files exported by Flow but crashes on files from DAWs or the internet.

### Pitfall 2: Mono-to-Stereo Conversion in Panning
**What goes wrong:** Applying `pan(monoBuffer, -0.5)` produces a mono buffer with modified amplitude instead of a stereo buffer with L/R separation.
**Why it happens:** The panner checks `input.Channels` and processes each channel, but doesn't promote mono to stereo.
**How to avoid:** If input is mono, create a 2-channel output buffer. Apply `left = cos(angle) * sample` to channel 0, `sin(angle) * sample` to channel 1. If input is stereo, apply gains to existing L/R channels.
**Warning signs:** Panned mono sounds still play identically in both ears.

### Pitfall 3: Sidechain Buffer Length Mismatch
**What goes wrong:** `sidechain(kick, bass, ...)` where kick is 0.1s and bass is 4s. If the envelope follower stops when the trigger ends, the bass plays uncompressed for the remaining 3.9s.
**Why it happens:** Iterating only up to `Math.Min(trigger.Frames, source.Frames)`.
**How to avoid:** Iterate over the full length of `source`. For frames beyond `trigger.Frames`, the trigger envelope naturally decays via the release coefficient (no special handling needed, just use 0.0 as the trigger sample).
**Warning signs:** Sidechain ducking only works when both buffers are the same length.

### Pitfall 4: Voice Stealing Click Artifacts
**What goes wrong:** When voice limit is hit and a voice is stolen, the audio cuts abruptly producing a click/pop.
**Why it happens:** Instant voice removal without crossfade.
**How to avoid:** Apply a 5ms fade-out (D-15) to the stolen voice's remaining buffer before removing it. At 44100 Hz, 5ms = 220 samples. Apply linear ramp from current amplitude to 0 over those samples.
**Warning signs:** Clicking sounds when playing dense polyphonic passages (8+ simultaneous notes).

### Pitfall 5: Sample Rate Conversion Aliasing
**What goes wrong:** Loading a 48kHz WAV into a 44100Hz engine produces metallic artifacts.
**Why it happens:** Linear interpolation (D-03) doesn't include an anti-aliasing filter when downsampling.
**How to avoid:** For downsampling (source rate > target rate), apply a lowpass filter at `targetRate / 2` before interpolation. For upsampling, linear interpolation alone is acceptable for this use case. Since D-03 specifies "linear interpolation is sufficient," this is acceptable but should be documented as a known quality tradeoff.
**Warning signs:** High-frequency artifacts when loading WAV files recorded at higher sample rates.

### Pitfall 6: Pan Context Not Propagating to Voice Creation
**What goes wrong:** `pan -0.5 { section intro { ... } }` sets the context but voices created inside don't pick up the pan value.
**Why it happens:** `BarRenderer.RenderBarToVoices` creates `Voice` instances with default `Pan = 0.0` and doesn't read from `MusicalContext`.
**How to avoid:** Pass `MusicalContext` (or at minimum the pan value) down through the render pipeline: `RenderSection` -> `RenderSequenceToVoices` -> `RenderBarToVoices` -> `Voice` constructor. Set `voice.Pan` from the active context.
**Warning signs:** `pan -0.5 { ... }` has no audible effect on rendered songs.

## Code Examples

### WAV Loading (Inverse of FileIO.ExportWavInternal)
```csharp
// Based on: flow-lang/StandardLibrary/Audio/FileIO.cs (write side)
public static AudioBuffer LoadWavInternal(string filepath)
{
    using var stream = new FileStream(filepath, FileMode.Open, FileAccess.Read);
    using var reader = new BinaryReader(stream);

    // Read RIFF header
    var riffId = reader.ReadBytes(4); // "RIFF"
    int fileSize = reader.ReadInt32();
    var waveId = reader.ReadBytes(4); // "WAVE"

    // Scan for fmt and data chunks (skip unknown chunks)
    int channels = 0, sampleRate = 0, bitsPerSample = 0;
    float[]? samples = null;

    while (stream.Position < stream.Length)
    {
        string chunkId = new string(reader.ReadChars(4));
        int chunkSize = reader.ReadInt32();

        if (chunkId == "fmt ")
        {
            short format = reader.ReadInt16();     // 1 = PCM
            channels = reader.ReadInt16();
            sampleRate = reader.ReadInt32();
            int byteRate = reader.ReadInt32();
            short blockAlign = reader.ReadInt16();
            bitsPerSample = reader.ReadInt16();
            // Skip any extra fmt bytes
            if (chunkSize > 16)
                reader.ReadBytes(chunkSize - 16);
        }
        else if (chunkId == "data")
        {
            int bytesPerSample = bitsPerSample / 8;
            int totalSamples = chunkSize / bytesPerSample;
            samples = ReadSamples(reader, totalSamples, bitsPerSample);
        }
        else
        {
            // Skip unknown chunks
            reader.ReadBytes(chunkSize);
        }
    }

    int frames = samples!.Length / channels;
    var buffer = new AudioBuffer(frames, channels, sampleRate);
    Array.Copy(samples, buffer.Data, samples.Length);

    // Resample if needed (D-03)
    if (sampleRate != 44100)
        buffer = Resample(buffer, 44100);

    return buffer;
}
```

### Constant-Power Panning (D-05)
```csharp
// Source: Standard audio engineering formula
public static AudioBuffer Apply(AudioBuffer input, float pan)
{
    // Map pan from [-1, 1] to [0, PI/2]
    float angle = (pan + 1f) * 0.25f * MathF.PI;
    float leftGain = MathF.Cos(angle);
    float rightGain = MathF.Sin(angle);

    var result = new AudioBuffer(input.Frames, 2, input.SampleRate);

    for (int frame = 0; frame < input.Frames; frame++)
    {
        float mono = input.Channels == 1
            ? input.GetSample(frame, 0)
            : (input.GetSample(frame, 0) + input.GetSample(frame, 1)) * 0.5f;

        result.SetSample(frame, 0, mono * leftGain);
        result.SetSample(frame, 1, mono * rightGain);
    }
    return result;
}
```

### Sidechain Compression (Reusing Compressor.cs Envelope Logic)
```csharp
// Based on: flow-lang/StandardLibrary/Audio/DSP/Compressor.cs envelope follower
public static AudioBuffer Apply(AudioBuffer source, AudioBuffer trigger,
    float thresholdDb, float ratio, float attackMs = 10f, float releaseMs = 100f)
{
    var result = new AudioBuffer(source.Frames, source.Channels, source.SampleRate);

    // Same coefficients as Compressor.cs lines 35-40
    float attackCoeff = attackMs > 0f
        ? (float)Math.Exp(-1.0 / (attackMs * 0.001 * source.SampleRate)) : 0f;
    float releaseCoeff = releaseMs > 0f
        ? (float)Math.Exp(-1.0 / (releaseMs * 0.001 * source.SampleRate)) : 0f;

    float envelopeDb = -96f;

    for (int frame = 0; frame < source.Frames; frame++)
    {
        // Peak detection on TRIGGER buffer (not source)
        float trigPeak = 0f;
        if (frame < trigger.Frames)
        {
            for (int ch = 0; ch < trigger.Channels; ch++)
            {
                float abs = Math.Abs(trigger.GetSample(frame, ch));
                if (abs > trigPeak) trigPeak = abs;
            }
        }

        // Same gain computation as Compressor.cs lines 56-83
        float inputDb = trigPeak > 1e-10f ? 20f * (float)Math.Log10(trigPeak) : -96f;
        float gainReductionDb = 0f;
        if (inputDb > thresholdDb)
            gainReductionDb = (inputDb - thresholdDb) * (1f - 1f / ratio);

        float targetDb = -gainReductionDb;
        envelopeDb = targetDb < envelopeDb
            ? attackCoeff * envelopeDb + (1f - attackCoeff) * targetDb
            : releaseCoeff * envelopeDb + (1f - releaseCoeff) * targetDb;

        float gainLinear = (float)Math.Pow(10.0, envelopeDb / 20.0);

        // Apply gain to SOURCE buffer
        for (int ch = 0; ch < source.Channels; ch++)
        {
            result.SetSample(frame, ch, source.GetSample(frame, ch) * gainLinear);
        }
    }
    return result;
}
```

### Voice Mixing with Pan (Fixing D-08 Bug)
```csharp
// Source: flow-lang/StandardLibrary/Audio/SongRenderer.cs MixVoicesToStereoBuffer
// Current code (buggy): applies voice.Gain but ignores voice.Pan
// Fixed version adds constant-power panning:
foreach (var voice in voices)
{
    int voiceStartFrame = (int)(voice.OffsetBeats * secondsPerBeat * sampleRate);
    float panAngle = (float)((voice.Pan + 1.0) * 0.25 * Math.PI);
    float leftGain = MathF.Cos(panAngle) * (float)voice.Gain;
    float rightGain = MathF.Sin(panAngle) * (float)voice.Gain;

    for (int frame = 0; frame < voice.Buffer.Frames; frame++)
    {
        int destFrame = voiceStartFrame + frame;
        if (destFrame < 0 || destFrame >= totalFrames) continue;

        float sample = voice.Buffer.Channels == 1
            ? voice.Buffer.GetSample(frame, 0)
            : voice.Buffer.GetSample(frame, 0); // left channel for stereo source

        result.SetSample(destFrame, 0, result.GetSample(destFrame, 0) + sample * leftGain);
        result.SetSample(destFrame, 1, result.GetSample(destFrame, 1) + sample * rightGain);
    }
}
```

### Voice Allocator Registration (Following setMaxIterations Pattern)
```csharp
// Based on: flow-lang/StandardLibrary/BuiltInFunctions.cs RegisterIterationGuard
// setMaxVoices mirrors setMaxIterations -- stores a config value on a shared context
var setMaxVoicesSig = new FunctionSignature("setMaxVoices", [IntType.Instance]);
registry.Register("setMaxVoices", setMaxVoicesSig, args =>
{
    VoiceAllocator.MaxVoices = args[0].As<int>();
    return Value.Void();
});
```

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | .flow script execution (no unit test framework) |
| Config file | None -- tests are standalone .flow scripts |
| Quick run command | `dotnet run --project flow-interpreter tests/test_audio_pipeline.flow` |
| Full suite command | `for test in tests/test_*.flow; do dotnet run --project flow-interpreter "$test"; done` |

### Phase Requirements to Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| AUDIO-01 | loadWav loads a WAV file and returns usable Buffer | integration | `dotnet run --project flow-interpreter tests/test_wav_loading.flow` | No -- Wave 0 |
| AUDIO-02 | pan() positions buffer in stereo field; Voice.Pan works in songs | integration | `dotnet run --project flow-interpreter tests/test_panning.flow` | No -- Wave 0 |
| AUDIO-03 | sidechain() applies trigger-driven compression | integration | `dotnet run --project flow-interpreter tests/test_sidechain.flow` | No -- Wave 0 |
| AUDIO-04 | Voice allocation limits active voices and steals quietest | integration | `dotnet run --project flow-interpreter tests/test_voice_allocation.flow` | No -- Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet build --nologo --verbosity quiet && dotnet run --project flow-interpreter tests/test_<feature>.flow`
- **Per wave merge:** `for test in tests/test_*.flow; do dotnet run --project flow-interpreter "$test"; done`
- **Phase gate:** Full suite green before verification

### Wave 0 Gaps
- [ ] `tests/test_wav_loading.flow` -- covers AUDIO-01 (needs a test WAV file; generate one inline with `writeWav` then `loadWav` roundtrip)
- [ ] `tests/test_panning.flow` -- covers AUDIO-02 (verify pan function and Voice.Pan in song render)
- [ ] `tests/test_sidechain.flow` -- covers AUDIO-03 (verify sidechain compression output)
- [ ] `tests/test_voice_allocation.flow` -- covers AUDIO-04 (verify voice limiting with dense polyphony)
- [ ] Test WAV file strategy: Generate test buffers in Flow, `writeWav` to temp file, `loadWav` back, verify frame count/channels match. No external test fixtures needed.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Voice.Pan ignored | Voice.Pan wired into mixer | This phase | Stereo separation actually works in song rendering |
| Unlimited voices | Configurable voice limit with steal-quietest | This phase | Prevents buffer overflow/clipping on dense passages |
| Synth-only audio | WAV sample loading | This phase | Users can incorporate real recordings/samples |

## Open Questions

1. **Pan context block: parser changes needed?**
   - What we know: `MusicalContextType` enum in `MusicalContextStatement.cs` already has Timesig, Tempo, Swing, Key, Dynamics, Rit, Accel. Adding `Pan` is straightforward.
   - What's unclear: The parser recognizes keywords like `tempo`, `timesig`, etc. -- does it use a fixed keyword list or token-based detection? Need to check if `pan` needs to be added as a new keyword token in `SimpleLexer.cs`.
   - Recommendation: Add `Pan` to `MusicalContextType` enum, add `Pan` property to `MusicalContext`, add `pan` keyword recognition in the parser's musical context parsing path. This piggybacks on existing infrastructure with minimal new code.

2. **VoiceAllocator scope: static or instance?**
   - What we know: `setMaxIterations` uses `ExecutionContext.MaxIterations` (instance property on context). `VoiceAllocator` could follow the same pattern.
   - What's unclear: Whether the allocator needs to be thread-safe or can assume single-threaded rendering.
   - Recommendation: Use a static `VoiceAllocator.MaxVoices` property (simpler, matches existing static DSP pattern). The rendering pipeline is single-threaded. If needed later, can be moved to `ExecutionContext`.

3. **WAV loading: relative path resolution**
   - What we know: `writeWav` takes a filepath string as-is. `use` statements resolve `@` prefix to stdlib dir, otherwise relative to current file.
   - What's unclear: Should `loadWav("kick.wav")` resolve relative to the .flow script's directory or the CWD?
   - Recommendation: Resolve relative to CWD (matches `writeWav` behavior). Document this in the function.

## Sources

### Primary (HIGH confidence)
- `flow-lang/StandardLibrary/Audio/FileIO.cs` -- WAV write format (RIFF header, fmt chunk, data chunk, 16/24/32-bit PCM)
- `flow-lang/StandardLibrary/Audio/DSP/Compressor.cs` -- Envelope follower with attack/release coefficients
- `flow-lang/StandardLibrary/Audio/SongRenderer.cs` -- `MixVoicesToStereoBuffer` (Voice.Pan bug confirmed: line 104 uses `voice.Gain` but never reads `voice.Pan`)
- `flow-lang/StandardLibrary/Audio/Voice.cs` -- `Pan` property exists (line 26), defaults to 0.0
- `flow-lang/StandardLibrary/Audio/EffectsFunctions.cs` -- Registration pattern for DSP built-ins
- `flow-lang/StandardLibrary/Audio/DSP/Delay.cs` -- Pure DSP function pattern (returns new buffer)
- `flow-lang/StandardLibrary/Audio/AudioCore.cs` -- `AudioBuffer` class (interleaved float32, Frames/Channels/SampleRate)
- `flow-lang/StandardLibrary/Audio/BarRenderer.cs` -- Voice creation pipeline
- `flow-lang/Runtime/MusicalContext.cs` -- Musical context properties (Tempo, Key, Swing, Velocity, TimeSignature)
- `flow-lang/Ast/Statements/MusicalContextStatement.cs` -- `MusicalContextType` enum
- `flow-lang/Interpreter/Interpreter.cs` -- `ExecuteMusicalContext` handler

### Secondary (MEDIUM confidence)
- Constant-power pan law: standard audio engineering formula (cos/sin), referenced in STACK.md

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- no new dependencies, all existing infrastructure
- Architecture: HIGH -- follows established patterns exactly (DSP classes, registration, musical context)
- Pitfalls: HIGH -- based on direct code reading and audio engineering fundamentals

**Research date:** 2026-03-29
**Valid until:** 2026-04-28 (stable codebase, no external dependency concerns)
