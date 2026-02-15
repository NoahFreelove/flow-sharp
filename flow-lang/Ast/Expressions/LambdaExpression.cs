using FlowLang.Ast.Statements;
using FlowLang.Core;
using FlowLang.TypeSystem;

namespace FlowLang.Ast.Expressions;

/// <summary>
/// Represents a lambda parameter with name and type.
/// </summary>
public record LambdaParameter(string Name, FlowType Type);

/// <summary>
/// Represents a lambda expression: fn Type name, Type name => body
/// </summary>
public record LambdaExpression(
    SourceLocation Location,
    IReadOnlyList<LambdaParameter> Parameters,
    IReadOnlyList<Statement> Body) : Expression(Location);
