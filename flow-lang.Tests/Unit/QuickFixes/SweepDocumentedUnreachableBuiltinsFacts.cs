using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Unit.QuickFixes;

/// <summary>
/// sweep-2026-06-14 (group documented-unreachable-builtins) regression facts.
///
/// Covers four confirmed bugs where a builtin was documented (BuiltInDocs /
/// CLAUDE.md) but unreachable from Flow source, or produced wrong output:
///
///   1. `length` was documented but only `len` was registered → calling
///      `(length xs)` errored "Function 'length' not found". Now `length` is a
///      registered alias of `len` (String + Array overloads) with a matching
///      `internal proc length` surface in std.flow.
///   2. `(zip a b)` carried a BuiltInDocs entry + CLAUDE.md mention but was never
///      registered → "Function 'zip' not found". Now Collections.Zip emits an
///      array of 2-tuples (stopping at the shorter length, charitable on
///      mismatch) registered as `zip` with an `internal proc zip` surface.
///   3. `(inspect seq)` was the documented alias of `(visualize seq)` (D-38-10)
///      with a live C# registration but NO `.flow` surface → unreachable from
///      Flow. Now std.flow declares `internal proc inspect` (Sequence + Buffer).
///   4. `(visualize seq)` / `(inspect seq)` reported "(no notes in sequence)" for
///      voice-block sequences because the per-bar loop only inspected
///      `bar.MusicalNotes` (a whole-bar rest placeholder for voice-block bars)
///      and never `bar.ParallelVoices`. Now both passes run; overlapping voices
///      stack on their pitch rows.
/// </summary>
[Collection("FlowScripts")]
public class SweepDocumentedUnreachableBuiltinsFacts
{
    // ---- Bug 1: `length` alias of `len` -----------------------------------

    [Fact]
    public void Length_OnArray_ReturnsCount()
    {
        using var runner = new FlowEngineRunner();
        var (success, stdout, _, errorCount) = runner.RunSource(
            "(print (str (length (list 1 2 3))))");

        Assert.True(success);
        Assert.Equal(0, errorCount);
        Assert.Equal("3", stdout.Trim());
    }

    [Fact]
    public void Length_OnString_ReturnsCharCount()
    {
        using var runner = new FlowEngineRunner();
        var (success, stdout, _, errorCount) = runner.RunSource(
            "(print (str (length \"hello\")))");

        Assert.True(success);
        Assert.Equal(0, errorCount);
        Assert.Equal("5", stdout.Trim());
    }

    // ---- Bug 2: `zip` ------------------------------------------------------

    [Fact]
    public void Zip_TwoEqualArrays_ProducesPairs()
    {
        using var runner = new FlowEngineRunner();
        var (success, stdout, _, errorCount) = runner.RunSource(
            "Int[] a = (list 1 2); Int[] b = (list 3 4); (print (str (zip a b)))");

        Assert.True(success);
        Assert.Equal(0, errorCount);
        Assert.Equal("[<<1, 3>>, <<2, 4>>]", stdout.Trim());
    }

    [Fact]
    public void Zip_MismatchedLengths_StopsAtShorter_NoError()
    {
        using var runner = new FlowEngineRunner();
        // Charitable: extra tail of the longer array is dropped, no exception.
        var (success, stdout, _, errorCount) = runner.RunSource(
            "Int[] a = (list 1 2 3); Int[] b = (list 9); (print (str (zip a b)))");

        Assert.True(success);
        Assert.Equal(0, errorCount);
        Assert.Equal("[<<1, 9>>]", stdout.Trim());
    }

    [Fact]
    public void Zip_PairsAreRealTuples_UnpackWorks()
    {
        using var runner = new FlowEngineRunner();
        // (unpack <<1, 3>> add) → 4 proves each element is a genuine 2-tuple
        // carrying both source values in order.
        var (success, stdout, _, errorCount) = runner.RunSource(
            "Int[] a = (list 1 2); Int[] b = (list 3 4); " +
            "(print (str (unpack (head (zip a b)) add)))");

        Assert.True(success);
        Assert.Equal(0, errorCount);
        Assert.Equal("4", stdout.Trim());
    }

    // ---- Bug 3: `inspect` reachable from Flow -----------------------------

    [Fact]
    public void Inspect_IsCallableFromFlow_NoFunctionNotFound()
    {
        using var runner = new FlowEngineRunner();
        var (success, stdout, stderr, errorCount) = runner.RunSource(
            "Sequence s = | C4 E4 G4 |\n(inspect s)");

        Assert.True(success);
        Assert.Equal(0, errorCount);
        Assert.DoesNotContain("not found", stderr);
        // Renders the same ASCII piano roll as visualize — pitch rows present.
        Assert.Contains("C4", stdout);
        Assert.Contains("G4", stdout);
    }

    [Fact]
    public void Inspect_AndVisualize_ProduceIdenticalFlowOutput()
    {
        string RunOne(string call)
        {
            using var runner = new FlowEngineRunner();
            var (success, stdout, _, errorCount) = runner.RunSource(
                $"Sequence s = | C4 E4 G4 |\n({call} s)");
            Assert.True(success);
            Assert.Equal(0, errorCount);
            return stdout;
        }

        Assert.Equal(RunOne("visualize"), RunOne("inspect"));
    }

    // ---- Bug 4: voice-block visualization ---------------------------------

    [Fact]
    public void Visualize_VoiceBlockSequence_ShowsAllVoices_NotEmpty()
    {
        using var runner = new FlowEngineRunner();
        var (success, stdout, _, errorCount) = runner.RunSource(
            "timesig 4/4 { Sequence vb = | {voice C4w} {voice E4q F4q G4q A4q} | (visualize vb) }");

        Assert.True(success);
        Assert.Equal(0, errorCount);
        // The pre-fix bug printed exactly this placeholder.
        Assert.DoesNotContain("(no notes in sequence)", stdout);
        // The held whole-note voice (C4) AND the running voice (E4..A4) must both
        // show their own pitch rows.
        Assert.Contains("C4", stdout);
        Assert.Contains("E4", stdout);
        Assert.Contains("A4", stdout);
    }

    [Fact]
    public void Inspect_VoiceBlockSequence_AlsoShowsAllVoices()
    {
        using var runner = new FlowEngineRunner();
        var (success, stdout, _, errorCount) = runner.RunSource(
            "timesig 4/4 { Sequence vb = | {voice C4w} {voice E4q F4q G4q A4q} | (inspect vb) }");

        Assert.True(success);
        Assert.Equal(0, errorCount);
        Assert.DoesNotContain("(no notes in sequence)", stdout);
        Assert.Contains("C4", stdout);
        Assert.Contains("A4", stdout);
    }
}
