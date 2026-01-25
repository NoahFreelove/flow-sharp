using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;

namespace FlowLang.StandardLibrary;

public static class collections
{
    public static Value Head(IReadOnlyList<Value> args)
    {
        var arr = args[0];
        if (arr.Type is not ArrayType)
            throw new InvalidOperationException($"Expected Array, got {arr.Type}");

        var elements = arr.As<IReadOnlyList<Value>>();
        if (elements.Count == 0)
            return Value.Void();

        return elements[0];
    }

    public static Value Tail(IReadOnlyList<Value> args)
    {
        var arr = args[0];
        if (arr.Type is not ArrayType arrayType)
            throw new InvalidOperationException($"Expected Array, got {arr.Type}");

        var elements = arr.As<IReadOnlyList<Value>>();
        return Value.Array(elements.Skip(1).ToArray(), arrayType.ElementType);
    }
}