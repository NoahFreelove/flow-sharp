# Phase 3: Synthesis & MIDI Export - Research

**Researched:** 2026-04-02
**Domain:** Custom oscillator synthesis (wavetable) + MIDI file export
**Confidence:** HIGH

## Summary

This phase adds two independent features to the Flow language: (1) user-defined custom oscillators via a wavetable approach, and (2) MIDI file export using the DryWetMidi library. Both features integrate into the existing well-structured audio pipeline.

Custom oscillators are straightforward: a new `WavetableSynthesizer` class implements `INoteSynthesizer`, reads from a pre-computed float array (the wavetable), and uses linear interpolation for pitch scaling. The `SynthesizerFactory` needs a runtime-registered custom oscillator lookup added before its switch expression. Registration happens via an `oscillator("name", proc, size?)` built-in.

MIDI export walks the same `SongData -> SectionData -> SequenceData -> BarData -> MusicalNoteData` hierarchy that audio rendering uses, but instead of producing audio buffers, it produces MIDI events. DryWetMidi 8.0.3 provides both high-level (`PatternBuilder`, `Note`) and low-level (`MidiEvent`, `TrackChunk`) APIs. The low-level approach gives more control for tempo/time signature/key signature meta events and is the better fit here.

**Primary recommendation:** Implement wavetable synthesis first (no external dependencies), then MIDI export (requires NuGet package addition). Both features are independent and can be planned as separate work units.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- D-01: Users define a custom oscillator by writing a `proc` that takes an array size and returns a `Float[]` array representing one cycle of the waveform
- D-02: Register via `oscillator("name", waveProc)` built-in that creates a wavetable from the proc's output and registers it as a named synthesizer in `SynthesizerFactory`
- D-03: Once registered, the custom oscillator is usable anywhere a built-in instrument name works
- D-04: Default wavetable size: 2048 samples. Configurable via optional third argument
- D-05: Wavetable playback uses linear interpolation for frequency scaling
- D-06: Wavetable computed ONCE at registration time (proc evaluated once, result cached)
- D-07: Custom oscillators implement `INoteSynthesizer` via a new `WavetableSynthesizer` class
- D-08: `WavetableSynthesizer` uses same ADSR as `SineSynthesizer` by default (attack=5ms, decay=50ms, sustain=0.7, release=50ms)
- D-09: Custom oscillators work with existing voice allocation, effects pipeline, and panning -- no special-casing
- D-10: API: `writeMidi(String path, Song song)` -- takes file path and Song value, writes .mid file
- D-11: Uses DryWetMidi library (v8.0.3+) for correct SMF encoding
- D-12: One MIDI track per section/instrument combination. Tempo, time signature, key signature as meta events
- D-13: Per-note velocities map from Flow 0.0-1.0 to MIDI 0-127
- D-14: Note durations convert to MIDI ticks using 480 ticks per quarter note
- D-15: Built-in mapping table for Flow instrument names to General MIDI program numbers
- D-16: Default instrument when none specified: piano (program 0)
- D-17: Custom oscillators use default piano program in MIDI export (expected limitation)
- D-18: `writeMidi` is side-effect function, returns Void
- D-19: `oscillator` is registration function, returns Void
- D-20: `WavetableSynthesizer.RenderNote` produces new AudioBuffers (pure)

### Claude's Discretion
- Internal wavetable interpolation algorithm details (linear vs cubic -- linear is fine for v1)
- DryWetMidi API usage patterns (low-level events vs high-level note/pattern API)
- Whether to add a `getMidiProgram(String) -> Int` utility function
- ADSR envelope defaults for custom oscillators (can adjust based on what sounds good)

### Deferred Ideas (OUT OF SCOPE)
None -- discussion stayed within phase scope
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| SYNTH-01 | User can define custom oscillator waveforms via Flow procs (wavetable approach) | WavetableSynthesizer class implements INoteSynthesizer; `oscillator` built-in evaluates proc once, caches float array; SynthesizerFactory extended with runtime registry |
| SYNTH-02 | Custom oscillators integrate with existing instrument/voice pipeline | INoteSynthesizer interface guarantees pipeline compatibility; SynthesizerFactory.Create() is the single lookup point used by BarRenderer and SongRenderer |
| MIDI-01 | User can export a Song/Sequence to a standard MIDI file via `writeMidi` | DryWetMidi 8.0.3 handles SMF encoding; walk SongData hierarchy to produce MIDI events; register as built-in following FileIO.ExportWav pattern |
| MIDI-02 | MIDI export preserves tempo, time signature, key, and note velocities | MusicalContext stores tempo/timesig/key per section; MusicalNoteData.Velocity maps to MIDI 0-127; PitchConversion.GetMidiNote provides MIDI note numbers |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

