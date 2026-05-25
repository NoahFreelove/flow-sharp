using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FlowLang.Diagnostics;
using Xunit;

namespace FlowLang.Tests.Integration.Phase44;

/// <summary>
/// Phase 44 Plan 44-05 Task 2 — inventory regression pin for the post-rewrite
/// shape of <c>TransformFunctions.cs</c>.
///
/// <para>
/// Mirrors Phase 42 <c>ClampGrepConsistencyTests</c>: shells out via
/// <c>File.ReadAllText</c> + <see cref="Regex"/> (no bash dependency), then
/// pins the post-Plan-44-05 invariants:
/// </para>
///
/// <list type="bullet">
///   <item>ZERO raw-arg <c>Math.Clamp(args[N].As&lt;...&gt;(), ...)</c> sites
///         remain in TransformFunctions.cs — every §6a clamp has been
///         rewritten with a strict-branch check BEFORE the
///         <c>Math.Clamp(&lt;localRawVar&gt;, ...)</c> fallback.</item>
///   <item>≥13 <c>ctx.CallerStrictMode</c> reads in TransformFunctions.cs
///         (one per rewritten site; allow ≥ since helper vars + comments may
///         multiply the count).</item>
///   <item>5 carve-out sites (Interpreter.cs:476 + StyleRegistry.cs:156/244/
///         258/265) STILL contain their original <c>WarnOnce(</c> calls and
///         do NOT contain <c>[strict]</c> / <c>CallerStrictMode</c> —
///         Pitfall 2 anti-regression pin.</item>
/// </list>
///
/// <para>
/// Repo root resolution mirrors <see cref="StrictErrorManifestLoader"/> +
/// Phase 42 <c>ClampGrepConsistencyTests.FindRepoRoot</c>: walks up from
/// <see cref="AppContext.BaseDirectory"/> looking for <c>flow-sharp.sln</c>.
/// </para>
/// </summary>
[Trait("Category", Phase44TestCategory.Phase44)]
[Collection("FlowScripts")]
public class Phase44ClampGrepConsistencyTests : IDisposable
{
    /// <summary>
    /// The regex shape of a raw §6a input-perimeter clamp PRE-Plan-44-05.
    /// Post-rewrite, every such site has been replaced with
    /// <c>Math.Clamp(&lt;localRawVar&gt;, lo, hi)</c> in the non-strict path,
    /// which does NOT match this pattern (no <c>args[N].As&lt;...&gt;()</c>
    /// inside the Clamp arguments).
    /// </summary>
    private const string InputPerimeterClampPattern = @"Math\.Clamp\(args\[\d+\]\.As<";

    /// <summary>
    /// Lower bound on <c>ctx.CallerStrictMode</c> / <c>context.CallerStrictMode</c>
    /// reads in TransformFunctions.cs after Plan 44-05. The 9 enclosing methods
    /// (Quantize + 8 strict wrappers: Crescendo / Decrescendo / Swell /
    /// Ritardando / Accelerando / Humanize / HumanizeGaussian / Tremolo) each
    /// read CallerStrictMode at least once, so ≥9. We use ≥13 as the lower
    /// bound to match the AUDIT §6a row count + tolerate XML doc references.
    /// </summary>
    private const int CallerStrictModeMinOccurrences = 13;

    public Phase44ClampGrepConsistencyTests()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    [Fact]
    public void Fact_InputPerimeterClampsRewritten_ZeroRawArgClampsRemain()
    {
        string repoRoot = FindRepoRoot();
        string path = Path.Combine(
            repoRoot, "flow-lang", "StandardLibrary", "Transforms",
            "TransformFunctions.cs");
        Assert.True(File.Exists(path),
            $"TransformFunctions.cs missing at {path}");

        string body = File.ReadAllText(path);
        var matches = Regex.Matches(body, InputPerimeterClampPattern);

        Assert.True(matches.Count == 0,
            $"expected ZERO raw-arg Math.Clamp(args[N].As<...>(...)) sites in " +
            $"TransformFunctions.cs after Plan 44-05 rewrite; found {matches.Count}. " +
            $"Each surviving site should be rewritten to read the raw arg into a " +
            $"local var FIRST, check strict, then Math.Clamp(<localVar>, lo, hi) in " +
            $"the non-strict branch. Offending matches:\n" +
            string.Join("\n", matches.Select(m => m.Value)));
    }

