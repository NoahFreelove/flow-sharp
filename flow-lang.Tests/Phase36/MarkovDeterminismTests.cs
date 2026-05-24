using System;
using System.IO;
using System.Linq;
using Xunit;

namespace FlowLang.Tests.Phase36;

/// <summary>
/// Phase 36 Plan 36-06 Task 2 — source-grep gate for the Markov surface.
///
/// Mirrors <c>PatternDeterminismTests.NoNewRandomInPatternFunctions</c>: the
/// stochastic generative primitives MUST route their PRNG through
/// <c>ExecutionContext.PrngRegistry</c> per D-v1.5-06 / D-36-09. Only the
/// EXPLICIT-SEED overloads (markovGenerate seeded + markov seeded one-shot)
/// are permitted to use <c>new Random(seed)</c> directly. The gate caps the
/// hit count at 2 — one for each seeded entry point.
///
/// <para>
/// The cross-engine determinism + structural-equality + one-shot/split
/// equivalence assertions live in <c>MarkovModelTests</c> behavior facts;
/// this file is the CI-style structural enforcement layer.
/// </para>
/// </summary>
public class MarkovDeterminismTests
{
    [Fact]
    public void NoNewRandomInMarkovFunctions()
    {
        // Phase 36 generative primitives ONLY use `new Random(` inside the
        // explicit-seed overloads (markovGenerate seeded + markov seeded
        // one-shot). The gate caps hits at 2.
        const int permittedNewRandomHits = 2;

        string repoRoot = FindRepoRoot();
        string targetFile = Path.Combine(repoRoot,
            "flow-lang", "StandardLibrary", "Generative", "MarkovFunctions.cs");
        Assert.True(File.Exists(targetFile),
            $"MarkovFunctions.cs not found at {targetFile}");

        string[] lines = File.ReadAllLines(targetFile);
        int hits = 0;
        var offenders = new System.Collections.Generic.List<string>();
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.TrimStart().StartsWith("//", StringComparison.Ordinal)) continue;
            if (line.Contains("new Random(", StringComparison.Ordinal))
            {
                hits++;
                offenders.Add($"line {i + 1}: {line.Trim()}");
            }
        }

        Assert.True(hits <= permittedNewRandomHits,
            $"Found {hits} `new Random(` occurrence(s) in MarkovFunctions.cs "
            + $"(expected at most {permittedNewRandomHits} — one per explicit-seed overload). "
            + "Phase 36 unseeded paths MUST route through PrngRegistry (D-v1.5-06 / D-36-09):\n  "
            + string.Join("\n  ", offenders));
    }

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
