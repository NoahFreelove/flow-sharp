using System.Linq;
using FlowLang.Ast;
using FlowLang.Ast.Statements;
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using FlowLang.Parsing;
using Xunit;

namespace FlowLang.Tests.Integration.Phase41;

/// <summary>
/// Phase 41 DOC-01 — doc-comment ATTACHMENT contract: a <c>///</c> block binds to
/// the FOLLOWING <c>ProcDeclaration.DocComment</c>; a proc with no <c>///</c> gets
/// <c>DocComment == null</c> (charitable signature-only); an orphan <c>///</c> (no
/// proc after it, or a non-proc statement between) is dropped CHARITABLY — never an
/// error (per <c>feedback_charitable_interpretation</c> + D-07, Pitfall 2).
/// </summary>
[Trait("Category", "Phase41")]
public class DocCommentBindTests
{
    private static (Program Program, ErrorReporter Reporter) Parse(string source)
    {
        var reporter = new ErrorReporter();
        var tokens = new SimpleLexer(source, reporter).Tokenize();
        var program = new Parser(tokens, reporter).Parse();
        return (program, reporter);
    }

    private static ProcDeclaration FirstProc(Program program) =>
        program.Statements.OfType<ProcDeclaration>().First();

    [Fact]
    public void DocComment_BindsToFollowingProc()
    {
        var (program, reporter) = Parse(
            "/// adds two numbers\nproc add2 (Int: x)\n    (add x 2)\nend proc");

        var proc = FirstProc(program);
        Assert.Equal("add2", proc.Name);
        Assert.Equal("adds two numbers", proc.DocComment);
        Assert.False(reporter.HasErrors);
    }

    [Fact]
    public void MultiLineDocComment_BindsJoined()
    {
        var (program, _) = Parse(
            "/// line one\n/// line two\nproc foo (Int: x)\n    (print (str x))\nend proc");
        Assert.Equal("line one\nline two", FirstProc(program).DocComment);
    }

    [Fact]
    public void InternalProc_BindsDocComment()
    {
        // internal procs are forward declarations (no body / no `end`).
        var (program, reporter) = Parse("/// internal helper\ninternal proc helper (Int: x)");
        var proc = FirstProc(program);
        Assert.True(proc.IsInternal);
        Assert.Equal("internal helper", proc.DocComment);
        Assert.False(reporter.HasErrors);
    }

    [Fact]
    public void ProcWithoutDocComment_HasNullDocComment()
    {
        // Charitable signature-only: a proc with no /// is valid, DocComment null.
        var (program, reporter) = Parse("proc plain (Int: x)\n    (add x 1)\nend proc");
        Assert.Null(FirstProc(program).DocComment);
        Assert.False(reporter.HasErrors);
    }

    [Fact]
    public void OrphanDocComment_DroppedCharitably()
    {
        // A /// followed by a non-proc statement (here a declaration) then a proc:
        // the orphan /// is dropped silently; the later proc's DocComment is null,
        // and the ErrorReporter accumulates ZERO errors.
        var (program, reporter) = Parse(
            "/// orphan doc\nInt x = 5;\nproc foo (Int: n)\n    (add n 1)\nend proc");

        Assert.Null(FirstProc(program).DocComment);
        Assert.False(reporter.HasErrors);
    }

    [Fact]
    public void TrailingOrphanDocComment_NoError()
    {
        // A /// with NOTHING after it (EOF) is dropped without an error.
        var (program, reporter) = Parse(
            "proc foo (Int: n)\n    (add n 1)\nend proc\n/// trailing orphan");
        Assert.False(reporter.HasErrors);
        Assert.Null(FirstProc(program).DocComment);
    }

    [Fact]
    public void DocCommentSeparatedByPlainComment_DroppedCharitably()
    {
        // CR-01 regression: a `///` block separated from its proc by an intervening
        // plain `// comment` line must NOT bind to the proc — a doc-comment only
        // binds to a proc that IMMEDIATELY follows it. The intervening comment drops
        // the pending buffer charitably (no error), so `foo` is left undocumented.
        var (program, reporter) = Parse(
            "/// doc for nothing\n// an ordinary comment line\nproc foo (Int: x)\n    (add x 2)\nend proc");

        var proc = FirstProc(program);
        Assert.Equal("foo", proc.Name);
        Assert.Null(proc.DocComment);
        Assert.False(reporter.HasErrors);
    }

    [Fact]
    public void DocCommentDoesNotLeakAcrossCommentToLaterProc()
    {
        // CR-01 regression: the leaked buffer must not be consumed by a LATER proc
        // either. `/// alpha` precedes a plain comment, then `alpha` proc (no `///`
        // immediately above it → null), then `beta` proc. Neither inherits the
        // orphaned doc-comment.
        var (program, reporter) = Parse(
            "/// alpha doc\n// separator comment\nproc alpha (Int: a)\n    (add a 1)\nend proc\n" +
            "proc beta (Int: b)\n    (add b 2)\nend proc");

        var procs = program.Statements.OfType<ProcDeclaration>().ToList();
        Assert.Equal(2, procs.Count);
        Assert.Null(procs[0].DocComment);
        Assert.Null(procs[1].DocComment);
        Assert.False(reporter.HasErrors);
    }

    [Fact]
    public void DocCommentDoesNotLeakToSecondProc()
    {
        // A documented proc followed by an undocumented proc: the second must NOT
        // inherit the first's doc-comment (Pitfall 2 — buffer cleared on consume).
        var (program, _) = Parse(
            "/// first\nproc one (Int: a)\n    (add a 1)\nend proc\n" +
            "proc two (Int: b)\n    (add b 2)\nend proc");

        var procs = program.Statements.OfType<ProcDeclaration>().ToList();
        Assert.Equal(2, procs.Count);
        Assert.Equal("first", procs[0].DocComment);
        Assert.Null(procs[1].DocComment);
    }
}
