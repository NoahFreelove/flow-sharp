using System.Numerics;

namespace FlowLang.TypeSystem.PrimitiveTypes;

/// <summary>
/// Represents an arbitrary-precision number (BigInteger).
/// </summary>
public sealed class NumberType : FlowType
{
    private NumberType() { }

    public static NumberType Instance { get; } = new();

    public override string Name => "Number";

    public override int GetSpecificity() => 90; // Lowest specificity among numeric types
}
