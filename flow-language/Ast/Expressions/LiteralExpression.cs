using FlowLang.Core;

namespace FlowLang.Ast.Expressions;

/// <summary>
/// Represents a literal value (int, float, string, bool, note, semitone, time, decibel).
/// </summary>
/// <param name="IsMusicLiteral">
/// True when the literal's string payload is the raw text of a music-literal token
/// (Note / Semitone / Cent / Time / Decibel / Hertz) that must be resolved by
/// <c>TryParseSpecialLiteral</c> at eval time. False for ordinary quoted-string
/// literals, whose payload is a genuine <see cref="string"/> and must NOT be
/// re-typed as a music value (audit §2.1 — e.g. <c>String s = "10s"</c> stays a String,
/// <c>"a"</c> stays a String, and a dict keyed by <c>"10s"</c> round-trips).
/// </param>
public record LiteralExpression(
    SourceLocation Location,
    object Value,
    Span? Span = null,
    bool IsMusicLiteral = false) : Expression(Location);
