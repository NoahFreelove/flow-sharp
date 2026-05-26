using System;
using FlowLang.Diagnostics;
using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Integration.Phase44;

/// <summary>
/// Phase 44 Plan 44-08 Task 1 — Facts pinning the pre-strict bug fix per
/// ROADMAP line 404. <c>(print Int x)</c> charitably auto-strs via
/// <see cref="StandardLibrary.StdLib.AutoStr"/> in non-strict (today
/// it fails overload resolution per the String-only registration at
/// <c>BuiltInFunctions.cs:165-169</c>). The Void-wildcard
/// <c>(print)</c> registration sits alongside the existing String
/// overload — OverloadResolver scoring (+1000 exact vs +500
/// compatible) preserves byte-identical behavior for
/// <c>(print "hello")</c> per RESEARCH Pitfall 3.
/// <para>
/// Strict-mode path emits canonical
/// <c>[strict] (print) requires String — got &lt;Type&gt;</c>; Plan 44-09's
/// REQ-STRICT-09 test suite pins the exact wording via the
/// strict-error-manifest.csv. Plan 44-08 lands the error TEXT here.
/// </para>
/// </summary>
[Trait("Category", Phase44TestCategory.Phase44)]
[Collection("FlowScripts")]
public class PrintCharitablyTests : IDisposable
{
    public PrintCharitablyTests()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    [Fact]
    public void Fact_PrintString_NonStrict_ProducesByteIdenticalOutput()
    {
        using var runner = new FlowEngineRunner();
        var (ok, stdout, stderr, _) = runner.RunSource("(print \"hello\")\n");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");
        Assert.Equal("hello" + Environment.NewLine, stdout);
    }

    [Fact]
    public void Fact_PrintString_Strict_ProducesByteIdenticalOutput()
    {
        // Pitfall 3: explicit String overload scores +1000 vs Void-wildcard
        // +500, so (print "hello") routes to the String path regardless of
        // mode — no strict error fires even when CallerStrictMode=true.
        using var runner = new FlowEngineRunner();
        var (ok, stdout, stderr, errorCount) = runner.RunSource(
            "enable strict;\n(print \"hello\")\n");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");
        Assert.Equal("hello" + Environment.NewLine, stdout);
        Assert.Equal(0, errorCount);
    }

    [Fact]
    public void Fact_PrintInt_NonStrict_AutoStrs()
    {
        using var runner = new FlowEngineRunner();
        var (ok, stdout, stderr, _) = runner.RunSource("(print 42)\n");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");
        Assert.Equal("42" + Environment.NewLine, stdout);
    }

    [Fact]
    public void Fact_PrintDouble_NonStrict_AutoStrs()
    {
        using var runner = new FlowEngineRunner();
        var (ok, stdout, stderr, _) = runner.RunSource("(print 3.14)\n");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");
        Assert.Equal("3.14" + Environment.NewLine, stdout);
    }

    [Fact]
    public void Fact_PrintMusicType_NonStrict_AutoStrs()
    {
        // Decibel literal -12dB → AutoStr produces "-12dB" (sign-prefix
        // matches StrDecibel format convention).
        using var runner = new FlowEngineRunner();
        var (ok, stdout, stderr, _) = runner.RunSource(
            "Decibel d = -12dB\n(print d)\n");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");
        Assert.Equal("-12dB" + Environment.NewLine, stdout);
    }

    [Fact]
    public void Fact_PrintBool_NonStrict_AutoStrs()
    {
        using var runner = new FlowEngineRunner();
        var (ok, stdout, stderr, _) = runner.RunSource("(print true)\n");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");
        Assert.Equal("true" + Environment.NewLine, stdout);
    }

    [Fact]
    public void Fact_PrintSequence_NonStrict_AutoStrs_NonEmpty()
    {
        // Sequence ToString falls through to Value.ToString → SequenceData
        // dedicated repr. We only assert non-empty + no error — the exact
        // sequence stringification is owned by SequenceData.ToString and
        // not load-bearing for this test.
        using var runner = new FlowEngineRunner();
        var (ok, stdout, stderr, _) = runner.RunSource(
            "Sequence s = | C4 D4 E4 |\n(print s)\n");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");
        Assert.False(string.IsNullOrWhiteSpace(stdout), "expected non-empty sequence print");
    }

    [Fact]
    public void Fact_PrintInt_Strict_ProducesStrictError()
    {
        // Strict file calling (print 42) — the explicit String overload
        // does NOT match (Int is not String), so OverloadResolver falls
        // through to the Void-wildcard which branches on CallerStrictMode
        // and emits the canonical error.
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, errorCount) = runner.RunSource(
            "enable strict;\n(print 42)\n");
        Assert.False(ok, "expected strict-mode error");
        Assert.Equal(1, errorCount);
        Assert.Contains("[strict] (print) requires String — got Int", stderr);
    }
}
