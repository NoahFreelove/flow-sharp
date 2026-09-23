namespace FlowLang.Runtime;

/// <summary>
/// Conversions between domain types that <see cref="Value.ConvertTo"/> does not know
/// about (for example Note to Semitone, or milliseconds to seconds). Domain layers
/// register a converter when they are installed; a converter returns null for any
/// pair it does not handle.
/// </summary>
public static class ValueConversions
{
    private static readonly List<Func<Value, TypeSystem.FlowType, Value?>> _converters = new();
    private static readonly object _lock = new();

    public static void Register(Func<Value, TypeSystem.FlowType, Value?> converter)
    {
        lock (_lock) _converters.Add(converter);
    }

    public static Value? TryConvert(Value value, TypeSystem.FlowType target)
    {
        Func<Value, TypeSystem.FlowType, Value?>[] converters;
        lock (_lock) converters = _converters.ToArray();
        foreach (var converter in converters)
            if (converter(value, target) is { } converted)
                return converted;
        return null;
    }
}
