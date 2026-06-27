using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Integration.Sweep0614;

/// <summary>
/// Regression coverage for the sweep-2026-06-14 "lexer-literals" group:
///
///   1. Locale-independent music-literal parsing — Cent/Decibel/Millisecond/
///      Second literals must read '.' as the decimal point in EVERY culture
///      (comma-decimal locales previously silently 10x-corrupted the value via a
///      bare double.TryParse that treats '.' as a thousands separator).
///   2. Negative numeric literal after a value-end token — `(add 5 -3)`,
///      `(transpose (| ... |) -2)` (after RParen), and the documented negative
///      index `xs@-1` (after `@`) must lex `-N` as a single signed literal.
///   3. Rest-with-duration-suffix — `_q`/`_h`/`_e`/`_w`/`_s` must lex as a
///      standalone Underscore rest + duration suffix, not a single identifier.
///   4. Uppercase note-name typo in a note stream — `| C4 D4 Z9 E4 |` must
///      recover charitably (rest + located one-shot advisory) and keep the
///      surrounding notes instead of silently dropping them with an
///      "Empty note stream" error pointed at the closing pipe.
/// </summary>
public class LexerLiteralsSweepTests
{
    private static List<Token> Lex(string source)
    {
        var lexer = new SimpleLexer(source, new ErrorReporter());
        return lexer.Tokenize();
    }

    // ===== Bug: locale-dependent double.TryParse — silent 10x corruption =====

    [Theory]
    [InlineData("Second s = 2.5s\n(print (str s))", "2.5s", 2.5)]
    [InlineData("Decibel d = -12.5dB\n(print (str d))", "-12.5dB", -12.5)]
    [InlineData("Millisecond m = 100.5ms\n(print (str m))", "100.5ms", 100.5)]
    [InlineData("Cent c = 50.5c\n(print (str c))", "50.5c", 50.5)]
    public void MusicLiterals_ParseInvariant_UnderCommaDecimalLocale(
        string source, string desc, double expected)
    {
        // Force a comma-decimal culture (de-DE) for the duration of the eval.
        // Before the fix, the bare double.TryParse read '.' as a thousands group
        // → 25 / -125 / 1005 / 505. After the fix, InvariantCulture is pinned.
        var savedCulture = CultureInfo.CurrentCulture;
        var savedThread = CultureInfo.DefaultThreadCurrentCulture;
        try
        {
            var de = new CultureInfo("de-DE");
            CultureInfo.CurrentCulture = de;
            CultureInfo.DefaultThreadCurrentCulture = de;

            using var engine = new FlowEngine(verbose: false);
            // Evaluate the bare literal so the Value's underlying double is
            // exactly what the literal-parse path produced (display formatting is
            // a separate, culture-dependent concern).
            string literal = desc; // e.g. "2.5s"
            var result = engine.ExecuteScriptAndGetResult(literal);
            Assert.NotNull(result);
            Assert.False(engine.ErrorReporter.HasErrors,
                $"[{desc}] unexpected error: {engine.ErrorReporter.FormatErrors()}");
            Assert.IsType<double>(result!.Data);
            Assert.Equal(expected, (double)result.Data!, precision: 6);
        }
        finally
        {
            CultureInfo.CurrentCulture = savedCulture;
            CultureInfo.DefaultThreadCurrentCulture = savedThread;
        }
    }

    // ===== Bug: negative literal after a value-end token =====

    [Theory]
    [InlineData("after IntLiteral", "(add 5 -3)", -3)]
    [InlineData("after FloatLiteral", "(add 5.0 -3)", -3)]
    [InlineData("second negative after first literal", "(add -3 -4)", -4)]
    [InlineData("after At (negative index)", "xs@-1", -1)]
    public void NegativeLiteral_AfterValueEndToken_LexesAsSingleSignedToken(
        string desc, string source, int expected)
    {
        var tokens = Lex(source);
        Assert.True(
            tokens.Any(t => t.Type == TokenType.IntLiteral
                && t.Value != null && t.Value.Equals(expected)),
            $"[{desc}] expected IntLiteral({expected}) in token stream, got: " +
            string.Join(", ", tokens.Select(t => $"{t.Type}({t.Value ?? t.Text})")));
        // No stray standalone Minus must remain in the stream for these forms.
        Assert.DoesNotContain(tokens, t => t.Type == TokenType.Minus);
    }

