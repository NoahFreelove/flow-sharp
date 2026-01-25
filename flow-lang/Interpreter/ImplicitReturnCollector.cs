using FlowLang.Runtime;
using FlowLang.TypeSystem.PrimitiveTypes;

namespace FlowLang.Interpreter;

/// <summary>
/// Collects non-void return values during function execution for implicit returns.
/// </summary>
public class ImplicitReturnCollector
{
    private readonly List<Value> _collectedValues = [];
    private bool _hasExplicitReturn = false;

    /// <summary>
    /// Adds a value to the collection if it's not void.
    /// </summary>
    public void Collect(Value value)
    {
        if (_hasExplicitReturn)
            return;

        if (value.Type is not VoidType)
            _collectedValues.Add(value);
    }

    /// <summary>
    /// Marks that an explicit return was encountered.
    /// Clears any previously collected values.
    /// </summary>
    public void MarkExplicitReturn()
    {
        _hasExplicitReturn = true;
        _collectedValues.Clear();
    }

    /// <summary>
    /// Gets the final return value.
    /// If no values collected, returns Void.
    /// If one value collected, returns that value.
    /// If multiple values collected, returns an array.
    /// </summary>
    public Value GetResult()
    {
        if (_hasExplicitReturn || _collectedValues.Count == 0)
            return Value.Void();

        if (_collectedValues.Count == 1)
            return _collectedValues[0];

        // Return array of collected values
        var elementType = _collectedValues[0].Type;
        return Value.Array(_collectedValues, elementType);
    }

    public void Clear()
    {
        _collectedValues.Clear();
        _hasExplicitReturn = false;
    }
}
