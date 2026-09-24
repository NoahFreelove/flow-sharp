using FlowLang.Core;
using FlowLang.Lexing;

namespace FlowLang.Ast;

/// <summary>
/// Represents the root node of a Flow program.
///
/// Phase 21 D-08: gains a <see cref="PragmaSet"/> field carrying the file-scope
/// pragma declarations parsed by <see cref="PragmaScanner"/> before lex/parse.
/// Useful for LSP tooling, future incremental re-parse, and diagnostic reporting.
/// A backward-compatible 2-arg ctor preserves existing call sites that don't
/// care about pragmas (LSP, older tests).
/// </summary>
public record Program(
    SourceLocation Location,
    IReadOnlyList<Statement> Statements,
    PragmaSet Pragmas) : AstNode(Location)
{
    /// <summary>
    /// Backward-compat overload — defaults <see cref="Pragmas"/> to
    /// <see cref="PragmaSet.Empty"/>.
    /// </summary>
    public Program(SourceLocation location, IReadOnlyList<Statement> statements)
        : this(location, statements, PragmaSet.Empty) { }
}
