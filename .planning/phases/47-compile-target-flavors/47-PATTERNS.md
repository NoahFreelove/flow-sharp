# Phase 47: Compile-Target Flavors - Pattern Map

**Mapped:** 2026-05-25
**Files analyzed:** 13 (4 new, 7 modified, 2 build configs)
**Analogs found:** 11 / 13 (2 require new patterns introduced by this phase)

## Scope Summary

Phase 47 is a **build-time refactor** that introduces `FlowTarget=Desktop|Web` MSBuild conditioning. There are zero new language features. All work is one of:

1. csproj structural changes (new `<FlowTarget>` property + conditional `<ItemGroup>`)
2. New stub file (`Audio/WebAudioBackend.cs`) following the existing `IAudioBackend` interface
3. Single-call-site guards (`#if !FLOW_WEB`) at `FlowEngine` constructor + `BuiltInFunctions.RegisterAll` style entry points
4. Two-line guard insertion at `Parser.ParseStatement` (`live` block keyword) + `ModuleLoader.LoadModule` (stripped-stdlib module-name list)
5. New `FlowEngine.IsWebTarget` / `FlowEngine.SupportsLiveBlocks` static flags
6. New test project (`AssemblyReferenceScanTests`) referencing Mono.Cecil 0.11.5
7. New `[FlowTargetFact("...")]` xUnit attribute wrapping `FactAttribute` with `Skip = ...`

**Critical observation:** No existing `#if` preprocessor directives in flow-lang or flow-interpreter — Phase 47 introduces the convention. No existing `<ItemGroup Condition="...">` blocks in any csproj — Phase 47 introduces this too. No existing `OperatingSystem.IsBrowser()` calls — Phase 47 introduces.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `flow-lang/flow-lang.csproj` | build config | n/a (MSBuild eval) | current csproj structure | self-modify (no precedent) |
| `flow-lang/Audio/WebAudioBackend.cs` (NEW) | backend impl | request-response | `flow-lang/Audio/PulseAudioSimpleBackend.cs` | exact (sibling `IAudioBackend` impl) |
| `flow-lang/Audio/AudioPlaybackManager.cs` | service | request-response | own existing `DetectBackend()` | self-modify (add Web probe branch) |
| `flow-lang/StandardLibrary/BuiltInFunctions.cs` | registration site | n/a (init-time) | own `RegisterAllImplementations` | self-modify (no analog needed — only `#if` wraps) |
| `flow-lang/Core/FlowEngine.cs` | orchestrator | init-time | own constructor (existing `_audioManager` wiring) | self-modify (add static flag init + `#if` guards on SfzBuiltins/OscFunctions/InputFunctions/LiveBlock registrations) |
| `flow-lang/Runtime/ModuleLoader.cs` | service | request-response (synchronous load) | own `LoadModule` early-return on circular import | self-modify (add stripped-module gate at top) |
| `flow-lang/Parsing/Parser.cs` | parser | request-response | own `ParseLiveBlockStatement` + Phase 26 D-15 stray-arithmetic error | self-modify (add `if (!FlowEngine.SupportsLiveBlocks)` after `Match(TokenType.Live)`) |
| `flow-lang/Runtime/ExecutionContext.cs` | runtime state | n/a (data only) | own `SfzEnabled`/`OscEnabled` flag pattern | not strictly needed — flags live on `FlowEngine` per D-47-10 |
| `flow-lang.Tests/Helpers/FlowTargetFactAttribute.cs` (NEW) | test attr | n/a (test metadata) | existing `[Fact(Skip = "...")]` pattern (Phase 39) | role-match (no existing custom Fact subclass) |
| `flow-lang.Tests/AssemblyReferenceScanTests.cs` (NEW) | test | reflective read | no precedent in repo | no analog — Mono.Cecil pattern from RESEARCH |
| `flow-lang.Tests/flow-lang.Tests.csproj` | build config | n/a | current csproj | self-modify (add Mono.Cecil PackageReference) |
| `.planning/phases/47-compile-target-flavors/47-AUDIT.md` | doc | n/a | Phase 42 `42-AUDIT.md` shape | role-match (closer-doc convention) |
| `flow-lang/Samples/**` | stripped data | n/a | no MSBuild reference exists today | NO-OP (handled by SampleCache null-fallback, see Pattern 6) |

