using System.Collections.Generic;
using FlowLang.Runtime;
using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Phase36;

/// <summary>
/// Phase 36 Plan 36-09 — behavior facts for the chaos-map generative
/// primitives: <c>(lorenz sigma rho beta length seed)</c>,
/// <c>(logistic r length seed)</c>, and <c>(quantizeToScale series scale)</c>.
///
/// <para>
/// <b>Lorenz</b> integrates the canonical 3-state Lorenz attractor via
/// forward-Euler (dt=0.01, warmup=100) and returns the x-axis trajectory as
/// an <c>Array[Double]</c>. Initial conditions <c>(x=1.0, y=0.0, z=0.0)</c>
/// receive a tiny seed-derived perturbation so different seeds produce
/// distinct trajectories. Cross-platform FP divergence is documented at
/// D-36-09 (Pitfall 4 in 36-RESEARCH); same-platform two-run cmp-clean is
/// preserved.
/// </para>
///
/// <para>
/// <b>Logistic map</b> iterates x_{n+1} = r * x_n * (1 - x_n) over a
/// seed-derived initial x in (0, 1) with the same warm-up policy. r values
/// outside [0, 4] charitably clamp + WarnOnce per D-v1.5-05.
/// </para>
///
/// <para>
/// <b>quantizeToScale</b> ships in two overloads: a String-form that
/// resolves a scale name (e.g. "cmajor") via
/// <see cref="FlowLang.StandardLibrary.Harmony.ScaleDatabase.GetScaleNotes"/>
/// (D-36-08 Claude's-Discretion pick), and an Array[Note] form that lets
/// composers supply the scale directly. Both normalise the input series to
/// [0, 1] and floor-map to scale-note indices.
/// </para>
/// </summary>
public class ChaosTests
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

    private static SequenceData RunSequenceScript(string body, string varName = "result")
    {
        using var runner = new FlowEngineRunner();
        var (success, _, stderr, errorCount) = runner.RunSource(Prelude + "\n" + body);
        Assert.True(success && errorCount == 0,
            $"Script failed: errorCount={errorCount}\nstderr:\n{stderr}\nbody:\n{body}");
        return runner.GetVariable(varName).As<SequenceData>();
    }

    // ====================================================================
    // Lorenz
    // ====================================================================

    [Fact]
    public void LorenzReturnsArrayDoubleOfLength()
    {
        // (lorenz 10.0 28.0 2.6667 200 42) returns Array[Double] of length 200.
        var arr = RunArrayScript("""
            Double[] result = (lorenz 10.0 28.0 2.6667 200 42)
            """);
        Assert.Equal(200, arr.Count);
        // Each element is a Double.
        foreach (var v in arr)
        {
            Assert.IsType<double>(v.Data);
        }
    }

    [Fact]
    public void LorenzWithCanonicalButterflyParams()
    {
        // Canonical butterfly params σ=10, ρ=28, β=8/3. The chaotic trajectory
        // stays bounded but visits both wings of the attractor; |x| < 30 in
        // practice after the warm-up settles into the attractor.
        var arr = RunArrayScript("""
            Double[] result = (lorenz 10.0 28.0 2.6667 200 42)
            """);
        // First 10 elements must be inside the bounded-attractor envelope.
        for (int i = 0; i < 10; i++)
        {
            double x = (double)arr[i].Data!;
            Assert.True(System.Math.Abs(x) < 30.0,
                $"Element {i} = {x} escaped bounded-attractor envelope (|x| < 30)");
        }
    }

    [Fact]
    public void LorenzDegenerateParamsFallsBackToCanonical()
    {
        // σ < 0 or ρ <= 0 or β <= 0 → fall back to canonical (10, 28, 8/3) +
        // WarnOnce. Two calls with degenerate vs the EXACT canonical-butterfly
        // constants (8/3 computed via `(div 8.0 3.0)`, NOT the
        // 4-decimal-rounded 2.6667) produce identical output at the same seed.
        // The exact-canonical comparison is required because forward-Euler
        // integration over a chaotic attractor amplifies even a sub-1e-5
        // discrepancy in β over 100 iterations.
        var degenerate = RunArrayScript("""
            Double[] result = (lorenz -1.0 28.0 2.6667 100 42)
            """);
        var canonical = RunArrayScript("""
            Double beta = (div 8.0 3.0)
            Double[] result = (lorenz 10.0 28.0 beta 100 42)
            """);
        Assert.Equal(degenerate.Count, canonical.Count);
        for (int i = 0; i < degenerate.Count; i++)
        {
            Assert.Equal((double)canonical[i].Data!, (double)degenerate[i].Data!);
        }
    }

    // ====================================================================
    // Logistic map
    // ====================================================================

    [Fact]
    public void LogisticReturnsArrayDoubleInUnitInterval()
    {
        // (logistic 3.9 100 42) returns Array[Double] where every element
        // sits in [0, 1] (logistic map invariant under r ∈ [0, 4]).
        var arr = RunArrayScript("""
            Double[] result = (logistic 3.9 100 42)
            """);
        Assert.Equal(100, arr.Count);
        foreach (var v in arr)
        {
            double x = (double)v.Data!;
            Assert.InRange(x, 0.0, 1.0);
        }
    }

    [Fact]
    public void LogisticFixedPointBelow3()
    {
        // For r in (1, 3), the logistic map settles to x* = 1 - 1/r within
        // a few iterations. With r=2.5 and warmup=100, every emitted value
        // should be at x* = 0.6 (within a tight tolerance).
        var arr = RunArrayScript("""
            Double[] result = (logistic 2.5 50 42)
            """);
        double expected = 1.0 - 1.0 / 2.5; // = 0.6
        foreach (var v in arr)
        {
            double x = (double)v.Data!;
            Assert.True(System.Math.Abs(x - expected) < 1e-6,
                $"Logistic r=2.5 expected fixed point {expected}, got {x}");
        }
    }

    [Fact]
    public void LogisticClampsROutsideRange()
    {
        // r > 4 produces escape from [0, 1] and NaN. Charitable clamp to 4.0
        // + WarnOnce. The clamped output is the same as r=4.0 at the same
        // seed.
        var clamped = RunArrayScript("""
            Double[] result = (logistic 5.0 10 42)
            """);
        var canonical = RunArrayScript("""
            Double[] result = (logistic 4.0 10 42)
            """);
        Assert.Equal(clamped.Count, canonical.Count);
        for (int i = 0; i < clamped.Count; i++)
        {
            Assert.Equal((double)canonical[i].Data!, (double)clamped[i].Data!);
        }
    }

    // ====================================================================
    // quantizeToScale
    // ====================================================================

    [Fact]
    public void QuantizeToScaleStringForm()
    {
        // (quantizeToScale [0.0, 0.5, 1.0] "cmajor") produces a Sequence
        // with 3 notes that span the C major scale (7 notes): the endpoints
        // map to index 0 (lowest scale tone) and index 6 (highest); the
        // midpoint maps to a middle index.
        var seq = RunSequenceScript("""
            Double[] series = [0.0, 0.5, 1.0]
            Sequence result = (quantizeToScale series "cmajor")
            """);
        // Collect all the notes from the sequence's bars.
        var notes = new List<MusicalNoteData>();
        foreach (var bar in seq.Bars)
            notes.AddRange(bar.MusicalNotes);
        Assert.Equal(3, notes.Count);
        // C major: C D E F G A B — note 0 is C, note 6 is B.
        Assert.Equal('C', notes[0].NoteName);
        Assert.Equal('B', notes[2].NoteName);
    }

    [Fact]
    public void QuantizeToScaleArrayForm()
    {
        // Direct Array[Note] form: caller supplies scale notes explicitly
        // (escape hatch from ScaleDatabase). 3 input values, 3 scale notes →
        // exact-match mapping.
        var seq = RunSequenceScript("""
            Double[] series = [0.0, 0.5, 1.0]
            Note[] scale = [C4, E4, G4]
            Sequence result = (quantizeToScale series scale)
            """);
        var notes = new List<MusicalNoteData>();
        foreach (var bar in seq.Bars)
            notes.AddRange(bar.MusicalNotes);
        Assert.Equal(3, notes.Count);
        // index 0 → C4; index 1 (mid via 0.5*3=1.5 floor=1) → E4; index 2 → G4
        Assert.Equal('C', notes[0].NoteName);
        Assert.Equal('E', notes[1].NoteName);
        Assert.Equal('G', notes[2].NoteName);
    }

    [Fact]
    public void QuantizeToScaleNormalizesRange()
    {
        // Unbounded input range (negative + positive) is normalised to [0, 1]
        // before quantization. The endpoint -5.0 maps to the lowest scale
        // note; +5.0 maps to the highest.
        var seq = RunSequenceScript("""
            Double[] series = [-5.0, 0.0, 5.0]
            Sequence result = (quantizeToScale series "cmajor")
            """);
        var notes = new List<MusicalNoteData>();
        foreach (var bar in seq.Bars)
            notes.AddRange(bar.MusicalNotes);
        Assert.Equal(3, notes.Count);
        // C major scale endpoints — first note = C (index 0), last note = B (index 6).
        Assert.Equal('C', notes[0].NoteName);
        Assert.Equal('B', notes[2].NoteName);
    }

    [Fact]
    public void QuantizeUnknownScaleNameCharitablyFallsBackToChromatic()
    {
        // Unknown scale name: charitable fallback to chromatic (12 notes
        // C..B) + WarnOnce per CLAUDE.md ergonomics. The fallback produces
        // a Sequence — not an error — so the composer hears something.
        var seq = RunSequenceScript("""
            Double[] series = [0.0, 0.5, 1.0]
            Sequence result = (quantizeToScale series "bogus")
            """);
        var notes = new List<MusicalNoteData>();
        foreach (var bar in seq.Bars)
            notes.AddRange(bar.MusicalNotes);
        Assert.Equal(3, notes.Count);
        // Chromatic fallback spans C4..B4 (12 notes); index 0 = C, index 11 = B.
        Assert.Equal('C', notes[0].NoteName);
        Assert.Equal('B', notes[2].NoteName);
    }
}
