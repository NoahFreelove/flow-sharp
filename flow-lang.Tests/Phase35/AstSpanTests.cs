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
/// Phase 35 LANG-04 Wave 1 — AST Span population gates.
///
/// Walks the Program produced by <see cref="Parser"/> and asserts every
/// <see cref="Expression"/> and <see cref="Statement"/> node carries a
/// non-Unknown <c>Span</c>. Verifies Pitfall 1's audit (no construction
/// site escapes the migration sweep).
/// </summary>
public class AstSpanTests
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

    private static FlowLang.Core.Span? GetSpan(AstNode node)
    {
        // Span lives as a public property on every derived record per Wave 1.
        // Use reflection to extract it generically across all record subtypes
        // — keeps the walker compact without enumerating every record type.
        // Fully-qualify `Span` because System.Span<T> would otherwise shadow.
        var prop = node.GetType().GetProperty("Span");
        if (prop is null) return null;
        return prop.GetValue(node) as FlowLang.Core.Span;
    }

    private static IEnumerable<AstNode> WalkNodes(Program program)
    {
        foreach (var stmt in program.Statements)
            foreach (var node in WalkStatement(stmt))
                yield return node;
    }

    private static IEnumerable<AstNode> WalkStatement(Statement stmt)
    {
        yield return stmt;
        switch (stmt)
        {
            case ExpressionStatement es:
                foreach (var n in WalkExpression(es.Expression)) yield return n;
                break;
            case VariableDeclaration vd:
                foreach (var n in WalkExpression(vd.Value)) yield return n;
                break;
            case AssignmentStatement asg:
                foreach (var n in WalkExpression(asg.Value)) yield return n;
                break;
            case ReturnStatement rs:
                foreach (var n in WalkExpression(rs.Value)) yield return n;
                break;
            case ProcDeclaration pd:
                foreach (var inner in pd.Body)
                    foreach (var n in WalkStatement(inner)) yield return n;
                break;
            case SectionDeclaration sd:
                foreach (var inner in sd.Body)
                    foreach (var n in WalkStatement(inner)) yield return n;
                break;
            case MusicalContextStatement mc:
                foreach (var n in WalkExpression(mc.Value)) yield return n;
                if (mc.Value2 is not null)
                    foreach (var n in WalkExpression(mc.Value2)) yield return n;
                foreach (var inner in mc.Body)
                    foreach (var n in WalkStatement(inner)) yield return n;
                break;
            case ForStatement fs:
                foreach (var n in WalkExpression(fs.Collection)) yield return n;
                foreach (var inner in fs.Body)
                    foreach (var n in WalkStatement(inner)) yield return n;
                break;
            case WhileStatement ws:
                foreach (var n in WalkExpression(ws.Condition)) yield return n;
                foreach (var inner in ws.Body)
                    foreach (var n in WalkStatement(inner)) yield return n;
                break;
            case TupleDestructureStatement tds:
                foreach (var n in WalkExpression(tds.Value)) yield return n;
                break;
            case TuningContextStatement tcs:
                foreach (var n in WalkExpression(tcs.TuningExpr)) yield return n;
                foreach (var inner in tcs.Body)
                    foreach (var n in WalkStatement(inner)) yield return n;
                break;
        }
    }

    private static IEnumerable<AstNode> WalkExpression(Expression expr)
    {
        yield return expr;
        switch (expr)
        {
            case FunctionCallExpression fc:
                foreach (var a in fc.Arguments)
                    foreach (var n in WalkExpression(a)) yield return n;
                break;
            case FlowExpression fe:
                foreach (var n in WalkExpression(fe.Left)) yield return n;
                foreach (var n in WalkExpression(fe.Right)) yield return n;
                break;
            case TupleUnpackFlowExpression tu:
                foreach (var n in WalkExpression(tu.Left)) yield return n;
                foreach (var n in WalkExpression(tu.Right)) yield return n;
                break;
            case ArrayLiteralExpression al:
                foreach (var e in al.Elements)
                    foreach (var n in WalkExpression(e)) yield return n;
                break;
            case ArrayIndexExpression ai:
                foreach (var n in WalkExpression(ai.Array)) yield return n;
                foreach (var n in WalkExpression(ai.Index)) yield return n;
                break;
            case TupleLiteralExpression tl:
                foreach (var e in tl.Elements)
                    foreach (var n in WalkExpression(e)) yield return n;
                break;
            case MemberAccessExpression ma:
                foreach (var n in WalkExpression(ma.Object)) yield return n;
                break;
            case LazyExpression le:
                foreach (var n in WalkExpression(le.InnerExpression)) yield return n;
                break;
            case InterpolatedStringExpression ise:
                foreach (var p in ise.Parts)
                    foreach (var n in WalkExpression(p)) yield return n;
                break;
            case LambdaExpression lam:
                foreach (var inner in lam.Body)
                    foreach (var n in WalkStatement(inner)) yield return n;
                break;
        }
    }

    [Fact]
    public void EveryAstNodeHasNonUnknownSpanAfterParse()
    {
        var program = ParseSource("Int x = (add 1 2); (print x)");
        var visited = 0;
        foreach (var node in WalkNodes(program))
        {
            var span = GetSpan(node);
            Assert.NotNull(span);
            Assert.NotEqual(Span.Unknown, span);
            visited++;
        }
        Assert.True(visited > 0, "Expected at least one AST node to be visited");
    }

    [Fact]
    public void NestedExpressionSpansParentBracketsChild()
    {
        // Parse `(add (mul 2 3) 4)`. The outer FunctionCallExpression must
        // span from the open paren through the close paren of the outer
        // call; the inner (mul 2 3) call carries its own narrower Span.
        var program = ParseSource("(add (mul 2 3) 4)");
        // Statement is an ExpressionStatement wrapping the outer call.
        var es = Assert.IsType<ExpressionStatement>(program.Statements[0]);
        var outer = Assert.IsType<FunctionCallExpression>(es.Expression);
        var outerSpan = GetSpan(outer);
        Assert.NotNull(outerSpan);
        Assert.NotEqual(Span.Unknown, outerSpan);

        // Outer span starts at column 1 (the open paren) — back-compat invariant.
        Assert.Equal(outer.Location, outerSpan!.Start);

        // The inner call's span must lie strictly INSIDE the outer span
        // (start ≥ outer.start, end ≤ outer.end).
        Assert.Equal("mul", ((FunctionCallExpression)outer.Arguments[0]).Name);
        var inner = (FunctionCallExpression)outer.Arguments[0];
        var innerSpan = GetSpan(inner);
        Assert.NotNull(innerSpan);
        Assert.NotEqual(Span.Unknown, innerSpan);
        Assert.True(innerSpan!.Start.Column >= outerSpan.Start.Column,
            $"inner start column {innerSpan.Start.Column} < outer start column {outerSpan.Start.Column}");
        Assert.True(innerSpan.End.Column <= outerSpan.End.Column,
            $"inner end column {innerSpan.End.Column} > outer end column {outerSpan.End.Column}");
    }
}
