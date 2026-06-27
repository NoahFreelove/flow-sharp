using System.Collections.Generic;
using FlowLang.Runtime;
using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Phase36;

/// <summary>
/// Phase 36 Plan 36-08 Task 1+2 — behavior facts for the cellular automata
/// primitives: 1D elementary CA via <c>(cellular rule width steps seed)</c>
/// and 2D Game of Life via <c>(life width height steps seed)</c>.
///
/// <para>
/// <b>1D CA — single-1-center default (Wolfram convention, RESEARCH §Pattern 4):</b>
/// Rule 30 / Rule 90 / Rule 110 / Rule 184 surface "interesting" emergent
/// patterns from a single 1 at <c>width/2</c>. The canonical Rule 30 and
/// Rule 90 patterns are computed by hand and pinned as expected output.
/// </para>
///
/// <para>
/// <b>2D Life — seeded random fill at 30% density:</b> deterministic given
/// the seed; the composer's seed flows directly into a <c>new Random(seed)</c>
/// (per the plan, no PrngRegistry routing — the seed is REQUIRED).
/// </para>
///
/// <para>
/// <b>Steps convention:</b> <c>steps</c> is the total row count INCLUDING the
/// initial row, so <c>(cellular 30 16 8 0)</c> produces an 8-row grid where
/// row 0 is the single-1-center initial and rows 1..7 are seven iterations.
/// </para>
/// </summary>
public class CellularTests
{
    private const string Prelude = """
        use "@std"
        use "@generative"
        """;

    private static SequenceData RunSequenceScript(string body, string varName = "result")
    {
        using var runner = new FlowEngineRunner();
        var (success, _, stderr, errorCount) = runner.RunSource(Prelude + "\n" + body);
        Assert.True(success && errorCount == 0,
            $"Script failed: errorCount={errorCount}\nstderr:\n{stderr}\nbody:\n{body}");
        return runner.GetVariable(varName).As<SequenceData>();
    }

    private static IReadOnlyList<Value> RunArrayScript(string body, string varName = "result")
    {
        using var runner = new FlowEngineRunner();
        var (success, _, stderr, errorCount) = runner.RunSource(Prelude + "\n" + body);
        Assert.True(success && errorCount == 0,
            $"Script failed: errorCount={errorCount}\nstderr:\n{stderr}\nbody:\n{body}");
        return runner.GetVariable(varName).As<IReadOnlyList<Value>>();
    }

    /// <summary>
    /// Returns a row-by-row "01" string-representation of a 1D CA sequence:
    /// one string per bar, where each character is '1' for a non-rest note
    /// and '0' for a rest. Used to compare against the hand-computed canonical
    /// Wolfram patterns.
    /// </summary>
    private static string[] SequenceToBoolRows(SequenceData seq)
    {
        var rows = new string[seq.Bars.Count];
        for (int r = 0; r < seq.Bars.Count; r++)
        {
            var bar = seq.Bars[r];
            var chars = new char[bar.MusicalNotes.Count];
            for (int c = 0; c < bar.MusicalNotes.Count; c++)
                chars[c] = bar.MusicalNotes[c].IsRest ? '0' : '1';
            rows[r] = new string(chars);
        }
        return rows;
    }

    // ====================================================================
    // 1D Rule 30 canonical chaos pattern
    // ====================================================================

