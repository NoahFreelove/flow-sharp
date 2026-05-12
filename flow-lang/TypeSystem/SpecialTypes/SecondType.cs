using FlowLang.TypeSystem.PrimitiveTypes;

namespace FlowLang.TypeSystem.SpecialTypes;

/// <summary>
/// Represents a time value in seconds.
/// </summary>
public sealed class SecondType : FlowType
{
    private SecondType() { }

    public static SecondType Instance { get; } = new();

    public override string Name => "Second";

    public override bool CanConvertTo(FlowType target)
    {
        // Seconds can convert to Milliseconds (CONTEXT D-02 — STAYS)
        return target is MillisecondType
            || base.CanConvertTo(target);
    }

    /// <summary>
    /// Phase 26.2 ERG-01 — Second is compatible with Double and Float so
    /// (reverb buf 0.5 1.5) and (reverb buf 0.5 1.5s) both reach the same
    /// parameter slots. Mirrors CentType.cs:24-27.
    /// </summary>
    public override bool IsCompatibleWith(FlowType target)
    {
        return target is DoubleType or FloatType || base.IsCompatibleWith(target);
    }

    public override int GetSpecificity() => 123;

    /// <summary>
    /// Parses a second string like "2.5s" into a double value.
    /// </summary>
    public static double Parse(string secStr)
    {
        if (string.IsNullOrEmpty(secStr) || !secStr.EndsWith("s") || secStr.EndsWith("ms"))
            throw new ArgumentException($"Invalid second format: {secStr}");

        string valueStr = secStr[..^1]; // Remove "s"

        if (!double.TryParse(valueStr, out double value))
            throw new ArgumentException($"Invalid second value: {valueStr}");

        return value;
    }

    /// <summary>
    /// Formats a second value back to string representation.
    /// </summary>
    public static string Format(double seconds)
    {
        return $"{seconds}s";
    }
}
