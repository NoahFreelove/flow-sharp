# Phase 47: Compile-Target Flavors - Context

**Gathered:** 2026-05-25
**Status:** Ready for planning

<domain>
## Phase Boundary

Introduce `FlowTarget=Desktop|Web` MSBuild conditioning so the flow-lang library can compile cleanly under WASM by stripping features that cannot run in a browser sandbox. Foundation for Phase 48 — without target flavors, the WASM build cannot link (P/Invoke targets fail at JIT time, `FileSystemWatcher` is unavailable, raw UDP sockets are blocked).

**Three coordinated changes:**

- **`<FlowTarget>` MSBuild property** added to `flow-lang/flow-lang.csproj` with default value `Desktop`. Set via `dotnet build -p:FlowTarget=Web` or `=Desktop`. Conditional `<DefineConstants>$(DefineConstants);FLOW_WEB</DefineConstants>` activates when `FlowTarget=Web` — gives C# `#if FLOW_WEB` / `#if !FLOW_WEB` preprocessor switches without runtime cost.

- **File-level exclusion via `<Compile Remove="..." />`** in a conditional `<ItemGroup Condition="'$(FlowTarget)' == 'Web'">`. Removes files that don't compile in browser: `Audio/PulseAudioSimpleBackend.cs`, `Audio/PulseAudioCaptureBackend.cs`, `Audio/CoreAudioBackend.cs`, `StandardLibrary/Audio/Sfz/**/*.cs`, `StandardLibrary/Network/Osc/**/*.cs`, `Live/**/*.cs`, `Interpreter/LambdaCaptureAuditor.cs`, `Runtime/LiveBlockRegistry.cs`, `Runtime/LiveReloadManager.cs`, `Audio/InputFunctions.cs` (Phase 38 micBuffer surface), `flow-jetbrains/**` (Kotlin, not Web-relevant). `<None Remove="Samples/**" />` excludes the 3.05 MB U-Iowa MIS sample bundle from embedded resources.

- **Conditional registration in `BuiltInFunctions.cs::RegisterAll`** for features whose code stays but whose builtins should not surface. `#if !FLOW_WEB` guards on `RegisterSfz()`, `RegisterOsc()`, `RegisterMicInput()`, `RegisterLiveBlock()` calls. The wrapped Register*() methods themselves stay un-guarded — only the call sites in the main RegisterAll() entry point. Cleaner separation; if Phase 49 wants WebMIDI later (v1.6), it adds a new `WebMidiBackend` + `RegisterWebMidi()` call without touching the strip-list.

**What stays in Web target** (≈85% of the language surface):
- Full core language: lexer (`SimpleLexer.cs`), parser (`Parser.cs`), AST nodes, evaluator (`ExpressionEvaluator.cs`), interpreter (`Interpreter.cs`), type system (`TypeSystem/**`), `OverloadResolver`, error reporter.
- All music types: Note, Chord, Semitone, Cent, Millisecond, Second, Decibel, Beat, Hertz, Sequence, MusicalNote, Section, Song, Symbol, Tuple, Dict.
- Pattern matching (Phase 35), parameterized sections (Phase 36 SECT-01).
- All synthesis: sine/saw/square/triangle/noise oscillators, drums/organ/wavetable synths.
- All DSP: reverb, lowpass/highpass/bandpass filters, compress/sidechain, delay, gain (dB) / volume (linear), granular synthesis (Phase 37 DSP-01), time-stretch (DSP-02), pitch-shift (DSP-03).
- Phase 36 stdlibs: `@patterns` (13 Tidal combinators), `@generative` (Markov/L-system/cellular/Lorenz/logistic), `@improv` (jam + style packs).
- MIDI file write via DryWetMidi 8.0.3 — **pending WASM compatibility verification at Plan 47-04**.
- Notation IO export: MusicXML (Phase 39 XML-01), LilyPond (LILY-01), ABC import (ABC-01), MML import (MML-01) — XmlWriter / text-only emitters, no native deps.
- Voice allocation (Phase 28), voicePool blocks, articulation enum + envelopes.
- Pragmas: `enable strict;`, `enable matchExhaustive;`, `enable justIntonation;`, etc.

