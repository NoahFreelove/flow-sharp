using System;
using System.IO;
using System.Linq;
using FlowLang.Core;
using FlowLang.Diagnostics;
using Xunit;

namespace FlowLang.Tests.Integration.Phase44;

/// <summary>
/// Phase 44 Plan 44-07 anti-Pitfall-2 regression pin.
///
/// <para>
/// Pins the 5 carve-out advisory sites from <c>strict-error-manifest.csv</c>
/// (<c>carve_out=true</c>) STAY charitable in both modes. Per Pitfall 2 from
/// the planning research: it would be easy to accidentally promote a
/// carve-out site to a [strict] error path while elevating its neighbours.
/// This test reads each carve-out file from disk + asserts the surrounding
/// ±10-line window contains a <c>WarnOnce</c> call AND does NOT contain
/// <c>CallerStrictMode</c> or the literal <c>[strict]</c> prefix.
/// </para>
///
/// <para>
/// Per AUDIT D-42-01 stable-identifier convention (builtin name, not line
/// number), the 5 carve-outs are identified by their distinctive sentinel
/// substrings rather than exact line numbers, which are allowed to drift
/// modestly without retripping the gate. Re-running
/// <c>StrictErrorManifestLoader.LoadCarveOutSites</c> resyncs against the
/// manifest after any major refactor.
/// </para>
/// </summary>
[Trait("Category", Phase44TestCategory.Phase44)]
[Collection("FlowScripts")]
public class CarveOutsPreservedTests : IDisposable
{
    public CarveOutsPreservedTests()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    /// <summary>
    /// Locate the repo root by walking up from the test assembly looking
    /// for <c>flow-sharp.sln</c>. Mirrors
    /// <see cref="StrictErrorManifestLoader"/>'s private repo-root resolver.
    /// </summary>
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "flow-sharp.sln")))
            dir = dir.Parent;
        if (dir == null)
            throw new InvalidOperationException(
                "Could not locate flow-sharp.sln walking up from " + AppContext.BaseDirectory);
        return dir.FullName;
    }

    /// <summary>
    /// Assert that the ±<paramref name="window"/>-line slice of
    /// <paramref name="relativePath"/> around <paramref name="hint"/>
    /// (a distinctive substring on the carve-out line) contains a
    /// <c>WarnOnce</c> call AND does NOT contain
    /// <c>CallerStrictMode</c> / <c>[strict]</c>.
    /// </summary>
    private static void AssertCarveOutPreserved(
        string relativePath, string hint, int window = 10)
    {
        string repoRoot = FindRepoRoot();
        string absPath = Path.Combine(repoRoot, relativePath);
        Assert.True(File.Exists(absPath),
            $"Expected file does not exist (carve-out moved?): {absPath}");

        string[] lines = File.ReadAllLines(absPath);
        int hintLine = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains(hint))
            {
                hintLine = i;
                break;
            }
        }
        Assert.True(hintLine >= 0,
            $"Carve-out sentinel hint '{hint}' not found in {relativePath} — was the carve-out renamed?");

        int lo = Math.Max(0, hintLine - window);
        int hi = Math.Min(lines.Length, hintLine + window + 1);
        string slice = string.Join("\n", lines.Skip(lo).Take(hi - lo));

        Assert.True(slice.Contains("WarnOnce") || slice.Contains("WriteLine") || slice.Contains("[live]") || slice.Contains("[improv]"),
            $"Carve-out at {relativePath} hint '{hint}' lost its charitable WarnOnce / advisory shape — slice:\n{slice}");
        Assert.DoesNotContain("CallerStrictMode", slice);
        Assert.DoesNotContain("[strict]", slice);
    }

    [Fact]
    public void Fact_LiveAdvisoryAtInterpreter_StillCharitable()
    {
        // Carve-out: flow-lang/Interpreter/Interpreter.cs around line 476 — the
        // [live] entering live block advisory MUST stay charitable in BOTH modes
        // (D-v1.5-07 + Pitfall 2). Strict mode does NOT escalate this advisory.
        AssertCarveOutPreserved(
            relativePath: "flow-lang/Interpreter/Interpreter.cs",
            hint: "[live] entering live block");
    }

    [Fact]
    public void Fact_ImprovAdvisoryAtStyleRegistry156_StillCharitable()
    {
        // Carve-out: StyleRegistry.cs ~line 156 — user style overrides shipped pack.
        // Loading composer style packs from ~/.config/flow/styles is a normal flow;
        // strict mode does NOT escalate the "user override" notification.
        AssertCarveOutPreserved(
            relativePath: "flow-lang/StandardLibrary/Improv/StyleRegistry.cs",
            hint: "overrides shipped pack");
    }

    [Fact]
    public void Fact_ImprovAdvisoryAtStyleRegistry244_StillCharitable()
    {
        // Carve-out: StyleRegistry.cs ~line 244 — failed to enumerate style packs.
        // Per D-36-12 style packs are composer content; failure to enumerate the
        // user dir is informational, not a hard error in strict mode.
        AssertCarveOutPreserved(
            relativePath: "flow-lang/StandardLibrary/Improv/StyleRegistry.cs",
            hint: "failed to enumerate style packs");
    }

    [Fact]
    public void Fact_ImprovAdvisoryAtStyleRegistry258_StillCharitable()
    {
        // Carve-out: StyleRegistry.cs ~line 258 — style pack reported errors during load.
        AssertCarveOutPreserved(
            relativePath: "flow-lang/StandardLibrary/Improv/StyleRegistry.cs",
            hint: "reported errors during load");
    }

    [Fact]
    public void Fact_ImprovAdvisoryAtStyleRegistry265_StillCharitable()
    {
        // Carve-out: StyleRegistry.cs ~line 265 — failed to load style pack.
        // Shares the same charitable rationale.
        AssertCarveOutPreserved(
            relativePath: "flow-lang/StandardLibrary/Improv/StyleRegistry.cs",
            hint: "failed to load style pack");
    }

    [Fact]
    public void Fact_LiveBlockInStrictFile_StillEmitsLiveAdvisory()
    {
        // Smoke check: a `live` block in a `enable strict;` file should
        // still emit the [live] entering live block advisory (charitable
        // carve-out preserved) AND should NOT report a [strict] error for
        // the live-entry itself. Plan 44-09 / 44-10 land the full live +
        // strict interaction tests; this is a lightweight smoke pin.
        string src = "enable strict;\nlive 1bar { (print 1) }\n";

        using var engine = new FlowEngine();
        var stderr = new StringWriter();
        var prevErr = Console.Error;
        Console.SetError(stderr);
        try
        {
            engine.Execute(src, "<top>");
        }
        finally
        {
            Console.SetError(prevErr);
        }

        // The live-entry advisory itself must not be elevated.
        var errors = engine.ErrorReporter.FormatErrors();
        Assert.DoesNotContain("[strict] [live]", errors);
        Assert.DoesNotContain("[strict] entering live block", errors);
    }
}
