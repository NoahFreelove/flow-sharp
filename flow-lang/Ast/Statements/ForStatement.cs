using FlowLang.Core;
using FlowLang.TypeSystem;

namespace FlowLang.Ast.Statements;

/// <summary>
/// A for-each loop statement: for Type varName in collection { body }
/// </summary>
public record ForStatement(
    SourceLocation Location,
    FlowType ElementType,
    string VariableName,
    Expression Collection,
    IReadOnlyList<Statement> Body,
    Span? Span = null
) : Statement(Location);
