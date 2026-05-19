using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using Xunit;

namespace FlowLang.Tests.Phase35;

/// <summary>
/// Phase 35 Plan 35-05 Wave 0 — match-keyword lexer gates (LANG-01).
///
/// Asserts that <c>match</c> and <c>when</c> register as their own
/// <see cref="TokenType"/> values (TokenType.Match / TokenType.When) and
/// that the existing <c>_</c> wildcard tokenization remains TokenType.Underscore.
///
/// RED state: TokenType.Match + TokenType.When entries do not yet exist —
/// the test fails to compile until Task 2 lands the enum additions and
/// the keyword-table entries in SimpleLexer.
/// </summary>
public class MatchLexerTests
{
    private static List<Token> Lex(string source)
    {
        var lexer = new SimpleLexer(source, new ErrorReporter());
        return lexer.Tokenize();
    }

    [Fact]
    public void MatchKeywordTokenized()
    {
        var tokens = Lex("match");
        Assert.Contains(tokens, t => t.Type == TokenType.Match);
        // Exactly one Match token (plus the EOF tail).
        Assert.Single(tokens.Where(t => t.Type == TokenType.Match));
    }

    [Fact]
    public void WhenKeywordTokenized()
    {
        var tokens = Lex("when");
        Assert.Contains(tokens, t => t.Type == TokenType.When);
        Assert.Single(tokens.Where(t => t.Type == TokenType.When));
    }

    [Fact]
    public void UnderscoreStillTokenizesAsWildcard()
    {
        // Pre-existing behavior — the wildcard underscore continues to lex as
        // TokenType.Underscore (already used as the note-stream rest). Pattern
        // matching reuses the same token; no change here.
        var tokens = Lex("_");
        Assert.Contains(tokens, t => t.Type == TokenType.Underscore);
    }
}
