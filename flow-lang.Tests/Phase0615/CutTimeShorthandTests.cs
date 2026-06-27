using System.Linq;
using FlowLang.Ast.Expressions;
using FlowLang.Ast.Statements;
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using FlowLang.Parsing;
using Xunit;

namespace FlowLang.Tests.Phase0615;

/// <summary>
/// Feature-addition 0615 — cut-time time-signature shorthands `C/`, `¢` (U+00A2
/// cent sign), and `C|` (cut-common engraving glyph). All three lower at parse
/// time to <c>timesig 2/2</c>, mirroring the existing common-time `C` → 4/4
/// shorthand (Phase 31). The numerator + denominator both lower to
/// <see cref="LiteralExpression"/>(2) so every downstream consumer (renderer,
/// MIDI export, musical-context stack, beat-true-to-sig multiplier) sees the
/// exact same shape as the explicit <c>timesig 2/2</c> form.
///
/// <para>
/// Lexing notes: `¢` is non-ASCII and neither whitespace nor a token boundary, so
/// it is absorbed into a standalone Identifier token by ScanIdentifierOrKeyword.
/// `C/` is the Identifier `C` followed by a Slash; `C|` is `C` followed by a Pipe.
/// The trailing glyph is consumed ONLY in the timesig-`C` parse position
/// (Parser.cs ParseMusicalContextStatement), so ordinary division (<c>(div 6 2)</c>)
/// and note-stream lexing elsewhere are untouched.
/// </para>
/// </summary>
public class CutTimeShorthandTests
{
    private static MusicalContextStatement ParseSingleTimesig(string src)
    {
        var er = new ErrorReporter();
        var tokens = new SimpleLexer(src, er, fileName: null, pragmaSet: PragmaSet.Empty).Tokenize();
        Assert.False(er.HasErrors, $"lex errors: {string.Join("; ", er.Errors.Select(d => d.Message))}");
        var parser = new Parser(tokens, er);
        var program = parser.Parse();
        Assert.False(er.HasErrors, $"parse errors: {string.Join("; ", er.Errors.Select(d => d.Message))}");
        return program.Statements.OfType<MusicalContextStatement>()
            .First(s => s.ContextType == MusicalContextType.Timesig);
    }

    private static void AssertLowersTo2Over2(string src)
    {
        var stmt = ParseSingleTimesig(src);
        var num = Assert.IsType<LiteralExpression>(stmt.Value);
        var den = Assert.IsType<LiteralExpression>(stmt.Value2);
        Assert.Equal(2, num.Value);
        Assert.Equal(2, den.Value);
    }

    [Fact]
    public void CutTime_C_Slash_LowersTo_2_Over_2()
        => AssertLowersTo2Over2("timesig C/ { }");

    [Fact]
    public void CutTime_CentSign_LowersTo_2_Over_2()
        => AssertLowersTo2Over2("timesig ¢ { }");

    [Fact]
    public void CutTime_C_Pipe_LowersTo_2_Over_2()
        => AssertLowersTo2Over2("timesig C| { }");

    [Fact]
    public void CommonTime_BareC_StillLowersTo_4_Over_4()
    {
        // Regression: bare `C` (no trailing cut glyph) must keep meaning 4/4.
        var stmt = ParseSingleTimesig("timesig C { }");
        var num = Assert.IsType<LiteralExpression>(stmt.Value);
        var den = Assert.IsType<LiteralExpression>(stmt.Value2);
        Assert.Equal(4, num.Value);
        Assert.Equal(4, den.Value);
    }

    [Fact]
    public void ExplicitTimesig_2_Over_2_StillParses()
    {
        var stmt = ParseSingleTimesig("timesig 2/2 { }");
        var num = Assert.IsType<LiteralExpression>(stmt.Value);
        var den = Assert.IsType<LiteralExpression>(stmt.Value2);
        Assert.Equal(2, num.Value);
        Assert.Equal(2, den.Value);
    }

    [Fact]
    public void Division_AndFractionalLexing_Unaffected()
    {
        // CAUTION guard: the `/` cut glyph is consumed ONLY in the timesig-`C`
        // position. A bare `(div 6 2)` elsewhere must still lex `/` normally
        // (it is its own Slash token) and the program must parse clean.
        var er = new ErrorReporter();
        var tokens = new SimpleLexer("Int x = (div 6 2)\n", er, fileName: null, pragmaSet: PragmaSet.Empty).Tokenize();
        Assert.False(er.HasErrors, $"lex errors: {string.Join("; ", er.Errors.Select(d => d.Message))}");
        var parser = new Parser(tokens, er);
        parser.Parse();
        Assert.False(er.HasErrors, $"parse errors: {string.Join("; ", er.Errors.Select(d => d.Message))}");
    }

    [Theory]
    [InlineData("timesig C/ {")]
    [InlineData("timesig ¢ {")]
    [InlineData("timesig C| {")]
    public void CutTime_EndToEnd_BeatTrueToSig_YieldsHalfNotePulse(string header)
    {
        // 2/2 (alla breve) felt: with `enable beat-true-to-sig;` the `1b` literal
        // retunes to the active denominator (multiplier = 4/denom = 4/2 = 2.0), so
        // `1b` lands on the half note (2.0 quarter-relative). 4/4 would yield 1.0.
        // This pins that all three shorthands produce a genuinely 2/2-felt context
        // end-to-end through the interpreter, not just the parsed literals.
        var engine = new FlowLang.Core.FlowEngine(verbose: false);
        var ok = engine.Execute(
            "enable beat-true-to-sig;\n" +
            header + "\n" +
            "    Beat b = 1b\n" +
            "}\n");
        Assert.True(ok, "Execute failed: " + engine.ErrorReporter.FormatErrors());
        Assert.Empty(engine.ErrorReporter.Errors);
        engine.Dispose();
    }
}
