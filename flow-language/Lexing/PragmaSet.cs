using FlowLang.Core;

namespace FlowLang.Lexing;

/// <summary>
/// Per-file pragma extraction result. Closed-set membership defined by
/// <see cref="PragmaRegistry"/>. Phase 21 D-02.
///
/// Held by <see cref="FlowLang.Ast.Program"/> (D-08) and threaded via constructor
/// parameters into <see cref="FlowLang.Parsing.Parser"/> + <see cref="SimpleLexer"/>
/// (D-05). Each imported file gets its OWN PragmaSet so pragmas do not propagate
/// across <c>use</c> imports (D-06; PRAG-02).
/// </summary>
public sealed record PragmaSet(
    IReadOnlySet<string> Enabled,
    IReadOnlyList<PragmaDeclarationSite> Sites)
{
    /// <summary>
    /// Singleton empty set. Used as the default when no pragmas were declared
    /// (the overwhelmingly common case for legacy .flow files).
    /// </summary>
    public static readonly PragmaSet Empty = new(
        new HashSet<string>(StringComparer.Ordinal),
        Array.Empty<PragmaDeclarationSite>());

    /// <summary>True iff <paramref name="pragmaName"/> was enabled in this file.</summary>
    public bool Has(string pragmaName) => Enabled.Contains(pragmaName);
}

/// <summary>
/// Where a pragma was declared. Carries name + source location so diagnostics can
/// point back to the originating <c>enable</c> line. Multiple sites for the same
/// pragma name are recorded (set semantics in <see cref="PragmaSet.Enabled"/>;
/// duplicate declaration sites still appear here for provenance).
/// </summary>
public sealed record PragmaDeclarationSite(string Name, SourceLocation Location);
