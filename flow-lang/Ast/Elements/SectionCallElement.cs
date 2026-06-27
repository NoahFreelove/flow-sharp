using FlowLang.Core;

namespace FlowLang.Ast.Elements;

/// <summary>
/// Phase 36 Plan 36-10 (D-36-13/14/15) — parameterized section call inside a
/// <c>Song = [ ... ]</c> expression. Carries:
///
/// <list type="bullet">
///   <item><description><see cref="PositionalArgs"/> — call-site positional
///   expressions, evaluated left-to-right at song-element dispatch time.</description></item>
///   <item><description><see cref="NamedArgs"/> — call-site named expressions
///   (mirrors <c>FunctionCallExpression.NamedArgs</c> from Phase 36 Plan 36-02).
///   Null when no named args appear.</description></item>
///   <item><description><see cref="RepeatCount"/> — postfix <c>*N</c> repetition
///   per D-36-14; defaults to 1 when no repeat suffix is parsed.</description></item>
/// </list>
///
/// <para>
/// Lives in <c>flow-lang/Ast/Elements/</c> per the plan's RESEARCH §Recommended
/// Project Structure — a parallel family to <c>Ast/Expressions/</c> and
/// <c>Ast/Statements/</c>, used only inside <c>SongExpression</c>'s element list.
/// </para>
/// </summary>
public record SectionCallElement(
    SourceLocation Location,
    string Name,
    IReadOnlyList<Expression> PositionalArgs,
    IReadOnlyDictionary<string, Expression>? NamedArgs = null,
    int RepeatCount = 1,
    Span? Span = null
) : SongElement(Location, Span);
