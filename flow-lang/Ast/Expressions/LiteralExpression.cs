using FlowLang.Core;

namespace FlowLang.Ast.Expressions;

/// <summary>
/// Represents a literal value (int, float, string, bool, note, semitone, time, decibel).
/// </summary>
public record LiteralExpression(
    SourceLocation Location,
    object Value) : Expression(Location);
