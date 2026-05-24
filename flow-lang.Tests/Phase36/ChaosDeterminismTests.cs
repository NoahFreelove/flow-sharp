using System;
using System.Collections.Generic;
using System.IO;
using FlowLang.Runtime;
using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Phase36;

/// <summary>
/// Phase 36 Plan 36-09 — determinism + structural gates for the chaos-map
/// primitives.
///
/// <para>
/// Both <c>lorenz</c> and <c>logistic</c> derive their single Random from the
/// composer's seed (REQ signature REQUIRES the seed arg) — same seed → same
/// trajectory. The integration loops themselves use NO PRNG after the
/// initial-condition perturbation, so determinism is end-to-end given the
/// seed.
/// </para>
///
/// <para>
/// Source-grep gate: at most 2 <c>new Random(</c> sites in
/// <c>ChaosFunctions.cs</c> — one per primitive (lorenz + logistic), each
/// bearing the <c>// PRNG-SANCTIONED:</c> marker per the Plan 36-06
/// convention.
/// </para>
/// </summary>
public class ChaosDeterminismTests
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
    public void LorenzSameSeedSameOutput()
    {
        // Two identical calls produce byte-identical doubles (bitwise compare).
        var a = RunArrayScript("""
            Double[] result = (lorenz 10.0 28.0 2.6667 200 42)
            """);
        var b = RunArrayScript("""
            Double[] result = (lorenz 10.0 28.0 2.6667 200 42)
            """);
        Assert.Equal(a.Count, b.Count);
        for (int i = 0; i < a.Count; i++)
        {
            double da = (double)a[i].Data!;
            double db = (double)b[i].Data!;
            // Exact byte-level equality via BitConverter — same-platform IEEE
            // 754 reproducibility (D-36-09 same-platform contract).
            Assert.Equal(BitConverter.DoubleToInt64Bits(da),
                         BitConverter.DoubleToInt64Bits(db));
        }
    }

    [Fact]
    public void LogisticSameSeedSameOutput()
    {
        var a = RunArrayScript("""
            Double[] result = (logistic 3.9 100 42)
            """);
        var b = RunArrayScript("""
            Double[] result = (logistic 3.9 100 42)
            """);
        Assert.Equal(a.Count, b.Count);
        for (int i = 0; i < a.Count; i++)
        {
            double da = (double)a[i].Data!;
            double db = (double)b[i].Data!;
            Assert.Equal(BitConverter.DoubleToInt64Bits(da),
                         BitConverter.DoubleToInt64Bits(db));
        }
    }

    [Fact]
    public void LorenzDifferentSeedsDifferentOutput()
    {
        // Sanity check: different seeds produce distinct trajectories.
        // Compare the LAST element (chaotic divergence is amplified late).
        var a = RunArrayScript("""
            Double[] result = (lorenz 10.0 28.0 2.6667 200 42)
            """);
        var b = RunArrayScript("""
            Double[] result = (lorenz 10.0 28.0 2.6667 200 99)
            """);
        double finalA = (double)a[a.Count - 1].Data!;
        double finalB = (double)b[b.Count - 1].Data!;
        Assert.NotEqual(finalA, finalB);
    }

    [Fact]
    public void ChaosFunctionsRandomGate()
    {
        // Source-grep gate: at most 2 `new Random(` sites in
        // ChaosFunctions.cs (one per primitive). Both must bear the
        // // PRNG-SANCTIONED: marker per the Plan 36-06 convention.
        const int permittedNewRandomHits = 2;

        string repoRoot = FindRepoRoot();
        string targetFile = Path.Combine(repoRoot,
            "flow-lang", "StandardLibrary", "Generative", "ChaosFunctions.cs");
        Assert.True(File.Exists(targetFile),
            $"ChaosFunctions.cs not found at {targetFile}");

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
            $"Found {hits} `new Random(` occurrence(s) in ChaosFunctions.cs "
            + $"(expected at most {permittedNewRandomHits} — one per primitive). "
            + "Phase 36 chaos primitives derive their PRNG from the REQ-mandated "
            + "seed arg only; if you add a stochastic overload, mark the new "
            + "site with // PRNG-SANCTIONED: and raise the cap:\n  "
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