## Pattern Assignments

### `flow-lang/flow-lang.csproj` (build config, MSBuild evaluation-time)

**Analog:** self (no `Condition="..."` precedent in any flow-sharp csproj)
**Strategy:** Introduce the convention. Insert `<FlowTarget>Desktop</FlowTarget>` in the existing `<PropertyGroup>` at line 3-8 (alongside `TargetFramework` / `RootNamespace` / `ImplicitUsings` / `Nullable`). Add a second `<PropertyGroup Condition="'$(FlowTarget)' == 'Web'">` block immediately after, defining `<DefineConstants>$(DefineConstants);FLOW_WEB</DefineConstants>`.

**Current PropertyGroup pattern** (`flow-lang.csproj:3-8`):
```xml
<PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>FlowLang</RootNamespace>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
</PropertyGroup>
```

**Current ItemGroup pattern for stdlib `.flow` files** (`flow-lang.csproj:23-98`):
```xml
<ItemGroup>
  <None Update="std.flow">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
  </None>
  <!-- ...11 more None Update entries... -->
</ItemGroup>
```

**Phase 47 addition shape** (NEW conditional ItemGroup, anywhere after line 21):
```xml
<!-- Phase 47 D-47-03: file-level exclusion when targeting Web -->
<ItemGroup Condition="'$(FlowTarget)' == 'Web'">
  <Compile Remove="Audio/PulseAudioSimpleBackend.cs" />
  <Compile Remove="Audio/PulseAudioCaptureBackend.cs" />
  <Compile Remove="Audio/CoreAudioBackend.cs" />
  <Compile Remove="StandardLibrary/Audio/Sfz/**/*.cs" />
  <Compile Remove="StandardLibrary/Network/OscFunctions.cs" />
  <Compile Remove="StandardLibrary/Network/OscHandleData.cs" />
  <Compile Remove="StandardLibrary/Audio/InputFunctions.cs" />
  <Compile Remove="Runtime/LiveBlockRegistry.cs" />
  <Compile Remove="Ast/Statements/LiveBlockStatement.cs" />
  <Compile Remove="Interpreter/LambdaCaptureAuditor.cs" />
  <None Remove="sfz.flow" />
  <None Remove="osc.flow" />
  <PackageReference Remove="Rug.Osc" />
</ItemGroup>
```

**Important csproj caveats found by audit:**
- `flow-lang/Samples/**` has NO existing `<None Update="..." />` entry — Phase 29 samples are loaded via `SampleCache(string samplesRoot = "flow-lang/Samples")` at runtime (`SampleCache.cs:68`). Therefore D-47-11 `<None Remove="Samples/**" />` may be **a no-op or unnecessary** unless implicit-include behavior of the SDK pulls Samples into the published binary. **Planner should verify at Plan 47-01** whether `dotnet publish -p:FlowTarget=Web` actually emits Samples into the WASM output before adding a `<Content Remove>` or `<None Remove>` directive.
- `Rug.Osc` is a `<PackageReference>` (line 13), not a `<Compile>` — strip via `<PackageReference Remove="Rug.Osc" Condition="'$(FlowTarget)' == 'Web'" />` syntax or move into the conditional ItemGroup above. Verify removal is honored at MSBuild eval time (some SDKs require `<ItemGroup Condition>` around the `<PackageReference Include>`).
- `LiveReloadManager.cs` lives in `flow-interpreter/` (NOT `flow-lang/`), so it's automatically out-of-scope for `flow-lang.csproj` strip-list. The CLI project (`flow-interpreter`) is excluded from Web builds at the build-target level (web build is library-only per CONTEXT line 42).

---

### `flow-lang/Audio/WebAudioBackend.cs` (NEW, backend impl, request-response)

**Analog:** `flow-lang/Audio/PulseAudioSimpleBackend.cs` (exact role + data flow match)

**Imports pattern** (`PulseAudioSimpleBackend.cs:1-3`):
```csharp
using System.Runtime.InteropServices;

namespace FlowLang.Audio;
```

**Static IsAvailable pattern** (`PulseAudioSimpleBackend.cs:23-34`):
```csharp
public static bool IsAvailable()
{
    try
    {
        pa_strerror(0);
        return true;
    }
    catch (DllNotFoundException)
    {
        return false;
    }
}
```

