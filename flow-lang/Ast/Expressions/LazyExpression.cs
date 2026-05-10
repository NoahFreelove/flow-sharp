using FlowLang.Core;

namespace FlowLang.Ast.Expressions;

/// <summary>
/// Represents a lazy expression: lazy (expr)
/// The inner expression is not evaluated until forced.
/// </summary>
public record LazyExpression(
    SourceLocation Location,
    Expression InnerExpression) : Expression(Location);
