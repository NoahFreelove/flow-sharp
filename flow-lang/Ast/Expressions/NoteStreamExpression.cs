using FlowLang.Core;

namespace FlowLang.Ast.Expressions;

/// <summary>
/// A single element in a note stream — a note, rest, or chord.
/// </summary>
public abstract record NoteStreamElement(SourceLocation Location);

/// <summary>
/// A single note with optional duration suffix and modifiers.
/// e.g., C4, C4q, C4q., C4+50c
/// </summary>
public record NoteElement(
    SourceLocation Location,
    string NoteName,          // e.g., "C4", "D#5", "Ebb3"
    string? DurationSuffix,   // w, h, q, e, s, t (null = auto-fit)
    bool IsDotted,            // e.g., C4q.
    bool IsTied,              // e.g., C4h~
    double? CentOffset        // e.g., +50c, -25c (null = none)
) : NoteStreamElement(Location);

/// <summary>
/// A rest with optional duration.
/// e.g., _, _h, _q
/// </summary>
public record RestElement(
    SourceLocation Location,
    string? DurationSuffix,
    bool IsDotted
) : NoteStreamElement(Location);

/// <summary>
/// Simultaneous notes (chord bracket notation).
/// e.g., [C4 E4 G4]q
/// </summary>
public record ChordElement(
    SourceLocation Location,
    IReadOnlyList<string> Notes,
    string? DurationSuffix,
    bool IsDotted
) : NoteStreamElement(Location);

/// <summary>
/// A named chord symbol in a note stream (e.g., Cmaj7, Dm7).
/// Parsed at compile time via ChordParser.
/// </summary>
public record NamedChordElement(
    SourceLocation Location,
    string ChordSymbol,
    string? DurationSuffix,
    bool IsDotted
) : NoteStreamElement(Location);

/// <summary>
/// A roman numeral chord reference in a note stream (e.g., I, iv, V7).
/// Resolved at compile time via ScaleDatabase using the active key context.
/// </summary>
public record RomanNumeralElement(
    SourceLocation Location,
    string Numeral,
    string? DurationSuffix,
    bool IsDotted
) : NoteStreamElement(Location);

/// <summary>
/// Random choice from a set of notes: (? C4 E4 G4) or (?? C4 E4 G4)
/// Optional weights: (? C4:50 E4:30 G4:20)
/// </summary>
public record RandomChoiceElement(
    SourceLocation Location,
    IReadOnlyList<(string Note, int? Weight)> Choices,
    bool IsSeeded,
    string? DurationSuffix,
    bool IsDotted
) : NoteStreamElement(Location);

/// <summary>
/// A bar within a note stream, delimited by | ... |
/// </summary>
public record NoteStreamBar(
    SourceLocation Location,
    IReadOnlyList<NoteStreamElement> Elements
);

/// <summary>
/// A complete note stream expression: | C4 D4 | E4 F4 |
/// Evaluates to a Sequence.
/// </summary>
public record NoteStreamExpression(
    SourceLocation Location,
    IReadOnlyList<NoteStreamBar> Bars
) : Expression(Location);
