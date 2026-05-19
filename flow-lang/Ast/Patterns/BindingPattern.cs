using FlowLang.Core;

namespace FlowLang.Ast.Patterns;

/// <summary>
/// Phase 35 Plan 35-05 (LANG-01) — bare identifier pattern that matches every
/// scrutinee and binds it to <see cref="Name"/> for the arm body's scope.
///
/// <para>
/// Per Phase 35 RESEARCH §Pitfall 6, bindings DIE WITH THE ARM-BODY FRAME —
/// the evaluator pushes a temporary <see cref="Runtime.StackFrame"/> around the
/// arm body, declares the binding in it, and pops on completion. The binding
/// MUST NOT leak into the enclosing scope; that contract is gated by
/// <c>MatchRuntimeTests.BindingDoesNotLeakToEnclosingScope</c>.
/// </para>
/// </summary>
public record BindingPattern(
    SourceLocation Location,
    string Name,
    Span? Span = null) : Pattern(Location, Span);
