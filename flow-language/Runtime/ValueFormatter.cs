using System.Numerics;
using FlowLang.TypeSystem.PrimitiveTypes;

namespace FlowLang.Runtime;

/// <summary>
/// The one formatting rule shared by <c>(str x)</c>, <c>print</c> and string
/// interpolation: strings and notes appear bare, doubles use 10 significant digits,
/// Void is <c>()</c>, and domain types with a literal form show it
/// (<see cref="TypeSystem.FlowType.Format"/>).
/// </summary>
public static class ValueFormatter
{
    /// <summary>A double with 10 significant digits, as Flow prints numbers.</summary>
    public static string Number(double value) => value.ToString("G10");

    public static string AutoStr(Value v)
    {
        if (v.Type is StringType) return v.As<string>();
        if (v.Type is IntType) return v.As<int>().ToString();
        if (v.Type is LongType) return v.As<long>().ToString();
        if (v.Type is FloatType or DoubleType) return v.ToString();
        if (v.Type is NumberType) return v.As<BigInteger>().ToString();
        if (v.Type is BoolType) return v.As<bool>() ? "true" : "false";
        if (v.Type is SymbolType) return "#" + v.As<string>();
        if (v.Type is VoidType) return "()";
        // Units and notes show their literal form; collections, sequences, chords and
        // reference-identity types use Value.ToString's description.
        return v.Type.Format(v) ?? v.ToString();
    }
}
