using FlowLang.Core;
using FlowLang.Ast.Expressions;

namespace FlowLang.Ast.Statements;

/// <summary>
/// Represents an expression used as a statement.
/// </summary>
public record ExpressionStatement(
    SourceLocation Location,
    Expression Expression) : Statement(Location);
