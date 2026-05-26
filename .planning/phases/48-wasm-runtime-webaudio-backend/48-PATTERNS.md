# Phase 48: WASM Runtime + WebAudioBackend — Pattern Map

**Mapped:** 2026-05-25
**Files analyzed:** 8 (3 new, 2 modified, 3 test)
**Analogs found:** 7 / 8 (one new convention — JS ES module — has no in-repo precedent)

## Discrepancies vs CONTEXT.md / RESEARCH.md

Three load-bearing discrepancies the planner must reconcile before writing PLAN.md files:

### Discrepancy 1: `AudioBuffer` field is `Data`, not `Samples`

CONTEXT.md repeatedly references `AudioBuffer.Samples` (line 100-104). Actual codebase definition at `flow-lang/StandardLibrary/Audio/AudioCore.cs:10-44`:

```csharp
public class AudioBuffer
{
    public float[] Data { get; }       // <-- NOT "Samples"
    public int SampleRate { get; }
    public int Channels { get; }
    public int Frames { get; }
}
```

Plan 48-03's `WebAudioBackend.Play(AudioBuffer)` marshal site MUST use `.Data`, not `.Samples`. Every existing call site in the codebase reads `.Data` — Plan 48-03 inherits the naming. The CONTEXT.md text is informally referring to the float buffer; the property is `Data`.

### Discrepancy 2: `IAudioBackend.Play` takes `float[]`, not `AudioBuffer`

CONTEXT.md D-48-01 step 3 says "marshal AudioBuffer.Samples to JS"; D-47-05's stub pins `Play(float[] samples, int sampleRate, int channels, CancellationToken)` — see `flow-lang/Audio/WebAudioBackend.cs:47-48` + `flow-lang/Audio/IAudioBackend.cs:21-25`. The signature is `Play(float[] samples, int sampleRate, int channels, CancellationToken cancellationToken = default)`. The `AudioPlaybackManager` does NOT wrap an `AudioBuffer` — the underlying backend deals in raw float arrays. Plan 48-03's [JSImport] marshal site sends `float[]` + sample rate + channels metadata across the boundary; the `AudioBuffer` is unpacked at the `PlaybackFunctions.cs:81` caller seam BEFORE entering `WebAudioBackend.Play`. Phase 48 does NOT change the IAudioBackend signature — D-47-05 pinned it.

### Discrepancy 3: `InternalFunctionRegistry` is NOT reflection-heavy

CONTEXT.md repeats (lines 13, 41, 113) that `InternalFunctionRegistry` uses `Type.GetMethods()` discovery and needs `<TrimmerRootDescriptor>` preservation. **Verified false** — `grep` for `GetMethods`, `BindingFlags`, `Activator.CreateInstance`, `Type.GetType` across `flow-lang/StandardLibrary/**` and `flow-lang/Core/**` returns zero hits. The registry at `flow-lang/StandardLibrary/InternalFunctionRegistry.cs:10-20` is a plain `Dictionary<string, List<(FunctionSignature, Func<IReadOnlyList<Value>, Value>)>>` populated by explicit `Register(...)` calls from `BuiltInFunctions.cs::RegisterAll`. The only `System.Reflection` use in the entire codebase is in `scripts/StdlibAuditor/Program.cs` (Phase 42, test-only).

**Implication for `<TrimmerRootDescriptor>`:** Plan 48-01's trim-roots.xml should preserve `FlowType` subclasses — these ARE accessed by name via the `Instance` static-getter pattern (see `flow-lang/TypeSystem/SpecialTypes/CentType.cs:10-14`), but linker liveness analysis already keeps the singleton accessor reachable when the type is referenced. **The real trim risk is `FlowType.Instance` static getters and music-type singletons** (Note/Chord/Beat/Hertz/etc.), not `InternalFunctionRegistry`. The descriptor should preserve:

- `FlowLang.TypeSystem.FlowType` and all subclasses
- All `*Type.Instance` static properties (linker sometimes elides static initializers under aggressive trim)
- `FlowLang.Runtime.Value` (interop with `[JSExport]` requires concrete reachability)
- `FlowLang.StandardLibrary.Audio.AudioBuffer` (interop boundary type)

