using System;
using FlowLang.Diagnostics;
using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem.PrimitiveTypes;
using Xunit;

namespace FlowLang.Tests.Integration.Phase44;

/// <summary>
/// Phase 44 Plan 44-08 Task 2 — Facts pinning the FIRST registration of
/// <c>(not)</c> in the InternalFunctionRegistry per RESEARCH A6
/// (<c>flow-lang/test.flow:39</c> previously commented on its absence).
/// <para>
/// Non-strict charitable wildcard: <c>(not 0)</c> → <c>true</c>,
/// <c>(not "x")</c> → <c>false</c>, <c>(not | C4 |)</c> → <c>false</c>
/// via truthy-coerce. Strict mode emits canonical
/// <c>[strict] (not) requires Bool — got &lt;Type&gt;</c>; Plan 44-09's
/// REQ-STRICT-09 test suite pins exact wording via the
/// strict-error-manifest.csv. Plan 44-08 lands the error TEXT here.
/// </para>
/// </summary>
[Trait("Category", Phase44TestCategory.Phase44)]
[Collection("FlowScripts")]
public class NotBuiltinTests : IDisposable
{
    public NotBuiltinTests()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    [Fact]
    public void Fact_NotTrue_ReturnsFalse()
    {
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource("Bool r = (not true)\n");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");
        var v = runner.GetVariable("r");
        Assert.IsType<BoolType>(v.Type);
        Assert.False(v.As<bool>());
    }

    [Fact]
    public void Fact_NotFalse_ReturnsTrue()
    {
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource("Bool r = (not false)\n");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");
        var v = runner.GetVariable("r");
        Assert.IsType<BoolType>(v.Type);
        Assert.True(v.As<bool>());
    }

    [Fact]
    public void Fact_NotIntZero_NonStrict_ReturnsTrue()
    {
        // (not 0) — non-strict charitable wildcard. 0 is falsy → not is true.
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource("Bool r = (not 0)\n");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");
        var v = runner.GetVariable("r");
        Assert.IsType<BoolType>(v.Type);
        Assert.True(v.As<bool>());
    }

    [Fact]
    public void Fact_NotIntFive_NonStrict_ReturnsFalse()
    {
        // (not 5) — non-strict charitable wildcard. 5 is truthy → not is false.
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource("Bool r = (not 5)\n");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");
        var v = runner.GetVariable("r");
        Assert.IsType<BoolType>(v.Type);
        Assert.False(v.As<bool>());
    }

    [Fact]
    public void Fact_NotEmptyString_NonStrict_ReturnsTrue()
    {
        // (not "") — non-strict charitable wildcard. Empty string is falsy
        // → not is true.
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource("Bool r = (not \"\")\n");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");
        var v = runner.GetVariable("r");
        Assert.IsType<BoolType>(v.Type);
        Assert.True(v.As<bool>());
    }

    [Fact]
    public void Fact_NotInt_Strict_ErrorReported()
    {
        // Strict file (not 5) — wildcard branches on CallerStrictMode and
        // emits the canonical error.
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, errorCount) = runner.RunSource(
            "enable strict;\nBool r = (not 5)\n");
        Assert.False(ok, "expected strict-mode error");
        Assert.Equal(1, errorCount);
        Assert.Contains("[strict] (not) requires Bool — got Int", stderr);
    }
}
