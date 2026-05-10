namespace FlowLang.TypeSystem.PrimitiveTypes;

/// <summary>
/// Represents a boolean value (true or false).
/// </summary>
public sealed class BoolType : FlowType
{
    private BoolType() { }

    public static BoolType Instance { get; } = new();

    public override string Name => "Bool";

    public override int GetSpecificity() => 115;
}