Phase 48 should NOT enumerate every `Register*` method — the public-Register-method graph is already statically reachable from `FlowEngine` constructor's call chain. Trim-mode's reachability analyzer handles it.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `flow-lang/wasm/trim-roots.xml` | config | n/a | none — new convention | no analog (MS docs only) |
| `flow-lang/wasm/flow-runtime.js` | runtime-glue | request-response | none — new convention | no analog (MS docs only) |
| `flow-lang/wasm/index.html` | dev-harness | n/a | none — dev-only | no analog (dev-only) |
| `flow-lang/Audio/WebAudioBackend.cs` (modify) | audio backend | streaming | `Audio/PulseAudioSimpleBackend.cs` | role-match (signature pinned by D-47-05) |
| `flow-lang/flow-lang.csproj` (modify) | config | n/a | self (Phase 47 already added FlowTarget Web conditional ItemGroup) | exact (extend existing conditional block) |
| `flow-lang.Tests/Integration/Phase48/WasmBuildPipelineTests.cs` | test | nested-process | `Phase47/BuildConditioningSmokeTests.cs` | exact |
| `flow-lang.Tests/Integration/Phase48/WebAudioBackendIntegrationTests.cs` | test | unit | `Phase47/WebAudioBackendStubTests.cs` | exact |
| `flow-lang.Tests/Integration/Phase48/FlowRuntimeJsApiTests.cs` | test | (deferred) | `Phase47/DryWetMidiWasmCompatTests.cs` (cross-target Fact) | partial — browser harness is HUMAN-UAT, not xUnit |

## Pattern Assignments

### `flow-lang/Audio/WebAudioBackend.cs` (modify — stub → real)

**Analog:** `flow-lang/Audio/PulseAudioSimpleBackend.cs` (lines 1-120)

**Method shape pinned by D-47-05:** All seven method signatures are LOCKED (CONTEXT.md line 10-11 explicitly says "Method signatures here are PINNED — Phase 48 must not change the public surface."). Phase 48 only swaps method BODIES, never signatures.

**Current stub** (`flow-lang/Audio/WebAudioBackend.cs:38-77`) — fill in these:

```csharp
public static bool IsAvailable() => OperatingSystem.IsBrowser();   // keep as-is
public bool Initialize(int sampleRate, int channels) => throw PNSE;
public void Play(float[] samples, int sampleRate, int channels, CancellationToken ct = default) => throw PNSE;
public void Stop() => throw PNSE;
public IReadOnlyList<string> GetDevices() => throw PNSE;
public bool SetDevice(string deviceName) => throw PNSE;
public void WriteChunk(float[] samples, int offset, int count, int sampleRate, int channels) => throw PNSE;
public void EnsureInitialized(int sampleRate, int channels) => throw PNSE;
public void Dispose() { /* no-op */ }
```

**Lifecycle pattern from PulseAudioSimpleBackend** (analog lines 36-78 + lock pattern):

```csharp
private IntPtr _connection;     // PulseAudio: opaque server handle
private int _sampleRate;
private int _channels;
private bool _disposed;
private readonly object _lock = new();
```

WebAudio equivalent (Phase 48 must add):

```csharp
private JSObject? _audioContext;      // [JSImport] handle to AudioContext
private JSObject? _activeSource;       // [JSImport] handle to current AudioBufferSourceNode (for Stop())
private int _sampleRate;
private int _channels;
private bool _disposed;
private readonly object _lock = new();
```

**Stereo promotion pattern (D-48-07):** Analog at `flow-lang/StandardLibrary/Audio/AudioCore.cs:209-219`:

```csharp
private static AudioBuffer MonoToStereo(AudioBuffer mono)
{
    var stereo = new AudioBuffer(mono.Frames, 2, mono.SampleRate);
    for (int f = 0; f < mono.Frames; f++)
    {
        float sample = mono.Data[f];
        stereo.Data[f * 2] = sample;
        stereo.Data[f * 2 + 1] = sample;
    }
    return stereo;
}
```

Phase 48 D-48-07 applies the same pattern but on `float[]` (no `AudioBuffer` wrapper since `IAudioBackend.Play` takes `float[]`). Code:

```csharp
// D-48-07: even mono Flow Buffers promote to stereo before marshalling
private static float[] PromoteToStereo(float[] mono, int channels)
{
    if (channels == 2) return mono;     // already stereo, pass-through
    var stereo = new float[mono.Length * 2];
    for (int i = 0; i < mono.Length; i++)
    {
        stereo[i * 2]     = mono[i];
        stereo[i * 2 + 1] = mono[i];
    }
    return stereo;
}
```

**[JSImport]/[JSExport] surface (no in-repo precedent):** D-48-06 declares the partial-static pattern per Microsoft's docs. Conceptual shape Phase 48 must follow:

```csharp
internal static partial class FlowRuntimeInterop
{
    [JSImport("createAudioContext", "flow-runtime")]
    internal static partial JSObject CreateAudioContext(int sampleRate);

    [JSImport("playStereoFloat32", "flow-runtime")]
    internal static partial JSObject PlayStereoFloat32(
        JSObject ctx, [JSMarshalAs<JSType.MemoryView>] Span<float> samples,
        int channels, int sampleRate);

    [JSImport("stopSource", "flow-runtime")]
    internal static partial void StopSource(JSObject sourceNode);

    [JSImport("closeContext", "flow-runtime")]
    internal static partial void CloseContext(JSObject ctx);
}
```

Note: `[JSMarshalAs<JSType.MemoryView>] Span<float>` is the multi-MB-Float32Array one-shot marshal per RESEARCH §5. Avoids the per-buffer streaming-interop latency trap.

**Error handling pattern from PulseAudio analog** (lines 67-77):

```csharp
if (_connection == IntPtr.Zero)
{
    var errorMsg = Marshal.PtrToStringAnsi(pa_strerror(error));
    Console.Error.WriteLine($"PulseAudio: Failed to connect: {errorMsg}");
    return false;
}
```

WebAudio equivalent for `Initialize`:

```csharp
try
{
    _audioContext = FlowRuntimeInterop.CreateAudioContext(sampleRate);
    _sampleRate = sampleRate;
    _channels = channels;
    return _audioContext != null;
}
catch (JSException ex)
{
    Console.Error.WriteLine($"WebAudio: Failed to create AudioContext: {ex.Message}");
    return false;
}
```

**`AudioPlaybackManager.DetectBackend` already handles Web-first probe** — Phase 48 does NOT touch `flow-lang/Audio/AudioPlaybackManager.cs:146-179`. Verified at `AudioPlaybackManager.cs:156-157`:

```csharp
if (WebAudioBackend.IsAvailable())
    return new WebAudioBackend();
```

This is the load-bearing branch from Phase 47 D-47-06. Phase 48 inherits unchanged.

---

### `flow-lang/flow-lang.csproj` (modify — extend existing FlowTarget=Web ItemGroup)

**Analog:** self at `flow-lang/flow-lang.csproj:18-62` — Phase 47 already established the conditional pattern. Plan 48-01 extends that ItemGroup with WASM-specific properties.

**Phase 47's existing conditional block** (lines 18-20):

```xml
<PropertyGroup Condition="'$(FlowTarget)' == 'Web'">
  <DefineConstants>$(DefineConstants);FLOW_WEB</DefineConstants>
</PropertyGroup>
```

**Phase 48 must add** (per D-48-01..04):

```xml
<PropertyGroup Condition="'$(FlowTarget)' == 'Web'">
  <DefineConstants>$(DefineConstants);FLOW_WEB</DefineConstants>
  <!-- Phase 48 D-48-01 (jiterpreter) + D-48-03 (invariant globalization) + D-48-02 (trim) -->
  <RuntimeIdentifier>browser-wasm</RuntimeIdentifier>
  <WasmEnableJiterpreter>true</WasmEnableJiterpreter>
  <InvariantGlobalization>true</InvariantGlobalization>
  <HybridGlobalization>false</HybridGlobalization>
  <TrimMode>full</TrimMode>
  <!-- Phase 48 D-48-04: symbol map Debug-only -->
  <WasmEmitSymbolMap Condition="'$(Configuration)' == 'Debug'">true</WasmEmitSymbolMap>
</PropertyGroup>

<ItemGroup Condition="'$(FlowTarget)' == 'Web'">
  <!-- existing Phase 47 strip list at lines 40-61 stays -->
  <!-- Phase 48: trimmer-roots descriptor preserving FlowType singletons -->
  <TrimmerRootDescriptor Include="wasm\trim-roots.xml" />
</ItemGroup>
```

