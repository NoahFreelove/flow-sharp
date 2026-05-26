using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
using System.Numerics;

namespace FlowLang.StandardLibrary;

public static class Utils
{
    // ===== Comparison and Equality Helpers =====

    /// <summary>
    /// Converts a Value to a comparable numeric representation.
    /// Returns (IsNumeric, DoubleValue, BigIntValue)
    /// </summary>
    public static (bool IsNumeric, double DoubleValue, BigInteger? BigIntValue) ToComparableNumber(Value value)
    {
        return value.Type switch
        {
            IntType => (true, value.As<int>(), new BigInteger(value.As<int>())),
            LongType => (true, value.As<long>(), new BigInteger(value.As<long>())),
            FloatType => (true, value.As<double>(), null),
            DoubleType => (true, value.As<double>(), null),
            NumberType => (true, (double)value.As<BigInteger>(), value.As<BigInteger>()),
            SemitoneType => (true, value.As<int>(), new BigInteger(value.As<int>())),
            CentType => (true, value.As<double>(), null),
            MillisecondType => (true, value.As<double>(), null),
            SecondType => (true, value.As<double>(), null),
            DecibelType => (true, value.As<double>(), null),
            _ => (false, 0, null)
        };
    }

    /// <summary>
    /// Compares two numeric values.
    /// Returns -1 if a < b, 0 if a == b, 1 if a > b.
    /// Throws InvalidOperationException if either value is not numeric.
    /// </summary>
    public static int CompareNumeric(Value a, Value b)
    {
        var (aIsNumeric, aDouble, aBigInt) = ToComparableNumber(a);
        var (bIsNumeric, bDouble, bBigInt) = ToComparableNumber(b);

        if (!aIsNumeric)
            throw new InvalidOperationException($"Cannot compare non-numeric type: {a.Type}");
        if (!bIsNumeric)
            throw new InvalidOperationException($"Cannot compare non-numeric type: {b.Type}");

        // If both have BigInteger representations (whole numbers), use BigInteger comparison
        if (aBigInt.HasValue && bBigInt.HasValue)
        {
            return aBigInt.Value.CompareTo(bBigInt.Value);
        }

        // Otherwise, use double comparison
        return aDouble.CompareTo(bDouble);
    }

    /// <summary>
    /// Implements loose equality with type conversion (like JavaScript ==).
    /// </summary>
    public static bool LooseEquals(Value a, Value b)
    {
        // If same type, delegate to strict equals
        if (a.Type.Equals(b.Type))
            return StrictEquals(a, b);

        // Try numeric comparison
        var (aIsNumeric, _, _) = ToComparableNumber(a);
        var (bIsNumeric, _, _) = ToComparableNumber(b);

        if (aIsNumeric && bIsNumeric)
        {
            return CompareNumeric(a, b) == 0;
        }

        // Both void
        if (a.Type is VoidType && b.Type is VoidType)
            return true;

        // Different types, not both numeric
        return false;
    }

    /// <summary>
    /// Implements strict equality (like JavaScript ===).
    /// Both type and value must match.
    /// </summary>
    public static bool StrictEquals(Value a, Value b)
    {
        // Type must match
        if (!a.Type.Equals(b.Type))
            return false;

        // Both void
        if (a.Type is VoidType)
            return true;

        // Primitive types
        if (a.Data is int aInt && b.Data is int bInt)
            return aInt == bInt;
        if (a.Data is long aLong && b.Data is long bLong)
            return aLong == bLong;
        if (a.Data is double aDouble && b.Data is double bDouble)
            return aDouble == bDouble;
        if (a.Data is bool aBool && b.Data is bool bBool)
            return aBool == bBool;
        if (a.Data is string aStr && b.Data is string bStr)
            return aStr == bStr;
        if (a.Data is BigInteger aBigInt && b.Data is BigInteger bBigInt)
            return aBigInt == bBigInt;

        // Arrays - recursive comparison
        if (a.Data is IReadOnlyList<Value> aArr && b.Data is IReadOnlyList<Value> bArr)
        {
            if (aArr.Count != bArr.Count)
                return false;

            for (int i = 0; i < aArr.Count; i++)
            {
                if (!StrictEquals(aArr[i], bArr[i]))
                    return false;
            }
            return true;
        }

        // Default: use object equality
        return Equals(a.Data, b.Data);
    }

    /// <summary>
    /// Phase 44 Plan 44-09 Task 2 — strict-mode wrapper around
    /// <see cref="LooseEquals"/>. When <paramref name="ctx"/>'s
    /// <see cref="ExecutionContext.CallerStrictMode"/> is true and the two
    /// values have DIFFERENT types, return <c>false</c> directly — D-11
    /// set-theoretic equality (<c>(equals 1 1.0)</c> → <c>false</c> in
    /// strict; "1 is not 1.0 — different types" is a defensible answer).
    /// Per RESEARCH Open Question 1 Option (b), the non-strict path is
    /// UNCHANGED — <see cref="LooseEquals"/> retains the JS-style numeric
    /// coercion at line 73-76 so <c>(equals 1 1.0)</c> non-strict returns
    /// <c>true</c>. Same-type comparisons in BOTH modes route through
    /// <see cref="StrictEquals"/> via <see cref="LooseEquals"/>.
    /// </summary>
    public static bool LooseEqualsStrict(Value a, Value b, FlowLang.Runtime.ExecutionContext ctx)
    {
        if (ctx.CallerStrictMode && !a.Type.Equals(b.Type))
        {
            // D-11 set-theoretic: cross-type strict equality returns false
            // (NOT error — equality has a defensible answer where ordering
            // does not; see CrossTypeComparisonStrictTests for asymmetry).
            return false;
        }
        return LooseEquals(a, b);
    }
}