**Phase 47 IsAvailable shape** (per D-47-07):
```csharp
public static bool IsAvailable() => OperatingSystem.IsBrowser();
```

**Class declaration pattern** (`PulseAudioSimpleBackend.cs:9-18`):
```csharp
public sealed class PulseAudioSimpleBackend : IAudioBackend
{
    private IntPtr _connection;
    private int _sampleRate;
    private int _channels;
    private bool _disposed;
    private readonly object _lock = new();

    public string Name => "PulseAudio";
    public bool IsInitialized => _connection != IntPtr.Zero;
```

**WebAudioBackend stub shape:** Implements all 8 `IAudioBackend` methods (per `IAudioBackend.cs:7-72`):
- `Initialize(int, int) -> bool`
- `Play(float[], int, int, CancellationToken) -> void`
- `Stop() -> void`
- `GetDevices() -> IReadOnlyList<string>`
- `SetDevice(string) -> bool`
- `Name -> string` (returns `"WebAudio"`)
- `IsInitialized -> bool` (returns `false` always for stub)
- `WriteChunk(...)`, `EnsureInitialized(int, int)`, `Dispose()`

Per D-47-05, all methods **except `IsAvailable()`** throw `PlatformNotSupportedException("WebAudioBackend stub — Phase 48 will implement via [JSImport]")`. Phase 48 replaces method bodies; signatures unchanged.

---

### `flow-lang/Audio/AudioPlaybackManager.cs` (service, add Web probe branch)

**Analog:** own existing `DetectBackend()` method (lines 138-158).

**Current DetectBackend pattern** (`AudioPlaybackManager.cs:138-158`):
```csharp
private static IAudioBackend DetectBackend()
{
    // macOS: prefer CoreAudio via AudioToolbox.framework.
    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
        if (CoreAudioBackend.IsAvailable())
            return new CoreAudioBackend();
    }

    // Try PulseAudio Simple API
    if (PulseAudioSimpleBackend.IsAvailable())
        return new PulseAudioSimpleBackend();

    throw new PlatformNotSupportedException(
        "No audio output available. On Linux, install PipeWire or PulseAudio. " +
        "On macOS, CoreAudio (AudioToolbox.framework) should be present by default.");
}
```

**Phase 47 modification** (per D-47-06): add as the **first** probe branch (cheapest check, browser-only):
```csharp
private static IAudioBackend DetectBackend()
{
    // Phase 47 D-47-06: Web target probe first — OperatingSystem.IsBrowser()
    // is a JIT intrinsic, dead-code-eliminated on Desktop trim-mode builds.
    if (WebAudioBackend.IsAvailable())
        return new WebAudioBackend();

    // macOS: prefer CoreAudio...
    // [existing branches unchanged]
}
```

**Charitable null-backend fallback note:** CONTEXT line 65 mentions a `NullAudioBackend` pattern at `IAudioBackend.cs:21-29` — **this does not exist in current code**. The existing `DetectBackend()` throws `PlatformNotSupportedException` when no backend is available. Phase 47 planner should decide whether to:
- (a) keep the throw (current behavior + Web added as a new probe) — matches CONTEXT D-47-06 line "if no backend probes available, return a no-op backend" intent but current code throws, OR
- (b) introduce a true `NullAudioBackend` class. Recommend (a) for minimum surface change unless ASSEMBLY-SCAN tests require silent fallback.

---

### `flow-lang/StandardLibrary/BuiltInFunctions.cs` (registration site, conditional `#if` wrap)

**Analog:** own `RegisterAllImplementations` (lines 35-72). CONTEXT mentions `RegisterSfz()`/`RegisterOsc()`/`RegisterMicInput()`/`RegisterLiveBlock()` but **these do not exist in current code.** SFZ/OSC are registered in `FlowEngine.cs` directly (lines 185 + 202); `InputFunctions.RegisterContextDependent` is called from `RegisterContextDependentFunctions` (line 1027); there is no `RegisterLiveBlock` (live blocks are AST-level, not registry).

**Phase 47 strategy revision:** The single guard site moves from `BuiltInFunctions.RegisterAll` to `FlowEngine.cs` constructor (see next entry). CONTEXT D-47-08's "ONLY guard site" intent applies to **the single FlowEngine constructor**, not the misnamed BuiltInFunctions methods.

