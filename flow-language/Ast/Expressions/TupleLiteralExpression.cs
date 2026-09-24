using FlowLang.Core;

namespace FlowLang.Ast.Expressions;

/// <summary>
/// Tuple literal `&lt;&lt;elem1, elem2, ...&gt;&gt;` (Phase 26.1 TUP-09).
/// Empty `&lt;&lt;&gt;&gt;` and singleton `&lt;&lt;x&gt;&gt;` are valid arities. Per-position element
/// types are inferred from element types at evaluation time (see
/// <c>ExpressionEvaluator.EvaluateTupleLiteral</c>).
/// </summary>
public record TupleLiteralExpression(
    SourceLocation Location,
    IReadOnlyList<Expression> Elements,
    Span? Span = null) : Expression(Location);