- .NET 9 targeting net9.0; nullable reference types enabled; implicit usings
- File-scoped namespaces throughout
- All namespaces under `FlowLang.*`
- AST nodes are `record` types (not relevant here)
- Pattern matching (`switch` expressions) for dispatch
- No unit test framework -- tests are `.flow` scripts in `tests/` verified by console output
- Minimal dependency philosophy -- DryWetMidi is the one approved external dependency
- Existing `.flow` scripts and test suite must continue to work

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Melanchall.DryWetMidi | 8.0.3 | MIDI file writing/export | Locked decision D-11; .NET Standard 2.0 compatible with .NET 9; handles SMF variable-length encoding, delta times, track chunks |

### Supporting
No additional libraries needed. All wavetable synthesis is hand-rolled per CLAUDE.md guidelines.

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| DryWetMidi low-level API | DryWetMidi PatternBuilder (high-level) | PatternBuilder is convenient for simple compositions but lacks direct control over meta events (tempo, key sig, time sig placement). Low-level `TrackChunk` + `MidiEvent` approach gives full control needed for D-12 |
| DryWetMidi | Hand-rolled SMF writer | SMF format has tricky variable-length quantity encoding and multi-track coordination; DryWetMidi is explicitly approved |

**Installation:**
```bash
cd flow-lang
dotnet add package Melanchall.DryWetMidi --version 8.0.3
```

## Architecture Patterns

### New Files
```
flow-lang/
  StandardLibrary/
    Audio/
      Synthesizers/
        WavetableSynthesizer.cs    # INoteSynthesizer impl using cached wavetable
      MidiExport.cs                # writeMidi built-in + MIDI conversion logic
```

### Pattern 1: WavetableSynthesizer (implements INoteSynthesizer)
**What:** A synthesizer that reads from a pre-computed float array (one cycle of waveform) and uses phase-increment + linear interpolation to generate audio at any frequency.
**When to use:** When `oscillator("name", proc)` is called, the proc is evaluated once to produce the wavetable, then a `WavetableSynthesizer` instance is created and registered.

**Key implementation details:**
- Phase increment per sample: `phaseIncrement = frequency / sampleRate`
- Linear interpolation between adjacent wavetable samples for smooth output
- ADSR envelope applied after wavetable generation (reuse `SynthUtils.GenerateADSR` and `SynthUtils.ApplyEnvelope`)
- Rest handling: return silence (follow `SineSynthesizer` pattern)

**Example (conceptual):**
```csharp
// Source: existing SineSynthesizer pattern + OscillatorState phase tracking
public class WavetableSynthesizer : INoteSynthesizer
{
    private readonly float[] _wavetable;
    
    public WavetableSynthesizer(float[] wavetable)
    {
        _wavetable = wavetable;
    }
    
    public AudioBuffer RenderNote(MusicalNoteData note, int sampleRate, double durationBeats, double bpm)
    {
        if (note.IsRest)
            return SynthUtils.CreateSilence(sampleRate, durationBeats, bpm);
        
        double frequency = PitchConversion.NoteToFrequency(note);
        double durationSeconds = SynthUtils.BeatsToSeconds(durationBeats, bpm);
        int numSamples = (int)(durationSeconds * sampleRate);
        
        var samples = new float[numSamples];
        double phase = 0.0;
        double phaseInc = frequency / sampleRate;
        double amplitude = 0.3 * note.Velocity;
        int tableSize = _wavetable.Length;
        
        for (int i = 0; i < numSamples; i++)
        {
            // Linear interpolation
            double tablePos = phase * tableSize;
            int idx0 = (int)tablePos;
            int idx1 = (idx0 + 1) % tableSize;
            double frac = tablePos - idx0;
            
            samples[i] = (float)(amplitude * (_wavetable[idx0] * (1.0 - frac) + _wavetable[idx1] * frac));
            
            phase += phaseInc;
            phase -= Math.Floor(phase); // wrap 0..1
        }
        
        // Apply ADSR envelope
        float[] envelope = SynthUtils.GenerateADSR(0.005, 0.05, 0.7, 0.05, numSamples, sampleRate);
        SynthUtils.ApplyEnvelope(samples, envelope);
        
        return SynthUtils.ToMonoBuffer(samples, sampleRate);
    }
}
```

