# Current contracts and dependency inventory

This describes the September 2026 compatibility implementation, not the target
architecture. Machine-readable inventories and measured source hashes are in
[this directory](README.md). Executable feature-interaction documentation is
Phase 1 work; Phase 0 records the surfaces and existing tests to preserve.

## Current source, module, and output contracts

| Surface | Current contract and evidence |
| --- | --- |
| Frontend | `SimpleLexer.Tokenize` → `Parser.Parse` → `Ast.Program`; prefix expressions, optional-parenthesis calls, `->` / `~>`, implicit returns, musical literals, units, and pragmas coexist in one grammar. `Parsing/TypeParser.cs` binds runtime `FlowType`s directly. Existing parser, pipeline, lazy, scope, overload, strict, and music tests characterize these features; extraction must not silently change them. |
| Execution | `FlowEngine.Execute(source, fileName)` returns success, clears its error reporter, registers source text, scans pragmas, lexes/parses, applies configuration, and interprets. Annotations are substantially checked at runtime. `Context`, `ErrorReporter`, `SourceMap`, `GetLastExpressionResult` and `ExecuteScriptAndGetResult` are the present embedding surface. There is no safe public shared analysis API yet; `flow check` executes under console suppression. |
| Initial modules | The constructor registers C# implementations, explicitly loads `@std`, then loads shipped/user improv packs. `std.flow` imports `@collections` and `@bars`; it declares both general functions and musical conversions/formatters. A no-music distribution does not exist yet. |
| Import resolution | `ModuleLoader.LoadModule` normalizes paths, returns `AlreadyLoaded` for successful repeated imports, detects currently-loading cycles, reads/parses and executes module bodies. `@name` resolves beside the app; configured search paths precede relative-file resolution for other imports. Web falls back to embedded sources and diagnoses stripped modules. Successful imports are cached per loader. |
| Export/binding | C# registrations supply implementations; `.flow` `internal proc` declarations bind signatures. `ModuleLoader` runs module code in its module frame, propagates exports, supports qualified lookup, and saves/restores the declaring-file strict bit. Phase 43 qualified-access/collision tests and Phase 44 strict-propagation tests are the current regression coverage. Module initialization is executable, not static metadata discovery. |
| Text/diagnostics | `StdLib.Print` writes to `Console.Out`; renderer advisories use `RenderingDiagnostics.WarnOnce` on stderr with process-global dedup. CLI/REPL and `WasmEntry.RunFromJs` capture/redirect global console streams. `ErrorReporter` stores diagnostics and source locations separately. Capture is not concurrent-session-safe. |
| Audio | `SongRenderer.RenderSong` accepts Flow values and returns `Value.Buffer`; `AudioBuffer` contains interleaved float PCM plus frame/channel/rate metadata. Rendering consumes executable/domain objects and static engine services. `writeWav`, MIDI export, and playback are side-effectful bridges, not language primitives to retain in a future pure core. Existing two-run/RMS/SFZ/timing tests remain the compatibility checks. |
| MIDI/vocals | Preserve current hand/voice splitting and charitable phoneme fallback per [the recorded decision](../../decisions/2026-09-20-baseline-compatibility.md). |
| Browser | `WasmEntry.RunFromJs` returns serialized results; play/stop/dispose and MIDI-byte access are separate interop entry points. The Web build strips desktop services, embeds modules, and creates an AppBundle. Publish/embedded-surface tests inspect the produced bundle; they are not an in-browser listening test. |

## Assemblies and native closure

