using FlowLang.Core;

namespace FlowLang.Ast.Expressions;

/// <summary>
/// Represents a variable reference.
/// </summary>
public record VariableExpression(
    SourceLocation Location,
    string Name) : Expression(Location);
