# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Is

Flow is an interpreted, statically-typed language designed for music production. It features a flow operator (`->`) for function chaining, music-specific types (Note, Chord, Song, etc.), inline note stream syntax (`| C4 D4 E4 |`), musical context blocks (tempo, key, time signature), and a full audio pipeline from composition to WAV export to real-time playback. The interpreter is written in C# targeting .NET 10.

## Build & Run Commands

```bash
# Build the solution
dotnet build

# Run a .flow script
dotnet run --project flow-interpreter path/to/script.flow

# Start the REPL
dotnet run --project flow-interpreter

# Start the REPL in watch mode (auto-reload on file change)
dotnet run --project flow-interpreter -- --watch path/to/script.flow

# Evaluate a string expression
dotnet run --project flow-interpreter -e 'Int x = 5; (print (str x))'

# Run a specific test
dotnet run --project flow-interpreter tests/test_comprehensive.flow

# Run all tests
for test in tests/test_*.flow; do dotnet run --project flow-interpreter "$test"; done
```

There is no unit test framework. Tests are `.flow` scripts in `tests/` that are executed directly and verified by their console output (success = no errors). There are 70+ test files covering basic features, pipes, audio, musical context, note streams, chords, song structure, instruments, effects, transforms, generative features, lambdas, and imports.

## Project Structure

Two projects in the solution:
- **flow-lang** — Core language library (class library, namespace `FlowLang`)
- **flow-interpreter** — Console app for REPL and script execution (namespace `FlowInterpreter`)

### Directory Layout (flow-lang)

```
flow-lang/
  Core/                          # FlowEngine orchestrator
  Lexing/                        # SimpleLexer, TokenType (78 token types)
  Parsing/                       # Parser, TypeParser
  Ast/
    Expressions/                 # 13 expression node types
    Statements/                  # 8 statement node types
  Interpreter/                   # ExpressionEvaluator, Interpreter
  Runtime/                       # ExecutionContext, StackFrame, Value, ModuleLoader,
                                 #   MusicalContext, NoteStreamCompiler, Thunk
  TypeSystem/
    PrimitiveTypes/              # 15 primitive types
    SpecialTypes/                # 16 special (music) types
    OverloadResolver.cs, ArrayType.cs
  StandardLibrary/
    BuiltInFunctions.cs          # Main registration (stdlib, collections, bars)
    InternalFunctionRegistry.cs  # Signature → lambda mapping
    Audio/                       # Audio built-ins (23 files)
      DSP/                       #   Reverb, Filter, Compressor, Delay
      Synthesizers/              #   Piano, Brass, Sax, Drums
    Harmony/                     # ChordParser, HarmonyFunctions, ScaleDatabase
    Transforms/                  # TransformFunctions (transpose, invert, retrograde, etc.)
  Audio/                         # Playback infrastructure
    AudioPlaybackManager.cs      #   Manages audio backend lifecycle
    IAudioBackend.cs             #   Backend abstraction
    PulseAudioSimpleBackend.cs   #   PulseAudio P/Invoke implementation
  *.flow                         # Standard library modules (std, collections, audio, etc.)
```

## Architecture: Execution Pipeline

```
Source → SimpleLexer → Token[] → Parser → AST (immutable records) → Interpreter → Value
```

**FlowEngine** (`flow-lang/Core/FlowEngine.cs`) orchestrates the full pipeline. It creates the `InternalFunctionRegistry`, `ExecutionContext`, and `Interpreter`, then runs lexing → parsing → interpretation. It also owns the `AudioPlaybackManager` for real-time playback.

### Key components by pipeline stage:

