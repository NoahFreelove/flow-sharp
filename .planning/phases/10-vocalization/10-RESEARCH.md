# Phase 10: Vocalization - Research

**Researched:** 2026-04-03
**Domain:** Formant-based vocal synthesis, external TTS integration, DSP filtering
**Confidence:** HIGH

## Summary

Phase 10 adds two vocal synthesis capabilities to Flow: (1) a formant-based vowel/consonant synthesizer that uses the existing biquad bandpass filter infrastructure to shape a buzz source into recognizable vowel sounds, and (2) an external TTS hook that shells out to a configurable system command and captures WAV output as a standard AudioBuffer.

The formant synthesis approach is well-suited to Flow's existing architecture. The project already has biquad bandpass filters (`Filter.Bandpass`), oscillator generation (`SynthUtils.GenerateSaw`, `GenerateSquare`), ADSR envelopes, white noise generation, and the one-pole lowpass filter -- all the primitives needed for formant synthesis. The Csound formant frequency tables (widely used in computer music for decades) provide well-documented F1-F5 frequencies, bandwidths, and amplitude values for 5 vowels across 5 voice types.

The TTS hook uses `System.Diagnostics.Process` (built into .NET) to shell out and `FileIO.LoadWavInternal` to parse the captured WAV output -- no new dependencies required for either feature.

**Primary recommendation:** Implement formant synthesis using parallel biquad bandpass filters (reusing existing `Filter` class coefficients) applied to a pulse/saw buzz source, with vowel formant data from the Csound/CCRMA reference tables. Implement TTS hook as a thin Process wrapper that captures stdout WAV data and parses it with the existing WAV loader.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** Primary engine: formant synthesis -- zero dependencies, hand-rolled in C#. Buzz source (pulse wave at pitch) filtered through formant bandpass filters to produce vowel sounds.
- **D-02:** Each vowel is defined by a set of formant frequencies (F1, F2, F3). Standard acoustic phonetics values.
- **D-03:** Consonants use noise bursts (s), clicks (t), and nasal resonance (n) -- simple approximations.
- **D-04:** Secondary engine: external TTS hook -- `tts(String text) -> Buffer` shells out to a configurable command (e.g., espeak-ng, piper-tts) and captures the WAV output. Unknown audio length is fine.
- **D-05:** `sing(String phoneme, Note pitch, Double duration) -> Buffer` -- formant synthesis of a single vowel/syllable at a given pitch and duration in seconds.
- **D-06:** `tts(String text) -> Buffer` -- external TTS hook. Calls a configurable system command, captures WAV output, returns as Buffer.
- **D-07:** Both functions return standard `AudioBuffer` -- composable via `->` and mixable with `mix()`. No special vocal type needed.
- **D-08:** TTS command configurable via `setTtsCommand(String)` built-in. Default: `"espeak-ng --stdout"`. User can set to any command that writes WAV to stdout.
- **D-09:** 5 vowel phonemes: "ah" (a), "ee" (i), "oh" (o), "oo" (u), "eh" (e). Each with standard formant frequency table.
- **D-10:** 3 consonant approximations (stretch goal): "s" (noise burst), "t" (click transient), "n" (nasal resonance). Allow simple syllables: "na", "ta", "sa".
- **D-11:** Syllable parsing: if phoneme string is 2+ chars and starts with a consonant, split into consonant onset + vowel nucleus. E.g., "na" = "n" onset + "ah" vowel.
- **D-12:** External TTS hook as separate function, not integrated with formant engine.
- **D-13:** Vocals produce regular `AudioBuffer`. Users combine with instruments using existing `mix()` function. No special song-level vocal integration.
- **D-14:** Implementation lives in `flow-lang/StandardLibrary/Audio/Vocalization/` -- new subdirectory.
- **D-15:** Register `sing` and `tts` in `BuiltInFunctions.cs`. Add `internal proc` declarations in `audio.flow`.

### Claude's Discretion
- Exact formant frequency tables (standard acoustic phonetics references)
- Buzz source waveform details (pulse width, spectral tilt)
- Bandpass filter implementation (reuse existing DSP filters or new dedicated formant filter)
- Consonant timing (onset duration in ms)
- TTS command error handling (what to return if command fails)
- Whether to add a `singSequence(String[] phonemes, Sequence notes) -> Buffer` convenience function

