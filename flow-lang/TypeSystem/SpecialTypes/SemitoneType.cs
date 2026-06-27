using FlowLang.TypeSystem.PrimitiveTypes;

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
    /// Semitone is compatible with Int, allowing transpose(seq, 2) to match
    /// the transpose(Sequence, Semitone) overload without requiring a literal like +2st.
    /// <para>
    /// sweep-0614: deliberately NOT widened to Long. A global
    /// <c>Semitone.IsCompatibleWith(Long)</c> would, via the resolver's
    /// inverse-direction clause (<c>param.IsCompatibleWith(arg)</c> in
    /// <see cref="FunctionSignature"/>.SlotMatches), also make the Int-backed
    /// idempotent <c>semitones(Semitone)</c> overload accept a Long arg and then
    /// throw on <c>As&lt;int&gt;()</c> — contradicting the documented D-08
    /// "(semitones x) is Int-ONLY; Long falls through to No matching overload"
    /// carve-out. The whole-number-Long → transpose ergonomics gap is instead
    /// closed by an explicit <c>transpose(Sequence, Long)</c> overload in
    /// TransformFunctions, scoped to transpose only.
    /// </para>
    /// </summary>
    public override bool IsCompatibleWith(FlowType target)
    {
        return target is IntType || base.IsCompatibleWith(target);
    }

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
