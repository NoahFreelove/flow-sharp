using FlowLang.Core;

namespace FlowLang.Ast.Elements;

/// <summary>
/// Phase 36 Plan 36-10 (D-36-13) — base type for the elements appearing inside
/// a <c>Song = [ ... ]</c> expression. Two concrete forms:
///
/// <list type="bullet">
///   <item><description><see cref="BareSectionElement"/> — bare-identifier
///   form <c>verse</c> or <c>verse*3</c>. Zero-arg section reference;
///   backward-compatible with the pre-Phase-36 surface.</description></item>
///   <item><description><see cref="SectionCallElement"/> — parameterized
///   form <c>verse(C4, 2)</c> or <c>verse(root=C4)*3</c>. Carries positional
///   args, named args, and a repeat count.</description></item>
/// </list>
/// </summary>
public abstract record SongElement(SourceLocation Location, Span? Span = null);

/// <summary>
/// Phase 36 Plan 36-10 — bare-identifier section reference inside a song
/// expression (the legacy form, unchanged semantically from
/// <c>Ast.Expressions.SongSectionReference</c>). Wrapping it in a
/// <see cref="SongElement"/> sibling of <see cref="SectionCallElement"/> lets
/// the parser and evaluator dispatch on element kind without a discriminator
/// field.
/// </summary>
public record BareSectionElement(
    SourceLocation Location,
    string Name,
    int RepeatCount = 1,
    Span? Span = null
) : SongElement(Location, Span);
