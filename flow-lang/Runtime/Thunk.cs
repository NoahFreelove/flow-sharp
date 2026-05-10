using FlowLang.Ast;
using FlowLang.Interpreter;

namespace FlowLang.Runtime;

/// <summary>
/// Represents a deferred computation that can be forced to produce a value.
/// </summary>
public class Thunk
{
    private Expression? _expression;
    private ExpressionEvaluator? _evaluator;
    private Value? _cachedValue;
    private bool _isEvaluated;
    private readonly object _lock = new object();

    public Thunk(Expression expression, ExpressionEvaluator evaluator)
    {
        _expression = expression ?? throw new ArgumentNullException(nameof(expression));
        _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        _isEvaluated = false;
    }

    /// <summary>
    /// Forces evaluation of the thunk, returning the cached value if already evaluated.
    /// </summary>
    public Value Force()
    {
        if (!_isEvaluated)
        {
            lock (_lock)
            {
                if (!_isEvaluated)
                {
                    _cachedValue = _evaluator!.Evaluate(_expression!);
                    _isEvaluated = true;

                    // Allow GC to collect expression and evaluator
                    _expression = null;
                    _evaluator = null;
                }
            }
        }

        return _cachedValue!;
    }

    public bool IsEvaluated => _isEvaluated;
}
