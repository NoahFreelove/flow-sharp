using System;
using System.IO;
using System.Linq;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Patterns;
using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Phase36;

/// <summary>
/// Phase 36 Plan 36-05 Task 2 — stochastic-combinator determinism gates.
///
/// Three stochastic combinators (sometimes / degrade / sparseSeq) plus the
/// default-prob sometimes overload route their PRNG through
/// <see cref="ExecutionContext.PrngRegistry"/>. Per D-v1.5-06 / D-36-09:
/// <list type="bullet">
///   <item>Two invocations at the same call site within a single render pass
///         share PRNG state, so the second draw is the next sample after
///         the first.</item>
///   <item>Reset-at-render-boundary clears PRNG state so the FIRST draw
///         after reset matches the FIRST draw of the prior pass — two-run
///         cmp-clean determinism contract.</item>
/// </list>
///
/// The source-grep gate <c>NoNewRandomInPatternFunctions</c> ensures
/// <c>PatternFunctions.cs</c> does not construct <c>new Random()</c>
/// instances directly (would bypass the registry's seed-derivation +
/// snapshot semantics).
/// </summary>
[Collection("FlowScripts")]
public class PatternDeterminismTests
{
    public PatternDeterminismTests()
    {
        RenderingDiagnostics.ResetForTesting();
        PatternFunctions.ResetChunkRotationForTesting();
    }

    private static SequenceData EvalSequence(FlowEngineRunner runner, string body, string varName = "result")
    {
        var source = "use \"@std\"\nuse \"@patterns\"\n" + body;
        var (success, _, stderr, errorCount) = runner.RunSource(source);
        Assert.True(success && errorCount == 0,
            $"Script failed: errorCount={errorCount}\nstderr:\n{stderr}\nsource:\n{source}");
        return runner.GetVariable(varName).As<SequenceData>();
    }

    private static int CountNotes(SequenceData seq) =>
        seq.Bars.Sum(b => b.MusicalNotes.Count);

    // ====================================================================
    // Two-run reset determinism
    // ====================================================================

    [Fact]
    public void SometimesRoutesPrngDeterministically()
    {
        // Two separate engines (= two separate ExecutionContexts) running the
        // SAME source produce structurally identical sequences out of
        // sometimes — because each engine's PrngRegistry seeds from the same
        // (SourceLocation, "sometimes") key + the same FNV-1a derivation.
        var src1 = RenderOnce();
        var src2 = RenderOnce();
        Assert.Equal(src1.Bars.Count, src2.Bars.Count);
        for (int i = 0; i < src1.Bars.Count; i++)
        {
            Assert.Equal(src1.Bars[i].MusicalNotes.Count, src2.Bars[i].MusicalNotes.Count);
            for (int j = 0; j < src1.Bars[i].MusicalNotes.Count; j++)
            {
                Assert.Equal(src1.Bars[i].MusicalNotes[j].NoteName,
                             src2.Bars[i].MusicalNotes[j].NoteName);
            }
        }

        static SequenceData RenderOnce()
        {
            using var runner = new FlowEngineRunner();
            return EvalSequence(runner, """
                Sequence src = | C4q D4q | E4q F4q | G4q A4q | B4q C5q |
                Sequence result = (sometimes 0.5 (fn Sequence s => (rev s)) src)
                """);
        }
    }

    [Fact]
    public void DegradeRoutesPrngDeterministically()
    {
        // Two-run cmp-clean: same source position → same draws → same drops.
        int countA, countB;
        {
            using var runner = new FlowEngineRunner();
            countA = CountNotes(EvalSequence(runner, """
                Sequence src = | C4q D4q | E4q F4q | G4q A4q | B4q C5q |
                Sequence result = (degrade src)
                """));
        }
        {
            using var runner = new FlowEngineRunner();
            countB = CountNotes(EvalSequence(runner, """
                Sequence src = | C4q D4q | E4q F4q | G4q A4q | B4q C5q |
                Sequence result = (degrade src)
                """));
        }
        Assert.Equal(countA, countB);
    }

    [Fact]
    public void SparseSeqRoutesPrngDeterministically()
    {
        int countA, countB;
        {
            using var runner = new FlowEngineRunner();
            countA = CountNotes(EvalSequence(runner, """
                Sequence src = | C4q D4q | E4q F4q | G4q A4q | B4q C5q |
                Sequence result = (sparseSeq 0.3 src)
                """));
        }
        {
            using var runner = new FlowEngineRunner();
            countB = CountNotes(EvalSequence(runner, """
                Sequence src = | C4q D4q | E4q F4q | G4q A4q | B4q C5q |
                Sequence result = (sparseSeq 0.3 src)
                """));
        }
        Assert.Equal(countA, countB);
    }

