using System;
using FlowLang.Diagnostics;
using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem.PrimitiveTypes;
using Xunit;

namespace FlowLang.Tests.Integration.Phase44;

/// <summary>
/// Phase 44 Plan 44-09 Task 2 — D-13 regression-pin: Dict lookup stays
/// type-strict by design in BOTH modes. Phase 26.1 already hashes Dict
/// keys by type+value — Int 1 ≠ Float 1.0 ≠ Double 1.0 as keys; Symbol
/// <c>#foo</c> ≠ String <c>"foo"</c> as keys. Phase 44 changes NOTHING
/// about Dict key matching — this xUnit suite locks the behavior to
/// prevent inadvertent loosening per RESEARCH Pitfall 13.
/// <para>
/// Construction note: Dict literal `(dict K V K V …)` infers its static
/// Dict&lt;K, V&gt; type from the FIRST key, so a single literal can't carry
/// mixed key types in Flow's type system. We start from empty `(dict)`
/// (typed Dict&lt;Void, V&gt;) and incrementally `(set)` heterogeneous keys
/// — same runtime behavior, declarable shape.
/// </para>
/// <para>
/// T-44-09-03 mitigation: pins (a) Int vs Float keys distinct, (b)
/// <c>has</c> returns true for each as DIFFERENT keys, (c) Symbol vs
/// String keys distinct, and (d) dict size grows as expected when
/// inserting same-value-different-type keys.
/// </para>
/// </summary>
[Trait("Category", Phase44TestCategory.Phase44)]
[Collection("FlowScripts")]
public class DictTypeStrictRegressionTests : IDisposable
{
    public DictTypeStrictRegressionTests()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
    }

    [Fact]
    public void Fact_DictIntFloatKeys_DistinctBothModes()
    {
        // Dict with Int 1 + Float 1.0 keys → both distinct, both retrievable.
        // Non-strict mode regression pin.
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(
            "Dict<Void, String> d = (dict)\n" +
            "d = (set d 1 \"one\")\n" +
            "d = (set d 1.0 \"one-point-oh\")\n" +
            "String a = (get d 1)\n" +
            "String b = (get d 1.0)\n");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");
        Assert.Equal("one", runner.GetVariable("a").As<string>());
        Assert.Equal("one-point-oh", runner.GetVariable("b").As<string>());

        // Strict mode: same behavior.
        using var runner2 = new FlowEngineRunner();
        var (ok2, _, stderr2, _) = runner2.RunSource(
            "enable strict;\n" +
            "Dict<Void, String> d = (dict)\n" +
            "d = (set d 1 \"one\")\n" +
            "d = (set d 1.0 \"one-point-oh\")\n" +
            "String a = (get d 1)\n" +
            "String b = (get d 1.0)\n");
        Assert.True(ok2, $"expected clean strict run; stderr: {stderr2}");
        Assert.Equal("one", runner2.GetVariable("a").As<string>());
        Assert.Equal("one-point-oh", runner2.GetVariable("b").As<string>());
    }

    [Fact]
    public void Fact_DictHasInt_DoesNotMatchFloat_BothModes()
    {
        // (has d 1) true; (has d 1.0) true — both as DISTINCT keys.
        // Critical: neither lookup confuses the other.
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(
            "Dict<Void, String> d = (dict)\n" +
            "d = (set d 1 \"one\")\n" +
            "d = (set d 1.0 \"one-point-oh\")\n" +
            "Bool h1 = (has d 1)\n" +
            "Bool h2 = (has d 1.0)\n");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");
        Assert.True(runner.GetVariable("h1").As<bool>());
        Assert.True(runner.GetVariable("h2").As<bool>());

        using var runner2 = new FlowEngineRunner();
        var (ok2, _, stderr2, _) = runner2.RunSource(
            "enable strict;\n" +
            "Dict<Void, String> d = (dict)\n" +
            "d = (set d 1 \"one\")\n" +
            "d = (set d 1.0 \"one-point-oh\")\n" +
            "Bool h1 = (has d 1)\n" +
            "Bool h2 = (has d 1.0)\n");
        Assert.True(ok2, $"expected clean strict run; stderr: {stderr2}");
        Assert.True(runner2.GetVariable("h1").As<bool>());
        Assert.True(runner2.GetVariable("h2").As<bool>());
    }

    [Fact]
    public void Fact_DictSymbolStringKeys_DistinctBothModes()
    {
        // Symbol #foo and String "foo" are DISTINCT keys per Phase 26.1 SYM-01.
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(
            "Dict<Void, String> d = (dict)\n" +
            "d = (set d #foo \"sym\")\n" +
            "d = (set d \"foo\" \"str\")\n" +
            "String a = (get d #foo)\n" +
            "String b = (get d \"foo\")\n");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");
        Assert.Equal("sym", runner.GetVariable("a").As<string>());
        Assert.Equal("str", runner.GetVariable("b").As<string>());

        using var runner2 = new FlowEngineRunner();
        var (ok2, _, stderr2, _) = runner2.RunSource(
            "enable strict;\n" +
            "Dict<Void, String> d = (dict)\n" +
            "d = (set d #foo \"sym\")\n" +
            "d = (set d \"foo\" \"str\")\n" +
            "String a = (get d #foo)\n" +
            "String b = (get d \"foo\")\n");
        Assert.True(ok2, $"expected clean strict run; stderr: {stderr2}");
        Assert.Equal("sym", runner2.GetVariable("a").As<string>());
        Assert.Equal("str", runner2.GetVariable("b").As<string>());
    }

    [Fact]
    public void Fact_DictSize_AfterMixedTypeKeys()
    {
        // (size d) should equal the number of inserted DISTINCT keys, not
        // collapse Int 1 + Float 1.0 + String "1" into one slot.
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(
            "Dict<Void, Int> d = (dict)\n" +
            "d = (set d 1 1)\n" +
            "d = (set d 1.0 2)\n" +
            "d = (set d \"1\" 3)\n" +
            "Int n = (size d)\n");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");
        Assert.Equal(3, runner.GetVariable("n").As<int>());

        using var runner2 = new FlowEngineRunner();
        var (ok2, _, stderr2, _) = runner2.RunSource(
            "enable strict;\n" +
            "Dict<Void, Int> d = (dict)\n" +
            "d = (set d 1 1)\n" +
            "d = (set d 1.0 2)\n" +
            "d = (set d \"1\" 3)\n" +
            "Int n = (size d)\n");
        Assert.True(ok2, $"expected clean strict run; stderr: {stderr2}");
        Assert.Equal(3, runner2.GetVariable("n").As<int>());
    }
}
