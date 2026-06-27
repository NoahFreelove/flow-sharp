<!-- generated-by: gsd-doc-writer -->

# Flow Language Architecture

This document is a developer's tour of how Flow is structured. It's aimed at
someone who wants to understand the codebase before adding a feature, fixing a
bug, or porting Flow to a new platform. For composer-facing language reference,
see [`README.md`](../README.md) and [`FEATURES.md`](../FEATURES.md). For the
agent-shaped deep dive used by Claude Code sessions, see
[`CLAUDE.md`](../CLAUDE.md) — this doc is its public-facing companion, not its
replacement.

## What Flow Is, In Architectural Terms

Flow is a single-pass tree-walking interpreter for a statically-typed,
functional-leaning, music-domain DSL. The interpreter is written in C# 13
targeting .NET 10. There is no bytecode VM, no JIT compilation step, and no
intermediate representation beyond the AST itself — execution walks the AST
directly via pattern-matched switch dispatch.

The architecture is shaped by three design priorities, in order:

1. **Composer ergonomics over everything.** Hot paths can be slow; type
   coercions can be flexible; degenerate inputs return reasonable defaults
   instead of throwing. If a design choice forces composers to think about the
   interpreter, it's the wrong choice.
2. **Genre-agnostic music modeling.** Every primitive (Note, Chord, Sequence,
   Section, Song, Tuning, Sfz) is designed to serve a symphony as readily as
   a death metal track. No genre is privileged in the type system or the
   standard library.
3. **Minimal external dependencies.** Flow ships with two real third-party
   libraries: `Melanchall.DryWetMidi` (MIDI file IO — the one thing not worth
   hand-rolling) and `OmniSharp.Extensions.LanguageServer` (LSP protocol
   plumbing for the LSP server). Everything else — lexing, parsing, type
   resolution, audio synthesis, DSP, SFZ orchestral sampling, MusicXML/LilyPond
   IO, MIDI parsing for `midi2flow` — is hand-rolled C# inside this repo.

## Execution Pipeline

A Flow program flows through six stages, all orchestrated by `FlowEngine`
(`flow-lang/Core/FlowEngine.cs`):

```
Source
   │
   ▼
PragmaScanner  ──►  pragma set + transformed source
   │
   ▼
SimpleLexer    ──►  Token[]
   │
   ▼
Parser         ──►  Program (AST of immutable records)
   │
   ▼
ApplyTuningPragma  (resolves file-scope tuning to musical context)
   │
   ▼
Interpreter    ──►  Value(s) + side effects (WAV writes, audio playback)
```

There is no separate type-check pass — types are resolved at runtime as part of
overload dispatch. There is no AST-rewrite optimization pass. Each stage is
pure-functional in shape (its output depends only on its input), which makes
tracing bugs straightforward: lex a script, dump tokens, parse them, dump the
AST, walk it, observe the Value.

### Stage 0 — Pragma scanning

`Lexing/PragmaScanner.cs` strips file-scope `enable X;` lines before lexing
begins. This keeps pragmas out of the token stream entirely — by the time the
parser sees the source, all pragmas are stored in a `PragmaSet` attached to the
program. Currently used for tuning system selection (`enable justIntonation;`,
`enable pythagorean;`, `enable equalTemperament;`).

### Stage 1 — Lexing

`Lexing/SimpleLexer.cs` is a hand-rolled character-by-character tokenizer
(~1,260 lines, 78 token types). It does NOT use a regex engine or lexer
generator. The lexer is hand-rolled because Flow's music literals are
context-sensitive in ways that don't fit a normal tokenizer:

- Note literals like `C4`, `F#5`, `Bb3` need to be distinguished from
  identifiers based on surrounding tokens.
- Chord symbols like `Cmaj7`, `F#dim`, `Bb7` need to be detected at lex time so
  the parser can build a `ChordLiteralExpression` instead of three identifiers.
- Duration suffixes (`q`, `h`, `w`, `e`, `s`), articulation symbols (`stacc`,
  `leg`, `ten`, `marc`), and dotted/tied notation (`C4q.`, `C4h~`) all need
  music-aware lookahead.
