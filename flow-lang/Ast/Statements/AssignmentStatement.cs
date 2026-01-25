using FlowLang.Core;
using FlowLang.Ast.Expressions;

namespace FlowLang.Ast.Statements;

/// <summary>
/// Represents a variable reassignment: name = value
/// </summary>
public record AssignmentStatement(
    SourceLocation Location,
    string Name,
    Expression Value) : Statement(Location);
