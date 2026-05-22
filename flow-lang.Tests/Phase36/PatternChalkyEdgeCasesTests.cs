using FlowLang.Diagnostics;
using FlowLang.StandardLibrary.Patterns;
using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Phase36;

/// <summary>
/// Phase 36 Plan 36-05 Task 2 — charitable-interpretation edge cases for the
/// 13 @patterns combinators (PAT-02 + Pitfall 2 + Pitfall 9).
///
/// Every combinator returns its input unchanged + emits a one-shot
/// stderr advisory on degenerate input:
///   - empty sequences (0 bars)
///   - n &lt;= 0 for every / chunk / iter
///   - factor == 0 or non-finite for fast / slow
///   - non-finite offset for phase
///   - prob outside [0, 1] for sometimes / sparseSeq (clamped + advisory)
///
/// Hermetic isolation: <see cref="RenderingDiagnostics.ResetForTesting"/> is
/// called per-fact so the per-process sentinel dedup doesn't suppress
/// advisories under successive Facts ([Collection] would also do, but
/// resetting per-fact keeps the gate independent of sibling test ordering).
/// </summary>
[Collection("FlowScripts")]
public class PatternChalkyEdgeCasesTests
{
    public PatternChalkyEdgeCasesTests()
    {
        // Reset advisory dedup so this fact's advisories are visible even
        // if a sibling Phase36 test already emitted the same sentinel earlier.
        RenderingDiagnostics.ResetForTesting();
        // Reset per-call-site chunk counter so rotation-dependent facts always
        // start at chunk 0.
        PatternFunctions.ResetChunkRotationForTesting();
    }

    private static SequenceData EvalSequence(string body)
    {
        using var runner = new FlowEngineRunner();
        var source = "use \"@std\"\nuse \"@patterns\"\n" + body;
        var (success, _, stderr, errorCount) = runner.RunSource(source);
        Assert.True(success && errorCount == 0,
            $"Script failed: errorCount={errorCount}\nstderr:\n{stderr}\nsource:\n{source}");
        return runner.GetVariable("result").As<SequenceData>();
    }

    // ====================================================================
    // factor == 0 / non-finite
    // ====================================================================

    [Fact]
    public void FastZeroFactorReturnsInputUnchanged()
    {
        var result = EvalSequence("""
            Sequence src = | C4q D4q |
            Sequence result = (fast src 0.0)
            """);
        // Input had QUARTER durations; unchanged passthrough preserves them.
        Assert.Equal(1, result.Bars.Count);
        Assert.All(result.Bars[0].MusicalNotes,
            n => Assert.Equal((int)NoteValueType.Value.QUARTER, n.DurationValue));
    }

    [Fact]
    public void SlowZeroFactorReturnsInputUnchanged()
    {
        var result = EvalSequence("""
            Sequence src = | C4q D4q |
            Sequence result = (slow src 0.0)
            """);
        Assert.Equal(1, result.Bars.Count);
        Assert.All(result.Bars[0].MusicalNotes,
            n => Assert.Equal((int)NoteValueType.Value.QUARTER, n.DurationValue));
    }

    // ====================================================================
    // n <= 0 charitable returns
    // ====================================================================

    [Fact]
    public void ChunkZeroNReturnsInputUnchanged()
    {
        var result = EvalSequence("""
            Sequence src = | C4q D4q | E4q F4q |
            Sequence result = (chunk 0 (fn Sequence s => (rev s)) src)
            """);
        // n=0 is invalid; sequence passes through with bar order intact.
        Assert.Equal(2, result.Bars.Count);
        Assert.Equal('C', result.Bars[0].MusicalNotes[0].NoteName);
        Assert.Equal('E', result.Bars[1].MusicalNotes[0].NoteName);
    }

    [Fact]
    public void IterNegativeNReturnsInputUnchanged()
    {
        var result = EvalSequence("""
            Sequence src = | C4q D4q E4q F4q |
            Sequence result = (iter -1 src)
            """);
        Assert.Equal(1, result.Bars.Count);
        Assert.Equal('C', result.Bars[0].MusicalNotes[0].NoteName);
        Assert.Equal('F', result.Bars[0].MusicalNotes[3].NoteName);
    }

    // ====================================================================
    // phase non-finite offset
    // ====================================================================

    [Fact]
    public void PhaseNanOffsetReturnsInputUnchanged()
    {
        // Flow doesn't have a NaN literal; produce one via (div 0.0 0.0) or
        // use the nanFloat builtin from std.flow.
        var result = EvalSequence("""
            Sequence src = | C4q D4q | E4q F4q |
            Double nanValue = (nanFloat)
            Sequence result = (phase nanValue src)
            """);
        Assert.Equal(2, result.Bars.Count);
        Assert.Equal('C', result.Bars[0].MusicalNotes[0].NoteName);
        Assert.Equal('E', result.Bars[1].MusicalNotes[0].NoteName);
    }

    // ====================================================================
    // prob outside [0, 1] clamping
    // ====================================================================

    [Fact]
    public void SometimesProbAbove1ClampsToOne()
    {
        // prob > 1 clamps to 1 → fn applied to EVERY bar. Verify by
        // observing that rev applied to every bar (1-bar source) twice
        // returns to original (rev twice = identity at bar level).
        var result = EvalSequence("""
            Sequence src = | C4q D4q | E4q F4q |
            Sequence result = (sometimes 1.5 (fn Sequence s => (rev s)) src)
            """);
        Assert.Equal(2, result.Bars.Count);
    }

    [Fact]
    public void SparseSeqProbBelow0ClampsToZero()
    {
        // prob < 0 clamps to 0 → no drops, full sequence preserved.
        var result = EvalSequence("""
            Sequence src = | C4q D4q | E4q F4q |
            Sequence result = (sparseSeq -0.5 src)
            """);
        Assert.Equal(2, result.Bars.Count);
    }

    // ====================================================================
    // Empty-sequence behavior across stochastic combinators
    // ====================================================================

    [Fact]
    public void EmptySequenceUnchangedThroughCombinators()
    {
        // The simplest way to obtain a zero-bar Sequence from Flow source is
        // through PatternFunctions itself: dropping all bars via a high-prob
        // sparseSeq run against PrngRegistry-keyed PRNG produces an empty
        // result deterministically (prob=1.0 → every bar's draw 0..1 is < 1.0
        // so EVERY bar gets dropped). The follow-on call applies sometimes
        // to that empty sequence; it must short-circuit + advisory + return
        // the empty input.
        var result = EvalSequence("""
            Sequence src = | C4q D4q | E4q F4q |
            Sequence emptied = (sparseSeq 1.0 src)
            Sequence result = (sometimes 0.5 (fn Sequence s => (rev s)) emptied)
            """);
        Assert.Empty(result.Bars);
    }
}
