using FlowLang.Core;

namespace FlowLang.Ast.Expressions;

/// <summary>
/// A chord literal expression like Cmaj7, Dm, Gsus4.
/// The chord text is stored and parsed at evaluation time.
/// </summary>
public record ChordLiteralExpression(
    SourceLocation Location,
    string ChordText
) : Expression(Location);