**Strip-list rationale** (per browser sandbox constraints):

| Stripped | File / Module | Reason |
|----------|---------------|--------|
| Native audio backends | `PulseAudio*Backend.cs`, `CoreAudioBackend.cs` | `[DllImport("libpulse-simple")]` / `[DllImport("AudioToolbox")]` — no native libs in browser |
| SFZ sampler | `StandardLibrary/Audio/Sfz/**`, `sfz.flow`, `@sfz` module | External sample dependency (100s of MB potential); browser sandbox prevents arbitrary file loads |
| U-Iowa MIS samples | `flow-lang/Samples/**` (3.05 MB) | 20% of WASM budget; Phase 29 sampled instruments charitably fall back to synthesis-only |
| Live coding | `live { }` AST + `LiveBlockRegistry` + `LambdaCaptureAuditor` + watch mode | Requires `FileSystemWatcher` (System.IO.FileSystemWatcher) — unavailable in browser |
| Audio input | `(micBuffer)` + `PulseAudioCaptureBackend` | PulseAudio P/Invoke; v1.6 backlog: `getUserMedia` integration |
| OSC server/client | `StandardLibrary/Network/Osc/**`, Rug.Osc 1.2.5 | Raw UDP sockets unavailable; WebRTC DataChannel is v1.6+ |
| MIDI hardware | Phase 40 `IMidiBackend` impls (RtMidi.Core) | Hardware port access; v1.6 backlog: WebMIDI as new IMidiBackend impl |
| REPL + `flow watch`/`test`/`doc` CLIs | `flow-interpreter/**` | Not language features — CLI entry points, web build is library-only |

**Composer-facing error UX**: When a Web-built script uses a stripped feature (e.g. `live 1bar { ... }` block, `(micBuffer 1.0s)`, `(oscListen 8000 "/cc" handler)`, `use "@sfz"`), the parser/evaluator emits a clear advisory pointing at the line with text "feature requires Desktop target — run with `flow run script.flow` locally". No silent fallback to bug-via-omission; charitable interpretation per `feedback_charitable_interpretation` says "tell the composer what's wrong and how to fix it" not "pretend it works."

</domain>

<decisions>
## Implementation Decisions

### Build Conditioning (Area 1)