- Unit-suffixed numerics — `-12dB`, `100ms`, `2.5s`, `+50c`, `+2st`, `800Hz`,
  `1.5kHz` — lex as single tokens at expression-start positions. `1.5kHz` is
  canonicalized to `1500.0` Hz at lex time so the rest of the pipeline sees
  one numeric type, not a unit discriminator.

### Stage 2 — Parsing

`Parsing/Parser.cs` is a hand-rolled recursive-descent parser (~1,980 lines).
It builds an AST of C# `record` types — all AST nodes are immutable. The parser
does two important parse-time transforms:

- **The `->` flow operator is a parse-time rewrite, not a runtime construct.**
  `x -> func(arg)` becomes `func(x, arg)` as a normal `FunctionCallExpression`
  in the AST. There is no `FlowExpression` in the live AST after parsing
  completes — the operator only exists in the source. This is why `->` has
  zero runtime cost.
- **Common-time shorthand `timesig C { ... }` lowers to `timesig 4/4 { ... }`
  at parse time.** Same downstream AST as the explicit form, so the renderer
  and MIDI export never see the shorthand variant.

The companion `Parsing/Parser.NoteStream.cs` handles the note-stream syntax
(`| C4 D4 E4 |`) — it produces a `NoteStreamExpression` whose actual compilation
into a `Sequence` value is deferred to evaluation time (Stage 5), when the
active musical context is known. `Parsing/TypeParser.cs` handles type
annotations including arrays, generic dict types, function types, and music
types.

### Stage 3 — AST

The AST lives in `flow-lang/Ast/`:

- **Expressions** (`Ast/Expressions/`, 17 node types) — `LiteralExpression`,
  `VariableExpression`, `FunctionCallExpression`, `ArrayLiteralExpression`,
  `ArrayIndexExpression`, `LambdaExpression`, `MemberAccessExpression`,
  `ChordLiteralExpression`, `NoteStreamExpression`, `SongExpression`,
  `SymbolLiteralExpression`, `TupleLiteralExpression`,
  `TupleUnpackFlowExpression`, `LazyExpression`, `MatchExpression`,
  `InterpolatedStringExpression`, `ProgressionExpression`.
- **Statements** (`Ast/Statements/`, 14 node types) — `VariableDeclaration`,
  `AssignmentStatement`, `ProcDeclaration`, `ReturnStatement`,
  `ExpressionStatement`, `ImportStatement`, `MusicalContextStatement`,
  `SectionDeclaration`, `TupleDestructureStatement`, `ForStatement`,
  `WhileStatement`, `BreakStatement`, `ContinueStatement`,
  `TuningContextStatement`.
- **Patterns** (`Ast/Patterns/`) — `LiteralPattern`, `BindingPattern`,
  `WildcardPattern`, `ConstructorPattern`, `GuardPattern`, `MatchArm`.

Note that there is no `BinaryExpression` node — arithmetic is performed by
prefix builtins (`(add)`, `(sub)`, `(mul)`, `(div)`, `(neg)`, `(idiv)`,
`(concat)`). The parser produces a `FunctionCallExpression` for every
arithmetic operation. This is a deliberate design decision: Flow is
Lisp-influenced, and infix arithmetic would conflict with the prefix
S-expression style used throughout the language.

### Stage 4 — Tuning pragma resolution

A small step between parse and interpret: `FlowEngine.ApplyTuningPragma`
translates file-scope tuning pragmas into the bottom-of-stack frame of the
musical context's `TuningStack`. This is the bridge between the lex-time
pragma extraction and the runtime musical context system.

### Stage 5 — Interpretation

`Interpreter/Interpreter.cs` (~1,060 lines) walks the AST using switch dispatch
on the node type. Each statement type has its own handler; expression
evaluation is delegated to `Interpreter/ExpressionEvaluator.cs`. The
interpreter is single-threaded and synchronous.

Function calls — whether to user procs or built-in C# implementations — go
through `TypeSystem/OverloadResolver.cs`, which picks the most-specific
matching overload based on argument types.

## Project Layout

The solution contains seven projects, organized by responsibility:

| Project | Role |
|---------|------|
| `flow-lang/` | The language library — lexer, parser, AST, type system, interpreter, runtime, standard library, audio pipeline. All other projects depend on this. |
| `flow-interpreter/` | Legacy entry point — REPL, watch-mode, script runner. Predates the unified CLI; kept for backwards compatibility. |
| `flow-cli/` | The shipping `flow` binary — 13 subcommands (`run`, `eval`, `repl`, `watch`, `play`, `render`, `flow2midi`, `midi2flow`, `check`, `new`, `test`, `lsp`, `version`). |
| `flow-lsp/` | Language Server Protocol 3.17 server over stdio. Powers editor integrations. |
| `flow-midi/` | Standalone MIDI import library + the `midi2flow` CLI logic. Round-trips `.mid` → `.flow` source. |
| `flow-jetbrains/` | JetBrains IDE plugin (Gradle/Kotlin project) that drives `flow-lsp` via LSP4IJ. |
| `vscode-extension/` | VSCode language extension (TypeScript) with bundled per-platform `flow-lsp` binaries. |

### `flow-lang/` internals

The library is organized by pipeline stage rather than by feature:

```
flow-lang/
├── Core/              FlowEngine + SourceMap + SourceLocation + Span
├── Lexing/            SimpleLexer + Token/TokenType + PragmaScanner/Set/Registry
├── Parsing/           Parser + Parser.NoteStream + TypeParser
├── Ast/
│   ├── Expressions/   17 expression record types
│   ├── Statements/    14 statement record types
│   ├── Patterns/      6 pattern record types
│   ├── Elements/      shared element types (proc params, etc.)
│   └── Program.cs     top-level AST root
├── TypeSystem/
│   ├── PrimitiveTypes/   16 primitives (Int, Long, Float, Double, String,
│   │                     Bool, Number, Buffer, Lazy, Function, Envelope,
│   │                     OscillatorState, Voice, Track, Symbol, Void)
│   ├── SpecialTypes/     22 music types (Note, Chord, Sequence, Section,
│   │                     Song, Tuning, Sfz, MarkovModel, LsystemModel,
│   │                     Hertz, Cent, Semitone, Decibel, Millisecond,
│   │                     Second, Beat, Bar, TimeSignature, NoteValue,
│   │                     MusicalNote, Tuple, Dict)
│   ├── OverloadResolver.cs   specificity-scored function dispatch
│   ├── FunctionSignature.cs  signature + named parameter metadata
│   ├── ArrayType.cs          generic Array<T>
│   └── TypeChecker.cs        compatibility/conversion helpers
├── Interpreter/
│   ├── Interpreter.cs            statement dispatch
│   ├── ExpressionEvaluator.cs    expression dispatch
│   ├── ImplicitReturnCollector.cs  collects trailing expressions
│   ├── PatternMatcher.cs         pattern dispatch for match/sections
│   └── SectionOverloadDispatch.cs  picks parameterized section by signature
├── Runtime/
│   ├── ExecutionContext.cs     call stack, scoping, musical context, PRNG
│   ├── StackFrame.cs           dictionary-backed variable/function scope
│   ├── Value.cs                CLR value + Flow type wrapper, factory methods
│   ├── MusicalContext.cs       tempo/timesig/key/swing/voicePool/tuning stack
│   ├── NoteStreamCompiler.cs   compiles | ... | into Sequence values
│   ├── ProgressionCompiler.cs  compiles chord progressions
│   ├── ModuleLoader.cs         resolves use "@stdlib" + use "relative/path"
│   ├── PrngRegistry.cs         per-callsite deterministic Random registry
│   ├── Thunk.cs                memoizing thunk for lazy evaluation
│   ├── FlowConfig.cs           ~/.config/flow/config.toml binding
│   ├── DictData.cs             generic Dict<K,V> backing storage
│   ├── MarkovModelData.cs      Markov chain model storage
│   └── LsystemModelData.cs     L-system model storage
├── StandardLibrary/
│   ├── BuiltInFunctions.cs           registers all C# builtins at startup
│   ├── InternalFunctionRegistry.cs   signature → lambda mapping
│   ├── StdLib.cs                     core I/O + arithmetic + control flow
│   ├── Collections/                  list operations
│   ├── Audio/
│   │   ├── SongRenderer.cs           sections → sequences → voices → buffer
│   │   ├── BarRenderer.cs            voice-block aware bar rendering
│   │   ├── SequenceRenderer.cs       sequence → audio buffer
│   │   ├── VoiceAllocator.cs         polyphony / voice stealing
│   │   ├── SampleCache.cs            bundled-sample lazy-load cache
│   │   ├── SampledInstrumentRenderer.cs  varispeed sample playback
│   │   ├── DSP/                      Reverb, Filter, Compressor, Delay,
│   │   │                             GranularEngine, StretchEngine,
│   │   │                             PitchShiftEngine, PhaseVocoder, Psola,
│   │   │                             Fft, Hps, Panner, WindowFunctions
│   │   ├── Synthesizers/             Piano, Brass, Sax, Bell, Flute, Organ,
│   │   │                             Strings, Drums, Wavetable
│   │   ├── Sfz/                      SFZ orchestral sampler (opt-in)
│   │   ├── Tuning/                   Scala .scl loader
│   │   ├── Vocalization/             vocal synthesis primitives
│   │   ├── MidiExport.cs             multi-track SMF emit (DryWetMidi)
│   │   └── FileIO.cs                 WAV read/write
│   ├── Harmony/                      ChordParser, ScaleDatabase, Voicings,
│   │                                 HarmonyFunctions (roman numerals)
│   ├── Transforms/                   transpose, invert, retrograde, etc.
│   ├── Patterns/                     13 Tidal-style combinators
│   ├── Generative/                   Markov, L-system, cellular, chaos
│   ├── Improv/                       jam + style registry
│   ├── Notation/                     MusicXML / LilyPond / ABC / MML IO
│   ├── Composition/                  timeline / voice / track helpers
│   └── TestFramework/                (test ...) builtin + snapshot/restore
├── Audio/
│   ├── AudioPlaybackManager.cs       backend lifecycle owner
│   ├── IAudioBackend.cs              backend abstraction
│   └── PulseAudioSimpleBackend.cs    PulseAudio simple API via P/Invoke
├── Diagnostics/
│   ├── ErrorReporter.cs              error accumulation (not throwing)
│   ├── FlowDiagnostic.cs / FlowError.cs
│   ├── DiagnosticRenderer.cs         Rust-style multi-line diagnostic emit
│   └── LevenshteinHelper.cs          "did you mean...?" suggestions
├── Samples/                          CC-BY 4.0 University of Iowa MIS bundle
├── improv/styles/                    shipped style packs (jazz/blues/classical)
└── *.flow                            stdlib source modules
```