**No change needed to BuiltInFunctions.cs** unless planner chooses to introduce wrapper methods (`RegisterSfz` etc.) to match CONTEXT phrasing. Recommended: keep the guard in FlowEngine.cs where the existing per-feature registration calls already live (matches "minimum-surface-change" rationale at D-47-08).

---

### `flow-lang/Core/FlowEngine.cs` (orchestrator, add static flags + `#if !FLOW_WEB` guards)

**Analog:** own constructor (lines 105-238).

**Current SFZ registration pattern** (`FlowEngine.cs:179-185`):
```csharp
// Phase 33 Plan 33-05: wire the SFZ surface — loadSfz(Symbol) +
// loadSfz(String) + __enableSfzModule(Dict) builtins. All three check
// ExecutionContext.SfzEnabled at call time, so the registration is
// safe even when no script imports @sfz (CONTEXT D-10). The
// __enableSfzModule call inside sfz.flow flips the gate during
// `use "@sfz"` import.
SfzBuiltins.Register(internalRegistry, _context);
```

**Current OSC registration pattern** (`FlowEngine.cs:193-202`):
```csharp
// Phase 38 Plan 38-06 — register the @osc stdlib surface
// (oscSend / oscListen / oscStop / oscBundle / oscSendBundle +
// __enableOscModule marker). All 5 surface builtins gate on
// ExecutionContext.OscEnabled...
FlowLang.StandardLibrary.Network.OscFunctions.Register(internalRegistry, _context);
```

**Current MicBuffer registration** (called from `BuiltInFunctions.cs:1027`):
```csharp
Audio.InputFunctions.RegisterContextDependent(registry, context);
```

**Phase 47 modification:** Wrap each in `#if !FLOW_WEB` at its actual call site:
```csharp
#if !FLOW_WEB
SfzBuiltins.Register(internalRegistry, _context);
#endif

// ...later...

#if !FLOW_WEB
FlowLang.StandardLibrary.Network.OscFunctions.Register(internalRegistry, _context);
#endif
```

For `InputFunctions`, the call lives in `BuiltInFunctions.RegisterContextDependentFunctions` (line 1027) — wrap there:
```csharp
#if !FLOW_WEB
Audio.InputFunctions.RegisterContextDependent(registry, context);  // Phase 44 Plan 44-07
#endif
```

**Static flag additions** (per D-47-10), insert near top of class alongside existing `CurrentSampleCache`/`CurrentSfzSampleCache`/`CurrentExecutionContext` static props (`FlowEngine.cs:80-99`):
```csharp
/// <summary>
/// Phase 47 D-47-10: true when this binary was built with FlowTarget=Web
/// (FLOW_WEB defined). Read by Parser and ModuleLoader to gate parse-time /
/// load-time stripped features. Set in the static constructor.
/// </summary>
public static bool IsWebTarget { get; } =
#if FLOW_WEB
    true;
#else
    false;
#endif

/// <summary>
/// Phase 47 D-47-10: false on Web target (live blocks require FileSystemWatcher,
/// unavailable in browser sandbox). Read by Parser.ParseStatement before
/// matching the `live` keyword.
/// </summary>
public static bool SupportsLiveBlocks { get; } = !IsWebTarget;
```

**Static-flag pattern precedent** (mirrors existing static `CurrentSampleCache` shape at `FlowEngine.cs:80`):
```csharp
public static SampleCache? CurrentSampleCache { get; private set; }
```

---

### `flow-lang/Runtime/ModuleLoader.cs` (service, add stripped-stdlib gate)

**Analog:** own `LoadModule` early-return path on circular import (lines 57-61).

**Current early-return pattern** (`ModuleLoader.cs:57-61`):
```csharp
if (_currentlyLoading.Contains(resolvedPath))
{
    _errorReporter.ReportError($"Circular import detected: {resolvedPath}", errorLocation);
    return ModuleLoadResult.Error;
}
```