| Stage | Key File | Role |
|-------|----------|------|
| Lexing | `Lexing/SimpleLexer.cs` | Manual character-by-character tokenizer with music literal detection (notes, chords, durations) |
| Parsing | `Parsing/Parser.cs` | Recursive descent parser; transforms `->` into nested calls at parse time; parses note streams, chords, songs, musical context blocks, lambdas, sections |
| Type parsing | `Parsing/TypeParser.cs` | Parses type annotations including arrays, music types, and function types |
| AST | `Ast/Expressions/`, `Ast/Statements/` | All nodes are C# `record` types (immutable) |
| Evaluation | `Interpreter/ExpressionEvaluator.cs` | Evaluates expressions to `Value` instances via switch dispatch |
| Execution | `Interpreter/Interpreter.cs` | Executes statements, manages function calls, implicit returns, musical context, sections |
| Runtime | `Runtime/ExecutionContext.cs` | Call stack (`Stack<StackFrame>`), variable/function scoping, musical context stack |
| Scope | `Runtime/StackFrame.cs` | Dictionary-based variable/function storage with parent chain |
| Values | `Runtime/Value.cs` | Wraps CLR values with Flow type info; factory methods for each type |
| Musical ctx | `Runtime/MusicalContext.cs` | Tempo, time signature, key, and swing state (push/pop scoping) |
| Note streams | `Runtime/NoteStreamCompiler.cs` | Compiles `\| C4 D4 E4 \|` syntax into Sequence values at evaluation time |
| Overloads | `TypeSystem/OverloadResolver.cs` | Resolves function calls by argument types using specificity scoring |
| Built-ins | `StandardLibrary/InternalFunctionRegistry.cs` | Maps function signatures to C# lambdas |
| Registration | `StandardLibrary/BuiltInFunctions.cs` | Registers all C# built-in functions at startup |
| Imports | `Runtime/ModuleLoader.cs` | Handles `use` statements; `@` prefix = stdlib dir, otherwise relative path |
| Lazy eval | `Runtime/Thunk.cs` | Memoizing thunk for deferred expressions (used by `if`, `and`, `or`) |
| Audio | `Audio/AudioPlaybackManager.cs` | Real-time audio playback via IAudioBackend abstraction |
| Synthesis | `StandardLibrary/Audio/Synthesizers/` | Instrument-specific note rendering (piano, brass, sax, drums) |
| DSP | `StandardLibrary/Audio/DSP/` | Audio effects processing (reverb, filter, compressor, delay) |
| Harmony | `StandardLibrary/Harmony/` | Chord parsing, scale database, roman numeral resolution |

## Key Design Decisions

- **Flow operator `->` is a parse-time transform**: `x -> func(arg)` becomes `func(x, arg)` as a `FunctionCallExpression` — there is no runtime flow concept.
- **Overload resolution**: Functions can have multiple overloads. `OverloadResolver` scores candidates: exact match (+1000), compatible (+500), convertible (+100). `Void` parameters act as wildcards.
- **Implicit returns**: The last non-void expression in a `proc` body is the return value, tracked by `ImplicitReturnCollector`.
- **Module imports execute in the caller's context** — no separate scope/namespace isolation. Circular imports are detected.
- **Error accumulation**: `ErrorReporter` collects errors rather than throwing, allowing multiple errors per pass.
- **Musical context is scoped**: `tempo`, `timesig`, `key`, and `swing` blocks push/pop state on a context stack in `ExecutionContext`, so nested blocks inherit and override naturally.
- **Note streams compile at evaluation time**: `| C4 D4 E4 |` is parsed into a `NoteStreamExpression` AST node, then `NoteStreamCompiler` produces a Sequence value using the active musical context (key, tempo, time signature).
- **Chord parsing at compile time**: `ChordParser.Parse()` recognizes chord symbols like `Cmaj7`, `Dm`, `F#dim` and produces `ChordData` with root, quality, and note list.
- **Roman numerals resolve from key context**: `I`, `IV`, `V7` etc. in note streams are resolved by `HarmonyFunctions` using the active key from `MusicalContext`.
- **Song rendering is multi-pass**: Sections → sequences → voices → buffers → final mix, with instrument selection (piano, brass, sax, drums).
- **Audio backend abstraction**: `IAudioBackend` allows platform-specific playback implementations; currently PulseAudio via P/Invoke.

## Language Features

### Core
- Static typing with type inference for some contexts
- Flow operator `->` for function chaining
- `proc` declarations with implicit returns
- Lambda functions: `fn Int x => x * 2`, `fn Int a, Int b => a + b`
- Function type annotations: `(Int => Int)`, `(Int, Int => Int)`
- Array literals and indexing (`arr@0`)
- Lazy evaluation
- Module imports via `use`

