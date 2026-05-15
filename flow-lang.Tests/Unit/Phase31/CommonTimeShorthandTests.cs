using System.Linq;
using FlowLang.Ast.Expressions;
using FlowLang.Ast.Statements;
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using FlowLang.Parsing;
using Xunit;

namespace FlowLang.Tests.Unit.Phase31;

/// <summary>
/// Phase 31 Plan 31-08 expansion — common-time shorthand `C` for time signatures.
///
/// `timesig C { ... }` desugars at parse time to `timesig 4/4 { ... }`. The
/// shorthand is convention-driven (music notation has used `C` for common time
/// since the late medieval period) and avoids the awkward `timesig 4/4` form for
/// the overwhelmingly common case. The numerator and denominator both lower to
/// `LiteralExpression(4)` so every downstream consumer (renderer, MIDI export,
/// musical-context stack) sees the same shape as the explicit form.
///
/// Out of scope (intentional): `cut`/`Ȼ` shorthand for 2/2, lowercase `c`,
/// trailing-`o` (`C.` for compound time). Composer writes `2/2` explicitly.
/// </summary>
public class CommonTimeShorthandTests
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

    [Fact]
    public void TimesigC_LowersTo_4_Over_4()
    {
        var stmt = ParseSingleTimesig("timesig C { }");
        var num = Assert.IsType<LiteralExpression>(stmt.Value);
        var den = Assert.IsType<LiteralExpression>(stmt.Value2);
        Assert.Equal(4, num.Value);
        Assert.Equal(4, den.Value);
    }

    [Fact]
    public void ExplicitTimesig_4_Over_4_StillParses()
    {
        // Backward-compat: the existing `timesig 4/4` form must still produce
        // the same LiteralExpression(4)/LiteralExpression(4) shape.
        var stmt = ParseSingleTimesig("timesig 4/4 { }");
        var num = Assert.IsType<LiteralExpression>(stmt.Value);
        var den = Assert.IsType<LiteralExpression>(stmt.Value2);
        Assert.Equal(4, num.Value);
        Assert.Equal(4, den.Value);
    }

    [Fact]
    public void ExplicitTimesig_NonCommon_StillParses()
    {
        // 7/8 has nothing to do with the C shorthand; the new guard must not
        // shadow the existing numeric path.
        var stmt = ParseSingleTimesig("timesig 7/8 { }");
        var num = Assert.IsType<LiteralExpression>(stmt.Value);
        var den = Assert.IsType<LiteralExpression>(stmt.Value2);
        Assert.Equal(7, num.Value);
        Assert.Equal(8, den.Value);
    }

    [Fact]
    public void TimesigLowercaseC_DoesNotLower_RaisesParseError()
    {
        // Music notation uses capital `C` exclusively for common time. A
        // lowercase `c` should fall through to the IntLiteral path and error.
        var er = new ErrorReporter();
        var tokens = new SimpleLexer("timesig c { }", er, fileName: null, pragmaSet: PragmaSet.Empty).Tokenize();
        var parser = new Parser(tokens, er);
        parser.Parse();
        Assert.True(er.HasErrors, "lowercase `c` should not be accepted as common-time shorthand");
        Assert.Contains(er.Errors, d => d.Message.Contains("Expected integer numerator"));
    }
}