### Deferred Ideas (OUT OF SCOPE)
- Full phoneme set (all English IPA phonemes)
- Note stream integration (`| "ah"C4q "ee"E4q |`) -- requires parser changes
- Vocal section type in Song expressions
- Vibrato/portamento on vocals
- Multi-voice choir synthesis
</user_constraints>

## Standard Stack

### Core (No New Dependencies)
| Component | Location | Purpose | Why Standard |
|-----------|----------|---------|--------------|
| Biquad bandpass filter | `Audio/DSP/Filter.cs` | Formant resonance filtering | Already implemented, correct biquad coefficients |
| SynthUtils oscillators | `Audio/Synthesizers/SynthUtils.cs` | Buzz source (saw wave) and noise generation | GenerateSaw, GenerateWhiteNoise, GenerateADSR already exist |
| AudioBuffer | `Audio/AudioCore.cs` | Output format for vocal audio | Standard project audio container |
| FileIO WAV loader | `Audio/FileIO.cs` | Parse WAV from TTS output | LoadWavInternal handles 16/24/32-bit PCM |
| System.Diagnostics.Process | .NET BCL | Shell out to TTS command | Built into .NET 9, no package needed |
| PitchConversion | `Audio/PitchConversion.cs` | Convert Note to Hz for buzz source pitch | Already handles all note names and octaves |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Parallel biquad bandpass | Single formant-specific resonator class | Biquad reuse is simpler and already tested; dedicated resonator could be more efficient but adds code |
| Saw wave buzz source | Pulse wave with variable width | Saw has richer harmonics by default; pulse width would add complexity for marginal benefit in v1 |
| Process.Start for TTS | CliWrap library | Unnecessary dependency; Process.Start with stdout redirect is ~15 lines of code |

**Installation:**
```bash
# No new packages needed
dotnet build
```

## Architecture Patterns

### Recommended Project Structure
```
flow-lang/StandardLibrary/Audio/Vocalization/
    FormantSynthesizer.cs     # Core formant synthesis engine
    FormantData.cs            # Vowel formant frequency/bandwidth/amplitude tables
    ConsonantSynthesizer.cs   # Noise burst, click, nasal approximations
    TtsHook.cs                # External TTS process wrapper
    VocalizationFunctions.cs  # Registration: sing(), tts(), setTtsCommand()
```

### Pattern 1: Formant Synthesis Pipeline
**What:** Generate a buzz source at the target pitch, then filter it through parallel bandpass filters (one per formant), scale each by its amplitude, and sum the results.
**When to use:** Every `sing()` call.
**Example:**
```csharp
// Source: Csound formant synthesis model + existing Filter.cs biquad
public AudioBuffer SynthesizeVowel(string vowel, double frequencyHz, double durationSeconds, int sampleRate)
{
    int numSamples = (int)(durationSeconds * sampleRate);
    var buzzSamples = new float[numSamples];
    
    // Buzz source: sawtooth at pitch frequency (rich harmonics)
    SynthUtils.GenerateSaw(buzzSamples, frequencyHz, 0.8, sampleRate);
    
    // Apply spectral tilt: -6dB/octave lowpass to simulate glottal source
    SynthUtils.OnePoleLP(buzzSamples, frequencyHz * 4, sampleRate);
    
    var buzzBuffer = SynthUtils.ToMonoBuffer(buzzSamples, sampleRate);
    
    // Get formant data for this vowel
    var formants = FormantData.GetFormants(vowel); // returns F1-F5 with BW and amplitude
    
    // Filter through parallel bandpass filters, scale by amplitude, sum
    var result = new AudioBuffer(numSamples, 1, sampleRate);
    foreach (var formant in formants)
    {
        float lowHz = formant.Frequency - formant.Bandwidth / 2f;
        float highHz = formant.Frequency + formant.Bandwidth / 2f;
        
        // Clamp to valid range
        lowHz = Math.Max(lowHz, 20f);
        highHz = Math.Min(highHz, sampleRate / 2f - 1f);
        if (highHz <= lowHz) continue;
        
        var filtered = Filter.Bandpass(buzzBuffer, lowHz, highHz);
        float gain = (float)Math.Pow(10.0, formant.AmplitudeDb / 20.0); // dB to linear
        
        for (int i = 0; i < result.Data.Length && i < filtered.Data.Length; i++)
            result.Data[i] += filtered.Data[i] * gain;
    }
    
    // Apply ADSR envelope for natural onset/offset
    float[] envelope = SynthUtils.GenerateADSR(
        attack: 0.02, decay: 0.05, sustain: 0.8, release: 0.05,
        frames: numSamples, sampleRate: sampleRate);
    SynthUtils.ApplyEnvelope(result.Data, envelope);
    
    return result;
}
```

