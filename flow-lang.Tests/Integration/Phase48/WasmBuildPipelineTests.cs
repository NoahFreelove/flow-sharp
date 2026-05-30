using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xunit;

namespace FlowLang.Tests.Integration.Phase48;

/// <summary>
/// Phase 48 Plan 48-01 — Pin acceptance of the FlowTarget=Web WASM publish
/// pipeline. Each test shells out to `dotnet publish flow-lang/flow-lang.csproj`
/// and asserts exit 0 + the resulting WASM bundle is well-formed + measured
/// bundle size below the 30 MB uncompressed hard cap (D-48-05).
///
/// Modeled directly on Phase 47's <see cref="Phase47.BuildConditioningSmokeTests"/>
/// with three differences:
///   1. <c>publish</c> instead of <c>build</c> (WASM AppBundle is publish-only output)
///   2. 10-minute timeout instead of 2 (Mono-WASM publish is slow — jiterpreter
///      generation alone takes ~30s)
///   3. Three Facts: exit code, boot-manifest bundle structure, bundle size
///
/// All tests are plain xUnit Facts (NOT FlowTargetFact-gated) — they shell out
/// to a separate <c>dotnet publish</c> process so they run from the Desktop test
/// runner regardless of FLOW_WEB.
///
/// Layout note (fix(48-06) — see .planning/debug/wasm-boot-no-app-bundle.md):
///   FlowTarget=Web now forces <c>WasmGenerateAppBundle=true</c> +
///   <c>IsBrowserWasmProject=true</c> + <c>WasmMainJSPath</c> so the Mono-WASM
///   runtime pack emits a real BOOTABLE app bundle. The servable bundle root is
///   <c>browser-wasm/AppBundle/</c> (NOT under <c>publish/</c>) — that is where
///   <c>WasmAppDir</c> defaults (<c>$(OutputPath)/AppBundle</c>). The runtime
///   files live under <c>AppBundle/_framework/</c>: <c>dotnet.js</c>,
///   <c>dotnet.boot.js</c> (the boot manifest, <c>mainAssemblyName: flow-lang.dll</c>),
///   <c>dotnet.native.wasm</c>, and the assemblies as Webcil <c>.wasm</c> files
///   (<c>flow-lang.wasm</c>, <c>System.*.wasm</c>) — NOT <c>.dll</c>. The pre-fix
///   flat <c>publish/</c> tree (with real PE <c>.dll</c> files but NO
///   <c>dotnet.boot.js</c>) is still emitted alongside; the helper below prefers
///   the bootable AppBundle layout and falls back to the flat tree only for
///   resilience.
/// </summary>
[Collection(WasmWebPublishCollection.Name)]
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
        throw new InvalidOperationException(
            "Could not locate repo root from " + AppContext.BaseDirectory);
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
        // 10-minute cap — WASM publish is slow; jiterpreter generation alone takes ~30s
        // and full AOT cross-compile of System.Private.CoreLib can hit 2-3 minutes.
        if (!p.WaitForExit(600_000))
        {
            try { p.Kill(); } catch { /* best-effort */ }
            return (-1, stdout, stderr + "\n[test] WaitForExit timed out at 10 minutes");
        }
        return (p.ExitCode, stdout, stderr);
    }

    /// <summary>
    /// Locate the directory containing the WASM runtime artifacts (dotnet.js).
    /// fix(48-06): the bootable AppBundle lands at
    /// <c>bin/Release/net10.0/browser-wasm/AppBundle/_framework/</c>. We probe
    /// that first; the flat library-publish layout (<c>publish/dotnet.js</c>,
    /// pre-fix, NO boot manifest) is kept only as a resilience fallback so the
    /// helper never throws if a future SDK changes the default <c>WasmAppDir</c>.
    /// </summary>
    private static string LocateWasmFrameworkDir(string repoRoot)
    {
        var browserWasm = Path.Combine(
            repoRoot, "flow-lang", "bin", "Release", "net10.0", "browser-wasm");

        // Bootable AppBundle layout (fix(48-06)): browser-wasm/AppBundle/_framework/dotnet.js
        var appBundleFramework = Path.Combine(browserWasm, "AppBundle", "_framework");
        if (File.Exists(Path.Combine(appBundleFramework, "dotnet.js")))
            return appBundleFramework;

        // Nested-under-publish layout (some SDK configurations): publish/AppBundle/_framework/
        var publishAppBundle = Path.Combine(browserWasm, "publish", "AppBundle", "_framework");
        if (File.Exists(Path.Combine(publishAppBundle, "dotnet.js")))
            return publishAppBundle;

        // Flat library-publish layout (pre-fix fallback): publish/dotnet.js
        var flatPublish = Path.Combine(browserWasm, "publish");
        if (File.Exists(Path.Combine(flatPublish, "dotnet.js")))
            return flatPublish;

        // Not found — return the expected bootable path for the diagnostic message.
        return appBundleFramework;
    }

    [Fact]
    public void WasmPublish_ExitCodeIsZero()
    {
        var (code, stdout, stderr) = RunDotnetPublish("-p:FlowTarget=Web -c Release");
        Assert.True(code == 0,
            $"Expected exit 0 with FlowTarget=Web publish, got {code}.\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
    }

    /// <summary>
    /// fix(48-06) REGRESSION GATE. The original (pre-fix) version of this test
    /// was MISNAMED — it asserted only <c>dotnet.js</c> / <c>flow-lang.dll</c> /
    /// <c>dotnet.native.wasm</c> in the flat publish tree, which were ALL present
    /// even though NO <c>dotnet.boot.js</c> was emitted. That is the exact
    /// defect-class gap that let the no-boot-manifest bug ship: the browser
    /// <c>dotnet.create()</c> 404s without <c>dotnet.boot.js</c>. This version
    /// asserts the BOOT MANIFEST exists in a coherent servable layout — the
    /// strongest automated proxy for "the runtime actually boots in a browser".
    /// </summary>
    [Fact]
    public void WasmPublish_ProducesBootableAppBundle()
    {
        var (code, stdout, stderr) = RunDotnetPublish("-p:FlowTarget=Web -c Release");
        Assert.True(code == 0, $"Publish must succeed before checking bundle.\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");

        var repoRoot = FindRepoRoot();
        var frameworkDir = LocateWasmFrameworkDir(repoRoot);

        // THE regression gate: dotnet.boot.js is the boot manifest dotnet.create()
        // fetches. Its absence is what produced the original
        // "Failed to load config file dotnet.boot.js" 404 in the browser. .NET 10
        // names it dotnet.boot.js; accept blazor.boot.json as a forward-compat
        // alias in case a future SDK reverts the naming.
        bool bootManifest =
            File.Exists(Path.Combine(frameworkDir, "dotnet.boot.js")) ||
            File.Exists(Path.Combine(frameworkDir, "blazor.boot.json"));
        Assert.True(bootManifest,
            $"WASM publish missing the boot manifest (dotnet.boot.js) — expected in {frameworkDir}. " +
            $"Without it dotnet.create() 404s in the browser. This is the fix(48-06) regression gate.");

        // Canonical Mono-WASM marker — dotnet.js is the JS loader entry point.
        var dotnetJs = Path.Combine(frameworkDir, "dotnet.js");
        Assert.True(File.Exists(dotnetJs),
            $"WASM publish missing dotnet.js — expected at {dotnetJs}");

        // dotnet.native.wasm is the Mono runtime compiled to WASM bytecode.
        var dotnetWasm = Path.Combine(frameworkDir, "dotnet.native.wasm");
        Assert.True(File.Exists(dotnetWasm),
            $"WASM publish missing dotnet.native.wasm — expected at {dotnetWasm}");

        // The main Flow assembly. In the bootable AppBundle layout assemblies are
        // Webcil-encoded (flow-lang.wasm); in the flat fallback layout it is a PE
        // flow-lang.dll. Accept either so the gate is layout-resilient.
        bool mainAssembly =
            File.Exists(Path.Combine(frameworkDir, "flow-lang.wasm")) ||
            File.Exists(Path.Combine(frameworkDir, "flow-lang.dll"));
        Assert.True(mainAssembly,
            $"WASM publish missing the main Flow assembly (flow-lang.wasm or flow-lang.dll) " +
            $"— expected in {frameworkDir}");
    }

    [Fact]
    public void WasmBundle_UncompressedSize_MeasuredAndRecorded()
    {
        var (code, stdout, stderr) = RunDotnetPublish("-p:FlowTarget=Web -c Release");
        Assert.True(code == 0, $"Publish must succeed before measuring.\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");

        var repoRoot = FindRepoRoot();
        var frameworkDir = LocateWasmFrameworkDir(repoRoot);

        // Measure only browser-shippable artifacts: .dll (IL assemblies), .wasm (Mono runtime
        // + Webcil assemblies), .js (loader glue), .dat (ICU data), .flow (stdlib modules at
        // runtime), .json (manifests), .md (license/credit attribution if present).
        //
        // Excluded by design: .a (static-link archives — build-time only, not shipped),
        // .c/.h/.rsp (Emscripten build artifacts).
        long total = new DirectoryInfo(frameworkDir)
            .EnumerateFiles("*", SearchOption.AllDirectories)
            .Where(f =>
                f.Extension == ".dll" || f.Extension == ".wasm" || f.Extension == ".js" ||
                f.Extension == ".dat" || f.Extension == ".flow" || f.Extension == ".json" ||
                f.Extension == ".md")
            .Sum(f => f.Length);

        // Print so CI can capture in test output for size-trend tracking.
        Console.WriteLine(
            $"[WasmBundle] uncompressed shipped-artifact size: {total / 1_000_000} MB ({total} bytes)");

        // Phase 48 D-48-05: 15 MB compressed target ≈ 30 MB uncompressed after Brotli.
        // Soft assert at 30 MB — if exceeded, Plan 48-05 lazy-loading kicks in.
        Assert.True(total < 30L * 1024L * 1024L,
            $"WASM bundle uncompressed shipped artifacts {total / 1_000_000} MB exceeds 30 MB hard cap " +
            $"(15 MB target compressed). Plan 48-05 lazy-load required.");
    }
}