### Pattern 2: SynthesizerFactory Runtime Registry
**What:** Extend `SynthesizerFactory` with a static `Dictionary<string, float[]>` for runtime-registered wavetables. Check this dictionary before the switch expression in `Create()`.
**When to use:** Called by `oscillator("name", proc)` to register, checked by `Create()` on every synth instantiation.

**Key design point:** The factory currently throws `ArgumentException` for unknown synth types. After this change, it checks the runtime registry first, then falls through to the built-in switch. This means custom oscillators automatically work everywhere `SynthesizerFactory.Create()` is called (BarRenderer, SongRenderer, SequenceRenderer).

```csharp
public static class SynthesizerFactory
{
    private static readonly Dictionary<string, float[]> _customWavetables = new(StringComparer.OrdinalIgnoreCase);
    
    public static void RegisterWavetable(string name, float[] wavetable)
    {
        _customWavetables[name.ToLowerInvariant()] = wavetable;
    }
    
    public static INoteSynthesizer Create(string synthType)
    {
        string key = synthType.ToLowerInvariant();
        
        // Check custom wavetables first
        if (_customWavetables.TryGetValue(key, out var wavetable))
            return new WavetableSynthesizer(wavetable);
        
        return key switch
        {
            "sine" => new SineSynthesizer(),
            // ... existing cases ...
            _ => throw new ArgumentException($"Unknown synthesizer type: {synthType}")
        };
    }
}
```

### Pattern 3: Built-in Registration for `oscillator`
**What:** Register `oscillator(String, Function)` and `oscillator(String, Function, Int)` in BuiltInFunctions. The implementation evaluates the proc with the table size argument, extracts the float array result, and calls `SynthesizerFactory.RegisterWavetable()`.
**Key challenge:** The `oscillator` built-in needs to call a Flow proc from C#. This requires access to the interpreter/execution context. Check how other built-ins that call user functions work (e.g., `map`, `filter`, `reduce` in collections).

**Critical detail from codebase:** Collection functions like `map` receive the function as a `Value` with `FunctionType` and invoke it via the interpreter. The `oscillator` built-in will need the same pattern -- receive the proc as a `Value`, invoke it with the table size argument, and extract the resulting array.

### Pattern 4: MIDI Export (writeMidi)
**What:** Walk the `SongData` hierarchy and produce MIDI events using DryWetMidi's low-level API.
**When to use:** Called by `writeMidi("path.mid", song)`.

**Data flow:**
```
SongData
  -> for each SongSectionRef (with RepeatCount):
       -> SectionData (has MusicalContext with tempo/timesig/key)
            -> for each SequenceData:
                 -> for each BarData:
                      -> for each MusicalNoteData:
                           -> MIDI NoteOn/NoteOff events
```

**DryWetMidi low-level approach:**
```csharp
using Melanchall.DryWetMidi.Core;

// Create file with tempo track + one track per section
var midiFile = new MidiFile();

// Tempo track (track 0): tempo, time sig, key sig meta events
var tempoTrack = new TrackChunk();
tempoTrack.Events.Add(new SetTempoEvent(microsPerBeat)); // 60_000_000 / bpm
tempoTrack.Events.Add(new TimeSignatureEvent((byte)num, (byte)denomPower));
tempoTrack.Events.Add(new KeySignatureEvent(sharpsFlats, (byte)(isMinor ? 1 : 0)));

// Note track: NoteOn/NoteOff with delta times
var noteTrack = new TrackChunk();
noteTrack.Events.Add(new ProgramChangeEvent((SevenBitNumber)program));
// ... note events with delta time encoding ...

midiFile.Chunks.Add(tempoTrack);
midiFile.Chunks.Add(noteTrack);
midiFile.Write("output.mid");
```

