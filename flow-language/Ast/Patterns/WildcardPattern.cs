using FlowLang.Core;

namespace FlowLang.Ast.Patterns;

/// <summary>
/// Phase 35 Plan 35-05 (LANG-01) — the <c>_</c> arm pattern; matches every
/// scrutinee unconditionally and binds nothing. Composers use a single
/// <c>| _ => default</c> tail to make a match exhaustive (per
/// D-v1.5-05 silent-Void fall-through is the v1.5 default; Plan 35-06
/// adds the matchExhaustive pragma + WARN-vs-error policy).
/// </summary>
public record WildcardPattern(
    SourceLocation Location,
    Span? Span = null) : Pattern(Location, Span);
