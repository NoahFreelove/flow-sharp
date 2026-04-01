# Technology Stack

**Project:** Flow Language - Audio/Music Feature Expansion
**Researched:** 2026-03-29

## Guiding Principle: Minimal Dependencies

Flow's existing codebase has exactly one NuGet dependency (Pidgin 3.5.1, not even used by the main parser). The MIDI importer (`flow-midi`) has zero dependencies. This is a deliberate design choice -- the project hand-rolls its own audio pipeline, WAV export, synthesizers, DSP, and MIDI parsing. Adding heavy external libraries would break this philosophy and create maintenance burden.

**Recommendation: Continue hand-rolling most features.** The features in scope (sidechain compression, panning, polyrhythms, voice allocation, custom oscillators, pattern generation, loop constructs, string interpolation, chord DSL, visualization) are all implementable with straightforward DSP math and interpreter extensions. Only MIDI export justifies an external dependency.

## Recommended Stack

### Core Runtime (Existing -- No Changes)

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| .NET 9 | net9.0 | Runtime | Already in use; LTS not required for a personal/dev tool |
| C# 13 | Latest | Language | Record types, pattern matching, file-scoped namespaces already used throughout |
| PulseAudio (P/Invoke) | System | Audio playback | Already implemented via `PulseAudioSimpleBackend`; stereo support exists |

**Confidence: HIGH** -- verified from csproj and existing code.

### New External Dependency: MIDI Export

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| Melanchall.DryWetMidi | 8.0.3 | MIDI file writing/export | The only feature that justifies an external library. Writing correct SMF (Standard MIDI Format) files requires handling variable-length encoding, delta times, track chunks, tempo maps, and channel assignments. Hand-rolling this is error-prone. DryWetMidi targets .NET Standard 2.0 (compatible with .NET 9), is actively maintained (v9.0.0-prerelease1 exists), has 198K+ downloads, and provides both low-level event access and high-level note/pattern APIs. |

**Confidence: HIGH** -- verified version and .NET 9 compatibility on NuGet.

**Alternative considered:** Hand-write MIDI export (the project already has a hand-written MIDI *parser* in `flow-midi/Midi/MidiParser.cs`). Rejected because writing is harder than reading -- you must produce byte-perfect SMF files that other software (DAWs, notation apps) can parse. A battle-tested library eliminates an entire class of bugs.

### Features Requiring NO New Dependencies (Hand-Roll)

These features are best implemented as pure C# within `flow-lang` using existing patterns:

| Feature | Implementation Approach | Why No Library Needed |
|---------|------------------------|----------------------|
| **Polyphonic voice allocation** | Voice pool manager in `StandardLibrary/Audio/` with round-robin or steal-oldest policy | Simple data structure (List + priority); existing `Voice` type and `SongRenderer` already mix multiple voices |
| **Custom oscillator definitions** | Allow `proc` functions as oscillator callbacks; evaluate user function per-sample or per-block | Interpreter already supports lambda/proc evaluation; add `OscillatorType` that wraps a user function |
| **Sidechain compression** | Extend existing `Compressor.cs` with a second input buffer as the sidechain source | Existing compressor already has envelope follower, attack/release coefficients -- just swap peak detection to read from sidechain buffer instead of input |
| **Spatial audio / panning** | Constant-power stereo panning: `left = cos(angle) * sample`, `right = sin(angle) * sample` | Two lines of math per sample; `AudioBuffer` already supports stereo (interleaved LRLRLR) |
| **Sample import (WAV loading)** | Reverse the existing `FileIO.cs` WAV writer -- read RIFF headers, parse fmt/data chunks, return `AudioBuffer` | The project already writes WAV with full understanding of the format; reading is the inverse operation |
| **Pattern variation / probabilistic** | Extend existing `(? ...)` random choice syntax with Markov chains, weighted selection, mutation operators | Existing `NoteStreamCompiler` already handles `(? ...)` and `(?? ...)`; extend with new syntax |
| **Polyrhythm support** | Allow multiple `timesig` contexts to run simultaneously; render each voice with its own time grid, then mix | `MusicalContext` stack already supports push/pop; extend to allow parallel contexts per voice |
| **Beat-synced live reload** | Use `FileSystemWatcher` (built into .NET), quantize reload to next bar boundary using tempo/timesig from `MusicalContext` | `FileSystemWatcher` is in `System.IO`; watch mode already exists in `flow-interpreter` |
| **Loop constructs (for/while)** | New AST nodes (`ForStatement`, `WhileStatement`), parser rules, interpreter dispatch | Standard interpreter feature; follow existing `ProcDeclaration`/`SectionDeclaration` patterns |
| **String interpolation** | Lexer recognizes `$"..."` or `"...{expr}..."`, parser produces `InterpolatedStringExpression`, evaluator concatenates | Common language feature; lexer already handles complex string parsing |
| **Chord progression DSL** | New syntax (e.g., `progression || I - IV - V - I ||`), parsed into chord sequence with auto-voicing | `ChordParser` and `HarmonyFunctions` already resolve roman numerals; extend with voice-leading algorithm |
| **Sequence visualization** | ASCII piano roll rendered to console: pitch on Y axis, time on X axis | Pure string building; `MusicalNoteData` already contains pitch/duration info |