The 12 stdlib `.flow` files in `flow-lang/` are loaded via `use "@name"`:
`std` (which auto-imports `@collections` and `@bars`), `collections`, `audio`,
`bars`, `notation`, `composition`, `patterns`, `generative`, `improv`, `sfz`,
`notation-io`, `test`. They're copied to the build output via the csproj's
`<None Update="..." CopyToOutputDirectory>` items.

## Key Subsystems

### Type system and overload resolution

Every Flow type extends the `FlowType` base class and implements three methods:
`IsCompatibleWith(other)`, `CanConvertTo(other)`, and `GetSpecificity()`. The
numeric widening chain is `Int → Long → Float → Double → Number` — narrower
types are more specific.

When a function is called, `OverloadResolver` scores each candidate against
the actual arguments:

| Match quality | Score |
|---------------|-------|
| Exact type match | +1000 |
| Compatible (e.g. `Decibel` accepts `Double`) | +500 |
| Convertible (numeric widening) | +100 |
| `Void` parameter (wildcard) | matches anything |

The highest-scoring overload wins. Ties are resolved deterministically by
declaration order. This system supports the 24 overloads of `pitchShift`
(3 cents-types × 8 arity steps) without ambiguity, while still letting
composers pass a bare `Double` where a `Decibel` is expected.

### Music-typed literals

Music types are the heart of Flow's expressive power. Each one has a custom
literal at the lexer level and a custom `FlowType` subclass with its own
compatibility rules:

