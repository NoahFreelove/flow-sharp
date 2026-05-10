namespace FlowLang.TypeSystem.PrimitiveTypes;

/// <summary>
/// Represents an envelope type for amplitude shaping.
/// </summary>
public sealed class EnvelopeType : FlowType
{
    private EnvelopeType() { }

    public static EnvelopeType Instance { get; } = new();

    public override string Name => "Envelope";

    public override int GetSpecificity() => 138;
}
