using FlowLang.Core;
using FlowLang.Ast.Expressions;

namespace FlowLang.Ast.Statements;

/// <summary>
/// Represents an explicit return statement.
/// </summary>
public record ReturnStatement(
    SourceLocation Location,
    Expression Value,
    Span? Span = null) : Statement(Location);
