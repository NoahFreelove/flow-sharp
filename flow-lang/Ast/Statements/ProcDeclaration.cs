using FlowLang.Core;
using FlowLang.TypeSystem;

namespace FlowLang.Ast.Statements;

/// <summary>
/// Represents a procedure (function) declaration.
/// </summary>
public record ProcDeclaration(
    SourceLocation Location,
    string Name,
    IReadOnlyList<Parameter> Parameters,
    IReadOnlyList<Statement> Body,
    bool IsInternal) : Statement(Location);

/// <summary>
/// Represents a function parameter.
/// </summary>
public record Parameter(
    string Name,
    FlowType Type,
    bool IsVarArgs = false);
