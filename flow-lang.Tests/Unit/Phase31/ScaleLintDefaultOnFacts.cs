using FlowLang.Lexing;
using FlowLang.Tests.Unit.Phase17;
using FlowLsp.Diagnostics;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

namespace FlowLang.Tests.Unit.Phase31;

/// <summary>
/// Phase 31 Plan 31-02 Task 2 (D-03 promotion): pins the scaleLint default-on
/// contract. Phase 24 D-19 (pragma-required activation gate) is superseded
/// by Phase 31 D-03 — the analyzer now runs unconditionally. The
/// <c>enable scaleLint;</c> pragma remains parseable per Phase 31 D-04 (no
/// language-level opt-out; editor-side suppression is the policy answer) and
/// is accepted as a no-op for v1.3 backward compatibility.
///
/// Source string stays <c>"flow.scaleLint"</c> per Phase 31 D-05.
/// </summary>
public class ScaleLintDefaultOnFacts
{
    [Fact]
    public void ScaleLint_DefaultOn_NoPragma_EmitsInformationOnNonDiatonic()
    {
        // No `enable scaleLint;` — yet the analyzer must STILL fire (D-03).
        var src = "key Cmajor { | C4 F#4 | }";
        var result = LspFixtures.Parse(src);
        var diags = ScaleLintAnalyzer.Analyze(result.Ast, result.Tokens, src);
        Assert.Single(diags);
        Assert.Equal(DiagnosticSeverity.Information, diags[0].Severity);
        Assert.Equal("flow.scaleLint", diags[0].Source);
        Assert.Contains("F#4", diags[0].Message);
    }

    [Fact]
    public void ScaleLint_DefaultOn_WithPragma_StillEmits_PragmaIsNoOp()
    {
        // With the pragma — behavior is IDENTICAL to the no-pragma case.
        // The pragma is now a recognized no-op per Phase 31 D-03.
        var src = "enable scaleLint;\nkey Cmajor { | C4 F#4 | }";
        var result = LspFixtures.Parse(src);
        var diags = ScaleLintAnalyzer.Analyze(result.Ast, result.Tokens, src);
        Assert.Single(diags);
        Assert.Equal(DiagnosticSeverity.Information, diags[0].Severity);
        Assert.Equal("flow.scaleLint", diags[0].Source);
        Assert.Contains("F#4", diags[0].Message);
    }

    [Fact]
    public void ScaleLint_DefaultOn_NoKeyBlock_EmitsZero()
    {
        // Phase 24 D-22 silent-on-no-key contract is preserved — without a
        // key context the analyzer has nothing to compare against.
        var src = "| C4 F#4 |";
        var result = LspFixtures.Parse(src);
        var diags = ScaleLintAnalyzer.Analyze(result.Ast, result.Tokens, src);
        Assert.Empty(diags);
    }

    [Fact]
    public void ScaleLint_DefaultOn_PragmaStillParses_RegistryAccepts()
    {
        // The pragma name remains registered in PragmaRegistry so
        // `enable scaleLint;` parses cleanly without surfacing the D-12
        // unknown-pragma error. The pragma is now a silent no-op (description
        // updated to reflect Phase 31 D-03).
        Assert.True(PragmaRegistry.KnownPragmas.ContainsKey("scaleLint"));
        Assert.True(PragmaRegistry.IsKnown("scaleLint"));

        // And parsing a source file with the pragma must succeed without
        // adding an unknown-pragma error.
        var src = "enable scaleLint;\nInt x = 5;";
        var result = LspFixtures.Parse(src);
        Assert.DoesNotContain(result.Errors,
            e => e.Message.Contains("unknown pragma 'scaleLint'"));
    }
}
