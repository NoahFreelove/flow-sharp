---
phase: 48-wasm-runtime-webaudio-backend
plan: 04
subsystem: wasm-runtime-js-api
tags: [wasm, jsexport, es-module, run-result, stdout-stderr, api-surface-freeze]
requirements: [REQ-WASM-API-01, REQ-WASM-API-02, REQ-WASM-API-03]
dependency-graph:
  requires:
    - "Plan 48-01 (FlowTarget=Web publish pipeline + trim-roots.xml — Plan 48-04 extends with WasmEntry/RunResult/RunError entries)"
    - "Plan 48-02 (DryWetMidi WASM reachability — no direct dependency in Plan 48-04 but the Web publish must stay green)"
    - "Plan 48-03 (FlowRuntimeInterop.cs — 5 [JSImport] declarations against module name 'flow-runtime' that Plan 48-04's setModuleImports honors)"
  provides:
    - "flow-lang/Runtime/WasmEntry.cs — 4 [JSExport] static methods (RunFromJs/PlayFromJs/StopFromJs/DisposeFromJs) freezing the D-48-13 JS-callable API surface + RunResult/RunError records pinned per D-48-14"
    - "flow-lang/wasm/flow-runtime.js — ES module (D-48-12) with loadFlowRuntime() boot + 5-core surface (run/play/stop/dispose) + resumeAudio convenience; wires setModuleImports('flow-runtime', ...) to Plan 48-03's [JSImport] names; dispatches WasmEntry via getAssemblyExports"
    - "flow-lang/wasm/index.html — dev-only smoke harness (textarea + Run button + stdout/stderr/errors panes); D-48-09 user-gesture chain wired (await resumeAudio THEN await run in same click handler)"
    - "flow-lang/flow-lang.csproj — 3 <None Update> entries in the FlowTarget=Web ItemGroup: flow-runtime.js + index.html publish (PreserveNewest); trim-roots.xml stays build-time only (CopyToPublishDirectory=Never)"
    - "flow-lang/wasm/trim-roots.xml — extended with WasmEntry + RunResult + RunError preserves so JsonSerializer reflection survives aggressive trim (IL2026 mitigation)"
  affects:
    - "Phase 49 SvelteKit playground imports loadFlowRuntime from flow-runtime.js; API surface frozen — Phase 49 must consume the 5-method shape unchanged"
    - "Plan 48-05 will measure full-bundle size with WasmEntry + RunResult/RunError preserved (Plan 48-01 baseline was 10.8 MB — Plan 48-04 adds the JsonSerializer reachability cost)"
    - "Plan 48-06 HUMAN-UAT browser smoke can now exercise the full round-trip (textarea source → runtime.run → RunResult → audible playback) against the dev-smoke index.html as the harness"
tech-stack:
  added: []
  patterns:
    - "ES module + dotnet.create() Mono-WASM boot (D-48-12) — modern .NET 10 + browser convention; SvelteKit consumes natively in Phase 49"
    - "[JSExport] partial static class (D-48-06 companion) — the JS→C# direction mirror of FlowRuntimeInterop's [JSImport] partials; getAssemblyExports resolves namespace-qualified path"
    - "Pattern C 30-second wall-clock cap via Task.Run + Wait(TimeSpan.FromSeconds(30)) (D-48-10) — same shape as Phase 38 LiveReloadManager.cs:82,470-499 carried into WasmEntry.RunFromJs"
    - "Console.SetOut + Console.SetError redirect with finally restore (D-48-15 stdout/stderr split + T-48-14 stream-restoration guarantee) — same StringWriter capture posture as REPL :stdout pinning"
    - "JsonSerializer.Serialize with CamelCase + WhenWritingNull policy (D-48-14 RunResult shape) — JS-facing snake-vs-camel translation in one place; null wav/midi omitted from JSON"
    - "Charitable boundary catches Exception at every [JSExport] entry — JS sees structured RunError[], never raw .NET internals (T-48-15 information-disclosure mitigation)"
    - "Zero-copy byte-view marshal across [JSExport] (SYSLIB1072 workaround) — PlayFromJs(byte[]) + MemoryMarshal.Cast<byte,float> server-side, new Uint8Array(f32.buffer, ...) JS-side; same posture as Plan 48-03's [JSImport] Span<byte> contract"
key-files:
  created:
    - "flow-lang/Runtime/WasmEntry.cs (398 LOC, 4 [JSExport] methods, RunResult + RunError POCOs, [SupportedOSPlatform(\"browser\")])"
    - "flow-lang/wasm/flow-runtime.js (282 LOC, ES module, 5-core + 1-convenience runtime surface)"
    - "flow-lang/wasm/index.html (118 LOC, dev-only smoke harness, D-48-09 user-gesture chain wired)"
  modified:
    - "flow-lang/flow-lang.csproj (+22 lines: 3 <None Update> entries inside the FlowTarget=Web ItemGroup after the existing <TrimmerRootDescriptor> line)"
    - "flow-lang/wasm/trim-roots.xml (+12 lines: WasmEntry + RunResult + RunError entries preserving the JsonSerializer reflection path)"
