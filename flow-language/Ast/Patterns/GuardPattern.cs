using FlowLang.Ast;
using FlowLang.Core;

namespace FlowLang.Ast.Patterns;

/// <summary>
/// Phase 35 Plan 35-05 (LANG-01) — wraps an <see cref="Inner"/> pattern with
/// a boolean side-condition <see cref="GuardExpression"/>. The guard runs in
/// the extended scope produced by <see cref="Inner"/>'s bindings — so a guard
/// can read variables introduced by a sibling <see cref="BindingPattern"/>.
///
/// <para>
/// Surface form: <c>| n when (greater n 0) => "pos"</c> — parser produces
/// <c>GuardPattern(Inner=BindingPattern(n), GuardExpression=(greater n 0))</c>.
/// </para>
/// </summary>
public record GuardPattern(
    SourceLocation Location,
    Pattern Inner,
    Expression GuardExpression,
    Span? Span = null) : Pattern(Location, Span);
