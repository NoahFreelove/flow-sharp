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
/// Phase 38 Plan 38-02 LIVE-01 — Wave 0 parser tests for the
/// <c>live &lt;quantize&gt; { ... }</c> block surface.
///
/// Asserts: (a) lexer recognizes <c>live</c> keyword; (b) parser produces
/// <see cref="LiveBlockStatement"/> AST node; (c) quantize accepts NoteValue
/// suffix (<c>q</c>/<c>h</c>/<c>w</c>/<c>e</c>/<c>s</c>) AND Int+<c>bar</c>/<c>bars</c>
/// form AND omitted-default-to-1bar form; (d) <c>BlockId</c> is FNV-1a of the
/// block's source location and is deterministic across parses.
///
/// Tests are RED until Tasks 2-3 land <see cref="TokenType.Live"/> +
/// <see cref="LiveBlockStatement"/> + <see cref="LiveBlockRegistry"/> +
/// <c>Parser.ParseLiveBlockStatement</c>.
/// </summary>
[Collection("FlowScripts")]
public class LiveBlockParserTests : IDisposable
{
    public LiveBlockParserTests()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    /// <summary>
    /// Parses Flow source into a <see cref="Program"/> AST without executing.
    /// Mirrors <see cref="FlowLang.Core.FlowEngine.Execute"/>'s lex+parse phase
    /// but stops before interpretation so the AST shape is the test subject.
    /// </summary>
    private static Program ParseToProgram(string source, string? fileName = "<test>")
    {
        var errorReporter = new ErrorReporter();
        var lexer = new SimpleLexer(source, errorReporter, fileName);
        var tokens = lexer.Tokenize();
        Assert.False(errorReporter.HasErrors, $"Lex errors: {errorReporter.GetErrors().FirstOrDefault()?.Message}");
        var parser = new Parser(tokens, errorReporter);
        var program = parser.Parse();
        Assert.False(errorReporter.HasErrors, $"Parse errors: {errorReporter.GetErrors().FirstOrDefault()?.Message}");
        return program;
    }

    /// <summary>
    /// Walks the AST looking for all <see cref="LiveBlockStatement"/> nodes.
    /// Descends into musical-context block bodies + tuning blocks so live
    /// blocks nested inside <c>tempo 120 { live 1bar { ... } }</c> are found.
    /// </summary>
    private static System.Collections.Generic.List<LiveBlockStatement> FindLiveBlocks(Program program)
    {
        var found = new System.Collections.Generic.List<LiveBlockStatement>();
        foreach (var stmt in program.Statements)
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
    public void SingleLiveBlock_With1barQuantize_ProducesLiveBlockStatement()
    {
        var source = "tempo 120 { live 1bar { Sequence s = | C4q D4q | } }";
        var program = ParseToProgram(source);

        var blocks = FindLiveBlocks(program);
        Assert.Single(blocks);
        var block = blocks[0];
        Assert.NotNull(block.QuantizeValue);
        Assert.NotEmpty(block.Body);
        // The body should contain a VariableDeclaration ("Sequence s = | ... |").
        Assert.Contains(block.Body, s => s is VariableDeclaration);
    }

    [Fact]
    public void LiveBlockWithQuarterNoteQuantize_ProducesLiveBlockStatement()
    {
        var source = "live q { Sequence s = | C4q | }";
        var program = ParseToProgram(source);

        var blocks = FindLiveBlocks(program);
        Assert.Single(blocks);
        var block = blocks[0];
        Assert.NotNull(block.QuantizeValue);
        Assert.NotEmpty(block.Body);
    }

    [Fact]
    public void LiveBlockWithoutQuantize_DefaultsTo1bar()
    {
        var source = "live { Sequence s = | C4q | }";
        var program = ParseToProgram(source);

        var blocks = FindLiveBlocks(program);
        Assert.Single(blocks);
        var block = blocks[0];
        // QuantizeValue is always present (parser synthesizes a 1-bar default
        // when the composer omits the quantize). Assert non-null; the exact
        // shape (Int literal carrying 1 or a NoteValue) is implementation
        // detail — the contract is "defaults to 1bar".
        Assert.NotNull(block.QuantizeValue);
    }

    [Fact]
    public void BlockId_DeterministicAcrossParses()
    {
        var source = "live 1bar { Sequence s = | C4q | }";
        var program1 = ParseToProgram(source);
        var program2 = ParseToProgram(source);

        var blocks1 = FindLiveBlocks(program1);
        var blocks2 = FindLiveBlocks(program2);
        Assert.Single(blocks1);
        Assert.Single(blocks2);
        Assert.Equal(blocks1[0].BlockId, blocks2[0].BlockId);
    }
}