### Music-Specific
- **Musical context blocks**: `tempo 120 { ... }`, `timesig 4/4 { ... }`, `key Cmajor { ... }`, `swing 0.6 { ... }`
- **Note stream expressions**: `| C4 D4 E4 F4 |` with duration suffixes (`q`, `h`, `w`, `e`, `s`), rests (`_`), dotted notes (`C4q.`), tied notes (`C4h~`), cent offsets (`C4+50c`), chord brackets (`[C4 E4 G4]q`)
- **Chord literals**: `Cmaj7`, `Dm`, `F#dim`, `Bb7`
- **Roman numerals** (in key context): `I`, `ii`, `IV`, `V7`, `vi`
- **Random choice in note streams**: `(? C4 E4 G4)`, weighted: `(? C4:50 E4:30 G4:20)`, seeded: `(?? C4 E4 G4)`
- **Section declarations**: `section intro { ... }`
- **Song expressions**: `Song song = [intro verse*2 chorus bridge outro]`
- **Pattern transforms**: `transpose`, `invert`, `retrograde`, `augment`, `diminish`, `up`, `down`, `repeat`
- **Audio effects**: `reverb`, `lowpass`, `highpass`, `bandpass`, `compress`, `delay`, `gain`
- **Synthesizers**: piano, brass/horn, sax/saxophone, drums/drum
- **Playback**: `play`, `loop`, `preview`, `stop`
- **WAV export**: `writeWav`

## AST Node Types

### Expressions (13 types in `Ast/Expressions/`)
| Node | Description |
|------|-------------|
| `LiteralExpression` | Literals (int, float, string, bool, note, etc.) |
| `VariableExpression` | Variable reference |
| `BinaryExpression` | Binary operations (+, -, *, /, ==, !=, <, >, etc.) |
| `FunctionCallExpression` | Function invocation |
| `FlowExpression` | Flow operator (->) |
| `ArrayLiteralExpression` | Array literal |
| `ArrayIndexExpression` | Array indexing (arr@0) |
| `LazyExpression` | Lazy evaluation wrapper |
| `LambdaExpression` | Lambda functions (fn x => ...) |
| `MemberAccessExpression` | Member access (obj.field) |
| `ChordLiteralExpression` | Chord literal (Cmaj7, Dm) |
| `NoteStreamExpression` | Note stream (\| C4 D4 E4 \|) |
| `SongExpression` | Song arrangement ([intro verse chorus]) |

### Statements (8 types in `Ast/Statements/`)
| Node | Description |
|------|-------------|
| `VariableDeclaration` | Variable declaration with type |
| `AssignmentStatement` | Variable assignment |
| `ProcDeclaration` | Procedure definition |
| `ReturnStatement` | Explicit return |
| `ExpressionStatement` | Expression as statement |
| `ImportStatement` | Module import (use) |
| `MusicalContextStatement` | Musical context blocks (tempo, timesig, key, swing) |
| `SectionDeclaration` | Section declaration (section name { ... }) |

## Type System

Types are in `TypeSystem/` with two subdirectories. Each type extends `FlowType` and implements `IsCompatibleWith()`, `CanConvertTo()`, and `GetSpecificity()`. Numeric widening chain: Int → Long → Float → Double → Number.

### Primitive Types (`TypeSystem/PrimitiveTypes/`)
Void, Int, Float, Long, Double, String, Bool, Number, Buffer, Lazy, Function, Envelope, OscillatorState, Voice, Track

### Special Types (`TypeSystem/SpecialTypes/`)
Note, Semitone, Cent, Millisecond, Second, Decibel, Beat, Bar, TimeSignature, NoteValue, Sequence, MusicalNote, Chord, Section, Song

### Array Type
`ArrayType` in `TypeSystem/` — generic array with element type tracking.

## Built-in Function Categories

### Core (stdlib.cs)
I/O (`print`, `input`), string conversion (`str`), arithmetic, type conversions, comparisons, logical operators (`and`, `or`, `not`), control flow (`if`), random (`random`, `randomInt`, `choose`)

