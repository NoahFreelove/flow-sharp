using System.Collections.Generic;
using System.Linq;
using FlowLang.Ast;
using FlowLang.Ast.Elements;
using FlowLang.Ast.Expressions;
using FlowLang.Ast.Patterns;
using FlowLang.Ast.Statements;
using FlowLang.Diagnostics;
using FlowLang.Lexing;
using FlowLang.Parsing;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Phase36;

/// <summary>
/// Phase 36 Plan 36-10 (SECT-01) — section-parameter parser gates.
///
/// <para>
/// Task 1 covers PARSING ONLY — runtime dispatch (OverloadResolver section
/// dispatch + synthetic-frame binding + default-value evaluation) lands in
/// Task 2 / SectionOverloadTests + SectionDefaultsTests.
/// </para>
/// </summary>
public class SectionParamsParserTests
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

    private static SectionDeclaration FindSection(Program program, string name)
    {
        foreach (var stmt in program.Statements)
        {
            if (stmt is SectionDeclaration sd && sd.Name == name) return sd;
        }
        throw new Xunit.Sdk.XunitException($"Expected a SectionDeclaration named '{name}'.");
    }

    private static SongExpression FindSong(Program program)
    {
        foreach (var stmt in program.Statements)
        {
            var expr = stmt switch
            {
                VariableDeclaration vd => vd.Value,
                ExpressionStatement es => es.Expression,
                _ => null,
            };
            if (expr is SongExpression song) return song;
        }
        throw new Xunit.Sdk.XunitException("Expected a SongExpression at top level.");
    }

    [Fact]
    public void BareSectionStillParses()
    {
        // Backward-compat: zero-arg section still produces SectionDeclaration
        // with Parameters/DefaultValues both null.
        var program = ParseSource("section verse { (print \"v\") }");
        var sd = FindSection(program, "verse");
        Assert.Null(sd.Parameters);
        Assert.Null(sd.DefaultValues);
    }

    [Fact]
    public void ParameterizedSectionParses()
    {
        var program = ParseSource("section verse(Note root) { (print root) }");
        var sd = FindSection(program, "verse");
        Assert.NotNull(sd.Parameters);
        Assert.Single(sd.Parameters!);
        var bp = Assert.IsType<BindingPattern>(sd.Parameters![0]);
        Assert.Equal("root", bp.Name);
        Assert.NotNull(bp.TypeAnnotation);
        Assert.IsType<NoteType>(bp.TypeAnnotation);
        Assert.NotNull(sd.DefaultValues);
        Assert.Null(sd.DefaultValues![0]);
    }

    [Fact]
    public void MultipleParamsCommaSeparated()
    {
        var program = ParseSource("section verse(Note root, Int repeats) { (print root) }");
        var sd = FindSection(program, "verse");
        Assert.NotNull(sd.Parameters);
        Assert.Equal(2, sd.Parameters!.Count);
        var b1 = Assert.IsType<BindingPattern>(sd.Parameters[0]);
        Assert.Equal("root", b1.Name);
        var b2 = Assert.IsType<BindingPattern>(sd.Parameters[1]);
        Assert.Equal("repeats", b2.Name);
    }

    [Fact]
    public void PatternConstructorInSection()
    {
        var program = ParseSource("section verse(Cmaj7) { (print \"chord\") }");
        var sd = FindSection(program, "verse");
        Assert.NotNull(sd.Parameters);
        Assert.Single(sd.Parameters!);
        var cp = Assert.IsType<ConstructorPattern>(sd.Parameters![0]);
        Assert.True(cp.IsChordLiteral);
        Assert.Equal("Cmaj7", cp.Name);
    }

    [Fact]
    public void TupleDestructureParam()
    {
        var program = ParseSource("section verse(<<Note root, Int repeats>>) { (print root) }");
        var sd = FindSection(program, "verse");
        Assert.NotNull(sd.Parameters);
        Assert.Single(sd.Parameters!);
        var cp = Assert.IsType<ConstructorPattern>(sd.Parameters![0]);
        Assert.Equal("Tuple", cp.Name);
        Assert.Equal(2, cp.SubPatterns.Count);
        var r = Assert.IsType<BindingPattern>(cp.SubPatterns[0]);
        Assert.Equal("root", r.Name);
        Assert.IsType<NoteType>(r.TypeAnnotation);
    }

    [Fact]
    public void GuardPatternInSection()
    {
        // Guard at end: `Note root when (greater 1 0)` — Phase 35 GuardPattern wraps inner binding.
        var program = ParseSource(
            "section pivot(Note root when (greater 1 0)) { (print root) }");
        var sd = FindSection(program, "pivot");
        Assert.NotNull(sd.Parameters);
        Assert.Single(sd.Parameters!);
        var gp = Assert.IsType<GuardPattern>(sd.Parameters![0]);
        var inner = Assert.IsType<BindingPattern>(gp.Inner);
        Assert.Equal("root", inner.Name);
    }

    [Fact]
    public void DefaultValueParams()
    {
        var program = ParseSource(
            "section verse(Note root = C4, Int repeats = 2) { (print root) }");
        var sd = FindSection(program, "verse");
        Assert.NotNull(sd.DefaultValues);
        Assert.Equal(2, sd.DefaultValues!.Count);
        Assert.NotNull(sd.DefaultValues[0]);
        Assert.NotNull(sd.DefaultValues[1]);
    }

    [Fact]
    public void SongExpressionRecognizesSectionCall()
    {
        var program = ParseSource(
            "section verse(Note root) { (print root) }\n" +
            "section chorus { (print \"chorus\") }\n" +
            "Song s = [verse(C4) chorus]");
        var song = FindSong(program);
        Assert.NotNull(song.Elements);
        Assert.Equal(2, song.Elements!.Count);
        var call = Assert.IsType<SectionCallElement>(song.Elements[0]);
        Assert.Equal("verse", call.Name);
        Assert.Single(call.PositionalArgs);
        var bare = Assert.IsType<BareSectionElement>(song.Elements[1]);
        Assert.Equal("chorus", bare.Name);
    }

    [Fact]
    public void RepeatOperatorOnSectionCall()
    {
        var program = ParseSource(
            "section verse(Note root) { (print root) }\n" +
            "Song s = [verse(C4)*3]");
        var song = FindSong(program);
        Assert.NotNull(song.Elements);
        var call = Assert.IsType<SectionCallElement>(song.Elements![0]);
        Assert.Equal(3, call.RepeatCount);
    }

    [Fact]
    public void NamedArgInSectionCall()
    {
        var program = ParseSource(
            "section verse(Note root, Int repeats) { (print root) }\n" +
            "Song s = [verse(root=C4, repeats=2)]");
        var song = FindSong(program);
        Assert.NotNull(song.Elements);
        var call = Assert.IsType<SectionCallElement>(song.Elements![0]);
        Assert.NotNull(call.NamedArgs);
        Assert.Equal(2, call.NamedArgs!.Count);
        Assert.Contains("root", call.NamedArgs.Keys);
        Assert.Contains("repeats", call.NamedArgs.Keys);
    }
}