- **D-47-01: `FlowTarget` is a top-level MSBuild property in `flow-lang/flow-lang.csproj`, not a separate `.csproj`**. Rationale: keeps single source of truth for the library; avoids parallel-csproj drift; conventional .NET pattern for target-conditioned builds (Microsoft's own `<TargetFramework>` family uses this). Rejected: separate `flow-lang.Web.csproj` — adds maintenance overhead, drifts from `flow-lang.csproj`, requires test infrastructure dance.

- **D-47-02: `FLOW_WEB` is the preprocessor symbol; absence implies Desktop**. Asymmetric (no `FLOW_DESKTOP` constant). Rationale: Desktop is the default everywhere; explicit `FLOW_WEB` guards mark intentional Web-aware code. Matches the .NET ecosystem convention (`NETSTANDARD2_0` is defined when targeting netstandard2.0, no inverse `NOT_NETSTANDARD2_0`).

- **D-47-03: Conditional `<ItemGroup>` for `<Compile Remove>` runs at MSBuild evaluation time, before C# compile**. Rationale: cleaner than wrapping entire files in `#if FLOW_WEB` blocks (which would still parse the file's references, leading to type-resolution errors for missing P/Invoke targets). File-level exclusion is the canonical pattern for strip-builds.

- **D-47-04: `flow-jetbrains/**` is NOT in the strip-list because it's a sibling project**, not part of `flow-lang.csproj`. Its build is independent (Kotlin/Gradle). Mentioned in the strip-list table above only for documentation completeness — no MSBuild change needed.

### Audio Backend Abstraction (Area 2)

- **D-47-05: New `Audio/WebAudioBackend.cs` ships as a STUB in Phase 47**. Implements `IAudioBackend` interface with all methods throwing `PlatformNotSupportedException` except `IsAvailable()` which returns `OperatingSystem.IsBrowser()` (false on Desktop, true under Mono-WASM). Phase 48 fills in the real implementation via `[JSImport]`/`[JSExport]`. Rationale: Phase 47 wants to ship a working Web build that links — even if it can't play audio yet. Phase 48 absorbs the audio integration without churning the project file.

- **D-47-06: `AudioPlaybackManager.PickBackend()` adds a new branch — `if (new WebAudioBackend().IsAvailable()) return new WebAudioBackend();`** — between the existing PulseAudio and CoreAudio probes. Order: Web first (cheap probe), then PulseAudio (Linux), then CoreAudio (macOS), then null (silent fallback). Charitable: if no backend probes available, return a no-op backend that swallows playback calls (existing pattern per `IAudioBackend.cs:21-29` `NullAudioBackend`).

- **D-47-07: `IsAvailable()` uses `OperatingSystem.IsBrowser()` not `RuntimeInformation.OSDescription.Contains("Browser")`**. Rationale: `OperatingSystem.IsBrowser()` is a JIT intrinsic that the Mono-WASM linker can dead-code-eliminate — Desktop builds shed the entire `WebAudioBackend` reachability through trim-mode analysis without manual guarding. Bonus: same intrinsic that the BCL itself uses for `WebAssembly` partial trust.

### Builtin Registration (Area 3)

- **D-47-08: `RegisterAll()` in `BuiltInFunctions.cs` is the ONLY guard site**. Per-feature `Register*()` methods (`RegisterSfz`, `RegisterOsc`, etc.) stay unguarded so they can be unit-tested independently on Desktop. The `#if !FLOW_WEB` guards live in the central RegisterAll() entry only. Rationale: minimum-surface-change principle; easier to audit (one file, one place); aligns with how Phase 38 OSC + Phase 33 SFZ already register conditionally (composer-opt-in via `use "@x"` for those, build-opt-in here).

- **D-47-09: When a stripped feature is invoked at runtime, the evaluator emits an advisory and returns `Void`** — NOT throws. Charitable-interpretation rule per `feedback_charitable_interpretation` and Phase 36 PAT-02 precedent. Example: `(oscListen 8000 "/path" handler)` in a Web-build script → stderr advisory `[osc] OSC server unavailable on Web target — line 12. Build with FlowTarget=Desktop to enable. Returning Void.` Composer-facing UX matches Phase 38 D-38-13 charitable approach. Live `live { }` blocks are a PARSE-TIME error because they're block syntax not a builtin — the parser short-circuits with a Rust-style diagnostic when `#if !FLOW_WEB` is active.

- **D-47-10: Parse-time gates for `live { ... }` and `use "@sfz"`/`@osc"`** — implement in `Parser.cs` (block keywords) and `ModuleLoader.cs` (stdlib import). Module loader checks the FlowTarget by reading a new `FlowEngine.IsWebTarget` static flag (set at engine construction time). Parser checks a new `FlowEngine.SupportsLiveBlocks` flag. Both flags set via `FlowEngine` constructor — Desktop sets true, Web (auto-detected via `OperatingSystem.IsBrowser()`) sets false. Cheap static check, no per-call overhead.

### Sample Bundle Handling (Area 4)

- **D-47-11: `flow-lang/Samples/**` is excluded from Web embedded resources via `<None Remove="Samples/**" />` and Phase 29 `SampledInstrumentRenderer` charitably falls back**. When `SampleCache.LoadSample()` returns null (file not found / bundle stripped), the renderer falls through to the synthesis path. Existing pattern at `SampledInstrumentRenderer.cs` already handles this for "sample missing for this pitch" — Web build just always hits that path. Zero new code; net effect: piano sounds like Phase 28 synth piano under Web target. Acceptance test: Web build of `examples/tutorial.flow` rendering a piano sequence produces byte-identical output to the same script run on Desktop with `Samples/` directory renamed (proving fallback path is what fires).

- **D-47-12: Phase 33 SFZ is fully stripped — sample-based SFZ instruments don't fall back, they parse-error**. Rationale: SFZ is opt-in (`use "@sfz"`) and explicitly external (composer supplied `(loadSfz "/path/file.sfz")` paths). On Web target, the entire `@sfz` module is unavailable, so the `use` statement fails at ModuleLoader time with the advisory above. Phase 29 sampled tonal instruments fall back because they're transparent (the composer wrote `Piano p = ...` not `(loadSfz "...")` — composer-intent is "make piano sound", not "use this specific sample file"); Phase 33 SFZ doesn't fall back because composer-intent is the opposite (specific file, specific sampler).

### Testing (Area 5)

- **D-47-13: `flow-lang.Tests` runs Desktop-only by default**. New opt-in test pass `dotnet test -p:FlowTarget=Web` runs only the Web-compatible subset (per-test attribute `[FlowTargetFact("Desktop", "Web")]` or `[FlowTargetFact("Web")]`). Most tests are platform-independent (lexer, parser, type system, harmony, transforms) and should pass under both. Tests that exercise stripped features (live blocks, SFZ, OSC, mic input, native audio backends) are tagged Desktop-only and skip silently under Web. Plan 47-04 defines the test attribute + tag application sweep.

- **D-47-14: New `AssemblyReferenceScanTests` validates the Web build has no references to stripped namespaces** (Mono.Cecil-based reflective scan over the Web-compiled `flow-lang.dll`). Acceptance: zero references to `Rug.Osc`, `System.IO.FileSystemWatcher`, `libpulse-simple` P/Invoke surface, `AudioToolbox` P/Invoke surface, `Microsoft.Extensions.Hosting` (if any drift), `RtMidi.Core`. Catches drift if a future PR accidentally re-introduces a stripped dependency via transitive reference.

</decisions>

<code_context>
## Existing Code Insights

**Existing IAudioBackend pattern** (`flow-lang/Audio/IAudioBackend.cs`):
- Interface with `Initialize()`, `Play(AudioBuffer)`, `Stop()`, `IsAvailable()`.
- `NullAudioBackend` already provides the silent-fallback pattern Phase 47 inherits.
- `AudioPlaybackManager.PickBackend()` is the polymorphic dispatch entry — minimal change for D-47-06.

**Existing PragmaRegistry pattern** (`flow-lang/Lexing/PragmaRegistry.cs:27`):
- Phase 21 (Pragma System) established `KnownPragmas` Dictionary with description strings.
- Phase 47 doesn't add a pragma — `FlowTarget` is a BUILD-time flag, not a composer pragma. But the PragmaRegistry pattern informs the static-flag approach in D-47-10.

**Existing strip-via-condition pattern** (`flow-lang.csproj:**`):
- `<TargetFramework>net10.0</TargetFramework>` is unconditional today.
- No existing `Condition="..."` ItemGroups in the project. Phase 47 introduces the pattern; future phases reuse it (e.g., Phase 41 cross-platform binaries).

**Existing module-load gate pattern** (`flow-lang/Runtime/ModuleLoader.cs`):
- `use "@sfz"` resolves via `LoadStdlibModule(name)` — Phase 47 adds a guard at the top of this method that throws a charitable advisory when the module is in the stripped-on-Web list.
- Module list: `audio.flow`, `collections.flow`, `bars.flow`, `notation.flow`, `composition.flow`, `sfz.flow`, `patterns.flow`, `generative.flow`, `improv.flow`, `notation-io.flow`, `osc.flow`. Stripped-on-Web: `sfz.flow`, `osc.flow`. The rest stay.

**Existing FlowEngine construction surface** (`flow-lang/Core/FlowEngine.cs`):
- Constructor takes ` ErrorReporter`, `BuiltInDocs`, etc. Phase 47 adds `IsWebTarget` + `SupportsLiveBlocks` static properties on FlowEngine set in the constructor via `OperatingSystem.IsBrowser()`.

**Reference for charitable advisory pattern**:
- Phase 36 PAT-02 (charitable interpretation in `@patterns`): stderr `[patterns] ...` prefix + dedup via `WarnOnce`. Phase 47 advisories follow same pattern with `[target]` prefix.

**Reference for assembly-reference scan**:
- No existing in-repo precedent. Mono.Cecil is widely used (.NET MIT-licensed). Plan 47-05 introduces the scanner; future phases reuse for similar invariants.

</code_context>

<specifics>
## Specific Ideas

1. Plan 47-01: MSBuild conditioning foundation — add `<FlowTarget>` property, conditional `<DefineConstants>`, conditional `<ItemGroup>` with strip-list `<Compile Remove>` entries, conditional `<None Remove>` for Samples. Acceptance: `dotnet build -p:FlowTarget=Web` succeeds (may have warnings about unreferenced symbols; errors are the only blocker).

2. Plan 47-02: `WebAudioBackend.cs` stub + `IAudioBackend` integration — file scaffolding, `IsAvailable()` via `OperatingSystem.IsBrowser()`, all other methods throw `PlatformNotSupportedException`. `AudioPlaybackManager.PickBackend()` adds Web-first probe (D-47-06). Acceptance: Desktop build picks PulseAudio/CoreAudio as before; Web build (if attempted to play audio) throws the platform exception with clear message.

3. Plan 47-03: `BuiltInFunctions.cs` central guard sweep — wrap `RegisterSfz()`, `RegisterOsc()`, `RegisterMicInput()`, `RegisterLiveBlock()` calls in `#if !FLOW_WEB`. Add `FlowEngine.IsWebTarget` + `FlowEngine.SupportsLiveBlocks` static flags. Parser + ModuleLoader gates check these flags (D-47-10). Acceptance: Web build with `use "@sfz"` parses fine but ModuleLoader emits the charitable advisory; Web build with `live 1bar { }` block raises a Rust-style parse error.

4. Plan 47-04: DryWetMidi 8.0.3 WASM-compat smoke + test framework `FlowTargetFact` attribute + Desktop-only tag sweep. Acceptance: Web build succeeds with DryWetMidi referenced OR strip DryWetMidi from Web + add advisory at `writeMidi` call site if it doesn't compile. xUnit `[FlowTargetFact("Web")]` and `[FlowTargetFact("Desktop")]` discriminate test runs.

5. Plan 47-05: AssemblyReferenceScanTests via Mono.Cecil — `Mono.Cecil` 0.11.5 (MIT) reflective scan of Web-compiled `flow-lang.dll`. Asserts zero references to `Rug.Osc`, `FileSystemWatcher`, `[DllImport("libpulse-simple")]` / `[DllImport("AudioToolbox")]`, `RtMidi.Core`. Acceptance: scan runs in `flow-lang.Tests` opt-in `[Trait("FlowTarget", "Web")]` pass.

6. Plan 47-06: Closer — Phase 47 VERIFICATION + ROADMAP/STATE/REQUIREMENTS/CLAUDE.md sweep. Acceptance: ROADMAP Phase 47 marked complete; STATE.md updated; REQUIREMENTS.md gets new REQ-WEB-TARGET-01..10 IDs assigned to the Phase 47 plans retroactively (since they were TBD at phase-add time); CLAUDE.md gets new section "Compile-Target Flavors" documenting the `FlowTarget=Desktop|Web` build surface for future contributors.

</specifics>

<deferred>
## Deferred Ideas

- v1.6 WebMIDI as new `IMidiBackend` impl for Web target (`WebMidiBackend.cs`).
- v1.6 `getUserMedia` integration as new Web-only path for `(micBuffer)`.
- v1.6 WebRTC DataChannel for OSC-like surface (different protocol, different model).
- v1.6 SFZ on Web via lazy-loaded sample CDN — re-enable `@sfz` opt-in if a real composer asks.
- v1.6 source-generator for `InternalFunctionRegistry` (per D-v1.5-02) eliminates the reflection-heavy registry pattern. Would let Mono-WASM AOT-link more aggressively. Phase 47 ships with reflection intact (jiterpreter handles it; not a Phase 47 problem).
- `FlowTarget=Minimal` for embedded use cases (no audio at all, just composition + WAV file write). Not needed for Phase 47-49 scope; trivial future-add once `FlowTarget=Web` pattern is in place.

</deferred>