    [Fact]
    public void RenderBoundaryResetRestartsPrng()
    {
        // First sparseSeq inside a render produces some output; second
        // sparseSeq (same call site, second render = post-reset) produces
        // the same output because PrngRegistry.ResetAtRenderBoundary fires
        // before each writeWav / renderSong pass.
        using var engine = new FlowEngine(verbose: false);
        var loc = new SourceLocation(42, 10, "renders.flow");

        engine.Context.PrngRegistry.ResetAtRenderBoundary();
        var rngA = engine.Context.PrngRegistry.GetRandom(loc, "sparseSeq");
        int firstA = rngA.Next();

        engine.Context.PrngRegistry.ResetAtRenderBoundary();
        var rngB = engine.Context.PrngRegistry.GetRandom(loc, "sparseSeq");
        int firstB = rngB.Next();

        Assert.Equal(firstA, firstB);
    }

    [Fact]
    public void ChunkRotationResetsAtRenderBoundary()
    {
        // sweep-0614: the chunk rotation counter used to live in a process-
        // static field that was NEVER cleared at a render boundary, so a
        // second render of the same source rotated chunk to a different bar →
        // byte-different audio (broke two-run cmp-clean). It now lives on the
        // per-context PrngRegistry and is cleared by ResetAtRenderBoundary,
        // so the FIRST rotation after each boundary is identical.
        using var engine = new FlowEngine(verbose: false);
        var loc = new SourceLocation(7, 3, "chunk-renders.flow");

        engine.Context.PrngRegistry.ResetAtRenderBoundary();
        int firstPassA = engine.Context.PrngRegistry.NextChunkRotation(loc); // 0
        int firstPassB = engine.Context.PrngRegistry.NextChunkRotation(loc); // 1 — advances within a pass

        engine.Context.PrngRegistry.ResetAtRenderBoundary();
        int secondPassA = engine.Context.PrngRegistry.NextChunkRotation(loc); // 0 again

        Assert.Equal(0, firstPassA);
        Assert.Equal(1, firstPassB);              // advances within one render pass
        Assert.Equal(firstPassA, secondPassA);    // boundary reset restarts at 0
    }

    [Fact]
    public void ChunkProducesByteIdenticalAudioAcrossTwoRendersInOneProcess()
    {
        // End-to-end two-run cmp-clean: building + rendering the SAME chunk-
        // using source twice in ONE process must produce byte-identical WAV.
        // Before the fix the static rotation counter advanced between renders,
        // transposing a different bar the second time (cmp differ at byte 49).
        const string body = """
            use "@std"
            use "@patterns"
            use "@audio"
            Sequence s = | C4q C4q | D4q D4q | E4q E4q | F4q F4q |
            proc buildSeq ()
              Sequence r = (chunk 4 (fn Sequence x => (transpose x +12st)) s)
              r
            end proc
            """;

        byte[] RenderOnce(string outPath)
        {
            using var runner = new FlowEngineRunner();
            string script = body + "\n"
                + "section A { Sequence v = (buildSeq) }\n"
                + "Song song = [A]\n"
                + "(writeWav \"" + outPath + "\" (renderSong song \"sine\"))\n";
            var (ok, _, stderr, errCount) = runner.RunSource(script, "<chunk-twrun>");
            Assert.True(ok && errCount == 0, $"render failed: errCount={errCount}\n{stderr}");
            return File.ReadAllBytes(outPath);
        }

        string tmpA = Path.Combine(Path.GetTempPath(), $"flow_chunk_a_{Guid.NewGuid():N}.wav");
        string tmpB = Path.Combine(Path.GetTempPath(), $"flow_chunk_b_{Guid.NewGuid():N}.wav");
        try
        {
            byte[] a = RenderOnce(tmpA);
            byte[] b = RenderOnce(tmpB);
            Assert.Equal(a, b);
        }
        finally
        {
            if (File.Exists(tmpA)) File.Delete(tmpA);
            if (File.Exists(tmpB)) File.Delete(tmpB);
        }
    }

    // ====================================================================
    // Source-grep gate — Phase 36 PRNG routing enforcement
    // ====================================================================

    [Fact]
    public void NoNewRandomInPatternFunctions()
    {
        // CI gate: PatternFunctions.cs must NOT contain `new Random(`
        // outside of `//`-prefixed comment lines. Phase 36 stochastic
        // combinators route all PRNG through PrngRegistry per D-v1.5-06.
        string repoRoot = FindRepoRoot();
        string targetFile = Path.Combine(repoRoot,
            "flow-lang", "StandardLibrary", "Patterns", "PatternFunctions.cs");
        Assert.True(File.Exists(targetFile),
            $"PatternFunctions.cs not found at {targetFile}");

        string[] lines = File.ReadAllLines(targetFile);
        int hits = 0;
        var offenders = new System.Collections.Generic.List<string>();
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.TrimStart().StartsWith("//", StringComparison.Ordinal))
                continue;
            if (line.Contains("new Random(", StringComparison.Ordinal))
            {
                hits++;
                offenders.Add($"line {i + 1}: {line.Trim()}");
            }
        }

        Assert.True(hits == 0,
            $"Found {hits} `new Random(` occurrence(s) in PatternFunctions.cs — "
            + "Phase 36 stochastic combinators MUST route through "
            + "PrngRegistry (D-v1.5-06 / D-36-09):\n  "
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
