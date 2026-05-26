# Phase 48 Research: WASM Runtime + WebAudioBackend

**Gathered:** 2026-05-25
**Status:** Ready for planning
**Source:** Research pass via gsd-phase-researcher (2026-05-25, pre-charter)

## Domain Context

Flow is an interpreted music language in C# / .NET 10. Today it renders audio via an `IAudioBackend` abstraction (PulseAudio on Linux via `libpulse-simple` P/Invoke, CoreAudio on macOS via `AudioToolbox` P/Invoke). Phase 48 builds `flow-lang.dll` under Mono-WASM (FlowTarget=Web, ready as of Phase 47) and ships a new `WebAudioBackend` that pushes rendered audio through the browser's `AudioContext`.

The single biggest feasibility unknown: how to get audio out of a .NET-in-WASM runtime into the browser's audio output.

## Key Research Findings

### Finding #1: No prior art for .NET/Mono-WASM driving AudioWorklet directly

Emscripten's [Wasm Audio Worklets API](https://emscripten.org/docs/api_reference/wasm_audio_worklets.html) is C/C++-only — explicitly notes worklets run in a Wasm Worker (not pthread). No equivalent exists for Mono-WASM. `[JSImport]`/`[JSExport]` interop is main-thread only ([dotnet/runtime#85592](https://github.com/dotnet/runtime/issues/85592)), so .NET code cannot run inside an `AudioWorkletProcessor`'s 3ms/128-frame budget.

**Conclusion:** v1 ships offline-render path, not AudioWorklet.

### Finding #2 (CANONICAL PATTERN): Offline render → `AudioBuffer` → `AudioBufferSourceNode`

This pattern matches Flow's existing composition → WAV buffer → play pipeline exactly:

1. Render the full song to a `Float32Array` in .NET (existing Flow renderer — runs in browser main thread).
2. Marshal the Float32Array once via `[JSExport]` to JS.
3. JS glue creates an `AudioBuffer`, calls `audioBuffer.copyToChannel(samples, 0/1)`.
4. JS glue creates an `AudioBufferSourceNode`, sets `node.buffer = audioBuffer`, connects to `audioContext.destination`, calls `node.start()`.

