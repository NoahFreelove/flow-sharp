using System.Linq;
using FlowLang.Ast.Statements;
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using FlowLang.Parsing;
using Xunit;

namespace FlowLang.Tests.Integration.Phase44;

/// <summary>
/// Phase 44 Plan 44-02 Task 1 — Facts pinning the parse-time capture of the
/// declaring file's <c>enable strict;</c> pragma onto every
/// <see cref="ProcDeclaration"/> node via the new trailing
/// <c>IsStrict</c> field (D-02 / D-03).
///
/// <para>
/// The Parser threads <c>_pragmaSet?.Has("strict") ?? false</c> at the
/// single <c>new ProcDeclaration(...)</c> construction site in
/// <c>flow-lang/Parsing/Parser.cs:384</c>, mirroring the Phase 35
/// <c>MatchExpression.CapturedPragmas: _pragmaSet</c> idiom at
/// <c>Parser.cs:1794</c> (the closest in-tree analog).
/// </para>
///
/// <para>
/// Pragma composition (Pitfall 8): a file declaring BOTH <c>enable strict;</c>
/// AND another pragma (e.g. <c>enable justIntonation;</c>) must continue to
/// carry both bits — capturing the strict bit per-proc must not clobber the
/// program-level PragmaSet, and every ProcDeclaration declared in that file
/// must carry <c>IsStrict == true</c>.
/// </para>
/// </summary>
[Trait("Category", Phase44TestCategory.Phase44)]
[Collection("FlowScripts")]
public class ProcDeclarationStrictAstTests
{
    /// <summary>
    /// Parse <paramref name="source"/> through the full PragmaScanner +
    /// SimpleLexer + Parser pipeline so the parser's <c>_pragmaSet</c> field
    /// matches what FlowEngine.Execute would see in production (the only path
    /// where <c>ProcDeclaration.IsStrict</c> can be set non-default).
    /// </summary>
    private static Ast.Program ParseToProgram(string source, string fileName = "<test>")
    {
        var reporter = new ErrorReporter();
        var (pragmaSet, transformedSource) = PragmaScanner.Scan(source, fileName, reporter);
        var lexer = new SimpleLexer(transformedSource, reporter, fileName, pragmaSet);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens, reporter, pragmaSet);
        var program = parser.Parse();
        Assert.False(
            reporter.HasErrors,
            $"parse failed for source:\n{source}\nerrors:\n{reporter.FormatErrors()}");
        return program;
    }

    [Fact]
    public void Fact_StrictPragma_SetsProcIsStrictTrue()
    {
        var program = ParseToProgram(
            "enable strict;\nproc foo ()\n    1\nend proc\n");
        var proc = program.Statements.OfType<ProcDeclaration>().Single();
        Assert.True(
            proc.IsStrict,
            "ProcDeclaration.IsStrict must be true when declaring file has `enable strict;` (Phase 44 D-02/D-03).");
    }

    [Fact]
    public void Fact_NoStrictPragma_LeavesIsStrictFalse()
    {
        var program = ParseToProgram("proc foo ()\n    1\nend proc\n");
        var proc = program.Statements.OfType<ProcDeclaration>().Single();
        Assert.False(
            proc.IsStrict,
            "ProcDeclaration.IsStrict must default to false when no `enable strict;` pragma is present.");
    }

    [Fact]
    public void Fact_StrictPlusJustIntonation_BothPragmasCompose()
    {
        // Pitfall 8 — capturing the strict bit per-proc must not clobber
        // the program-level PragmaSet. Both pragmas continue to apply.
        var program = ParseToProgram(
            "enable strict;\nenable justIntonation;\nproc foo ()\n    1\nend proc\n");

        var proc = program.Statements.OfType<ProcDeclaration>().Single();
        Assert.True(
            proc.IsStrict,
            "ProcDeclaration.IsStrict must be true even when other pragmas are also enabled.");
        Assert.True(
            program.Pragmas.Has("justIntonation"),
            "justIntonation must remain enabled on the Program-level PragmaSet alongside strict.");
        Assert.True(
            program.Pragmas.Has("strict"),
            "strict must remain enabled on the Program-level PragmaSet alongside justIntonation.");
    }

    [Fact]
    public void Fact_MultipleProcs_AllCarryStrictBit()
    {
        // Every ProcDeclaration parsed under a strict pragma must carry the bit —
        // the parser does NOT consume / clear the strict pragma between proc
        // declarations (it's a file-scope bit, not a one-shot statement modifier).
        var program = ParseToProgram(
            "enable strict;\n"
            + "proc a ()\n    1\nend proc\n"
            + "proc b ()\n    2\nend proc\n");
        var procs = program.Statements.OfType<ProcDeclaration>().ToList();
        Assert.Equal(2, procs.Count);
        Assert.True(procs[0].IsStrict, "first proc under `enable strict;` must have IsStrict=true.");
        Assert.True(procs[1].IsStrict, "second proc under `enable strict;` must have IsStrict=true.");
    }
}