| Literal | Type | Coerces from |
|---------|------|--------------|
| `-12dB` | `Decibel` | `Double`, `Float` |
| `100ms` | `Millisecond` | `Double`, `Float` |
| `2.5s` | `Second` | `Double`, `Float` |
| `+50c` | `Cent` | `Double`, `Float` |
| `+2st` | `Semitone` | `Int` (whole-number-by-design) |
| `440Hz`, `1.5kHz` | `Hertz` | `Double`, `Float` |
| `1.5` (Beat-tagged) | `Beat` | `Double`, `Float` |
| `#foo` | `Symbol` | strict (no numeric coercion) |
| `C4`, `F#5`, `Bb3` | `Note` | strict |
| `Cmaj7`, `Dm`, `F#dim` | `Chord` | strict |

The "strict" types refuse silent numeric coercion — `(equals #foo "foo")`
returns `false`, and a `Symbol` cannot be passed where a `Double` is expected.
This matters for dict keys, where reference identity is the contract.

### Musical context — a runtime stack

`Runtime/MusicalContext.cs` is the runtime equivalent of "what does this note
mean in this position?". A musical context tracks the active tempo, time
signature, key, swing factor, voice pool size, sustain pedal state, and
tuning system. It's organized as a stack: each `tempo 120 { ... }`,
`timesig 4/4 { ... }`, `key Cmajor { ... }`, `swing 0.6 { ... }`,
`voicePool 32 { ... }`, or `tuning t { ... }` block pushes a new frame; the
frame pops when the block ends. Lookups walk the stack top-down, so inner
blocks naturally override outer ones.

The context system is what lets note streams compile correctly at evaluation
time. A `| C4 D4 E4 |` literal in source has no notion of tempo or key — those
are resolved by `Runtime/NoteStreamCompiler.cs` against the active context
when the surrounding statement executes. The same source string produces a
different `Sequence` value in a `tempo 60` block vs. a `tempo 240` block.

### Standard library — C# builtins + Flow modules

The standard library is split between C# implementations and Flow source:

- **C# builtins** are registered into `InternalFunctionRegistry` at engine
  startup by `StandardLibrary/BuiltInFunctions.cs`. Each builtin has a
  `FunctionSignature` (name, parameter types, parameter names, varargs flag)
  and a C# lambda that consumes a `Value[]` and returns a `Value`.
- **Flow stdlib modules** (`.flow` files in `flow-lang/`) provide
  composer-friendly forward declarations, helper procs, and module activation
  gates. The `@sfz` and `@notation-io` modules are opt-in: importing them
  flips a runtime gate on `ExecutionContext` (`SfzEnabled`,
  `NotationIoEnabled`) that the corresponding builtins check on each call.

When `FlowEngine` starts up, it registers every C# builtin unconditionally —
the opt-in gating is purely runtime, controlled by the `use` statement.

### Audio pipeline

The end-to-end audio path for `renderSong` (and its `writeWav` consumer):

```
Song
  │
  ▼ SongRenderer.RenderSong
Section[] × instrument-name
  │
  ▼ per-section
Sequence[]                       voices ← per-track + per-bar voice blocks
  │
  ▼ SequenceRenderer + BarRenderer
Voice[] (with Pan, Velocity, Articulation)
  │
  ├── synth path                          ├── sample path                 ├── sampler:NAME path
  │     Synthesizers/PianoSynth, etc.    │   SampledInstrumentRenderer    │   SfzRenderer
  │     + SynthUtils.ArticulationADSR    │   + SampleCache                │   + SfzSampleCache
  │                                       │   + SamplePathArticMultipliers│
  ▼                                       ▼                                ▼
                          Buffer (mono or stereo)
                                          │
                                          ▼ DSP (reverb, filter, compressor, delay, granular, stretch, pitchShift, pan)
                                          │
                                          ▼ writeWav (FileIO.cs)
                                       .wav file
                                          │
                                          └── or → AudioPlaybackManager → IAudioBackend → PulseAudioSimpleBackend
```

The synth, sample, and SFZ paths coexist in `SongRenderer` and are selected
per-instrument via the instrument-name string passed to `renderSong`. The
synth path was the original Flow rendering engine; the sample path was added
in Phase 29 for bundled CC-BY 4.0 University of Iowa instrument samples; the
SFZ path was added in Phase 33 for the VSCO Community Edition library and
other external SFZ patches. All three apply Phase 28's locked articulation
envelope on top of their per-path rendering.

