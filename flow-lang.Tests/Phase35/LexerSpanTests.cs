using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using Xunit;

namespace FlowLang.Tests.Phase35;

/// <summary>
/// Phase 35 LANG-04 Wave 1 — span migration foundation gates.
///
/// Asserts that every Token produced by <see cref="SimpleLexer"/> carries a
/// non-Unknown <c>Span</c> whose <c>Start</c> equals the existing
/// <c>Location</c> field (defaulted-parameter back-compat) and whose
/// <c>End</c> is the source position one character past the last consumed
/// character of the token. The migration MUST be additive; <c>Location</c>
/// stays for the 200+ read-sites in LSP / tests / interpreter (per
/// RESEARCH § Pitfall 1).
/// </summary>
public class LexerSpanTests
{
    private static List<Token> Lex(string source)
    {
        var lexer = new SimpleLexer(source, new ErrorReporter());
        return lexer.Tokenize();
    }

    [Fact]
    public void EveryTokenHasNonUnknownSpan()
    {
        var tokens = Lex("(add 1 2)");
        Assert.NotEmpty(tokens);
        foreach (var tok in tokens)
        {
            // The default ctor leaves Span = null; the migration MUST fill it in.
            Assert.NotNull(tok.Span);
            Assert.NotEqual(Span.Unknown, tok.Span);
            // Phase 21 / Phase 35 back-compat invariant: Span.Start == Location.
            Assert.Equal(tok.Location, tok.Span!.Start);
        }
    }

    [Fact]
    public void MultiCharTokenSpansToEndOfToken()
    {
        // `transpose` is a 9-character identifier. The end column must be
        // 9 characters past the start column (exclusive end position — one
        // past the last consumed character of the token).
        var tokens = Lex("transpose");
        var ident = tokens.First(t => t.Type == TokenType.Identifier);
        Assert.NotNull(ident.Span);
        Assert.NotEqual(Span.Unknown, ident.Span);
        Assert.Equal(9, ident.Span!.End.Column - ident.Span.Start.Column);
        // The token's Location and Span.Start agree on the start position.
        Assert.Equal(ident.Location.Line, ident.Span.Start.Line);
        Assert.Equal(ident.Location.Column, ident.Span.Start.Column);
    }

    [Fact]
    public void SingleCharTokenHasZeroOrOneWidthSpan()
    {
        // For single-character tokens (e.g., `(`), Span.End == Span.Start
        // (zero-width span via Span.At) is acceptable per the lexer plan.
        var tokens = Lex("(");
        var lparen = tokens.First(t => t.Type == TokenType.LParen);
        Assert.NotNull(lparen.Span);
        Assert.NotEqual(Span.Unknown, lparen.Span);
        // Single-char tokens use Span.At(start) — Start and End collapse.
        Assert.Equal(lparen.Span!.Start, lparen.Span.End);
    }
}