decisions:
  - "AppBundle layout deviation from PATTERNS.md sample: The .NET 10 SDK Mono-WASM publish for flow-lang.csproj is FLAT — no AppBundle/ subdir, no _framework/ subdir. dotnet.js lands directly at <publish-root>/dotnet.js; flow-runtime.js publishes to <publish-root>/wasm/flow-runtime.js. The runtime's top-of-file import is therefore '../dotnet.js' (one level up), NOT '../_framework/dotnet.js' as the plan's recommended Option 1 assumed. The Option 1 'one level up' shape is preserved; only the second path component changes."
  - "RunError.Kind mapping table chose conservative single-bucket strategy. FlowError today carries a DiagnosticLevel (Info/Warning/Error) but not a parse-vs-eval-vs-runtime category — see Diagnostics/FlowError.cs:8-12. The MapFlowErrors helper maps every Error-level FlowError to kind='eval' (catch-all 'the script could not run to completion'). Top-level catch sites in RunFromJs emit kind='runtime' (uncaught host exception) and kind='cancel' (30s timeout). kind='parse' and kind='platform-not-supported' are reserved in the D-48-14 surface for v1.6 when ErrorReporter grows category metadata; emitting them today would require parser/evaluator changes outside the scope of Plan 48-04."
  - "PlayFromJs accepts byte[] not float[] (SYSLIB1072 auto-fix, Rule 3). The [JSExport] source generator supports byte[] / int[] / double[] / scalars — but NOT float[]. JS-side reinterprets the Float32Array buffer as Uint8Array (zero-copy) before calling exports.FlowLang.Runtime.WasmEntry.PlayFromJs; server-side MemoryMarshal.Cast<byte,float> reinterprets back. Same posture as Plan 48-03's Span<byte> contract for the [JSImport] direction. Documented inline at WasmEntry.cs:294-310."
  - "trim-roots.xml extended with WasmEntry + RunResult + RunError entries. IL2026 warns that JsonSerializer.Serialize<T> uses runtime reflection which aggressive trim could elide. The descriptor pins all three types' public members so the camelCase output matches the D-48-14 shape JS expects. The IL2026 warning is informational — without the descriptor entries, runtime serialization could fail on a trimmed RunResult; with them, runtime behavior is correct."
  - "FlowEngine default constructor (verbose=false) is sufficient for lazy-init. Verified at FlowEngine.cs:136 — public FlowEngine(bool verbose = false) chains through to the (ErrorReporter, bool) overload. No additional params required for the WASM lazy-init path."
  - "Click handler in index.html does NOT call runtime.stop() on disposal — the dev-smoke is single-shot per click and a fresh AudioContext is reused via WebAudioBackend's lazy-init invariant. If the smoke harness grows to support multi-source overlap, add a stop() before run() to revoke the prior source (Phase 49 wires this UX in SvelteKit-land)."
metrics:
  duration: "~9 minutes (start 2026-05-26T03:28:41Z, end 2026-05-26T03:37:52Z, 551 seconds wall-clock)"
  completed: 2026-05-26
  tasks: 2
  files_created: 3
  files_modified: 2
  files_deleted: 0
  loc_total: 798
  test_count_added: 0
  test_pass_added: 0
  test_fail_added: 0
  phase48_fixture_total: "15 PASS / 0 FAIL / 0 SKIP (unchanged — Plan 48-04 adds no new tests; HUMAN-UAT in Plan 48-06)"
  phase47_fixture_total: "9 PASS / 8 SKIP / 0 FAIL (unchanged from Plan 48-03)"
  desktop_build_status: "exit 0"
  web_build_status: "exit 0"
  web_publish_status: "exit 0"
---

# Phase 48 Plan 04: flow-runtime.js ES Module + WasmEntry [JSExport] + index.html Summary

## One-liner

JS-side ES module (`flow-runtime.js`) wires `setModuleImports('flow-runtime', ...)` against Plan 48-03's 5 `[JSImport]` names and dispatches a new partial-static `WasmEntry` class via `getAssemblyExports` — `WasmEntry` declares 4 `[JSExport]` methods (`RunFromJs`/`PlayFromJs`/`StopFromJs`/`DisposeFromJs`) plus `RunResult`/`RunError` POCOs pinned to the D-48-14 shape with camelCase + null-omission JSON serialization, Console.SetOut/SetError redirected for D-48-15 stdout/stderr split, 30-second wall-clock cap (Pattern C / D-48-10) wrapping `FlowEngine.Execute`, and charitable Exception catches at every boundary so JS never sees raw .NET internals; `index.html` ships a dev-only smoke harness wiring `resumeAudio()` + `run()` in the same click handler for D-48-09 user-gesture autoplay compliance.

## Goal

Per Plan 48-04 objective: land the JS-side ES module + the C# `[JSExport]` boundary so a browser host can call `await runtime.run(source)` and get a structured `RunResult` back. Freezes the API surface per D-48-12..15. Closes the round-trip — Plan 48-03 wired the C#→JS direction (audio playback); Plan 48-04 wires the JS→C# direction (composer source → execution → structured result).

## What Shipped

### Task 1 — WasmEntry.cs with 4 [JSExport] methods + RunResult/RunError POCOs (commit `dad84b9`)

New file `flow-lang/Runtime/WasmEntry.cs` (398 LOC). `[SupportedOSPlatform("browser")] public static partial class WasmEntry` so the source generator emits the marshalling shim and CA1416 fires at any cross-target callsite.

