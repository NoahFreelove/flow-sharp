using System;
using System.IO;
using System.Linq;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;
// Disambiguate FlowLang.Runtime.ExecutionContext from System.Threading.ExecutionContext —
// the bare name is ambiguous under net10.0's implicit usings.
using ExecutionContext = FlowLang.Runtime.ExecutionContext;

namespace FlowLang.Tests.Phase36;

/// <summary>
/// Phase 36 Plan 36-11 Task 2 — determinism source-grep gate + charitable
/// behavior facts for <c>jam</c>.
///
/// <para>
/// Mirrors <see cref="MarkovDeterminismTests"/> +
/// <see cref="PatternDeterminismTests"/>: the only sanctioned
/// <c>new Random(</c> usage in <c>JamFunctions.cs</c> is the explicit-seed
/// path. The source-grep gate caps the hit count at 1.
/// </para>
/// </summary>
[Collection("FlowScripts")]
public class JamDeterminismTests
{
    [Fact]
    public void NoUnseededNewRandomInJamFunctions()
    {
        // Phase 36 stochastic primitives MUST route their PRNG through
        // ExecutionContext.PrngRegistry per D-v1.5-06 / D-36-09. Only the
        // explicit-seed overload in jam is permitted to call new Random(seed)
        // directly. The gate caps the non-comment count at 1.
        var sourcePath = LocateJamFunctionsSource();
        Assert.True(File.Exists(sourcePath), $"Source not located at: {sourcePath}");

        var lines = File.ReadAllLines(sourcePath);
        int hits = 0;
        foreach (var rawLine in lines)
        {
            // Strip comments — single-line // and inline trailing //. Multi-line
            // /* */ comments are not used in JamFunctions.cs (matches the
            // PatternDeterminismTests precedent).
            var line = rawLine;
            int idx = line.IndexOf("//", StringComparison.Ordinal);
            if (idx >= 0) line = line[..idx];
            if (line.Contains("new Random("))
                hits++;
        }
        Assert.True(hits <= 1,
            $"JamFunctions.cs must contain at most one `new Random(` (the explicit-seed "
            + $"overload); found {hits}. Route additional PRNG paths through "
            + "ExecutionContext.PrngRegistry per D-36-09.");
    }

    [Fact]
    public void StyleKeyIncompatibilityIsCharitable()
    {
        // D-36-08: when the user picks a style+key combination that's musically
        // incompatible (e.g., a chord progression mostly outside the active
        // key), jam emits a one-shot stderr advisory + STILL produces a
        // non-empty Sequence. NOT a hard error.
        //
        // Reset the WarnOnce dedup state — other tests in this collection may
        // have already tripped the jam:style-key-mismatch sentinel.
        RenderingDiagnostics.ResetForTesting();
        using var runner = new FlowEngineRunner();
        // Out-of-key chord progression: most chord roots are sharps — chord
        // tones land outside Cmajor's pitch classes. Top-level declarations so
        // the test can probe `result` from GlobalFrame; jam reads the active
        // key from the explicit "Cmajor" arg, not from any context block.
        var (success, _, stderr, _) = runner.RunSource("""
            use "@std"
            use "@improv"
            Sequence over = | Csmaj7 | Asm7 | Fsmaj7 | Bfmaj7 |
            Sequence result = (jam over #blues 4 "Cmajor" 42 2)
            """);
        Assert.True(success, $"Script failed; stderr:\n{stderr}");

        // Result is non-empty.
        var seq = runner.GetVariable("result").As<SequenceData>();
        Assert.Equal(4, seq.Bars.Count);

        // Advisory fired on stderr (sentinel: jam:style-key-mismatch:...).
        Assert.Contains("may produce unexpected harmonic flavor", stderr);
    }

    /// <summary>
    /// Locate <c>JamFunctions.cs</c> by walking up from the test assembly's
    /// directory toward the repo root. Tests run from
    /// <c>flow-lang.Tests/bin/Debug/net10.0/</c> — three parents up to
    /// the test project, one more up to the repo root where
    /// <c>flow-lang/StandardLibrary/Improv/JamFunctions.cs</c> lives.
    /// </summary>
    private static string LocateJamFunctionsSource()
    {
        // Phase 36 Plan 36-05's PatternDeterminismTests use the same strategy.
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8; i++)
        {
            var candidate = Path.Combine(dir,
                "flow-lang", "StandardLibrary", "Improv", "JamFunctions.cs");
            if (File.Exists(candidate)) return candidate;
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
        // Last-ditch fallback — return the relative path; assertion below fires.
        return "flow-lang/StandardLibrary/Improv/JamFunctions.cs";
    }
}
