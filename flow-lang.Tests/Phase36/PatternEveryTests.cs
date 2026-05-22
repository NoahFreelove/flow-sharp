using System.Linq;
using FlowLang.StandardLibrary.Patterns;
using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Phase36;

/// <summary>
/// Phase 36 Plan 36-05 Task 1 — 10 deterministic combinators' xUnit facts.
///
/// Pins the structural shape of <c>every / fast / slow / chunk / phase / rev /
/// iter / palindrome / jux / superimpose</c> against fixed Sequence inputs
/// constructed via <c>FlowEngineRunner.GetVariable</c>. The script-side
/// note-stream → Sequence compilation is the source of truth (avoids hand-
/// building bars with the full ctor); the asserts read from the engine's
/// global frame.
///
/// <para>
/// Charitable-interpretation facts (PAT-02 + Pitfall 9): zero-length input,
/// n=0, factor=0, NaN offset all return their input unchanged. The advisory
/// emission is the visible side-effect — but RenderingDiagnostics dedups by
/// sentinel per process, so direct stderr inspection is brittle across
/// successive facts. We assert behavior (sequence unchanged) and trust the
/// dedicated PatternChalkyEdgeCasesTests (Task 2) for advisory-text gates.
/// </para>
/// </summary>
[Collection("FlowScripts")]
public class PatternEveryTests
{
    private const string Prelude = """
        use "@std"
        use "@patterns"
        """;

    /// <summary>
    /// Helper: build a 4-bar Sequence via note-stream literal, apply a single
    /// pattern transform, and read the bound result variable's
    /// <see cref="SequenceData"/> back through the engine's global frame.
    /// Per Phase 33 SfzBindingTests precedent: top-level <c>Sequence ... =</c>
    /// bindings land in the global frame; tempo/timesig blocks scope their
    /// CHILD variable declarations, so the note-stream literal sits at file
    /// top-level wrapped in a single context block that immediately closes.
    /// </summary>
    private static SequenceData EvalSequence(string body)
    {
        using var runner = new FlowEngineRunner();
        // Wrap the body in a tempo/timesig block AT TOP LEVEL: bindings INSIDE
        // the block scope to the block frame, so we re-export `result` by
        // assigning it back outside the block. The simplest pattern: open the
        // context block first, declare src + intermediate result there, then
        // copy to a top-level alias.
        var source = Prelude + "\n" + body;
        var (success, _, stderr, errorCount) = runner.RunSource(source);
        Assert.True(success && errorCount == 0,
            $"Script failed: errorCount={errorCount}\nstderr:\n{stderr}\nsource:\n{source}");
        return runner.GetVariable("result").As<SequenceData>();
    }

    // ====================================================================
    // every — D-36-04 BARS
    // ====================================================================

    [Fact]
    public void EveryAppliesFnAtCycleBoundary()
    {
        // every 2 fast 2 on a 4-bar source with quarter notes:
        // - bars 0 and 2 (i % 2 == 0) get the lambda applied — fast 2 halves
        //   durations, so notes in those bars become eighth-duration enum
        //   (QUARTER=2 → EIGHTH=3).
        // - bars 1 and 3 pass through with QUARTER durations intact.
        var result = EvalSequence("""
            Sequence src = | C4q D4q | E4q F4q | G4q A4q | B4q C5q |
            Sequence result = (every 2 (fn Sequence s => (fast s 2.0)) src)
            """);

        Assert.Equal(4, result.Bars.Count);
        Assert.All(result.Bars[0].MusicalNotes,
            n => Assert.Equal((int)NoteValueType.Value.EIGHTH, n.DurationValue));
        Assert.All(result.Bars[1].MusicalNotes,
            n => Assert.Equal((int)NoteValueType.Value.QUARTER, n.DurationValue));
        Assert.All(result.Bars[2].MusicalNotes,
            n => Assert.Equal((int)NoteValueType.Value.EIGHTH, n.DurationValue));
        Assert.All(result.Bars[3].MusicalNotes,
            n => Assert.Equal((int)NoteValueType.Value.QUARTER, n.DurationValue));
    }

