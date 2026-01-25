namespace FlowLang.TypeSystem.PrimitiveTypes;

/// <summary>
/// Represents a 64-bit signed integer.
/// </summary>
public sealed class LongType : FlowType
{
    private LongType() { }

    public static LongType Instance { get; } = new();

    public override string Name => "Long";

    public override bool CanConvertTo(FlowType target)
    {
        // Long can convert to Double, Number
        return target is DoubleType or NumberType
            || base.CanConvertTo(target);
    }

    public override int GetSpecificity() => 108;
}
