using FlowLang.TypeSystem.PrimitiveTypes;

namespace FlowLang.TypeSystem.SpecialTypes;

/// <summary>
/// Represents a logarithmic gain value in decibels.
/// </summary>
public sealed class DecibelType : FlowType
{
    private DecibelType() { }

    public static DecibelType Instance { get; } = new();

    public override string Name => "Decibel";

    public override int GetSpecificity() => 128;

    /// <summary>
    /// Decibel is compatible with Double and Float so existing builtins that take a
    /// Double parameter (e.g. gain(Buffer, Double) which already interprets its argument
    /// as dB) accept literal -12dB without "No matching overload" errors. Mirrors CentType.
    /// </summary>
    public override bool IsCompatibleWith(FlowType target)
    {
        return target is DoubleType or FloatType || base.IsCompatibleWith(target);
    }

    /// <summary>
    /// Parses a decibel string like "-3dB", "+6dB", "0dB" into a double value.
    /// </summary>
    public static double Parse(string dbStr)
    {
        if (string.IsNullOrEmpty(dbStr) || !dbStr.EndsWith("dB"))
            throw new ArgumentException($"Invalid decibel format: {dbStr}");

        string valueStr = dbStr[..^2]; // Remove "dB"

        if (!double.TryParse(valueStr, out double value))
            throw new ArgumentException($"Invalid decibel value: {valueStr}");

        return value;
    }

    /// <summary>
    /// Formats a decibel value back to string representation.
    /// </summary>
    public static string Format(double decibels)
    {
        string sign = decibels > 0 ? "+" : "";
        return $"{sign}{decibels}dB";
    }
}