### Audio backend abstraction

`flow-lang/Audio/IAudioBackend.cs` is a small interface (initialize, write
buffer, stop, dispose). The only shipping implementation is
`PulseAudioSimpleBackend.cs`, which uses P/Invoke to call libpulse-simple
directly. The abstraction exists so future backends (ALSA, JACK, CoreAudio,
WASAPI) can be added without touching the playback callers — `play`, `loop`,
and `preview` go through `AudioPlaybackManager`, not directly to PulseAudio.

### Diagnostic reporting

`Diagnostics/ErrorReporter.cs` accumulates errors rather than throwing —
multiple errors can be reported per pass, and the engine decides what to do
with them. `DiagnosticRenderer.cs` produces Rust-style multi-line diagnostics
with source-line quotation (the `SourceMap` on `FlowEngine` keeps the
necessary file content cached). `LevenshteinHelper.cs` powers "did you mean
X?" suggestions for misspelled identifiers.

This pattern is what enables the charitable-interpretation policy: a builtin
can clamp a degenerate input AND emit a one-shot stderr advisory in the same
call, without aborting the render.

## Key Design Decisions

### `->` is a parse-time transform

The flow operator has no runtime representation. `x -> func(arg)` is rewritten
at parse time into `func(x, arg)`, which is then handled by the normal
function-call machinery. This means `->` chains have zero runtime overhead
relative to nested calls — they're literally the same AST.

The companion `~>` operator (tuple-unpack flow) cannot be a parse-time
transform because the unpacking depends on the runtime type of the LHS, so it
survives as a `TupleUnpackFlowExpression` AST node. On non-tuple LHS values
it falls through to single-arg `->` semantics.

### Implicit returns collect every non-Void expression

A proc body doesn't need an explicit `return`. The `ImplicitReturnCollector`
gathers every non-Void expression evaluated during the body. At exit:

- 0 collected → `Void`
- 1 collected → that value
- 2+ collected → an `Array` of all collected values

An explicit `return X` clears the collector and short-circuits with `X`. This
keeps simple procs as short as possible while still supporting multi-value
returns without tuples. There is no `null`, `Void`, `Nothing`, or `()` source
literal — `return` without an expression is a parse error.

### Charitable interpretation everywhere

When a composer passes `reverb(input, 5.0, 5.0, 5.0)` and the function
expects each parameter clamped to `[0, 1.0]`, Flow clamps silently and emits
a one-shot stderr advisory. When `markov` gets a corpus too short for the
requested order, it clamps the order and emits an advisory. When `lsystem`
gets an iteration count above 20, it clamps to 20 and emits an advisory.

The pattern is: never throw, always produce a usable result, always log the
adjustment once per process. This is the JavaScript-coercion philosophy
applied to music — the script will play SOMETHING reasonable; the composer
can tighten it later. The one-shot advisory ensures the stderr stream doesn't
become noise on a per-frame loop.

### Two-run cmp-clean deterministic rendering

Two consecutive renders of the same source at the same git SHA produce
byte-identical WAV and MIDI output. This is the "two-run cmp-clean"
determinism contract, enforced by CI regression baselines under
`flow-lang.Tests/baselines/`.

The mechanism for stochastic primitives is `Runtime/PrngRegistry.cs`: every
unseeded random call routes through a per-callsite, per-generator-name
`Random` instance keyed by `(SourceLocation, name)`. The seed is derived from
an FNV-1a 32-bit hash over the source location and the generator name, NOT
from `object.GetHashCode()` (which is per-process randomized in .NET). The
registry resets at every `renderSong` / `writeWav` boundary so PRNG state
doesn't accumulate across multiple renders in one process.

A source-grep CI gate enforces zero unsanctioned `new Random(` constructions
in `StandardLibrary/Patterns/`, `Generative/`, and `Improv/`. Exceptions
(explicit-seed paths where the seed is a required argument) are marked with
the `// PRNG-SANCTIONED:` comment.

### Cross-platform determinism caveat — chaos primitives