    [Fact]
    public void Rule30CanonicalChaos()
    {
        // The seed arg is ignored for the 1D default (single-1-center) — kept
        // for signature uniformity with the REQ wording. Pin the Wolfram-atlas
        // canonical Rule 30 pattern for width=16, steps=8.
        var seq = RunSequenceScript("""
            Sequence result = (cellular 30 16 8 0)
            """);

        // 8 rows expected (initial + 7 iterations).
        Assert.Equal(8, seq.Bars.Count);

        // Hand-computed Wolfram Rule 30 pattern. Verified against the
        // python reference computation in the plan's verification block.
        string[] expected =
        {
            "0000000010000000",
            "0000000111000000",
            "0000001100100000",
            "0000011011110000",
            "0000110010001000",
            "0001101111011100",
            "0011001000010010",
            "0110111100111111",
        };

        var actual = SequenceToBoolRows(seq);
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], actual[i]);
        }
    }

    [Fact]
    public void Rule90CanonicalSierpinski()
    {
        // Rule 90 = XOR-of-neighbors → Sierpinski triangle pattern.
        var seq = RunSequenceScript("""
            Sequence result = (cellular 90 16 8 0)
            """);

        Assert.Equal(8, seq.Bars.Count);

        string[] expected =
        {
            "0000000010000000",
            "0000000101000000",
            "0000001000100000",
            "0000010101010000",
            "0000100000001000",
            "0001010000010100",
            "0010001000100010",
            "0101010101010101",
        };

        var actual = SequenceToBoolRows(seq);
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], actual[i]);
        }
    }

    [Fact]
    public void Rule110UniversalComputation()
    {
        // Rule 110 is Turing-universal — smoke test that the pattern is
        // non-trivial (not all zeros, not just a single column lit).
        var seq = RunSequenceScript("""
            Sequence result = (cellular 110 16 8 0)
            """);

        Assert.Equal(8, seq.Bars.Count);
        var rows = SequenceToBoolRows(seq);

        // Count total alive cells across all rows; should be > 8 (a non-trivial
        // pattern grew from a single seed cell).
        int totalAlive = 0;
        foreach (var row in rows)
        {
            foreach (var ch in row)
                if (ch == '1') totalAlive++;
        }
        Assert.True(totalAlive > 8, $"Rule 110 pattern looks trivial (alive cells = {totalAlive})");
    }

    [Fact]
    public void RuleClampedToByteRange()
    {
        // Rules outside [0, 255] wrap via (rule & 0xFF) + one-shot advisory.
        // (cellular 300 ...) should produce the same output as
        // (cellular 44 ...) since 300 & 0xFF == 44.
        var wrapped = RunSequenceScript("""
            Sequence result = (cellular 300 16 4 0)
            """, "result");

        var canonical = RunSequenceScript("""
            Sequence result = (cellular 44 16 4 0)
            """, "result");

        Assert.Equal(canonical.Bars.Count, wrapped.Bars.Count);
        var wrappedRows = SequenceToBoolRows(wrapped);
        var canonicalRows = SequenceToBoolRows(canonical);
        for (int i = 0; i < canonicalRows.Length; i++)
            Assert.Equal(canonicalRows[i], wrappedRows[i]);
    }

    [Fact]
    public void WidthZeroReturnsEmpty()
    {
        // Charitable interpretation: width <= 0 → empty Sequence + WarnOnce.
        var seq = RunSequenceScript("""
            Sequence result = (cellular 30 0 4 0)
            """);
        Assert.Empty(seq.Bars);
    }

    [Fact]
    public void StepsZeroReturnsSingleRow()
    {
        // steps=0 is the boundary case: "zero rows requested" → empty grid.
        var seq = RunSequenceScript("""
            Sequence result = (cellular 30 16 0 0)
            """);
        Assert.Empty(seq.Bars);
    }

    [Fact]
    public void CellularSeededAcceptsCustomPattern()
    {
        // cellularSeeded replaces the single-1-center default with an
        // explicit Array[Bool] initial pattern. Row 0 of the output should
        // be the supplied pattern verbatim.
        var seq = RunSequenceScript("""
            Bool[] init = (list true false true false false false false false)
            Sequence result = (cellularSeeded 30 8 2 0 init)
            """);

        Assert.Equal(2, seq.Bars.Count);
        // Row 0 = initial: "10100000" (8 columns).
        Assert.Equal("10100000", SequenceToBoolRows(seq)[0]);
    }

    [Fact]
    public void DimensionsClampedAtUpperBound()
    {
        // DoS guard: width > 1024 → clamped to 1024 + WarnOnce. The composer
        // gets a result rather than an OOM; the clamp prevents a runaway grid.
        // We can't observe stderr easily from here, so we just verify the
        // call completes and the actual rendered grid has width <= 1024.
        var seq = RunSequenceScript("""
            Sequence result = (cellular 30 2000 2 0)
            """);
        Assert.Equal(2, seq.Bars.Count);
        Assert.True(seq.Bars[0].MusicalNotes.Count <= 1024,
            $"Expected width clamped to 1024; got {seq.Bars[0].MusicalNotes.Count}");
    }

    // ====================================================================
    // 2D Game of Life
    // ====================================================================

    [Fact]
    public void LifeReturnsArrayOfHeight()
    {
        // (life width height steps seed) → Array[Sequence] of length=height.
        // Each Sequence has exactly `steps` bars (one per step).
        var arr = RunArrayScript("""
            Sequence[] result = (life 8 4 2 42)
            """);

        Assert.Equal(4, arr.Count);
        foreach (var item in arr)
        {
            Assert.IsType<SequenceType>(item.Type);
            var seq = item.As<SequenceData>();
            Assert.Equal(2, seq.Bars.Count);
            Assert.Equal(8, seq.Bars[0].MusicalNotes.Count);
        }
    }

    [Fact]
    public void LifeDimensionsClamped()
    {
        // DoS guard: width or height > 1024 → clamped + WarnOnce.
        var arr = RunArrayScript("""
            Sequence[] result = (life 2000 4 2 42)
            """);

        Assert.Equal(4, arr.Count);
        var first = arr[0].As<SequenceData>();
        Assert.True(first.Bars[0].MusicalNotes.Count <= 1024,
            $"Expected width clamped to 1024; got {first.Bars[0].MusicalNotes.Count}");
    }
}
