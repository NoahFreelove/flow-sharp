using FlowLang.Ast.Patterns;
using FlowLang.Core;

namespace FlowLang.Ast.Statements;

/// <summary>
/// A section declaration: <c>section name { ... }</c> (zero-arg form) or
/// <c>section name(Pattern, Pattern, ...) { ... }</c> (parameterized form,
/// Phase 36 SECT-01 / D-36-13..18).
///
/// <para>
/// Phase 36 Plan 36-10 (D-36-17) extends the AST with defaulted-positional
/// <see cref="Parameters"/> and <see cref="DefaultValues"/> fields — mirrors
/// the Phase 35 LANG-03 / Phase 36 36-02 defaulted-positional sweep
/// convention so every pre-Phase-36 construction site compiles unchanged.
/// </para>
///
/// <para>
/// <see cref="Parameters"/> is <c>null</c> for zero-arg sections (the
/// backward-compatible legacy form). When non-null, each entry is a Phase 35
/// pattern that the section's call args are tested against — full pattern
/// syntax is supported (LiteralPattern, BindingPattern, ConstructorPattern
/// with music-aware extractors, GuardPattern, tuple destructure).
/// </para>
///
/// <para>
/// <see cref="DefaultValues"/> mirrors <see cref="Parameters"/> 1:1. Each
/// slot's <c>Expression?</c> is the optional default value (D-36-15): a
/// section parameter <c>Note root = C4</c> stores <c>DefaultValues[i] =
/// LiteralExpression(C4)</c>; a parameter without <c>= ...</c> stores
/// <c>null</c>.
/// </para>
/// </summary>
public record SectionDeclaration(
    SourceLocation Location,
    string Name,
    IReadOnlyList<Statement> Body,
    Span? Span = null,
    IReadOnlyList<Pattern>? Parameters = null,
    IReadOnlyList<Expression?>? DefaultValues = null
) : Statement(Location);
