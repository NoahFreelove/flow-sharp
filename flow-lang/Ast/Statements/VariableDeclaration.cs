using FlowLang.Core;
using FlowLang.TypeSystem;
using FlowLang.Ast.Expressions;

namespace FlowLang.Ast.Statements;

/// <summary>
/// Represents a variable declaration: Type name = value
/// </summary>
public record VariableDeclaration(
    SourceLocation Location,
    FlowType Type,
    string Name,
    Expression Value) : Statement(Location);