### Pattern 2: TTS External Process Hook
**What:** Shell out to a configurable command, pipe text as argument, capture stdout as WAV bytes, parse with existing WAV loader.
**When to use:** Every `tts()` call.
**Example:**
```csharp
// Source: System.Diagnostics.Process + FileIO.LoadWavInternal pattern
public static AudioBuffer RunTts(string text, string ttsCommand)
{
    // Parse command: e.g., "espeak-ng --stdout" -> executable="espeak-ng", baseArgs="--stdout"
    var parts = ttsCommand.Split(' ', 2);
    string executable = parts[0];
    string baseArgs = parts.Length > 1 ? parts[1] : "";
    
    var process = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = $"{baseArgs} \"{text}\"",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        }
    };
    
    process.Start();
    using var memStream = new MemoryStream();
    process.StandardOutput.BaseStream.CopyTo(memStream);
    process.WaitForExit();
    
    if (process.ExitCode != 0)
        throw new InvalidOperationException($"TTS command failed with exit code {process.ExitCode}");
    
    // Parse WAV from memory (reuse existing WAV parsing logic)
    memStream.Position = 0;
    return ParseWavFromStream(memStream);
}
```

### Pattern 3: Consonant Onset + Vowel Nucleus
**What:** For syllables like "na", "ta", "sa", generate a short consonant onset (10-50ms) and crossfade into the vowel body.
**When to use:** When phoneme string is 2+ chars starting with a recognized consonant.
**Example:**
```csharp
// Consonant onset durations (milliseconds)
// "s" = 80ms white noise burst, highpass filtered at 4kHz
// "t" = 10ms click (sharp attack, fast decay noise burst)
// "n" = 40ms nasal resonance (low-frequency buzz with strong F1, attenuated higher formants)
```

### Pattern 4: Registration Pattern (follows existing conventions)
**What:** Static `Register(InternalFunctionRegistry)` method, matching `EffectsFunctions.Register` pattern.
**When to use:** Registering sing/tts/setTtsCommand built-ins.
**Example:**
```csharp
// Source: follows EffectsFunctions.cs pattern exactly
public static class VocalizationFunctions
{
    private static string _ttsCommand = "espeak-ng --stdout";
    
    public static void Register(InternalFunctionRegistry registry)
    {
        // sing(String, Note, Double) -> Buffer
        var singSignature = new FunctionSignature("sing",
            [StringType.Instance, NoteType.Instance, DoubleType.Instance]);
        registry.Register("sing", singSignature, Sing);
        
        // tts(String) -> Buffer
        var ttsSignature = new FunctionSignature("tts", [StringType.Instance]);
        registry.Register("tts", ttsSignature, Tts);
        
        // setTtsCommand(String) -> Void
        var setTtsSig = new FunctionSignature("setTtsCommand", [StringType.Instance]);
        registry.Register("setTtsCommand", setTtsSig, SetTtsCommand);
    }
}
```

### Anti-Patterns to Avoid
- **Creating a new VocalBuffer type:** D-07 and D-13 explicitly require standard AudioBuffer output. No special types.
- **Modifying the parser for vocal syntax:** Note stream integration is deferred. This is pure stdlib work.
- **Per-sample formant filtering:** Use the existing biquad filter on whole buffers, not sample-by-sample custom filters. The biquad is already optimized and tested.
- **Blocking the main thread with TTS:** Process.Start with stdout capture is synchronous but acceptable for v1. The TTS call is user-initiated and expected to take time.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Biquad bandpass filter | Custom resonator | `Filter.Bandpass()` from `DSP/Filter.cs` | Already implemented, correct coefficients, tested |
| WAV parsing from TTS output | New WAV parser | Adapt `FileIO.LoadWavInternal` to accept `Stream` | Existing parser handles 16/24/32-bit PCM with resampling |
| Oscillator generation | Custom buzz source | `SynthUtils.GenerateSaw()` | Already additive, phase-continuous |
| Envelope shaping | Custom amplitude ramp | `SynthUtils.GenerateADSR()` + `ApplyEnvelope()` | Standard ADSR, tested |
| Note-to-Hz conversion | Frequency lookup table | `PitchConversion.NoteToFrequency()` | Handles all notes, alterations, octaves |
| dB-to-linear conversion | Manual math everywhere | Helper method `DbToLinear(double db)` | `Math.Pow(10.0, db / 20.0)` -- but centralize it |