| Assembly / host | Current dependencies and migration pressure |
| --- | --- |
| `flow-lang` | Frontend, interpreter, types, modules, music, DSP, IO and device adapters in one assembly. DryWetMidi 8.0.3 on Desktop/Web; Desktop also Tomlyn 2.3.2, Rug.Osc 1.2.5, NAudio.Wasapi 2.3.0 (and NAudio.Core transitively). Construction allocates audio/sample services and registers musical functions eagerly. |
| `flow-midi` | Standalone MIDI parser, quantizer and Flow source generator; no project or package dependencies. Most conversion types are internal with test-friend access. Its model is distinct from Flow's MIDI export model. |
| `flow-lsp` | References `flow-lang`, OmniSharp language-server stack. Builtin metadata/engine coupling prevents a pure analyzer distribution today. |
| `flow-interpreter` | References `flow-lang`, `flow-lsp`, PrettyPrompt; owns REPL, watch mode, render-job coordination, console output and live timeout behavior. |
| `flow` CLI | References all four projects and System.CommandLine. Commands compose running/rendering/export/check/doc behavior. |
| Developer tools | `Migrate26`, `StdlibAuditor`, and new `BaselineProbe` reference existing runtime/host projects. Test projects reference the production hosts and test/analyzer packages. These are not distribution components. |

Linux audio uses `Audio/LibPulse.cs` through PulseAudio simple playback/capture;
real-time MIDI uses direct `Audio/LibRtMidi.cs` bindings, and JACK transport uses
`StandardLibrary/Midi/JackFunctions.cs`. These native libraries are host-provided.
macOS uses AudioToolbox through `CoreAudioBackend`; Windows uses WASAPI/NAudio.
Desktop selection is in `AudioPlaybackManager`; declaring an interop type does
not itself prove a device was opened. OSC uses the managed Rug.Osc package.
TTS and notation bridges can invoke external tools (for example `mscore`).
Web excludes desktop native paths at compile time; DryWetMidi remains for file
export. Source interop sites and target conditions are enumerated in JSON.

Assets include shipped samples/licenses and improv packs; `.flow` modules are
copied beside Desktop hosts and embedded for Web. `SampleCache` / `SfzSampleCache`
and style-pack initialization are explicit obstacles to a minimal pure host.
Keep attribution and sample manifests intact during extraction.

## Mutable state ownership and first extraction candidates

| State / owner | Current coupling | Planned seam |
| --- | --- | --- |
| `FlowEngine.CurrentSampleCache`, `CurrentSfzSampleCache`, `CurrentExecutionContext` | Latest constructed engine publishes process-static renderer dependencies; disposal clears them | Explicit render/evaluation services owned by session |
| `FlowConfig.Active` | Mutable process-wide configuration, read by hosts and runtime | Immutable per-session snapshot |
| `RenderingDiagnostics._emitted` | Process-global advisory dedup, guarded by a lock; test order can consume another test's advisory | Per-session/job diagnostic sink |
| `WasmEntry._sharedEngine`, `_sharedBackend`, `_lastMidiBytes`, console redirection | Locked global host lifetime and output capture | Explicit browser session lifetime/output |
| `SynthUtils.Rng` | Reset at render boundaries; concurrent renders can interfere | Render-owned deterministic random source |
| `PianoSynthesizer.CurrentReleaseSec` | AsyncLocal override bridged through static property | Explicit render options |
| `ExecutionContext` / `StackFrame` | Lexical variables, musical context, PRNG/style/section/module registries coexist | Preserve lexical and musical scope semantics while separating ownership |
| Runtime type singletons, symbol/cache tables, generated delegates | Static references include legitimate immutable identities and mutable caches; `readonly` alone does not prove immutability | Classify before changing type identity or cache lifetime; full field candidates are in JSON |
| `LiveReloadManager` | Host-owned tasks; timeout does not establish worker termination | Bounded job coordinator with cancellation and latest-request-wins publication |

A minimal-host proof should first execute a collection/string/numeric program,
record which services initialize, and then assert the eventual dependency
closure excludes music/native/sample assets. Today the metadata and constructor
inventory demonstrate that this gate cannot pass. Do not create forwarding
assemblies and call that separation. Public source/diagnostic/value APIs and
module signature ownership need decisions and executable contracts in Phase 1
before the Phase 2/3 moves.
