using FlowLang.Core;

namespace FlowLang.Ast.Expressions;

/// <summary>
/// Tuple-unpack flow operator: <c>tup ~&gt; func(args)</c> (Phase 26.1 TUP-10).
/// When <see cref="Left"/> evaluates to a Tuple, components unpack into
/// positional args of <see cref="Right"/>. On non-tuple LHS, falls through
/// to single-arg <c>-&gt;</c> semantics (charitable per ROADMAP success
/// criterion 3, ergonomics-priority memory).
/// <para>
/// Per RESEARCH Q5 the parser ALWAYS emits this node — parse-time arity is
/// unknown for variable RHS, so unpacking is deferred to the evaluator.
/// </para>
/// </summary>
public record TupleUnpackFlowExpression(
    SourceLocation Location,
    Expression Left,
    Expression Right) : Expression(Location);
