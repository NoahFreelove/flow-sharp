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
[Collection(WasmWebPublishCollection.Name)]
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
    /// Locate a REAL PE flow-lang.dll that Mono.Cecil can read for the assembly-
    /// reference scan. fix(48-06): the bootable AppBundle encodes assemblies as
    /// Webcil (<c>flow-lang.wasm</c>) which stock Cecil cannot parse — but the
    /// publish step ALSO emits the flat tree with the un-Webcil'd PE
    /// <c>publish/flow-lang.dll</c> alongside the AppBundle, so the metadata scan
    /// keeps reading that. The <c>file</c> probe confirms it is a real PE assembly.
    /// </summary>
    private static string LocatePublishedFlowLangDll(string repoRoot)
    {
        var browserWasm = Path.Combine(
            repoRoot, "flow-lang", "bin", "Release", "net10.0", "browser-wasm");
        // Flat publish tree — real PE flow-lang.dll (Cecil-readable).
        var flat = Path.Combine(browserWasm, "publish", "flow-lang.dll");
        if (File.Exists(flat)) return flat;
        // Some SDK configurations nest the flat tree differently.
        var nested = Path.Combine(browserWasm, "publish", "AppBundle", "_framework", "flow-lang.dll");
        if (File.Exists(nested)) return nested;
        // Last resort: the build-output flow-lang.dll (pre-publish PE).
        var buildOutput = Path.Combine(browserWasm, "flow-lang.dll");
        if (File.Exists(buildOutput)) return buildOutput;
        // Not found — return the flat path for diagnostic error message.
        return flat;
    }

    /// <summary>
    /// Locate the bootable AppBundle's Webcil main assembly (flow-lang.wasm).
    /// fix(48-06): in the AppBundle layout the assemblies ship as Webcil .wasm,
    /// so the presence check uses that rather than a .dll.
    /// </summary>
    private static string LocateBundledFlowLangWasm(string repoRoot)
    {
        var browserWasm = Path.Combine(
            repoRoot, "flow-lang", "bin", "Release", "net10.0", "browser-wasm");
        var appBundle = Path.Combine(browserWasm, "AppBundle", "_framework", "flow-lang.wasm");
        if (File.Exists(appBundle)) return appBundle;
        var publishAppBundle = Path.Combine(
            browserWasm, "publish", "AppBundle", "_framework", "flow-lang.wasm");
        if (File.Exists(publishAppBundle)) return publishAppBundle;
        return appBundle;
    }

    [Fact]
    public void FlowLangDll_PublishedToAppBundle()
    {
        var (code, stdout, stderr) = RunDotnetPublish("-p:FlowTarget=Web -c Release");
        Assert.True(code == 0,
            $"Expected publish exit 0, got {code}.\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");

        var repoRoot = FindRepoRoot();

        // fix(48-06): the bootable AppBundle ships the main assembly as Webcil
        // flow-lang.wasm under AppBundle/_framework/. Assert that artifact exists.
        var bundledWasm = LocateBundledFlowLangWasm(repoRoot);
        Assert.True(File.Exists(bundledWasm),
            $"Bootable AppBundle missing the Webcil main assembly flow-lang.wasm — " +
            $"expected at {bundledWasm}.");

        // Sanity floor — a stripped-to-zero assembly would be < 1 KB.
        var info = new FileInfo(bundledWasm);
        Assert.True(info.Length > 1024,
            $"Bundled flow-lang.wasm suspiciously small: {info.Length} bytes at {bundledWasm}");
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
