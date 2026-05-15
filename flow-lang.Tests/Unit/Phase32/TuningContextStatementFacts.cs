using System.Linq;
using FlowLang.Ast.Expressions;
using FlowLang.Ast.Statements;
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using FlowLang.Parsing;
using Xunit;

namespace FlowLang.Tests.Unit.Phase32;

/// <summary>
/// Phase 32 Plan 32-06 Task 1 — parser-level Facts that the new
/// <c>tuning &lt;expr&gt; { ... }</c> musical-context block parses to a
/// <see cref="TuningContextStatement"/> AST node for all three CONTEXT D-15
/// composer surfaces:
///
/// <list type="bullet">
///   <item>identifier — <c>tuning partch { }</c></item>
///   <item>inline call — <c>tuning (loadScala "x.scl") { }</c></item>
///   <item>string-literal sugar — <c>tuning "x.scl" { }</c> (desugars at parse
///   time to <c>(loadScala "x.scl")</c> per D-15)</item>
/// </list>
///
/// Threat T-32-AST mitigation: the desugared <see cref="FunctionCallExpression"/>
/// MUST carry the SourceLocation of the user's typed <c>tuning</c> keyword line,
/// NOT a synthetic frame — so runtime errors point at the user's source line.
///
/// These Facts operate at the lexer + parser layer (no interpreter, no
/// FlowEngineRunner) — fast, hermetic. Pattern mirrored on
/// <c>Phase31.CommonTimeShorthandTests.ParseSingleTimesig</c>.
/// </summary>
public class TuningContextStatementFacts
{
    private static (FlowLang.Ast.Program program, ErrorReporter reporter) ParseSource(string src)
    {
        var er = new ErrorReporter();
        var tokens = new SimpleLexer(src, er, fileName: null, pragmaSet: PragmaSet.Empty).Tokenize();
        var parser = new Parser(tokens, er);
        var program = parser.Parse();
        return (program, er);
    }

    private static TuningContextStatement ParseSingleTuningBlock(string src)
    {
        var (program, er) = ParseSource(src);
        Assert.False(er.HasErrors,
            $"unexpected parse errors: {string.Join("; ", er.Errors.Select(d => d.Message))}");
        return program.Statements.OfType<TuningContextStatement>().First();
    }

    [Fact]
    public void Parse_TuningWithIdentifier_ProducesVariableExpression()
    {
        var stmt = ParseSingleTuningBlock("tuning partch { }");
        var varExpr = Assert.IsType<VariableExpression>(stmt.TuningExpr);
        Assert.Equal("partch", varExpr.Name);
        Assert.Empty(stmt.Body);
    }

    [Fact]
    public void Parse_TuningWithInlineCall_ProducesFunctionCallExpression()
    {
        var stmt = ParseSingleTuningBlock("tuning (loadScala \"x.scl\") { }");
        var callExpr = Assert.IsType<FunctionCallExpression>(stmt.TuningExpr);
        Assert.Equal("loadScala", callExpr.Name);
        Assert.Single(callExpr.Arguments);
        var arg = Assert.IsType<LiteralExpression>(callExpr.Arguments[0]);
        Assert.Equal("x.scl", arg.Value);
    }

    [Fact]
    public void Parse_TuningWithStringLiteral_DesugarsToLoadScalaCall()
    {
        // D-15 string-literal sugar: `tuning "x.scl" { }` desugars at parse time
        // to a FunctionCallExpression for `loadScala`.
        var stmt = ParseSingleTuningBlock("tuning \"x.scl\" { }");
        var callExpr = Assert.IsType<FunctionCallExpression>(stmt.TuningExpr);
        Assert.Equal("loadScala", callExpr.Name);
        Assert.Single(callExpr.Arguments);
        var arg = Assert.IsType<LiteralExpression>(callExpr.Arguments[0]);
        Assert.Equal("x.scl", arg.Value);
    }

    [Fact]
    public void Parse_TuningStringLiteralDesugar_PreservesSourceLocation()
    {
        // T-32-AST mitigation Fact: the desugared FunctionCallExpression's
        // SourceLocation MUST be the line of the user's typed `tuning`
        // keyword (line 3 here), NOT a synthetic frame or SourceLocation.Unknown.
        // Two leading blank lines push the `tuning` keyword to line 3.
        const string src = "\n\ntuning \"x.scl\" { }\n";
        var stmt = ParseSingleTuningBlock(src);
        var callExpr = Assert.IsType<FunctionCallExpression>(stmt.TuningExpr);
        Assert.Equal(3, callExpr.Location.Line);
        // Sanity: the TuningContextStatement itself also anchors at line 3.
        Assert.Equal(3, stmt.Location.Line);
        // The LiteralExpression argument should carry the line of the literal
        // (also line 3 since it's on the same line as `tuning`).
        var arg = Assert.IsType<LiteralExpression>(callExpr.Arguments[0]);
        Assert.Equal(3, arg.Location.Line);
    }

    [Fact]
    public void Parse_TuningWithBody_CollectsBodyStatements()
    {
        var stmt = ParseSingleTuningBlock("tuning partch { Int x = 5 }");
        Assert.Single(stmt.Body);
        Assert.IsType<VariableDeclaration>(stmt.Body[0]);
    }

    [Fact]
    public void Parse_NestedTuningInsideTempo_BothNodesPresent()
    {
        // tempo 120 { tuning partch { } } → MusicalContextStatement(Tempo) wrapping
        // a TuningContextStatement.
        var (program, er) = ParseSource("tempo 120 { tuning partch { } }");
        Assert.False(er.HasErrors,
            $"unexpected parse errors: {string.Join("; ", er.Errors.Select(d => d.Message))}");
        var tempoStmt = program.Statements.OfType<MusicalContextStatement>()
            .First(s => s.ContextType == MusicalContextType.Tempo);
        Assert.Single(tempoStmt.Body);
        var inner = Assert.IsType<TuningContextStatement>(tempoStmt.Body[0]);
        var varExpr = Assert.IsType<VariableExpression>(inner.TuningExpr);
        Assert.Equal("partch", varExpr.Name);
    }

    [Fact]
    public void Parse_TuningWithoutExpr_RaisesError()
    {
        // `tuning { }` is missing the required expression after `tuning`.
        var (_, er) = ParseSource("tuning { }");
        Assert.True(er.HasErrors,
            "expected a parse error for `tuning { }` (missing expression)");
    }
}
