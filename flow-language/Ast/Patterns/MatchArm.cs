using FlowLang.Ast;
using FlowLang.Core;

namespace FlowLang.Ast.Patterns;

/// <summary>
/// Phase 35 Plan 35-05 (LANG-01) — one arm of a <see cref="Ast.Expressions.MatchExpression"/>:
/// a <see cref="Pattern"/> guarding a body <see cref="Expression"/>.
///
/// <para>
/// MatchArm is a value record, mirroring the
/// <see cref="Ast.Expressions.LambdaParameter"/> sub-record shape: it does NOT
/// inherit from <see cref="Pattern"/>, <see cref="Expression"/>, or
/// <see cref="AstNode"/>. The arm sequence is held by
/// <see cref="Ast.Expressions.MatchExpression.Arms"/> in source order, and
/// the runtime's first-match-wins contract walks the list naively
/// (D-v1.5-11 — decision-tree compile deferred to v1.6).
/// </para>
/// </summary>
public record MatchArm(
    Pattern Pattern,
    Expression Body,
    Span? Span = null);
