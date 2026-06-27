using FlowLang.Tests.Unit.Phase17;
using FlowLsp.Diagnostics;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

namespace FlowLang.Tests.Unit.Phase24;

/// <summary>
/// Phase 24 Plan 24-04 + Phase 31 Plan 31-02: pins the single-publish merge
/// invariant. LSP publishDiagnostics REPLACES per-URI, so parse errors AND
/// every analyzer-source diagnostic MUST be composed into one
/// <see cref="OmniSharp.Extensions.LanguageServer.Protocol.Models.Container{T}"/>
/// per parse cycle. Source-tag separation is preserved: parse errors keep
/// "flow", scale-lint keeps "flow.scaleLint", unused-import keeps
/// "flow.unusedImport", unreachable-section keeps "flow.unreachableSection",
/// shadowed-variable keeps "flow.shadowedVariable" — editors filter
/// independently.
///
/// Empty-publish-clears-squiggles invariant (DiagnosticsPublisher.cs:52 comment)
/// is preserved by always returning a list (possibly empty) and always pushing
/// it. Phase 31 Plan 31-02 deleted the pre-Phase-31 short-circuit
/// (early-return-Array.Empty when parseDiags + lintDiags both empty) because
/// the three new analyzers may fire even when those two are silent.
///
/// Phase 31 Plan 31-02 added the <c>StdlibSymbolIndex</c> parameter to
/// <see cref="CombinedDiagnosticsPublisher.BuildAll"/> — every test call here
/// passes <see cref="LspFixtures.StdlibIndex"/> to construct one.
/// </summary>
public class CombinedDiagnosticsPublisherFacts
{
    [Fact]
    public void BuildAll_NoErrorsNoLint_ReturnsEmpty()
    {
        // Phase 31 Plan 31-08 scope expansion: with UndefinedSymbolAnalyzer wired
        // through BuildAll, `print` (declared in std.flow as `internal proc`)
        // would flag without `use "@std"`. The pre-Phase-31 source omitted the
        // import; we add it to keep the test's intent ("no errors, no lint, no
        // undefined symbols") satisfied under the six-analyzer pipeline.
        var src = "use \"@std\"\nproc greet()\n    (print \"hi\")\nend proc";
        var result = LspFixtures.Parse(src);
        var diags = CombinedDiagnosticsPublisher.BuildAll(result, src, LspFixtures.StdlibIndex());
        Assert.Empty(diags);
    }

    [Fact]
    public void CombinedPublish_ParseErrorsTagged_Flow()
    {
        var src = "proc (";  // intentional parse error
        var result = LspFixtures.Parse(src);
        var diags = CombinedDiagnosticsPublisher.BuildAll(result, src, LspFixtures.StdlibIndex());
        Assert.NotEmpty(diags);
        // Parse-error diagnostics must be tagged "flow" (not "flow.scaleLint")
        Assert.Contains(diags, d => d.Source == "flow");
    }

    [Fact]
    public void CombinedPublish_ScaleLintTagged_FlowScaleLint()
    {
        var src = "enable scaleLint;\nkey Cmajor { | F#4 | }";
        var result = LspFixtures.Parse(src);
        var diags = CombinedDiagnosticsPublisher.BuildAll(result, src, LspFixtures.StdlibIndex());
        Assert.Contains(diags, d => d.Source == "flow.scaleLint");
        Assert.Contains(diags, d => d.Severity == DiagnosticSeverity.Information);
    }

    [Fact]
    public void BuildAll_PragmaAbsent_StillEmitsScaleLint_Phase31_D03_DefaultOn()
    {
        // Pre-Phase-31 LINT-02 wire-level pin: absence of `enable scaleLint;` was
        // required to suppress lint diagnostics. SUPERSEDED by Phase 31 D-03 —
        // the analyzer now runs unconditionally so BuildAll emits a
        // "flow.scaleLint"-tagged diagnostic for the non-diatonic F#4 in
        // `key Cmajor { | F#4 | }` even with no pragma declared. The original
        // fact name is preserved (rename includes the supersession note for
        // composer-grep-ability).
        var src = "key Cmajor { | F#4 | }";
        var result = LspFixtures.Parse(src);
        var diags = CombinedDiagnosticsPublisher.BuildAll(result, src, LspFixtures.StdlibIndex());
        Assert.Contains(diags, d => d.Source == "flow.scaleLint");
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
        var diags = CombinedDiagnosticsPublisher.BuildAll(result, src, LspFixtures.StdlibIndex());
        Assert.NotNull(diags);
        Assert.Empty(diags);
    }
}
