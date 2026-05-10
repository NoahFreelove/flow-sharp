using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase20;

/// <summary>
/// DEFER-05 regression: slice(Array[T], Int, Int) and slice(Sequence, Int, Int)
/// gain Python-style negative-from-end normalization (count + idx) BEFORE the
/// existing Phase 14 D-01 silent two-sided clamp. Extreme negatives clamp
/// post-normalization per CONTEXT D-USER-D.
///
/// Verification matrix (20-RESEARCH §Pattern 2): every Phase14/SliceTests Fact
/// case COINCIDES between old silent-clamp and new Python normalization;
/// only NEW negative cases observe a behavioral change. These Facts pin the
/// new behavior — Phase14/SliceTests pins the unchanged coincidence cases.
///
/// Direct C# dispatch via Collections.SliceArray / Collections.SliceSequence
/// bypasses the parser per 20-RESEARCH Pitfall 4 (negative-literal
/// binary-subtraction ambiguity at script-level).
/// </summary>
public class SliceNegativeTests
{
    private static Value MakeIntArray(params int[] xs)
    {
        var elems = new List<Value>();
        foreach (var x in xs) elems.Add(Value.Int(x));
        return Value.Array(elems, IntType.Instance);
    }

    // Bar construction mirrors Phase14/SliceTests.MakeThreeBarSequence — empty
    // MusicalNotes, valid TimeSignature satisfies SequenceData.AddBar invariants
    // (Mode == Musical, TimeSignature != null).
    private static SequenceData MakeThreeBarSequence()
    {
        var ts = new TimeSignatureData(4, 4);
        var seq = new SequenceData();
        seq.AddBar(new BarData(new List<MusicalNoteData>(), ts));
        seq.AddBar(new BarData(new List<MusicalNoteData>(), ts));
        seq.AddBar(new BarData(new List<MusicalNoteData>(), ts));
        return seq;
    }

    // ---- Array[T] overload — DEFER-05 acceptance ----

    [Fact]
    public void Array_NegativeStart_FromEnd()
    {
        // -3 + 5 = 2; slice arr 2 5 = [3, 4, 5]
        var arr = MakeIntArray(1, 2, 3, 4, 5);
        var result = Collections.SliceArray(new[] { arr, Value.Int(-3), Value.Int(5) });
        var elems = result.As<IReadOnlyList<Value>>();
        Assert.Equal(3, elems.Count);
        Assert.Equal(3, elems[0].As<int>());
        Assert.Equal(4, elems[1].As<int>());
        Assert.Equal(5, elems[2].As<int>());
    }

    [Fact]
    public void Array_NegativeEnd_FromEnd()
    {
        // -1 + 5 = 4; slice arr 0 4 = [1, 2, 3, 4]
        var arr = MakeIntArray(1, 2, 3, 4, 5);
        var result = Collections.SliceArray(new[] { arr, Value.Int(0), Value.Int(-1) });
        var elems = result.As<IReadOnlyList<Value>>();
        Assert.Equal(4, elems.Count);
        Assert.Equal(1, elems[0].As<int>());
        Assert.Equal(2, elems[1].As<int>());
        Assert.Equal(3, elems[2].As<int>());
        Assert.Equal(4, elems[3].As<int>());
    }

    [Fact]
    public void Array_BothNegative()
    {
        // -3 + 5 = 2; -1 + 5 = 4; slice arr 2 4 = [3, 4]
        var arr = MakeIntArray(1, 2, 3, 4, 5);
        var result = Collections.SliceArray(new[] { arr, Value.Int(-3), Value.Int(-1) });
        var elems = result.As<IReadOnlyList<Value>>();
        Assert.Equal(2, elems.Count);
        Assert.Equal(3, elems[0].As<int>());
        Assert.Equal(4, elems[1].As<int>());
    }