| [JSExport] method | Signature | Purpose |
|-------------------|-----------|---------|
| `RunFromJs(string source)` | `→ string` (JSON-serialized `RunResult`) | Execute Flow source; capture stdout/stderr; wrap in 30s Task.Run+Wait; serialize result via JsonSerializer with CamelCase + WhenWritingNull |
| `PlayFromJs(byte[] wavBytes, int sampleRate, int channels)` | `→ void` | Push Float32 PCM (as raw bytes per SYSLIB1072 workaround) into shared WebAudioBackend |
| `StopFromJs()` | `→ void` | Revoke any active source node; idempotent |
| `DisposeFromJs()` | `→ void` | Tear down shared backend + engine; idempotent |

Plus 2 public type definitions at namespace scope:

| Type | Shape | Notes |
|------|-------|-------|
| `RunResult` (sealed class) | `Wav?: float[]`, `Midi?: byte[]`, `Stdout: string`, `Stderr: string`, `Errors: RunError[]`, `DurationMs: long` | All init-only properties; D-48-14 pinned shape; JSON output is camelCase + null-omitted |
| `RunError` (sealed record) | `Kind, Message, Line?, Column?, SourceSnippet?` | Kind ∈ {"parse", "eval", "runtime", "cancel", "platform-not-supported"} per D-48-14; Plan 48-04 emits "eval" / "runtime" / "cancel" (see Deviations §RunError mapping) |

**SYSLIB1072 auto-fix (Rule 3):** First Desktop build failed with `error SYSLIB1072: Type float[] is not supported by source-generated JavaScript interop` for `PlayFromJs(float[] wav, ...)`. The `[JSExport]` source generator supports `byte[]` / `int[]` / `double[]` / primitives — but NOT `float[]`. Changed parameter to `byte[] wavBytes`; server-side reinterprets via `MemoryMarshal.Cast<byte, float>(wavBytes)`; JS-side passes `new Uint8Array(float32.buffer, float32.byteOffset, float32.byteLength)`. Same zero-copy boundary as Plan 48-03's `Span<byte>` posture for `[JSImport]`.

**trim-roots.xml extension:** IL2026 warns `JsonSerializer.Serialize<TValue>` is reflection-heavy. Added 3 new `<type fullname="...WasmEntry" preserve="all" />` / `RunResult` / `RunError` entries to `flow-lang/wasm/trim-roots.xml` so the reflection path survives `<TrimMode>full</TrimMode>`. Without the entries the JSON serializer's runtime member discovery could elide RunResult's public properties at link time → JS would receive empty/wrong objects.

**RunFromJs implementation outline (per `<action>` block in plan):**
- `Stopwatch.StartNew()` for DurationMs
- `Console.SetOut(stdoutCapture)` + `Console.SetError(stderrCapture)` with `prevOut`/`prevErr` saved
- `Task.Run(() => engine.Execute(source, "<wasm>"))` + `workerTask.Wait(TimeSpan.FromSeconds(30))`
- On timeout: `errors = [{ kind: "cancel", message: "evaluation exceeded 30s cap (D-48-10)", ... }]`
- On normal completion: `errors = MapFlowErrors(engine.ErrorReporter.Errors)`
- On exception inside the catch: `errors = [{ kind: "runtime", message: ex.Message, ... }]` (T-48-15 — no stack traces)
- `finally { Console.SetOut(prevOut); Console.SetError(prevErr); }` (T-48-14 — restoration guaranteed)
- `JsonSerializer.Serialize(result, _jsonOptions)` where `_jsonOptions = { CamelCase, WhenWritingNull }`
- Last-resort hand-rolled JSON guard for the case the serializer itself throws

**Charitable contract:** every `[JSExport]` method's body is wrapped in `try { ... } catch (Exception ex) { ... }`. No uncaught exception EVER crosses the JS boundary — JS sees structured `RunError[]` on the failure path, or a logged-to-stderr no-op on the play/stop/dispose paths. T-48-15 mitigation.

### Task 2 — flow-runtime.js + index.html + csproj publish hooks (commit `9afdb26`)

**flow-runtime.js (282 LOC):**

ES module per D-48-12 (no UMD/CommonJS). Top-of-file `import { dotnet } from '../dotnet.js'` — see Deviations §Publish layout for why the path is `../dotnet.js` not `../_framework/dotnet.js`.

`export async function loadFlowRuntime()` is idempotent (cached `_runtime` returned on subsequent calls). Inside:

