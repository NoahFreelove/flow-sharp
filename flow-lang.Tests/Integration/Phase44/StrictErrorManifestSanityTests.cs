using System;
using System.IO;
using System.Linq;
using FlowLang.Diagnostics;
using Xunit;

namespace FlowLang.Tests.Integration.Phase44;

/// <summary>
/// Phase 44 Plan 44-00 Task 2 — Wave 0 sanity Facts pinning the
/// hand-curated <c>strict-error-manifest.csv</c> against its schema, partition
/// counts, and carve-out cardinality.
///
/// <para>
/// These Facts are the regression-pin for the Wave 0 deliverable: every
/// downstream Theory in Plans 44-05..44-08 reads from
/// <see cref="StrictErrorManifestLoader"/>, so if the CSV's shape drifts these
/// Facts catch it BEFORE the downstream Theory rows go red en masse.
/// </para>
///
/// <para>
/// Tolerance band for the in-scope row count is intentionally wide per W8 NOTE
/// in 44-00-PLAN.md — Plan 44-00 owns the upper-bound regression-pin; per-module
/// exactness is owned downstream by Plans 44-06 + 44-07 via their per-module
/// <c>[InlineData("&lt;module&gt;.cs", &lt;count&gt;)]</c> rows.
/// </para>
///
/// <para>
/// Collection isolation + RenderingDiagnostics.ResetForTesting mirrors Phase
/// 42 + 43 integration-layout convention. The Wave 0 sanity Facts don't
/// touch the engine — they read the CSV file and assert structural invariants
/// — but the disposable + reset are kept for consistency with downstream
/// Phase 44 fixtures that DO exercise the engine.
/// </para>
/// </summary>
[Trait("Category", Phase44TestCategory.Phase44)]
[Collection("FlowScripts")]
public class StrictErrorManifestSanityTests : IDisposable
{
    /// <summary>
    /// Tolerance band for the in-scope (carve_out=false) row count. Bounds
    /// per W8 NOTE in 44-00-PLAN.md; per-module exactness lives in Plans
    /// 44-06 + 44-07.
    ///
    /// Current curated count (2026-05-24): 113 in-scope rows. The wide band
    /// [100, 140] accommodates forward drift from new stdlib WarnOnce sites
    /// added between now and Plan 44-08 closeout, while still catching
    /// catastrophic regressions (e.g. someone deletes 30 rows by accident).
    /// </summary>
    private const int InScopeLowerBound = 100;
    private const int InScopeUpperBound = 140;

    public StrictErrorManifestSanityTests()
    {
        // Per Phase 42 + 43 pattern: reset one-shot dedup state so any
        // downstream test that DOES exercise advisories starts clean.
        RenderingDiagnostics.ResetForTesting();
    }

    public void Dispose()
    {
        // Symmetric reset on disposal — keeps subsequent collections clean.
        RenderingDiagnostics.ResetForTesting();
    }

    [Fact]
    public void Fact_ManifestFileExists()
    {
        string repoRoot = FindRepoRoot();
        string path = Path.Combine(repoRoot, StrictErrorManifestLoader.ManifestRelPath);
        Assert.True(File.Exists(path),
            $"strict-error-manifest.csv missing at {path}. " +
            "Plan 44-00 Task 2 produces this file; the loader and all downstream " +
            "Plans 44-05..44-08 Theory rows fail without it.");
    }

    [Fact]
    public void Fact_ManifestHeaderMatchesSchema()
    {
        string repoRoot = FindRepoRoot();
        string path = Path.Combine(repoRoot, StrictErrorManifestLoader.ManifestRelPath);
        string firstLine = File.ReadLines(path).First();
        Assert.Equal(StrictErrorManifestLoader.ExpectedHeader, firstLine);
    }

    [Fact]
    public void Fact_InScopeRowCount_BetweenLowerAndUpperBound()
    {
        int count = StrictErrorManifestLoader.LoadInScopeSites().Count();
        Assert.True(
            count >= InScopeLowerBound && count <= InScopeUpperBound,
            $"strict-error-manifest.csv in-scope row count {count} outside " +
            $"tolerance [{InScopeLowerBound}, {InScopeUpperBound}]. " +
            "W8 NOTE: tolerance is intentionally wide for Wave 1; Plans 44-06 + " +
            "44-07 tighten via per-module Theory cardinality. A catastrophic " +
            "drift suggests either (a) a large stdlib WarnOnce purge or (b) " +
            "the CSV got corrupted.");
    }