**Confidence: HIGH** -- all verified against existing codebase patterns.

### Libraries Explicitly NOT Recommended

| Library | Why Not |
|---------|---------|
| **NAudio** | Windows-centric (COM/MME/WASAPI dependencies). Flow targets Linux with PulseAudio. NAudio would pull in platform-specific baggage and duplicate existing functionality. |
| **CSCore** | Similar to NAudio -- Windows-focused, heavy, would duplicate the hand-built audio pipeline. |
| **NWaves** | Tempting for DSP (has biquad filters, panning, effects). But Flow already has hand-written implementations of all needed DSP (reverb, filters, compressor, delay). Adding NWaves would create two parallel DSP stacks. At v0.9.6 (last updated Oct 2021), it's also showing signs of abandonment. |
| **managed-midi** | Marked as "past project" on GitHub. DryWetMidi is the clear winner. |
| **Pidgin** (already referenced) | Already in the csproj but unused by the actual parser. Could be removed to clean up dependencies. |
| **System.Numerics.Tensors / SIMD** | Premature optimization. Current sample-by-sample processing is clear and correct. SIMD vectorization could help if profiling shows buffer operations as bottleneck, but that's a performance phase concern, not a feature concern. |

## Implementation Techniques by Feature

### Polyphonic Voice Allocation

Standard approach: fixed-size voice pool (8-16 voices), allocate on note-on, release on note-off. When pool exhausted, steal oldest or quietest voice. This is how every hardware synthesizer works.

```csharp
// Sketch: VoiceAllocator.cs
public class VoiceAllocator
{
    private readonly Voice[] _pool;
    private int _nextVoice;

    public Voice Allocate(MusicalNoteData note) { /* round-robin or steal-oldest */ }
    public void Release(int voiceIndex) { /* mark available */ }
}
```

**Confidence: HIGH** -- well-established pattern, no ambiguity.

### Custom Oscillator Definitions

Allow Flow users to define oscillator shapes as procs that take phase (0-1) and return amplitude (-1 to 1):

```
proc Float myOsc(Float phase) {
    (if (< phase 0.5) 1.0 -1.0)  // square wave
}
```

Implementation: wrap the user proc in an `IOscillator` interface adapter that calls the interpreter for each sample (or per-block with optimization).

**Confidence: HIGH** -- interpreter already evaluates lambdas; wrapping in a callback is straightforward.

### Sidechain Compression

The existing `Compressor.Apply()` uses peak detection on the input signal. Sidechain compression uses a *different* signal for peak detection:

```csharp
// Current: envelope follows input
float peak = GetPeak(input, frame);
// Sidechain: envelope follows sidechain source
float peak = GetPeak(sidechainSource, frame);
```

Expose as: `sidechainCompress(input, sidechain, threshold, ratio, attack, release)`

**Confidence: HIGH** -- trivial extension of existing code.

### Spatial Audio / Panning

Constant-power panning law (industry standard):

```csharp
float angle = pan * MathF.PI / 2f; // pan: 0=left, 0.5=center, 1=right
float left = MathF.Cos(angle);
float right = MathF.Sin(angle);
```

Per-voice panning in `SongRenderer`: add `pan` property to voice/instrument assignment.

**Confidence: HIGH** -- standard DSP, well-documented.

