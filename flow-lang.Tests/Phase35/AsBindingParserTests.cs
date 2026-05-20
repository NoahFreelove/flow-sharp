using FlowLang.Ast;
using FlowLang.Ast.Expressions;
using FlowLang.Ast.Statements;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using FlowLang.Parsing;
using Xunit;

namespace FlowLang.Tests.Phase35;

/// <summary>
/// Phase 35 Plan 35-07 Wave 0 — `-> CALL as name` parser gates (LANG-03).
///
/// Drives the <see cref="Parser"/> on short flow-chain sources containing
/// `as NAME` clauses, walks the resulting AST, and asserts the produced
/// <see cref="FlowExpression"/> nodes carry the expected
/// <c>IntermediateName</c> annotation.
///
/// RED state: FlowExpression has no IntermediateName field yet, and the
/// parser does not yet consume the `as` token. Task 2 adds the field;
/// Task 3 wires the parser; both must land before these tests flip GREEN.
/// </summary>
public class AsBindingParserTests
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

    /// <summary>
    /// Walks the program AST, collecting every FlowExpression node in any
    /// statement's expression slot (including those nested inside other
    /// FlowExpression chains).
    /// </summary>
    private static List<FlowExpression> CollectFlowExpressions(Program program)
    {
        var collected = new List<FlowExpression>();
        foreach (var stmt in program.Statements)
        {
            Expression? expr = stmt switch
            {
                ExpressionStatement es => es.Expression,
                VariableDeclaration vd => vd.Value,
                _ => null,
            };
            if (expr is not null) Walk(expr, collected);
        }
        return collected;
    }

    private static void Walk(Expression e, List<FlowExpression> sink)
    {
        if (e is FlowExpression fe)
        {
            sink.Add(fe);
            Walk(fe.Left, sink);
            Walk(fe.Right, sink);
        }
        else if (e is FunctionCallExpression fc)
        {
            foreach (var arg in fc.Arguments) Walk(arg, sink);
        }
    }

    [Fact]
    public void SingleAsClauseParses()
    {
        // `seq -> (transpose 2) as melody -> render` — the chain step that
        // produces the `(transpose 2)` result must carry IntermediateName=
        // "melody". Use bare identifiers so we don't need stdlib resolution
        // for the parser-side assertion.
        var program = ParseSource("seq -> (transpose 2) as melody -> render");
        var flows = CollectFlowExpressions(program);
        Assert.Contains(flows, fe => fe.IntermediateName == "melody");
    }

    [Fact]
    public void MultipleAsClausesParse()
    {
        // Two `as` annotations in the same chain — both names must appear
        // as IntermediateName on the corresponding FlowExpression nodes.
        var program = ParseSource(
            "seq -> (transpose 2) as melody -> (legato 0.5) as legatoMelody -> render");
        var flows = CollectFlowExpressions(program);
        var names = flows
            .Where(fe => fe.IntermediateName != null)
            .Select(fe => fe.IntermediateName!)
            .ToList();
        Assert.Contains("melody", names);
        Assert.Contains("legatoMelody", names);
    }

    [Fact]
    public void AsRequiresIdentifierAfter()
    {
        // `as 42` — the `as` keyword must be followed by an Identifier, not
        // a literal. The parser reports an error; tolerant about exact
        // diagnostic shape.
        var reporter = new ErrorReporter();
        var lexer = new SimpleLexer("seq -> (f) as 42 -> render", reporter);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens, reporter);
        parser.Parse();
        Assert.True(
            reporter.HasErrors,
            "Expected a parse error when `as` is followed by a non-identifier; got none. " +
            "Errors: " + reporter.FormatErrors());
    }
}
