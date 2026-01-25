namespace FlowLang.TypeSystem.SpecialTypes;

/// <summary>
/// Represents a time value in milliseconds.
/// </summary>
public sealed class MillisecondType : FlowType
{
    private MillisecondType() { }

    public static MillisecondType Instance { get; } = new();

    public override string Name => "Millisecond";

    public override bool CanConvertTo(FlowType target)
    {
        // Milliseconds can convert to Seconds
        return target is SecondType
            || base.CanConvertTo(target);
    }

    public override int GetSpecificity() => 122;

    /// <summary>
    /// Parses a millisecond string like "100ms" into a double value.
    /// </summary>
    public static double Parse(string msStr)
    {
        if (string.IsNullOrEmpty(msStr) || !msStr.EndsWith("ms"))
            throw new ArgumentException($"Invalid millisecond format: {msStr}");

        string valueStr = msStr[..^2]; // Remove "ms"

        if (!double.TryParse(valueStr, out double value))
            throw new ArgumentException($"Invalid millisecond value: {valueStr}");

        return value;
    }

    /// <summary>
    /// Formats a millisecond value back to string representation.
    /// </summary>
    public static string Format(double milliseconds)
    {
        return $"{milliseconds}ms";
    }
}
