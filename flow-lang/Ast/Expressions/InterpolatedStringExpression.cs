using FlowLang.Core;

namespace FlowLang.Ast.Expressions;

/// <summary>
/// Represents a string interpolation expression: $"text {expr} text"
/// Parts is a list of expressions -- LiteralExpression for text segments,
/// arbitrary expressions for {expr} segments.
/// </summary>
public record InterpolatedStringExpression(
    SourceLocation Location,
    IReadOnlyList<Expression> Parts,
    Span? Span = null
) : Expression(Location);
