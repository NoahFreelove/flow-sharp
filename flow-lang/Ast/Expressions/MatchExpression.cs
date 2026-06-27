using FlowLang.Ast.Patterns;
using FlowLang.Core;
using FlowLang.Lexing;

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
///   <item><description>If no arm matches, evaluator policy is determined by
///   <see cref="CapturedPragmas"/> (Plan 35-06 / D-v1.5-05):</description>
///     <list type="bullet">
///       <item><description>With <c>matchExhaustive</c> set — promote to a
///       FlowDiagnostic at Error level via ErrorReporter.</description></item>
///       <item><description>Without — WARN once per match Span via
///       RenderingDiagnostics.WarnOnce, then fall through to
///       <c>Value.Void()</c> (charitable interpretation).</description></item>
///     </list>
///   </item>
/// </list>
/// </para>
///
/// <para>
/// Phase 35 Plan 35-06 (LANG-02) added <see cref="CapturedPragmas"/> as a
/// defaulted-null last positional parameter. The parser captures the active
/// per-file PragmaSet from its own session and threads it onto every
/// MatchExpression node it builds. The evaluator consults this property
/// (NOT a thread-walking context lookup) so that imported modules — which
/// each carry their own PragmaSet per Phase 21 D-06 / Pitfall 4 — get the
/// correct policy even when their match expressions are evaluated in the
/// importer's frame.
/// </para>
/// </summary>
public record MatchExpression(
    SourceLocation Location,
    Expression Scrutinee,
    IReadOnlyList<MatchArm> Arms,
    Span? Span = null,
    PragmaSet? CapturedPragmas = null) : Expression(Location);
