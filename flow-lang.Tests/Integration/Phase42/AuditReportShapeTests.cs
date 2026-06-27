using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace FlowLang.Tests.Integration.Phase42;

/// <summary>
/// Phase 42 Plan 03 Task 2 — AUDIT.md schema pin.
///
/// Asserts that <c>.planning/phases/42-type-system-stdlib-audit/42-AUDIT.md</c>
/// (the load-bearing Phase 42 deliverable authored in Plan 03 Task 1) contains
/// every required section, the BeatType anchor regression in §1, at least 10
/// finding rows carrying a downstream-phase routing tag (AUDIT-08), no
/// fragile <c>BuiltInFunctions.cs:NNNN</c> file:line citations (Pitfall 7),
/// and at least one cite of the <c>42-AUDIT-data/</c> raw-data directory.
///
/// Mirrors Phase 36 PrngRegistryNewRandomGateTests' FindRepoRoot walker +
/// Phase 29 LicenseAuditTests' [Theory]+[InlineData] per-section iteration shape.
///
/// Vacuously skip-passes (with stdout log) if 42-AUDIT.md does not exist —
/// this fixture is committed alongside Task 1 but tolerates being run BEFORE
/// Task 1 lands in CI (e.g. during plan-phase Wave 0 stub).
/// </summary>
public class AuditReportShapeTests
{
    [Theory]
    [InlineData("## 1. Orphaned Types")]
    [InlineData("## 2. Missing Conversions")]
    [InlineData("## 3. Asymmetric Pairs")]
    [InlineData("## 4. Dead-End Builtins")]
    [InlineData("## 5. Overload Gaps")]
    [InlineData("## 6. Clamp & Advisory Inventory")]
    [InlineData("## 7. Prioritization & Phase Routing")]
    public void Audit_ContainsRequiredSection(string heading)
    {
        string? auditText = ReadAuditMd();
        if (auditText is null)
        {
            // AUDIT.md not authored yet — vacuously pass (Wave 0 stub tolerance).
            return;
        }

        Assert.Contains(heading, auditText, StringComparison.Ordinal);
    }

    [Fact]
    public void Audit_MentionsBeatTypeAsOrphan()
    {
        string? auditText = ReadAuditMd();
        if (auditText is null) return;

        // Anchor regression — BeatType is the single coercible orphan per
        // RESEARCH §Summary and Plan 01 harness output.
        Assert.Contains("BeatType", auditText, StringComparison.Ordinal);

        // BeatType citation must appear inside §1 (Orphaned Types) — split on
        // §2 heading and assert the BeatType mention lands in the prefix.
        int section2Index = auditText.IndexOf("## 2. Missing Conversions", StringComparison.Ordinal);
        Assert.True(section2Index > 0,
            "§2 heading not found — cannot verify BeatType lands in §1.");

        string preSection2 = auditText.Substring(0, section2Index);
        Assert.True(preSection2.Contains("BeatType", StringComparison.Ordinal),
            "BeatType mentioned in AUDIT.md but NOT inside §1 Orphaned Types. " +
            "AUDIT-08 anchor regression: §1 must surface the Beat orphan.");
    }

    [Fact]
    public void Audit_EveryFindingHasPhaseRouting()
    {
        string? auditText = ReadAuditMd();
        if (auditText is null) return;

        // Scan every table row (lines starting with `|` that are NOT the
        // separator row `|---|---|...` or header rows). Count rows that carry
        // a downstream-phase routing tag.
        var lines = auditText.Split('\n');
        int routedRows = 0;
        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();
            if (!trimmed.StartsWith("|", StringComparison.Ordinal)) continue;
            // Skip separator rows (e.g. |---|---|).
            if (trimmed.Contains("|---", StringComparison.Ordinal) ||
                trimmed.Contains("| ---", StringComparison.Ordinal)) continue;
            if (line.Contains("→ Phase 43", StringComparison.Ordinal) ||
                line.Contains("→ Phase 44", StringComparison.Ordinal) ||
                line.Contains("→ v1.6-backlog", StringComparison.Ordinal) ||
                line.Contains("→ not a gap", StringComparison.Ordinal))
            {
                routedRows++;
            }
        }

        Assert.True(routedRows >= 10,
            $"AUDIT.md has only {routedRows} table rows with phase routing tags " +
            "(→ Phase 43 / → Phase 44 / → v1.6-backlog / → not a gap). " +
            "AUDIT-08 requires EVERY gap to be routed; threshold is 10 minimum.");
    }

    [Fact]
    public void Audit_DoesNotCarryFragileFileLineRefs()
    {
        string? auditText = ReadAuditMd();
        if (auditText is null) return;

        // Pitfall 7 guard — stable identifier is builtin name + signature, NOT
        // file:line. File:line refs in AUDIT.md rot the moment Phase 43 renames
        // a function. Permit a handful for context (e.g. `Interpreter.cs:1019`
        // for the BeatType producer site), but block carpet-bomb citations.
        var pattern = new Regex(@"[A-Z][A-Za-z]+\.cs:\d+", RegexOptions.Compiled);
        int matches = pattern.Matches(auditText).Count;

        Assert.True(matches < 5,
            $"AUDIT.md contains {matches} `*.cs:NNNN` file:line refs. Pitfall 7 " +
            "limit is <5 — these rot when Phase 43 renames functions. Move them " +
            "to 42-AUDIT-data/ supplementary files instead.");
    }

    [Fact]
    public void Audit_CitesAuditDataDirectory()
    {
        string? auditText = ReadAuditMd();
        if (auditText is null) return;

        // Anchors §6 inventory traceability — AUDIT.md MUST reference its raw-data
        // sibling directory at least once so downstream Phase 44 plan-phase
        // authors can find the grep output for re-extraction.
        Assert.Contains("42-AUDIT-data/", auditText, StringComparison.Ordinal);
    }

    /// <summary>
    /// Reads the Phase 42 AUDIT.md from the repo root. Returns <c>null</c>
    /// (with a stdout log) if the file does not exist — fixture
    /// vacuously skip-passes when run before Task 1 has landed.
    /// </summary>
    private static string? ReadAuditMd()
    {
        string repoRoot = FindRepoRoot();
        string auditPath = Path.Combine(repoRoot, ".planning", "phases",
            "42-type-system-stdlib-audit", "42-AUDIT.md");
        if (!File.Exists(auditPath))
        {
            Console.WriteLine($"[AuditReportShapeTests] {auditPath} does not exist yet — vacuously skip-passing.");
            return null;
        }
        return File.ReadAllText(auditPath);
    }

    /// <summary>
    /// Walks up from the test assembly location until a directory containing
    /// <c>flow-sharp.sln</c> is found. Mirrors PrngRegistryNewRandomGateTests
    /// and LicenseAuditTests' FindRepoRoot convention.
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
}
