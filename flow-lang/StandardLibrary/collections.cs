using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;

namespace FlowLang.StandardLibrary;

public static class collections
{
    // ===== Array Functions =====

    /// <summary>
    /// Creates an array from variable arguments.
    /// If all elements have the same type, uses that type.
    /// If elements have different types, uses Void[] (mixed-type array).
    /// </summary>
    public static Value List(IReadOnlyList<Value> args)
    {
        if (args.Count == 0)
            return Value.Array([], VoidType.Instance);

        // Check if all elements have the same type
        var firstType = args[0].Type;
        bool allSameType = true;

        for (int i = 1; i < args.Count; i++)
        {
            if (!args[i].Type.Equals(firstType))
            {
                allSameType = false;
                break;
            }
        }
        // Use the common type if all are the same, otherwise use Void for mixed types
        var elementType = allSameType ? firstType : VoidType.Instance;
        return Value.Array(args.ToList(), elementType);
    }
    
    /// <summary>
    /// Returns the length of an array.
    /// </summary>
    public static Value Len(IReadOnlyList<Value> args)
    {
        var arr = args[0].As<IReadOnlyList<Value>>();
        return Value.Int(arr.Count);
    }
    
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

    public static Value Last(IReadOnlyList<Value> args)
    {
        var arr = args[0];
        if (arr.Type is not ArrayType)
            throw new InvalidOperationException($"Expected Array, got {arr.Type}");

        var elements = arr.As<IReadOnlyList<Value>>();
        if (elements.Count == 0)
            return Value.Void();

        return elements[^1];
    }

    public static Value Init(IReadOnlyList<Value> args)
    {
        var arr = args[0];
        if (arr.Type is not ArrayType arrayType)
            throw new InvalidOperationException($"Expected Array, got {arr.Type}");

        var elements = arr.As<IReadOnlyList<Value>>();
        return Value.Array(elements.Take(elements.Count - 1).ToArray(), arrayType.ElementType);
    }

    public static Value Empty(IReadOnlyList<Value> args)
    {
        var arr = args[0];
        if (arr.Type is not ArrayType)
            throw new InvalidOperationException($"Expected Array, got {arr.Type}");

        var elements = arr.As<IReadOnlyList<Value>>();
        return Value.Bool(elements.Count == 0);
    }

    public static Value Reverse(IReadOnlyList<Value> args)
    {
        var arr = args[0];
        if (arr.Type is not ArrayType arrayType)
            throw new InvalidOperationException($"Expected Array, got {arr.Type}");

        var elements = arr.As<IReadOnlyList<Value>>();
        return Value.Array(elements.Reverse().ToArray(), arrayType.ElementType);
    }

    public static Value Take(IReadOnlyList<Value> args)
    {
        var arr = args[0];
        var n = args[1];

        if (arr.Type is not ArrayType arrayType)
            throw new InvalidOperationException($"Expected Array, got {arr.Type}");
        if (n.Type is not IntType)
            throw new InvalidOperationException($"Expected Int, got {n.Type}");

        var elements = arr.As<IReadOnlyList<Value>>();
        var count = n.As<int>();
        return Value.Array(elements.Take(count).ToArray(), arrayType.ElementType);
    }

    public static Value Drop(IReadOnlyList<Value> args)
    {
        var arr = args[0];
        var n = args[1];

        if (arr.Type is not ArrayType arrayType)
            throw new InvalidOperationException($"Expected Array, got {arr.Type}");
        if (n.Type is not IntType)
            throw new InvalidOperationException($"Expected Int, got {n.Type}");

        var elements = arr.As<IReadOnlyList<Value>>();
        var count = n.As<int>();
        return Value.Array(elements.Skip(count).ToArray(), arrayType.ElementType);
    }

    public static Value Append(IReadOnlyList<Value> args)
    {
        var arr = args[0];
        var element = args[1];

        if (arr.Type is not ArrayType arrayType)
            throw new InvalidOperationException($"Expected Array, got {arr.Type}");

        var elements = arr.As<IReadOnlyList<Value>>();
        var newElements = new List<Value>(elements) { element };
        return Value.Array(newElements.ToArray(), arrayType.ElementType);
    }

    public static Value Prepend(IReadOnlyList<Value> args)
    {
        var element = args[0];
        var arr = args[1];

        if (arr.Type is not ArrayType arrayType)
            throw new InvalidOperationException($"Expected Array, got {arr.Type}");

        var elements = arr.As<IReadOnlyList<Value>>();
        var newElements = new List<Value> { element };
        newElements.AddRange(elements);
        return Value.Array(newElements.ToArray(), arrayType.ElementType);
    }

    public static Value Concat(IReadOnlyList<Value> args)
    {
        var arr1 = args[0];
        var arr2 = args[1];

        if (arr1.Type is not ArrayType arrayType1)
            throw new InvalidOperationException($"Expected Array, got {arr1.Type}");
        if (arr2.Type is not ArrayType)
            throw new InvalidOperationException($"Expected Array, got {arr2.Type}");

        var elements1 = arr1.As<IReadOnlyList<Value>>();
        var elements2 = arr2.As<IReadOnlyList<Value>>();
        var combined = elements1.Concat(elements2).ToArray();
        return Value.Array(combined, arrayType1.ElementType);
    }

    public static Value Contains(IReadOnlyList<Value> args)
    {
        var arr = args[0];
        var searchElement = args[1];

        if (arr.Type is not ArrayType)
            throw new InvalidOperationException($"Expected Array, got {arr.Type}");

        var elements = arr.As<IReadOnlyList<Value>>();
        foreach (var element in elements)
        {
            if (ValueEquals(element, searchElement))
                return Value.Bool(true);
        }
        return Value.Bool(false);
    }

    private static bool ValueEquals(Value a, Value b)
    {
        if (!a.Type.Equals(b.Type))
            return false;

        return a.Type switch
        {
            IntType => a.As<int>() == b.As<int>(),
            FloatType => Math.Abs(a.As<float>() - b.As<float>()) < float.Epsilon,
            DoubleType => Math.Abs(a.As<double>() - b.As<double>()) < double.Epsilon,
            BoolType => a.As<bool>() == b.As<bool>(),
            StringType => a.As<string>() == b.As<string>(),
            _ => ReferenceEquals(a.Data, b.Data)
        };
    }
}