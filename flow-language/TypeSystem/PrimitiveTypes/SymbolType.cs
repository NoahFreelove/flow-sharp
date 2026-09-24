namespace FlowLang.TypeSystem.PrimitiveTypes;

/// <summary>
/// Symbol primitive type. <c>#foo</c> literals are interned via
/// <see cref="FlowLang.Runtime.ExecutionContext.SymbolInternTable"/> for pointer-equality
/// semantics. Strict separation from <see cref="StringType"/> per Phase 26.1 CONTEXT —
/// <c>(equals #foo "foo")</c> returns <c>false</c>.
/// </summary>
public sealed class SymbolType : FlowType
{
    private SymbolType() { }

    public static SymbolType Instance { get; } = new();

    public override string Name => "Symbol";

    public override int GetSpecificity() => 125;

    public override bool IsHashable() => true;
}
