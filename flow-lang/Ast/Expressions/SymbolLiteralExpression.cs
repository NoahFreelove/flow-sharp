using FlowLang.Core;

namespace FlowLang.Ast.Expressions;

/// <summary>
/// A symbol literal expression like <c>#kick</c>, <c>#snare</c> (Phase 26.1 SYM-01).
/// The leading <c>#</c> is consumed at lex time; <see cref="Name"/> is the body without <c>#</c>.
/// Evaluation interns the symbol via <c>ExecutionContext.SymbolInternTable</c> for pointer-equality.
/// </summary>
public record SymbolLiteralExpression(
    SourceLocation Location,
    string Name
) : Expression(Location);
