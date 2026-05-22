using FlowLang.Core;

namespace FlowLang.Ast.Expressions;

/// <summary>
/// Represents a function call expression.
///
/// <para>
/// Phase 36 Plan 36-02 (D-36-11): <see cref="NamedArgs"/> is the
/// defaulted-positional extension carrying named-argument bindings parsed
/// from the universal `(fn name=value)` surface. The field is nullable —
/// pre-Phase-36 call sites continue to compile unchanged, and the
/// interpreter takes the legacy positional-only dispatch path when
/// <see cref="NamedArgs"/> is null. Mixed shape: positional
/// <see cref="Arguments"/> precede all named args; the parser raises a
/// clear diagnostic if a positional follows a named binding.
/// </para>
/// </summary>
public record FunctionCallExpression(
    SourceLocation Location,
    string Name,
    IReadOnlyList<Expression> Arguments,
    Span? Span = null,
    IReadOnlyDictionary<string, Expression>? NamedArgs = null) : Expression(Location);
