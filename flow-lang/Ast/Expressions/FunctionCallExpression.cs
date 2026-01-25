using FlowLang.Core;

namespace FlowLang.Ast.Expressions;

/// <summary>
/// Represents a function call expression.
/// </summary>
public record FunctionCallExpression(
    SourceLocation Location,
    string Name,
    IReadOnlyList<Expression> Arguments) : Expression(Location);
