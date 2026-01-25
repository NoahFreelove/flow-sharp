using FlowLang.Core;

namespace FlowLang.Ast.Expressions;

/// <summary>
/// Represents a flow operator expression: left -> right
/// The result of left is prepended to the arguments of right.
/// </summary>
public record FlowExpression(
    SourceLocation Location,
    Expression Left,
    Expression Right) : Expression(Location);
