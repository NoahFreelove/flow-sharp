using FlowLang.Core;
using FlowLang.Diagnostics;
using Xunit;

namespace FlowLang.Tests.Phase35;

/// <summary>
/// Phase 35 LANG-04 Wave 2a — golden-file tests for the Rust-style
/// multi-line diagnostic renderer.
///
/// <para>
/// Each fact constructs a <see cref="FlowDiagnostic"/> + populated
/// <see cref="SourceMap"/>, renders via <see cref="DiagnosticRenderer.Render"/>
/// with <c>useColor:false</c> (so the baseline file stays plain ASCII), then
/// compares the output against a baseline file under
/// <c>flow-lang.Tests/baselines/Phase35/diagnostics/</c>. The baselines are
/// the exact format spec from <c>35-RESEARCH.md § Example 4</c> — header /
/// location / pipe-prefixed source quote / caret line / optional notes /
/// optional did-you-mean help row.
/// </para>
/// </summary>
public class DiagnosticRendererGoldenTests
{
    private static readonly string BaselineDir =
        Path.Combine(AppContext.BaseDirectory, "baselines", "Phase35", "diagnostics");

    /// <summary>
    /// Baselines committed under <c>flow-lang.Tests/baselines/Phase35/diagnostics/</c>;
    /// MSBuild copies them to <c>bin/.../baselines/Phase35/diagnostics/</c> via the
    /// project's CopyToOutputDirectory rule (added in Task 2 alongside this fact class).
    /// </summary>
    private static string ReadBaseline(string name)
    {
        var path = Path.Combine(BaselineDir, name);
        Assert.True(File.Exists(path),
            $"Baseline file missing: {path} — Task 2 must pre-populate baselines under " +
            $"flow-lang.Tests/baselines/Phase35/diagnostics/ AND add a CopyToOutputDirectory rule.");
        // Normalize line endings — the diagnostic renderer always emits \n.
        return File.ReadAllText(path).Replace("\r\n", "\n").TrimEnd('\n');
    }

    [Fact]
    public void UnknownIdentifierDiagnostic_RendersExpectedFormat()
    {
        // Fixture: `seq -> (transpos 2)` on line 7 of tests/test_chain.flow.
        // The composer-typed identifier `transpos` is at column 9 (after `(`)
        // and is 8 characters long, ending at column 17 (half-open).
        const string sourceFile = "tests/test_chain.flow";
        const string sourceLine = "seq -> (transpos 2)";
        var sources = new SourceMap();
        // Synthesize a multi-line source so the renderer reads line 7 specifically.
        // Lines 1-6 are placeholder so line numbers in the output match the baseline.
        var srcText = string.Join("\n",
            "// fixture file",
            "Sequence seq = | C4q D4q |",
            "",
            "proc dummy() {",
            "  (print \"hi\")",
            "}",
            sourceLine,
            "");
        sources.Register(sourceFile, srcText);

        var startLoc = new SourceLocation(7, 9, sourceFile);
        var endLoc = new SourceLocation(7, 17, sourceFile);
        var primary = new Span(startLoc, endLoc);

        var diag = new FlowDiagnostic(
            DiagnosticLevel.Error,
            "unknown identifier 'transpos'",
            primary,
            Labels: [new DiagnosticLabel(primary, "not found in scope")],
            Notes: ["tried looking in: enclosing function 'main', module 'std', module 'audio'"],
            Suggestion: "transpose");

        var actual = DiagnosticRenderer.Render(diag, sources, useColor: false);
        var expected = ReadBaseline("unknown_identifier.txt");
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TypeMismatchDiagnostic_RendersExpectedFormat()
    {
        // Fixture: `(add 1 "two")` on line 3 of tests/test_types.flow.
        // Primary span = whole call (col 1..14); secondary span = `"two"` arg (col 8..13).
        const string sourceFile = "tests/test_types.flow";
        const string sourceLine = "(add 1 \"two\")";
        var sources = new SourceMap();
        var srcText = string.Join("\n",
            "// type mismatch fixture",
            "",
            sourceLine,
            "");
        sources.Register(sourceFile, srcText);

        var primary = new Span(
            new SourceLocation(3, 1, sourceFile),
            new SourceLocation(3, 14, sourceFile));
        var secondary = new Span(
            new SourceLocation(3, 8, sourceFile),
            new SourceLocation(3, 13, sourceFile));

        var diag = new FlowDiagnostic(
            DiagnosticLevel.Error,
            "type mismatch: expected Int, found String",
            primary,
            Labels:
            [
                new DiagnosticLabel(primary, "in this call"),
                new DiagnosticLabel(secondary, "this argument has type String"),
            ],
            Notes: ["function 'add' is overloaded on (Int, Int), (Double, Double)"],
            Suggestion: null);

        var actual = DiagnosticRenderer.Render(diag, sources, useColor: false);
        var expected = ReadBaseline("type_mismatch.txt");
        Assert.Equal(expected, actual);
    }
}
