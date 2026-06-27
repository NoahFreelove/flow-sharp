using FlowLang.Core;

namespace FlowLang.Ast.Patterns;

/// <summary>
/// Phase 35 Plan 35-05 (LANG-01) — base record for pattern AST nodes used
/// inside <c>(match scrutinee | pat => body | ... )</c> arms.
///
/// <para>
/// Patterns are a PARALLEL FAMILY to <see cref="AstNode"/> — they do NOT
/// inherit from it. Per Phase 35 RESEARCH §Recommended Project Structure,
/// patterns live under <c>Ast/Patterns/</c> distinct from
/// <c>Ast/Expressions/</c> and <c>Ast/Statements/</c>. The matching
/// <c>MatchExpression</c> itself sits in <c>Ast/Expressions/</c> because
/// it IS an expression — only its sub-pattern children are pattern records.
/// </para>
///
/// <para>
/// Each pattern carries a <see cref="SourceLocation"/> for back-compat with
/// the existing diagnostic surface AND a defaulted <see cref="Core.Span"/>
/// for Phase 35-01's Rust-style multi-line diagnostic rendering. The parser
/// populates Span at construction time per the LANG-04 sweep convention.
/// </para>
/// </summary>
public abstract record Pattern(SourceLocation Location, Span? Span = null);
