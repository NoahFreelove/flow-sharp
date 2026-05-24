using FlowLang.Ast.Elements;
using FlowLang.Core;

namespace FlowLang.Ast.Expressions;

/// <summary>
/// A reference to a section in a song arrangement, with optional repeat count.
/// (Legacy zero-arg form; Phase 36 Plan 36-10 introduces
/// <see cref="SectionCallElement"/> for parameterized calls but the legacy
/// shape is preserved so pre-Phase-36 evaluator paths continue working.)
/// </summary>
public record SongSectionReference(string Name, int RepeatCount);

/// <summary>
/// A song expression: <c>[section1 section2*2 section3]</c> (legacy) or
/// <c>[verse(C4) chorus verse(D4)*3]</c> (Phase 36 SECT-01, parameterized).
///
/// <para>
/// Phase 36 Plan 36-10 (D-36-13) extends the element list with a parallel
/// <see cref="Elements"/> defaulted-positional field — a <see cref="SongElement"/>
/// list mixing <see cref="BareSectionElement"/> (legacy bare-identifier form)
/// and <see cref="SectionCallElement"/> (parameterized form). Pre-Phase-36
/// constructions still populate <see cref="Sections"/>; the Phase 36 parser
/// populates BOTH fields, with <see cref="Sections"/> carrying the bare-name
/// summary for legacy evaluator paths and <see cref="Elements"/> carrying the
/// full call-args / repeat-count detail.
/// </para>
/// </summary>
public record SongExpression(
    SourceLocation Location,
    IReadOnlyList<SongSectionReference> Sections,
    Span? Span = null,
    IReadOnlyList<SongElement>? Elements = null
) : Expression(Location);
