using FlowLang.Core;

namespace FlowLang.Ast.Statements;

/// <summary>
/// A break statement that exits the innermost loop.
/// </summary>
public record BreakStatement(SourceLocation Location) : Statement(Location);
