using FlowLang.Core;

namespace FlowLang.Ast.Expressions;

/// <summary>
/// Represents a member access expression: object.member
/// </summary>
public record MemberAccessExpression(
    SourceLocation Location,
    Expression Object,
    string MemberName,
    Span? Span = null) : Expression(Location);