    [Fact]
    public void EveryCharitablyIgnoresZeroN()
    {
        // n=0 is invalid; every must return input UNCHANGED + emit advisory.
        var result = EvalSequence("""
            Sequence src = | C4q D4q |
            Sequence result = (every 0 (fn Sequence s => (rev s)) src)
            """);
        Assert.Equal(1, result.Bars.Count);
        Assert.Equal(2, result.Bars[0].MusicalNotes.Count);
        Assert.Equal('C', result.Bars[0].MusicalNotes[0].NoteName);
        Assert.Equal('D', result.Bars[0].MusicalNotes[1].NoteName);
    }

    [Fact]
    public void EveryCharitablyIgnoresNegativeN()
    {
        var result = EvalSequence("""
            Sequence src = | C4q D4q |
            Sequence result = (every -3 (fn Sequence s => (rev s)) src)
            """);
        Assert.Equal(1, result.Bars.Count);
    }

    // ====================================================================
    // fast / slow
    // ====================================================================

    [Fact]
    public void FastHalvesDurations()
    {
        // fast 2 should shift QUARTER → EIGHTH on every note.
        var result = EvalSequence("""
            Sequence src = | C4q D4q |
            Sequence result = (fast src 2.0)
            """);
        Assert.Equal(1, result.Bars.Count);
        Assert.All(result.Bars[0].MusicalNotes,
            n => Assert.Equal((int)NoteValueType.Value.EIGHTH, n.DurationValue));
    }

    [Fact]
    public void SlowDoublesDurations()
    {
        // slow 2 should shift QUARTER → HALF on every note.
        var result = EvalSequence("""
            Sequence src = | C4q D4q |
            Sequence result = (slow src 2.0)
            """);
        Assert.Equal(1, result.Bars.Count);
        Assert.All(result.Bars[0].MusicalNotes,
            n => Assert.Equal((int)NoteValueType.Value.HALF, n.DurationValue));
    }

    [Fact]
    public void FastUnitFactorIsIdentity()
    {
        // factor=1 is identity (no enum shift); content unchanged.
        var result = EvalSequence("""
            Sequence src = | C4q D4q |
            Sequence result = (fast src 1.0)
            """);
        Assert.Equal(1, result.Bars.Count);
        Assert.All(result.Bars[0].MusicalNotes,
            n => Assert.Equal((int)NoteValueType.Value.QUARTER, n.DurationValue));
    }

    // ====================================================================
    // rev / iter / palindrome
    // ====================================================================

    [Fact]
    public void RevReversesBarOrder()
    {
        var result = EvalSequence("""
            Sequence src = | C4q D4q | E4q F4q | G4q A4q |
            Sequence result = (rev src)
            """);
        Assert.Equal(3, result.Bars.Count);
        // Original bar 0 was [C4, D4]; after rev it must be the LAST bar.
        Assert.Equal('G', result.Bars[0].MusicalNotes[0].NoteName);
        Assert.Equal('E', result.Bars[1].MusicalNotes[0].NoteName);
        Assert.Equal('C', result.Bars[2].MusicalNotes[0].NoteName);
        // Within-bar note order preserved (rev is BAR-LEVEL — compare to retrograde).
        Assert.Equal('D', result.Bars[2].MusicalNotes[1].NoteName);
    }

    [Fact]
    public void IterRotatesByNSteps()
    {
        // 1 bar containing 4 notes; iter 2 shifts by totalNotes/n = 4/2 = 2.
        // Original: [C, D, E, F] → after iter 2: [E, F, C, D].
        var result = EvalSequence("""
            Sequence src = | C4q D4q E4q F4q |
            Sequence result = (iter 2 src)
            """);
        Assert.Equal(1, result.Bars.Count);
        Assert.Equal('E', result.Bars[0].MusicalNotes[0].NoteName);
        Assert.Equal('F', result.Bars[0].MusicalNotes[1].NoteName);
        Assert.Equal('C', result.Bars[0].MusicalNotes[2].NoteName);
        Assert.Equal('D', result.Bars[0].MusicalNotes[3].NoteName);
    }

    [Fact]
    public void PalindromeConcatenatesSeqAndReversed()
    {
        // [A B] → [A B B A].
        var result = EvalSequence("""
            Sequence src = | C4q D4q | E4q F4q |
            Sequence result = (palindrome src)
            """);
        Assert.Equal(4, result.Bars.Count);
        Assert.Equal('C', result.Bars[0].MusicalNotes[0].NoteName);
        Assert.Equal('E', result.Bars[1].MusicalNotes[0].NoteName);
        // Mirror: bar 2 = original bar 1 (E F); bar 3 = original bar 0 (C D).
        Assert.Equal('E', result.Bars[2].MusicalNotes[0].NoteName);
        Assert.Equal('C', result.Bars[3].MusicalNotes[0].NoteName);
    }