    [Fact]
    public void NegativeLiteral_AfterRParen_LexesAsSignedToken()
    {
        // `(transpose (| C4 D4 |) -2)` — the `-2` follows the inner RParen.
        var tokens = Lex("timesig 4/4 { Sequence s = (transpose (| C4 D4 |) -2) }");
        Assert.Contains(tokens, t => t.Type == TokenType.IntLiteral && t.Value != null && t.Value.Equals(-2));
    }

    [Fact]
    public void NegativeArrayIndex_LexesAndEvaluatesToLastElement()
    {
        // The documented `xs@-1` surface syntax must reach the negative-index
        // handling (ExpressionEvaluator: arr.Count + index).
        using var engine = new FlowEngine(verbose: false);
        var result = engine.ExecuteScriptAndGetResult("Ints xs = [1, 2, 3]\nxs@-1");
        Assert.NotNull(result);
        Assert.False(engine.ErrorReporter.HasErrors,
            $"xs@-1 errored: {engine.ErrorReporter.FormatErrors()}");
        Assert.Equal(3, System.Convert.ToInt32(result!.Data));
    }

    [Fact]
    public void TempoMinus_StillStandaloneMinus_NotBrokenByValueEndAdditions()
    {
        // Music-context keyword sign paths follow the KEYWORD token, not a
        // value-end token — they must keep emitting a standalone Minus so the
        // dedicated Match(Minus) parsers fire. Guards the value-end additions.
        var tokens = Lex("tempo -120 { (print 1) }");
        int tempoIdx = tokens.FindIndex(t => t.Type == TokenType.Tempo);
        Assert.True(tempoIdx >= 0, "tempo keyword not found");
        Assert.Equal(TokenType.Minus, tokens[tempoIdx + 1].Type);
        Assert.Equal(TokenType.IntLiteral, tokens[tempoIdx + 2].Type);
    }

    // ===== Bug: rest-with-duration-suffix `_q` =====

    [Theory]
    [InlineData("| _q |", "q")]
    [InlineData("| _h |", "h")]
    [InlineData("| _e |", "e")]
    [InlineData("| _w |", "w")]
    [InlineData("| _s |", "s")]
    public void RestWithDurationSuffix_LexesUnderscoreThenSuffix(string stream, string suffix)
    {
        var tokens = Lex(stream);
        int underscoreIdx = tokens.FindIndex(t => t.Type == TokenType.Underscore);
        Assert.True(underscoreIdx >= 0,
            $"expected a standalone Underscore for '{stream}', got: " +
            string.Join(", ", tokens.Select(t => $"{t.Type}({t.Text})")));
        // The suffix letter must follow as its own Identifier token.
        Assert.Equal(TokenType.Identifier, tokens[underscoreIdx + 1].Type);
        Assert.Equal(suffix, tokens[underscoreIdx + 1].Text);
    }

    [Fact]
    public void RestWithDurationSuffix_ParsesToBarNotEmptyStreamError()
    {
        using var engine = new FlowEngine(verbose: false);
        engine.Execute("use \"@std\"\ntimesig 4/4 { Sequence s = | C4q _q _ _ |\n(print (str s)) }");
        Assert.False(engine.ErrorReporter.HasErrors,
            $"`| C4q _q _ _ |` must parse, got: {engine.ErrorReporter.FormatErrors()}");
    }

    [Fact]
    public void WholeRestBar_Parses()
    {
        using var engine = new FlowEngine(verbose: false);
        engine.Execute("tempo 120 { timesig 4/4 { Sequence s = | _w | C4w |\n(print (str s)) } }");
        Assert.False(engine.ErrorReporter.HasErrors,
            $"`| _w | C4w |` must parse, got: {engine.ErrorReporter.FormatErrors()}");
    }

    [Fact]
    public void InternalMarker_DoubleUnderscore_StillLexesAsSingleIdentifier()
    {
        // The `__`-prefixed internal-marker carve-out must survive the
        // `_q`-rest split (e.g. `__enableSfzModule`).
        var tokens = Lex("__enableSfzModule");
        Assert.Contains(tokens, t => t.Type == TokenType.Identifier && t.Text == "__enableSfzModule");
        Assert.DoesNotContain(tokens, t => t.Type == TokenType.Underscore);
    }

