using FlowLsp.Handlers;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

namespace FlowLang.Tests.Unit.Phase17;

/// <summary>
/// Phase 17 Plan 03 Task 2 — DiagnosticsPublisher Facts.
///
/// Exercise the pure <see cref="DiagnosticsPublisher.BuildDiagnostics"/> transform
/// without booting an OmniSharp <c>ILanguageServerFacade</c>. These pin the
/// FlowError → LSP Diagnostic mapping contract (severity, source tag, 1-based →
/// 0-based range). Empty-diagnostic publish semantics (must call even when empty)
/// are covered by the Publish-side wiring in Program.cs, not by the static transform.
/// </summary>
public class DiagnosticsHandlerTests
{
    [Fact]
    public void BuildDiagnostics_ValidSource_ReturnsEmpty()
    {
        var result = LspFixtures.Parse("proc greet()\n    (print \"hi\")\nend proc");
        var diags = DiagnosticsPublisher.BuildDiagnostics(result.Errors);
        Assert.Empty(diags);
    }

    [Fact]
    public void BuildDiagnostics_ParseError_ReturnsDiagnosticWithErrorSeverity()
    {
        var result = LspFixtures.Parse("proc (");
        var diags = DiagnosticsPublisher.BuildDiagnostics(result.Errors);
        Assert.NotEmpty(diags);
        Assert.All(diags, d => Assert.Equal(DiagnosticSeverity.Error, d.Severity));
    }

    [Fact]
    public void BuildDiagnostics_SourceFieldIsFlow()
    {
        var result = LspFixtures.Parse("proc (");
        var diags = DiagnosticsPublisher.BuildDiagnostics(result.Errors);
        Assert.NotEmpty(diags);
        Assert.All(diags, d => Assert.Equal("flow", d.Source));
    }

    [Fact]
    public void BuildDiagnostics_RangeIsZeroBased()
    {
        var result = LspFixtures.Parse("proc (");
        var diags = DiagnosticsPublisher.BuildDiagnostics(result.Errors);
        Assert.NotEmpty(diags);
        // SourceLocation is 1-based → LSP 0-based; clamps must produce non-negative coords.
        Assert.All(diags, d =>
        {
            Assert.True(d.Range.Start.Line >= 0, $"Start.Line was {d.Range.Start.Line}");
            Assert.True(d.Range.Start.Character >= 0, $"Start.Character was {d.Range.Start.Character}");
            Assert.True(d.Range.End.Line >= d.Range.Start.Line);
            Assert.True(d.Range.End.Character > d.Range.Start.Character
                     || d.Range.End.Line > d.Range.Start.Line);
        });
    }
}
