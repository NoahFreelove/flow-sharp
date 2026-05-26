using System;
using FlowLang.Diagnostics;
using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem.PrimitiveTypes;
using Xunit;

namespace FlowLang.Tests.Integration.Phase44;

/// <summary>
/// Phase 44 Plan 44-08 Task 3 — Facts pinning the D-12 last-truthy
/// semantics for non-strict <c>(and)</c> / <c>(or)</c> per the composer's
/// Area 4.2 discuss-phase choice (RESEARCH Open Question 2 RESOLVED).
/// v1.5 breaking change vs the prior Bool-only <c>AndBool</c> /
/// <c>OrBool</c> return shape — permitted under D-v1.5-01 pre-traction
/// latitude (<c>project_pre_public_no_legacy_burden</c> memo).
/// <para>
/// OverloadResolver scoring guarantees byte-identical behavior for the
/// existing <c>(and Bool Bool)</c> / <c>(or Bool Bool)</c> call sites:
/// the Bool-typed overload scores +1000 while the new Void-wildcard
/// scores +500 — wildcard only fires on non-Bool argument types.
/// Strict-mode Bool-required tightening is owned by Plan 44-09.
/// </para>
/// </summary>
[Trait("Category", Phase44TestCategory.Phase44)]
[Collection("FlowScripts")]
public class AndOrLastTruthyTests : IDisposable
{
    public AndOrLastTruthyTests()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    [Fact]
    public void Fact_AndIntString_NonStrict_ReturnsLastTruthyString()
    {
        // (and 1 "foo") — both truthy, last operand "foo" returned.
        // Bool-typed overload doesn't match (Int + String != Bool), so the
        // wildcard fires.
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(
            "String r = (and 1 \"foo\")\n");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");
        var v = runner.GetVariable("r");
        Assert.IsType<StringType>(v.Type);
        Assert.Equal("foo", v.As<string>());
    }

    [Fact]
    public void Fact_OrBoolInt_NonStrict_ReturnsFirstTruthyInt()
    {
        // (or false 42) — first operand false is falsy, second 42 is truthy
        // so it's returned verbatim. Bool-typed overload doesn't match (Bool
        // + Int != Bool/Bool), so the wildcard fires.
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(
            "Int r = (or false 42)\n");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");
        var v = runner.GetVariable("r");
        Assert.IsType<IntType>(v.Type);
        Assert.Equal(42, v.As<int>());
    }

    [Fact]
    public void Fact_AndFalseInt_NonStrict_ShortCircuitsToFalse()
    {
        // (and false 1) — first operand false is falsy, short-circuit returns
        // false verbatim. Bool + Int doesn't match Bool-typed overload, so
        // wildcard fires.
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(
            "Bool r = (and false 1)\n");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");
        var v = runner.GetVariable("r");
        Assert.IsType<BoolType>(v.Type);
        Assert.False(v.As<bool>());
    }

    [Fact]
    public void Fact_OrEmptyStringString_NonStrict_ReturnsSecondTruthy()
    {
        // (or "" "fallback") — empty string is falsy, "fallback" is truthy
        // so it's returned. Both args are String → no Bool-typed match;
        // wildcard fires.
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(
            "String r = (or \"\" \"fallback\")\n");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");
        var v = runner.GetVariable("r");
        Assert.IsType<StringType>(v.Type);
        Assert.Equal("fallback", v.As<string>());
    }

    [Fact]
    public void Fact_AndBoolBool_NonStrict_BoolOverloadWins()
    {
        // (and true false) — Bool-typed overload (+1000) wins over the
        // Void-wildcard (+500). Returns Bool false. Regression pin:
        // existing call sites continue receiving a Bool, not whatever the
        // last-truthy wildcard would return (which is still false in this
        // case but the SHAPE matters — composers downstream may rely on
        // Bool type discrimination).
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(
            "Bool r = (and true false)\n");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");
        var v = runner.GetVariable("r");
        Assert.IsType<BoolType>(v.Type);
        Assert.False(v.As<bool>());
    }

    [Fact]
    public void Fact_OrSymbolString_NonStrict_ReturnsFirstTruthySymbol()
    {
        // (or #foo "bar") — Symbol is always truthy, returned verbatim.
        // Symbol + String → no Bool-typed match; wildcard fires.
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(
            "Symbol r = (or #foo \"bar\")\n");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");
        var v = runner.GetVariable("r");
        // Symbol intern table: underlying CLR string is the symbol's name.
        Assert.Equal("foo", v.As<string>());
    }
}
