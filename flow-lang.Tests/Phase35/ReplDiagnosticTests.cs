using FlowLang.Core;
using FlowLang.Diagnostics;
using Xunit;

namespace FlowLang.Tests.Phase35;

/// <summary>
/// Phase 35 LANG-04 Wave 2a — REPL/eval sentinel-keyed SourceMap entries
/// must render via the diagnostic renderer just like file-backed sources.
///
/// <para>
/// REPL/eval sessions register their in-memory source under
/// <see cref="SourceMap.EvalKey"/> / <see cref="SourceMap.StdinKey"/> /
/// <see cref="SourceMap.ReplKey"/> (per Plan 35-01 § "REPL/eval sentinels").
/// The diagnostic renderer must resolve these keys exactly like file paths
/// — the sentinel string is what appears in the <c>--&gt;</c> location line.
/// </para>
/// </summary>
public class ReplDiagnosticTests
{
    [Fact]
    public void EvalSentinelKeyResolvesSourceFromSourceMap()
    {
        var sources = new SourceMap();
        sources.Register(SourceMap.EvalKey, "Int z = (add 1 \"two\")\n");

        var span = new Span(
            new SourceLocation(1, 16, SourceMap.EvalKey),
            new SourceLocation(1, 21, SourceMap.EvalKey));
        var diag = new FlowDiagnostic(
            DiagnosticLevel.Error,
            "type mismatch in REPL eval",
            span,
            Labels: [new DiagnosticLabel(span, "this is wrong")],
            Notes: [],
            Suggestion: null);

        var output = DiagnosticRenderer.Render(diag, sources, useColor: false);

        // The location row must use the sentinel string verbatim.
        Assert.Contains($"--> {SourceMap.EvalKey}:1:16", output);
        // The source-quote row must contain the registered REPL source line.
        Assert.Contains("Int z = (add 1 \"two\")", output);
    }

    [Fact]
    public void StdinSentinelKeyAlsoResolves()
    {
        var sources = new SourceMap();
        sources.Register(SourceMap.StdinKey, "(print foo)\n");

        var span = new Span(
            new SourceLocation(1, 8, SourceMap.StdinKey),
            new SourceLocation(1, 11, SourceMap.StdinKey));
        var diag = new FlowDiagnostic(
            DiagnosticLevel.Error,
            "unknown identifier 'foo'",
            span,
            Labels: [new DiagnosticLabel(span, "not found")],
            Notes: [],
            Suggestion: null);

        var output = DiagnosticRenderer.Render(diag, sources, useColor: false);

        Assert.Contains($"--> {SourceMap.StdinKey}:1:8", output);
        Assert.Contains("(print foo)", output);
    }

    [Fact]
    public void MissingSourceEntryRendersLocationWithoutQuote()
    {
        // No registration for the path; renderer should still emit the
        // header + location lines but skip the source-quote/caret rows.
        var sources = new SourceMap();
        var span = new Span(
            new SourceLocation(5, 3, "tests/never_registered.flow"),
            new SourceLocation(5, 8, "tests/never_registered.flow"));
        var diag = new FlowDiagnostic(
            DiagnosticLevel.Error,
            "orphan diagnostic",
            span,
            Labels: [new DiagnosticLabel(span, "")],
            Notes: [],
            Suggestion: null);

        var output = DiagnosticRenderer.Render(diag, sources, useColor: false);
        Assert.Contains("error: orphan diagnostic", output);
        Assert.Contains("--> tests/never_registered.flow:5:3", output);
        // The caret-line + source-quote pattern includes ` | ` after the
        // line-number prefix; absent because no source was registered.
        Assert.DoesNotContain(" 5 | ", output);
    }
}