**Phase 47 addition shape** — insert immediately after the `_loadedModules.Contains` short-circuit at line 54, before `_currentlyLoading` check:
```csharp
// Phase 47 D-47-09: Web-target stripped-module gate. The @sfz and @osc
// modules' implementations are absent (stripped via <Compile Remove>) so
// loading them would either link-error (already prevented by csproj) or
// silently no-op. Emit a charitable advisory and return Error so the
// composer's `use "@sfz"` import line carries diagnostic context.
if (Core.FlowEngine.IsWebTarget && IsStrippedOnWeb(path))
{
    var fname = Path.GetFileName(path.TrimStart('@'));
    Diagnostics.RenderingDiagnostics.WarnOnce(
        $"target:stripped-module:{fname}",
        $"[target] module '{path}' unavailable on Web target — line {errorLocation.Line}. " +
        $"Build with FlowTarget=Desktop to enable.");
    return ModuleLoadResult.Error;
}
```

**Helper method** (new static or private):
```csharp
private static bool IsStrippedOnWeb(string moduleName)
{
    // Phase 47 D-47-11/D-47-12: only @sfz and @osc are stripped at module-load.
    // Phase 29 sampled instruments fall back transparently (see Pattern 6).
    return moduleName == "@sfz" || moduleName == "@osc"
        || moduleName == "@sfz.flow" || moduleName == "@osc.flow";
}
```

**Advisory pattern** mirrors Phase 43 `module-dup` / `module-shadow` `WarnOnce` calls at `ModuleLoader.cs:172-188`:
```csharp
RenderingDiagnostics.WarnOnce(
    $"module-dup:{modDecl.Name}",
    $"[module] duplicate module name '{modDecl.Name}' — last load wins");
```

---

### `flow-lang/Parsing/Parser.cs` (parser, add parse-time `live` gate)

**Analog:** own `Match(TokenType.Live)` dispatch at line 220-221 + Phase 26 D-15 stray-arithmetic ParseException at lines 286-290.

**Current Live dispatch** (`Parser.cs:214-221`):
```csharp
// Phase 38 D-38-02 LIVE-01: `live <quantize> { ... }` block. Quantize
// accepts Int + optional `bar`/`bars` suffix, a NoteValue identifier
// (`q`/`h`/`w`/`e`/`s`), or is omitted entirely (defaults to 1bar).
// BlockId is FNV-1a of the keyword's SourceLocation so the runtime
// LiveBlockRegistry slot survives re-renders per D-38-02 independent
// multi-block swap.
if (Match(TokenType.Live))
    return ParseLiveBlockStatement();
```

**Existing ParseException pattern** (`Parser.cs:286-290`):
```csharp
if (Check(TokenType.Star) || Check(TokenType.Slash))
{
    throw new ParseException(
        $"Unexpected token '{CurrentToken.Text}' at {CurrentToken.Location} — Phase 26 removed infix arithmetic; use prefix builtins (add)/(sub)/(mul)/(div).");
}
```

**Phase 47 modification shape** — insert gate immediately after `Match(TokenType.Live)`:
```csharp
if (Match(TokenType.Live))
{
    // Phase 47 D-47-09: live blocks require FileSystemWatcher (browser-unavailable).
    // Parse-time error rather than runtime advisory because `live { ... }` is
    // block syntax, not a builtin invocation — composer needs a Rust-style
    // diagnostic pointing at the source line.
    if (!Core.FlowEngine.SupportsLiveBlocks)
    {
        throw new ParseException(
            $"`live` block requires Desktop target — line {PreviousToken.Location.Line}. " +
            $"Build with FlowTarget=Desktop or run with `flow run script.flow` locally.");
    }
    return ParseLiveBlockStatement();
}
```

**Note:** `TokenType.Live` is **still recognized** by the lexer in Web builds (Lexing/SimpleLexer.cs is not stripped). Stripping the `LiveBlockStatement.cs` AST file would break the parser even for the `throw new ParseException` line above (the method `ParseLiveBlockStatement` references `LiveBlockStatement`). **Two options:**
- (a) **Keep `LiveBlockStatement.cs` in the Web build** (remove it from the strip-list). The AST node exists but no code path constructs one because of the parse-time throw. Recommend this — minimal surface change.
- (b) Strip `LiveBlockStatement.cs` AND `ParseLiveBlockStatement` together via `#if !FLOW_WEB` around both. More invasive.

Recommend (a). The strip-list in CONTEXT line 15 should NOT include `LiveBlockStatement.cs` (CONTEXT lists `Runtime/LiveBlockRegistry.cs` only, which is safe — its consumers are also stripped).

---

### `flow-lang.Tests/Helpers/FlowTargetFactAttribute.cs` (NEW, test attr)

