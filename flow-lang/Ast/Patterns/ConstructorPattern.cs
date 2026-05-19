using FlowLang.Core;

namespace FlowLang.Ast.Patterns;

/// <summary>
/// Phase 35 Plan 35-05 (LANG-01) — named constructor pattern (generic) with
/// optional sub-patterns. The discriminator flags
/// (<see cref="IsChordLiteral"/>, <see cref="IsRomanNumeral"/>,
/// <see cref="IsArticulationSymbol"/>) select the music-aware extractor
/// implementation in Plan 35-06's PatternMatcher.
///
/// <para>
/// In Plan 35-05, all three flags default to <c>false</c>. The parser sets
/// <see cref="IsChordLiteral"/> when a chord literal token (Cmaj7, Dm, F#dim)
/// appears in a pattern position — so Plan 35-06's match logic can dispatch
/// to <c>ChordParser.IsChordSymbol</c> / <c>ChordParser.Parse</c> without
/// re-tokenizing. Roman-numeral and articulation-symbol detection are added
/// in Plan 35-06.
/// </para>
/// </summary>
public record ConstructorPattern(
    SourceLocation Location,
    string Name,
    IReadOnlyList<Pattern> SubPatterns,
    Span? Span = null) : Pattern(Location, Span)
{
    /// <summary>
    /// Plan 35-06: when true, PatternMatcher routes through chord-quality
    /// extraction (ChordParser-backed) instead of structural equality.
    /// </summary>
    public bool IsChordLiteral { get; init; }

    /// <summary>
    /// Plan 35-06: when true, PatternMatcher resolves the numeral against
    /// the active key musical context before comparing.
    /// </summary>
    public bool IsRomanNumeral { get; init; }

    /// <summary>
    /// Plan 35-06: when true, PatternMatcher reads the scrutinee's
    /// <c>Articulation</c> enum value and compares against the symbol name.
    /// </summary>
    public bool IsArticulationSymbol { get; init; }
}
