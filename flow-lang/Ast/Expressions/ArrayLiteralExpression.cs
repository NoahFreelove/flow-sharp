using FlowLang.Core;

namespace FlowLang.Ast.Expressions;

/// <summary>
/// Represents an array literal expression: [elem1, elem2, ...]
/// </summary>
public record ArrayLiteralExpression(
    SourceLocation Location,
    IReadOnlyList<Expression> Elements) : Expression(Location);
