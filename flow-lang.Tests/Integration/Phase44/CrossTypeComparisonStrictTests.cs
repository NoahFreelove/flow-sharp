using System;
using FlowLang.Diagnostics;
using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem.PrimitiveTypes;
using Xunit;

namespace FlowLang.Tests.Integration.Phase44;

/// <summary>
/// Phase 44 Plan 44-09 Task 2 — Facts pinning the D-11 strict cross-type
/// comparison + equality surface. <c>(gt)</c> / <c>(lt)</c> / <c>(gte)</c> /
/// <c>(lte)</c> cross-type → error in strict (no defined cross-type
/// ordering); <c>(equals)</c> cross-type → <c>false</c> in strict
/// (set-theoretic, NOT error — defensible answer for equality where
/// ordering has no defensible answer). Non-strict path preserves the
/// existing <see cref="FlowLang.StandardLibrary.Utils.LooseEquals"/> +
/// <see cref="FlowLang.StandardLibrary.Utils.CompareNumeric"/> behavior:
/// numeric coercion makes <c>(equals 1 1.0)</c> return <c>true</c> in
/// non-strict per RESEARCH Open Question 1 Option (b) recommendation.
/// <para>
/// Symbol vs String regression-pin per Phase 26.1 SYM-01 — Symbol is
/// strictly separate from String in BOTH modes (cross-type non-numeric
/// already returns false in <see cref="Utils.LooseEquals"/> line 82-83).
/// </para>
/// </summary>
[Trait("Category", Phase44TestCategory.Phase44)]
[Collection("FlowScripts")]
public class CrossTypeComparisonStrictTests : IDisposable
{
    public CrossTypeComparisonStrictTests()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    [Fact]
    public void Fact_GtIntDouble_Strict_ErrorReported()
    {
        // Strict (gt 1 1.0) — Int vs Double cross-type; no defined ordering
        // in strict per D-11. Emit canonical error verbatim.
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, errorCount) = runner.RunSource(
            "enable strict;\nBool r = (gt 1 1.0)\n");
        Assert.False(ok, "expected strict-mode error");
        Assert.True(errorCount >= 1, $"expected at least 1 error; stderr: {stderr}");
        Assert.Contains(
            "[strict] cross-type comparison Int vs Double — use explicit (double x) / (int x)",
            stderr);
    }

    [Fact]
    public void Fact_GtIntDouble_NonStrict_ReturnsFalseViaCoercion()
    {
        // Non-strict (gt 1 1.0) — Utils.CompareNumeric coerces both to
        // double; 1.0 == 1.0 → not greater → false.
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource("Bool r = (gt 1 1.0)\n");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");
        var v = runner.GetVariable("r");
        Assert.IsType<BoolType>(v.Type);
        Assert.False(v.As<bool>());
    }

    [Fact]
    public void Fact_LtIntDouble_Strict_ErrorReported()
    {
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, errorCount) = runner.RunSource(
            "enable strict;\nBool r = (lt 1 2.0)\n");
        Assert.False(ok, "expected strict-mode error");
        Assert.True(errorCount >= 1, $"expected at least 1 error; stderr: {stderr}");
        Assert.Contains(
            "[strict] cross-type comparison Int vs Double — use explicit (double x) / (int x)",
            stderr);
    }

    [Fact]
    public void Fact_GteIntDouble_Strict_ErrorReported()
    {
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, errorCount) = runner.RunSource(
            "enable strict;\nBool r = (gte 1 1.0)\n");
        Assert.False(ok, "expected strict-mode error");
        Assert.True(errorCount >= 1, $"expected at least 1 error; stderr: {stderr}");
        Assert.Contains(
            "[strict] cross-type comparison Int vs Double — use explicit (double x) / (int x)",
            stderr);
    }

    [Fact]
    public void Fact_LteIntDouble_Strict_ErrorReported()
    {
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, errorCount) = runner.RunSource(
            "enable strict;\nBool r = (lte 1 1.0)\n");
        Assert.False(ok, "expected strict-mode error");
        Assert.True(errorCount >= 1, $"expected at least 1 error; stderr: {stderr}");
        Assert.Contains(
            "[strict] cross-type comparison Int vs Double — use explicit (double x) / (int x)",
            stderr);
    }

    [Fact]
    public void Fact_GtSameType_BothModes_Works()
    {
        // Same-type Int-Int — no cross-type check fires; returns Bool(true)
        // in both modes.
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource("Bool r = (gt 2 1)\n");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");
        Assert.True(runner.GetVariable("r").As<bool>());

        using var runner2 = new FlowEngineRunner();
        var (ok2, _, stderr2, _) = runner2.RunSource(
            "enable strict;\nBool r = (gt 2 1)\n");
        Assert.True(ok2, $"expected clean strict run; stderr: {stderr2}");
        Assert.True(runner2.GetVariable("r").As<bool>());
    }

    [Fact]
    public void Fact_GtExplicitConversion_StrictWorks()
    {
        // (gt (double 1) 1.0) strict — explicit conversion makes both Double,
        // no cross-type → no error. 1.0 == 1.0 → not greater → false.
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, errorCount) = runner.RunSource(
            "enable strict;\nBool r = (gt (double 1) 1.0)\n");
        Assert.True(ok, $"expected clean strict run with explicit conv; stderr: {stderr}");
        Assert.Equal(0, errorCount);
        var v = runner.GetVariable("r");
        Assert.IsType<BoolType>(v.Type);
        Assert.False(v.As<bool>());
    }

    [Fact]
    public void Fact_EqualsIntDouble_Strict_ReturnsFalse()
    {
        // Strict (equals 1 1.0) → false (D-11 set-theoretic: defensible
        // answer "1 is not 1.0 — different types"). NOT an error — Plan
        // 44-09's strict equality differs from strict comparison in this
        // asymmetry per D-11.
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, errorCount) = runner.RunSource(
            "enable strict;\nBool r = (equals 1 1.0)\n");
        Assert.True(ok, $"expected clean strict run (set-theoretic false, not error); stderr: {stderr}");
        Assert.Equal(0, errorCount);
        var v = runner.GetVariable("r");
        Assert.IsType<BoolType>(v.Type);
        Assert.False(v.As<bool>());
    }

    [Fact]
    public void Fact_EqualsIntDouble_NonStrict_ReturnsTrue()
    {
        // Non-strict (equals 1 1.0) → true (Utils.LooseEquals numeric
        // coercion at line 73-76). RESEARCH Open Question 1 Option (b)
        // recommendation: non-strict path UNCHANGED. T-44-09-01 regression
        // pin.
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(
            "Bool r = (equals 1 1.0)\n");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");
        var v = runner.GetVariable("r");
        Assert.IsType<BoolType>(v.Type);
        Assert.True(v.As<bool>());
    }

    [Fact]
    public void Fact_EqualsSymbolString_BothModes_ReturnsFalse()
    {
        // (equals #foo "foo") → false in BOTH modes (Phase 26.1 SYM-01
        // regression pin — Symbol strictly separate from String). The
        // cross-type non-numeric path in Utils.LooseEquals returns false
        // already (line 82-83) — no Phase 44 change needed.
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(
            "Bool r = (equals #foo \"foo\")\n");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");
        Assert.False(runner.GetVariable("r").As<bool>());

        using var runner2 = new FlowEngineRunner();
        var (ok2, _, stderr2, _) = runner2.RunSource(
            "enable strict;\nBool r = (equals #foo \"foo\")\n");
        Assert.True(ok2, $"expected clean strict run; stderr: {stderr2}");
        Assert.False(runner2.GetVariable("r").As<bool>());
    }
}