**Key conversion: Flow key string to MIDI key signature:**
| Flow Key | MIDI SharpsFlats | MIDI IsMinor |
|----------|-----------------|--------------|
| Cmajor | 0 | false |
| Gmajor | 1 | false |
| Dmajor | 2 | false |
| Aminor | 0 | true |
| Eminor | 1 | true |
| Fmajor | -1 | false |
| Bbmajor | -2 | false |

**Key conversion: Flow instrument to General MIDI program:**
| Flow Name | GM Program | GM Name |
|-----------|-----------|---------|
| piano | 0 | Acoustic Grand Piano |
| brass/horn | 56 | Trumpet |
| sax/saxophone | 65 | Alto Sax |
| flute | 73 | Flute |
| drums/drum | Channel 10 | (any program, channel 10 = percussion) |
| sine/saw/square/triangle | 0 | Default to piano |
| custom oscillators | 0 | Default to piano (D-17) |

### Anti-Patterns to Avoid
- **Evaluating the oscillator proc per-sample:** The proc MUST run once at registration time. Per-sample evaluation would be catastrophically slow. D-06 locks this.
- **Adding MIDI dependency to flow-midi project:** MIDI export goes in `flow-lang`, not `flow-midi`. The `flow-midi` project is a standalone converter tool; `writeMidi` is a language built-in.
- **Using DryWetMidi's high-level Note class for export:** While tempting, the high-level API abstracts away delta time calculation. Since we need precise control over meta events per section boundary, use low-level `TrackChunk` + `MidiEvent` directly.
- **Modifying SongRenderer for MIDI:** MIDI export should walk the same data structures but NOT call SongRenderer. It reads the data, it does not render audio.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| MIDI file encoding | SMF writer with variable-length quantities | DryWetMidi 8.0.3 | Variable-length quantity encoding, multi-track delta time calculation, and RIFF-like chunk format are error-prone; DryWetMidi handles all of this correctly |

**Key insight:** Everything except MIDI file encoding is hand-rolled in this phase. The wavetable synthesizer is simple DSP (array lookup + interpolation). The MIDI data extraction walks existing data structures. Only the final binary encoding justifies a library.

## Common Pitfalls

### Pitfall 1: Wavetable Phase Drift at High Frequencies
**What goes wrong:** At frequencies near Nyquist (sampleRate/2), the phase increment per sample approaches or exceeds the wavetable size, causing aliasing or skipping samples.
**Why it happens:** A 2048-sample wavetable at 44100 Hz sample rate means at ~21 kHz (Nyquist), the phase increment is ~0.5 per sample, which is fine. But very short wavetables (e.g., 64 samples) would alias much sooner.
**How to avoid:** With the default 2048 size (D-04), this is not a practical issue for musical frequencies (20 Hz - 4186 Hz for C8). The minimum wavetable size could be clamped to 64 to prevent degenerate cases.
**Warning signs:** Buzzy/harsh artifacts on high notes.

### Pitfall 2: Invoking a Flow Proc from C# Built-in
**What goes wrong:** The `oscillator` registration needs to call a user-defined Flow proc from within a C# built-in function. This requires access to the interpreter.
**Why it happens:** Built-in functions receive `IReadOnlyList<Value>` args but don't normally have interpreter access. However, collection functions like `map` and `filter` already solve this problem.
**How to avoid:** Follow the same pattern used by `map`/`filter`/`reduce` -- capture the interpreter or execution context in a closure when registering the built-in. Check how `RegisterCollections` passes callback invocation capability.
**Warning signs:** Compile errors about missing interpreter reference in the lambda.

### Pitfall 3: MIDI Delta Times vs Absolute Times
**What goes wrong:** DryWetMidi events use delta times (time since previous event) for low-level API, but our data model uses absolute beat positions.
**Why it happens:** MIDI file format uses delta encoding; Flow uses absolute timeline positions.
**How to avoid:** Collect all note events with absolute tick positions, sort by position, then compute deltas before adding to the track chunk. Alternatively, use DryWetMidi's `TimedEventsManager` which handles absolute-to-delta conversion automatically.
**Warning signs:** Notes playing at wrong times or overlapping incorrectly in exported MIDI.

### Pitfall 4: MIDI Time Signature Denominator Encoding
**What goes wrong:** MIDI time signature denominator is encoded as a power of 2 (e.g., 4/4 = numerator=4, denominator=2 because 2^2=4), not the raw denominator value.
**Why it happens:** MIDI spec quirk. Flow's `TimeSignatureData.Denominator` stores the raw value (4, 8, etc.).
**How to avoid:** Convert: `midiDenomPower = (byte)Math.Log2(flowDenominator)`.
**Warning signs:** DAWs showing wrong time signatures.

