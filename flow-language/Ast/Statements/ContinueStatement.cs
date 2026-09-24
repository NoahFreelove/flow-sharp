using FlowLang.Core;

namespace FlowLang.Ast.Statements;

/// <summary>
/// A continue statement that skips to the next iteration of the innermost loop.
/// </summary>
public record ContinueStatement(
    SourceLocation Location,
    Span? Span = null) : Statement(Location);