**Analog:** existing `[Fact(Skip = "...")]` usage at `MusicXmlRoundTripTests.cs:121`.

**Existing skip pattern** (`MusicXmlRoundTripTests.cs:121`):
```csharp
[Fact(Skip = "requires mscore in PATH — XML-02 gate lights up automatically when CI provisions one")]
public void StructuralPreservation_NoteCountMatches()
{
    // ...
}
```

**Phase 47 attribute shape** (subclass `FactAttribute`, set Skip conditionally):
```csharp
namespace FlowLang.Tests.Helpers;

/// <summary>
/// Phase 47 D-47-13: xUnit FactAttribute that skips the test unless the
/// current build's FlowTarget matches one of the supplied tokens. Use
/// [FlowTargetFact("Desktop")] for Desktop-only tests, [FlowTargetFact("Web")]
/// for Web-only, or [FlowTargetFact("Desktop", "Web")] for cross-target tests.
/// </summary>
public sealed class FlowTargetFactAttribute : Xunit.FactAttribute
{
    public FlowTargetFactAttribute(params string[] targets)
    {
#if FLOW_WEB
        const string current = "Web";
#else
        const string current = "Desktop";
#endif
        if (Array.IndexOf(targets, current) < 0)
        {
            Skip = $"Skipped on {current} — test runs under: {string.Join(", ", targets)}";
        }
    }
}
```

**Note:** Tests project must inherit `FLOW_WEB` define from the referenced `flow-lang.csproj`. If the Tests project is always Desktop-built, the attribute defaults to Desktop. Planner should verify whether `dotnet test -p:FlowTarget=Web` propagates the define to the Tests project (it should — `ProjectReference` inherits MSBuild properties unless suppressed).

---

### `flow-lang.Tests/AssemblyReferenceScanTests.cs` (NEW, reflective test)

**Analog:** No precedent in repo. RESEARCH cites Mono.Cecil pattern.

**Pattern from RESEARCH:** Mono.Cecil 0.11.5 (MIT) — reflective scan of `flow-lang.dll` for forbidden references. Per D-47-14, asserts zero references to:
- `Rug.Osc`
- `System.IO.FileSystemWatcher`
- `libpulse-simple` P/Invoke (DllImport string)
- `AudioToolbox` P/Invoke (DllImport string)
- `RtMidi.Core` (Phase 40 forward-look)

**Skeleton** (write fresh, no in-repo analog):
```csharp
using Mono.Cecil;
using Xunit;

namespace FlowLang.Tests.Phase47;

public class AssemblyReferenceScanTests
{
    [FlowTargetFact("Web")]
    public void WebBuild_HasNoRefsToStrippedNamespaces()
    {
        var asmPath = typeof(FlowLang.Core.FlowEngine).Assembly.Location;
        using var asm = AssemblyDefinition.ReadAssembly(asmPath);

        var forbidden = new[] {
            "Rug.Osc", "System.IO.FileSystemWatcher", "RtMidi.Core"
        };
        var typeRefs = asm.MainModule.GetTypeReferences()
            .Select(tr => tr.FullName).ToList();

        foreach (var bad in forbidden)
        {
            Assert.DoesNotContain(typeRefs, t => t.StartsWith(bad));
        }

        // P/Invoke string scan via MethodDefinition.PInvokeInfo
        foreach (var t in asm.MainModule.Types)
        foreach (var m in t.Methods)
            if (m.PInvokeInfo != null)
                Assert.False(
                    m.PInvokeInfo.Module.Name.Contains("libpulse")
                    || m.PInvokeInfo.Module.Name.Contains("AudioToolbox"),
                    $"P/Invoke leak: {m.FullName} -> {m.PInvokeInfo.Module.Name}");
    }
}
```

---

### `flow-lang.Tests/flow-lang.Tests.csproj` (build config)

**Analog:** own existing PackageReference list at lines 11-16.

**Existing pattern** (`flow-lang.Tests.csproj:11-16`):
```xml
<ItemGroup>
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.13.0" />
  <PackageReference Include="xunit.v3" Version="3.2.2" />
  <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5" />
  <PackageReference Include="coverlet.collector" Version="6.0.2" />
</ItemGroup>
```

**Phase 47 addition** (Mono.Cecil for AssemblyReferenceScanTests):
```xml
<PackageReference Include="Mono.Cecil" Version="0.11.5" />
```

