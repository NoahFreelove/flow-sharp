using FlowLang.Core;
using FlowLang.Diagnostics;
using Xunit;

namespace FlowLang.Tests.Phase35;

/// <summary>
/// Phase 35 LANG-04 Wave 2a — TTY-color emission contract for the diagnostic
/// renderer.
///
/// <para>
/// The renderer accepts an explicit <c>useColor</c> flag rather than relying
/// on <c>Console.IsOutputRedirected</c> at the call-site — because the
/// renderer returns a <c>string</c> (not raw stderr writes), the caller
/// decides whether to embed ANSI escapes. <see cref="DiagnosticRenderer.Render"/>
/// with <c>useColor:true</c> embeds ANSI escapes (e.g. <c>\x1b[31m</c> for
/// the <c>error:</c> keyword + carets); with <c>useColor:false</c> the
/// output is pure ASCII (the baseline-comparable form).
/// </para>
///
/// <para>
/// The <c>Program.cs:77</c> precedent uses <c>Console.ForegroundColor</c>
/// — .NET auto-suppresses color when stdout is redirected. Top-level emit
/// (Task 3) wraps the renderer call in the same shape; the renderer itself
/// returns a string whose ANSI content is gated by <c>useColor</c>.
/// </para>
/// </summary>
public class DiagnosticTtyTests
{
    private static FlowDiagnostic SampleDiagnostic()
    {
        var sourceFile = "tests/sample.flow";
        var primary = new Span(
            new SourceLocation(1, 1, sourceFile),
            new SourceLocation(1, 4, sourceFile));
        return new FlowDiagnostic(
            DiagnosticLevel.Error,
            "sample error",
            primary,
            Labels: [new DiagnosticLabel(primary, "here")],
            Notes: [],
            Suggestion: null);
    }

    private static SourceMap SampleSources()
    {
        var sources = new SourceMap();
        sources.Register("tests/sample.flow", "abc def\n");
        return sources;
    }

    [Fact]
    public void RenderEmitsAnsiWhenUseColorTrue()
    {
        var output = DiagnosticRenderer.Render(SampleDiagnostic(), SampleSources(), useColor: true);
        // The renderer must embed at least one ANSI escape sequence when
        // useColor is on — at minimum the red `error:` keyword.
        Assert.Contains("\x1b[", output);
    }

    [Fact]
    public void RenderEmitsPlainWhenUseColorFalse()
    {
        var output = DiagnosticRenderer.Render(SampleDiagnostic(), SampleSources(), useColor: false);
        // No ANSI escapes when useColor is off — the baseline-comparable form.
        Assert.DoesNotContain("\x1b[", output);
    }
}
