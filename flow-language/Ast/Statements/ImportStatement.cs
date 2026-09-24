using FlowLang.Core;

namespace FlowLang.Ast.Statements;

/// <summary>
/// Represents an import statement: use "filepath"
/// </summary>
public record ImportStatement(
    SourceLocation Location,
    string FilePath,
    Span? Span = null) : Statement(Location);