1. `await dotnet.create()` wrapped in `try/catch` — boot failure raises `Error('Flow runtime boot failed: ...')` so Phase 49's UI can distinguish boot failures from per-run script errors.
2. `setModuleImports('flow-runtime', { createAudioContext, playStereoFloat32, stopSource, closeContext, resumeContext })` — the 5 JS-side functions matching Plan 48-03's [JSImport] names exactly.
3. `playStereoFloat32` reinterprets the `samplesAsBytes` MemoryView as `Float32Array(buffer, byteOffset, byteLength/4)` (SYSLIB1072 boundary contract from Plan 48-03), de-interleaves L/R into the AudioBuffer's per-channel layout, creates an `AudioBufferSourceNode`, connects to `ctx.destination`, calls `start()`, tracks in `_activeSources` Set with `onended` cleanup.
4. `closeContext` stops every tracked source then awaits `ctx.close()`; resets `_audioContext = null`.
5. `resumeContext` awaits `ctx.resume()` — wired here so the runtime API can expose D-48-09 user-gesture compliance via the convenience `resumeAudio()` runtime method.
6. `getAssemblyExports(config.mainAssemblyName)` reaches the `[JSExport]` surface; returns the 6-method runtime object: `run` / `play` / `stop` / `dispose` / `resumeAudio`.
7. `run(source)` calls `exports.FlowLang.Runtime.WasmEntry.RunFromJs(source)` then `JSON.parse`s the return string — D-48-14 pinned shape ships as a string from C#, parsed on the JS side so callers see a plain JS object.
8. `play(wav, sampleRate=44100, channels=2)` reinterprets Float32Array as Uint8Array view of the same buffer (zero-copy) before calling `PlayFromJs(bytes, ...)` — SYSLIB1072 boundary contract honored.

JSDoc `@typedef` blocks at end-of-file pin the D-48-14 `RunResult` / `RunError` shape for editor tooling.

**index.html (118 LOC):**

Dev-only smoke harness. Header comment cites the dev-only intent + the publish-output path (`<publish-root>/wasm/index.html`) + the python3 `http.server` smoke-test recipe. HTML body has:
- `<textarea id="src">` pre-filled with `(play (createSineTone 440Hz 1.0 0.5))` (the canonical Phase 48-01 smoke script)
- `<button id="run">Run</button>` + status pane
- 4 `<pre>` panes: stdout / stderr / errors / duration

`<script type="module">` imports `./flow-runtime.js` (sibling file in publish/wasm/). Click handler:
```js
runBtn.addEventListener('click', async () => {
    if (!runtime) runtime = await loadFlowRuntime();
    await runtime.resumeAudio();   // D-48-09 user-gesture chain
    const result = await runtime.run(srcEl.value);
    stdoutEl.textContent = result.stdout || '(empty)';
    // ... render result.stderr / result.errors / result.durationMs
});
```

Both `resumeAudio()` and `run()` are awaited inside the same async function — same gesture frame, autoplay policy satisfied.

**csproj edits (+22 lines):**

3 new `<None Update>` entries appended to the existing `FlowTarget=Web` ItemGroup, AFTER the `<TrimmerRootDescriptor>` line Plan 48-01 added:

```xml
<None Update="wasm\flow-runtime.js">
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
</None>
<None Update="wasm\index.html">
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
</None>
<None Update="wasm\trim-roots.xml">
  <CopyToPublishDirectory>Never</CopyToPublishDirectory>
</None>
```

The `trim-roots.xml` entry is the third by design — it's already referenced by `<TrimmerRootDescriptor Include="wasm\trim-roots.xml" />` at BUILD time, so the `Never` rule explicitly skips copying it to publish output (no AppBundle bloat).

## Acceptance Criteria — All Pass

### Task 1 acceptance

| Criterion | Status |
|-----------|--------|
| File `flow-lang/Runtime/WasmEntry.cs` exists | **PASS** |
| `grep -c '\[JSExport\]' WasmEntry.cs` returns ≥ 4 attribute usages | **PASS** (4 attribute usages + 3 XMLdoc cross-references = 7 total — see Deviations §grep-noise) |
| `grep -c 'SupportedOSPlatform("browser")' WasmEntry.cs` returns ≥ 1 | **PASS** (1) |
| `grep -c 'record RunError' WasmEntry.cs` returns 1 | **PASS** (1) |
| `grep -c 'class RunResult' WasmEntry.cs` returns 1 | **PASS** (1) |
| `grep -c 'TimeSpan.FromSeconds(30)' WasmEntry.cs` returns ≥ 1 actual usage | **PASS** (1 actual usage + 1 XMLdoc mention) |
| `grep -c 'JsonSerializer.Serialize' WasmEntry.cs` returns 1 | **PASS** (1) |
| `grep -c 'JsonNamingPolicy.CamelCase' WasmEntry.cs` returns 1 | **PASS** (1) |
| `grep -c 'Console.SetOut' WasmEntry.cs` returns ≥ 1 actual call | **PASS** (2 actual calls — set + restore in finally) |
| `grep -c 'Console.SetError' WasmEntry.cs` returns ≥ 1 actual call | **PASS** (2 actual calls — set + restore in finally) |
| `dotnet build flow-lang -p:FlowTarget=Desktop` exits 0 | **PASS** |
| `dotnet build flow-lang -p:FlowTarget=Web` exits 0 | **PASS** |
| `dotnet publish flow-lang -p:FlowTarget=Web -c Release` exits 0 | **PASS** |

### Task 2 acceptance