    // ====================================================================
    // chunk / phase  — D-36-04 BARS
    // ====================================================================

    [Fact]
    public void ChunkAppliesOneChunkPerCycle()
    {
        // Pre-condition: reset chunk rotation so this fact starts from index 0.
        PatternFunctions.ResetChunkRotationForTesting();

        // 4 bars, chunk 4, transposing fn: chunk 0 gets fn on first invocation.
        // After chunkSize = ceil(4 / 4) = 1: chunk 0 is bar 0 alone.
        var result = EvalSequence("""
            Sequence src = | C4q D4q | E4q F4q | G4q A4q | B4q C5q |
            Sequence result = (chunk 4 (fn Sequence s => (transpose s +12st)) src)
            """);

        Assert.Equal(4, result.Bars.Count);
        // Bar 0 should have C5/D5 (transposed up an octave from C4/D4).
        Assert.Equal('C', result.Bars[0].MusicalNotes[0].NoteName);
        Assert.Equal(5, result.Bars[0].MusicalNotes[0].Octave);
        // Bar 1 unchanged: E4.
        Assert.Equal('E', result.Bars[1].MusicalNotes[0].NoteName);
        Assert.Equal(4, result.Bars[1].MusicalNotes[0].Octave);
    }

    [Fact]
    public void PhaseRotatesBars()
    {
        // 4 bars; phase 0.5 → rotate by round(0.5 × 4) = 2.
        // Original bars: [C, E, G, B]; after rotate by 2: [G, B, C, E].
        var result = EvalSequence("""
            Sequence src = | C4q D4q | E4q F4q | G4q A4q | B4q C5q |
            Sequence result = (phase 0.5 src)
            """);
        Assert.Equal(4, result.Bars.Count);
        Assert.Equal('G', result.Bars[0].MusicalNotes[0].NoteName);
        Assert.Equal('B', result.Bars[1].MusicalNotes[0].NoteName);
        Assert.Equal('C', result.Bars[2].MusicalNotes[0].NoteName);
        Assert.Equal('E', result.Bars[3].MusicalNotes[0].NoteName);
    }

    // ====================================================================
    // jux / superimpose
    // ====================================================================

    [Fact]
    public void JuxLayersOriginalAndFnAsVoiceBlock()
    {
        // jux transposes the layered voice up an octave. Result bar's
        // ParallelVoices = [original, fn(original)].
        var result = EvalSequence("""
            Sequence src = | C4q D4q |
            Sequence result = (jux (fn Sequence s => (transpose s +12st)) src)
            """);
        Assert.Equal(1, result.Bars.Count);
        var bar = result.Bars[0];
        Assert.NotNull(bar.ParallelVoices);
        Assert.Equal(2, bar.ParallelVoices!.Count);
        // First voice = original (C4).
        Assert.Equal('C', bar.ParallelVoices[0].MusicalNotes[0].NoteName);
        Assert.Equal(4, bar.ParallelVoices[0].MusicalNotes[0].Octave);
        // Second voice = transposed (C5).
        Assert.Equal('C', bar.ParallelVoices[1].MusicalNotes[0].NoteName);
        Assert.Equal(5, bar.ParallelVoices[1].MusicalNotes[0].Octave);
    }

    [Fact]
    public void SuperimposeLayersOriginalAndFn()
    {
        var result = EvalSequence("""
            Sequence src = | C4q D4q |
            Sequence result = (superimpose (fn Sequence s => (transpose s +7st)) src)
            """);
        Assert.Equal(1, result.Bars.Count);
        var bar = result.Bars[0];
        Assert.NotNull(bar.ParallelVoices);
        Assert.Equal(2, bar.ParallelVoices!.Count);
        // Voice 0 = original (C4).
        Assert.Equal('C', bar.ParallelVoices[0].MusicalNotes[0].NoteName);
        Assert.Equal(4, bar.ParallelVoices[0].MusicalNotes[0].Octave);
        // Voice 1 = +7 semitones → G4.
        Assert.Equal('G', bar.ParallelVoices[1].MusicalNotes[0].NoteName);
    }
}
