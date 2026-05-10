namespace FlowLang.TypeSystem.PrimitiveTypes;

/// <summary>
/// Represents a 32-bit signed integer.
/// </summary>
public sealed class IntType : FlowType
{
    private IntType() { }

    public static IntType Instance { get; } = new();

    public override string Name => "Int";

    public override bool CanConvertTo(FlowType target)
    {
        // Int can convert to Long, Float, Double, Number, NoteValue (int-backed enum)
        return target is LongType or FloatType or DoubleType or NumberType
            or SpecialTypes.NoteValueType
            || base.CanConvertTo(target);
    }

    public override int GetSpecificity() => 110;
}
