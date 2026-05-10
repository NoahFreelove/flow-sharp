using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase14;

/// <summary>
/// DX-05 regression: slice(Array[T], Int, Int) and slice(Sequence, Int, Int).
/// Silent two-sided clamping per Phase 14 CONTEXT D-01.
/// Observable-value pins (Phase 13 D-11): numeric .Count on Array / Bars, typed element round-trip.
/// </summary>
public class SliceTests
{
    private static Value MakeIntArray(params int[] xs)
    {
        var elems = new List<Value>();
        foreach (var x in xs) elems.Add(Value.Int(x));
        return Value.Array(elems, IntType.Instance);
    }

    // ---- Array[T] overload ----

    [Fact]
    public void Array_NormalRange()
    {
        var arr = MakeIntArray(1, 2, 3, 4, 5);
        var result = Collections.SliceArray(new[] { arr, Value.Int(1), Value.Int(4) });
        var elems = result.As<IReadOnlyList<Value>>();
        Assert.Equal(3, elems.Count);
        Assert.Equal(2, elems[0].As<int>());
        Assert.Equal(3, elems[1].As<int>());
        Assert.Equal(4, elems[2].As<int>());
    }

    [Fact]
    public void Array_NegativeStartClamps()
    {
        var arr = MakeIntArray(1, 2, 3, 4, 5);
        var result = Collections.SliceArray(new[] { arr, Value.Int(-5), Value.Int(2) });
        var elems = result.As<IReadOnlyList<Value>>();
        Assert.Equal(2, elems.Count);
        Assert.Equal(1, elems[0].As<int>());
        Assert.Equal(2, elems[1].As<int>());
    }

    [Fact]
    public void Array_EndExceedsCountClamps()
    {
        var arr = MakeIntArray(1, 2, 3, 4, 5);
        var result = Collections.SliceArray(new[] { arr, Value.Int(3), Value.Int(100) });
        var elems = result.As<IReadOnlyList<Value>>();
        Assert.Equal(2, elems.Count);
        Assert.Equal(4, elems[0].As<int>());
        Assert.Equal(5, elems[1].As<int>());
    }

    [Fact]
    public void Array_InvertedRangeEmpty()
    {
        var arr = MakeIntArray(1, 2, 3, 4, 5);
        var result = Collections.SliceArray(new[] { arr, Value.Int(3), Value.Int(2) });
        Assert.Empty(result.As<IReadOnlyList<Value>>());
    }

    [Fact]
    public void Array_StartEqualsEndEmpty()
    {
        var arr = MakeIntArray(1, 2, 3, 4, 5);
        var result = Collections.SliceArray(new[] { arr, Value.Int(2), Value.Int(2) });
        Assert.Empty(result.As<IReadOnlyList<Value>>());
    }

    [Fact]
    public void Array_PreservesElementType()
    {
        var arr = MakeIntArray(1, 2, 3);
        var result = Collections.SliceArray(new[] { arr, Value.Int(0), Value.Int(2) });
        Assert.True(result.Type is ArrayType, $"Expected ArrayType, got {result.Type}");
        var arrType = (ArrayType)result.Type;
        Assert.True(arrType.ElementType is IntType, $"Expected IntType element, got {arrType.ElementType}");
    }

    // ---- Sequence overload ----
    //
    // Build SequenceData via AddBar(new BarData(musicalNotes, TimeSignatureData)).
    // AddBar requires Mode == Musical and TimeSignature != null (SequenceType.cs:32-41).
    // Observable pin: result.Bars.Count — numeric, per CONTEXT D-11 discipline.

    private static SequenceData MakeThreeBarSequence()
    {
        var ts = new TimeSignatureData(4, 4);
        var seq = new SequenceData();
        // Each bar is a musical bar with no notes — valid under the AddBar invariant
        // which only checks Mode/TimeSignature. Empty MusicalNotes is acceptable.
        seq.AddBar(new BarData(new List<MusicalNoteData>(), ts));
        seq.AddBar(new BarData(new List<MusicalNoteData>(), ts));
        seq.AddBar(new BarData(new List<MusicalNoteData>(), ts));
        return seq;
    }

    [Fact]
    public void Sequence_ReturnsCorrectBarCount()
    {
        var seq = MakeThreeBarSequence();
        var result = Collections.SliceSequence(
            new[] { Value.Sequence(seq), Value.Int(1), Value.Int(3) });
        Assert.Equal(2, result.As<SequenceData>().Bars.Count);
    }

    [Fact]
    public void Sequence_NegativeStartClamps()
    {
        var seq = MakeThreeBarSequence();
        var result = Collections.SliceSequence(
            new[] { Value.Sequence(seq), Value.Int(-5), Value.Int(2) });
        Assert.Equal(2, result.As<SequenceData>().Bars.Count);
    }

    [Fact]
    public void Sequence_InvertedRangeEmpty()
    {
        var seq = MakeThreeBarSequence();
        var result = Collections.SliceSequence(
            new[] { Value.Sequence(seq), Value.Int(2), Value.Int(1) });
        Assert.Empty(result.As<SequenceData>().Bars);
    }
}
