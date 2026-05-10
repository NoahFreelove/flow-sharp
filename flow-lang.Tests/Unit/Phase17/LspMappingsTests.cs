using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLsp;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

namespace FlowLang.Tests.Unit.Phase17;

/// <summary>
/// Phase 17 Plan 03 Task 1 — LspMappings Facts.
///
/// Pin the 1-based → 0-based coordinate math and DiagnosticLevel → DiagnosticSeverity
/// mapping. The underflow guard (Math.Max(0, ...)) matters because
/// <see cref="SourceLocation.Unknown"/> carries (0,0).
/// </summary>
public class LspMappingsTests
{
    [Fact]
    public void SourceLocation_MapsToZeroBasedRange()
    {
        var r = LspMappings.ToRange(new SourceLocation(3, 7));
        Assert.Equal(2, r.Start.Line);
        Assert.Equal(6, r.Start.Character);
        Assert.Equal(2, r.End.Line);
        Assert.Equal(7, r.End.Character);
    }

    [Fact]
    public void UnknownLocation_ClampsToZero()
    {
        var r = LspMappings.ToRange(SourceLocation.Unknown);
        Assert.Equal(0, r.Start.Line);
        Assert.Equal(0, r.Start.Character);
        Assert.Equal(0, r.End.Line);
        Assert.Equal(1, r.End.Character);
    }

    [Theory]
    [InlineData(DiagnosticLevel.Error, DiagnosticSeverity.Error)]
    [InlineData(DiagnosticLevel.Warning, DiagnosticSeverity.Warning)]
    [InlineData(DiagnosticLevel.Info, DiagnosticSeverity.Information)]
    public void DiagnosticLevel_MapsCorrectly(DiagnosticLevel level, DiagnosticSeverity expected)
        => Assert.Equal(expected, LspMappings.ToSeverity(level));
}
