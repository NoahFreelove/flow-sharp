using FlowLang.Core;

namespace FlowLang.Lexing;

/// <summary>
/// Represents a token in the Flow language source code.
///
/// Phase 21 plan 21-02 (D-15): added optional positional <see cref="OriginalText"/>
/// field. When non-null, it carries the composer's original source text BEFORE any
/// canonicalization (e.g. <c>H4q</c> typed under <c>enable hAsB;</c> when <see cref="Text"/>
/// is the canonical <c>B4q</c>). When null (the overwhelmingly common case),
/// <see cref="Text"/> is itself the original — the <see cref="DiagnosticText"/> helper
/// returns whichever is present so error messages preserve the composer's authorship.
///
/// The 5th positional parameter is OPTIONAL — every existing 4-arg <c>new Token(...)</c>
/// call site continues to compile unchanged. Memory cost: 8 bytes per token (a null
/// reference) — negligible.
/// </summary>
public record Token(
    TokenType Type,
    string Text,
    SourceLocation Location,
    object? Value = null,
    string? OriginalText = null)
{
    /// <summary>
    /// Composer-friendly text for diagnostics. Returns <see cref="OriginalText"/> when
    /// non-null (the pre-canonicalization shape), falling back to <see cref="Text"/>.
    /// Use this in error messages so a composer who wrote <c>H4q</c> reads back
    /// <c>H4q</c> even though the renderer consumes the canonical <c>B4q</c>.
    /// </summary>
    public string DiagnosticText => OriginalText ?? Text;

    public override string ToString()
    {
        if (Value != null)
            return $"{Type}('{Text}', {Value}) at {Location}";
        return $"{Type}('{Text}') at {Location}";
    }
}
