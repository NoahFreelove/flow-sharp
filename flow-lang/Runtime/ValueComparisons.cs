using System.Numerics;

namespace FlowLang.Runtime;

/// <summary>
/// How domain values take part in numeric comparison and equality. Domain layers
/// register, when installed:
/// <list type="bullet">
///   <item>a numeric view: which of their values compare as plain numbers
///   (a semitone count, a decibel level), and</item>
///   <item>a common scale: pairs of values of different types that measure the same
///   quantity and compare after conversion (<c>(equals 1000ms 1s)</c>).</item>
/// </list>
/// Each function returns null for values it does not handle.
/// </summary>
public static class ValueComparisons
{
    private static readonly List<Func<Value, (double Double, BigInteger? Whole)?>> _numericViews = new();
    private static readonly List<Func<Value, Value, (double A, double B)?>> _commonScales = new();
    private static readonly object _lock = new();

    public static void RegisterNumericView(Func<Value, (double Double, BigInteger? Whole)?> view)
    {
        lock (_lock) _numericViews.Add(view);
    }

    public static void RegisterCommonScale(Func<Value, Value, (double A, double B)?> scale)
    {
        lock (_lock) _commonScales.Add(scale);
    }

    /// <summary>The value as a comparable number, when a domain type provides one.</summary>
    public static (double Double, BigInteger? Whole)? NumericView(Value value)
    {
        foreach (var view in Snapshot(_numericViews))
            if (view(value) is { } number)
                return number;
        return null;
    }

    /// <summary>Both values on one scale, when they measure the same quantity.</summary>
    public static (double A, double B)? CommonScale(Value a, Value b)
    {
        foreach (var scale in Snapshot(_commonScales))
            if (scale(a, b) is { } pair)
                return pair;
        return null;
    }

    private static T[] Snapshot<T>(List<T> list)
    {
        lock (_lock) return list.ToArray();
    }
}
