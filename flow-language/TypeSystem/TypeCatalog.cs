using System.Collections.Concurrent;
using FlowLang.TypeSystem.PrimitiveTypes;

namespace FlowLang.TypeSystem;

/// <summary>
/// Binds type names from annotations to types. The language registers its own types;
/// domain layers (music, audio, network) register theirs when they are installed. A
/// grammar type name with no binding parses to <see cref="UnresolvedType"/>, so syntax
/// never depends on which domain layers a host includes.
/// </summary>
public sealed class TypeCatalog
{
    private readonly ConcurrentDictionary<string, FlowType> _types = new(StringComparer.Ordinal);

    /// <summary>The process-wide catalog of installed types.</summary>
    public static TypeCatalog Default { get; } = CreateWithLanguageTypes();

    private static TypeCatalog CreateWithLanguageTypes()
    {
        var catalog = new TypeCatalog();
        catalog.Register("Void", VoidType.Instance);
        catalog.Register("Int", IntType.Instance);
        catalog.Register("Float", FloatType.Instance);
        catalog.Register("Long", LongType.Instance);
        catalog.Register("Double", DoubleType.Instance);
        catalog.Register("String", StringType.Instance);
        catalog.Register("Bool", BoolType.Instance);
        catalog.Register("Number", NumberType.Instance);
        catalog.Register("Symbol", SymbolType.Instance);
        catalog.Register("Function", FunctionType.Instance);
        catalog.Register("Buf", Parsing.BufType.Instance);
        return catalog;
    }

    public void Register(string name, FlowType type) => _types[name] = type;

    public FlowType? TryResolve(string name) => _types.TryGetValue(name, out var type) ? type : null;

    /// <summary>The bound type, or an <see cref="UnresolvedType"/> placeholder.</summary>
    public FlowType Resolve(string name) => TryResolve(name) ?? new UnresolvedType(name);
}

/// <summary>
/// A grammar type name no installed layer defines (for example a music type in a
/// language-only host). It parses, but nothing is compatible with it, so binding a
/// value to it reports an error naming the type.
/// </summary>
public sealed class UnresolvedType(string name) : FlowType
{
    public override string Name => name;
    public override bool IsCompatibleWith(FlowType other) => false;
    public override int GetSpecificity() => 0;
}
