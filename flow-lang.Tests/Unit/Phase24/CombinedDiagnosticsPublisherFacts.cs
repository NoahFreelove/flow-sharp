using FlowLang.Tests.Unit.Phase17;
using FlowLsp.Diagnostics;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

namespace FlowLang.Tests.Unit.Phase24;

/// <summary>
/// Phase 24 Plan 24-04: pins the single-publish merge invariant.
/// LSP publishDiagnostics REPLACES per-URI, so parse errors AND scale-lint
/// diagnostics MUST be composed into one Container&lt;Diagnostic&gt; per parse cycle.
/// Source-tag separation is preserved: parse errors keep "flow", scale-lint
/// keeps "flow.scaleLint" — editors can filter independently.
///
/// Empty-publish-clears-squiggles invariant (DiagnosticsPublisher.cs:52 comment)
/// is preserved by always returning a list (possibly empty) and always pushing it.
/// </summary>
public class CombinedDiagnosticsPublisherFacts
{
    [Fact]
    public void BuildAll_NoErrorsNoLint_ReturnsEmpty()
    {
        var src = "proc greet()\n    (print \"hi\")\nend proc";
        var result = LspFixtures.Parse(src);
        var diags = CombinedDiagnosticsPublisher.BuildAll(result, src);
        Assert.Empty(diags);
    }

    [Fact]
    public void CombinedPublish_ParseErrorsTagged_Flow()
    {
        var src = "proc (";  // intentional parse error
        var result = LspFixtures.Parse(src);
        var diags = CombinedDiagnosticsPublisher.BuildAll(result, src);
        Assert.NotEmpty(diags);
        // Parse-error diagnostics must be tagged "flow" (not "flow.scaleLint")
        Assert.Contains(diags, d => d.Source == "flow");
    }

    [Fact]
    public void CombinedPublish_ScaleLintTagged_FlowScaleLint()
    {
        var src = "enable scaleLint;\nkey Cmajor { | F#4 | }";
        var result = LspFixtures.Parse(src);
        var diags = CombinedDiagnosticsPublisher.BuildAll(result, src);
        Assert.Contains(diags, d => d.Source == "flow.scaleLint");
        Assert.Contains(diags, d => d.Severity == DiagnosticSeverity.Information);
    }

    [Fact]
    public void BuildAll_PragmaAbsent_NoLintDiagnostics()
    {
        // LINT-02 wire-level: even if the source has a key block + non-diatonic note,
        // the absence of `enable scaleLint;` means BuildAll emits zero "flow.scaleLint"
        // tagged diagnostics. Parse errors (if any) still flow through tagged "flow".
        var src = "key Cmajor { | F#4 | }";
        var result = LspFixtures.Parse(src);
        var diags = CombinedDiagnosticsPublisher.BuildAll(result, src);
        Assert.DoesNotContain(diags, d => d.Source == "flow.scaleLint");
    }

    [Fact]
    public void BuildAll_PragmaAbsentWithKeyBlock_ReturnsEmpty_ClearsStaleSquiggles()
    {
        // Pitfall 6 (empty-publish-clears-squiggles): when both parse-errors AND lint
        // produce zero diagnostics, BuildAll MUST return an empty list (NOT null,
        // NOT skip the publish call). The instance Publish method then forwards the
        // empty list to PublishDiagnostics so editors clear any prior squiggles.
        // This Fact pins the static-composer half of the invariant; the unconditional
        // forwarding half is enforced by the source-grep acceptance criterion in Task 2.
        var src = "key Cmajor { | C4 D4 E4 F4 | }";  // no pragma, all-diatonic, no errors
        var result = LspFixtures.Parse(src);
        var diags = CombinedDiagnosticsPublisher.BuildAll(result, src);
        Assert.NotNull(diags);
        Assert.Empty(diags);
    }
}