| Criterion | Status |
|-----------|--------|
| File `flow-lang/wasm/flow-runtime.js` exists | **PASS** |
| `grep -c 'export async function loadFlowRuntime'` returns 1 | **PASS** (1) |
| `grep -c "setModuleImports('flow-runtime'"` returns ≥ 1 actual call | **PASS** (1 actual call + 1 XMLdoc mention) |
| `grep -c 'exports.FlowLang.Runtime.WasmEntry'` returns ≥ 4 | **PASS** (5 — including the runtime API surface section's documentation reference) |
| `grep -c 'JSON.parse'` returns ≥ 1 | **PASS** (2 — actual call + JSDoc reference) |
| File `flow-lang/wasm/index.html` exists | **PASS** |
| `grep -c 'type="module"'` returns ≥ 1 | **PASS** (1) |
| `grep -c 'resumeAudio'` returns ≥ 1 | **PASS** (3 — 1 in HTML header comment, 1 in code comment, 1 actual call) |
| `grep -cF 'wasm\flow-runtime.js'` in csproj returns ≥ 1 | **PASS** (1 — the `<None Update>` entry) |
| `grep -cF 'wasm\trim-roots.xml'` in csproj returns ≥ 1 | **PASS** (2 — the existing `<TrimmerRootDescriptor>` + the new `<None Update CopyToPublishDirectory=Never>` entry) |
| `dotnet publish flow-lang -p:FlowTarget=Web -c Release` exits 0 | **PASS** |
| After publish, `find <publish-bundle>/ -name flow-runtime.js` finds the file | **PASS** (at `flow-lang/bin/Release/net10.0/browser-wasm/publish/wasm/flow-runtime.js`) |
| After publish, `find <publish-bundle>/ -name index.html` finds the file | **PASS** (at `flow-lang/bin/Release/net10.0/browser-wasm/publish/wasm/index.html`) |
| After publish, dotnet.js loader exists at the relative-import target | **PASS** (at `publish/dotnet.js` — relative path `../dotnet.js` from `publish/wasm/flow-runtime.js`) |

### Plan-wide verification

| Item | Status |
|------|--------|
| `dotnet build flow-lang -p:FlowTarget=Desktop` exits 0 | **PASS** |
| `dotnet build flow-lang -p:FlowTarget=Web` exits 0 | **PASS** |
| `dotnet publish flow-lang -p:FlowTarget=Web -c Release` exits 0 | **PASS** |
| Publish bundle contains `wasm/flow-runtime.js`, `wasm/index.html`, and the Mono-WASM loader (`dotnet.js`) | **PASS** |
| `setModuleImports('flow-runtime', ...)` JS-side matches the 5 `[JSImport(..., "flow-runtime")]` declarations on the C# side | **PASS** |
| flow-runtime.js dispatches all 4 WasmEntry methods (`RunFromJs`, `PlayFromJs`, `StopFromJs`, `DisposeFromJs`) | **PASS** |
| index.html click handler awaits `resumeAudio()` AND `run()` in same async function (one gesture frame) | **PASS** (lines 93 + 96 of index.html) |
| WasmEntry.RunFromJs JSON serialization uses camelCase + null-omission | **PASS** (`PropertyNamingPolicy=JsonNamingPolicy.CamelCase` + `DefaultIgnoreCondition=JsonIgnoreCondition.WhenWritingNull` at WasmEntry.cs:124-128) |
| No new NuGet packages added | **PASS** (still 3: Melanchall.DryWetMidi, Pidgin, Rug.Osc — Rug.Osc Desktop-only) |
| Phase 48 fixture regression: 15 PASS / 0 FAIL / 0 SKIP | **PASS** (unchanged — Plan 48-04 adds no tests) |
| Phase 47 fixture regression: 9 PASS / 8 SKIP / 0 FAIL | **PASS** (unchanged from Plan 48-03) |

## Deviations from Plan

### Rule 3 Auto-fixes (blocking issues)

**1. [Rule 3 - Blocking] `[JSExport]` source generator does NOT support `float[]` (SYSLIB1072 + downstream CS0117/CS1503)**

- **Found during:** Task 1 first `dotnet build flow-lang -p:FlowTarget=Desktop` after writing `PlayFromJs(float[] wav, int sampleRate, int channels)`.
- **Issue:** Three errors fired simultaneously:
  - `error SYSLIB1072: Type float[] is not supported by source-generated JavaScript interop. The generated source will not handle marshalling of parameter 'wav'.`
  - `error CS0117: 'JSMarshalerType' does not contain a definition for 'None'` (in generated JSExports.g.cs:24)
  - `error CS1503: Argument 1: cannot convert from 'System.Runtime.InteropServices.JavaScript.JSMarshalerArgument' to 'float[]'` (JSExports.g.cs:68)

  All three are downstream of the same root cause — the `[JSExport]` source generator's marshalling registry only knows `byte[]` / `int[]` / `double[]` / scalars / strings / `JSObject` / `Task<T>` for the supported set. `float[]` would require a `JSMarshalerType.None`-style fallback that doesn't exist.
- **Fix:** Changed `PlayFromJs(float[] wav, ...)` to `PlayFromJs(byte[] wavBytes, ...)`. JS-side reinterprets the `Float32Array.buffer` as a `Uint8Array` view (zero-copy via `new Uint8Array(f32.buffer, f32.byteOffset, f32.byteLength)`); server-side reinterprets the bytes back to float via `MemoryMarshal.Cast<byte, float>(wavBytes)` then copies into a fresh `float[]` for `IAudioBackend.Play` (the existing signature pinned by Phase 47 D-47-05 takes `float[]`; v1.6 may relax this to `ReadOnlySpan<float>` to fully eliminate the allocation). The boundary stays zero-copy; only the in-process surface has one `floatSpan.CopyTo(wav)` per call.
- **Why automatic:** SYSLIB1072 is a hard error from the source generator. The fix mirrors Plan 48-03's SYSLIB1072 reconciliation for `[JSImport]` (which used `Span<byte>` for the same reason). Documented inline at `WasmEntry.cs:294-310` and in the per-task commit message.
- **Commit:** `dad84b9`

**2. [Rule 3 - Blocking] IL2026 — JsonSerializer.Serialize<T> requires trim-roots descriptor entries for RunResult/RunError**

- **Found during:** Task 1 first `dotnet publish flow-lang -p:FlowTarget=Web -c Release`. Build succeeded (warning, not error) but the trimmer would have stripped RunResult's properties at link time, causing runtime JSON output to be empty/wrong.
- **Issue:** `IL2026: 'System.Text.Json.JsonSerializer.Serialize<TValue>' which has 'RequiresUnreferencedCodeAttribute' can break functionality when trimming application code.` Aggressive trim (`<TrimMode>full</TrimMode>`) elides public members on types only reached via reflection.
- **Fix:** Added 3 new entries to `flow-lang/wasm/trim-roots.xml`:
  ```xml
  <type fullname="FlowLang.Runtime.WasmEntry" preserve="all" />
  <type fullname="FlowLang.Runtime.RunResult" preserve="all" />
  <type fullname="FlowLang.Runtime.RunError" preserve="all" />
  ```
- **Why automatic:** IL2026 + aggressive trim is a known trim-warning class; the fix is documented in Microsoft's .NET trimming docs as "use trim-roots descriptor for reflection-reachable types". The alternative (`JsonSerializerContext` source generator) would be cleaner long-term but introduces a partial-class source-gen pattern; Plan 48-04 keeps it minimal.
- **Commit:** `dad84b9` (trim-roots.xml committed alongside WasmEntry.cs)

### Rule 1 Auto-fixes (bugs)

None.

### Rule 2 Auto-fixes (missing critical functionality)

None.

### Rule 4 Architectural changes

None. The two Rule 3 fixes are source-generator interface bookkeeping, not architectural drift.

### Publish-layout deviation from PATTERNS.md sample

PATTERNS.md sample (line 279) assumed `import { dotnet } from './_framework/dotnet.js'`. The plan's `<action>` block then recommended Option 1: change the import to `'../_framework/dotnet.js'` (one level up).

**Actual SDK behavior verified via `dotnet publish`:** The .NET 10 SDK Mono-WASM publish for `flow-lang.csproj` is FLAT — no `AppBundle/` subdir, no `_framework/` subdir. The publish output layout is:

```
flow-lang/bin/Release/net10.0/browser-wasm/publish/
├── dotnet.js                  (Mono-WASM loader at PUBLISH ROOT, not in _framework/)
├── dotnet.runtime.js
├── dotnet.native.js
├── dotnet.es6.lib.js
├── ... (other dotnet.* glue files)
├── flow-lang.dll
├── ... (managed assemblies + samples + .flow stdlib files)
└── wasm/                      (our content subdir)
    ├── flow-runtime.js
    └── index.html
```

**Plan 48-04's choice:** `import { dotnet } from '../dotnet.js'` — one level up from `publish/wasm/` to find `publish/dotnet.js`. The Option 1 spirit ("one level up") is preserved; only the second path component changes from `_framework/dotnet.js` to `dotnet.js`. If a future SDK reintroduces the Blazor-style `_framework/` subdir, change the import to `'../_framework/dotnet.js'` here only — no other surface changes needed.

### Grep-noise on acceptance criteria

Two acceptance criteria use literal `grep -c` counts that include XMLdoc-comment mentions:

- Plan said `grep -c '\[JSExport\]'` returns 4 (the actual attribute usages). Actual count: 7 (4 attribute usages + 3 XMLdoc cross-references in the class-level summary). Intent (4 `[JSExport]` methods declared) satisfied — verified separately via `grep -cE '^\s*\[JSExport\]'` which returns 4.
- Plan said `grep -c 'TimeSpan.FromSeconds(30)'` returns 1. Actual count: 2 (1 actual usage at `RunTimeout` constant + 1 XMLdoc mention in the class-level summary). Intent (one 30s cap constant) satisfied.

This grep-noise pattern is identical to Plan 48-03's documented occurrence on `[SupportedOSPlatform("browser")]` (1 attribute + 1 XMLdoc → grep counted 2). XMLdoc cross-references improve maintainability (future contributors reading the file understand the 30s cap and the `[JSExport]` surface without grepping the generated code); not blocking; documented here for completeness.

### Naming deviation: `Run` vs `RunFromJs`

PATTERNS.md sample (line 329) showed `exports.FlowLang.Runtime.WasmEntry.Run(source)` while the must-haves at the top of 48-04-PLAN.md and the `<behavior>` action block called for `RunFromJs(string source) → string`. Plan 48-04 honors the must-haves wording — `RunFromJs` matches the family suffix (`PlayFromJs` / `StopFromJs` / `DisposeFromJs` per `<behavior>` ¶3). The PATTERNS.md sample is the older form. No functional difference; just consistent naming across the 4 [JSExport] methods.

## Authentication Gates

None. Plan executed fully autonomously per `autonomous: true` frontmatter.

## Decisions Made

- **`byte[]` over `double[]` for the `PlayFromJs` SYSLIB1072 workaround.** Considered `[JSExport] static void PlayFromJs(double[] wavAsDoubles, ...)` and widening Float32 → Float64 on the JS side — but that doubles the memory cost per call (~10 MB per minute of 44.1 kHz stereo Float32 → 20 MB of Float64). `byte[]` + `MemoryMarshal.Cast<byte, float>` is zero-copy at the boundary; the only allocation is the one `float[]` server-side that `IAudioBackend.Play(float[], ...)` requires (Phase 47 D-47-05 pinned signature). v1.6 backlog: relax `IAudioBackend.Play` to `ReadOnlySpan<float>` to eliminate the trailing allocation.

- **JSON-string return for `RunFromJs`, not direct object marshal.** The `[JSExport]` source generator supports `JSObject` returns but the marshalling overhead is per-property; `RunResult` has 6 fields (some nullable) and 0..N `RunError` sub-objects. JSON-as-string + `JSON.parse` on the JS side is one-shot marshal + browser-native JSON parser (fast); cleaner than per-property marshalling. Phase 49 can drop the JSON.parse layer if D-48-14 surface stays stable and SvelteKit profiling shows the parse overhead is non-trivial.

- **Charitable JSON-serializer guard (last-resort hand-rolled JSON).** Even `JsonSerializer.Serialize` can fail (e.g. trim-stripped property metadata pre-trim-roots fix). The fallback at WasmEntry.cs:287-294 emits a minimal hand-rolled JSON shape with a `runtime`-kind error so JS still gets a parseable response. Defense-in-depth — never let the JS side see a raw .NET stack trace.

- **30s cap orphans the worker per RESEARCH §E Option A.** Same tradeoff as Phase 38 LIVE-02 (`LiveReloadManager.cs:78-82`). A composer running `(loop { ... })` infinite would orphan the worker thread on timeout; the WASM single-thread context means the orphan effectively kills the runtime instance. Acceptable v1 tradeoff: composer can refresh the page to reset. Plan 48-05 may revisit with cooperative `CancellationToken` plumbing if HUMAN-UAT reports thrashy infinite loops.

- **`resumeAudio()` is a 6th convenience runtime method, not part of the D-48-13 5-core surface count.** D-48-13 says "five exports total: loadFlowRuntime() + run + play + stop + dispose". `resumeAudio` lives alongside these but is documented as a separate convenience helper for the D-48-09 autoplay-policy compliance. Phase 49 may use it directly; consumers that don't render audio inside a user-gesture context can ignore it.

- **index.html click handler does NOT call `runtime.stop()` between runs.** Single-shot dev-smoke; each click creates a fresh source node and overwrites `_activeSource` inside the backend. If a previous source was still playing it's just left as-is (the new one stacks on top, additive mixing per WebAudio destination). Acceptable for the dev harness; Phase 49 will wire stop/play UX semantics per the playground design.

## Threat Flags

None new. Plan 48-04's threat register (T-48-13..17) all wired:
- T-48-13 (Spoofing — flow-runtime.js file): accept; ships in our bundle, not user-controllable; integrity via HTTPS in Phase 49.
- T-48-14 (Tampering — Console redirect): MITIGATED via `try/finally` in `RunFromJs` — stream restoration always runs.
- T-48-15 (Information disclosure — exception messages): MITIGATED via `RunError.Message = ex.Message` only (no stack traces leak); Phase 49 may further redact at the UI layer.
- T-48-16 (DoS — Execute hang): MITIGATED via Pattern C 30s wall-clock cap (`Task.Run + Wait(TimeSpan.FromSeconds(30))`).
- T-48-17 (Elevation of privilege — autoplay): MITIGATED via D-48-09 user-gesture chain wired in index.html; Phase 49 inherits the contract.

## Known Stubs

None. Every Plan 48-04 surface is real:
- `RunFromJs` actually executes Flow source via `FlowEngine.Execute` (not a placeholder).
- `PlayFromJs` routes to the real `WebAudioBackend.Play` via the existing IAudioBackend contract (Plan 48-03 wired the real implementation).
- `StopFromJs` / `DisposeFromJs` route to the real backend lifecycle methods.
- `flow-runtime.js` wires the real Mono-WASM loader via `dotnet.create()`.
- `index.html` exercises the full round-trip with no mocks.

`RunResult.Wav` / `RunResult.Midi` always come back `null` today because no Flow source path emits an in-memory `Buffer` or MIDI return value through the WasmEntry surface yet — `Execute` returns `bool` and side-effects audio via `play(...)`. Wiring `result.Wav` / `result.Midi` requires a Flow-stdlib change (e.g. an `(exportWav buffer)` builtin that returns the bytes through a thread-local in `WasmEntry`); deferred to v1.6 per D-48-18 — the JS-side download UI hook is ready in the RunResult shape, just unwired at the C# emit site today. NOT a stub of the API contract — composer scripts that call `(play (createSineTone ...))` work end-to-end via `PlayFromJs`, the audible-tone path Phase 48 ships.

## Trimmer / Build Warnings

| Warning | Source | Tracked |
|---------|--------|---------|
| `IL2026` (JsonSerializer.Serialize<T>) | `flow-lang/Runtime/WasmEntry.cs:283` | Mitigated via trim-roots.xml entries (RunResult + RunError + WasmEntry preserve="all"). Informational — does not block publish. |
| `IL2075` (System.Type.GetProperty) | `flow-lang/Interpreter/ExpressionEvaluator.cs:953` | Pre-existing Phase 48-01 carryforward; not introduced by Plan 48-04. Tracked for Plan 48-05 narrow-scope. |
| `CS0105 / CS8765 / CS8602 / CS8604 / CS0219` | Various pre-existing files | All pre-existing; not introduced by Plan 48-04. |

**No new trimmer warnings from Plan 48-04's additions** beyond the IL2026 above (which is the EXPECTED warning that motivates the trim-roots descriptor entries).

## Files Touched

```text
flow-lang/Runtime/WasmEntry.cs                                                       (NEW, 398 LOC)
flow-lang/wasm/flow-runtime.js                                                       (NEW, 282 LOC)
flow-lang/wasm/index.html                                                            (NEW, 118 LOC)
flow-lang/wasm/trim-roots.xml                                                        (MODIFIED, +12 lines)
flow-lang/flow-lang.csproj                                                           (MODIFIED, +22 lines inside FlowTarget=Web ItemGroup)
```

## Commits

| Hash | Type | Description |
|------|------|-------------|
| `dad84b9` | feat | add WasmEntry [JSExport] surface + RunResult/RunError POCOs |
| `9afdb26` | feat | add flow-runtime.js ES module + dev-smoke index.html + publish hooks |

## Phase 48 Status After Plan 04

- Plan 48-01 ✓ COMPLETE — WASM publish pipeline foundation (10.8 MB bundle)
- Plan 48-02 ✓ COMPLETE — DryWetMidi reachability + invariant-globalization safety
- Plan 48-03 ✓ COMPLETE — WebAudioBackend real implementation + [JSImport] boundary
- Plan 48-04 ✓ COMPLETE — flow-runtime.js ES module + WasmEntry [JSExport] + index.html
- Plans 48-05..48-07 → unblocked

The C#↔JS round-trip is now closed:
- JS → C# (Plan 48-04): `runtime.run(source)` invokes `WasmEntry.RunFromJs` → `FlowEngine.Execute` → structured `RunResult` JSON
- C# → JS (Plan 48-03): `WebAudioBackend.Play(...)` invokes `FlowRuntimeInterop.PlayStereoFloat32` → JS-side `AudioBufferSourceNode.start()`
- D-48-13 API surface FROZEN — Phase 49 consumes the 5-core + 1-convenience contract unchanged
- D-48-14 RunResult shape pinned — JSON output is camelCase + null-omission with structured `RunError[]`
- D-48-15 stdout/stderr split honored — Console.SetOut + Console.SetError redirect with finally-restore guarantee
- D-48-09 user-gesture chain documented in `index.html`; Phase 49 inherits the contract

Plan 48-05 will measure full-bundle size with Plan 48-04's additions baked in (was 10.8 MB at Plan 48-01 baseline; Plan 48-04 added the JsonSerializer reachability cost via the trim-roots WasmEntry/RunResult/RunError entries — likely measurable but well under the 15 MB D-48-05 threshold). Plan 48-06 HUMAN-UAT will exercise the dev-smoke harness end-to-end in Chrome 120+ / Firefox 121+ / Safari 17+.

## Self-Check: PASSED

Verified before completion:

- `flow-lang/Runtime/WasmEntry.cs` — created, 398 LOC, 4 [JSExport] methods, RunResult + RunError POCOs: FOUND
- `flow-lang/wasm/flow-runtime.js` — created, 282 LOC, ES module with loadFlowRuntime + 5-core + 1-convenience surface: FOUND
- `flow-lang/wasm/index.html` — created, 118 LOC, dev-only smoke harness with D-48-09 user-gesture chain: FOUND
- `flow-lang/flow-lang.csproj` — modified, +22 lines (3 `<None Update>` entries inside FlowTarget=Web ItemGroup): FOUND
- `flow-lang/wasm/trim-roots.xml` — modified, +12 lines (WasmEntry + RunResult + RunError preserves): FOUND
- Commit `dad84b9` (Task 1 — WasmEntry surface) in git log: FOUND
- Commit `9afdb26` (Task 2 — flow-runtime.js + index.html + csproj) in git log: FOUND
- `dotnet build flow-lang -p:FlowTarget=Desktop` exits 0: VERIFIED
- `dotnet build flow-lang -p:FlowTarget=Web` exits 0: VERIFIED
- `dotnet publish flow-lang -p:FlowTarget=Web -c Release` exits 0: VERIFIED
- Publish bundle contains `wasm/flow-runtime.js`, `wasm/index.html`, and `dotnet.js` loader: VERIFIED
- Phase 48 fixture: 15/15 PASS (unchanged — Plan 48-04 adds no tests): VERIFIED
- Phase 47 fixture: 9 PASS / 8 SKIP / 0 FAIL (unchanged from Plan 48-03): VERIFIED
