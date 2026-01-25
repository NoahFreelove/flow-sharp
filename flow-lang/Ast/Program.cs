using FlowLang.Core;

namespace FlowLang.Ast;

/// <summary>
/// Represents the root node of a Flow program.
/// </summary>
public record Program(
    SourceLocation Location,
    IReadOnlyList<Statement> Statements) : AstNode(Location);
