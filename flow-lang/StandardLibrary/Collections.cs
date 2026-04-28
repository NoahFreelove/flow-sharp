using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
using ExecutionContext = FlowLang.Runtime.ExecutionContext;

namespace FlowLang.StandardLibrary;

public static class Collections
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
            throw new InvalidOperationException("Cannot get head of empty array");

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
            throw new InvalidOperationException("Cannot get last of empty array");

        return elements[^1];
    }

    public static Value Init(IReadOnlyList<Value> args)
    {
        var arr = args[0];
        if (arr.Type is not ArrayType arrayType)
            throw new InvalidOperationException($"Expected Array, got {arr.Type}");

        var elements = arr.As<IReadOnlyList<Value>>();
        if (elements.Count == 0)
            throw new InvalidOperationException("Cannot get init of empty array");

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
        if (count < 0) count = 0;
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
        if (count < 0) count = 0;
        return Value.Array(elements.Skip(count).ToArray(), arrayType.ElementType);
    }

    /// <summary>
    /// DEFER-01 (Phase 20): range(start, end) and range(start, end, step) -> Array[Int].
    /// Standard Pythonic semantics — start inclusive, end exclusive, default step=1, negative
    /// step iterates backward, empty array when range is unsatisfiable. step==0 is undefined
    /// and throws.
    /// </summary>
    public static Value Range(IReadOnlyList<Value> args)
    {
        if (args[0].Type is not IntType)
            throw new InvalidOperationException($"Expected Int, got {args[0].Type}");
        if (args[1].Type is not IntType)
            throw new InvalidOperationException($"Expected Int, got {args[1].Type}");

        int start = args[0].As<int>();
        int end = args[1].As<int>();
        int step = 1;
        if (args.Count >= 3)
        {
            if (args[2].Type is not IntType)
                throw new InvalidOperationException($"Expected Int, got {args[2].Type}");
            step = args[2].As<int>();
        }

        if (step == 0)
            throw new InvalidOperationException("range step cannot be zero");

        var result = new List<Value>();
        if (step > 0)
        {
            for (int i = start; i < end; i += step)
                result.Add(Value.Int(i));
        }
        else
        {
            for (int i = start; i > end; i += step)
                result.Add(Value.Int(i));
        }
        return Value.Array(result, IntType.Instance);
    }

    /// <summary>
    /// DX-05 (Phase 14) + DEFER-05 (Phase 20 plan 20-03): returns a sub-array from start
    /// (inclusive) to end (exclusive). Negative indices are interpreted Python-style as
    /// from-end (-1 means last, -2 means second-to-last, etc.). Out-of-range indices
    /// (still negative or > count after normalization) clamp silently per Phase 14 D-01
    /// tradition AND D-USER-D extreme-negative clamp policy. start >= end (post-clamp)
    /// returns an empty array (preserving ElementType).
    /// </summary>
    public static Value SliceArray(IReadOnlyList<Value> args)
    {
        var arr = args[0];
        var startVal = args[1];
        var endVal = args[2];

        if (arr.Type is not ArrayType arrayType)
            throw new InvalidOperationException($"Expected Array, got {arr.Type}");
        if (startVal.Type is not IntType)
            throw new InvalidOperationException($"Expected Int, got {startVal.Type}");
        if (endVal.Type is not IntType)
            throw new InvalidOperationException($"Expected Int, got {endVal.Type}");

        var elements = arr.As<IReadOnlyList<Value>>();
        int count = elements.Count;
        // DEFER-05: normalize negative indices Python-style (count + idx) BEFORE clamp.
        int rawStart = startVal.As<int>();
        int rawEnd   = endVal.As<int>();
        int normStart = rawStart < 0 ? rawStart + count : rawStart;
        int normEnd   = rawEnd   < 0 ? rawEnd   + count : rawEnd;
        // Phase 14 D-01 silent-clamp tradition preserved post-normalization (D-USER-D).
        int s = Math.Clamp(normStart, 0, count);
        int e = Math.Clamp(normEnd,   0, count);
        if (s >= e)
            return Value.Array(Array.Empty<Value>(), arrayType.ElementType);
        return Value.Array(elements.Skip(s).Take(e - s).ToArray(), arrayType.ElementType);
    }

    /// <summary>
    /// DX-05 (Phase 14) + DEFER-05 (Phase 20 plan 20-03): returns a sub-sequence
    /// containing bars [start, end). Negative indices are interpreted Python-style
    /// as from-end (mirrors SliceArray normalization shape). Out-of-range indices
    /// clamp silently per Phase 14 D-01 + D-USER-D extreme-negative policy. Each
    /// retained bar is appended via SequenceData.AddBar, preserving the
    /// musical-bar invariant (Mode == Musical, TimeSignature != null).
    /// </summary>
    public static Value SliceSequence(IReadOnlyList<Value> args)
    {
        var seq = args[0].As<SequenceData>();
        if (args[1].Type is not IntType)
            throw new InvalidOperationException($"Expected Int, got {args[1].Type}");
        if (args[2].Type is not IntType)
            throw new InvalidOperationException($"Expected Int, got {args[2].Type}");

        int count = seq.Bars.Count;
        // DEFER-05: normalize negative indices Python-style (count + idx) BEFORE clamp.
        int rawStart = args[1].As<int>();
        int rawEnd   = args[2].As<int>();
        int normStart = rawStart < 0 ? rawStart + count : rawStart;
        int normEnd   = rawEnd   < 0 ? rawEnd   + count : rawEnd;
        // Phase 14 D-01 silent-clamp tradition preserved post-normalization (D-USER-D).
        int s = Math.Clamp(normStart, 0, count);
        int e = Math.Clamp(normEnd,   0, count);
        if (s >= e)
            return Value.Sequence(new SequenceData());

        var result = new SequenceData();
        for (int i = s; i < e; i++)
            result.AddBar(seq.Bars[i]);
        return Value.Sequence(result);
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

    // ===== Higher-Order Functions =====

    private static Value InvokeCallback(ExecutionContext context, FunctionOverload callback, List<Value> args)
    {
        if (callback.IsInternal) return callback.Implementation!(args);
        return context.Invoker!.ExecuteUserFunctionWithCaptures(callback.Declaration!, args, callback.CapturedVariables);
    }

    public static Value Each(IReadOnlyList<Value> args, ExecutionContext context)
    {
        var arr = args[0].As<IReadOnlyList<Value>>();
        var callback = args[1].As<FunctionOverload>();

        foreach (var element in arr)
        {
            InvokeCallback(context, callback, new List<Value> { element });
        }

        return Value.Void();
    }

    public static Value Map(IReadOnlyList<Value> args, ExecutionContext context)
    {
        var arr = args[0].As<IReadOnlyList<Value>>();
        var callback = args[1].As<FunctionOverload>();

        var results = new List<Value>();
        foreach (var element in arr)
        {
            results.Add(InvokeCallback(context, callback, new List<Value> { element }));
        }

        if (results.Count == 0)
            return Value.Array(results, VoidType.Instance);

        var elementType = results[0].Type;
        if (!results.All(r => r.Type.Equals(elementType)))
            elementType = VoidType.Instance;

        return Value.Array(results, elementType);
    }

    public static Value Filter(IReadOnlyList<Value> args, ExecutionContext context)
    {
        var arr = args[0].As<IReadOnlyList<Value>>();
        var callback = args[1].As<FunctionOverload>();

        var results = new List<Value>();
        foreach (var element in arr)
        {
            var result = InvokeCallback(context, callback, new List<Value> { element });
            if (result.As<bool>())
                results.Add(element);
        }

        if (results.Count == 0)
            return Value.Array(results, VoidType.Instance);

        var elementType = results[0].Type;
        return Value.Array(results, elementType);
    }

    public static Value Reduce(IReadOnlyList<Value> args, ExecutionContext context)
    {
        var arr = args[0].As<IReadOnlyList<Value>>();
        var initial = args[1];
        var callback = args[2].As<FunctionOverload>();

        var accumulator = initial;
        foreach (var element in arr)
        {
            accumulator = InvokeCallback(context, callback, new List<Value> { accumulator, element });
        }

        return accumulator;
    }

    private static bool ValueEquals(Value a, Value b)
    {
        if (!a.Type.Equals(b.Type))
            return false;

        return a.Type switch
        {
            IntType => a.As<int>() == b.As<int>(),
            FloatType => Math.Abs(a.As<double>() - b.As<double>()) < 1e-9,
            DoubleType => Math.Abs(a.As<double>() - b.As<double>()) < 1e-9,
            BoolType => a.As<bool>() == b.As<bool>(),
            StringType => a.As<string>() == b.As<string>(),
            _ => ReferenceEquals(a.Data, b.Data)
        };
    }
}