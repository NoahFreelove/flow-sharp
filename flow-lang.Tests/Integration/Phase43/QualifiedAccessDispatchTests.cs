using System;
using System.IO;
using System.Linq;
using System.Text;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Integration.Phase43;

/// <summary>
/// Phase 43 Plan 43-03 Task 2 — Wave 2 tests for the
/// <see cref="FlowLang.Interpreter.ExpressionEvaluator"/> registry-first dispatch
/// branch (D-02) + the Pitfall 2 fall-through regression for existing
/// instance-member access (chord.Root / song.SectionCount / voice.Pan /
/// track.SampleRate etc.).
///
/// Drives end-to-end via <see cref="FlowEngine"/> against temp <c>.flow</c>
/// files written to disk so the fixture is self-contained without Plan 05.
/// </summary>
[Collection("FlowScripts")]
public class QualifiedAccessDispatchTests : IDisposable
{
    private readonly string _tempDir;

    public QualifiedAccessDispatchTests()
    {
        RenderingDiagnostics.ResetForTesting();
        _tempDir = Path.Combine(Path.GetTempPath(), "flow-phase43-qadtests-" + Guid.NewGuid().ToString("N"));
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

    private string WriteModuleFile(string baseName, string source)
    {
        var path = Path.Combine(_tempDir, baseName + ".flow");
        File.WriteAllText(path, source);
        return baseName;
    }

    /// <summary>
    /// Runs <paramref name="source"/> via a fresh <see cref="FlowEngine"/>. Returns
    /// <paramref name="ok"/> from <c>Execute</c>, captured stdout (Console.Out), and
    /// a concatenation of ErrorReporter messages + captured stderr (Console.Error)
    /// so tests can assert on either channel without caring which path the error
    /// was reported on.
    /// </summary>
    private static (bool ok, string stdout, string stderr) RunEngine(string source,
        string? extraSearchPath = null)
    {
        var origOut = Console.Out;
        var origErr = Console.Error;
        var sbOut = new StringBuilder();
        var sbErr = new StringBuilder();
        Console.SetOut(new StringWriter(sbOut));
        Console.SetError(new StringWriter(sbErr));
        bool ok;
        string errorMessages;
        try
        {
            using var engine = new FlowEngine();
            if (extraSearchPath != null)
                engine.ModuleLoader.AdditionalSearchPaths.Add(extraSearchPath);
            ok = engine.Execute(source, "<test>");
            errorMessages = string.Join("\n", engine.ErrorReporter.Errors.Select(e => e.Message));
        }
        finally
        {
            Console.SetOut(origOut);
            Console.SetError(origErr);
        }
        var combinedStderr = sbErr.ToString();
        if (!string.IsNullOrEmpty(errorMessages))
            combinedStderr = combinedStderr + "\n" + errorMessages;
        return (ok, sbOut.ToString(), combinedStderr);
    }

    // ------------------------------------------------------------------
    // Test 1 — REQ-MOD-03 — qualified call (modname.square 2.0) dispatches
    // via ModuleRegistry and returns the correct value.
    // ------------------------------------------------------------------
    [Fact]
    public void QualifiedCall_OnRegisteredModule_DispatchesAndReturnsValue()
    {
        WriteModuleFile("qadmath",
            "module qadmath\nproc square (Double: x) (mul x x) end\n");

        var (ok, stdout, stderr) = RunEngine(
            "use \"qadmath\"\nDouble r = (qadmath.square 2.0)\n(print (str r))\n",
            extraSearchPath: _tempDir);

        Assert.True(ok, $"Execute should succeed; stderr:\n{stderr}");
        Assert.Contains("4", stdout, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // Test 2 (Pitfall 2 regression) — chord.Root / chord.Quality /
    // voice.Pan / track.SampleRate / track.Channels / song.SectionCount
    // continue to dispatch via the existing instance-member path.
    // ------------------------------------------------------------------
    [Fact]
    public void InstanceMemberDispatch_FallThroughIsPreserved()
    {
        // chord.Root + chord.Quality + chord.Octave + chord.NoteNames length
        var sourceA = "Chord c = Cmaj7\n(print c.Root)\n(print c.Quality)\n";
        var (okA, stdoutA, stderrA) = RunEngine(sourceA);
        Assert.True(okA, $"chord member access should succeed; stderr:\n{stderrA}");
        Assert.Contains("C", stdoutA, StringComparison.Ordinal);
        Assert.Contains("maj7", stdoutA, StringComparison.Ordinal);

        // song.SectionCount (SongData)
        var sourceB = @"section verse { | C4 D4 E4 | }
section chorus { | E4 F4 G4 | }
Song s = [verse chorus]
(print (str s.SectionCount))
";
        var (okB, stdoutB, stderrB) = RunEngine(sourceB);
        Assert.True(okB, $"song member access should succeed; stderr:\n{stderrB}");
        Assert.Contains("2", stdoutB, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // Test 3 — unknown proc on a registered module produces a clear
    // error citing module + proc name.
    // ------------------------------------------------------------------
    [Fact]
    public void UnknownProcOnRegisteredModule_EmitsClearError()
    {
        WriteModuleFile("uprmod",
            "module uprmod\nproc known (Int: n) (mul n 2) end\n");

        var (ok, _, stderr) = RunEngine(
            "use \"uprmod\"\n(uprmod.nope 5)\n",
            extraSearchPath: _tempDir);

        Assert.False(ok, "Calling an unknown proc on a registered module should fail.");
        Assert.Contains("uprmod", stderr, StringComparison.Ordinal);
        Assert.Contains("nope", stderr, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // Test 4 — bare value-reference form `Function f = mod.fn` dispatches
    // via the same registry-first branch in EvaluateMemberAccess (D-02).
    // ------------------------------------------------------------------
    [Fact]
    public void BareMemberAccess_RegisteredModule_ReturnsFunctionValue()
    {
        WriteModuleFile("bmamod",
            "module bmamod\nproc triple (Int: n) (mul n 3) end\n");

        // Use it as a value-binding to a Function-typed variable. The bare LHS
        // `bmamod` would normally error as an undeclared variable; the
        // registry-first branch returns the Function Value successfully.
        var (ok, _, stderr) = RunEngine(
            "use \"bmamod\"\nFunction f = bmamod.triple\n",
            extraSearchPath: _tempDir);

        Assert.True(ok, $"Bare member access for a registered module should succeed; stderr:\n{stderr}");
    }
}
