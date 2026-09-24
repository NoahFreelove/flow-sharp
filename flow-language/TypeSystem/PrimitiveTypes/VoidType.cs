namespace FlowLang.TypeSystem.PrimitiveTypes;

/// <summary>
/// Represents the Void type (no return value).
/// Also acts as a wildcard type for generic parameters.
/// </summary>
public sealed class VoidType : FlowType
{
    private VoidType() { }

    public static VoidType Instance { get; } = new();

    public override string Name => "Void";

    public override int GetSpecificity() => 0; // Void has lowest specificity

    public override bool IsCompatibleWith(FlowType target)
    {
        // Void type acts as a wildcard - any type is compatible with Void
        // This allows Void[] to match Int[], String[], etc.
        return true;
    }

    public override bool CanConvertTo(FlowType target)
    {
        // Void can convert to any type (acts as a wildcard)
        return true;
    }
}
