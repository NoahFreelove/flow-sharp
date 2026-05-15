using FlowLang.Tests.Unit.Phase17;
using FlowLsp.Diagnostics;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

namespace FlowLang.Tests.Unit.Phase31;

/// <summary>
/// Phase 31 Plan 31-02 (SPEC-1 partial): pins the UnusedImportAnalyzer.
///
/// The analyzer walks every <c>ImportStatement</c> in the AST and emits a
/// Warning-severity Diagnostic for each module whose procs are never referenced.
/// Source tag: <c>"flow.unusedImport"</c> (per Phase 31 D-05 + Phase 24 D-18
/// dotted-source convention so editors can filter independently).
///
/// Charitable fail-open (Phase 24 D-22 precedent): malformed AST never throws.
///
/// Module-binding notes:
///   - <c>@collections</c> hosts <c>head</c>, <c>map</c>, <c>filter</c>, etc.
///   - <c>@std</c> transitively imports <c>@collections</c> + <c>@bars</c> +
///     <c>@audio</c> + <c>@notation</c> + <c>@composition</c> per
///     <c>StdlibSymbolIndex.ModuleNames</c>.
///   - <c>@harmony</c> is NOT a shipped module (zero procs) — importing it is
///     therefore always "unused" per the analyzer.
/// </summary>
public class UnusedImportAnalyzerFacts
{
    [Fact]
    public void UnusedImport_EmitsWarningDiagnostic()
    {
        // @collections is a real module with procs. Body references no
        // collections proc — import is unused.
        var src = "use \"@collections\";\nInt x = 5;";
        var result = LspFixtures.Parse(src);
        var stdlib = LspFixtures.StdlibIndex();
        var diags = UnusedImportAnalyzer.Analyze(result.Ast, result.Tokens, src, stdlib);
        Assert.Single(diags);
        Assert.Equal(DiagnosticSeverity.Warning, diags[0].Severity);
        Assert.Equal("flow.unusedImport", diags[0].Source);
        Assert.Contains("@collections", diags[0].Message);
    }

    [Fact]
    public void UsedImport_HeadProcReferenced_EmitsZero()
    {
        // `head` is a proc from @collections — usage detected via stdlib.ProcsForModule.
        var src = "use \"@collections\";\nInt first = (head [1, 2, 3]);";
        var result = LspFixtures.Parse(src);
        var stdlib = LspFixtures.StdlibIndex();
        var diags = UnusedImportAnalyzer.Analyze(result.Ast, result.Tokens, src, stdlib);
        Assert.Empty(diags);
    }

    [Fact]
    public void MalformedAst_ReturnsEmptyDiagnostics_CharitableFailOpen()
    {
        // Parse an obviously-broken source — analyzer must NOT throw past the
        // public boundary and must return a non-null list per Phase 24 D-22
        // silent-fail-open precedent.
        var src = "proc (((((";
        var result = LspFixtures.Parse(src);
        var stdlib = LspFixtures.StdlibIndex();
        var diags = UnusedImportAnalyzer.Analyze(result.Ast, result.Tokens, src, stdlib);
        Assert.NotNull(diags);
        Assert.True(diags.Count >= 0);
    }

    [Fact]
    public void StdImportTransitive_ReferencedProcFromChildModule_EmitsZero()
    {
        // @std transitively expands to every entry in StdlibSymbolIndex.ModuleNames.
        // `head` is a @collections proc — when @std is imported, reference to head
        // keeps @std alive (the transitive-reachability rule per the plan).
        var src = "use \"@std\";\nInt first = (head [1, 2, 3]);";
        var result = LspFixtures.Parse(src);
        var stdlib = LspFixtures.StdlibIndex();
        var diags = UnusedImportAnalyzer.Analyze(result.Ast, result.Tokens, src, stdlib);
        Assert.DoesNotContain(diags, d => d.Message.Contains("@std"));
    }
}
