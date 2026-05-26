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
///   3. Three Facts: exit code, bundle structure, bundle size
///
/// All tests are plain xUnit Facts (NOT FlowTargetFact-gated) — they shell out
/// to a separate <c>dotnet publish</c> process so they run from the Desktop test
/// runner regardless of FLOW_WEB.
///
/// Layout note (deviation from PLAN.md must-haves):
///   The PLAN's "AppBundle/_framework/dotnet.js" layout applies to Blazor
///   WebAssembly *application* projects. flow-lang.csproj is a *library*
///   project (Microsoft.NET.Sdk + RuntimeIdentifier=browser-wasm), so the
///   publish output is flat at <c>publish/</c> with dotnet.js + dotnet.native.wasm
///   + flow-lang.dll alongside each other. Both layouts are valid WASM publish
///   shapes; the test checks both to stay resilient to future SDK changes.
/// </summary>
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
    /// Locate the publish directory containing the WASM artifacts. Library publish
    /// outputs to <c>bin/Release/net10.0/browser-wasm/publish/</c> directly; Blazor app
    /// publish nests under <c>publish/AppBundle/_framework/</c>. Returns whichever
    /// layout actually produced a <c>dotnet.js</c>.
    /// </summary>
    private static string LocateWasmFrameworkDir(string repoRoot)
    {
        var basePublish = Path.Combine(
            repoRoot, "flow-lang", "bin", "Release", "net10.0", "browser-wasm", "publish");
        // Library layout: publish/dotnet.js
        if (File.Exists(Path.Combine(basePublish, "dotnet.js")))
            return basePublish;
        // Blazor app layout: publish/AppBundle/_framework/dotnet.js
        var appBundleFramework = Path.Combine(basePublish, "AppBundle", "_framework");
        if (File.Exists(Path.Combine(appBundleFramework, "dotnet.js")))
            return appBundleFramework;
        // Not found — return the base path for diagnostic error message
        return basePublish;
    }

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
        var (code, stdout, stderr) = RunDotnetPublish("-p:FlowTarget=Web -c Release");
        Assert.True(code == 0, $"Publish must succeed before checking bundle.\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");

        var repoRoot = FindRepoRoot();
        var frameworkDir = LocateWasmFrameworkDir(repoRoot);

        // Canonical Mono-WASM marker — dotnet.js is the JS loader entry point.
        var dotnetJs = Path.Combine(frameworkDir, "dotnet.js");
        Assert.True(File.Exists(dotnetJs),
            $"WASM publish missing dotnet.js — expected at {dotnetJs}");

        // flow-lang.dll is the IL-to-WASM-compiled Flow runtime.
        var flowLangDll = Path.Combine(frameworkDir, "flow-lang.dll");
        Assert.True(File.Exists(flowLangDll),
            $"WASM publish missing flow-lang.dll — expected at {flowLangDll}");

        // dotnet.native.wasm is the Mono runtime compiled to WASM bytecode.
        var dotnetWasm = Path.Combine(frameworkDir, "dotnet.native.wasm");
        Assert.True(File.Exists(dotnetWasm),
            $"WASM publish missing dotnet.native.wasm — expected at {dotnetWasm}");
    }

    [Fact]
    public void WasmBundle_UncompressedSize_MeasuredAndRecorded()
    {
        var (code, stdout, stderr) = RunDotnetPublish("-p:FlowTarget=Web -c Release");
        Assert.True(code == 0, $"Publish must succeed before measuring.\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");

        var repoRoot = FindRepoRoot();
        var frameworkDir = LocateWasmFrameworkDir(repoRoot);

        // Measure only browser-shippable artifacts: .dll (IL assemblies), .wasm (Mono runtime),
        // .js (loader glue), .dat (ICU data), .flow (stdlib modules at runtime), .json (manifests),
        // .md (license/credit attribution if present).
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
