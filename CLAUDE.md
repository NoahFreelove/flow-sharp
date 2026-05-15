# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Is

Flow is an interpreted, statically-typed language designed for music production. It features a flow operator (`->`) for function chaining, music-specific types (Note, Chord, Song, etc.), inline note stream syntax (`| C4 D4 E4 |`), musical context blocks (tempo, key, time signature), and a full audio pipeline from composition to WAV export to real-time playback. The interpreter is written in C# targeting .NET 10.

## Goals & Non-Goals

**Goals**
- **Ergonomics first.** Music production is historically slow and demands beefy
  computers. Flow prioritizes composer ergonomics over everything else — runtime
  efficiency, type strictness, and generality all yield to it.
- **Genre-agnostic.** Flow should let you write a classical symphony, an EDM
  track, jazzy blues, modern pop, and death metal in one place. We support MIDI,
  instrument generation, and plan to add vocaloid-style voices.
- **Make the easy cases fast.** The common, well-typed paths should be as fast
  as we can reasonably make them.

**Non-Goals**
- **General-purpose computation.** Flow *can* compute, but that's not what it's
  for. Don't bend the language to serve non-musical use cases.
- **Maximum runtime efficiency.** Flow is interpreted — it isn't trying to be C.
- **Type strictness for its own sake.** If a user wants to multiply a `Float` by
  a `Double`, let them. The flexible path doesn't have to match the speed of the
  strict-typed equivalent — flexibility is the point.