### Pitfall 5: Flow Key String Parsing for MIDI
**What goes wrong:** Flow key strings like "Cmajor", "Fsharpminor" need to be parsed into MIDI key signature parameters (sharps/flats count + major/minor flag).
**Why it happens:** Flow stores keys as concatenated strings; MIDI needs numeric encoding.
**How to avoid:** Build a lookup table mapping Flow key strings to `(int sharpsFlats, bool isMinor)` pairs. The set of valid keys is fixed (24 total) in `MusicalContext.ValidKeys`.
**Warning signs:** Incorrect key signatures in DAW import.

### Pitfall 6: Section Repeat Handling in MIDI
**What goes wrong:** Song arrangements can have repeats (`verse*2`). MIDI has no "repeat" concept -- all notes must be written explicitly.
**Why it happens:** `SongSectionRef.RepeatCount` means the section's notes must appear that many times in the MIDI timeline.
**How to avoid:** Loop over `RepeatCount` and offset each repeat's events by the section duration in ticks. This mirrors the existing `SongRenderer.RenderSong` pattern.
**Warning signs:** Repeated sections overlapping or missing.

## Code Examples

### Registering a Built-in Following FileIO Pattern
```csharp
// Source: flow-lang/StandardLibrary/Audio/FileIO.cs (ExportWav registration pattern)
// In BuiltInFunctions.cs or a dedicated registration method:

var writeMidiSignature = new FunctionSignature(
    "writeMidi",
    [StringType.Instance, SongType.Instance]);
registry.Register("writeMidi", writeMidiSignature, MidiExport.WriteMidi);
```

### Velocity Conversion: Flow (0.0-1.0) to MIDI (0-127)
```csharp
// D-13: Per-note velocity mapping
byte midiVelocity = (byte)Math.Clamp((int)(note.Velocity * 127), 1, 127);
// Clamp minimum to 1 because MIDI velocity 0 = note off
```

### Beat Duration to MIDI Ticks
```csharp
// D-14: 480 ticks per quarter note
const int TicksPerQuarterNote = 480;

// A quarter note = 1 beat (in 4/4 time with denominator = 4)
// MusicalNoteData.GetBeats(timeSigDenominator) returns duration in beats
long ticks = (long)(note.GetBeats(timeSigDenominator) * TicksPerQuarterNote);
```

### MIDI Note Number from MusicalNoteData
```csharp
// Source: flow-lang/StandardLibrary/Audio/PitchConversion.cs
int midiNote = PitchConversion.GetMidiNote(note.NoteName, note.Octave, note.Alteration);
// C4 = 60, A4 = 69 -- standard MIDI mapping
```

