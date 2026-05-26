using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Xunit;

namespace FlowLang.Tests.Integration.Phase48;

/// <summary>
/// Phase 48 Plan 48-02 — Post-publish assertion that the WASM-published
/// flow-lang.dll retains its Melanchall.DryWetMidi 8.0.3 assembly reference
/// reachably. Extends Phase 47-04's compile-time smoke
/// (<see cref="Phase47.DryWetMidiWasmCompatTests"/>, which proves the API is
/// callable on Desktop with FlowTarget=Web compiled) into the actual Mono-WASM
/// publish output (which the linker / trim analyzer could otherwise strip).
///
/// If <see cref="FlowLangDll_RetainsDryWetMidiReference"/> fires RED, escalate
/// to D-48-17 fallback: strip DryWetMidi from the Web build and have writeMidi
/// emit a parse-time advisory ("MIDI file write unavailable on Web target —
/// open Desktop to export"). v1.6 backlog: hand-rolled MIDI writer for Web.
///
/// Both Facts are plain <c>[Fact]</c> (NOT FlowTargetFact-gated) — they shell
/// out to a separate <c>dotnet publish</c> process and inspect its output via
/// Mono.Cecil, so they run from the Desktop test runner regardless of
/// FLOW_WEB.
/// </summary>
public class DryWetMidiWasmPublishTests
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
    /// Locate the published flow-lang.dll. Library publish outputs to
    /// <c>bin/Release/net10.0/browser-wasm/publish/flow-lang.dll</c> directly;
    /// Blazor app publish nests under <c>publish/AppBundle/_framework/flow-lang.dll</c>.
    /// Returns whichever path actually produced the dll.
    /// </summary>
    private static string LocatePublishedFlowLangDll(string repoRoot)
    {
        var basePublish = Path.Combine(
            repoRoot, "flow-lang", "bin", "Release", "net10.0", "browser-wasm", "publish");
        // Library layout: publish/flow-lang.dll
        var flat = Path.Combine(basePublish, "flow-lang.dll");
        if (File.Exists(flat)) return flat;
        // Blazor app layout: publish/AppBundle/_framework/flow-lang.dll
        var nested = Path.Combine(basePublish, "AppBundle", "_framework", "flow-lang.dll");
        if (File.Exists(nested)) return nested;
        // Not found — return the flat path for diagnostic error message
        return flat;
    }

    [Fact]
    public void FlowLangDll_PublishedToAppBundle()
    {
        var (code, stdout, stderr) = RunDotnetPublish("-p:FlowTarget=Web -c Release");
        Assert.True(code == 0,
            $"Expected publish exit 0, got {code}.\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");

        var repoRoot = FindRepoRoot();
        var publishedDll = LocatePublishedFlowLangDll(repoRoot);

        Assert.True(File.Exists(publishedDll),
            $"Published flow-lang.dll missing — expected at {publishedDll} " +
            $"(library-publish flat layout or Blazor AppBundle/_framework/ layout).");

        // Sanity floor — a stripped-to-zero assembly would be < 1 KB.
        var info = new FileInfo(publishedDll);
        Assert.True(info.Length > 1024,
            $"Published flow-lang.dll suspiciously small: {info.Length} bytes at {publishedDll}");
    }

    [Fact]
    public void FlowLangDll_RetainsDryWetMidiReference()
    {
        var (code, stdout, stderr) = RunDotnetPublish("-p:FlowTarget=Web -c Release");
        Assert.True(code == 0,
            $"Publish must succeed before scanning.\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");

        var repoRoot = FindRepoRoot();
        var publishedDll = LocatePublishedFlowLangDll(repoRoot);
        Assert.True(File.Exists(publishedDll),
            $"Published flow-lang.dll missing — expected at {publishedDll}");

        // Read the published .dll's metadata (NOT the test runner's copy of
        // flow-lang.dll — that's the Desktop build). Mono.Cecil reads the
        // metadata table without IL execution, so this is safe + cheap.
        using var asm = AssemblyDefinition.ReadAssembly(publishedDll);
        var refs = asm.MainModule.AssemblyReferences.Select(r => r.Name).ToList();

        // Trim analyzer + linker should have KEPT the DryWetMidi reference
        // because MidiExport.cs uses MidiFile.Write / NoteOnEvent / etc.
        // unconditionally (no #if !FLOW_WEB strip on that file).
        bool retained = refs.Any(name =>
            name.StartsWith("Melanchall.DryWetMidi", StringComparison.Ordinal));

        Assert.True(retained,
            $"Expected Melanchall.DryWetMidi assembly reference in published flow-lang.dll " +
            $"(escalate to D-48-17 fallback if absent — strip DryWetMidi from Web build, " +
            $"writeMidi becomes parse-time advisory).\n" +
            $"Actual references: {string.Join(", ", refs)}");
    }
}
