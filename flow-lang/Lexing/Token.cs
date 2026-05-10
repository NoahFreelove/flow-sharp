using FlowLang.Core;

namespace FlowLang.Lexing;

/// <summary>
/// Represents a token in the Flow language source code.
/// </summary>
public record Token(
    TokenType Type,
    string Text,
    SourceLocation Location,
    object? Value = null)
{
    public override string ToString()
    {
        if (Value != null)
            return $"{Type}('{Text}', {Value}) at {Location}";
        return $"{Type}('{Text}') at {Location}";
    }
}
