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
///
/// <para>
/// <b>Phase 44 review WR-09 — cross-file semantics for lambdas:</b>
/// when this <c>ProcDeclaration</c> is the synthetic record produced by
/// <see cref="Interpreter.ExpressionEvaluator.EvaluateLambda"/>,
/// <c>IsStrict</c> is captured from the file that DECLARED the lambda,
/// not the file (or library state) that LATER invokes it. So a strict-file
/// lambda passed into a charitable library's higher-order function still
/// executes with strict semantics — its body's <c>(print 5)</c> raises
/// <c>[strict] (print) requires String</c> even though the immediate
/// caller is non-strict. This is intentional under D-03's "file-scope
/// strict" contract but can surprise composers handing lambdas to
/// charitable libraries: APIs that accept lambdas should document whether
/// strict-bit propagation matters for their use case. See the XML doc on
/// <c>EvaluateLambda</c> for the call-chain mechanics.
/// </para>
/// </summary>
/// <para>
/// <b>Phase 45 Plan 45-06 D-04 — <c>IsBeatTrueToSig</c>:</b> captured at parse
/// time from the declaring file's <c>PragmaSet.Has("beat-true-to-sig")</c>,
/// mirroring <c>IsStrict</c> exactly. The Interpreter
/// (<c>ExecuteUserFunctionWithCaptures</c>) pushes/pops
/// <c>ExecutionContext.BeatTrueToSig = proc.IsBeatTrueToSig</c> on entry/exit
/// in the SAME try/finally as the strict-bit push/pop. Without this, a
/// <c>(beat N)</c> call inside a proc declared in a pragma-OFF helper file
/// would read the CALLER's live <c>BeatTrueToSig</c> bit (the importer's),
/// multiplying by the wrong file's pragma state. The ModuleLoader file-load
/// save-set-restore (Plan 45-03) only covers the import boundary, not the
/// later proc-invocation boundary — so the per-proc capture is required for
/// the cross-file boundary (REQ-BEAT-TEST-04 / Pitfall 3) to hold.
/// </para>
/// <para>
/// <b>Phase 41 Plan 41-02 DOC-01 / D-07 — <c>DocComment</c>:</b> the text of the
/// <c>///</c> doc-comment block immediately preceding this declaration (leading
/// <c>///</c> + one optional space stripped, contiguous lines newline-joined), or
/// <c>null</c> when the proc has no <c>///</c>. Captured at parse time: the lexer
/// emits an out-of-band <see cref="Lexing.TokenType.DocComment"/> token which the
/// Parser buffers in <c>_pendingDocComment</c> and threads here at
/// <c>flow-lang/Parsing/Parser.cs</c> (<c>ParseProcDeclaration</c>), clearing the
/// buffer on consume so it never leaks to the next proc. Defaulted trailing
/// parameter (<c>= null</c>) preserves binary back-compat with every existing
/// positional construction site, the same rationale the <c>IsStrict</c> /
/// <c>IsBeatTrueToSig</c> fields above cite. <b>Charitable (D-07):</b> a proc with
/// no <c>///</c> is valid and gets a signature-only doc entry downstream (the
/// <c>flow doc</c> generator, Plan 41-03), never an error; an orphaned <c>///</c>
/// (no proc following) is dropped silently by the Parser.
/// </para>
public record ProcDeclaration(
    SourceLocation Location,
    string Name,
    IReadOnlyList<Parameter> Parameters,
    IReadOnlyList<Statement> Body,
    bool IsInternal,
    Span? Span = null,
    bool IsStrict = false,
    bool IsBeatTrueToSig = false,
    string? DocComment = null) : Statement(Location);

/// <summary>
/// Represents a function parameter.
/// </summary>
public record Parameter(
    string Name,
    FlowType Type,
    bool IsVarArgs = false);
