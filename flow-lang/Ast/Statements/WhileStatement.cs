using FlowLang.Core;

namespace FlowLang.Ast.Statements;

/// <summary>
/// A while loop statement: while condition { body }
/// </summary>
public record WhileStatement(
    SourceLocation Location,
    Expression Condition,
    IReadOnlyList<Statement> Body
) : Statement(Location);
