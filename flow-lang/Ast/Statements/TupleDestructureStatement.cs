using FlowLang.Ast.Expressions;
using FlowLang.Core;
using FlowLang.TypeSystem;

namespace FlowLang.Ast.Statements;

/// <summary>
/// Destructure pattern slot (Phase 26.1 TUP-09).
/// <see cref="Type"/> is OPTIONAL — composers can write <c>&lt;&lt;a, b&gt;&gt; = expr</c>
/// without per-slot type annotations and rely on RHS typing
/// (see CONTEXT § Specifics block 2 / Tuple destructuring scope).
/// </summary>
public record TupleDestructurePattern(FlowType? Type, string Name);

/// <summary>
/// Destructuring assignment statement: <c>&lt;&lt;Type? name, Type? name, ...&gt;&gt; = expr</c>
/// (Phase 26.1 TUP-09). Arity check is at runtime since RHS type is not known
/// statically for variables.
/// </summary>
public record TupleDestructureStatement(
    SourceLocation Location,
    IReadOnlyList<TupleDestructurePattern> Patterns,
    Expression Value,
    Span? Span = null) : Statement(Location);