The `lorenz` and `logistic` chaos-map primitives are exempt from
cross-platform determinism. Chained floating-point arithmetic in forward-Euler
integration amplifies platform-specific FPU and `Math.*` quirks exponentially
after ~50 iterations. Same-platform two-run cmp-clean is preserved (verified
on Linux x64), but rendered output of Lorenz/logistic-driven music differs
across platforms.

This is a deliberate, documented limitation — the primitives are valuable
for music regardless of cross-platform byte-identity. Markov, L-system, and
cellular-automata primitives stay cross-platform deterministic because they
use integer arithmetic.

### Module imports execute in the caller's context

There is no module namespace isolation. `use "@audio"` evaluates `audio.flow`
in the importing script's `ExecutionContext`, so every proc and variable
declared there becomes visible to the caller with no qualified-name syntax.
Circular imports are detected by `ModuleLoader` and rejected. This design
matches the C-include / Lisp-load tradition more than the Python/JavaScript
module tradition, and it's deliberate — composers shouldn't have to think
about namespaces while writing music.

### Single-engine-per-process convention

`FlowEngine` exposes some static accessors (`CurrentSampleCache`,
`CurrentSfzSampleCache`, `CurrentExecutionContext`) used by the static
`SongRenderer` class. This is safe under the project convention that one
process owns one `FlowEngine`. The convention is documented in
`FlowEngine.cs` next to each static field. Concurrent-engine support (e.g.
for a server hosting multiple sessions) would require refactoring those
fields through `ExecutionContext` — the comments call this out as a
v1.5+ refactor target.

## Dependency Philosophy

Flow's NuGet manifest is intentionally short:

| Project | External dependency | Why |
|---------|--------------------|-----|
| `flow-lang` | `Melanchall.DryWetMidi` 8.0.3 | Standard MIDI Format is too tedious to hand-roll correctly (variable-length encoding, delta times, multi-track chunks, tempo maps). Library is actively maintained, .NET Standard 2.0, compatible with .NET 10. |
| `flow-lang` | `Pidgin` 3.5.1 | Historical — referenced but unused. A v1.5 cleanup target. |
| `flow-cli` | `System.CommandLine` 2.0.7 | Subcommand parsing + help generation. Standard Microsoft library. |
| `flow-cli` | `Tomlyn` 2.3.2 | Parses `~/.config/flow/config.toml`. |
| `flow-lsp` | `OmniSharp.Extensions.LanguageServer` 0.19.9 | LSP 3.17 protocol plumbing — same reasoning as DryWetMidi: not worth hand-rolling. Trimming is intentionally disabled because OmniSharp uses reflection. |

Everything else is hand-rolled in this repo: lexing, parsing, AST, type
system, interpreter, audio synthesis, all DSP (reverb, filters, compressor,
delay, granular, time-stretch, pitch-shift, phase vocoder, PSOLA, FFT,
windowing, panning), SFZ sampler, MusicXML emit, LilyPond emit, ABC parser,
MML parser, WAV read/write, PulseAudio P/Invoke, MIDI parser (for the
`midi2flow` import path — the export path uses DryWetMidi).

The reason is consistency, not ideology. Adding a library means accepting its
opinions about error reporting, threading, memory ownership, and update
cadence. For a music DSL where the whole point is composer-facing ergonomics,
each of those is a place where the library's design and Flow's design can
disagree. Hand-rolling means every diagnostic, every threading decision, and
every numeric tolerance matches the rest of the codebase.

## Where to Start

If you're new to the codebase, three concrete entry points:

1. **Read `FlowEngine.cs` end-to-end.** It's ~380 lines and shows the whole
   pipeline wiring in one file. Every other file is reachable from it.
2. **Run `dotnet test` and pick a failing or interesting test.** Tests in
   `flow-lang.Tests/` cover every subsystem; finding one that exercises the
   subsystem you want to learn is the fastest path to understanding it.
3. **Run `dotnet run --project flow-interpreter -- --watch some.flow`** and
   modify a Flow file. Watch mode reloads on every save and prints
   diagnostics — the live-reload loop is the fastest feedback cycle for
   understanding how source maps to behavior.

For composer-facing language reference, see [`README.md`](../README.md) and
[`FEATURES.md`](../FEATURES.md). For exhaustive per-phase implementation
detail, the agent-shaped `CLAUDE.md` at the repo root remains the deepest
reference.
