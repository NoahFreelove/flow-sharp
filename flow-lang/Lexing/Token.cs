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
/// Phase 35 plan 35-01 (LANG-04): added optional positional <see cref="Span"/> field
/// for Rust-style diagnostic rendering. When non-null, it carries the token's full
/// [start, end) source-position range; when null (back-compat construction sites
/// in test code), <see cref="EffectiveSpan"/> synthesizes a zero-width span from
/// <see cref="Location"/>. The lexer populates <see cref="Span"/> at every Token
/// construction site post-Phase-35.
///
/// The 5th + 6th positional parameters are both OPTIONAL — every existing 4-arg
/// <c>new Token(...)</c> call site continues to compile unchanged. Memory cost:
/// 16 bytes per token (two null references) — negligible.
/// </summary>
public record Token(
    TokenType Type,
    string Text,
    SourceLocation Location,
    object? Value = null,
    string? OriginalText = null,
    Span? Span = null)
{
    /// <summary>
    /// Composer-friendly text for diagnostics. Returns <see cref="OriginalText"/> when
    /// non-null (the pre-canonicalization shape), falling back to <see cref="Text"/>.
    /// Use this in error messages so a composer who wrote <c>H4q</c> reads back
    /// <c>H4q</c> even though the renderer consumes the canonical <c>B4q</c>.
    /// </summary>
    public string DiagnosticText => OriginalText ?? Text;

    /// <summary>
    /// Phase 35 LANG-04 helper: returns <see cref="Span"/> if non-null, otherwise
    /// a synthesized zero-width <see cref="FlowLang.Core.Span"/> at the existing
    /// <see cref="Location"/>. Back-compat callers that constructed Tokens via the
    /// 3/4/5-arg ctor (no Span supplied) get a meaningful Span for diagnostics
    /// without having to opt in to the migration.
    /// </summary>
    public Span EffectiveSpan => Span ?? FlowLang.Core.Span.At(Location);

    public override string ToString()
    {
        if (Value != null)
            return $"{Type}('{Text}', {Value}) at {Location}";
        return $"{Type}('{Text}') at {Location}";
    }
}
