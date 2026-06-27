using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using Xunit;

namespace FlowLang.Tests.Phase35;

/// <summary>
/// Phase 35 Plan 35-07 Wave 0 — `as` keyword reservation gates (LANG-03).
///
/// Asserts that <c>as</c> registers as its own <see cref="TokenType"/> value
/// (TokenType.As) and that prior composer usage of <c>as</c> as a bare
/// variable identifier is no longer accepted as a variable declaration —
/// per D-v1.5-01 pre-public no-deprecation latitude this breaks in a single
/// commit.
///
/// RED state: TokenType.As doesn't exist yet; lexer table has no `as` entry.
/// Task 2 lands the enum + keyword-table updates and flips this GREEN.
/// </summary>
public class AsKeywordReservationTests
{
    private static List<Token> Lex(string source)
    {
        var lexer = new SimpleLexer(source, new ErrorReporter());
        return lexer.Tokenize();
    }

    [Fact]
    public void AsTokenEmittedFromKeyword()
    {
        // The bare keyword `as` must lex to exactly one TokenType.As token
        // (plus the EOF tail). Mirrors Plan 35-05's MatchKeywordTokenized
        // shape — one keyword, one dedicated token type.
        var tokens = Lex("as");
        Assert.Contains(tokens, t => t.Type == TokenType.As);
        Assert.Single(tokens.Where(t => t.Type == TokenType.As));
    }

    [Fact]
    public void AsAsVariableNameNoLongerAllowed()
    {
        // Per D-v1.5-01 latitude: any composer code that used `as` as a
        // variable name now produces a parser error since `as` is reserved.
        // We parse `Int as = 5` end-to-end and assert the engine reports
        // at least one error — exact diagnostic shape is allowed to vary
        // (FlowError legacy OR FlowDiagnostic richer form) so long as
        // ErrorReporter.HasErrors is true after Execute.
        using var engine = new FlowLang.Core.FlowEngine(verbose: false);
        engine.Execute("Int as = 5");
        Assert.True(
            engine.ErrorReporter.HasErrors,
            "Expected a parse/runtime error when using reserved `as` as a variable name; got none. " +
            "Errors: " + engine.ErrorReporter.FormatErrors());
    }
}