**Key insight:** The entire formant synthesis pipeline can be built from existing SynthUtils + Filter primitives. The only genuinely new code is the formant data tables, the syllable parser, and the TTS process wrapper.

## Common Pitfalls

### Pitfall 1: Bandpass Filter Range Validation
**What goes wrong:** Formant bandwidths can produce lowHz/highHz values outside the valid range (below 0 or above Nyquist). `Filter.Bandpass` throws `ArgumentException` for invalid ranges.
**Why it happens:** Some formant entries have narrow bandwidths at low frequencies (e.g., F1=250Hz with BW=60Hz gives lowHz=220, highHz=280 -- valid, but F5=4950Hz with BW=140Hz gives highHz=5020 which is above Nyquist at 44100/2=22050 -- still valid in this case, but edge cases exist).
**How to avoid:** Clamp lowHz to >= 20Hz and highHz to < Nyquist before calling `Filter.Bandpass`. Skip formants that would produce invalid ranges.
**Warning signs:** `ArgumentException` from Filter.cs during `sing()` calls.

### Pitfall 2: Formant Amplitude Scaling
**What goes wrong:** Formant amplitudes from the Csound table are in dB relative to F1 (F1 is always 0 dB). If you sum all 5 formant-filtered signals without proper amplitude scaling, the output clips.
**Why it happens:** dB values like -6, -7 are close to unity; summing 5 near-unity signals easily exceeds 1.0.
**How to avoid:** Convert dB to linear gain (`Math.Pow(10, db/20)`), then normalize the final output or apply a conservative master gain (e.g., 0.3).
**Warning signs:** Harsh distortion or clipping in output WAV files.

### Pitfall 3: TTS Process Hangs
**What goes wrong:** If the TTS command doesn't exist or hangs, `process.WaitForExit()` blocks forever.
**Why it happens:** User sets a TTS command that isn't installed or produces no output.
**How to avoid:** Set a timeout on `WaitForExit(timeoutMs)` (e.g., 30 seconds). Return a silent buffer or throw a descriptive error on timeout.
**Warning signs:** REPL becomes unresponsive after `tts()` call.

### Pitfall 4: WAV Parsing from Memory Stream
**What goes wrong:** `FileIO.LoadWavInternal` takes a file path, not a stream. TTS output is in memory.
**Why it happens:** The existing WAV loader was designed for file I/O only.
**How to avoid:** Either (a) refactor `LoadWavInternal` to accept a `Stream`/`BinaryReader` (minor change -- extract the core parsing logic), or (b) write TTS output to a temp file and load it (simpler but slower). Option (a) is preferred.
**Warning signs:** Having to write temp files for every TTS call.

### Pitfall 5: Note Type Argument Extraction
**What goes wrong:** The `sing` function takes a `Note` type argument, but extracting pitch information from a `Value` wrapping a Note needs care.
**Why it happens:** Note is a special type in the type system. Need to understand how `Value.As<T>()` works with note data and what the underlying CLR type is.
**How to avoid:** Check how existing synthesizers receive note data. The `INoteSynthesizer.RenderNote` takes `MusicalNoteData`. For `sing()`, the Note value needs to be unwrapped to get frequency. Check if `Value.As<MusicalNoteData>()` works or if a different extraction path is needed.
**Warning signs:** Runtime cast exceptions when calling `sing`.

### Pitfall 6: Consonant-Vowel Crossfade Clicks
**What goes wrong:** Abrupt transition from consonant onset to vowel body produces audible click artifacts.
**Why it happens:** Discontinuity in waveform amplitude at the splice point.
**How to avoid:** Use a short (2-5ms) linear crossfade between consonant tail and vowel onset. Overlap the two signals rather than hard-cutting.
**Warning signs:** "Pop" or "click" sound at the start of syllables like "ta" or "na".

## Code Examples

