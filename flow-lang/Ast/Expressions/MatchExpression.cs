using FlowLang.Ast.Patterns;
using FlowLang.Core;

namespace FlowLang.Ast.Expressions;

/// <summary>
/// Phase 35 Plan 35-05 (LANG-01) — represents the pattern-matching expression
/// <c>(match scrutinee | pat1 => body1 | pat2 => body2 | _ => default)</c>.
///
/// <para>
/// Lives in <c>Ast/Expressions/</c> (sibling to
/// <see cref="FlowExpression"/>) because the form IS an expression — it
/// evaluates to the body Value of the first matching arm. Per Phase 35
/// RESEARCH §Recommended Project Structure, only the sub-pattern children
/// (Pattern subtypes) live under <c>Ast/Patterns/</c>; the match node itself
/// is an Expression.
/// </para>
///
/// <para>
/// Arm semantics (Plan 35-05 cut):
/// <list type="bullet">
///   <item><description>Arms are evaluated in source order (naive linear
///   scan per D-v1.5-11).</description></item>
///   <item><description>First matching arm wins — no C-style
///   fall-through.</description></item>
///   <item><description>If no arm matches, the evaluator returns
///   <c>Value.Void()</c> silently. Plan 35-06 layers the
///   <c>matchExhaustive</c> pragma + WARN-vs-error policy on top.</description></item>
/// </list>
/// </para>
/// </summary>
public record MatchExpression(
    SourceLocation Location,
    Expression Scrutinee,
    IReadOnlyList<MatchArm> Arms,
    Span? Span = null) : Expression(Location);