- **A language for one kind of music.** No genre is privileged in the design.

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
    Expressions/                 # 12 expression node types
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
- **Implicit returns**: `ImplicitReturnCollector` collects **every** non-void expression during execution. At end of function: 0 collected → `Void`; 1 collected → that value; 2+ collected → an **array** of all collected values. An explicit `return X` statement clears the collected list and short-circuits with `X`. To return Void implicitly, ensure no non-void expressions trail the function (e.g. end with a `(print)` call or a variable declaration, both of which are void). There is no `Void`/`null`/`Nothing`/`()` literal in source — `return` without an expression is a parse error.
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
- Lambda functions: `fn Int x => (mul x 2)`, `fn Int a, Int b => (add a b)`
- Function type annotations: `(Int => Int)`, `(Int, Int => Int)`
- Array literals and indexing (`arr@0`)
- Lazy evaluation
- Module imports via `use`
- Prefix-only arithmetic via `(add)`/`(sub)`/`(mul)`/`(div)`/`(neg)`/`(idiv)` and `(concat)` builtins (no infix `+ - * /`)
- **Symbol primitive type:** `#foo` interned literal — pointer-equality, strict separation from String (Phase 26.1)
- **Tuple type:** `<<a, b, c>>` literal with per-position types and arity (`<<>>` empty + `<<x>>` singleton valid); `tup@N` indexing; `<<a, b>> = expr` destructuring assignment; structural equality (Phase 26.1)
- **`~>` flow operator:** unpacks a tuple into a multi-arg call as a parse-time chain operator; falls through to single-arg `->` semantics on non-tuple LHS (Phase 26.1)
- **Generic `Dict<K, V>`:** insertion-order preservation; allowed keys are Int / Long / Float / String / Symbol / Note / Chord / Tuple-of-hashables; constructed via `(dict K V K V ...)` or `(dictTuple <<K,V>> ...)` builtins; 14-op surface — `(get)` / `(getOr)` / `(set)` / `(remove)` / `(has)` / `(keys)` / `(values)` / `(size)` / `(merge)` / `(each)` / `(map)` / `(filter)` (Phase 26.1)
- **`(unpack tuple func)`:** runtime first-class apply — S-expression equivalent of `~>`; mirrors Lisp's `(apply f args)` (Phase 26.1)
- **`gain` vs `volume` distinction:** `gain(Buffer, Double|Decibel)` interprets its 2nd arg as decibels (negative = attenuate, positive = amplify); `volume(Buffer, Double)` interprets its 2nd arg as a linear multiplier (0.5 = half-amplitude, 2.0 = double-amplitude). Composer chooses by semantic intent — function name documents the unit. Negative `volume` rejected (use `gain` for dB attenuation); both emit clipping warnings to stderr when post-multiplication samples exceed 1.0 (Phase 26.2)
- **Hertz type + literal syntax:** `Hertz` first-class music type for audio frequency parameters; `800Hz` and `1.5kHz` literals (kHz canonical-Hz at lex time: 1.5kHz → 1500.0). Used by filters (`lowpass`/`highpass`/`bandpass`) + signal generators (`createSineTone`/`createSawTone`/`createSquareTone`/`createTriangleTone`); coexists with bare-Double overloads via OverloadResolver exact-match scoring (Phase 26.2)
- **Voice-block polyphony:** `| {voice C4w} {voice C5q D5q E5q F5q} |` — multiple parallel voices share a bar's onset; the compiler emits `BarData.ParallelVoices` and `BarRenderer` recurses into each voice block, mixing additively in `SongRenderer`. Same render path used by both audio (WAV) and MIDI export — voice blocks produce overlapping NoteOn events at the parent's tick. Phase 22's `legato(Sequence, Double)` transform stays compatible (Phase 28)
- **First-class `Articulation.Legato`:** `leg` note-stream token (alongside `>` Accent, `stacc` Staccato, `ten` Tenuto, `marc` Marcato) compiles to `Articulation.Legato` enum value. Distinct from the Phase 22 `legato()` transform — the enum value drives per-note articulation envelope shaping; the transform adjusts `DurationOverlap`. Both compose: a note with `Articulation.Legato` AND `DurationOverlap=0.5` renders at 1.0 × 1.10 × 1.5 = 1.65 of authored duration (Phase 28)
- **Locked articulation rules:** Staccato 25% duration + sustain=0 + release×0.5; Marcato 25% duration + Accent's velocity boost; Tenuto 100% duration + release×1.2 soft; Legato 110% duration + crossfade overlap; Accent +0.30 velocity (clamped); Sforzando 100% duration + 1.5×→1.0× envelope spike over first 15% of frames (replaces prior `velocity = 0.95` static — composer's base velocity passes through). All 9 shipping synthesizers (Piano, Brass, Sax, Drums, Bell, Flute, Organ, Strings, Wavetable) route through `SynthUtils.GenerateArticulationADSR`; drums opt out via `isPercussion: true` no-op (Phase 28)
- **Multi-track MIDI export:** `writeMidi` emits one MIDI track per uniqueSequenceName + the conductor track. Sequence names route to GM programs via prefix-match (`piano*`→0, `brass*`/`horn*`→56, `sax*`→65, `flute*`→73, `string*`→48, `organ*`→19, `bell*`→14, `drum*`→0 on channel 9 GM percussion). Cross-section same-name sequences concatenate onto the same track in chronological tick order (Phase 28)
- **Voice-pool allocation:** `voicePool N { ... }` musical-context block (range 1..256) with the SPEC-7 locked default of 32. When the voice count exceeds the pool, the active voice with the EARLIEST onset is truncated at the new voice's onset (steal-oldest). Tiebreaker: original input index — deterministic across runs, preserves Phase 18/25/27 two-run-cmp-clean determinism contract (Phase 28)
- **Sample-based tonal instruments:** Piano, Brass, Sax, Strings, Flute, Bell render via `SampledInstrumentRenderer` (`flow-lang/StandardLibrary/Audio/SampledInstrumentRenderer.cs`) backed by a CC-BY 4.0 University-of-Iowa MIS bundle under `flow-lang/Samples/` (3.05 MB / 21 WAVs / 44.1 kHz 16-bit mono, ≤ 5 MB cap enforced by `RepoSizeTests`). Each tonal synthesizer class is a ≤ 25-line delegation shell; the renderer picks the nearest-pitch sample and linear-interpolation-varispeed shifts, then layers Phase 28's locked articulation envelope on top. Piano has two velocity layers (pp / ff cross-faded by `note.Velocity`); other tonal instruments use a single mezzo-forte sample with linear amplitude scaling. Drums, Organ, and Wavetable remain synthesis-based with hand-rolled DSP gains (≥ 20% harmonic-richness floor measured by `HarmonicRichnessTests`) per SPEC D-02. Eager-load on `renderSong` entry via per-FlowEngine `SampleCache` keyed by `(instrument, midiPitch)`; subsequent renders in the same process reuse the cache (Phase 29)
- **Sample bundle attribution:** `flow-lang/Samples/CREDITS.md` (bundle-wide) plus per-instrument `flow-lang/Samples/{instrument}/LICENSE.md` ship attribution for the CC-BY 4.0 source. SPEC-2 was relaxed from "CC0 only" to "CC0 / Public Domain / CC-BY 3.0 / CC-BY 4.0" at Plan 29-01 (2026-05-11); CC-BY-SA and CC-BY-NC remain rejected. Audited automatically by `flow-lang.Tests/Integration/Phase29/LicenseAuditTests.cs` (Phase 29)
- **Known sampled-instrument quirks (v1.5 backlog):** the flute's two-sample coverage (G4 + G5) produces an audible timbre crossover at D5 when melodies span the boundary — fix is more flute samples or weighted cross-fade. Sampled instruments under extreme staccato (25% duration + sustain=0 + release×0.5 — Phase 28's locked envelope) sound thinner than the same articulation on the Phase 28 hand-rolled synths because the envelope cuts before the sample body develops — v1.5 will add per-articulation envelope multipliers for the sample path. Drum realism gain is measurable (≥ 20% harmonic richness) but subtle in casual A/B because drums remain synth-only per SPEC D-02 — sampled drums need transient-preserving pitch shift (Phase 29 v1.5 follow-ups)

### Music Types Quick Reference

Composer-facing summary of every music-typed literal Flow ships, the runtime type
backing it, the numeric coercions it accepts, and the function sites that take it.
Single source of truth — alongside the Special Types list above.

