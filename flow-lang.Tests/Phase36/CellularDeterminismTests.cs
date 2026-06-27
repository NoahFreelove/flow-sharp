using System;
using System.Collections.Generic;
using System.IO;
using FlowLang.Runtime;
using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Phase36;

/// <summary>
/// Phase 36 Plan 36-08 Task 2 — determinism + structural gates for the
/// cellular automata surface.
///
/// <para>
/// 1D <c>cellular</c> is purely deterministic (no PRNG — single-1-center
/// default is fixed; the seed arg is accepted for signature uniformity but
/// ignored). 2D <c>life</c> uses ONE <c>new Random(seed)</c> for the initial
/// 30%-density fill — the composer's seed flows directly to that
/// construction (per the plan, REQ signatures REQUIRE the seed; no
/// PrngRegistry routing for the unseeded path because no unseeded path
/// exists for the public surface).
/// </para>
///
/// <para>
/// Source-grep gate: at most 1 <c>new Random(</c> (the life initial fill)
/// inside <c>CellularFunctions.cs</c>. The line bears the
/// <c>// PRNG-SANCTIONED:</c> marker per the Plan 36-06 convention to keep
/// the cross-Generative-directory gate strict while permitting documented
/// explicit-seed exceptions.
/// </para>
/// </summary>
public class CellularDeterminismTests
{
    private const string Prelude = """
        use "@std"
        use "@generative"
        """;

    private static IReadOnlyList<Value> RunArrayScript(string body, string varName = "result")
    {
        using var runner = new FlowEngineRunner();
        var (success, _, stderr, errorCount) = runner.RunSource(Prelude + "\n" + body);
        Assert.True(success && errorCount == 0,
            $"Script failed: errorCount={errorCount}\nstderr:\n{stderr}\nbody:\n{body}");
        return runner.GetVariable(varName).As<IReadOnlyList<Value>>();
    }

    [Fact]
    public void LifeSameSeedSameOutput()
    {
        // (life 8 8 4 42) twice produces the same grid sequence.
        var a = RunArrayScript("""
            Sequence[] result = (life 8 8 4 42)
            """);
        var b = RunArrayScript("""
            Sequence[] result = (life 8 8 4 42)
            """);

        Assert.Equal(a.Count, b.Count);
        for (int r = 0; r < a.Count; r++)
        {
            var seqA = a[r].As<SequenceData>();
            var seqB = b[r].As<SequenceData>();
            Assert.Equal(seqA.Bars.Count, seqB.Bars.Count);
            for (int s = 0; s < seqA.Bars.Count; s++)
            {
                var notesA = seqA.Bars[s].MusicalNotes;
                var notesB = seqB.Bars[s].MusicalNotes;
                Assert.Equal(notesA.Count, notesB.Count);
                for (int c = 0; c < notesA.Count; c++)
                    Assert.Equal(notesA[c].IsRest, notesB[c].IsRest);
            }
        }
    }

    [Fact]
    public void LifeDifferentSeedsDifferentOutput()
    {
        // Two different seeds should not coincidentally produce identical
        // grids at width=8, height=8, steps=4. Compare initial row (step 0)
        // of row 0: if every cell matches, the seeds collapsed — flag it.
        var a = RunArrayScript("""
            Sequence[] result = (life 8 8 4 42)
            """);
        var b = RunArrayScript("""
            Sequence[] result = (life 8 8 4 99)
            """);

        // Find at least one mismatched cell across the whole grid.
        bool foundDifference = false;
        for (int r = 0; r < a.Count && !foundDifference; r++)
        {
            var seqA = a[r].As<SequenceData>();
            var seqB = b[r].As<SequenceData>();
            for (int s = 0; s < seqA.Bars.Count && !foundDifference; s++)
            {
                var notesA = seqA.Bars[s].MusicalNotes;
                var notesB = seqB.Bars[s].MusicalNotes;
                for (int c = 0; c < notesA.Count && !foundDifference; c++)
                {
                    if (notesA[c].IsRest != notesB[c].IsRest)
                        foundDifference = true;
                }
            }
        }
        Assert.True(foundDifference,
            "Expected at least one cell to differ between seeds 42 and 99");
    }

    [Fact]
    public void NoUnseededRandomInCellularFunctions()
    {
        // The 2D life fill IS the only sanctioned `new Random(seed)` site —
        // gate caps the per-file hit count at 1 (the unsanctioned count is 0).
        const int permittedNewRandomHits = 1;

        string repoRoot = FindRepoRoot();
        string targetFile = Path.Combine(repoRoot,
            "flow-lang", "StandardLibrary", "Generative", "CellularFunctions.cs");
        Assert.True(File.Exists(targetFile),
            $"CellularFunctions.cs not found at {targetFile}");

        string[] lines = File.ReadAllLines(targetFile);
        int hits = 0;
        var offenders = new List<string>();
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
            $"Found {hits} `new Random(` occurrence(s) in CellularFunctions.cs "
            + $"(expected at most {permittedNewRandomHits} — the life initial-fill site). "
            + "1D cellular is purely deterministic; if you add a stochastic overload, "
            + "raise the cap and add the `// PRNG-SANCTIONED:` marker:\n  "
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
