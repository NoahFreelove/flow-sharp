using System.Runtime.ExceptionServices;
using FlowLang.Ast;
using FlowLang.Interpreter;

namespace FlowLang.Runtime;

/// <summary>
/// Represents a deferred computation that can be forced to produce a value.
/// Caches both successful values and exceptions; re-throws cached exceptions
/// with the original stack trace preserved (ExceptionDispatchInfo semantics).
/// Cancellation is never cached: a thunk interrupted by a cancelled or timed-out
/// evaluation is evaluated afresh the next time it is forced.
/// </summary>
public class Thunk
{
    private readonly Expression _expression;
    private readonly ExpressionEvaluator _evaluator;
    private readonly object _gate = new();
    private Value? _value;
    private ExceptionDispatchInfo? _error;
    private bool _evaluating;

    public Thunk(Expression expression, ExpressionEvaluator evaluator)
    {
        _expression = expression ?? throw new ArgumentNullException(nameof(expression));
        _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
    }

    /// <summary>
    /// Forces evaluation. Returns the cached value if already evaluated.
    /// If the evaluator threw on first access, re-throws the same exception
    /// with the original stack trace preserved. Thread-safe: concurrent callers
    /// wait for one evaluation.
    /// </summary>
    public Value Force()
    {
        lock (_gate)
        {
            if (_value is not null) return _value;
            _error?.Throw();
            if (_evaluating)
                throw new InvalidOperationException("A lazy value depends on itself.");
            _evaluating = true;
            try
            {
                _value = _evaluator.Evaluate(_expression);
                return _value;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _error = ExceptionDispatchInfo.Capture(ex);
                throw;
            }
            finally
            {
                _evaluating = false;
            }
        }
    }

    public bool IsEvaluated => _value is not null;
}
