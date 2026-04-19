using FlowLang.Runtime;
using FlowLang.StandardLibrary;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using Xunit;

namespace FlowLang.Tests.Unit;

public class CollectionsTests
{
    [Fact]
    public void Init_EmptyArray_ThrowsInvalidOperationException()
    {
        var emptyArray = Value.Array(new List<Value>(), VoidType.Instance);
        var ex = Assert.Throws<InvalidOperationException>(
            () => Collections.Init(new[] { emptyArray }));
        Assert.Equal("Cannot get init of empty array", ex.Message);
    }

    [Fact]
    public void Init_SingleElementArray_ReturnsEmpty()
    {
        var arr = Value.Array(new List<Value> { Value.Int(42) }, IntType.Instance);
        var result = Collections.Init(new[] { arr });
        var elements = result.As<IReadOnlyList<Value>>();
        Assert.Empty(elements);
    }

    [Fact]
    public void Init_MultipleElements_ReturnsAllButLast()
    {
        var arr = Value.Array(
            new List<Value> { Value.Int(1), Value.Int(2), Value.Int(3) },
            IntType.Instance);
        var result = Collections.Init(new[] { arr });
        var elements = result.As<IReadOnlyList<Value>>();
        Assert.Equal(2, elements.Count);
        Assert.Equal(1, elements[0].As<int>());
        Assert.Equal(2, elements[1].As<int>());
    }
}
