namespace FlowLang.Core;

/// <summary>
/// Phase 35 LANG-04 Wave 1 — a half-open source-position range
/// [<see cref="Start"/>, <see cref="End"/>) used by Tokens and AST records
/// to drive Rust-style diagnostic rendering.
///
/// <para>
/// Span SUPPLEMENTS <see cref="SourceLocation"/>; it does NOT replace it.
/// Per Phase 35 RESEARCH § Pitfall 1, 200+ read-sites across the LSP,
/// tests, and interpreter consume the existing single-point
/// <see cref="SourceLocation"/> field — removing it forces a same-PR
/// sweep. Span is added as a defaulted last positional parameter on
/// every <c>Token</c> and AST record so existing construction sites
/// continue to compile unchanged.
/// </para>
///
/// <para>
/// For multi-character tokens, <see cref="End"/> is the source position
/// ONE CHARACTER PAST the last consumed character (half-open). For
/// single-character tokens use <see cref="At(SourceLocation)"/> which
/// produces a zero-width span where <c>Start == End</c>.
/// </para>
/// </summary>
public record Span(SourceLocation Start, SourceLocation End)
{
    /// <summary>
    /// Sentinel singleton — used when a Span is constructed without a
    /// known source position (synthetic AST nodes, error-recovery paths).
    /// Diagnostics that render an Unknown span print "?:?".
    /// </summary>
    public static Span Unknown { get; } = new(SourceLocation.Unknown, SourceLocation.Unknown);

    /// <summary>
    /// Convenience constructor for zero-width spans at a single source
    /// position — used by single-character lexer tokens and by the
    /// <c>EffectiveSpan</c> fallback on Tokens whose <see cref="Span"/>
    /// field has not yet been populated by the migration.
    /// </summary>
    public static Span At(SourceLocation loc) => new(loc, loc);

    /// <summary>
    /// Convenience constructor for a span between two known source
    /// positions — used by AST records that capture the open delimiter
    /// at production start and the close delimiter at production end.
    /// </summary>
    public static Span Between(SourceLocation start, SourceLocation end) => new(start, end);

    /// <summary>
    /// Collapse on equal endpoints: a zero-width span prints as its
    /// single <see cref="Start"/>; a true range prints as <c>Start..End</c>.
    /// </summary>
    public override string ToString() =>
        Start == End ? Start.ToString() : $"{Start}..{End}";
}
