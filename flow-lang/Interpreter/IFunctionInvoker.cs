using FlowLang.Ast;
using FlowLang.Ast.Statements;
using FlowLang.Runtime;
using System.Collections.Generic;

namespace FlowLang.Interpreter;

/// <summary>
/// Extracted interface to allow decoupled execution of user functions
/// and closures, breaking the circular dependency between Interpreter and Evaluator.
///
/// <para>
/// Phase 36 Plan 36-10 (SECT-01) adds <see cref="ExecuteStatement"/> +
/// <see cref="LastExpressionValue"/> so the section-call dispatcher in
/// <see cref="ExpressionEvaluator"/> can re-execute a parameterized
/// section's body under a synthetic frame.
/// </para>
/// </summary>
public interface IFunctionInvoker
{
    Value ExecuteUserFunction(ProcDeclaration proc, IReadOnlyList<Value> args);
    Value ExecuteUserFunctionWithCaptures(ProcDeclaration proc, IReadOnlyList<Value> args, IReadOnlyDictionary<string, Value>? capturedVariables);

    /// <summary>
    /// Phase 36 Plan 36-10 — execute a single statement (used by the
    /// section-call dispatcher to re-run a parameterized section's body).
    /// </summary>
    void ExecuteStatement(Statement stmt);

    /// <summary>
    /// Phase 36 Plan 36-10 — exposes the last evaluated expression-statement
    /// value so the section-call dispatcher can capture bare-expression
    /// sequences (mirrors the Interpreter.ExecuteSectionDeclaration capture
    /// shape).
    /// </summary>
    Value? LastExpressionValue { get; }
}
