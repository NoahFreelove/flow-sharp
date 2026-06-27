using System;
using System.IO;
using FlowLang.Runtime;
using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Phase36;

/// <summary>
/// Phase 36 Plan 36-07 Task 2 — determinism + structural gates for the L-system surface.
///
/// L-system rewriting is purely deterministic — no PRNG is involved. The
/// source-grep gate (<see cref="NoNewRandomInLsystemFunctions"/>) asserts
/// <c>LsystemFunctions.cs</c> contains zero <c>new Random(</c> occurrences,
/// because Plan 36-07 does NOT use the <c>// PRNG-SANCTIONED:</c> marker
/// convention from Plan 36-06 (which is reserved for the explicit-seed
/// overloads of stochastic primitives). The behavior gate
/// (<see cref="LsystemOneShotEquivalentToModelGenerate"/>) pins the
/// composer-facing equivalence: <c>(lsystem axiom rules N)</c> ≡
/// <c>(lsystemGenerate (lsystemModel axiom rules) N)</c>.
/// </summary>
public class LsystemDeterminismTests
{
    [Fact]
    public void NoNewRandomInLsystemFunctions()
    {
        // L-system is pure deterministic rewrite — no PRNG. Source-grep gate
        // ensures the file doesn't accidentally introduce a Random
        // construction (would violate the deterministic-by-design contract).
        const int permittedNewRandomHits = 0;

        string repoRoot = FindRepoRoot();
        string targetFile = Path.Combine(repoRoot,
            "flow-lang", "StandardLibrary", "Generative", "LsystemFunctions.cs");
        Assert.True(File.Exists(targetFile),
            $"LsystemFunctions.cs not found at {targetFile}");

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
            $"Found {hits} `new Random(` occurrence(s) in LsystemFunctions.cs "
            + $"(expected {permittedNewRandomHits} — L-system is pure deterministic rewrite). "
            + "If you need stochastic rule overloading in v1.6, add `// PRNG-SANCTIONED:` "
            + "markers per the Plan 36-06 convention:\n  "
            + string.Join("\n  ", offenders));
    }

    [Fact]
    public void LsystemOneShotEquivalentToModelGenerate()
    {
        // (lsystem axiom rules N) must produce the same expanded sequence as
        // (lsystemGenerate (lsystemModel axiom rules) N) — the split shape per
        // D-36-06 is a refactor of the one-shot shape, not a different algorithm.
        var oneShot = RunAndGetArray("""
            use "@std"
            use "@generative"
            Dict<Symbol, Symbol[]> rules = (dict #A (list #A #B) #B (list #A))
            Symbol[] result = (lsystem #A rules 4)
            """, "result");
        var split = RunAndGetArray("""
            use "@std"
            use "@generative"
            Dict<Symbol, Symbol[]> rules = (dict #A (list #A #B) #B (list #A))
            LsystemModel m = (lsystemModel #A rules)
            Symbol[] result = (lsystemGenerate m 4)
            """, "result");

        Assert.Equal(oneShot.Count, split.Count);
        for (int i = 0; i < oneShot.Count; i++)
        {
            Assert.Equal(oneShot[i].Data, split[i].Data);
        }
    }

    private static System.Collections.Generic.IReadOnlyList<Value> RunAndGetArray(
        string source, string varName)
    {
        using var runner = new FlowEngineRunner();
        var (success, _, stderr, errorCount) = runner.RunSource(source);
        Assert.True(success && errorCount == 0,
            $"Script failed: errorCount={errorCount}\nstderr:\n{stderr}\nsource:\n{source}");
        return runner.GetVariable(varName).As<System.Collections.Generic.IReadOnlyList<Value>>();
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