**Sample bundle strip already in place** — `flow-lang.csproj:53-54`:

```xml
<Content Remove="Samples\**" />
<None Remove="Samples\**" />
```

Phase 48 verifies these still fire (no edit needed); D-48-03 inherits Phase 47 D-47-11 unchanged.

**DryWetMidi PackageReference at line 23** stays unconditional — Phase 47 Plan 47-04 verified WASM-compat on Desktop; Phase 48 verifies WASM-runtime end-to-end (D-48-17).

---

### `flow-lang/wasm/trim-roots.xml` (NEW, no analog)

No in-repo precedent. Microsoft's `<linker>` XML schema. Conceptual shape:

```xml
<?xml version="1.0" encoding="utf-8"?>
<linker>
  <!-- Phase 48 D-48-02: preserve FlowType subclasses + singletons.
       Trim-mode reachability sometimes elides static .cctor initializers;
       this descriptor pins the Instance accessors so type lookups by
       reflection-less getters keep working. -->
  <assembly fullname="flow-lang">
    <type fullname="FlowLang.TypeSystem.FlowType" preserve="all" />
    <type fullname="FlowLang.TypeSystem.SpecialTypes.NoteType" preserve="all" />
    <type fullname="FlowLang.TypeSystem.SpecialTypes.ChordType" preserve="all" />
    <type fullname="FlowLang.TypeSystem.SpecialTypes.BeatType" preserve="all" />
    <type fullname="FlowLang.TypeSystem.SpecialTypes.HertzType" preserve="all" />
    <!-- ... all music-type singletons per the table in CLAUDE.md ## Music Types Quick Reference ... -->
    <type fullname="FlowLang.StandardLibrary.Audio.AudioBuffer" preserve="all" />
    <type fullname="FlowLang.Runtime.Value" preserve="all" />
    <type fullname="FlowLang.Audio.WebAudioBackend" preserve="all" />
  </assembly>
</linker>
```

**Sources:** Microsoft's [.NET Trimming docs — Root descriptors XML](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trimming-options#root-descriptors). Plan 48-01 should enumerate FlowType subclasses by glob over `flow-lang/TypeSystem/SpecialTypes/*.cs`.

---

### `flow-lang/wasm/flow-runtime.js` (NEW, no analog)

No in-repo JS precedent (flow-lsp is a .NET LSP server, not browser JS). API surface frozen by D-48-13:

```javascript
// Phase 48 D-48-12: ES module, not UMD/CommonJS. Phase 49 SvelteKit consumes natively.
// API surface frozen per D-48-13 — minimal, no over-engineering.

import { dotnet } from './_framework/dotnet.js';

let _runtime = null;
let _audioContext = null;
let _activeSources = new Set();

export async function loadFlowRuntime() {
    if (_runtime) return _runtime;
    const { setModuleImports, getAssemblyExports, getConfig } = await dotnet.create();
    setModuleImports('flow-runtime', {
        createAudioContext: (sampleRate) => {
            if (!_audioContext) {
                _audioContext = new AudioContext({ sampleRate });
            }
            return _audioContext;
        },
        playStereoFloat32: (ctx, samples, channels, sampleRate) => {
            const buffer = ctx.createBuffer(channels, samples.length / channels, sampleRate);
            // de-interleave per WebAudio's per-channel layout
            for (let ch = 0; ch < channels; ch++) {
                const channelData = buffer.getChannelData(ch);
                for (let i = 0; i < channelData.length; i++) {
                    channelData[i] = samples[i * channels + ch];
                }
            }
            const source = ctx.createBufferSource();
            source.buffer = buffer;
            source.connect(ctx.destination);
            source.start();
            _activeSources.add(source);
            source.onended = () => _activeSources.delete(source);
            return source;
        },
        stopSource: (source) => {
            try { source.stop(); } catch (e) { /* already stopped */ }
            _activeSources.delete(source);
        },
        closeContext: async (ctx) => {
            for (const src of _activeSources) {
                try { src.stop(); } catch (e) { /* */ }
            }
            _activeSources.clear();
            await ctx.close();
            _audioContext = null;
        },
    });

    const config = getConfig();
    const exports = await getAssemblyExports(config.mainAssemblyName);
    _runtime = {
        run: (source) => exports.FlowLang.Runtime.WasmEntry.Run(source),
        play: (samples) => exports.FlowLang.Audio.WebAudioBackend.PlayFromJs(samples),
        stop: () => exports.FlowLang.Audio.WebAudioBackend.StopFromJs(),
        dispose: () => exports.FlowLang.Audio.WebAudioBackend.DisposeFromJs(),
    };
    return _runtime;
}
```

