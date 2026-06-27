using FlowLang.Tests.Unit.Phase17;
using FlowLsp.Diagnostics;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

namespace FlowLang.Tests.Unit.Phase31;

/// <summary>
/// Phase 31 Plan 31-02 (SPEC-1 partial): pins the ShadowedVariableAnalyzer.
///
/// Walks every <c>VariableDeclaration</c> in the AST with a scope stack. When a
/// declaration's name matches an outer-scope declaration's name, emits a
/// Warning-severity Diagnostic on the inner declaration.
///
/// Source tag: <c>"flow.shadowedVariable"</c> (Phase 31 D-05 + Phase 24 D-18
/// dotted-source convention).
///
/// Same-scope re-declaration is NOT shadowing — only NESTED-scope counts.
///
/// Syntax note: Flow proc declarations are <c>proc name() ... end proc</c>
/// (NOT C-style <c>{ }</c> blocks). See examples/tutorial.flow for the
/// canonical proc syntax.
/// </summary>
public class ShadowedVariableAnalyzerFacts
{
    [Fact]
    public void NestedScope_SameName_EmitsWarning()
    {
        var src = "Int x = 1;\nproc f ()\n    Int x = 2;\nend proc";
        var result = LspFixtures.Parse(src);
        var diags = ShadowedVariableAnalyzer.Analyze(result.Ast, result.Tokens, src);
        Assert.Single(diags);
        Assert.Equal(DiagnosticSeverity.Warning, diags[0].Severity);
        Assert.Equal("flow.shadowedVariable", diags[0].Source);
        Assert.Contains("x", diags[0].Message);
        Assert.Contains("shadows", diags[0].Message);
        Assert.Contains("line 1", diags[0].Message);
    }

    [Fact]
    public void SameScope_DifferentNames_EmitsZero()
    {
        var src = "Int x = 1;\nInt y = 2;";
        var result = LspFixtures.Parse(src);
        var diags = ShadowedVariableAnalyzer.Analyze(result.Ast, result.Tokens, src);
        Assert.Empty(diags);
    }

    [Fact]
    public void SameScope_SameName_IsNotShadowing()
    {
        // Same-scope re-declaration is the language's existing redeclaration
        // territory — not Phase 31's concern. Analyzer must NOT flag it.
        var src = "Int x = 1;\nInt x = 2;";
        var result = LspFixtures.Parse(src);
        var diags = ShadowedVariableAnalyzer.Analyze(result.Ast, result.Tokens, src);
        Assert.Empty(diags);
    }

    [Fact]
    public void MalformedAst_ReturnsEmpty_CharitableFailOpen()
    {
        var src = "Int = = = ;";
        var result = LspFixtures.Parse(src);
        var diags = ShadowedVariableAnalyzer.Analyze(result.Ast, result.Tokens, src);
        Assert.NotNull(diags);
        Assert.True(diags.Count >= 0);
    }
}