### Collections (BuiltInFunctions.cs)
`list`, `head`, `tail`, `last`, `init`, `empty`, `reverse`, `take`, `drop`, `append`, `prepend`, `concat`, `contains`, `map`, `filter`, `reduce`, `each`, `length`, `range`, `zip`

### Audio Core (Audio/*.cs)
Buffer creation (`buffer`, `silence`), signal generation (`sine`, `saw`, `square`, `triangle`, `noise`), envelopes (`adsr`, `applyEnvelope`), WAV export (`writeWav`)

### Audio Effects (Audio/DSP/)
`reverb`, `lowpass`, `highpass`, `bandpass`, `compress`, `delay`, `gain`

### Playback (Audio/PlaybackFunctions.cs)
`play`, `loop`, `preview`, `stop`, `audioDevices`, `setAudioDevice`, `isAudioAvailable`

### Musical Notation (Audio/ClassicalComposition.cs)
Musical notes (`musicalNote`, `rest`), bars, sequences, rendering (`renderSequence`, `renderSequences`)

### Song Rendering (Audio/SongRenderer.cs)
`renderSong` — renders a Song structure through sections → sequences → voices → buffer, with instrument selection

### Harmony (Harmony/)
`chordNotes`, `chordRoot`, `chordQuality`, `arpeggio`, `scaleNotes`, `resolveNumeral`, `getSections`, `sectionSequences`

### Pattern Transforms (Transforms/)
`transpose`, `invert`, `retrograde`, `augment`, `diminish`, `up`, `down`, `repeat`, `concat` (for sequences)

### Generative
`euclidean` rhythms, random choice in note streams (`(? ...)`, `(?? ...)`)

## Standard Library Modules (.flow files)

These live in `flow-lang/` and are loaded via `use "@name"`:
- `std.flow` — imports `@collections` and `@bars`
- `collections.flow` — list operations (head, tail, map, filter, etc.)
- `audio.flow` — buffer creation, signal generation, effects, playback convenience functions
- `bars.flow` — simple bar/note operations
- `notation.flow` — musical notation (note durations, rests, time signatures, bar/sequence building)
- `composition.flow` — timeline, voice, track convenience functions

## Adding New Built-in Functions

1. Define the function in `StandardLibrary/BuiltInFunctions.cs` (or the relevant partial class / subdirectory like `Audio/`, `Harmony/`, `Transforms/`)
2. Create a `FunctionSignature` with the name, parameter types, and varargs flag
3. Register via `registry.Register(signature, args => { ... })` in the appropriate `Register*` method
4. If adding a new module, call the registration method from `FlowEngine.cs`

## Adding New Synthesizers

1. Create a new class in `StandardLibrary/Audio/Synthesizers/` implementing the synthesizer pattern (see `PianoSynthesizer.cs` for reference)
2. Register the instrument name mapping in `SongRenderer.cs`

## C# Conventions

- .NET 10, nullable reference types enabled, implicit usings
- File-scoped namespaces throughout
- All namespaces under `FlowLang.*` (library) or `FlowInterpreter` (console app)
- AST nodes are `record` types for immutability
- Pattern matching (`switch` expressions) for node dispatch rather than visitor pattern
- External dependency: Pidgin parser combinator library (referenced but SimpleLexer/Parser are manual)

<!-- GSD:project-start source:PROJECT.md -->
## Project

**Flow Language**

Flow is an interpreted, statically-typed programming language designed for music production. Written in C# (.NET 10), it features a flow operator (`->`) for function chaining, music-specific types (Note, Chord, Song, etc.), inline note stream syntax, musical context blocks, a full audio pipeline from composition to WAV export, real-time playback via PulseAudio, and MIDI import. It targets composers, producers, and creative coders who want a textual, scriptable approach to music creation.

**Core Value:** Users can write musical ideas as code and hear them immediately — the language must faithfully translate musical notation into correct, playable audio.

### Constraints

- **Runtime**: .NET 10 — all code must target net10.0
- **Platform**: Linux primary (PulseAudio dependency), but IAudioBackend abstraction exists for portability
- **Dependencies**: Minimal — only Pidgin parser combinator (referenced but not used for main parser)
- **Performance**: Real-time audio playback requires efficient buffer operations; no GC pressure in hot paths
- **Compatibility**: Existing .flow scripts and test suite must continue to work
<!-- GSD:project-end -->