**Notes for the planner:**

- The JS file is generated/published BY `dotnet publish` into `bin/Release/net10.0/browser-wasm/AppBundle/_framework/`. The hand-written `flow-runtime.js` lives at `flow-lang/wasm/flow-runtime.js` and is included as content via `<None Update CopyToPublishDirectory>` (the pattern at lines 71-145 of `flow-lang.csproj` for .flow files).
- AudioContext.resume() in user-gesture chain is Phase 49's responsibility per D-48-09 — NOT in this file.
- `RunResult` shape (D-48-14): structured errors `{ kind, message, line?, column?, source_snippet? }[]`. Phase 48 must expose a C# `[JSExport]` that returns this shape (likely as JSON-serialized `JSObject`).

---

### `flow-lang/wasm/index.html` (NEW, dev-only smoke harness)

No analog needed. Standard `<script type="module" src="./flow-runtime.js">` boilerplate with a textarea and a button wired to `runtime.run(textarea.value)` inside an `onclick` (user-gesture chain per D-48-09). NOT shipped with the published bundle — gitignored under `flow-lang/wasm/.gitignore` or stays in repo for dev smoke only.

---

### `flow-lang.Tests/Integration/Phase48/WasmBuildPipelineTests.cs` (NEW)

**Analog:** `flow-lang.Tests/Integration/Phase47/BuildConditioningSmokeTests.cs` (lines 1-84) — exact match.

**Imports / harness pattern** (lines 1-53 of analog):

```csharp
using System;
using System.Diagnostics;
using System.IO;
using Xunit;

namespace FlowLang.Tests.Integration.Phase48;

public class WasmBuildPipelineTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "flow-lang", "flow-lang.csproj")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Could not locate repo root from " + AppContext.BaseDirectory);
    }

    private static (int exitCode, string stdout, string stderr) RunDotnetPublish(string args)
    {
        var repoRoot = FindRepoRoot();
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "publish flow-lang/flow-lang.csproj " + args + " -v quiet --nologo",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var p = Process.Start(psi)!;
        string stdout = p.StandardOutput.ReadToEnd();
        string stderr = p.StandardError.ReadToEnd();
        p.WaitForExit(600_000);  // 10-minute cap (WASM publish slow)
        return (p.ExitCode, stdout, stderr);
    }
}
```

**Fact pattern** (lines 55-83 of analog):

