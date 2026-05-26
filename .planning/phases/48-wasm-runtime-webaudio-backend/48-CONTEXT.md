# Phase 48: WASM Runtime + WebAudioBackend - Context

**Gathered:** 2026-05-25
**Status:** Ready for planning

<domain>
## Phase Boundary

Build `flow-lang` under .NET 10 Mono-WASM with `FlowTarget=Web` (Phase 47 prereq), ship a `WebAudioBackend` that pushes rendered audio through the browser's `AudioContext`, and produce a deployable JS bundle (`flow-runtime.js`) that Phase 49 consumes from its SvelteKit playground tab. Single biggest feasibility risk in the v1.5 milestone — research surfaced no .NET-in-WASM prior art for AudioWorklet driving, so v1 ships the conservative **offline-render → `AudioBuffer` → `AudioBufferSourceNode`** pattern.

**Three coordinated changes:**

- **Mono-WASM build pipeline** — `flow-lang.csproj` gains a WASM target (`<RuntimeIdentifier>browser-wasm</RuntimeIdentifier>` activated via `FlowTarget=Web`), trimmer configuration with `<TrimmerRootDescriptor>` for the reflection-heavy `InternalFunctionRegistry` (per D-v1.5-02 carryforward — the registry's `Type.GetMethods()` discovery defeats naive trimming), jiterpreter enabled (`<WasmEnableJiterpreter>true</WasmEnableJiterpreter>`), invariant globalization (`<InvariantGlobalization>true</InvariantGlobalization>` to skip the ICU bundle, ~10 MB savings).

- **`WebAudioBackend.cs` real implementation** — Phase 47 ships the stub; Phase 48 implements via `[JSImport]` + `[JSExport]`. `PlayBuffer(AudioBuffer)` marshals the `Float32Array` to JS in one shot, calls into JS that creates `AudioBufferSourceNode` from the buffer, copies samples to a `new AudioBuffer(...)`, and starts the node. `Stop()` revokes any active source nodes. `Dispose()` closes the `AudioContext`.

- **`flow-runtime.js` ES module glue** — new file at `flow-lang/wasm/flow-runtime.js` (or wherever `dotnet publish -p:FlowTarget=Web` puts it). Exports a tiny API: `await loadFlowRuntime() → { run(source: string) → Promise<RunResult>, play(wav: Float32Array) → AudioBufferSourceNode, stop() → void }`. `RunResult` shape: `{ wav?: Float32Array, midi?: Uint8Array, stdout: string, stderr: string, errors: string[], duration_ms: number }`. Phase 49 consumes this directly. No framework lock-in.

**The offline-render pattern (D-48-01 canonical)**: When composer calls `(play (createSineTone 440Hz 1.0 0.5))` in the playground, the runtime:

1. Compiles the Flow source via `FlowEngine` (existing path).
2. Evaluates the expression to a `Buffer` value (existing path — runs in .NET on the main browser thread; ~10-50ms for short pieces, up to 30s wall cap for full songs).
3. Marshals the buffer's `Float32Array` to JS via `[JSExport]`-bound method (one-shot allocation).
4. JS glue creates a `new AudioBuffer(channels, frames, 44100)`, calls `audioBuffer.copyToChannel(samples, 0)` (and channel 1 for stereo).
5. JS glue creates a new `AudioBufferSourceNode`, sets `node.buffer = audioBuffer`, connects to `audioContext.destination`, and calls `node.start()`.
6. JS returns the node so callers can `node.stop()` if needed.

This works because Flow's existing playback pipeline is composition → rendered buffer → backend.Play(buffer). The Web backend just replaces step 3 from "submit to PulseAudio" with "submit to WebAudio."

**No real-time hot-swap**: Phase 47 strips `live { }` blocks from Web (parse-time error). The offline-render pattern doesn't support live coding because we render the full buffer first. v1.6 stretch: SharedArrayBuffer ring-buffer + AudioWorklet pattern (D-48-02). Acceptable v1 scope because: (a) live coding is not the primary playground use case — sharing/learning/showcasing are; (b) playground is composer-curious surface, not pro live-coding tool; (c) the alternative pattern is genuinely frontier with no .NET-in-WASM precedent.

</domain>

<decisions>
## Implementation Decisions

### Build Pipeline (Area 1)

- **D-48-01: Mono-WASM jiterpreter, NOT NativeAOT-LLVM** (carryforward from D-v1.5-02). Rationale: FlowEngine's reflection-heavy `InternalFunctionRegistry` requires runtime type discovery; NativeAOT needs a source-generator pass that's v1.6 backlog. Jiterpreter is what Blazor uses today and matches Microsoft's recommended path for reflection-friendly .NET in browser.

- **D-48-02: `<TrimMode>full</TrimMode>` + explicit `<TrimmerRootDescriptor>`** at `flow-lang/wasm/trim-roots.xml`. The descriptor preserves: `FlowLang.StandardLibrary.InternalFunctionRegistry` and all `RegisterXxx` static methods, all `FlowType` subclasses (TypeSystem types are discovered by name), all music-type singletons (`Note.Instance` / `Chord.Instance` / etc.). Trimming aggressively otherwise — strips unused BCL, unused Phase 36 stdlibs at link time (lazy-loaded per D-48-05 instead).

- **D-48-03: `<InvariantGlobalization>true</InvariantGlobalization>` + `<HybridGlobalization>false</HybridGlobalization>`**. Rationale: ICU is ~10 MB; Flow doesn't need locale-aware string handling. All numeric parsing in Flow uses `CultureInfo.InvariantCulture` already. Acceptance test: invariant globalization mode passes the existing test suite under FlowTarget=Web. Risk: if any code path uses `string.ToUpper()`/`ToLower()` without `Culture` arg, the Turkish-I problem manifests; Plan 48-02 grep for `.ToUpper()` / `.ToLower()` / `string.Compare(` calls and adds Culture args where needed.

- **D-48-04: `<WasmEmitSymbolMap>true</WasmEmitSymbolMap>`** in Debug only. Production builds strip symbol maps for size + minor security (don't leak source structure). Plan 48-01 confirms size delta — symbol map is ~1-2 MB uncompressed.

- **D-48-05: Lazy-load Phase 36 stdlibs (`@patterns`, `@generative`, `@improv`) + Phase 39 (`@notation-io`) from a separate bundle if v1 bundle exceeds 15 MB**. Pattern: `flow-runtime.js` exports `await runtime.loadStdlib("patterns")` and the playground UI calls it lazily when a `use "@patterns"` import is encountered. Composer-side: same `use` statement; loader-side: fetches additional WASM module on demand. **Decision deferred to Plan 48-01 dry-run** — if v1 bundle ≤15 MB without lazy-loading, ship monolithic. If >15 MB, implement lazy-loading for the four opt-in stdlibs.

### WebAudioBackend Implementation (Area 2)

- **D-48-06: `[JSImport]`/`[JSExport]` for the JS↔C# boundary**. Modern .NET 10 interop surface, replaces `Microsoft.JSInterop` Blazor pattern. Type-safe at compile time. The C# side declares static partials: `[JSImport("createAudioBuffer", "flow-runtime")] static partial Task<JSObject> CreateAudioBuffer(int channels, int frames, int sampleRate);`. The JS side declares the matching exports.

- **D-48-07: Stereo audio always**. Even mono Flow Buffers get promoted to stereo before marshalling — left + right channels carry identical samples. Rationale: WebAudio `AudioContext` defaults to stereo output; mono→stereo promotion in C# (cheap) avoids JS-side branching. Matches Phase 37 B2 LOCK posture ("SFZ voices ALWAYS promote to stereo").

- **D-48-08: One `AudioContext` per `FlowEngine` instance, created lazily on first `play()` call**. Rationale: `AudioContext` is browser-allocated, expensive to create, and Chrome limits to ~6 simultaneous contexts per tab. The runtime holds one per engine; reuses across plays. Disposed on `runtime.dispose()`.

- **D-48-09: `audioContext.resume()` inside the user-gesture chain** (D-48-05 carryforward from autoplay policy). The playground "Run" button's `onclick` handler must call both `runtime.run(source)` AND `audioContext.resume()` in the same call frame. Phase 49 wires this UX. WebAudioBackend's `PlayBuffer` does NOT call resume() — that's the playground's responsibility (the backend can't know if it's in a gesture handler).

- **D-48-10: 30-second wall-clock evaluation cap** — composer code is wrapped in `CancellationTokenSource` with `CancelAfter(TimeSpan.FromSeconds(30))`. Long renders fail with a `[runtime] evaluation exceeded 30s cap — line N (best guess)` advisory. Same cap pattern as Phase 38 LIVE-02 file-watch debounce + 30s wall-clock cap. Composers wanting longer renders run Desktop.

- **D-48-11: `WebAudioBackend.IsAvailable()` returns `OperatingSystem.IsBrowser() && JSInterop available`**. The second clause guards against Mono-WASM running headlessly (e.g. server-side) where the BCL is in browser mode but no JS environment exists. Charitable fallback to `NullAudioBackend` in that case.

### JS Glue + Bundle (Area 3)

- **D-48-12: `flow-runtime.js` is an ES module, not UMD/CommonJS**. SvelteKit consumes ES modules natively. Rejected: UMD bundle for "broader compatibility" — adds 30% to wrapper size for use cases we don't target.

- **D-48-13: API surface kept minimal** (no over-engineering). Five exports total: `loadFlowRuntime()`, `RunResult` type, `runtime.run(source)`, `runtime.play(wav)`, `runtime.stop()`. `runtime.dispose()` for cleanup. `runtime.loadStdlib(name)` if D-48-05 lazy-loading is needed. Phase 49 may add convenience wrappers in SvelteKit-land; the runtime stays Svelte-agnostic.

- **D-48-14: Errors bubble structured, not strings**. `RunResult.errors: { kind: 'parse' | 'eval' | 'runtime' | 'cancel' | 'platform-not-supported', message: string, line?: number, column?: number, source_snippet?: string }[]`. Phase 49 renders these as Rust-style diagnostic boxes in the playground UI.

- **D-48-15: `stdout` and `stderr` captured separately**. Flow's existing `print` builtin writes to stdout; advisories (charitable interpretation messages) go to stderr. JS API surfaces both as strings in `RunResult`. Playground UI shows stderr in dimmed/italic text below stdout.

- **D-48-16: Two-run cmp-clean determinism preserved**. Same Flow source → byte-identical Float32Array output across `runtime.run()` calls at the same source. Verified by Plan 48-04 acceptance test: run the same script twice, `Float32Array.byteLength` matches, byte-level cmp returns equal. Lorenz / chaos primitives keep D-36-09 same-platform-only caveat (cross-browser/cross-platform FP divergence acknowledged).

### DryWetMidi WASM Compatibility (Area 4)

- **D-48-17: DryWetMidi 8.0.3 WASM compatibility verified at Plan 48-02**. Single 10-line smoke test: `dotnet publish -p:FlowTarget=Web` + new `WriteMidiFromWasmTests` that calls `MidiFile.Write(stream)` and asserts the resulting bytes are valid SMF. If smoke passes → MIDI export available on Web (composer's MIDI download); if it fails → strip DryWetMidi from Web build and have `writeMidi` emit a parse-time advisory "MIDI file write unavailable on Web target — open Desktop to export." v1.6 backlog: hand-rolled MIDI writer for Web (SMF format is well-documented; ~300 LOC effort).

- **D-48-18: If DryWetMidi WASM-compatible, MIDI download becomes a `Uint8Array` in `RunResult.midi`**. Phase 49 wires the download UI (creates Blob URL, triggers anchor click). No backend change; pure download mechanism.

### Notation IO on Web (Area 5)

- **D-48-19: MusicXML / LilyPond / ABC / MML all WASM-compatible** (verified via inspection: hand-rolled `XmlWriter` for MusicXML per Phase 39 D-39-08; text emission for LilyPond; hand-rolled parsers for ABC/MML). No external deps. They produce strings; Phase 49 wires the download UI similarly to D-48-18.

</decisions>

<code_context>
## Existing Code Insights

**Flow's existing playback pipeline**:
- `(play buffer)` → `AudioPlaybackManager.Play(buffer)` → `_currentBackend.Play(buffer)`.
- `_currentBackend` selected at FlowEngine construction time via `PickBackend()` chain.
- Phase 47 D-47-06 adds `WebAudioBackend` to the chain — Phase 48 fills in the real backend.

**Flow's existing rendering pipeline**:
- `(renderSong song)` → `SongRenderer.Render(song, instrumentMap)` → returns rendered `Buffer`.
- `(createSineTone freq dur amp)` → returns `Buffer` directly (no song needed).
- `(writeWav buffer "file.wav")` → `WavFileWriter.Write(buffer, path)` — Desktop path; Web rebuilds this as `runtime.exportWav()` returning a `Uint8Array` for browser download.

**Existing AudioBuffer structure** (`flow-lang/Audio/AudioBuffer.cs`):
- `Samples: float[]` (interleaved stereo or mono).
- `Channels: int` (1 or 2).
- `SampleRate: int` (44100 default).
- Phase 48 marshals `Samples` directly across the JS boundary; stereo promotion (D-48-07) happens in C# before marshal.

**Existing Music Types**:
- Hertz canonicalizes at lex time (Phase 26.2). `1.5kHz` → `1500.0` internally. No JS-side handling needed.
- All numeric music types serialize to `double` in JS interop (Float32Array uses 4-byte floats, separate from the metadata which is doubles).

**Existing test infrastructure**:
- `flow-lang.Tests` runs against Desktop today. Phase 47 D-47-13 introduces `FlowTargetFact` attribute discriminator. Plan 48-04 adds `[FlowTargetFact("Web")]` tests for the new acceptance paths.
- xUnit + Mono.Cecil already in dev deps (per Phase 47).

**Existing CancellationToken pattern** (`flow-interpreter/LiveStatusPanel.cs` per Phase 38 LIVE-02):
- 30-second `Task.Run + Wait(TimeSpan.FromSeconds(30))` pattern. Phase 48 reuses for D-48-10.

**Existing `[JSImport]`/`[JSExport]` precedent**:
- No existing in-repo use. Phase 48 introduces. Documentation: [Microsoft .NET 10 JS interop](https://learn.microsoft.com/en-us/aspnet/core/blazor/javascript-interoperability/).

**Existing browser-target precedent**:
- No prior Flow Web build. Phase 47 establishes the project conditioning; Phase 48 ships the first runnable Web build.

**Research findings cite** (research pass 2026-05-25):
1. No prior art for .NET/Mono-WASM driving AudioWorklet directly. Offline-render → AudioBuffer is canonical.
2. Pattern: render full song to Float32Array in .NET, marshal once via [JSExport], hand to AudioBufferSourceNode.start().
3. AudioWorklet only via SharedArrayBuffer ring-buffer pattern; requires COOP/COEP headers.
4. ≤15 MB compressed plausible but tight; sample bundle eats 20% (already stripped per Phase 47).
5. Gotchas: GC pauses irrelevant for offline render; autoplay policy requires AudioContext.resume() inside user gesture; [JSImport]/[JSExport] marshalling multi-MB Float32Array is one-shot and fast.

Sources:
- [dotnet/runtime#85592 — threading + JS interop](https://github.com/dotnet/runtime/issues/85592)
- [KristofferStrube/Blazor.WebAudio](https://github.com/KristofferStrube/Blazor.WebAudio)
- [.NET 9 WASM size regression #109787](https://github.com/dotnet/runtime/issues/109787)
- [Chrome Wasm ring-buffer pattern](https://googlechromelabs.github.io/web-audio-samples/audio-worklet/design-pattern/wasm-ring-buffer/)

</code_context>

<specifics>
## Specific Ideas

1. Plan 48-01: WASM build pipeline foundation — `flow-lang.csproj` gains `<RuntimeIdentifier>browser-wasm</RuntimeIdentifier>` conditional on `FlowTarget=Web`, jiterpreter + invariant globalization + full trim, `<TrimmerRootDescriptor>` at `flow-lang/wasm/trim-roots.xml`. Acceptance: `dotnet publish flow-lang -p:FlowTarget=Web -c Release` produces a `bin/Release/net10.0/browser-wasm/AppBundle/` directory. Bundle size measured + recorded.

2. Plan 48-02: DryWetMidi WASM compat smoke + culture-invariant sweep. New `WriteMidiFromWasmTests.cs` exercises `MidiFile.Write` under FlowTarget=Web; grep for `.ToUpper()`/`.ToLower()`/`Compare(string`/`Format(IFormatProvider` — add `CultureInfo.InvariantCulture` arg or `StringComparison.Ordinal` per call site. Acceptance: green smoke + grep clean.

3. Plan 48-03: WebAudioBackend real implementation — `[JSImport]`/`[JSExport]` boundary, AudioContext lifecycle, stereo promotion, 30s cancel cap. Includes the JS glue `flow-runtime.js` initial draft. Acceptance: `WebAudioBackendTests.cs` (FlowTarget=Web) exercises the boundary with a tiny `createSineTone` script; asserts marshalled Float32Array has expected length.

4. Plan 48-04: `flow-runtime.js` API surface freeze + HUMAN-UAT browser pass. Final ES module exports per D-48-13. HUMAN-UAT: load runtime in Chrome 120+ / Firefox 121+ / Safari 17+; run `(play (createSineTone 440Hz 1.0 0.5))`; hear audible tone; verify autoplay policy compliance (button click → audible). HUMAN-UAT rows tracked in 48-VERIFICATION.md.

5. Plan 48-05: Bundle size budget + (if needed) lazy-load stdlibs — if Plan 48-01 measured >15 MB, implement lazy-load for `@patterns`/`@generative`/`@improv`/`@notation-io`. `runtime.loadStdlib(name)` API + per-stdlib WASM module split. Acceptance: total bundle ≤15 MB compressed OR document the size + what was stripped to fit.

6. Plan 48-06: Two-run determinism + cross-browser regression suite. `Float32Array` byte-cmp at same source SHA. Acceptance: 5 canonical scripts (`createSineTone`, `renderSong` with patterns, `renderSong` with generative, `renderSong` with notation-io, MusicXML export) produce byte-identical Float32Array on Chrome + Firefox + Safari (modulo Lorenz/logistic — flagged with `flow-targets: not-deterministic-cross-platform` per D-36-09).

7. Plan 48-07: Closer — Phase 48 VERIFICATION + ROADMAP/STATE/REQUIREMENTS/CLAUDE.md sweep + handoff doc for Phase 49 (`flow-runtime.js` API contract, where to find the bundle, how to consume from SvelteKit).

</specifics>

<deferred>
## Deferred Ideas

- v1.6 AudioWorklet + SharedArrayBuffer ring-buffer streaming (D-48-02) for live-coding-in-browser. Requires Phase 49 to wire COOP/COEP headers (CF Pages supports natively); requires Phase 47 to UN-strip `live { }` blocks for Web. Multi-week stretch.
- v1.6 NativeAOT-LLVM via source-generator pass on `InternalFunctionRegistry` (per D-v1.5-02). Would let `flow-lang.dll` AOT-link, dropping bundle size 50%+. Source-gen authoring is non-trivial.
- v1.6 hand-rolled MIDI writer for Web if DryWetMidi WASM-incompatible (per D-48-17 fallback).
- v1.6 WebRTC DataChannel for OSC-shaped surface.
- v1.6 WebMIDI for live MIDI hardware playback.
- v1.6 IndexedDB persistence for saved Flow scripts (composer-local storage between sessions).
- v1.6 service worker for offline playground (cache-first PWA).

</deferred>
