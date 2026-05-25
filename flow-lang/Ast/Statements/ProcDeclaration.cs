using FlowLang.Core;
using FlowLang.TypeSystem;

namespace FlowLang.Ast.Statements;

/// <summary>
/// Represents a procedure (function) declaration.
///
/// <para>
/// <b>Phase 44 Plan 44-02 D-02 / D-03 — <c>IsStrict</c>:</b> captured at parse
/// time from the declaring file's <c>PragmaSet.Has("strict")</c>. The Parser
/// at <c>flow-lang/Parsing/Parser.cs:384</c> threads
/// <c>_pragmaSet?.Has("strict") ?? false</c> into this field; the Interpreter
/// (<c>flow-lang/Interpreter/Interpreter.cs</c>, <c>ExecuteUserFunctionWithCaptures</c>)
/// pushes/pops <c>ExecutionContext.StrictMode = proc.IsStrict</c> on entry/exit
/// in a try/finally adjacent to <c>PushFrame</c>/<c>PopFrame</c>. This is the
/// SOURCE of truth for "what file declared this proc" at run time — Plan 44-03
/// + Plans 44-05..44-08 read <c>ExecutionContext.CallerStrictMode</c> (the
/// call-boundary snapshot) instead of <c>StrictMode</c> at leaf sites per
/// the D-03 "stdlib stays charitable internally" contract.
/// </para>
///
/// <para>
/// Defaulted trailing parameter (<c>= false</c>) preserves binary back-compat
/// with every existing positional construction site of this record. The Phase 35
/// <c>MatchExpression.CapturedPragmas: _pragmaSet</c> threading at
/// <c>Parser.cs:1794</c> is the closest in-tree analog — Phase 44 threads only
/// the boolean evaluation of <c>.Has("strict")</c> rather than the full
/// PragmaSet (smaller surface, no nullable handling at the read site).
/// </para>
/// </summary>
public record ProcDeclaration(
    SourceLocation Location,
    string Name,
    IReadOnlyList<Parameter> Parameters,
    IReadOnlyList<Statement> Body,
    bool IsInternal,
    Span? Span = null,
    bool IsStrict = false) : Statement(Location);

/// <summary>
/// Represents a function parameter.
/// </summary>
public record Parameter(
    string Name,
    FlowType Type,
    bool IsVarArgs = false);
