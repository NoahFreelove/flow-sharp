using System.Collections.Generic;
using System.Linq;
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using Xunit;

namespace FlowLang.Tests.Unit.Phase31;

/// <summary>
/// Phase 31 Plan 31-03 (REQ-4 / SPEC-4) — pin the three new line-comment forms
/// added to <see cref="SimpleLexer.SkipWhitespaceAndComments"/>:
///   1. `;` at column-0 → Lisp-style line comment (D-11 Option A position-sensitive)
///   2. `TODO:` at column-0 → lead-in line comment
///   3. `FIXME:` at column-0 → lead-in line comment
///
/// All three arms mirror the existing `Note:` arm at SimpleLexer.cs:1144 — same
/// `IsStartOfLineContent()` gate, same consume-to-newline body. D-11 Option A is
/// the critical lock: a `;` MID-LINE remains a <see cref="TokenType.Semicolon"/>
/// statement terminator. Every shipping pragma (`enable hAsB;`) and every typed
/// declaration (`Int x = 5;`) preserves its current lex behavior — zero token-stream
/// change for any valid existing program, which by construction preserves the
/// Phase 18/25/27/28 two-run byte-identical determinism contract.
///
/// Decisions referenced:
///   D-11 — Option A position-sensitive `;` Lisp-style line comment (31-DECISIONS.md)
///
/// Pattern: lexer-direct (no parser/engine) per the
/// flow-lang.Tests/Unit/Phase26/NegativeLiteralLexFacts.cs precedent.
/// </summary>
public class Phase31LexerCommentFormsTests
{
    private static List<Token> Tokenize(string src)
    {
        var er = new ErrorReporter();
        return new SimpleLexer(src, er, fileName: null, pragmaSet: PragmaSet.Empty).Tokenize();
    }

    [Fact]
    public void Semicolon_AtColumn0_IsLineComment()
    {
        // D-11 Option A: `;` at column-0 → line comment to end-of-line.
        // The `proc` keyword on the next line must be the first non-Eof token after the comment.
        var tokens = Tokenize("; This is a Lisp comment\nproc main ()");
        Assert.DoesNotContain(tokens, t => t.Type == TokenType.Semicolon);
        // First non-Eof token is the `proc` keyword (verifies the whole comment line was consumed).
        var first = tokens.First(t => t.Type != TokenType.Eof);
        Assert.Equal(TokenType.Proc, first.Type);
    }

    [Fact]
    public void Semicolon_MidLine_IsStillSemicolonToken()
    {
        // D-11 Option A canary: mid-line `;` stays a statement-terminator Semicolon.
        var tokens = Tokenize("Int x = 5;");
        Assert.Contains(tokens, t => t.Type == TokenType.Semicolon);
    }

    [Fact]
    public void Semicolon_IndentedAtLineStart_IsLineComment()
    {
        // IsStartOfLineContent() accepts leading whitespace before the `;`.
        // The indented `;` line is comment; the `Int y = 1;` semicolon survives.
        var tokens = Tokenize("  ; indented comment\nInt y = 1;");
        // Exactly one Semicolon survives — the one on the `Int y = 1;` line.
        Assert.Single(tokens.Where(t => t.Type == TokenType.Semicolon));
    }

    [Fact]
    public void TODO_AtColumn0_IsLineComment()
    {
        // `TODO:` at column-0 → consume to end-of-line. No `TODO` identifier or `:` colon emitted
        // for the comment line. The `proc` keyword on the next line is the first real token.
        var tokens = Tokenize("TODO: fix this\nproc main ()");
        Assert.DoesNotContain(tokens, t => t.Text == "TODO");
        Assert.DoesNotContain(tokens, t => t.Type == TokenType.Colon);
        var first = tokens.First(t => t.Type != TokenType.Eof);
        Assert.Equal(TokenType.Proc, first.Type);
    }

    [Fact]
    public void FIXME_AtColumn0_IsLineComment()
    {
        // `FIXME:` at column-0 → consume to end-of-line. No `FIXME` identifier or `:` colon emitted.
        var tokens = Tokenize("FIXME: broken\nInt x = 5");
        Assert.DoesNotContain(tokens, t => t.Text == "FIXME");
        Assert.DoesNotContain(tokens, t => t.Type == TokenType.Colon);
        // The `Int x = 5` survives — verify the IntLiteral 5 made it through.
        Assert.Contains(tokens, t => t.Type == TokenType.IntLiteral);
    }

    [Fact]
    public void TODO_InsideStringLiteral_IsNotComment()
    {
        // RESEARCH Pitfall 8: SkipWhitespaceAndComments runs ONLY between tokens, never inside
        // ScanString. A `TODO:` inside `"..."` is part of the string literal.
        var tokens = Tokenize("(print \"TODO: hello\")");
        var strToken = tokens.FirstOrDefault(t => t.Type == TokenType.StringLiteral);
        Assert.NotNull(strToken);
        Assert.Contains("TODO", strToken!.Text);
        Assert.Contains("hello", strToken.Text);
    }

    [Fact]
    public void EnableHAsB_StillParses_OptionACanary()
    {
        // D-11 Option A regression canary: `enable hAsB;` is NOT column-0 `;` (the `;` follows
        // `hAsB`, not a fresh line-start). The Semicolon MUST survive — this is the pragma-syntax
        // gate that locked Option A over Option C in plan-phase.
        var tokens = Tokenize("enable hAsB;\nproc main ()");
        Assert.Contains(tokens, t => t.Type == TokenType.Semicolon);
    }

    [Fact]
    public void DoubleSlash_StillWorks_NoRegression()
    {
        // Existing `//` line-comment arm not affected by Phase 31's three new arms.
        var tokens = Tokenize("// regular comment\nInt x = 5;");
        // The comment line is consumed; `Int x = 5;` survives with its semicolon.
        Assert.Contains(tokens, t => t.Type == TokenType.Semicolon);
        Assert.Contains(tokens, t => t.Type == TokenType.IntLiteral);
    }
}
