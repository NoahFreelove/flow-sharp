using FlowLang.Core;
using FlowLang.Diagnostics;
using Xunit;

namespace FlowLang.Tests.Phase35;

/// <summary>
/// Phase 35 LANG-04 Wave 2a — ErrorReporter multi-diagnostic accumulation
/// and rendering.
///
/// <para>
/// The reporter exposes a parallel <c>Report(FlowDiagnostic)</c> overload
/// alongside the existing <c>Report(FlowError)</c>. <c>FormatDiagnostics</c>
/// renders each accumulated diagnostic via <see cref="DiagnosticRenderer.Render"/>,
/// joined by a single blank line (double <c>\n</c>) — matches rustc's
/// inter-diagnostic separator convention.
/// </para>
/// </summary>
public class MultiErrorRenderingTests
{
    [Fact]
    public void TwoDiagnosticsSeparatedByBlankLine()
    {
        const string sourceFile = "tests/multi.flow";
        var sources = new SourceMap();
        sources.Register(sourceFile,
            "Int x = (add 1 \"two\")\nInt y = (mul z 3)\n");

        var reporter = new ErrorReporter();

        var span1 = new Span(
            new SourceLocation(1, 16, sourceFile),
            new SourceLocation(1, 21, sourceFile));
        reporter.Report(new FlowDiagnostic(
            DiagnosticLevel.Error,
            "type mismatch: expected Int, found String",
            span1,
            Labels: [new DiagnosticLabel(span1, "String argument")],
            Notes: [],
            Suggestion: null));

        var span2 = new Span(
            new SourceLocation(2, 14, sourceFile),
            new SourceLocation(2, 15, sourceFile));
        reporter.Report(new FlowDiagnostic(
            DiagnosticLevel.Error,
            "unknown identifier 'z'",
            span2,
            Labels: [new DiagnosticLabel(span2, "not found in scope")],
            Notes: [],
            Suggestion: null));

        var formatted = reporter.FormatDiagnostics(sources);

        Assert.True(reporter.HasDiagnostics, "Reporter must surface HasDiagnostics=true once Report(FlowDiagnostic) lands.");
        Assert.Equal(2, reporter.Diagnostics.Count);
        Assert.Contains("type mismatch", formatted);
        Assert.Contains("unknown identifier 'z'", formatted);
        // Inter-diagnostic separator: exactly one blank line between the two
        // renders. The blank line splits the formatted output into two halves
        // each containing exactly one diagnostic.
        var parts = formatted.Split("\n\n");
        Assert.True(parts.Length >= 2,
            $"Expected at least one blank-line separator between diagnostics. Got:\n{formatted}");
    }

    [Fact]
    public void FormatDiagnosticsEmptyWhenNoneReported()
    {
        var reporter = new ErrorReporter();
        var sources = new SourceMap();
        Assert.False(reporter.HasDiagnostics);
        Assert.Equal(string.Empty, reporter.FormatDiagnostics(sources));
    }
}