---

## Shared Patterns

### Pattern 1 — Charitable advisory (`WarnOnce` + `[target]` prefix)

**Source:** `flow-lang/Diagnostics/RenderingDiagnostics.cs:29-36`
**Apply to:** ModuleLoader (stripped-module gate), and any other runtime advisory site Phase 47 introduces.

```csharp
public static void WarnOnce(string sentinelKey, string message)
{
    lock (_lock)
    {
        if (!_emitted.Add(sentinelKey)) return;
    }
    Console.Error.WriteLine(message);
}
```

**Phase 47 advisory shape** (mirrors Phase 36 `[patterns]` / Phase 43 `[module]` prefix convention):
```csharp
RenderingDiagnostics.WarnOnce(
    $"target:stripped-module:{moduleName}",
    $"[target] module '{moduleName}' unavailable on Web target — line {line}. Build with FlowTarget=Desktop to enable.");
```

Per-call sentinel keys (use the call-site or the stripped-feature name) avoid stderr flooding on repeated `use "@sfz"` imports across multiple files.

### Pattern 2 — Module-enabled gate (existing precedent)

**Source:** `flow-lang/StandardLibrary/Audio/Sfz/SfzBuiltins.cs:165-167`
**Apply to:** Any runtime gate (NOT used in Phase 47 — gate is build-time, but documented here so planner doesn't accidentally introduce a duplicate runtime check).

```csharp
if (!ctx.SfzEnabled)
    throw new InvalidOperationException(
        "loadSfz requires 'use \"@sfz\"' at the top of your script");
```

Phase 47 takes the *opposite* tack — the entire `SfzBuiltins.Register` call is `#if`-ed out so `loadSfz` doesn't even register, AND the `use "@sfz"` import is gated in `ModuleLoader`. Belt-and-suspenders: composer sees the advisory at parse-time (use line) rather than at first call-time.

### Pattern 3 — Static type-flag init (existing precedent)

**Source:** `flow-lang/Core/FlowEngine.cs:80, 90, 99` (`CurrentSampleCache`, `CurrentSfzSampleCache`, `CurrentExecutionContext`)
**Apply to:** `FlowEngine.IsWebTarget` + `FlowEngine.SupportsLiveBlocks` (D-47-10).

```csharp
public static SampleCache? CurrentSampleCache { get; private set; }
```

Phase 47 flags are **set once at class load time** via `#if FLOW_WEB` static initializer (compile-time constant — no runtime mutation). No need for `private set;` accessor.

### Pattern 4 — `[Fact(Skip = ...)]` conditional test (existing precedent)

**Source:** `flow-lang.Tests/Integration/Phase39/MusicXmlRoundTripTests.cs:121`
**Apply to:** `FlowTargetFactAttribute` (subclass `FactAttribute`, conditionally set `Skip` property).

```csharp
[Fact(Skip = "requires mscore in PATH — XML-02 gate lights up automatically when CI provisions one")]
```

Phase 47 reuses the property — the attribute's `Skip` is set in the constructor based on `FLOW_WEB` define matching the test's declared targets.

### Pattern 5 — `IAudioBackend` static `IsAvailable` (existing precedent)

**Source:** `flow-lang/Audio/PulseAudioSimpleBackend.cs:23-34` (also `CoreAudioBackend.cs:50`)
**Apply to:** `WebAudioBackend.IsAvailable()` (one-liner returning `OperatingSystem.IsBrowser()`).

The IsAvailable surface is consumed by `AudioPlaybackManager.DetectBackend()` at line 144 (`CoreAudioBackend.IsAvailable()`) + line 152 (`PulseAudioSimpleBackend.IsAvailable()`).

### Pattern 6 — Charitable null-fallback for missing samples (existing precedent)

**Source:** `flow-lang/StandardLibrary/Audio/SampleCache.cs:265-285`
**Apply to:** D-47-11 Web-target sample-stripping. **Zero new code** — the existing fallback fires when `_rawCache.TryGetValue(rawKey, out var raw)` returns false (no samples loaded).

```csharp
public AudioBuffer? GetVarispeed(string instrument, int sampleMidi, string velocity, int semitonesShift)
{
    instrument = (instrument ?? string.Empty).ToLowerInvariant();
    var shiftedKey = (instrument, sampleMidi, velocity, semitonesShift);
    if (_shiftedCache.TryGetValue(shiftedKey, out var cached)) return cached;

    var rawKey = (instrument, sampleMidi, velocity);
    if (!_rawCache.TryGetValue(rawKey, out var raw)) return null;  // <-- fallback path
    // ...
}
```

The caller (SampledInstrumentRenderer) sees `null` and routes through synthesis. Web target: `flow-lang/Samples/**` is absent (D-47-11 says strip via `<None Remove>` but no `<None Update="Samples/...">` exists today — see csproj caveat above). **Verify at Plan 47-01** that the WASM published binary actually omits Samples; if implicit-include MSBuild behavior smuggles them in, add explicit `<Content Remove="Samples\**" Condition="'$(FlowTarget)' == 'Web'" />`.

## No Analog Found

Files with no close in-repo match. Planner should consult CONTEXT/RESEARCH for these patterns:

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `AssemblyReferenceScanTests.cs` | reflective test | reflection-only | No existing Mono.Cecil-based test in repo; pattern from RESEARCH. Pure new code. |
| `<FlowTarget>` MSBuild property | build property | MSBuild eval | No existing `Condition="..."` ItemGroups in any flow-sharp csproj (verified across `flow-lang.csproj`, `flow-interpreter.csproj`, `flow-lsp.csproj`, `flow-cli.csproj`, `flow-midi.csproj`, `flow-lang.Tests.csproj`). Phase 47 introduces the convention. |

## Metadata

**Analog search scope:**
- `flow-lang/` (entire library)
- `flow-interpreter/` (CLI — only LiveReloadManager confirmed location)
- `flow-lang.Tests/` (xUnit suite — Helpers/, Integration/, Unit/)
- `.planning/phases/47-compile-target-flavors/47-CONTEXT.md` (decisions D-47-01..14)
- `.planning/STATE.md` (project state, Phase 38/43 highlights)
- `CLAUDE.md` (project conventions)

**Files scanned:** ~18 (csproj + .cs analogs)
**Pattern extraction date:** 2026-05-25

**Discrepancies between CONTEXT and code (planner alerts):**

1. **CONTEXT line 17 mentions `RegisterSfz()` / `RegisterOsc()` / `RegisterMicInput()` / `RegisterLiveBlock()` in `BuiltInFunctions.cs`.** These method names do NOT exist in current code. The actual registration sites are in `FlowEngine.cs` (SfzBuiltins line 185, OscFunctions line 202) and `BuiltInFunctions.RegisterContextDependentFunctions` (InputFunctions line 1027). There is no `RegisterLiveBlock` because live blocks are AST-level (no builtin registration). **Planner must rewrite the strip strategy** to wrap actual call sites in `FlowEngine.cs`, not invent new wrapper methods.

2. **CONTEXT line 65 mentions `NullAudioBackend` at `IAudioBackend.cs:21-29`.** This class does not exist. The interface lives at `IAudioBackend.cs:7-72`; no Null impl. Planner can choose to introduce one or accept current throw-on-no-backend behavior.

3. **CONTEXT line 15 lists `Samples/**` for `<None Remove>` exclusion.** No existing `<None Update="Samples/...">` entry in `flow-lang.csproj` — Samples are loaded at runtime from `flow-lang/Samples` relative path (`SampleCache.cs:68`). Whether MSBuild's implicit content-include behavior pulls them into publish output is unverified — planner should verify at Plan 47-01 before adding the strip directive.

4. **CONTEXT line 15 lists `StandardLibrary/Network/Osc/**/*.cs`.** Actual location is `StandardLibrary/Network/OscFunctions.cs` + `StandardLibrary/Network/OscHandleData.cs` (no `Osc/` subdirectory). Strip glob should be `StandardLibrary/Network/Osc*.cs` or list both files explicitly.

5. **CONTEXT line 15 lists `Live/**/*.cs`.** No `flow-lang/Live/` directory exists. Live-coding artifacts are scattered: `Ast/Statements/LiveBlockStatement.cs`, `Runtime/LiveBlockRegistry.cs`, `Interpreter/LambdaCaptureAuditor.cs`. `LiveReloadManager.cs` lives in `flow-interpreter/` (out of `flow-lang.csproj` scope).