    [Fact]
    public void Array_NegativeBoundaryEqualsLen()
    {
        // -5 + 5 = 0; coincides with old silent-clamp result.
        // Verification matrix row -5/2 — this case stays UNCHANGED.
        var arr = MakeIntArray(1, 2, 3, 4, 5);
        var result = Collections.SliceArray(new[] { arr, Value.Int(-5), Value.Int(2) });
        var elems = result.As<IReadOnlyList<Value>>();
        Assert.Equal(2, elems.Count);
        Assert.Equal(1, elems[0].As<int>());
        Assert.Equal(2, elems[1].As<int>());
    }

    [Fact]
    public void Array_ExtremeNegativeStartClampsToZero()
    {
        // -100 + 5 = -95 < 0; clamp to 0; slice arr 0 2 = [1, 2].
        // CONTEXT D-USER-D: extreme negatives clamp post-normalization (Python convention).
        var arr = MakeIntArray(1, 2, 3, 4, 5);
        var result = Collections.SliceArray(new[] { arr, Value.Int(-100), Value.Int(2) });
        var elems = result.As<IReadOnlyList<Value>>();
        Assert.Equal(2, elems.Count);
        Assert.Equal(1, elems[0].As<int>());
        Assert.Equal(2, elems[1].As<int>());
    }

    [Fact]
    public void Array_ExtremeNegativeEnd_ResultEmpty()
    {
        // 0 + 5 = 0 (start unchanged); -100 + 5 = -95 < 0; clamp end to 0.
        // s == e == 0 -> empty array (preserving ElementType).
        var arr = MakeIntArray(1, 2, 3, 4, 5);
        var result = Collections.SliceArray(new[] { arr, Value.Int(0), Value.Int(-100) });
        Assert.Empty(result.As<IReadOnlyList<Value>>());
    }

    [Fact]
    public void Array_PositiveCoincidence_NormalRange()
    {
        // Verification matrix coincidence row — positive-index path UNCHANGED.
        // Mirrors Phase14/SliceTests.Array_NormalRange exactly to gate regressions.
        var arr = MakeIntArray(1, 2, 3, 4, 5);
        var result = Collections.SliceArray(new[] { arr, Value.Int(1), Value.Int(4) });
        var elems = result.As<IReadOnlyList<Value>>();
        Assert.Equal(3, elems.Count);
        Assert.Equal(2, elems[0].As<int>());
        Assert.Equal(3, elems[1].As<int>());
        Assert.Equal(4, elems[2].As<int>());
    }

    [Fact]
    public void Array_PreservesElementType_OnNegativeSlice()
    {
        // Negative-index path must preserve ElementType per Phase 14 D-01 contract.
        var arr = MakeIntArray(1, 2, 3);
        var result = Collections.SliceArray(new[] { arr, Value.Int(-2), Value.Int(3) });
        Assert.True(result.Type is ArrayType, $"Expected ArrayType, got {result.Type}");
        var arrType = (ArrayType)result.Type;
        Assert.True(arrType.ElementType is IntType, $"Expected IntType element, got {arrType.ElementType}");
    }

    // ---- Sequence overload — DEFER-05 acceptance ----

    [Fact]
    public void Sequence_NegativeStart_FromEnd()
    {
        // 3-bar seq; -2 + 3 = 1; slice seq 1 3 = bars[1], bars[2] -> 2 bars.
        var seq = MakeThreeBarSequence();
        var result = Collections.SliceSequence(
            new[] { Value.Sequence(seq), Value.Int(-2), Value.Int(3) });
        Assert.Equal(2, result.As<SequenceData>().Bars.Count);
    }

    [Fact]
    public void Sequence_NegativeEnd_FromEnd()
    {
        // 3-bar seq; -1 + 3 = 2; slice seq 0 2 = bars[0], bars[1] -> 2 bars.
        var seq = MakeThreeBarSequence();
        var result = Collections.SliceSequence(
            new[] { Value.Sequence(seq), Value.Int(0), Value.Int(-1) });
        Assert.Equal(2, result.As<SequenceData>().Bars.Count);
    }
}