    [Fact]
    public void Fact_CarveOutCount_ExactlyFive()
    {
        int count = StrictErrorManifestLoader.LoadCarveOutSites().Count();
        Assert.Equal(5, count);
    }

    [Fact]
    public void Fact_Axis6aClampCount_ExactlyThirteen()
    {
        // §6a clamps: HIGH priority + axis=B + has a non-empty Param column +
        // is in TransformFunctions.cs. The Param column is what distinguishes
        // them from the ~46 §6b HIGH advisory rows.
        var sixaRows = StrictErrorManifestLoader.LoadAll()
            .Where(r => !r.CarveOut
                     && r.Priority == "HIGH"
                     && r.Axis == "B"
                     && !string.IsNullOrEmpty(r.Param)
                     && r.FilePath.EndsWith("TransformFunctions.cs",
                            StringComparison.Ordinal))
            .ToList();
        Assert.Equal(13, sixaRows.Count);

        // Pin the 13 line numbers per RESEARCH §"Site Inventory" Table §6a.
        var expectedLines = new[] { 106, 107, 649, 650, 657, 658, 666, 667, 785, 821, 904, 960, 1106 };
        var actualLines = sixaRows.Select(r => r.Line).OrderBy(x => x).ToArray();
        Assert.Equal(expectedLines, actualLines);
    }

    [Fact]
    public void Fact_NoInScopeSentinelLacksStrictPrefix()
    {
        // D-07: every in-scope sentinel_body MUST start with "[strict] " so the
        // verbatim error string is composer-visible as a strict-mode error.
        // Carve-out rows DO NOT carry the prefix (they stay charitable advisories).
        var offenders = StrictErrorManifestLoader.LoadAll()
            .Where(r => !r.CarveOut && !r.SentinelBody.StartsWith("[strict] ",
                StringComparison.Ordinal))
            .Select(r => $"{r.FilePath}:{r.Line}: {r.SentinelBody}")
            .ToList();
        Assert.True(offenders.Count == 0,
            "in-scope rows missing [strict] prefix (D-07 violation):\n" +
            string.Join("\n", offenders));
    }

    [Fact]
    public void Fact_CarveOutSites_PinnedAtExactFileLine()
    {
        // Anti-Pitfall-2 regression pin (D-06): the 5 carve-out sites MUST be
        // exactly the locked locations. Future stdlib edits that shift these
        // line numbers MUST update the CSV to keep the carve-out flag attached
        // to the correct WarnOnce call.
        var carveOuts = StrictErrorManifestLoader.LoadAll()
            .Where(r => r.CarveOut)
            .Select(r => $"{r.FilePath}:{r.Line}")
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        var expected = new[]
        {
            "flow-lang/Interpreter/Interpreter.cs:476",
            "flow-lang/StandardLibrary/Improv/StyleRegistry.cs:156",
            "flow-lang/StandardLibrary/Improv/StyleRegistry.cs:244",
            "flow-lang/StandardLibrary/Improv/StyleRegistry.cs:258",
            "flow-lang/StandardLibrary/Improv/StyleRegistry.cs:265",
        };

        Assert.Equal(expected, carveOuts);
    }

    [Fact]
    public void Fact_LoaderPartitionsCleanly_InScopePlusCarveOut_EqualsAll()
    {
        // Sanity: partition completeness. Every row is either in-scope or
        // carve-out — there is no third bucket.
        int all = StrictErrorManifestLoader.LoadAll().Count;
        int inScope = StrictErrorManifestLoader.LoadInScopeSites().Count();
        int carve = StrictErrorManifestLoader.LoadCarveOutSites().Count();
        Assert.Equal(all, inScope + carve);
    }

    [Fact]
    public void Fact_HighPlusMedLow_PartitionsInScope()
    {
        // Plans 44-06 + 44-07 split in-scope by priority. The HIGH +
        // MED-LOW partitions MUST sum to in-scope cardinality.
        int inScope = StrictErrorManifestLoader.LoadInScopeSites().Count();
        int high = StrictErrorManifestLoader.LoadHighPrioritySites().Count();
        int medLow = StrictErrorManifestLoader.LoadMedLowPrioritySites().Count();
        Assert.Equal(inScope, high + medLow);
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
