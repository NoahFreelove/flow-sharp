namespace FlowLang.TypeSystem.PrimitiveTypes;

/// <summary>
/// Represents a string of characters.
/// </summary>
public sealed class StringType : FlowType
{
    private StringType() { }

    public static StringType Instance { get; } = new();

    public override string Name => "String";

    public override int GetSpecificity() => 120;
}
