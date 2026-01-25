using FlowLang.Core;

namespace FlowLang.Ast.Expressions;

/// <summary>
/// Represents a binary operation (arithmetic, comparison, etc.).
/// </summary>
public record BinaryExpression(
    SourceLocation Location,
    Expression Left,
    BinaryOperator Operator,
    Expression Right) : Expression(Location);

public enum BinaryOperator
{
    Add,
    Subtract,
    Multiply,
    Divide
}
