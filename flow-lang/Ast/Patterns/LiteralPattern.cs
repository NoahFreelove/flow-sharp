using FlowLang.Core;

namespace FlowLang.Ast.Patterns;

/// <summary>
/// Phase 35 Plan 35-05 (LANG-01) — matches a scrutinee by structural equality
/// against the embedded literal <see cref="Value"/>. Supports Int / Long /
/// Double / String / Bool / Note literal payloads — the parser stores the
/// underlying CLR value (matching <see cref="Ast.Expressions.LiteralExpression"/>'s
/// payload convention) and <see cref="Interpreter.PatternMatcher"/> dispatches
/// via the value comparator.
/// </summary>
public record LiteralPattern(
    SourceLocation Location,
    object Value,
    Span? Span = null) : Pattern(Location, Span);