```csharp
[Fact]
public void WasmPublish_ExitCodeIsZero()
{
    var (code, stdout, stderr) = RunDotnetPublish("-p:FlowTarget=Web -c Release");
    Assert.True(code == 0,
        $"Expected exit 0 with FlowTarget=Web publish, got {code}.\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
}

[Fact]
public void WasmPublish_ProducesAppBundle()
{
    var (code, _, _) = RunDotnetPublish("-p:FlowTarget=Web -c Release");
    Assert.True(code == 0, "Publish must succeed before checking bundle");

    var repoRoot = FindRepoRoot();
    var appBundle = Path.Combine(repoRoot, "flow-lang", "bin", "Release", "net10.0",
                                  "browser-wasm", "publish", "AppBundle");
    Assert.True(Directory.Exists(appBundle), $"AppBundle not found at {appBundle}");
    Assert.True(File.Exists(Path.Combine(appBundle, "_framework", "dotnet.js")),
        "AppBundle missing _framework/dotnet.js — publish output malformed");
}

[Fact]
public void WasmBundle_TotalSizeWithinBudget()
{
    var (code, _, _) = RunDotnetPublish("-p:FlowTarget=Web -c Release");
    Assert.True(code == 0);

    var repoRoot = FindRepoRoot();
    var frameworkDir = Path.Combine(repoRoot, "flow-lang", "bin", "Release", "net10.0",
                                     "browser-wasm", "publish", "AppBundle", "_framework");
    long total = new DirectoryInfo(frameworkDir)
        .EnumerateFiles("*", SearchOption.AllDirectories)
        .Sum(f => f.Length);

    // Phase 48 D-48-05: 15 MB budget (uncompressed bundle, Brotli halves it).
    // Soft assert — if exceeded, Plan 48-05 lazy-loading kicks in.
    Assert.True(total < 30 * 1024 * 1024,
        $"WASM bundle uncompressed {total / 1_000_000} MB exceeds 30 MB hard cap " +
        $"(15 MB target compressed). Plan 48-05 lazy-load required.");
}
```

**Critical caveats:**

- Tests shell out via `dotnet publish` (not `dotnet build`) because the WASM-AppBundle artifacts are only produced at publish time.
- Timeout extended to 10 min (WASM publish is slow; jiterpreter generation alone takes ~30s).
- Web-target shelling tests already follow the FindRepoRoot pattern — exact analog.

---

### `flow-lang.Tests/Integration/Phase48/WebAudioBackendIntegrationTests.cs` (NEW)

**Analog:** `flow-lang.Tests/Integration/Phase47/WebAudioBackendStubTests.cs` (lines 1-82) — exact match for the test class shape; bodies invert (Phase 47 asserts THROWS, Phase 48 asserts SUCCESS under Web).

**Analog test attribute** (Phase 47 used plain `[Fact]` since stub Facts run cross-target). Phase 48 Integration tests should be `[FlowTargetFact("Web")]` since they exercise the [JSImport]-backed real backend.

**Analog imports + class shape** (lines 1-20 of `WebAudioBackendStubTests.cs`):

```csharp
using FlowLang.Audio;
using FlowLang.Tests.Helpers;
using Xunit;

namespace FlowLang.Tests.Integration.Phase48;

public class WebAudioBackendIntegrationTests
{
    [FlowTargetFact("Web")]
    public void IsAvailable_ReturnsTrue_OnBrowser()
    {
        // Under FlowTarget=Web AND running in a browser (or wasm-experimental
        // headless host), OperatingSystem.IsBrowser() returns true.
        // Note: Plan 48-03 must verify this Fact runs in an actual WASM test
        // host. If xunit-v3 + Microsoft.NET.Test.Sdk don't transparently host
        // tests under browser-wasm, this Fact MAY need to be HUMAN-UAT only.
        Assert.True(WebAudioBackend.IsAvailable());
    }

    [FlowTargetFact("Web")]
    public void Initialize_Succeeds_UnderBrowserHost()
    {
        var backend = new WebAudioBackend();
        Assert.True(backend.Initialize(44100, 2),
            "Initialize must succeed when running under Mono-WASM with AudioContext available");
    }

    [FlowTargetFact("Web")]
    public void Play_RoundTripsFloat32Array_NoException()
    {
        var backend = new WebAudioBackend();
        backend.Initialize(44100, 2);
        var samples = new float[44100 * 2];  // 1 second stereo
        // Sine wave at 440Hz, amplitude 0.5
        for (int i = 0; i < samples.Length / 2; i++)
        {
            float v = (float)(0.5 * Math.Sin(2.0 * Math.PI * 440.0 * i / 44100.0));
            samples[i * 2]     = v;
            samples[i * 2 + 1] = v;
        }
        backend.Play(samples, 44100, 2);  // must not throw
    }

    [FlowTargetFact("Web")]
    public void MonoInput_PromotesToStereo_BeforeMarshal()
    {
        // D-48-07: mono Buffer → stereo before [JSImport] marshal.
        // White-box test: invoke Play(mono) and assert the wire-marshalled
        // float[] length is 2× the input. Requires test-seam exposure of the
        // PromoteToStereo helper.
        var mono = new float[100];
        for (int i = 0; i < 100; i++) mono[i] = i * 0.01f;
        var promoted = WebAudioBackend.PromoteToStereo(mono, channels: 1);
        Assert.Equal(200, promoted.Length);
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(mono[i], promoted[i * 2]);
            Assert.Equal(mono[i], promoted[i * 2 + 1]);
        }
    }
}
```

