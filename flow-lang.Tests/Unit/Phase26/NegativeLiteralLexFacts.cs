using System.Collections.Generic;
using System.Linq;
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using Xunit;

namespace FlowLang.Tests.Unit.Phase26;

/// <summary>
/// Phase 26 Wave 0 (RED): pins Decision D-02/D-04 — negative number literals
/// (`-5`, `-3.14`) lex as single tokens at every expression-start position
/// (after `=`/`:`, after `(`, `,`, `[`, `->`, and the opening `|` of a note
/// stream, plus statement-start). Pre-Wave-1 the lexer emits two tokens
/// (Minus + IntLiteral); Wave 1 turns these GREEN.
///
/// Decisions referenced (locked in 26-CONTEXT.md):
///   D-02 — six expression-start lex positions for negative literals.
///   D-04 — implementation strategy: track previous-emitted-token type, emit
///          single `IntLiteral`/`FloatLiteral` when the prev token is one of
///          (LParen, Comma, LBracket, Arrow, Assign, Pipe, statement-start).
///
/// PLUS the Pitfall-1 invariant: music-context keywords (tempo/swing/pan/gain/
/// reverbTime) MUST be excluded from the expression-start gate so the existing
/// `Match(TokenType.Minus)` consumers in Parser.cs:450/465/527/542/556 keep
/// firing. Tested by `TempoMinus_PreservesStandaloneMinus`.
///
/// Pattern: lexer-direct (no parser/engine) per S-07; analog
/// flow-lang.Tests/Unit/Phase24/DiatonicSpellingsFacts.cs Theory + InlineData.
/// </summary>
public class NegativeLiteralLexFacts
{
    private static List<Token> Lex(string source)
    {
        var lexer = new SimpleLexer(source, new ErrorReporter());
        return lexer.Tokenize();
    }

    [Theory]
    // (description, source, expected token type, expected value)
    [InlineData("statement-start int",      "Int x = -5",                TokenType.IntLiteral,    -5)]
    [InlineData("statement-start float",    "Double y = -3.14",          TokenType.FloatLiteral,  -3.14)]
    [InlineData("after LParen",             "(add (-5) 3)",              TokenType.IntLiteral,    -5)]
    [InlineData("after Comma",              "(add 5, -7)",               TokenType.IntLiteral,    -7)]
    [InlineData("after LBracket",           "Int x = 5\nInt z = -1",     TokenType.IntLiteral,    -1)]
    [InlineData("after Arrow",              "5 -> add -3",               TokenType.IntLiteral,    -3)]
    [InlineData("after Pipe (note stream)", "Sequence s = | -5 |",       TokenType.IntLiteral,    -5)]
    public void NegativeLiteralLexesAsSingleToken(string desc, string source,
                                                  TokenType expectedType, object expectedValue)
    {
        // The expected behavior post-Wave-1: a single token with Type=expectedType
        // and Value equal to the signed literal. Pre-Wave-1, the lexer emits a
        // standalone Minus token followed by the unsigned IntLiteral, so this
        // assertion fails (intentional RED).
        var tokens = Lex(source);
        Assert.True(
            tokens.Any(t => t.Type == expectedType
                && t.Value != null
                && t.Value.Equals(expectedValue)),
            $"[{desc}] expected {expectedType}({expectedValue}) in token stream, got: " +
            string.Join(", ", tokens.Select(t => $"{t.Type}({t.Value ?? t.Text})")));
    }

    [Fact]
    public void TempoMinus_PreservesStandaloneMinus()
    {
        // Pitfall 1 — music-context keywords MUST be excluded from the
        // expression-start gate. After `tempo` identifier, the parser at
        // Parser.cs:450 expects to consume `Match(TokenType.Minus)` and then a
        // separate IntLiteral — so the lexer must NOT collapse `-120` into a
        // single signed IntLiteral here.
        //
        // This Fact is paradoxically GREEN both pre-Wave-1 (lexer doesn't
        // collapse anything yet) AND post-Wave-1 (lexer correctly excludes
        // music-context keywords from the expression-start gate). Its job is
        // to catch a regression in Wave 1 if the gate is implemented too
        // aggressively.
        var tokens = Lex("tempo -120 { (print 1) }");
        int tempoIdx = tokens.FindIndex(t => t.Text == "tempo");
        Assert.True(tempoIdx >= 0, "tempo keyword not found");
        Assert.Equal(TokenType.Minus, tokens[tempoIdx + 1].Type);
        Assert.Equal(TokenType.IntLiteral, tokens[tempoIdx + 2].Type);
        Assert.Equal(120, tokens[tempoIdx + 2].Value);
    }
}
