namespace FlowLang.TypeSystem.PrimitiveTypes;

/// <summary>
/// Represents a 64-bit floating point number.
/// </summary>
public sealed class DoubleType : FlowType
{
    private DoubleType() { }

    public static DoubleType Instance { get; } = new();

    public override string Name => "Double";

    public override bool CanConvertTo(FlowType target)
    {
        // Double can convert to Number
        return target is NumberType
            || base.CanConvertTo(target);
    }

    public override int GetSpecificity() => 103;

    /// <summary>
    /// Phase 26.1 DICT-01: Double is value-equal and a natural Dict key (mirrors Float).
    /// </summary>
    public override bool IsHashable() => true;
}