### Formant Data Table (Csound Reference, Tenor Voice)
```csharp
// Source: https://csound.com/docs/manual/MiscFormants.html (Appendix D)
// Using Tenor voice as default -- good general-purpose male voice range
public static class FormantData
{
    public record FormantEntry(float Frequency, float Bandwidth, float AmplitudeDb);
    
    // Tenor formant data: 5 formants per vowel
    public static readonly Dictionary<string, FormantEntry[]> TenorFormants = new()
    {
        ["ah"] = [ // vowel 'a' as in "father"
            new(650, 80, 0), new(1080, 90, -6), new(2650, 120, -7),
            new(2900, 130, -8), new(3250, 140, -22)
        ],
        ["ee"] = [ // vowel 'i' as in "see"
            new(290, 40, 0), new(1870, 90, -15), new(2800, 100, -18),
            new(3250, 120, -20), new(3540, 120, -30)
        ],
        ["eh"] = [ // vowel 'e' as in "bed"
            new(400, 70, 0), new(1700, 80, -14), new(2600, 100, -12),
            new(3200, 120, -14), new(3580, 120, -20)
        ],
        ["oh"] = [ // vowel 'o' as in "go"
            new(400, 70, 0), new(800, 80, -10), new(2600, 100, -12),
            new(2800, 130, -12), new(3000, 135, -26)
        ],
        ["oo"] = [ // vowel 'u' as in "blue"
            new(350, 40, 0), new(600, 60, -20), new(2700, 100, -17),
            new(2900, 120, -14), new(3300, 120, -26)
        ],
    };
    
    // Use first 3 formants by default (D-02 says F1, F2, F3)
    // F4 and F5 are available for higher quality if desired
}
```

### Registration in BuiltInFunctions.cs
```csharp
// In RegisterAllImplementations, add:
Audio.Vocalization.VocalizationFunctions.Register(registry);
```

### Internal Proc Declarations in audio.flow
```
Note: ===== Vocalization =====

Note: Synthesize a vowel/syllable at a given pitch and duration (seconds)
internal proc sing(String: phoneme, Note: pitch, Double: duration)

Note: Run external TTS engine on text, return audio buffer
internal proc tts(String: text)

Note: Set the external TTS command (default: "espeak-ng --stdout")
internal proc setTtsCommand(String: command)
```

### Consonant Approximations
```csharp
// Source: Standard acoustic phonetics approximations
// "s" = fricative: high-frequency noise burst (4kHz+ white noise, 80ms)
// "t" = plosive: sharp click transient (wideband noise burst, 10ms, fast attack)
// "n" = nasal: buzz at pitch with strong F1 only, attenuated F2+, 40ms

public static float[] GenerateConsonant(string consonant, double pitchHz, int sampleRate)
{
    return consonant switch
    {
        "s" => GenerateFricative(sampleRate, durationMs: 80),
        "t" => GeneratePlosive(sampleRate, durationMs: 10),
        "n" => GenerateNasal(pitchHz, sampleRate, durationMs: 40),
        _ => Array.Empty<float>()
    };
}

private static float[] GenerateFricative(int sampleRate, int durationMs)
{
    int samples = sampleRate * durationMs / 1000;
    var buffer = new float[samples];
    SynthUtils.GenerateWhiteNoise(buffer, 0.3);
    // Highpass at 4kHz to simulate sibilant
    SynthUtils.OnePoleLP(buffer, 8000, sampleRate); // crude approximation
    // Actually need highpass -- invert lowpass or use Filter.Highpass on AudioBuffer
    return buffer;
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| FOF synthesis (IRCAM) | Formant filtering with biquad cascades | Standard since 1990s | Biquad approach is simpler, well-understood, sufficient for Kraftwerk-style synthesis |
| Klatt synthesizer (1980) | Modern formant synths use same principles | Still relevant | Klatt's cascade/parallel formant model is what D-01 describes |
| Concatenative TTS | Neural TTS (Piper, Coqui) | 2020s | External hook (D-04) supports any engine; espeak-ng is concatenative, Piper is neural |

## Open Questions

1. **Note type unwrapping for sing()**
   - What we know: `sing` takes a `Note` type per D-05. Existing synths use `MusicalNoteData`.
   - What's unclear: How to extract pitch from a `Note` Value in the built-in function context (outside of INoteSynthesizer). Need to check how Note values are stored in `Value`.
   - Recommendation: Investigate `Value` wrapper for Note type during implementation. May need a `sing(String, Double, Double)` overload that takes frequency directly as alternative.

2. **FileIO stream-based WAV parsing**
   - What we know: `LoadWavInternal` takes a file path string.
   - What's unclear: Whether to refactor to accept `Stream` or use temp files.
   - Recommendation: Extract core WAV parsing into a `LoadWavFromStream(Stream)` method. Small refactor, high reuse value.

3. **singSequence convenience function**
   - What we know: Claude's discretion area. Would map phoneme arrays to note sequences.
   - What's unclear: Whether this adds enough value for v1 scope.
   - Recommendation: Skip for v1. Users can combine individual `sing()` calls with `mix()` or build their own helper in Flow code. Add if user feedback requests it.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 9 | Everything | Yes | 9.0.115 | -- |
| espeak-ng | Default TTS command | No | -- | `tts()` returns error; user must install or change command via `setTtsCommand()` |
| piper-tts | Alternative TTS | No | -- | Not required; espeak-ng is default |

**Missing dependencies with no fallback:**
- None -- formant synthesis (primary feature) has zero external dependencies.

**Missing dependencies with fallback:**
- espeak-ng: Not installed on this system. The `tts()` function will fail with the default command. This is expected and handled: the function should return a clear error message directing the user to install espeak-ng or configure a different TTS command via `setTtsCommand()`. The formant `sing()` function works independently.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | .flow test scripts (no unit test framework) |
| Config file | None -- tests are .flow scripts executed by the interpreter |
| Quick run command | `dotnet run --project flow-interpreter tests/test_vocalization.flow` |
| Full suite command | `for test in tests/test_*.flow; do dotnet run --project flow-interpreter "$test"; done` |

### Phase Requirements to Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| SC-1 | User can generate a vocal audio buffer from text and pitch information | integration | `dotnet run --project flow-interpreter tests/test_vocalization.flow` | No -- Wave 0 |
| SC-2 | Vocal output can be mixed with instrumental tracks in a Song | integration | `dotnet run --project flow-interpreter tests/test_vocalization.flow` | No -- Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet build && dotnet run --project flow-interpreter tests/test_vocalization.flow`
- **Per wave merge:** Full test suite
- **Phase gate:** Full suite green before verify

