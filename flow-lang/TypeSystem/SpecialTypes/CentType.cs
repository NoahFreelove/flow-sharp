using FlowLang.TypeSystem.PrimitiveTypes;

namespace FlowLang.TypeSystem.SpecialTypes;

/// <summary>
/// Represents a pitch offset in cents (1/100 of a semitone).
/// Used for precise microtonal pitch control.
/// 100 cents = 1 semitone.
/// </summary>
public sealed class CentType : FlowType
{
    private CentType() { }

    public static CentType Instance { get; } = new();

    public override string Name => "Cent";

    public override int GetSpecificity() => 143;

    /// <summary>
    /// Cent is compatible with Double and Float, allowing transpose(seq, 50.0)
    /// to match the transpose(Sequence, Cent) overload.
    /// </summary>
    public override bool IsCompatibleWith(FlowType target)
    {
        return target is DoubleType or FloatType || base.IsCompatibleWith(target);
    }

    /// <summary>
    /// Parse a cent value from string (e.g., "+50c", "-25c", "100c").
    /// </summary>
    public static double Parse(string centStr)
    {
        if (string.IsNullOrEmpty(centStr))
            throw new ArgumentException("Cent string cannot be empty");

        // Remove 'c' suffix
        if (!centStr.EndsWith("c"))
            throw new ArgumentException($"Invalid cent format: {centStr}. Must end with 'c'.");

        string numberPart = centStr.Substring(0, centStr.Length - 1);

        if (!double.TryParse(numberPart, out double centValue))
            throw new ArgumentException($"Invalid cent value: {numberPart}");

        return centValue;
    }
}
