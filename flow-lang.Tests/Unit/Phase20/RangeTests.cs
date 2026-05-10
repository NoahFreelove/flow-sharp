using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase20;

/// <summary>
/// DEFER-01 regression: range(Int, Int) and range(Int, Int, Int) -> Array[Int].
/// Standard Pythonic semantics — start inclusive, end exclusive, default step=1,
/// negative step iterates backward, empty array when range is unsatisfiable.
/// step==0 is undefined and throws InvalidOperationException.
/// Direct C# dispatch via Collections.Range bypasses the parser per 20-RESEARCH Pitfall 4
/// (negative-literal binary-subtraction ambiguity at script-level).
/// </summary>
public class RangeTests
{
    [Fact]
    public void TwoArg_DefaultStep()
    {
        var result = Collections.Range(new[] { Value.Int(0), Value.Int(5) });
        var elems = result.As<IReadOnlyList<Value>>();
        Assert.Equal(5, elems.Count);
        Assert.Equal(0, elems[0].As<int>());
        Assert.Equal(1, elems[1].As<int>());
        Assert.Equal(2, elems[2].As<int>());
        Assert.Equal(3, elems[3].As<int>());
        Assert.Equal(4, elems[4].As<int>());
    }

    [Fact]
    public void ThreeArg_PositiveStep()
    {
        var result = Collections.Range(new[] { Value.Int(0), Value.Int(10), Value.Int(2) });
        var elems = result.As<IReadOnlyList<Value>>();
        Assert.Equal(5, elems.Count);
        Assert.Equal(0, elems[0].As<int>());
        Assert.Equal(2, elems[1].As<int>());
        Assert.Equal(4, elems[2].As<int>());
        Assert.Equal(6, elems[3].As<int>());
        Assert.Equal(8, elems[4].As<int>());
    }

    [Fact]
    public void NegativeStep_IteratesBackward()
    {
        var result = Collections.Range(new[] { Value.Int(5), Value.Int(0), Value.Int(-1) });
        var elems = result.As<IReadOnlyList<Value>>();
        Assert.Equal(5, elems.Count);
        Assert.Equal(5, elems[0].As<int>());
        Assert.Equal(4, elems[1].As<int>());
        Assert.Equal(3, elems[2].As<int>());
        Assert.Equal(2, elems[3].As<int>());
        Assert.Equal(1, elems[4].As<int>());
    }

    [Fact]
    public void EmptyWhenStartEqualsEnd()
    {
        var result = Collections.Range(new[] { Value.Int(3), Value.Int(3) });
        var elems = result.As<IReadOnlyList<Value>>();
        Assert.Empty(elems);
    }

    [Fact]
    public void UnsatisfiableWithDefaultStepReturnsEmpty()
    {
        // start > end with implicit step=+1 is unsatisfiable — empty result.
        var result = Collections.Range(new[] { Value.Int(5), Value.Int(0) });
        var elems = result.As<IReadOnlyList<Value>>();
        Assert.Empty(elems);
    }

    [Fact]
    public void ZeroStepThrows()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Collections.Range(new[] { Value.Int(0), Value.Int(5), Value.Int(0) }));
        Assert.Contains("range step cannot be zero", ex.Message);
    }

    [Fact]
    public void PreservesElementTypeIsInt()
    {
        var result = Collections.Range(new[] { Value.Int(0), Value.Int(3) });
        Assert.True(result.Type is ArrayType, $"Expected ArrayType, got {result.Type}");
        var arrType = (ArrayType)result.Type;
        Assert.True(arrType.ElementType is IntType, $"Expected IntType element, got {arrType.ElementType}");
    }

    [Fact]
    public void NegativeStep_DescendingPath()
    {
        // Gates the step<0 loop branch with a multi-step descent.
        var result = Collections.Range(new[] { Value.Int(10), Value.Int(0), Value.Int(-2) });
        var elems = result.As<IReadOnlyList<Value>>();
        Assert.Equal(5, elems.Count);
        Assert.Equal(10, elems[0].As<int>());
        Assert.Equal(8, elems[1].As<int>());
        Assert.Equal(6, elems[2].As<int>());
        Assert.Equal(4, elems[3].As<int>());
        Assert.Equal(2, elems[4].As<int>());
    }
}