<!-- GSD:stack-start source:research/STACK.md -->
## Technology Stack

## Guiding Principle: Minimal Dependencies
## Recommended Stack
### Core Runtime (Existing -- No Changes)
| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| .NET 10 | net10.0 | Runtime | Already in use; LTS not required for a personal/dev tool |
| C# 13 | Latest | Language | Record types, pattern matching, file-scoped namespaces already used throughout |
| PulseAudio (P/Invoke) | System | Audio playback | Already implemented via `PulseAudioSimpleBackend`; stereo support exists |
### New External Dependency: MIDI Export
| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| Melanchall.DryWetMidi | 8.0.3 | MIDI file writing/export | The only feature that justifies an external library. Writing correct SMF (Standard MIDI Format) files requires handling variable-length encoding, delta times, track chunks, tempo maps, and channel assignments. Hand-rolling this is error-prone. DryWetMidi targets .NET Standard 2.0 (compatible with .NET 10), is actively maintained (v9.0.0-prerelease1 exists), has 198K+ downloads, and provides both low-level event access and high-level note/pattern APIs. |
### Features Requiring NO New Dependencies (Hand-Roll)
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
### Custom Oscillator Definitions
### Sidechain Compression
### Spatial Audio / Panning
### WAV Loading (Sample Import)
### MIDI Export (with DryWetMidi)
### Beat-Synced Live Reload
### Loop Constructs (for/while)
### String Interpolation
## Installation
# Add MIDI export dependency to flow-lang
# Everything else: no new packages needed
## Summary
| Category | Approach | External Dependencies |
|----------|----------|----------------------|
| MIDI Export | DryWetMidi library | YES -- Melanchall.DryWetMidi 8.0.3 |
| All other features | Hand-rolled C# in flow-lang | NO |
## Sources
- [DryWetMidi NuGet](https://www.nuget.org/packages/Melanchall.DryWetMidi) -- v8.0.3, .NET Standard 2.0, confirmed .NET 10 compatible
- [DryWetMidi GitHub](https://github.com/melanchall/drywetmidi) -- active maintenance, comprehensive MIDI file R/W API
- [NWaves GitHub](https://github.com/ar1st0crat/NWaves) -- v0.9.6, last updated Oct 2021 (NOT recommended)
- [NAudio GitHub](https://github.com/naudio/NAudio) -- Windows-centric (NOT recommended)
- [Constant-power panning](https://lite14.net/blog/2025/01/24/how-to-implement-audio-panning-for-spatial-sound-effects/) -- standard panning law reference
- [Sidechain compression guide](https://mixingmonster.com/sidechaining-in-music-production/) -- concept reference
- Existing codebase: `flow-lang/StandardLibrary/Audio/` (DSP, Synthesizers, FileIO), `flow-midi/Midi/` (MIDI parser)
<!-- GSD:stack-end -->

<!-- GSD:conventions-start source:CONVENTIONS.md -->
## Conventions

Conventions not yet established. Will populate as patterns emerge during development.
<!-- GSD:conventions-end -->

<!-- GSD:architecture-start source:ARCHITECTURE.md -->
## Architecture

Architecture not yet mapped. Follow existing patterns found in the codebase.
<!-- GSD:architecture-end -->

<!-- GSD:workflow-start source:GSD defaults -->
## GSD Workflow Enforcement

Before using Edit, Write, or other file-changing tools, start work through a GSD command so planning artifacts and execution context stay in sync.

Use these entry points:
- `/gsd:quick` for small fixes, doc updates, and ad-hoc tasks
- `/gsd:debug` for investigation and bug fixing
- `/gsd:execute-phase` for planned phase work

Do not make direct repo edits outside a GSD workflow unless the user explicitly asks to bypass it.
<!-- GSD:workflow-end -->

<!-- GSD:profile-start -->
## Developer Profile

> Profile not yet configured. Run `/gsd:profile-user` to generate your developer profile.
> This section is managed by `generate-claude-profile` -- do not edit manually.
<!-- GSD:profile-end -->