**Note for planner:** The Plan 48-03 acceptance Facts may need to be HUMAN-UAT (browser smoke) rather than xUnit if the test framework can't transparently host under browser-wasm. The CONTEXT.md "Validation Architecture" Block (line 71-76 of RESEARCH.md) calls for `dotnet test -p:FlowTarget=Web` to run "Mono-WASM headless test". This requires `Microsoft.NET.Test.Sdk` to support browser-wasm host — verify at Plan 48-03 dry-run. If unavailable, fall back to HUMAN-UAT per Plan 48-04.

---

### `flow-lang.Tests/Integration/Phase48/FlowRuntimeJsApiTests.cs` (NEW or HUMAN-UAT)

**Analog (partial):** `flow-lang.Tests/Integration/Phase47/DryWetMidiWasmCompatTests.cs` for the cross-target Fact pattern.

If xUnit + WASM host works (verified at Plan 48-03), Phase 48 lands xUnit Facts asserting the `flow-runtime.js` API shape via [JSExport] roundtrip:

```csharp
[FlowTargetFact("Web")]
public void RunResult_ContainsStdout_AfterPrintCall()
{
    var result = WasmEntry.Run("(print \"hello flow\")");
    Assert.Contains("hello flow", result.Stdout);
    Assert.Empty(result.Errors);
}
```

Otherwise this file is replaced by HUMAN-UAT rows in `48-HUMAN-UAT.md` covering Chrome 120+ / Firefox 121+ / Safari 17+ per RESEARCH.md "HUMAN-UAT (3 rows)".

## Shared Patterns

### Pattern A: Phase 47 conditional ItemGroup is the load-bearing extension point

**Source:** `flow-lang/flow-lang.csproj:18-62`
**Apply to:** `flow-lang.csproj` Plan 48-01 edits

Phase 47 established a single `<PropertyGroup Condition="'$(FlowTarget)' == 'Web'">` + a single `<ItemGroup Condition="'$(FlowTarget)' == 'Web'">`. Phase 48 EXTENDS both rather than introducing a new conditional. This preserves the "single source of truth" decision (D-47-01) and avoids `<PropertyGroup>` drift between conditionals. Concrete additions documented in the csproj pattern block above.

### Pattern B: `OperatingSystem.IsBrowser()` JIT intrinsic, not preprocessor symbol, for runtime branching

**Source:** `flow-lang/Audio/WebAudioBackend.cs:38` + `flow-lang/Audio/AudioPlaybackManager.cs:156`
**Apply to:** WebAudioBackend body Phase 48 edits — DO NOT add `#if FLOW_WEB` inside the class

```csharp
public static bool IsAvailable() => OperatingSystem.IsBrowser();
```

D-47-07 rationale: `OperatingSystem.IsBrowser()` is constant-folded on every Desktop runtime. The class stays in both builds; only the body branches at runtime. Plan 48-03 should NOT add `#if FLOW_WEB` guards inside `WebAudioBackend.cs` — the file already compiles on Desktop, just throws PlatformNotSupportedException there. Phase 48 swaps throws for [JSImport] bodies; the bodies still compile on Desktop (since [JSImport]/[JSExport] are .NET 7+ BCL attributes, available everywhere) and only EXECUTE on browser.

### Pattern C: 30-second wall-clock cap via Task.Run + Wait(TimeSpan)

**Source:** `flow-interpreter/LiveReloadManager.cs:82` + `:470-499`
**Apply to:** D-48-10 30s evaluation cap on `WasmEntry.Run(source)`