[Blazor.WebAudio](https://github.com/KristofferStrube/Blazor.WebAudio) (Kristoffer Strube) is the only published Blazor↔WebAudio wrapper and supports exactly this path. Phase 48 reuses the conceptual approach (NOT the library — we ship our own minimal glue to avoid Blazor lock-in).

### Finding #3: AudioWorklet streaming requires SharedArrayBuffer ring-buffer + COOP/COEP headers

The [Chrome WASM ring-buffer pattern](https://googlechromelabs.github.io/web-audio-samples/audio-worklet/design-pattern/wasm-ring-buffer/) shows: .NET produces samples on a worker thread, JS-side `AudioWorkletProcessor` (plain JS, not WASM-hosted .NET) reads from the SAB ring. Requires `Cross-Origin-Opener-Policy: same-origin` + `Cross-Origin-Embedder-Policy: require-corp` HTTP headers.

**v1.6 stretch goal.** Phase 49 wires the headers at CF Pages so the foundation is ready when v1.6 implements ring-buffer streaming.

`ScriptProcessorNode` is deprecated but works without COOP/COEP — viable fallback only, not target.

### Finding #4: ≤15 MB compressed bundle is plausible but tight for .NET 10 + jiterpreter

- .NET 9 framework bundle is ~2 MB Brotli'd after trimming ([WireFuture sizing](https://wirefuture.com/post/blazor-enhancements-in-net-9-performance-ssr-improvements)).
- Jiterpreter adds runtime memory cost not bundle size.
- Phase 29 sample bundle (3.05 MB) eats 20% — already stripped per Phase 47.
- Flow runtime (interpreter + stdlib `.flow` files + DryWetMidi) is the swing factor.

[.NET 9 WASM size regression #109787](https://github.com/dotnet/runtime/issues/109787) shows variance is real — budget should treat 15 MB as optimistic-target, not commitment. Plan a measurement step at Plan 48-01 dry-run. If exceeded, lazy-load Phase 36 stdlibs (`@patterns`, `@generative`, `@improv`) + Phase 39 (`@notation-io`) on-demand.

### Finding #5: Gotchas

- **GC pauses irrelevant for offline render.** Mono-WASM GC pauses are non-issue when entire song renders to Float32Array in one shot — render itself isn't real-time.
- **Autoplay policy requires user gesture.** `AudioContext.resume()` must be called inside a user-initiated event handler. Playground "Run" button's `onclick` handler must call both `runtime.run(source)` AND `audioContext.resume()` in the same call frame. No autoplay.
- **`[JSImport]`/`[JSExport]` marshalling multi-MB Float32Array is one-shot and fast.** Per-buffer streaming interop is the latency trap — avoid for v1.
- **P/Invoke audio backends fail to load on WASM.** Already handled by Phase 47 strip-list — `PulseAudio*Backend.cs`, `CoreAudioBackend.cs` excluded from Web build. `WebAudioBackend.IsAvailable()` returns `OperatingSystem.IsBrowser()` true under Mono-WASM (Phase 47 ships stub; Phase 48 fills in real impl).

## Sources

- [Wasm Audio Worklets API — Emscripten](https://emscripten.org/docs/api_reference/wasm_audio_worklets.html)
- [Blazor.WebAudio (KristofferStrube)](https://github.com/KristofferStrube/Blazor.WebAudio)
- [Multithreading + JS async interop in .NET 9 (dotnet/runtime#85592)](https://github.com/dotnet/runtime/issues/85592)
- [Blazor WASM multithreaded runtime (aspnetcore#54365)](https://github.com/dotnet/aspnetcore/issues/54365)
- [Ring Buffer in AudioWorkletProcessor — Chrome Labs](https://googlechromelabs.github.io/web-audio-samples/audio-worklet/design-pattern/wasm-ring-buffer/)
- [Blazor Enhancements in .NET 9 (size analysis)](https://wirefuture.com/post/blazor-enhancements-in-net-9-performance-ssr-improvements)
- [.NET 9 WASM size regression (dotnet/runtime#109787)](https://github.com/dotnet/runtime/issues/109787)
- [AudioWorklet design pattern — Chrome for Developers](https://developer.chrome.com/blog/audio-worklet-design-pattern/)
- [High Performance Web Audio with AudioWorklet — Mozilla Hacks](https://hacks.mozilla.org/2020/05/high-performance-web-audio-with-audioworklet-in-firefox/)
- Microsoft `[JSImport]`/`[JSExport]` interop docs (https://learn.microsoft.com/en-us/aspnet/core/blazor/javascript-interoperability/)

## Validation Architecture (Nyquist Dimension 8)

- **Build pipeline**: `dotnet publish flow-lang -p:FlowTarget=Web -c Release` produces `bin/Release/net10.0/browser-wasm/AppBundle/`. Plan 48-01 ships a smoke test asserting the publish step exits 0 and the output directory contains `flow-lang.wasm` + `flow-runtime.js`.
- **WebAudioBackend backend test**: Mono-WASM headless test runs in `dotnet test -p:FlowTarget=Web` — instantiates WebAudioBackend, asserts IsAvailable() returns true under browser-wasm, asserts PlayBuffer marshal contract (Float32Array length matches input).
- **HUMAN-UAT (3 rows)**: Chrome 120+ / Firefox 121+ / Safari 17+ manual smoke — `runtime.run('(play (createSineTone 440Hz 1.0 0.5))')` produces audible 440 Hz tone after Run-button gesture.
- **Bundle size CI gate**: `wc -c bin/Release/.../AppBundle/_framework/*` after Brotli-compress simulation; assert total ≤15 MB OR document strip needed.
- **Two-run determinism**: Same Flow source → byte-identical Float32Array output across `runtime.run()` calls (Plan 48-04).
- **Cross-browser parity (best-effort, not D-36-09 chaos primitives)**: 5 canonical scripts produce byte-identical Float32Array on Chrome + Firefox + Safari.

## Risk Summary

| Risk | Mitigation |
|------|-----------|
| DryWetMidi 8.0.3 WASM compile-time failure | Plan 48-02 ships 10-LOC smoke. **Pre-shipped by Phase 47-04 verification: DryWetMidi WASM smoke green on Desktop — Web smoke pending Phase 48 build pipeline.** |
| ≤15 MB bundle exceeded | Plan 48-05 lazy-loads Phase 36/39 stdlibs as separate JS bundle |
| Mono-WASM JIT failures on reflection-heavy `InternalFunctionRegistry` | `<TrimmerRootDescriptor>` preserves reflection roots; jiterpreter handles reflection at runtime cost (D-v1.5-02 reaffirmed) |
| First-paint UX broken by autoplay policy | First-gesture `AudioContext.resume()` wired in playground UI (Phase 49 ownership; Phase 48 documents the contract in flow-runtime.js API) |
| Cross-browser FP divergence on Lorenz/chaos primitives | D-36-09 carryforward — chaos primitives flagged not-cross-platform-deterministic |
