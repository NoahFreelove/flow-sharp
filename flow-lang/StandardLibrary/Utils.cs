using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
using System.Numerics;

namespace FlowLang.StandardLibrary;

public static class Utils
{
    private static int FIXED_RAND_SEED = 0;
    private static int RAND_SEED = 0;
    private static Random? fixed_gen = null;

    private static Random? gen = null;
    private static readonly object _randLock = new();

    // People who use random noise in tracks may like a certain seed so they can use the
    // ?? operator to use the fixed gen and reset it at playback to get "consistent randomness"

    private static Random GetRand(bool fixed_rng = false)
    {
        lock (_randLock)
        {
            if (fixed_rng)
            {
                if (fixed_gen == null)
                {
                    if (FIXED_RAND_SEED == 0)
                    {
                        FIXED_RAND_SEED = Random.Shared.Next();
                    }

                    fixed_gen = new Random(FIXED_RAND_SEED);
                }

                return fixed_gen;
            }

            if (gen == null)
            {
                RAND_SEED = Random.Shared.Next();
                gen = new Random(RAND_SEED);
            }

            return gen;
        }
    }

    public static void ResetGen()
    {
        fixed_gen = new Random(FIXED_RAND_SEED);
    }

    public static void SetSeed(int seed)
    {
        FIXED_RAND_SEED = seed;
        ResetGen();
    }

    public static long Rand(bool fixed_rng = false)
    {
        return GetRand(fixed_rng).NextInt64();
    }

    public static int IRand(bool fixed_rng = false)
    {
        return GetRand(fixed_rng).Next();
    }

    public static float FRand(bool fixed_rng = false)
    {
        return GetRand(fixed_rng).NextSingle();
    }

    public static double DRand(bool fixed_rng = false)
    {
        return GetRand(fixed_rng).NextDouble();
    }

    public static bool BRand(bool fixed_rng = false)
    {
        return GetRand(fixed_rng).NextSingle() < 0.5f;
    }

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
}