using FlowLang.Ast;
using FlowLang.Interpreter;
using System.Threading;

namespace FlowLang.Runtime;

/// <summary>
/// Represents a deferred computation that can be forced to produce a value.
/// Caches both successful values and exceptions; re-throws cached exceptions
/// with the original stack trace preserved (ExceptionDispatchInfo semantics).
/// </summary>
public class Thunk
{
    private readonly Lazy<Value> _lazy;

    public Thunk(Expression expression, ExpressionEvaluator evaluator)
    {
        if (expression == null) throw new ArgumentNullException(nameof(expression));
        if (evaluator == null) throw new ArgumentNullException(nameof(evaluator));

        // ExecutionAndPublication is the default for Lazy<T>(Func<T>).
        // Specifying it explicitly documents intent and guards against a
        // future .NET runtime changing the default mode.
        //
        // Lazy<T> internally uses ExceptionDispatchInfo.Capture + .Throw()
        // to cache and rethrow exceptions thrown by the factory, preserving
        // the original stack trace on every subsequent .Value access.
        // This satisfies both:
        //   - D-05 (ExceptionDispatchInfo stack preservation)
        //   - D-06 (thread-safe memoization)
        _lazy = new Lazy<Value>(
            () => evaluator.Evaluate(expression),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>
    /// Forces evaluation. Returns the cached value if already evaluated.
    /// If the evaluator threw on first access, re-throws the same exception
    /// with the original stack trace preserved.
    /// </summary>
    public Value Force() => _lazy.Value;

    public bool IsEvaluated => _lazy.IsValueCreated;
}
