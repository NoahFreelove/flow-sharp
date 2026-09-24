using FlowLang.Core;

namespace FlowLang.Ast.Expressions;

/// <summary>
/// A single chord element in a progression: a roman numeral with optional bar count.
/// e.g., "I" (1 bar), "IV:2" (2 bars), "V7" (1 bar)
/// </summary>
public record ProgressionElement(
    SourceLocation Location,
    string Numeral,    // "I", "IV", "vi", "V7"
    int BarCount       // Default 1, overridden by :N suffix
);

/// <summary>
/// A chord progression expression: progression | I IV V I |
/// Evaluates to a Sequence with voice-led chords.
/// </summary>
public record ProgressionExpression(
    SourceLocation Location,
    IReadOnlyList<ProgressionElement> Chords,
    int? VoiceCount,    // Optional voice count from "voices N" modifier. null = auto
    Span? Span = null
) : Expression(Location);