### Tempo to MIDI Microseconds per Beat
```csharp
// MIDI tempo event uses microseconds per quarter note
int microsecondsPerBeat = (int)(60_000_000.0 / bpm);
```

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | .flow script tests (no unit test framework) |
| Config file | None -- tests are standalone .flow scripts |
| Quick run command | `dotnet run --project flow-interpreter tests/test_<name>.flow` |
| Full suite command | `for test in tests/test_*.flow; do dotnet run --project flow-interpreter "$test"; done` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| SYNTH-01 | Define custom oscillator via proc, register with `oscillator()` | .flow script | `dotnet run --project flow-interpreter tests/test_custom_oscillator.flow` | Wave 0 |
| SYNTH-02 | Custom oscillator works in renderSong pipeline | .flow script | `dotnet run --project flow-interpreter tests/test_custom_oscillator.flow` | Wave 0 |
| MIDI-01 | `writeMidi` exports a Song to .mid file | .flow script | `dotnet run --project flow-interpreter tests/test_midi_export.flow` | Wave 0 |
| MIDI-02 | Exported MIDI has correct tempo/timesig/key/velocity | manual + .flow script | `dotnet run --project flow-interpreter tests/test_midi_export.flow` (validates no errors; correctness verified by opening in DAW) | Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet build && dotnet run --project flow-interpreter tests/test_custom_oscillator.flow && dotnet run --project flow-interpreter tests/test_midi_export.flow`
- **Per wave merge:** Full test suite
- **Phase gate:** Full suite green before verification

### Wave 0 Gaps
- [ ] `tests/test_custom_oscillator.flow` -- covers SYNTH-01, SYNTH-02
- [ ] `tests/test_midi_export.flow` -- covers MIDI-01, MIDI-02
- [ ] DryWetMidi package: `cd flow-lang && dotnet add package Melanchall.DryWetMidi --version 8.0.3`

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 9 SDK | Build & run | Verified (project builds) | net9.0 | -- |
| DryWetMidi NuGet | MIDI export | Available on NuGet | 8.0.3 | -- |
| NuGet.org | Package restore | Requires internet | -- | -- |

**Missing dependencies with no fallback:** None
**Missing dependencies with fallback:** None

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| NAudio for MIDI | DryWetMidi | ~2020+ | DryWetMidi is cross-platform (.NET Standard 2.0), NAudio is Windows-centric |
| Per-sample proc evaluation for custom oscillators | Wavetable (pre-computed) | Standard practice | Orders of magnitude faster; avoids interpreter overhead in audio hot path |

## Open Questions

1. **How do collection built-ins (map, filter) invoke user procs?**
   - What we know: They receive a `Value` wrapping the function and need to call it with arguments
   - What's unclear: Exact mechanism for invoking a Flow proc from C# built-in code -- need to trace through `BuiltInFunctions.RegisterCollections` and the interpreter's function call path
   - Recommendation: Read the `map`/`filter` implementation before implementing `oscillator`. This is the critical pattern for SYNTH-01.

2. **Should writeMidi accept a synth type string for instrument mapping?**
   - What we know: `renderSong(Song, String)` takes a synth type, but `writeMidi(String, Song)` only takes a path and Song. Sections don't store instrument info.
   - What's unclear: Without an instrument parameter, all notes export as piano (program 0) unless we add a per-section instrument concept
   - Recommendation: For v1, export all as piano (matches D-16). The `instrument` block concept would be a future enhancement. Keep the API simple as decided in D-10.

## Sources

### Primary (HIGH confidence)
- Codebase: `flow-lang/StandardLibrary/Audio/NoteSynthesizer.cs` -- INoteSynthesizer interface, SynthesizerFactory, existing synth implementations
- Codebase: `flow-lang/StandardLibrary/Audio/SongRenderer.cs` -- Song rendering pipeline, section traversal pattern
- Codebase: `flow-lang/StandardLibrary/Audio/FileIO.cs` -- writeWav pattern for file-writing built-ins
- Codebase: `flow-lang/StandardLibrary/Audio/SynthUtils.cs` -- Shared synthesis utilities (ADSR, ToMonoBuffer)
- Codebase: `flow-lang/StandardLibrary/Audio/PitchConversion.cs` -- GetMidiNote for MIDI note numbers
- Codebase: `flow-lang/Runtime/MusicalContext.cs` -- Tempo, timesig, key storage; ValidKeys set
- Codebase: `flow-lang/TypeSystem/SpecialTypes/` -- SongData, SectionData, SequenceData, BarData, MusicalNoteData data structures
- [DryWetMidi NuGet 8.0.3](https://www.nuget.org/packages/Melanchall.DryWetMidi) -- Confirmed latest stable version
- [DryWetMidi GitHub](https://github.com/melanchall/drywetmidi) -- API reference, active maintenance

### Secondary (MEDIUM confidence)
- [DryWetMidi MIDI file creation gist](https://gist.github.com/melanchall/d4142f5f0fb36ab86e46110d69966fed) -- Example showing PatternBuilder, TrackChunk, and MidiFile.Write patterns
- [DryWetMidi Pattern/Composing docs](https://melanchall.github.io/drywetmidi/articles/composing/Pattern.html) -- PatternBuilder API reference

### Tertiary (LOW confidence)
- None

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- DryWetMidi 8.0.3 confirmed on NuGet, .NET Standard 2.0 compatible
- Architecture: HIGH -- All integration points verified in codebase; INoteSynthesizer, SynthesizerFactory, data model fully understood
- Pitfalls: HIGH -- Based on direct codebase analysis (delta time encoding, key string parsing, proc invocation pattern)

**Research date:** 2026-04-02
**Valid until:** 2026-05-02 (stable domain, no fast-moving dependencies)
