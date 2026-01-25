namespace FlowLang.TypeSystem.PrimitiveTypes;

/// <summary>
/// Represents a 32-bit floating point number.
/// </summary>
public sealed class FloatType : FlowType
{
    private FloatType() { }

    public static FloatType Instance { get; } = new();

    public override string Name => "Float";

    public override bool CanConvertTo(FlowType target)
    {
        // Float can convert to Double, Number
        return target is DoubleType or NumberType
            || base.CanConvertTo(target);
    }

    public override int GetSpecificity() => 105;
}
