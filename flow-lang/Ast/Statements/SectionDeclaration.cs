using FlowLang.Core;

namespace FlowLang.Ast.Statements;

/// <summary>
/// A section declaration: section name { ... }
/// </summary>
public record SectionDeclaration(
    SourceLocation Location,
    string Name,
    IReadOnlyList<Statement> Body
) : Statement(Location);