    [Fact]
    public void Fact_CallerStrictModeReadCountAtLeast13()
    {
        string repoRoot = FindRepoRoot();
        string path = Path.Combine(
            repoRoot, "flow-lang", "StandardLibrary", "Transforms",
            "TransformFunctions.cs");
        Assert.True(File.Exists(path),
            $"TransformFunctions.cs missing at {path}");

        string body = File.ReadAllText(path);
        // Match both ctx.CallerStrictMode and context.CallerStrictMode (the
        // 8 strict-wrappers use `ctx.`; Quantize's inline branch uses `context.`).
        var matches = Regex.Matches(body, @"(ctx|context)\.CallerStrictMode");

        Assert.True(matches.Count >= CallerStrictModeMinOccurrences,
            $"expected ≥{CallerStrictModeMinOccurrences} CallerStrictMode reads " +
            $"in TransformFunctions.cs (one per Plan 44-05 strict-rewritten site); " +
            $"found {matches.Count}. A drift below ≥13 suggests one of the 9 enclosing " +
            $"methods lost its strict branch.");
    }

    [Fact]
    public void Fact_CarveOutLiveAdvisoryStillCharitable()
    {
        // Pitfall 2 anti-regression pin: the [live] block-entry advisory must
        // STAY charitable (WarnOnce, not ErrorReporter). The Interpreter.cs
        // file is owned by Plan 38 LIVE-01; Plan 44-05 must NOT touch it.
        //
        // NOTE: Interpreter.cs DOES legitimately contain `CallerStrictMode`
        // references from Plan 44-02's call-boundary save/restore — those are
        // unrelated to the [live] advisory and MUST be preserved. We scope
        // this Fact to the immediate line-region around the `[live]` literal
        // and assert NEITHER `[strict]` NOR `ReportError` appears in that
        // window, NOR is the WarnOnce-style emission removed.
        string repoRoot = FindRepoRoot();
        string path = Path.Combine(
            repoRoot, "flow-lang", "Interpreter", "Interpreter.cs");
        Assert.True(File.Exists(path), $"Interpreter.cs missing at {path}");

        var lines = File.ReadAllLines(path);

        // Find the line containing the `[live] entering live block` literal —
        // the carve-out site per manifest row 115 (Interpreter.cs:476).
        int liveLineIdx = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("[live] entering live block"))
            {
                liveLineIdx = i;
                break;
            }
        }
        Assert.True(liveLineIdx >= 0,
            "[live] entering live block advisory literal missing — carve-out site moved or deleted");

        // Inspect a ±10 line window around the carve-out site for strict
        // elevation indicators. The window is generous enough to catch a
        // refactor that hoists the strict check above the WarnOnce site
        // (Pitfall 2) but tight enough to exclude unrelated Plan 44-02
        // call-boundary CallerStrictMode references.
        int lo = Math.Max(0, liveLineIdx - 10);
        int hi = Math.Min(lines.Length - 1, liveLineIdx + 10);
        string window = string.Join("\n", lines[lo..(hi + 1)]);

        Assert.DoesNotContain("[strict]", window);
        Assert.DoesNotContain("ReportError", window);
        Assert.DoesNotContain("CallerStrictMode", window);
    }

    [Fact]
    public void Fact_CarveOutImprovStillCharitable()
    {
        // 4 [improv] carve-out lines (manifest rows 156/244/258/265):
        // StyleRegistry.cs MUST still contain its WarnOnce calls and must NOT
        // contain CallerStrictMode / [strict] (Pitfall 2 anti-regression pin).
        string repoRoot = FindRepoRoot();
        string path = Path.Combine(
            repoRoot, "flow-lang", "StandardLibrary", "Improv", "StyleRegistry.cs");
        Assert.True(File.Exists(path), $"StyleRegistry.cs missing at {path}");

        string body = File.ReadAllText(path);

        // [improv] advisories present + WarnOnce-style emission present.
        Assert.Contains("[improv]", body);
        Assert.Contains("WarnOnce", body);

        // No strict elevation leaked into the carve-out site.
        Assert.DoesNotContain("CallerStrictMode", body);
        Assert.DoesNotContain("[strict]", body);
    }

    // -------------------------------------------------------------------------
    // Helpers — mirrors Phase 42 ClampGrepConsistencyTests.FindRepoRoot.
    // -------------------------------------------------------------------------
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "flow-sharp.sln")))
            dir = dir.Parent;
        if (dir == null)
            throw new InvalidOperationException(
                "Could not locate flow-sharp.sln walking up from " +
                AppContext.BaseDirectory);
        return dir.FullName;
    }
}
