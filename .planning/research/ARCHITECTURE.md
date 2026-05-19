# Architecture Research — v1.5 Stage, Studio, Web

**Domain:** Brownfield additive integration into Flow interpreter (C# .NET 10) — language extensions, multi-platform backends, generative subsystems, web reach.
**Researched:** 2026-05-18
**Confidence:** HIGH (architecture is grounded in actual file inspection of `flow-lang/`, `flow-interpreter/`, `flow-lsp/`, `flow-cli/`, `flow-midi/`; speculative items — WASM playground, Ableton Link — clearly flagged MEDIUM/LOW where ground truth is external).

## System Overview

```
┌──────────────────────────────────────────────────────────────────────────────┐
│  Surface Projects (consumers of flow-lang)                                   │
│  ┌──────────────┐ ┌──────────┐ ┌──────────┐ ┌──────────────┐ ┌────────────┐  │
│  │ flow-cli     │ │ flow-    │ │ flow-lsp │ │ flow-midi    │ │ flow-doc   │  │
│  │ (11 cmds +   │ │ interp   │ │ (LSP svr)│ │ (MIDI⇄Flow)  │ │ (NEW v1.5) │  │
│  │ live, doc,   │ │ (REPL +  │ │          │ │              │ │            │  │
│  │ test NEW)    │ │ watch +  │ │          │ │              │ │ Phase 41   │  │
│  │              │ │ live NEW)│ │          │ │              │ │            │  │
│  └──────┬───────┘ └────┬─────┘ └────┬─────┘ └──────┬───────┘ └─────┬──────┘  │
│         │              │            │              │                │         │
├─────────┴──────────────┴────────────┴──────────────┴────────────────┴─────────┤
│  flow-lang  (Core/FlowEngine — pipeline orchestrator)                        │
│  ┌─────────────────────────────────────────────────────────────────────────┐ │
│  │  Source → Lexer → Parser → AST → Interpreter → Value                    │ │
│  │                                                                          │ │
│  │  Lexing/SimpleLexer ── adds: `match`/`live`/`as`/`test` keywords         │ │
│  │  Parsing/Parser     ── adds: match-expr, live-block, -> as name,        │ │
│  │                              parameterized-section args, ABC/MML import│ │
│  │  Ast/{Expr,Stmt}    ── NEW: MatchExpression, LiveBlockStatement,        │ │
│  │                              (FlowExpression extended w/ Binding?),    │ │
│  │                              (SectionDeclaration extended w/ Params)  │ │
│  │  Interpreter/ExprEval ── adds: match dispatch (irrefutable + guards),   │ │
│  │                                  granular/timestretch DSP evaluation   │ │
│  │  Runtime              ── extends: ExecutionContext (Link/JACK ticker,  │ │
│  │                          OSC server hook, AudioInputStream)            │ │
│  │  Diagnostics          ── Rust-style snippet renderer (span + caret)   │ │
│  │  TypeSystem           ── NEW: Pattern (compiled) — no new music type   │ │
│  │  StandardLibrary      ── NEW modules: @pattern, @generative, @osc,     │ │
│  │                          @midi-rt, @test, @audio-input, @musicxml,    │ │
│  │                          @lilypond, @abc, @mml                          │ │
│  └─────────────────────────────────────────────────────────────────────────┘ │
├──────────────────────────────────────────────────────────────────────────────┤
│  I/O Abstractions (Backend layer — Phase 41 cross-platform crunch)           │
│  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────────┐        │
│  │ IAudioBackend    │  │ IMidiBackend NEW │  │ IFileSystem NEW      │        │
│  │ (extend: Capture │  │ (parallel shape  │  │ (sample / .scl / .sfz│        │
│  │  + WebAudio impl │  │  to IAudioBacknd)│  │  reads abstracted    │        │
│  │  + WASAPI/CoreAud│  │                  │  │  for WASM)           │        │
│  └─────────┬────────┘  └─────────┬────────┘  └──────────┬───────────┘        │
│            │                     │                       │                    │
│   ┌────────┼─────────┐  ┌────────┼─────────┐   ┌─────────┼─────────┐         │
│   │ Pulse  │ WASAPI  │  │ ALSA-  │ WinMM   │   │ Disk    │ Browser │         │
│   │ Audio  │ Core-   │  │ Seq    │ CoreMIDI│   │ FS      │ FS / IDB│         │
│   │ Web-   │ Audio   │  │ Jack-  │ Web-    │   │ (existing)│ (WASM) │         │
│   │ Audio  │         │  │ MIDI   │ MIDI-API│   │         │         │         │
│   └────────┴─────────┘  └────────┴─────────┘   └─────────┴─────────┘         │
├──────────────────────────────────────────────────────────────────────────────┤
│  Network / Real-Time Transport (Phase 38 + Phase 40)                         │
│  ┌──────────────────┐  ┌──────────────────────────────────────────────────┐  │
│  │ OSC svr/client    │  │ Transport sync clock sources                     │  │
│  │ (UDP loopback +  │  │  - MIDI clock (24 PPQ; via IMidiBackend events) │  │
│  │  hot-bind, msg   │  │  - Ableton Link (UDP discovery + libabl_link)   │  │
│  │  pump → Flow CB) │  │  - JACK transport (libjack — Linux primary)     │  │
│  └──────────────────┘  └──────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────────────────┘
```

### Component Inventory — NEW vs MODIFIED vs UNTOUCHED

| Component | Status | What changes |
|-----------|--------|--------------|
| `Lexing/SimpleLexer` | **MODIFIED** | New keywords: `match` `live` `test` `as`; ABC/MML triggered via stdlib only (no lex change); span tracking already present via `SourceLocation` |
| `Parsing/Parser` | **MODIFIED** | `ParseMatchExpression`, `ParseLiveBlock`, `ParseSectionDeclaration` (add optional `(args)`), `ParseFlowChain` (extend `->` with optional `as name`) |
| `Ast/Expressions/MatchExpression.cs` | **NEW** | `record MatchExpression(Expression Scrutinee, List<MatchArm> Arms)` + `MatchArm(Pattern, Expression? Guard, Statement[] Body)` |
| `Ast/Expressions/FlowExpression.cs` | **MODIFIED** | Add `string? BindingName` for `expr -> func() as x` chain naming |
| `Ast/Statements/LiveBlockStatement.cs` | **NEW** | `record LiveBlockStatement(Expression QuantizeUnit, Statement[] Body)` — the auto-loop construct |
| `Ast/Statements/SectionDeclaration.cs` | **MODIFIED** | Add `List<Parameter>? Parameters` for `section verse(Int key) { ... }`; existing zero-arg form keeps backward compat |
| `Ast/Patterns/*.cs` | **NEW** | `Pattern` base + `LiteralPattern`, `IdentifierPattern` (binds), `WildcardPattern` (`_`), `ChordPattern` (`Cmaj7`), `NotePattern`, `TuplePattern`, `RangePattern` |
| `Interpreter/ExpressionEvaluator` | **MODIFIED** | New `EvaluateMatch` switch arm; pattern matching dispatch table; granular/time-stretch builtin hooks |
| `Diagnostics/ErrorReporter` | **MODIFIED** | Add `RenderSnippet(SourceLocation span, string source, int contextLines)` Rust-style renderer; existing `SourceLocation` already carries line+column |
| `Audio/IAudioBackend.cs` | **MODIFIED** | Add `StartCapture(...)`, `StopCapture()`, `OnSampleBlock` event for mic/line input |
| `Audio/PulseAudioSimpleBackend` | **MODIFIED** | Implement capture via `pa_simple_new(STREAM_RECORD, ...)` (mirror playback shape) |
| `Audio/WasapiBackend.cs` | **NEW** (Phase 41) | Windows playback+capture via `Windows.Media.Audio` or low-level COM |
| `Audio/CoreAudioBackend.cs` | **NEW** (Phase 41) | macOS playback+capture via `AVAudioEngine` P/Invoke or `AudioUnit` |
| `Audio/WebAudioBackend.cs` | **NEW** (Phase 41) | WASM JS-interop to `AudioWorklet`; capture via `getUserMedia` |
| `Audio/IMidiBackend.cs` | **NEW** (Phase 40) | `OpenPort`, `SendNoteOn`, `SendNoteOff`, `SendCC`, `SendClock`, `OnHotPlug` event |
| `Audio/AlsaSeqMidiBackend.cs`, `CoreMidiBackend.cs`, `WinMmMidiBackend.cs`, `WebMidiBackend.cs` | **NEW** (Phase 40 + 41) | Per-platform IMidiBackend impls |
| `Audio/AudioInputManager.cs` | **NEW** (Phase 38) | Wraps `IAudioBackend.StartCapture`; exposes ring-buffer + analysis (level, FFT) for Flow callbacks |
| `Audio/DSP/GranularSynth.cs` | **NEW** (Phase 37) | Sliding-window grain extraction + pitch/rate decoupling |
| `Audio/DSP/TimeStretchPitchShift.cs` | **NEW** (Phase 37) | Phase-vocoder OR WSOLA — independent time + pitch axes |
| `Audio/SongRenderer.cs` | **MODIFIED** | Voice gains `Pan` attribute (range -1.0..1.0); per-voice constant-power pan applied before mix; integrates Phase 37 stereo spread |
| `Audio/Sfz/SfzParser.cs` | **MODIFIED** (Phase 37) | Extend whitelist: `seq_position`, `seq_length` (round-robin), `lovel/hivel` velocity layers (already present — extend if not), `ampeg_*` per-articulation envelopes |
| `Audio/Sfz/SfzRenderer.cs` | **MODIFIED** (Phase 37) | Round-robin region selection; per-articulation envelope multiplier dict |
| `Audio/SampledInstrumentRenderer.cs` | **MODIFIED** (Phase 37) | Bundle: more flute samples (D5 timbre-crossover); ragtime piano warmth (eq/lpf chain); per-articulation envelope multipliers |
| `Runtime/ExecutionContext.cs` | **MODIFIED** | New: `IMidiBackend? MidiBackend`, `LinkTransport? Link`, `OscServer? OscServer`, `AudioInputManager? AudioInput`; passed through to stdlib callbacks |
| `Runtime/MusicalContext.cs` | **MODIFIED** | Phase 40: `Tempo` becomes Link-pollable (delegate or atomic-backed) when Link is active; cleanly degrades to local value when not |
| `Runtime/ParameterizedSectionCache.cs` | **NEW** (Phase 36) | Memoize section invocations keyed by `(name, ordered-arg-values)` to keep two-run determinism contract clean |
| `Runtime/PrngRegistry.cs` | **NEW** (Phase 36) | Seedable RNG pool keyed by `(call-site-id, generator-name)` so markov/lsystem/cellular/lorenz stay determinism-clean across runs |
| `StandardLibrary/Audio/Granular.cs` | **NEW** (Phase 37) | `granular(buf, grainSizeMs, density, pitch, scatter)` builtin → `GranularSynth` |
| `StandardLibrary/Audio/PanningFunctions.cs` | **MODIFIED** (Phase 37) | Existing `pan()` extended with `stereoSpread(voiceArray, width)` + per-instrument-pan musical-context block `pan -0.5 { ... }` |
| `StandardLibrary/Generative/*.cs` | **NEW** (Phase 36) | `MarkovChain.cs`, `LSystem.cs`, `CellularAutomaton.cs`, `LorenzAttractor.cs`, `ImprovEngine.cs` |
| `StandardLibrary/Patterns/*.cs` | **NEW** (Phase 36) | TidalCycles-style combinators (`fast`, `slow`, `rev`, `every`, `chop`, `striate`, `juxt`) operating on `Sequence` |
| `StandardLibrary/Test/AssertFunctions.cs` | **NEW** (Phase 35) | `assert`, `assertEq`, `assertApprox`, `expectError`, `test "name" { ... }`; module name `@test` |
| `StandardLibrary/Notation/MusicXmlExport.cs` | **NEW** (Phase 39) | Sequence/Song → MusicXML 4.0 emit |
| `StandardLibrary/Notation/LilyPondExport.cs` | **NEW** (Phase 39) | Sequence/Song → LilyPond `.ly` emit |
| `StandardLibrary/Notation/AbcImport.cs` | **NEW** (Phase 39) | ABC parser → Sequence value |
| `StandardLibrary/Notation/MmlImport.cs` | **NEW** (Phase 39) | MML parser → Sequence value |
| `StandardLibrary/Realtime/OscServer.cs`, `OscClient.cs` | **NEW** (Phase 38) | UDP loopback OSC 1.0 (no bundles initially); message → `Value` Flow callback |
| `StandardLibrary/Realtime/MidiRt.cs` | **NEW** (Phase 40) | `midiOut(port, msg)`, `onMidi(port, callback)`, `midiClockSync(bpm)`, `linkSync()`, `jackSync()` |
| `flow-interpreter/Repl.cs` | **MODIFIED** | LSP-in-process: embed `flow-lsp` library, call its completion engine on `Tab`; pretty piano-roll renders via `BufferPrinter` (already exists) |
| `flow-interpreter/LiveReloadManager.cs` | **MODIFIED** | Receives optional `live { ... }` AST node from rendered program → uses its quantize-unit instead of inferred bar boundary; otherwise unchanged |
| `flow-cli/Commands/LiveCommand.cs` | **NEW** | `flow live <file>` — wraps LiveReloadManager (modernized watch) with explicit `live { ... }` semantics; supersedes `flow watch` as the recommended live-coding entry |
| `flow-cli/Commands/TestCommand.cs` | **NEW** | `flow test [path]` — discovers `*.test.flow` files, runs each, reports pass/fail; thin wrapper over the `@test` stdlib's runner |
| `flow-cli/Commands/DocCommand.cs` | **NEW** | `flow doc [out-dir]` — emits HTML/MD from BuiltInDocs (104 entries) + proc signatures + `//` comments |
| `flow-doc/` (project) | **NEW or in-CLI** | Doc-generator library — recommendation: **as a flow-cli subcommand** with a small `flow-doc-lib` referenced from `flow-cli/`; standalone project only if a third consumer emerges |
| `flow-jetbrains/` | **UNTOUCHED CODE; PROCESS CHANGE** | Marketplace publish requires: signing key in CI, version bump, screenshots, marketplace listing — no functional code change |
| `flow-wasm/` (project) | **NEW** (Phase 41) | `dotnet workload install wasm-tools` + `Microsoft.NET.Sdk.WebAssembly` SDK; renders FlowEngine inside browser; depends on `WebAudioBackend` + `WebFsAdapter` |

## Recommended Project Structure (Post-v1.5)

```
flow-sharp/
├── flow-lang/                              # core interpreter library
│   ├── Ast/
│   │   ├── Expressions/
│   │   │   ├── MatchExpression.cs          # NEW Phase 35
│   │   │   └── ... (FlowExpression extended with BindingName)
│   │   ├── Patterns/                       # NEW Phase 35 — pattern AST nodes
│   │   │   ├── Pattern.cs                  # abstract base
│   │   │   ├── LiteralPattern.cs
│   │   │   ├── IdentifierPattern.cs        # irrefutable, binds
│   │   │   ├── WildcardPattern.cs          # _
│   │   │   ├── TuplePattern.cs             # <<a, b>>
│   │   │   ├── NotePattern.cs              # C4 specific pitch
│   │   │   ├── ChordPattern.cs             # Cmaj7
│   │   │   └── RangePattern.cs             # 0..10
│   │   └── Statements/
│   │       └── LiveBlockStatement.cs       # NEW Phase 38
│   ├── Audio/
│   │   ├── IAudioBackend.cs                # MODIFIED — adds StartCapture
│   │   ├── IMidiBackend.cs                 # NEW Phase 40
│   │   ├── AudioInputManager.cs            # NEW Phase 38
│   │   ├── Backends/                       # NEW folder (Phase 41 polish)
│   │   │   ├── PulseAudioSimpleBackend.cs  # MOVED from flat
│   │   │   ├── WasapiBackend.cs            # NEW Phase 41
│   │   │   ├── CoreAudioBackend.cs         # NEW Phase 41
│   │   │   ├── WebAudioBackend.cs          # NEW Phase 41
│   │   │   ├── AlsaSeqMidiBackend.cs       # NEW Phase 40
│   │   │   ├── CoreMidiBackend.cs          # NEW Phase 40+41
│   │   │   ├── WinMmMidiBackend.cs         # NEW Phase 40+41
│   │   │   └── WebMidiBackend.cs           # NEW Phase 41
│   │   └── Transport/                      # NEW folder (Phase 40)
│   │       ├── MidiClock.cs
│   │       ├── AbletonLink.cs              # P/Invoke to libabl_link
│   │       └── JackTransport.cs            # P/Invoke to libjack
│   ├── Diagnostics/
│   │   ├── ErrorReporter.cs                # MODIFIED — Rust-style snippets
│   │   └── SnippetRenderer.cs              # NEW Phase 35
│   ├── Runtime/
│   │   ├── ExecutionContext.cs             # MODIFIED — Midi/Osc/Link/Input
│   │   ├── MusicalContext.cs               # MODIFIED — Link-aware Tempo
│   │   ├── ParameterizedSectionCache.cs    # NEW Phase 36
│   │   └── PrngRegistry.cs                 # NEW Phase 36 — determinism
│   ├── StandardLibrary/
│   │   ├── Audio/
│   │   │   ├── DSP/
│   │   │   │   ├── GranularSynth.cs        # NEW Phase 37
│   │   │   │   └── TimeStretchPitchShift.cs# NEW Phase 37
│   │   │   ├── PanningFunctions.cs         # MODIFIED Phase 37
│   │   │   ├── SongRenderer.cs             # MODIFIED Phase 37 — voice.Pan
│   │   │   └── Sfz/                        # MODIFIED Phase 37 — RR + ampeg
│   │   ├── Generative/                     # NEW folder Phase 36
│   │   │   ├── MarkovChain.cs
│   │   │   ├── LSystem.cs
│   │   │   ├── CellularAutomaton.cs
│   │   │   ├── LorenzAttractor.cs
│   │   │   └── ImprovEngine.cs
│   │   ├── Patterns/                       # NEW folder Phase 36
│   │   │   └── PatternAlgebra.cs           # Tidal-style combinators
│   │   ├── Realtime/                       # NEW folder Phase 38 + 40
│   │   │   ├── OscServer.cs
│   │   │   ├── OscClient.cs
│   │   │   └── MidiRt.cs
│   │   ├── Notation/                       # NEW folder Phase 39
│   │   │   ├── MusicXmlExport.cs
│   │   │   ├── LilyPondExport.cs
│   │   │   ├── AbcImport.cs
│   │   │   └── MmlImport.cs
│   │   └── Test/                           # NEW folder Phase 35
│   │       └── AssertFunctions.cs
│   ├── generative.flow                     # NEW Phase 36 stdlib
│   ├── patterns.flow                       # NEW Phase 36 stdlib
│   ├── osc.flow                            # NEW Phase 38 stdlib
│   ├── midi-rt.flow                        # NEW Phase 40 stdlib
│   ├── test.flow                           # already exists — extended Phase 35
│   ├── audio-input.flow                    # NEW Phase 38 stdlib
│   ├── musicxml.flow / lilypond.flow / abc.flow / mml.flow  # NEW Phase 39
│   └── ... (existing modules untouched)
├── flow-cli/                               # CLI subcommands
│   └── Commands/
│       ├── LiveCommand.cs                  # NEW Phase 38
│       ├── TestCommand.cs                  # NEW Phase 35
│       └── DocCommand.cs                   # NEW Phase 41
├── flow-interpreter/
│   ├── Repl.cs                             # MODIFIED Phase 38 — LSP completion
│   └── LiveReloadManager.cs                # MODIFIED Phase 38 — live{} aware
├── flow-lsp/                               # in-process bridge target Phase 38
│   └── Handlers/CompletionHandler.cs       # already exists; expose engine for REPL
├── flow-wasm/                              # NEW project Phase 41
│   ├── flow-wasm.csproj                    # WebAssembly SDK
│   ├── Program.cs                          # main entry; constructs FlowEngine
│   ├── JsInterop/                          # WebAudio + getUserMedia + fetch
│   └── wwwroot/                            # static playground page
├── flow-doc/ OR (flow-cli/DocLib/)         # NEW Phase 41 — pick one
└── ... (existing projects untouched: flow-midi, flow-jetbrains, vscode-extension)
```

### Structure Rationale

- **`Audio/Backends/` folder:** four playback backends + four MIDI backends justify a folder; flat `Audio/` directory was fine at one backend but becomes noise at eight.
- **`Audio/Transport/`:** Link / JACK / MIDI-clock are conceptually different from audio backends — they're tempo sources, not sample sinks. Separate folder keeps the abstraction story clean.
- **`StandardLibrary/Realtime/`:** OSC + real-time MIDI share a "network/IPC event pump" implementation pattern, distinct from offline DSP.
- **`StandardLibrary/Notation/`:** Import and export share parser/emit infrastructure (e.g., a Note → glyph table). Co-locate.
- **`Ast/Patterns/`:** Pattern AST nodes are conceptually distinct from Expressions/Statements (they're consumed by `MatchExpression`, never by the main evaluator dispatch). Keeps the existing Expression/Statement bucket tidy.
- **`flow-wasm/` as a separate project** rather than a target in `flow-cli`: WebAssembly SDK pulls in different MSBuild props; cleaner to isolate so the desktop build stays uncoupled from WASM workload installs.
- **`flow-doc` recommendation: subcommand, NOT separate project.** The doc generator has one consumer (the `flow doc` command) and reads BuiltInDocs (already in `flow-lang`). A separate project would add a third assembly with no payoff. If JetBrains plugin or VSCode extension later needs to consume the generated docs programmatically, **then** spin it out.

## Architectural Patterns

### Pattern 1: Backend Abstraction by Interface + Per-Platform Impl

**What:** `IAudioBackend` already exists (PulseAudio impl). Same pattern repeats for `IMidiBackend` (Phase 40) and gets extended for capture + new platforms (Phase 41).

**When to use:** Whenever the underlying OS API differs across platforms but the language-facing semantics are uniform.

**Trade-offs:**
- (+) Single abstraction for stdlib to target; new platform = new file, no callsite churn.
- (+) WebAssembly fits naturally — it's just another impl (`WebAudioBackend : IAudioBackend`).
- (−) Capture must extend the interface — be careful not to force every backend to implement features no platform supports universally (e.g., low-latency capture on WebAudio's AudioWorklet is fine; on WinMM it requires WASAPI shared-mode trickery).

**Example shape (Phase 40 IMidiBackend):**
```csharp
public interface IMidiBackend : IDisposable
{
    bool Initialize();
    IReadOnlyList<string> GetInputPorts();
    IReadOnlyList<string> GetOutputPorts();
    bool OpenOutput(string port);
    bool OpenInput(string port);
    void SendNoteOn(int channel, int pitch, int velocity);
    void SendNoteOff(int channel, int pitch);
    void SendCC(int channel, int cc, int value);
    void SendClock();           // one 24-PPQ pulse
    void SendStart(); void SendStop(); void SendContinue();
    event Action<MidiMessage>? OnMessage;
    event Action<MidiPortEvent>? OnHotPlug;
    string Name { get; }
}
```

### Pattern 2: Stdlib Module = .flow file + matching C# folder

**What:** Existing convention — `flow-lang/audio.flow` declares Flow procs that wrap C# builtins registered in `flow-lang/StandardLibrary/Audio/`. The `.flow` file is the Flow-visible API surface; the C# folder is the implementation.

**When to use:** Every new stdlib module (Phases 36, 38, 39, 40 all add modules).

**Trade-offs:**
- (+) Flow-side procs can compose multiple C# builtins, add defaults, document.
- (+) Module loader (`@name` resolution) already handles this — zero new infrastructure.
- (−) Two files to maintain per module — discipline required to keep the `.flow` declaration in sync with the registered C# signature.

### Pattern 3: Musical-Context Stack for Scoped Behavior

**What:** `MusicalContext` push/pop pattern (Phase 1+) is the canonical way to scope state in Flow. Phase 28 added `voicePool`; Phase 32 added `tuning`. Phase 36 parameterized sections **must** play nicely with this stack (section args are scoped, not global).

**When to use:** Any feature whose value should be inherited by nested blocks and override-able.

**Trade-offs:**
- (+) Composable nesting: `tempo 120 { key Cmajor { tuning t { ... } } }` Just Works.
- (+) Phase 40 Ableton Link can override `Tempo` at the network layer without changing call sites — the stack reads the network-provided tempo when the Link frame is active.
- (−) Network-driven mutation of a context value (Phase 40 Link) is a new pattern; the stack so far has been pure pushdown.

### Pattern 4: Determinism via Seeded RNG Registry

**What:** v1.2 established "two-run cmp-clean" — consecutive runs produce byte-identical WAV. Phase 36's markov/lsystem/cellular/lorenz primitives MUST seed from a deterministic source. Recommendation: `PrngRegistry` in `Runtime/` keyed by `(call-site-SourceLocation, generator-name)`.

**When to use:** Every new generative primitive.

**Trade-offs:**
- (+) Keeps Phase 18/25/27/33 determinism contract intact.
- (+) Composer can explicitly seed via existing convention (`humanize seed:42`).
- (−) Call-site keys are SourceLocation-dependent — refactoring a script changes seeds. This is acceptable per established Phase 25 humanizeGaussian convention.

### Pattern 5: Capture Mode for Headless Render (existing — Phase 38 reuse)

**What:** `AudioPlaybackManager.CaptureMode = true` makes audio operations buffer-to-memory instead of streaming-to-backend. Used by `LiveReloadManager` already.

**When to use:**
- WASM playground (no PulseAudio available; render to memory then play via WebAudio).
- Test framework (Phase 35 — capture audio output, compare buffers via RMS).
- `flow render` CLI command (already uses it).

**Trade-offs:**
- (+) Single existing mechanism scales to four new use cases.
- (−) None — well-trodden.

## Data Flow

### v1.5 New Data Flow #1: Pattern Matching (Phase 35)

```
Source: match note { C4 => ... ; Note n => ... ; _ => ... }
   ↓
Lexer: TokenMatch + braces
   ↓
Parser → MatchExpression(scrutinee, [MatchArm(pat, guard?, body)*])
   ↓
ExpressionEvaluator.EvaluateMatch:
   1. Evaluate scrutinee → Value
   2. For each arm in order:
      a. TryBindPattern(arm.pattern, value, bindings) → bool
      b. If matched and (guard?.Evaluate(bindings + ctx) == true):
         - Push new StackFrame with bindings
         - Execute body
         - Return ImplicitReturnCollector's value
         - Pop frame
   3. If no arm matched: emit error via ErrorReporter (NOT throw — soft fail per existing model)
```

### v1.5 New Data Flow #2: Live Block + Quantized Hot-Swap (Phase 38)

```
File on disk: live 1bar { ... }
   ↓
flow-cli/LiveCommand → flow-interpreter/LiveReloadManager
   ↓
LiveReloadManager.Run():
   - Initial FlowEngine.Execute(source) in capture mode
   - Walk AST for LiveBlockStatement; extract QuantizeUnit (1bar / 2bar / 0.5beat ...)
   - Set FileSystemWatcher on source file
   - Start streaming playback loop
   ↓
[File changes]
   ↓
Background FlowEngine renders new version in capture mode → AudioBuffer
   ↓
Streaming loop: at NEXT QuantizeUnit boundary, atomically swap _currentBuffer
   - Existing 64-sample equal-power crossfade applied (already in LiveReloadManager)
   - Pending MusicalContext (tempo + timesig) overwrites current
   ↓
Playback continues seamlessly
```

**Key difference from existing watch mode:** the `live { ... }` block becomes the **explicit, in-source declaration** of "this is hot-swappable" — currently `--watch` infers a bar boundary from MusicalContext.TimeSignature. With `live`, the composer chooses quantize granularity per script.

### v1.5 New Data Flow #3: Real-Time MIDI Output (Phase 40)

```
Flow: midiOut #port_a (noteOn 60 100)
   ↓
StandardLibrary/Realtime/MidiRt — registered builtin
   ↓
ExecutionContext.MidiBackend!.SendNoteOn(0, 60, 100)
   ↓
[Platform-specific impl]
   - Linux: ALSA snd_seq_event_output
   - macOS: MIDIPacketListAdd + MIDISend
   - Windows: midiOutShortMsg
   - WASM: navigator.requestMIDIAccess().outputs[port].send(...)
```

### v1.5 New Data Flow #4: Ableton Link Tempo Slave (Phase 40)

```
Init: linkSync()
   ↓
ExecutionContext.Link = new LinkTransport()
LinkTransport spawns background thread that polls libabl_link's session_tempo
   ↓
MusicalContext.Tempo accessor (modified):
   - If ExecutionContext.Link != null && Link.IsActive:
     return Link.SessionTempo()
   - Else: return _localTempo  (existing path)
   ↓
Songs render with network-synced tempo
   ↓
Tempo changes propagate via the same accessor — no audio-thread mutation
```

### v1.5 New Data Flow #5: WASM Playground (Phase 41)

```
Browser: user types Flow source in textarea, clicks Play
   ↓
flow-wasm/Program.cs:
   - Read source via JS interop string passing
   - Construct FlowEngine (with WebAudioBackend instead of Pulse)
   - Execute in capture mode
   ↓
WebAudioBackend.Play():
   - JS interop: window.audioPlayer.queue(samples)
   - AudioWorklet pulls from queue
   ↓
Output through browser's WebAudio graph
```

**Hard constraints for WASM:**
- No P/Invoke (rules out PulseAudio, ALSA, libjack, libabl_link).
- No `System.IO.File` directly — use `IFileSystem` abstraction → either bundled-as-blob (sample bundle) or `fetch()` + IndexedDB cache.
- No threads in some configurations — use single-threaded async patterns where possible. (Note: .NET 10 WASM does support multithreading with the right flags — verify before committing.)

## Build Order (Phase Dependencies)

```
Phase 35 (Language Foundation) ─┬─→ Phase 36 (Pattern Algebra / Generative)
                                ├─→ Phase 37 (Sound Design)
                                ├─→ Phase 38 (Live 2.0)
                                ├─→ Phase 39 (Notation)
                                ├─→ Phase 40 (Studio Sync)
                                └─→ Phase 41 (Web / Distribution)

Phase 38 (Live 2.0 — modernized watch mode) ───→ Phase 41 (WASM playground = live in browser)

Phase 35's match + diagnostics + test framework feed EVERY later phase:
   - match: pattern-algebra branches in Phase 36, MIDI event dispatch in Phase 40
   - Rust-style diagnostics: every parser extension in 36/38/39 benefits
   - test framework: every later phase ships regression tests via @test
```

### Build-Order Justification

- **Phase 35 absolutely first.** Pattern matching is used in MIDI event dispatch (Phase 40 `onMidi #port { case (noteOn n v) => ...; case (cc n v) => ... }`), in pattern-algebra branches (Phase 36), and in import/export emit (Phase 39 — "match the note's articulation and emit the LilyPond glyph"). Rust-style diagnostics improve every later parser change. The test framework lets every later phase land regression tests.
- **Phase 36 can land before Phase 37** but they're commutative — neither blocks the other.
- **Phase 38 must precede Phase 41.** The WASM playground IS a watch-mode-in-browser; the modernized watch + `live` block is the cross-platform contract that Phase 41 implements in the browser.
- **Phase 39 can land any time after Phase 35** — it's standalone.
- **Phase 40 must precede Phase 41's MIDI piece.** Web MIDI API is implemented as just another `IMidiBackend`, so the abstraction must exist first.
- **Phase 41 last.** Cross-platform binaries need every other phase's I/O abstractions to be backend-clean.

### Recommended Sub-Order Within Phases (where it matters)

- **Phase 35:** diagnostics-snippet renderer **first** (improves error reporting for everything else), then test framework (gives regression bed), then `match` (the big one), then `-> as name` (smallest).
- **Phase 36:** pattern algebra **first** (foundation for the generative primitives, which slot in as members of the algebra), then markov + lsystem + cellular + lorenz, then improv (composes the others), then parameterized sections (independent — could go first).
- **Phase 37:** sampler polish first (low-risk Phase 33 extensions); granular + time-stretch + stereo pan independently.
- **Phase 38:** modernized watch + `live` block **first** (used by REPL polish); REPL polish; audio input; OSC.
- **Phase 39:** MusicXML export first (most-requested per industry); LilyPond export; ABC + MML import.
- **Phase 40:** `IMidiBackend` Linux first (project's primary platform); MIDI clock; Link; JACK.
- **Phase 41:** `flow doc` first (purely additive, no platform dependency); WASM playground; cross-platform binaries; JetBrains marketplace publish (process); third-genre showcase last (consumes everything).

## Integration Points — Cross-Cutting

### SFZ sampler (Phase 33 ↔ Phase 37)

`SfzParser` opcode whitelist extends with `seq_position`, `seq_length`, `ampeg_attack/decay/sustain/release`. `SfzRenderer.SelectRegion` gains round-robin state (per-region counter keyed by note+vel bucket). The Phase 28 articulation envelope hook in `SfzRenderer` gains a per-articulation multiplier dict so staccato on a sampled patch has different envelope params than on the synth path. **Risk:** sample-cache key changes — keep the existing `(absPath, midiPitch, articulation)` shape, add a `velocityLayer` field. Two-run determinism remains preserved by sort-key-ordered eager-load.

### Voice pool (Phase 28 ↔ Phase 37 stereo pan)

`SongRenderer` allocates one buffer per voice, mixes additively. Phase 37 stereo pan adds a per-voice `Pan` field to `Voice` (in `flow-lang/StandardLibrary/Audio/Voice.cs`). Constant-power pan applied per-voice BEFORE the additive mix: `mono_sample * cos(angle) → L`, `mono_sample * sin(angle) → R`. **Risk:** existing `Voice` is currently mono — promote to stereo at the mix-down step. The existing `mix()` builtin already handles mono→stereo promotion (v1.1) so the pipeline tolerates this. The voice-pool steal-oldest tiebreaker (Phase 28 deterministic by original index) is **unaffected** — pan is an attribute, not a selection key.

### Musical-context stack (Phase 36 ↔ Phase 40)

Phase 36 parameterized sections push a synthetic context frame on call (binding section parameters as scoped vars in the StackFrame, NOT in MusicalContext). Phase 40 Link/JACK **read from** the MusicalContext.Tempo accessor — the MusicalContext becomes a tempo-aware view (delegating to Link when active). **No write-back from the network into the stack** — keep mutation one-directional (Flow source → stack → playback; network → MusicalContext accessor → playback).

### Lexer/Parser (Phase 35 + 36 + 38)

New reserved keywords: `match`, `live`, `test`, `as` (already a contextual word — check existing `as` usage; if any conflict, scope it to flow chains only). Section args use existing `(` `)` tokens — minimal lex change, parser-level change in `ParseSectionDeclaration`. The `-> as name` flow extension: `as` is consumed only in `ParseFlowChain` context, not at statement-top — keeps it from polluting expression grammar.

### OverloadResolver (Phase 35 pattern matching)

**Pattern matching is orthogonal to function dispatch.** `match` is an expression, not a function call. The OverloadResolver does NOT participate in pattern matching. Pattern arms are resolved purely structurally (literal-equals, type-tag-equals, sub-pattern recurse). This keeps the existing specificity-scoring untouched.

### Dither RNG + Determinism Contract (Phase 36 generative ↔ existing)

Phase 36 generative primitives MUST seed from `PrngRegistry` (new). Default seeding strategy: hash of `(call-site SourceLocation, generator name, optional user seed)`. Composer can override via existing `seed: N` named-argument convention. The synth white-noise RNG + TPDF dither RNG already reseed at `renderSong`/`writeWav` boundaries (v1.2 Phase 15 Plan 05) — Phase 36 RNGs MUST reseed at the same boundaries. **Verification:** add a Phase 36 two-run cmp-clean test on a script using each generative primitive.

## Anti-Patterns to Avoid

### Anti-Pattern 1: Lifting Platform-Specific Code Out of IAudioBackend

**What people do:** Audio capture is "harder" on Linux than Windows, so they put the capture pipeline in `flow-cli/` or `flow-interpreter/` as platform-detection switches.

**Why it's wrong:** Breaks the WASM browser story. The capture pipeline must live inside `IAudioBackend` so the WASM backend can implement it via `getUserMedia` without touching CLI code. Same lesson v1.0 learned with PulseAudio.

**Do this instead:** Extend `IAudioBackend` with capture methods; each platform-specific backend implements them. The browser playground gets capture support transparently.

### Anti-Pattern 2: Hardcoding Tempo at Render Time

**What people do:** Phase 40 Link integration polls libabl_link at render-start, captures tempo as a constant, renders the whole song.

**Why it's wrong:** Link's whole point is that tempo CHANGES mid-session. A statically-rendered song doesn't slave to the network.

**Do this instead:** `MusicalContext.Tempo` accessor delegates to Link when active. The audio rendering loop is already chunk-based via `WriteChunk` — read tempo per chunk, not per render.

### Anti-Pattern 3: Adding a Sixth AST "Pattern Node Type" Inside Expressions

**What people do:** Add `MatchPattern` as a sibling of `LiteralExpression` in `Ast/Expressions/`.

**Why it's wrong:** Patterns aren't expressions — they don't evaluate to a Value, they bind names and return boolean match status. Mixing them pollutes the Expression hierarchy and breaks the ExpressionEvaluator's exhaustive switch.

**Do this instead:** Separate `Ast/Patterns/` folder with its own base class. `MatchExpression` (an Expression) contains `MatchArm[]`, each carrying a `Pattern`. Pattern evaluation is a separate dispatch table.

### Anti-Pattern 4: One-Off OSC/MIDI Threads

**What people do:** OSC server spawns its own thread; MIDI input spawns its own thread; AudioInput spawns its own thread. Each manages its own lifetime.

**Why it's wrong:** Three threads + shared mutable state in callbacks → race conditions. Currently the project has audio-thread-only mutation (one PulseAudio writer thread). Adding three more uncoordinated threads invites bugs that won't show up until a live performance.

**Do this instead:** One **event pump** in `Runtime/` that owns the network/MIDI/audio-input threads and marshals callbacks onto the Flow execution thread (or a dedicated "stdlib callback" thread with explicit lock-free queues to the audio thread). This mirrors what AudioPlaybackManager already does for playback.

### Anti-Pattern 5: WASM Build as a Target Profile of flow-cli

**What people do:** Add `<TargetFramework>net10.0;net10.0-browser</TargetFramework>` to flow-cli.csproj.

**Why it's wrong:** Browser target needs `Microsoft.NET.Sdk.WebAssembly`, not `Microsoft.NET.Sdk` — different SDK, not a target framework flip. Pulls in conflicting build properties.

**Do this instead:** Separate `flow-wasm/` project with its own SDK. Reference `flow-lang/` directly (it's a library — works on both). Backend-specific code (PulseAudio P/Invoke) is wrapped in `#if !BROWSER` or — preferred — isolated in `IAudioBackend` impls that flow-wasm just doesn't reference.

### Anti-Pattern 6: Test Framework as Subprocess Per Test

**What people do:** `flow test` shells out to `flow run test1.flow`, `flow run test2.flow`, etc., one process per test.

**Why it's wrong:** 70+ tests × ~1s startup = 70s overhead. Slow feedback discourages testing.

**Do this instead:** `flow test` constructs ONE FlowEngine, runs each `*.test.flow` script as a separate `Execute()` call in capture mode, captures pass/fail via the `@test` stdlib's accumulator. Reset musical-context stack + PrngRegistry + ExecutionContext bindings between tests for isolation. Total run time: O(actual work) not O(process spawn).

## REPL ↔ LSP Integration (Phase 38)

**Recommendation:** Embed `flow-lsp` as an in-process library reference from `flow-interpreter/Repl.cs`. The LSP project already references `flow-lang` directly (no shadow language model — v1.2 design choice) and its handlers (`CompletionHandler.cs`, etc.) are callable C# methods.

**Wiring:**
1. Add `flow-lsp` ProjectReference to `flow-interpreter.csproj`.
2. Repl owns a `DocumentManager` (already in flow-lsp) seeded with the REPL's accumulated source.
3. On `Tab` press: invoke `CompletionHandler.Handle(currentLine, cursorPos)` directly — no LSP RPC, no stdio, no JSON-RPC frame parsing.
4. Render completions to console via a simple list UI (ANSI menu).

**Rejected alternative:** running flow-lsp as a child process and talking JSON-RPC. Adds process management, latency, and IO complexity for zero benefit since both projects live in the same repo.

**Trade-off:** The REPL's binary grows by ~the size of flow-lsp's compiled assembly + OmniSharp.Extensions.LanguageServer (notable — ~5 MB). Acceptable for `flow repl`; if it bloats `flow run`, gate the LSP reference behind a `--rich-repl` flag or split flow-interpreter into `flow-interpreter-core` + `flow-interpreter-repl`.

## flow doc — Recommended Shape

**Recommendation:** `flow doc` is a `flow-cli` subcommand (`flow-cli/Commands/DocCommand.cs`). Its implementation lives in a small `flow-cli/Doc/` folder, NOT a separate project, because:
1. Only one consumer (the command itself).
2. The doc generator reads `BuiltInDocs` (already in `flow-lang`) + proc declarations (parses via `flow-lang.Parser`) + Flow `//` comments — all data is already in `flow-lang`.
3. A separate project adds one more `.csproj`, one more assembly to load, with no payoff.

**Output:** emit HTML + Markdown. HTML for the WASM playground (Phase 41 — sidebar reference); Markdown for GitHub wiki sync (existing scripts/ workflow).

**Spinoff trigger:** if the JetBrains plugin or VSCode extension wants programmatic access to docs at runtime (rather than baking docs at build time), extract `flow-cli/Doc/` to a `flow-doc/` library project. NOT before.

## JetBrains Marketplace Publish — Code Changes Required

The plugin scaffolding ships in v1.4 Phase 31. Marketplace publish requires (Phase 41):

**Code changes (small):**
- `flow-jetbrains/plugin.xml` — set production `<vendor>` block + URL + email.
- `flow-jetbrains/build.gradle.kts` — add `signPlugin` task with cert paths from env vars; add `publishPlugin` task.
- Add `flow-jetbrains/CHANGELOG.md` (marketplace renders this).
- Bump `version` to a stable semver (currently scaffolding likely sits at `0.0.1` or similar).

**Process (one-time):**
- JetBrains Marketplace account; vendor verification.
- Plugin signing cert (free for OSS) or self-signed for initial upload.
- First-publish goes through manual review (~3-5 business days).
- Subsequent updates auto-publish unless they touch sensitive APIs.

**No new C# code needed** — the LSP server bundled with the plugin is already buildable from v1.4 Phase 31 deliverables.

## Scaling Considerations

| Scenario | Architecture Adjustments |
|----------|--------------------------|
| Single user, single script, <30s render | No change — current design |
| Single user, live coding 30+ min session | LiveReloadManager already designed for this; verify `PrngRegistry` doesn't accumulate state across hot-reloads (reset on swap) |
| 10K-step generative score | Phase 36 markov/lsystem must stream — don't buffer the full sequence. Yield per-bar. |
| Network MIDI to 5+ external devices | One `IMidiBackend` instance, one open port — multiplex via channel field. No multi-backend orchestration. |
| Browser playground, 100 KB script | WebAssembly runtime supports it; main risk is sample-bundle blob size (3 MB in Phase 29) — gate sampled instruments behind on-demand load. |
| Ableton Link session w/ 4 peers | libabl_link handles peer discovery; Flow's only job is reading the tempo accessor. No new architecture. |

## Sources

- Existing codebase inspection:
  - `flow-lang/Audio/IAudioBackend.cs` — current 8-method interface, drives Phase 41 capture extension
  - `flow-lang/Audio/PulseAudioSimpleBackend.cs` — implementation pattern for new backends
  - `flow-interpreter/LiveReloadManager.cs` — existing watch+swap+crossfade pipeline (377 LOC); Phase 38 extends, doesn't replace
  - `flow-lang/Core/FlowEngine.cs` — orchestrator entry; `CurrentExecutionContext` static accessor pattern (v1.4 Phase 33) extends to MIDI/Link/OSC in v1.5
  - `flow-lang/Ast/{Expressions,Statements}/` — 16 expression types, 14 statement types — new nodes slot in without breaking the immutable-record pattern
  - `flow-lang/StandardLibrary/Audio/{DSP,Synthesizers,Sfz,Tuning}/` — module organization template for new Phase 36/37 folders
  - `flow-lsp/Handlers/CompletionHandler.cs` — direct-method-call surface for in-process REPL bridge
- Project documentation:
  - `CLAUDE.md` — architecture section (single source of truth for component boundaries)
  - `.planning/MILESTONES.md` v1.4 entry — voice-pool + articulation + SFZ patterns to inherit
  - `.planning/research/STACK.md` (in this directory) — DryWetMidi for offline MIDI export already decided; real-time MIDI (Phase 40) is NEW dependency surface to research separately
- Reference patterns from prior milestones:
  - v1.4 Phase 33 SFZ — opt-in stdlib module pattern (`use "@sfz"`) — repeats for Phase 38 (`@osc`), Phase 40 (`@midi-rt`), Phase 41 (none — `flow doc` is CLI)
  - v1.4 Phase 32 Tuning — first-class music type with reference identity — NOT needed for v1.5 (no new music types planned)
  - v1.3 Phase 26 prefix-only arithmetic — establishes that BREAKING grammar changes ship in one commit while pre-traction; Phase 35 `match` syntax can land cleanly
  - v1.2 Phase 17 LSP — direct `flow-lang` reference (no shadow language model) — Phase 38 REPL bridge inherits this discipline

---
*Architecture research for: Flow v1.5 Stage, Studio, Web milestone — additive integration with existing interpreter*
*Researched: 2026-05-18*
