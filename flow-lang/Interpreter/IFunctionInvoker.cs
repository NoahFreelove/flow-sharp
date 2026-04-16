using FlowLang.Ast.Statements;
using FlowLang.Runtime;
using System.Collections.Generic;

namespace FlowLang.Interpreter;

/// <summary>
/// Extracted interface to allow decoupled execution of user functions
/// and closures, breaking the circular dependency between Interpreter and Evaluator.
/// </summary>
public interface IFunctionInvoker
{
    Value ExecuteUserFunction(ProcDeclaration proc, IReadOnlyList<Value> args);
    Value ExecuteUserFunctionWithCaptures(ProcDeclaration proc, IReadOnlyList<Value> args, IReadOnlyDictionary<string, Value>? capturedVariables);
}
