using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Integration.Phase43;

/// <summary>
/// Phase 43 Plan 43-03 — Wave 2 tests for the ModuleLoader registration
/// hook + Interpreter ModuleDeclarationStatement no-op (Task 1) and the
/// D-04 last-import-wins shadow advisory (Task 2).
///
/// Drives end-to-end via <see cref="FlowEngine"/> against temp <c>.flow</c>
/// files written to disk + <c>ModuleLoader.AdditionalSearchPaths</c> so the
/// fixture is self-contained and does NOT depend on Plan 05 stdlib migration.
///
/// Per CONTEXT D-04 (last-import-wins shadow advisory) + D-05 (Register at
/// use-time) + D-06 (duplicate-module advisory; one-shot per process via
/// <see cref="RenderingDiagnostics.WarnOnce"/>).
///
/// Pattern: stderr-capture + dedup-across-runs (mirrors
/// <see cref="FlowLang.Tests.Integration.Phase38.LiveBlockDeterminismAdvisoryTests"/>
/// and <see cref="FlowLang.Tests.Integration.Phase37.StretchAutoAdvisoryTests"/>).
/// </summary>
[Collection("FlowScripts")]
public class ModuleCollisionAdvisoryTests : IDisposable
{
    private readonly string _tempDir;

    public ModuleCollisionAdvisoryTests()
    {
        RenderingDiagnostics.ResetForTesting();
        _tempDir = Path.Combine(Path.GetTempPath(), "flow-phase43-modtests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch { /* test-cleanup best-effort */ }
    }

    /// <summary>
    /// Captures stderr while <paramref name="action"/> runs; returns the
    /// captured text. Restores Console.Error on exit. Mirrors Phase 37
    /// StretchAutoAdvisoryTests.CaptureStderr.
    /// </summary>
    private static string CaptureStderr(Action action)
    {
        var original = Console.Error;
        var sb = new StringBuilder();
        var writer = new StringWriter(sb);
        Console.SetError(writer);
        try
        {
            action();
        }
        finally
        {
            Console.SetError(original);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Writes a .flow file at <c>_tempDir/&lt;baseName&gt;.flow</c> with the
    /// given source. Returns the bare module name (without .flow extension)
    /// suitable for <c>use "&lt;name&gt;"</c> when combined with
    /// <see cref="ModuleLoader.AdditionalSearchPaths"/>.
    /// </summary>
    private string WriteModuleFile(string baseName, string source)
    {
        var path = Path.Combine(_tempDir, baseName + ".flow");
        File.WriteAllText(path, source);
        return baseName;
    }

    // ------------------------------------------------------------------
    // Test 1 — module declaration registers the module + its exported procs
    // ------------------------------------------------------------------
    [Fact]
    public void ModuleDeclarationRegistersModuleAndProcs()
    {
        WriteModuleFile("mathmod",
            "module mathmod\nproc square (Double: x) (mul x x) end\n");

        using var engine = new FlowEngine();
        engine.ModuleLoader.AdditionalSearchPaths.Add(_tempDir);

        var ok = engine.Execute("use \"mathmod\"\n", "<test>");
        Assert.True(ok);

        Assert.True(engine.Context.ModuleRegistry.Contains("mathmod"),
            "Expected ModuleRegistry.Contains(\"mathmod\") == true after use.");
        Assert.True(engine.Context.ModuleRegistry.TryGetProc("mathmod", "square", out var proc));
        Assert.NotNull(proc);
        Assert.IsType<FlowLang.TypeSystem.PrimitiveTypes.FunctionType>(proc!.Type);
    }

    // ------------------------------------------------------------------
    // Test 2 — module-less file does NOT register anything
    // ------------------------------------------------------------------
    [Fact]
    public void ModuleLessFile_DoesNotRegisterInRegistry_StillExposesProcsUnqualified()
    {
        WriteModuleFile("noheader",
            "proc bareproc (Int: n) (mul n 2) end\n");

        using var engine = new FlowEngine();
        engine.ModuleLoader.AdditionalSearchPaths.Add(_tempDir);

        var ok = engine.Execute("use \"noheader\"\n", "<test>");
        Assert.True(ok);

        // No entry for the file's basename
        Assert.False(engine.Context.ModuleRegistry.Contains("noheader"));

        // But the proc is still in the global frame as before (back-compat per D-05).
        var overloads = engine.Context.GlobalFrame.GetFunctionOverloads("bareproc");
        Assert.NotEmpty(overloads);
    }

    // ------------------------------------------------------------------
    // Test 3 — two files declaring `module X` produce one duplicate-module
    // advisory (D-06), and re-running same source does NOT re-emit.
    // ------------------------------------------------------------------
    [Fact]
    public void DuplicateModuleName_EmitsOneShotAdvisory_AndDedupsAcrossRuns()
    {
        WriteModuleFile("dupa",
            "module dupmod\nproc fa (Int: n) (mul n 2) end\n");
        WriteModuleFile("dupb",
            "module dupmod\nproc fb (Int: n) (mul n 3) end\n");

        var stderr1 = CaptureStderr(() =>
        {
            using var engine = new FlowEngine();
            engine.ModuleLoader.AdditionalSearchPaths.Add(_tempDir);
            var ok = engine.Execute("use \"dupa\"\nuse \"dupb\"\n", "<test>");
            Assert.True(ok);
        });

        // Exactly one duplicate-module advisory.
        var matches = Regex.Matches(stderr1,
            @"\[module\] duplicate module name 'dupmod'");
        Assert.Equal(1, matches.Count);
        Assert.Contains("last load wins", stderr1, StringComparison.Ordinal);

        // Re-execute the SAME source in a NEW FlowEngine. WarnOnce dedup
        // is per-process — the second run must NOT emit a fresh advisory.
        var stderr2 = CaptureStderr(() =>
        {
            using var engine2 = new FlowEngine();
            engine2.ModuleLoader.AdditionalSearchPaths.Add(_tempDir);
            var ok2 = engine2.Execute("use \"dupa\"\nuse \"dupb\"\n", "<test>");
            Assert.True(ok2);
        });

        var matches2 = Regex.Matches(stderr2,
            @"\[module\] duplicate module name 'dupmod'");
        Assert.Equal(0, matches2.Count);
    }

    // ------------------------------------------------------------------
    // Test 4 — Pitfall 7 — two `use` lines pointing at the SAME file do NOT
    // re-register / do NOT fire the duplicate-module advisory.
    // ------------------------------------------------------------------
    [Fact]
    public void SameFileUsedTwice_DoesNotEmitDuplicateAdvisory()
    {
        WriteModuleFile("singleton",
            "module singleton\nproc once (Int: n) (mul n 7) end\n");

        var stderr = CaptureStderr(() =>
        {
            using var engine = new FlowEngine();
            engine.ModuleLoader.AdditionalSearchPaths.Add(_tempDir);
            var ok = engine.Execute("use \"singleton\"\nuse \"singleton\"\n", "<test>");
            Assert.True(ok);
        });

        Assert.False(RenderingDiagnostics.WasWarnedForTesting("module-dup:singleton"),
            $"Pitfall 7 — second use must NOT trigger duplicate-module advisory; got stderr:\n{stderr}");

        var matches = Regex.Matches(stderr, @"\[module\] duplicate module name");
        Assert.Equal(0, matches.Count);
    }

    // ------------------------------------------------------------------
    // Test 5 — Interpreter encountering ModuleDeclarationStatement at
    // execute-time is a no-op (does not throw, does not print).
    // ------------------------------------------------------------------
    [Fact]
    public void ModuleDeclarationStatement_AtExecuteTime_IsNoOp()
    {
        // Module declaration in the TOP-LEVEL program (not loaded as an import).
        // ModuleLoader does NOT see this — only the Interpreter's main dispatch.
        var source = "module topmod\nproc dummy (Int: n) (mul n 1) end\n";

        var stderr = CaptureStderr(() =>
        {
            using var engine = new FlowEngine();
            var ok = engine.Execute(source, "<test>");
            Assert.True(ok, "Top-level module declaration should not error at execute-time.");
        });

        // The Interpreter's ModuleDeclarationStatement arm prints nothing.
        Assert.DoesNotContain("[module]", stderr, StringComparison.Ordinal);
    }

}