```csharp
private static readonly TimeSpan RenderTimeout = TimeSpan.FromSeconds(30);

// ... at the call site ...
var workerTask = Task.Run(() => engine.Execute(source, "<wasm>"));
if (!workerTask.Wait(RenderTimeout))
{
    // emit [runtime] evaluation exceeded 30s cap advisory
    return new RunResult { Errors = new[] { new RunError(kind: "cancel", message: "...") } };
}
```

D-48-10 inherits this pattern verbatim. RESEARCH §E Option A documents the orphan-worker tradeoff (worker keeps running in background after timeout; v1.6 backlog for true cooperative cancellation).

### Pattern D: Charitable `[X]` stderr advisories via WarnOnce

**Source:** Phase 47 D-47-09 module-load gate at `ModuleLoader` (per CLAUDE.md §Compile-Target Flavors)
**Apply to:** Phase 48 runtime advisories (`[runtime] evaluation exceeded 30s cap`, `[runtime] AudioContext.resume() required — call from user gesture`)

Same prefix convention `[<subsystem>] <message> — <hint>`. Existing precedents:
- `[live] entering live block at line N` (Phase 38 D-v1.5-07)
- `[target] module '@X' unavailable on Web target — line N` (Phase 47 D-47-09)
- `[tuning] unmapped MIDI keys under '<desc>' — rendered as rest` (Phase 32 D-08)

Phase 48 adds `[runtime] ...` family for WASM-host advisories. Routed through `Diagnostics/RenderingDiagnostics.WarnOnce` if existing infra reachable on Web (verify at Plan 48-03 — likely yes since `@notation-io` advisories already use it on Web).

### Pattern E: Test-project FLOW_WEB define propagation

**Source:** `flow-lang.Tests/flow-lang.Tests.csproj:13-21`

Plan 48-01 must verify the test project's FLOW_WEB conditional propagation still fires under Phase 48's extended csproj. Phase 47 already wires it; no new edit needed unless the test infrastructure changes.

### Pattern F: Sample bundle stripping evidence — Phase 47 D-47-11 already in place

**Source:** `flow-lang/flow-lang.csproj:53-54`

```xml
<Content Remove="Samples\**" />
<None Remove="Samples\**" />
```

Phase 48 D-48-03 inherits. Plan 48-01 verifies the strip fires correctly under `dotnet publish -p:FlowTarget=Web` (acceptance: `bin/Release/.../AppBundle/Samples/` does NOT exist).

## No Analog Found

Files with no close match in the codebase (planner should rely on RESEARCH.md + Microsoft docs):

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `flow-lang/wasm/trim-roots.xml` | trimmer config | n/a | No prior trim XML in repo — Microsoft .NET trimming docs |
| `flow-lang/wasm/flow-runtime.js` | runtime glue | request-response | No browser JS in repo (flow-lsp is .NET LSP) — Microsoft Blazor JS interop docs |
| `flow-lang/wasm/index.html` | dev harness | n/a | Dev-only; standard HTML5 boilerplate |
| `[JSImport]/[JSExport]` interop surface | C# interop | request-response | No in-repo precedent per CONTEXT.md line 117-118 — first use in repo |

## Metadata

**Analog search scope:**
- `flow-lang/Audio/` (backend abstractions)
- `flow-lang/StandardLibrary/Audio/` (AudioBuffer, MonoToStereo, SongRenderer stereo mixing)
- `flow-lang/Core/FlowEngine.cs` (IsWebTarget, SupportsLiveBlocks static flags)
- `flow-lang/flow-lang.csproj` (FlowTarget Web conditional ItemGroup)
- `flow-lang.Tests/Integration/Phase47/` (BuildConditioningSmokeTests, WebAudioBackendStubTests, AssemblyReferenceScanTests, DryWetMidiWasmCompatTests, WebTargetModuleLoaderTests)
- `flow-lang.Tests/Helpers/FlowTargetFactAttribute.cs` (test discrimination)
- `flow-interpreter/LiveReloadManager.cs:82,470-499` (30s wall-clock cap pattern)
- `scripts/StdlibAuditor/` (negative search — confirmed no reflection in flow-lang)

**Files scanned:** ~30 (focused; early-stop at 5 strong analogs per role)
**Pattern extraction date:** 2026-05-25