    // ===== Bug: uppercase note-name typo in a note stream =====

    [Fact]
    public void UppercaseNoteTypo_RecoversCharitably_KeepsSurroundingNotes()
    {
        RenderingDiagnostics.ResetForTesting();
        using var engine = new FlowEngine(verbose: false);
        // `Z9` is the shape of a mistyped note name (first char outside A-G).
        // It must NOT abort the stream nor produce "Empty note stream".
        engine.Execute("timesig 4/4 { Sequence s = | C4 D4 Z9 E4 |\n(print (str s)) }");
        Assert.False(engine.ErrorReporter.HasErrors,
            $"uppercase typo must recover, not error; got: {engine.ErrorReporter.FormatErrors()}");
        // The located advisory naming the offending token must have fired. The
        // sentinel key is `note-stream-bad-note:<name>:<line>:<col>`; assert on
        // the name prefix so the test does not pin a fragile column number.
        Assert.True(
            RenderingDiagnostics.WasWarnedForTesting("note-stream-bad-note:Z9:1:36"),
            "expected a located one-shot advisory naming 'Z9' at line 1 col 36");
    }

    // ===== Regression (regression-notestream-hasb): the charitable typo recovery
    //       above must NOT be so aggressive that it (a) swallows the closing of a
    //       multi-line stream's NEXT declaration, nor (b) bypasses the hAsB pragma. =====

    [Fact]
    public void MultiLineStreams_BackToBackDeclarations_BothParseClean()
    {
        // Two consecutive multi-line note-stream declarations. Before the fix, the
        // charitable uppercase-typo branch consumed the SECOND declaration's type
        // name `Sequence` as a rest, so the first stream never terminated and the
        // parser then choked on `=` ("Unexpected token Assign"). A type name must
        // instead terminate the stream (IsEndOfNoteStream / break).
        RenderingDiagnostics.ResetForTesting();
        using var engine = new FlowEngine(verbose: false);
        engine.Execute(
            "use \"@std\"\n" +
            "Sequence a = |\n" +
            "  [E4 G4 B4]h B4q> [C4 E4 G4]q |\n" +
            "|\n" +
            "Sequence b = |\n" +
            "  [E4 G4 B4]h D5q> [E3 G3 C4]q |\n" +
            "|\n" +
            "(print \"ok-complex\")\n");
        Assert.False(engine.ErrorReporter.HasErrors,
            $"back-to-back multi-line streams must parse clean; got: {engine.ErrorReporter.FormatErrors()}");
        // The type name `Sequence` must NOT have been charitably swallowed as a typo.
        Assert.False(
            RenderingDiagnostics.WasWarnedForTesting("note-stream-bad-note:Sequence:5:1"),
            "the type keyword 'Sequence' must terminate the stream, not be eaten as a rest");
    }

    [Fact]
    public void HNote_WithoutHAsBPragma_StillRejected_NotCharitablyAccepted()
    {
        // PRAG-02 / DEFER-02 contract: `H4q` WITHOUT `enable hAsB;` reaches the
        // note-stream Identifier branch (the lexer only canonicalizes H→B when the
        // pragma is set). The charitable typo recovery must NOT swallow it as a rest
        // — an H note without the pragma must STILL be rejected (parse error).
        using var engine = new FlowEngine(verbose: false);
        engine.Execute(
            "use \"@std\"\n" +
            "Sequence seq = | H4q B4q |\n" +
            "(print (str seq))\n");
        Assert.True(engine.ErrorReporter.HasErrors,
            "H4q without 'enable hAsB;' must be rejected, not charitably rendered as a rest");
    }

    [Fact]
    public void HNote_WithHAsBPragma_StillAccepted_AsBNote()
    {
        // The companion direction: WITH `enable hAsB;`, H4q canonicalizes to B4q at
        // lex time and parses clean — the regression fix must not break the pragma's
        // accept path.
        using var engine = new FlowEngine(verbose: false);
        engine.Execute(
            "enable hAsB;\n" +
            "use \"@std\"\n" +
            "Sequence seq = | H4q B4q |\n" +
            "(print (str seq))\n");
        Assert.False(engine.ErrorReporter.HasErrors,
            $"H4q WITH 'enable hAsB;' must parse clean; got: {engine.ErrorReporter.FormatErrors()}");
    }
}