### Wave 0 Gaps
- [ ] `tests/test_vocalization.flow` -- test sing() with all 5 vowels, consonant syllables, and mix with instrumental buffer
- [ ] TTS test requires manual verification (espeak-ng not installed on build machine)

## Sources

### Primary (HIGH confidence)
- [Csound Formant Values (Appendix D)](https://csound.com/docs/manual/MiscFormants.html) -- Complete F1-F5 frequency, bandwidth, and amplitude tables for 5 voice types x 5 vowels. Industry-standard reference used in computer music for 30+ years.
- [CCRMA Formant Filtering Example](https://ccrma.stanford.edu/~jos/filters/Formant_Filtering_Example.html) -- F1/F2/F3 with bandwidths for vowel [a]; confirms biquad bandpass approach.
- [Wikipedia: Formant](https://en.wikipedia.org/wiki/Formant) -- Average vowel formant frequencies (Catford 2001).
- Existing codebase: `Filter.cs`, `SynthUtils.cs`, `AudioCore.cs`, `FileIO.cs`, `EffectsFunctions.cs`, `BrassSynthesizer.cs` -- all directly inspected.

### Secondary (MEDIUM confidence)
- [SuperCollider Singing Voice Tutorial](https://composerprogrammer.com/teaching/supercollider/sctutorial/12.2%20Singing%20Voice%20Synthesis.html) -- Confirms soprano formant values match Csound table; references same data source.
- [Sound on Sound: Formant Synthesis](https://www.soundonsound.com/techniques/formant-synthesis) -- Practical overview of formant synthesis approach.

### Tertiary (LOW confidence)
- Consonant onset durations (80ms fricative, 10ms plosive, 40ms nasal) -- based on general acoustic phonetics knowledge. Exact values should be tuned by ear during implementation.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- all components already exist in the codebase; no new dependencies
- Architecture: HIGH -- follows exact patterns of existing synthesizers and effect functions
- Formant data: HIGH -- Csound tables are the de facto standard in computer music
- Consonant approximations: MEDIUM -- simple approximations, may need tuning by ear
- TTS integration: HIGH -- straightforward Process.Start + WAV parsing
- Pitfalls: HIGH -- identified from direct code inspection of Filter.cs validation and Value type system

**Research date:** 2026-04-03
**Valid until:** 2026-05-03 (stable domain -- formant synthesis has been unchanged for decades)
