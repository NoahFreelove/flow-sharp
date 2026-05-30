using FlowLang.Core;

namespace FlowLang.Ast.Expressions;

/// <summary>
/// A beat literal expression like <c>0.5b</c>, <c>2b</c>, <c>-1b</c> (Phase 45 D-01).
/// Carries the raw source double exactly as written; the multiplier formula
/// <c>final = pragma_on ? raw × (4.0 / denom) : raw</c> applies at eval time
/// in <see cref="FlowLang.Interpreter.ExpressionEvaluator.EvaluateBeatLiteral"/>,
/// reading <see cref="FlowLang.Runtime.ExecutionContext.BeatTrueToSig"/> +
/// <see cref="FlowLang.Runtime.MusicalContext.TimeSignature"/>.
/// </summary>
public record BeatLiteralExpression(
    SourceLocation Location,
    double RawValue,
    Span? Span = null
) : Expression(Location);
