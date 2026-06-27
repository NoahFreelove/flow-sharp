using FlowLang.Ast;
using FlowLang.Ast.Expressions;
using FlowLang.Ast.Patterns;
using FlowLang.Ast.Statements;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using FlowLang.Parsing;
using Xunit;

namespace FlowLang.Tests.Phase35;

/// <summary>
/// Phase 35 Plan 35-05 Wave 0 — match-form parser gates (LANG-01).
///
/// Drives the <see cref="Parser"/> on small (match ...) sources and walks
/// the resulting AST to assert the expected <see cref="MatchExpression"/>
/// shape with the correct mix of pattern types per arm.
///
/// Also pins Pitfall 2 (note-stream `|` disambiguation): a top-level
/// `Sequence s = | C4 D4 |` continues to produce a NoteStreamExpression
/// after the match-arm `|` handling lands.
///
/// RED state: Pattern AST records do not yet exist; the file fails to
/// compile until Task 2 + 3 land the records, the lexer keywords, and
/// the ParseMatch/ParsePattern parser entry points.
/// </summary>
public class MatchParserTests
{
    private static Program ParseSource(string source)
    {
        var reporter = new ErrorReporter();
        var lexer = new SimpleLexer(source, reporter);
        var tokens = lexer.Tokenize();
        Assert.False(reporter.HasErrors, $"Lexer errors: {reporter.FormatErrors()}");
        var parser = new Parser(tokens, reporter);
        var program = parser.Parse();
        Assert.False(reporter.HasErrors, $"Parser errors: {reporter.FormatErrors()}");
        return program;
    }

    private static MatchExpression FirstMatch(Program program)
    {
        foreach (var stmt in program.Statements)
        {
            var expr = stmt switch
            {
                ExpressionStatement es => es.Expression,
                VariableDeclaration vd => vd.Value,
                _ => null,
            };
            if (expr is MatchExpression m) return m;
            // Drill through obvious wrappers.
            if (expr is FlowExpression fe && fe.Left is MatchExpression ml) return ml;
        }
        throw new Xunit.Sdk.XunitException("Expected a MatchExpression at top level.");
    }

    [Fact]
    public void SimpleMatchParses()
    {
        // 3 arms — two LiteralPatterns and a final WildcardPattern.
        var program = ParseSource("(match x | 1 => \"one\" | 2 => \"two\" | _ => \"other\")");
        var match = FirstMatch(program);
        Assert.Equal(3, match.Arms.Count);
        Assert.IsType<LiteralPattern>(match.Arms[0].Pattern);
        Assert.IsType<LiteralPattern>(match.Arms[1].Pattern);
        Assert.IsType<WildcardPattern>(match.Arms[2].Pattern);
    }

    [Fact]
    public void BindingPatternParses()
    {
        // Bare identifier as a pattern captures the scrutinee.
        var program = ParseSource("(match x | n => n)");
        var match = FirstMatch(program);
        Assert.Single(match.Arms);
        var bp = Assert.IsType<BindingPattern>(match.Arms[0].Pattern);
        Assert.Equal("n", bp.Name);
    }

    [Fact]
    public void GuardPatternParses()
    {
        // `x when (gt x 0)` — BindingPattern wrapped in a GuardPattern.
        var program = ParseSource("(match n | x when (gt x 0) => \"pos\" | _ => \"neg\")");
        var match = FirstMatch(program);
        Assert.Equal(2, match.Arms.Count);
        var gp = Assert.IsType<GuardPattern>(match.Arms[0].Pattern);
        var inner = Assert.IsType<BindingPattern>(gp.Inner);
        Assert.Equal("x", inner.Name);
        Assert.IsType<FunctionCallExpression>(gp.GuardExpression);
    }

    [Fact]
    public void NoteStreamStillParsesOutsideMatch()
    {
        // Pitfall 2 regression — top-level `| C4 D4 |` outside any (match ...)
        // still produces a NoteStreamExpression. The ParseMatch arm-delimiter
        // pipe handling MUST NOT touch this path.
        var program = ParseSource("Sequence s = | C4 D4 E4 |");
        var vd = Assert.IsType<VariableDeclaration>(program.Statements[0]);
        Assert.IsType<NoteStreamExpression>(vd.Value);
    }
}