| Literal              | Type          | IsCompatibleWith         | Accepted at                                                                          |
|----------------------|---------------|--------------------------|--------------------------------------------------------------------------------------|
| `-12dB`              | `Decibel`     | `Double`, `Float`        | `gain`, `compress` threshold, `sidechain` threshold, anywhere `Double` is accepted    |
| `100ms`              | `Millisecond` | `Double`, `Float`        | `delay`, `compress` attack/release, `sidechain` attack/release, `CanConvertTo Second` |
| `2.5s`               | `Second`      | `Double`, `Float`        | `reverb` decay, `CanConvertTo Millisecond`                                            |
| `+50c`               | `Cent`        | `Double`, `Float`        | `transpose` cent-precision                                                            |
| `+2st`               | `Semitone`    | `Int` (whole-numbers-by-design) | `transpose` semitone-precision                                                  |
| `1.5` (Beat-tagged)  | `Beat`        | `Double`, `Float`        | beat-position arithmetic                                                              |
| `440Hz` / `1.5kHz`   | `Hertz`       | `Double`, `Float`        | `lowpass`/`highpass`/`bandpass`, `createSineTone`/`createSawTone`/etc.                |
| `#foo`               | `Symbol`      | strict (no `Double`/`Float`) | `Dict<Symbol, V>` keys, identity-equality usage                                   |

Notes:
- Decibel and Millisecond/Second use the `CentType.cs:24-27` pattern (sealed singleton with `IsCompatibleWith(Double|Float)`); see `flow-lang/TypeSystem/SpecialTypes/`.
- Hertz stores a single canonical Hz double — `1.5kHz` is canonicalized to `1500.0` at lex time (no runtime unit-discriminator).
- Symbols are STRICTLY separate from `String` — `(equals #foo "foo")` is `false` (Phase 26.1 SYM-01).
- Music-typed literals at expression-start (after `(`, `=`, `,`, etc.) lex as single tokens — see Phase 26.2 ERG-05.

### Music-Specific
- **Musical context blocks**: `tempo 120 { ... }`, `timesig 4/4 { ... }`, `key Cmajor { ... }`, `swing 0.6 { ... }`. Timesig accepts the common-time shorthand `timesig C { ... }` (capital `C` only; lowers to 4/4 at parse time so the renderer / MIDI export / musical-context stack see identical data to the explicit form)
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

### Expressions (15 types in `Ast/Expressions/`)
| Node | Description |
|------|-------------|
| `LiteralExpression` | Literals (int, float, string, bool, note, etc.) |
| `VariableExpression` | Variable reference |
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
| `SymbolLiteralExpression` | Symbol literal (`#foo`) — interned at evaluation time via `ExecutionContext.SymbolInternTable` |
| `TupleLiteralExpression` | Tuple literal (`<<a, b, c>>`) with empty + singleton arities |
| `TupleUnpackFlowExpression` | Tuple-unpack flow operator (`~>`) — runtime dispatch with non-tuple fallthrough |

**Note:** Arithmetic uses prefix builtins, not AST nodes. See `(add)`/`(sub)`/`(mul)`/`(div)`/`(neg)`/`(idiv)` and `(concat)` in the Standard Library — there is no `BinaryExpression` AST node; the parser produces `FunctionCallExpression` for arithmetic.

### Statements (9 types in `Ast/Statements/`)
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
| `TupleDestructureStatement` | Destructuring assignment (`<<Type? name, Type? name>> = expr`) |

## Type System

Types are in `TypeSystem/` with two subdirectories. Each type extends `FlowType` and implements `IsCompatibleWith()`, `CanConvertTo()`, and `GetSpecificity()`. Numeric widening chain: Int → Long → Float → Double → Number.

### Primitive Types (`TypeSystem/PrimitiveTypes/`)
Void, Int, Float, Long, Double, String, Bool, Number, Buffer, Lazy, Function, Envelope, OscillatorState, Voice, Track

### Special Types (`TypeSystem/SpecialTypes/`)
Note, Semitone, Cent, Millisecond, Second, Decibel, Beat, Hertz, Bar, TimeSignature, NoteValue, Sequence, MusicalNote, Chord, Section, Song

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
`reverb`, `lowpass`, `highpass`, `bandpass`, `compress`, `delay`, `gain` (dB), `volume` (linear)

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

- **Pre-Phase-28 byte-identical determinism for tutorial.flow / showcase.flow output is dropped**: Phase 28's articulation envelope rewrite legitimately changes the rendered bytes. Two-run determinism IS preserved (consecutive runs at the same git SHA produce byte-identical output) — the contract in shape, not in pinned bytes.
- **RMS-windowed regression testing**: for behavior that legitimately changes bytes but should preserve perceptual fidelity, use `flow-lang.Tests/Helpers/RmsRegressionTests.AssertRmsWithinTolerance` (or the file-path overload `AssertWavMatchesBaseline`) with the SPEC-8 locked ±0.5 dB / 100ms tolerance. Baselines live under `flow-lang.Tests/baselines/Phase28/` — committed because the dither RNG is seeded deterministically (Phase 15 Plan 05) so two writes of the same buffer produce byte-identical baselines.
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
