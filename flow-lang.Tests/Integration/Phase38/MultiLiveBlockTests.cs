using System;
using System.Linq;
using FlowLang.Ast;
using FlowLang.Ast.Statements;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using FlowLang.Parsing;
using FlowLang.Runtime;
using Xunit;

namespace FlowLang.Tests.Integration.Phase38;

/// <summary>
/// Phase 38 Plan 38-02 D-38-02 — Wave 0 tests for multi-block independent swap.
///
/// Asserts: multiple <c>live</c> blocks in a single file each receive a
/// distinct <see cref="LiveBlockStatement.BlockId"/> AND each registers
/// independently in <see cref="ExecutionContext.LiveBlockRegistry"/> at
/// interpretation time, so Plan 38-03's per-block swap callback can address
/// them by ID.
/// </summary>
[Collection("FlowScripts")]
public class MultiLiveBlockTests : IDisposable
{
    public MultiLiveBlockTests()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    private static System.Collections.Generic.List<LiveBlockStatement> CollectLiveBlocks(
        System.Collections.Generic.IEnumerable<Statement> stmts)
    {
        var found = new System.Collections.Generic.List<LiveBlockStatement>();
        foreach (var stmt in stmts)
            WalkStatement(stmt, found);
        return found;
    }

    private static void WalkStatement(Statement stmt, System.Collections.Generic.List<LiveBlockStatement> found)
    {
        switch (stmt)
        {
            case LiveBlockStatement live:
                found.Add(live);
                foreach (var body in live.Body)
                    WalkStatement(body, found);
                break;
            case MusicalContextStatement ctx:
                foreach (var body in ctx.Body)
                    WalkStatement(body, found);
                break;
            case TuningContextStatement tctx:
                foreach (var body in tctx.Body)
                    WalkStatement(body, found);
                break;
        }
    }

    [Fact]
    public void TwoLiveBlocksInOneFile_GetDistinctBlockIds()
    {
        // Two live blocks on different lines — BlockId is FNV-1a of SourceLocation
        // so distinct source positions MUST produce distinct ids (D-38-02).
        var source =
            "tempo 120 {\n" +
            "  live 1bar { Sequence a = | C4q | }\n" +
            "  live 2bar { Sequence b = | E4q | }\n" +
            "}\n";

        var errorReporter = new ErrorReporter();
        var lexer = new SimpleLexer(source, errorReporter, "<test>");
        var tokens = lexer.Tokenize();
        Assert.False(errorReporter.HasErrors);
        var parser = new Parser(tokens, errorReporter);
        var program = parser.Parse();
        Assert.False(errorReporter.HasErrors);

        var blocks = CollectLiveBlocks(program.Statements);
        Assert.Equal(2, blocks.Count);
        Assert.NotEqual(blocks[0].BlockId, blocks[1].BlockId);
    }

    [Fact]
    public void TwoLiveBlocksInOneFile_BothRegisteredInLiveBlockRegistry()
    {
        var source =
            "tempo 120 {\n" +
            "  live 1bar { Sequence a = | C4q | }\n" +
            "  live 2bar { Sequence b = | E4q | }\n" +
            "}\n";

        using var engine = new FlowLang.Core.FlowEngine();
        var ok = engine.Execute(source, "<test>");
        Assert.True(ok, "FlowEngine.Execute should succeed");

        var snapshot = engine.Context.LiveBlockRegistry.Snapshot();
        Assert.Equal(2, snapshot.Count);
    }
}
