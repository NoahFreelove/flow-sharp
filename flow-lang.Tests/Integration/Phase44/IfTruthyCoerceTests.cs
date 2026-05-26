using System;
using FlowLang.Diagnostics;
using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Integration.Phase44;

/// <summary>
/// Phase 44 Plan 44-08 Task 2 — Facts pinning the non-strict charitable
/// <c>if</c> truthy-coerce path per D-12. The Void-wildcard
/// <c>if(Void, Void, Void)</c> sits alongside the existing
/// <c>if(Bool, Lazy&lt;Void&gt;, Lazy&lt;Void&gt;)</c> + <c>if(Bool, Void, Void)</c>
/// overloads — OverloadResolver scoring picks the Bool-typed overload
/// (+1000) for <c>(if true ...)</c> and falls through to the wildcard
/// (+500) for <c>(if 5 ...)</c> / <c>(if "x" ...)</c> / etc.
/// <para>
/// Strict mode (<c>CallerStrictMode == true</c>) emits the canonical
/// <c>[strict] (if) requires Bool — got &lt;Type&gt;</c>; Plan 44-09's
/// REQ-STRICT-09 test suite pins exact wording via the
/// strict-error-manifest.csv. Plan 44-08 lands the error TEXT here.
/// </para>
/// </summary>
[Trait("Category", Phase44TestCategory.Phase44)]
[Collection("FlowScripts")]
public class IfTruthyCoerceTests : IDisposable
{
    public IfTruthyCoerceTests()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    [Fact]
    public void Fact_IfBoolTrue_NonStrict_ExecutesThen()
    {
        // (if cond then else) — Flow uses prefix syntax, not block syntax.
        // Bool-typed (if Bool, Void, Void) overload wins at +1000.
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(
            "String r = (if true \"then\" \"else\")\n");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");
        Assert.Equal("then", runner.GetVariable("r").As<string>());
    }

    [Fact]
    public void Fact_IfBoolFalse_NonStrict_ExecutesElse()
    {
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(
            "String r = (if false \"then\" \"else\")\n");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");
        Assert.Equal("else", runner.GetVariable("r").As<string>());
    }

    [Fact]
    public void Fact_IfIntFive_NonStrict_TruthyExecutesThen()
    {
        // (if 5 ... ...) — 5 is truthy under non-strict charitable wildcard.
        // The Bool-typed (if Bool ...) overloads do NOT match (Int is not
        // Bool), so OverloadResolver falls to the new (Void, Void, Void)
        // wildcard which truthy-coerces via TruthyCoerce.
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(
            "String r = (if 5 \"then\" \"else\")\n");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");
        Assert.Equal("then", runner.GetVariable("r").As<string>());
    }

    [Fact]
    public void Fact_IfIntZero_NonStrict_FalsyExecutesElse()
    {
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(
            "String r = (if 0 \"then\" \"else\")\n");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");
        Assert.Equal("else", runner.GetVariable("r").As<string>());
    }

    [Fact]
    public void Fact_IfStringEmpty_NonStrict_FalsyExecutesElse()
    {
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(
            "String r = (if \"\" \"then\" \"else\")\n");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");
        Assert.Equal("else", runner.GetVariable("r").As<string>());
    }

    [Fact]
    public void Fact_IfStringNonEmpty_NonStrict_TruthyExecutesThen()
    {
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(
            "String r = (if \"x\" \"then\" \"else\")\n");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");
        Assert.Equal("then", runner.GetVariable("r").As<string>());
    }

    [Fact]
    public void Fact_IfInt_Strict_ErrorReported()
    {
        // Strict file (if 5 ...) — Bool-typed overloads don't match, falls
        // through to wildcard, wildcard branches on CallerStrictMode and
        // emits the canonical error.
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, errorCount) = runner.RunSource(
            "enable strict;\nString r = (if 5 \"then\" \"else\")\n");
        Assert.False(ok, "expected strict-mode error");
        Assert.True(errorCount >= 1, $"expected at least 1 error; stderr: {stderr}");
        Assert.Contains("[strict] (if) requires Bool — got Int", stderr);
    }
}
