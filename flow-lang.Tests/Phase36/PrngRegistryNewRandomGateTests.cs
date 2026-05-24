using System;
using System.IO;
using System.Linq;
using Xunit;

namespace FlowLang.Tests.Phase36;

/// <summary>
/// Phase 36 Plan 36-01 Task 2 — source-grep CI gate.
///
/// Scans <c>flow-lang/StandardLibrary/Patterns/</c>,
/// <c>flow-lang/StandardLibrary/Generative/</c>, and
/// <c>flow-lang/StandardLibrary/Improv/</c> for occurrences of
/// <c>new Random(</c> (outside <c>//</c> line comments). Asserts zero hits
/// EXCEPT on lines bearing the trailing comment marker
/// <c>// PRNG-SANCTIONED:</c> — which Plan 36-06+ uses to flag the
/// explicit-seed overloads of Markov / L-system / cellular / chaos
/// builtins, where <c>new Random(seed)</c> IS the contract.
///
/// Per D-v1.5-06 / D-36-09: every UNSEEDED PRNG-driven primitive MUST
/// route through <see cref="FlowLang.Runtime.PrngRegistry"/> rather
/// than constructing a wall-clock <c>Random</c> directly. The sanctioned
/// marker preserves the spirit of the gate (no wall-clock Randoms) while
/// permitting documented seed-driven exceptions. Today (Plan 36-01) the
/// three target directories did not yet exist — the fact passed vacuously
/// then; it BECOMES the gate the moment Plans 36-05+ create those
/// directories. Mirrors Phase 29 LicenseAuditTests' source-grep pattern.
/// </summary>
public class PrngRegistryNewRandomGateTests
{
    [Theory]
    [InlineData("Patterns")]
    [InlineData("Generative")]
    [InlineData("Improv")]
    public void NoNewRandomUnderGenerativeDirectories(string subDir)
    {
        string repoRoot = FindRepoRoot();
        string targetDir = Path.Combine(repoRoot, "flow-lang", "StandardLibrary", subDir);

        if (!Directory.Exists(targetDir))
        {
            // Directory does not exist yet — gate is vacuously satisfied.
            // Phase 36 Plans 36-05/06/07/08/09/11 create these dirs; this
            // Theory row activates then.
            return;
        }

        int hits = 0;
        var offenders = new System.Collections.Generic.List<string>();
        foreach (var file in Directory.GetFiles(targetDir, "*.cs", SearchOption.AllDirectories))
        {
            string[] lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                // Skip line-comment lines (leading whitespace + "//").
                if (line.TrimStart().StartsWith("//", StringComparison.Ordinal))
                    continue;
                if (line.Contains("new Random(", StringComparison.Ordinal))
                {
                    // Plan 36-06: lines bearing the trailing
                    // `// PRNG-SANCTIONED:` marker are documented exceptions —
                    // the explicit-seed overloads of generative builtins MUST
                    // construct `new Random(seed)` directly per their contract.
                    if (line.Contains("PRNG-SANCTIONED:", StringComparison.Ordinal))
                        continue;
                    hits++;
                    offenders.Add($"{file}:{i + 1}: {line.Trim()}");
                }
            }
        }

        Assert.True(hits == 0,
            $"Found {hits} `new Random(` occurrence(s) in {subDir}/ — Phase 36 PRNG-driven " +
            "primitives MUST route through PrngRegistry (D-v1.5-06 / D-36-09):\n  " +
            string.Join("\n  ", offenders));
    }

    /// <summary>
    /// Walks up from the test assembly location until a directory containing
    /// <c>flow-sharp.sln</c> is found. Mirrors the test-helper convention
    /// used by Phase 29 LicenseAuditTests.
    /// </summary>
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "flow-sharp.sln")))
            dir = dir.Parent;
        if (dir == null)
            throw new InvalidOperationException("Could not locate flow-sharp.sln walking up from " + AppContext.BaseDirectory);
        return dir.FullName;
    }
}
