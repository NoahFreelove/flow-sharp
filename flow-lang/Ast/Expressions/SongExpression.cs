using FlowLang.Core;

namespace FlowLang.Ast.Expressions;

/// <summary>
/// A reference to a section in a song arrangement, with optional repeat count.
/// </summary>
public record SongSectionReference(string Name, int RepeatCount);

/// <summary>
/// A song expression: [section1 section2*2 section3]
/// </summary>
public record SongExpression(
    SourceLocation Location,
    IReadOnlyList<SongSectionReference> Sections
) : Expression(Location);
