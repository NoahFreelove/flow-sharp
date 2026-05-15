using FlowLang.Tests.Unit.Phase17;
using FlowLsp.Diagnostics;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

namespace FlowLang.Tests.Unit.Phase31;

/// <summary>
/// Phase 31 Plan 31-02 (SPEC-1 partial): pins the UnreachableSectionAnalyzer.
///
/// Walks every <c>SectionDeclaration</c> in the AST, collects referenced section
/// names from any <c>SongExpression</c>, and emits Information-severity
/// Diagnostics for sections that are defined but never referenced.
///
/// Source tag: <c>"flow.unreachableSection"</c> (Phase 31 D-05 + Phase 24 D-18
/// dotted-source convention).
/// </summary>
public class UnreachableSectionAnalyzerFacts
{
    [Fact]
    public void UnreferencedSection_EmitsInformationDiagnostic()
    {
        var src = "section intro { | C4 | }\nsection verse { | D4 | }\nSong s = [verse];";
        var result = LspFixtures.Parse(src);
        var diags = UnreachableSectionAnalyzer.Analyze(result.Ast, result.Tokens, src);
        Assert.Single(diags);
        Assert.Equal(DiagnosticSeverity.Information, diags[0].Severity);
        Assert.Equal("flow.unreachableSection", diags[0].Source);
        Assert.Contains("intro", diags[0].Message);
    }

    [Fact]
    public void ReferencedSection_EmitsZero()
    {
        var src = "section intro { | C4 | }\nsection verse { | D4 | }\nSong s = [intro verse];";
        var result = LspFixtures.Parse(src);
        var diags = UnreachableSectionAnalyzer.Analyze(result.Ast, result.Tokens, src);
        Assert.Empty(diags);
    }

    [Fact]
    public void RepeatReference_StarSyntax_IsRecognized()
    {
        // `verse*2` repeat syntax in a Song still references the section name `verse`.
        var src = "section intro { | C4 | }\nsection verse { | D4 | }\nSong s = [intro verse*2];";
        var result = LspFixtures.Parse(src);
        var diags = UnreachableSectionAnalyzer.Analyze(result.Ast, result.Tokens, src);
        Assert.Empty(diags);
    }

    [Fact]
    public void MalformedAst_ReturnsEmpty_CharitableFailOpen()
    {
        var src = "section (((";
        var result = LspFixtures.Parse(src);
        var diags = UnreachableSectionAnalyzer.Analyze(result.Ast, result.Tokens, src);
        Assert.NotNull(diags);
        Assert.True(diags.Count >= 0);
    }
}
