using FlowLang.Core;
using FlowLang.Ast.Expressions;

namespace FlowLang.Ast.Statements;

/// <summary>
/// Phase 32 D-13 — a <c>tuning &lt;expr&gt; { ... }</c> musical-context block.
/// Sets the active tuning (a <see cref="FlowLang.StandardLibrary.Audio.Tuning.ResolvedTuning"/>)
/// for the body scope.
///
/// Parallel AST node to <see cref="MusicalContextStatement"/> rather than a
/// 6th <see cref="MusicalContextType"/> enum variant: the existing musical-context
/// variants all carry SCALAR primitive values (Int / Double / String key name);
/// the tuning block carries a <c>Tuning</c>-typed <see cref="Expression"/> which
/// the interpreter evaluates and pushes onto
/// <see cref="FlowLang.Runtime.MusicalContext.TuningStack"/> via
/// <see cref="FlowLang.Runtime.ExecutionContext.PushTuning"/>. Keeping value-shape
/// and dispatch clean — narrow blast radius, <see cref="MusicalContextStatement"/>
/// and its parser stay untouched.
///
/// Per CONTEXT D-15, three composer surface forms all route through this single
/// AST node via <see cref="TuningExpr"/>:
/// <list type="bullet">
///   <item>identifier: <c>tuning partch { }</c> — <see cref="VariableExpression"/></item>
///   <item>inline call: <c>tuning (loadScala "x.scl") { }</c> — <see cref="FunctionCallExpression"/></item>
///   <item>string-literal sugar: <c>tuning "x.scl" { }</c> — desugared at parse time
///   to <c>(loadScala "x.scl")</c> via a synthetic <see cref="FunctionCallExpression"/>
///   whose <see cref="Core.SourceLocation"/> is the line of the user's typed
///   <c>tuning</c> keyword (T-32-AST mitigation — runtime errors point at the
///   user's source line, not at a synthetic frame).</item>
/// </list>
///
/// Per D-14, the interpreter wraps the body in <c>try { ... } finally {
/// PopTuning(); }</c> so the stack frame still pops if the body throws —
/// ensuring blocks remain ephemeral across REPL eval boundaries (Pitfall 2
/// — pragmas sticky, blocks ephemeral).
/// </summary>
public record TuningContextStatement(
    SourceLocation Location,
    Expression TuningExpr,
    IReadOnlyList<Statement> Body
) : Statement(Location);
