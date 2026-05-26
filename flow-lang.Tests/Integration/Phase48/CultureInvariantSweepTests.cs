using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace FlowLang.Tests.Integration.Phase48;

/// <summary>
/// Phase 48 Plan 48-02 — Source-grep gate asserting zero unqualified
/// culture-sensitive string operations remain in <c>flow-lang/</c> production
/// source. Enforces the discipline established by Plan 48-02 Task 1.
///
/// Under <c>&lt;InvariantGlobalization&gt;true&lt;/InvariantGlobalization&gt;</c>
/// (D-48-03, ~10 MB ICU bundle savings), <c>.ToUpper()</c> / <c>.ToLower()</c>
/// calls without an explicit <c>CultureInfo</c> arg risk the Turkish-I problem
/// class — non-ASCII characters round-trip unpredictably. The fix is to use
/// <c>.ToUpperInvariant()</c> / <c>.ToLowerInvariant()</c> at every call site.
///
/// Both Facts are plain <c>[Fact]</c> (NOT FlowTargetFact-gated) — they read
/// source files directly via <c>File.ReadAllLines</c> regardless of build
/// target. They run from the Desktop test runner.
///
/// Scope:
/// - INCLUDED: every <c>.cs</c> file under <c>flow-lang/</c>
/// - EXCLUDED: <c>bin/</c>, <c>obj/</c> (build output), any <c>*.Tests/</c>
///   directory (tests may use locale-aware string handling intentionally to
///   test that very behavior).
///
/// Defense-in-depth gate per T-48-05: a contributor could bypass via
/// <c>.ToUpper(CultureInfo.CurrentCulture)</c>, but the unqualified-empty-parens
/// pattern is the common-case footgun and code review remains the primary
/// control.
/// </summary>
public class CultureInvariantSweepTests
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

    /// <summary>
    /// Enumerate every <c>.cs</c> file under <c>flow-lang/</c> production
    /// source, excluding build output (<c>bin/</c>, <c>obj/</c>) and any
    /// directory ending in <c>.Tests</c> (test code excluded by design).
    /// </summary>
    private static IEnumerable<string> EnumerateProductionCsFiles(string repoRoot)
    {
        var productionRoot = Path.Combine(repoRoot, "flow-lang");
        if (!Directory.Exists(productionRoot))
            yield break;

        foreach (var file in Directory.EnumerateFiles(
                     productionRoot, "*.cs", SearchOption.AllDirectories))
        {
            // Path segment exclusions — split on directory separator and reject any
            // path that has a segment matching bin, obj, or ending in .Tests
            var segments = file
                .Substring(repoRoot.Length)
                .Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                       StringSplitOptions.RemoveEmptyEntries);

            bool skip = false;
            foreach (var seg in segments)
            {
                if (string.Equals(seg, "bin", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(seg, "obj", StringComparison.OrdinalIgnoreCase) ||
                    seg.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase))
                {
                    skip = true;
                    break;
                }
            }

            if (!skip)
                yield return file;
        }
    }

    /// <summary>
    /// Scan production .cs files for the given regex of culture-sensitive calls.
    /// Skips comment-only lines and lines already containing the safe variant.
    /// </summary>
    /// <param name="pattern">Regex matching the unqualified call (e.g. <c>\.ToUpper\(\)</c>)</param>
    /// <param name="safeVariantMarker">Substring identifying the safe form (e.g. <c>"ToUpperInvariant"</c>) — lines containing this marker are skipped to avoid false positives</param>
    /// <returns>Violation list: (filePath, lineNumber, lineText)</returns>
    private static List<(string filePath, int lineNumber, string lineText)> FindViolations(
        string pattern,
        string safeVariantMarker)
    {
        var repoRoot = FindRepoRoot();
        var regex = new Regex(pattern, RegexOptions.Compiled);
        var violations = new List<(string, int, string)>();

        foreach (var file in EnumerateProductionCsFiles(repoRoot))
        {
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                // Skip comment-only lines (TrimStart for // … one-liner comments)
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("//", StringComparison.Ordinal))
                    continue;
                // Skip lines that already use the safe Invariant variant — the regex
                // \.ToUpper\(\) won't match ToUpperInvariant (empty parens differ),
                // but defense-in-depth: if a line contains BOTH (e.g. an inline
                // comment mentioning the unsafe form), favor the safe-marker check.
                if (line.Contains(safeVariantMarker, StringComparison.Ordinal))
                    continue;
                if (regex.IsMatch(line))
                {
                    // Record file path relative to repo root for readable error output
                    var relPath = file.Substring(repoRoot.Length).TrimStart(
                        Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    violations.Add((relPath, i + 1, line));
                }
            }
        }

        return violations;
    }

    [Fact]
    public void NoUnqualifiedToUpper_InProductionCode()
    {
        var violations = FindViolations(@"\.ToUpper\(\)", "ToUpperInvariant");

        if (violations.Count > 0)
        {
            var msg = $"Found {violations.Count} unqualified .ToUpper() call(s) in " +
                      $"production code (Phase 48 D-48-03 invariant-globalization gate). " +
                      $"Fix: replace .ToUpper() with .ToUpperInvariant().\n" +
                      string.Join("\n",
                          violations.Select(v => $"  {v.filePath}:{v.lineNumber}  {v.lineText.Trim()}"));
            Assert.Fail(msg);
        }
    }

    [Fact]
    public void NoUnqualifiedToLower_InProductionCode()
    {
        var violations = FindViolations(@"\.ToLower\(\)", "ToLowerInvariant");

        if (violations.Count > 0)
        {
            var msg = $"Found {violations.Count} unqualified .ToLower() call(s) in " +
                      $"production code (Phase 48 D-48-03 invariant-globalization gate). " +
                      $"Fix: replace .ToLower() with .ToLowerInvariant().\n" +
                      string.Join("\n",
                          violations.Select(v => $"  {v.filePath}:{v.lineNumber}  {v.lineText.Trim()}"));
            Assert.Fail(msg);
        }
    }
}
