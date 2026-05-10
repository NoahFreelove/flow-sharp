using FlowLang.Core;

namespace FlowLang.Ast.Expressions;

/// <summary>
/// Represents array indexing: array@index
/// </summary>
public record ArrayIndexExpression(
    SourceLocation Location,
    Expression Array,
    Expression Index) : Expression(Location);