### WAV Loading (Sample Import)

Reverse of existing `FileIO.cs`. Read RIFF header, validate "WAVE" format, parse fmt chunk for sample rate/channels/bit depth, read data chunk, convert to float32 AudioBuffer.

```csharp
public static AudioBuffer ImportWav(string filepath)
{
    using var reader = new BinaryReader(File.OpenRead(filepath));
    // Read RIFF header, fmt chunk, data chunk
    // Convert PCM samples to float32
    return new AudioBuffer(frames, channels, sampleRate);
}
```

**Confidence: HIGH** -- the project already demonstrates full WAV format knowledge in the writer.

### MIDI Export (with DryWetMidi)

Convert Flow's internal `SongData` / `SequenceData` / `MusicalNoteData` to MIDI events:

```csharp
var midiFile = new MidiFile();
var track = new TrackChunk();
// Convert MusicalNoteData -> MIDI NoteOn/NoteOff events
// Map Flow tempo -> MIDI tempo meta event
// Map Flow instruments -> MIDI program change
midiFile.Chunks.Add(track);
midiFile.Write("output.mid");
```

**Confidence: HIGH** -- DryWetMidi API is well-documented with examples.

### Beat-Synced Live Reload

Use .NET's built-in `FileSystemWatcher` to detect file changes. Queue reload for next bar boundary:

```csharp
var watcher = new FileSystemWatcher(directory, "*.flow");
watcher.Changed += (_, _) => {
    double beatsUntilNextBar = CalculateBeatsToNextBar(currentBeat, timeSignature);
    ScheduleReload(beatsUntilNextBar);
};
```

Watch mode already exists in `flow-interpreter`; this extends it with musical timing awareness.

**Confidence: MEDIUM** -- concept is clear but timing synchronization with audio playback thread needs careful implementation.

### Loop Constructs (for/while)

New AST nodes and parser rules:

```
// for loop
for Int i in (range 0 10) { (print (str i)) }

// while loop
Int x = 0
while (< x 10) { x = (+ x 1) }
```

Requires: `ForStatement` and `WhileStatement` AST records, parser rules (keyword detection already exists for `proc`, `section`, etc.), interpreter dispatch cases.

**Confidence: HIGH** -- standard interpreter feature, follows existing patterns exactly.

### String Interpolation

Lexer recognizes `$"text {expr} more text"`, parser produces a concatenation of string literals and evaluated expressions.

**Confidence: HIGH** -- common language feature, lexer already handles complex token sequences.

## Installation

```bash
# Add MIDI export dependency to flow-lang
cd flow-lang
dotnet add package Melanchall.DryWetMidi --version 8.0.3

# Everything else: no new packages needed
dotnet build
```

## Summary

| Category | Approach | External Dependencies |
|----------|----------|----------------------|
| MIDI Export | DryWetMidi library | YES -- Melanchall.DryWetMidi 8.0.3 |
| All other features | Hand-rolled C# in flow-lang | NO |

The project's existing architecture is well-suited for all planned features. The `AudioBuffer` class, `INoteSynthesizer` interface, DSP pipeline, `MusicalContext` stack, and interpreter infrastructure provide solid foundations. The only gap requiring an external library is MIDI file writing, where correctness matters more than in-house control.

## Sources

- [DryWetMidi NuGet](https://www.nuget.org/packages/Melanchall.DryWetMidi) -- v8.0.3, .NET Standard 2.0, confirmed .NET 9 compatible
- [DryWetMidi GitHub](https://github.com/melanchall/drywetmidi) -- active maintenance, comprehensive MIDI file R/W API
- [NWaves GitHub](https://github.com/ar1st0crat/NWaves) -- v0.9.6, last updated Oct 2021 (NOT recommended)
- [NAudio GitHub](https://github.com/naudio/NAudio) -- Windows-centric (NOT recommended)
- [Constant-power panning](https://lite14.net/blog/2025/01/24/how-to-implement-audio-panning-for-spatial-sound-effects/) -- standard panning law reference
- [Sidechain compression guide](https://mixingmonster.com/sidechaining-in-music-production/) -- concept reference
- Existing codebase: `flow-lang/StandardLibrary/Audio/` (DSP, Synthesizers, FileIO), `flow-midi/Midi/` (MIDI parser)
