namespace FlowLang.TypeSystem.PrimitiveTypes;

/// <summary>
/// Represents the type of a first-class function value (lambda/closure).
/// </summary>
public sealed class FunctionType : FlowType
{
    private FunctionType() { }
    public static FunctionType Instance { get; } = new();
    public override string Name => "Function";
    public override int GetSpecificity() => 50;
}
