namespace FlowLang.TypeSystem.SpecialTypes;

/// <summary>
/// Represents a relative pitch shift in semitones (+1st, -5st, etc.).
/// </summary>
public sealed class SemitoneType : FlowType
{
    private SemitoneType() { }

    public static SemitoneType Instance { get; } = new();

    public override string Name => "Semitone";

    public override int GetSpecificity() => 125;

    /// <summary>
    /// Parses a semitone string like "+1st", "-5st" into an integer value.
    /// </summary>
    public static int Parse(string semitoneStr)
    {
        if (string.IsNullOrEmpty(semitoneStr) || !semitoneStr.EndsWith("st"))
            throw new ArgumentException($"Invalid semitone format: {semitoneStr}");

        string valueStr = semitoneStr[..^2]; // Remove "st"

        if (!int.TryParse(valueStr, out int value))
            throw new ArgumentException($"Invalid semitone value: {valueStr}");

        return value;
    }

    /// <summary>
    /// Formats a semitone value back to string representation.
    /// </summary>
    public static string Format(int semitones)
    {
        string sign = semitones >= 0 ? "+" : "";
        return $"{sign}{semitones}st";
    }
}
