using FlowLang.Diagnostics;
using FlowLang.Tests.Fixtures;
using Xunit;

namespace FlowLang.Tests.Unit.Phase23;

/// <summary>
/// Phase 23 Plan 23-03 Task 2 / D-11: <c>enharmonic()</c> emits a one-shot stderr
/// advisory warning when called inside non-12-TET tuning. The conversion still
/// happens (existing behavior preserved); only the warning is added. Pitfall 5 #3.
///
/// Dedup contract: at most ONE warning per process per session — verified by
/// <see cref="Enharmonic_TwoCallsUnderJI_WarnsOnlyOnce"/>. Under EqualTemperament
/// (default + explicit), NO warning fires.
/// </summary>
[Collection("FlowScripts")]
public class EnharmonicWarningFacts : System.IDisposable
{
    public EnharmonicWarningFacts()  { RenderingDiagnostics.ResetForTesting(); }
    public void Dispose()            { RenderingDiagnostics.ResetForTesting(); }

    [Fact]
    public void Enharmonic_UnderJustIntonation_EmitsWarning()
    {
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(@"enable justIntonation;
use ""@std""
(print (str (enharmonic F#4)))
");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");
        Assert.Contains("[enharmonic] called inside tuning != equalTemperament", stderr);
        Assert.Contains("21 cent shift", stderr);
    }

    [Fact]
    public void Enharmonic_UnderPythagorean_EmitsWarning()
    {
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(@"enable pythagorean;
use ""@std""
(print (str (enharmonic Bb4)))
");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");
        Assert.Contains("[enharmonic] called inside tuning != equalTemperament", stderr);
    }

    [Fact]
    public void Enharmonic_UnderEqualTemperament_NoWarning()
    {
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(@"enable equalTemperament;
use ""@std""
(print (str (enharmonic F#4)))
");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");
        Assert.DoesNotContain("[enharmonic]", stderr);
    }

    [Fact]
    public void Enharmonic_NoPragma_NoWarning()
    {
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(@"use ""@std""
(print (str (enharmonic F#4)))
");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");
        Assert.DoesNotContain("[enharmonic]", stderr);
    }

    [Fact]
    public void Enharmonic_TwoCallsUnderJI_WarnsOnlyOnce()
    {
        using var runner = new FlowEngineRunner();
        var (ok, _, stderr, _) = runner.RunSource(@"enable justIntonation;
use ""@std""
(print (str (enharmonic F#4)))
(print (str (enharmonic Bb4)))
(print (str (enharmonic C#4)))
");
        Assert.True(ok, $"expected clean run; stderr: {stderr}");
        // Exactly ONE occurrence of the warning text in stderr (Pitfall 5 dedup).
        int count = stderr.Split("[enharmonic]").Length - 1;
        Assert.Equal(1, count);
    }
}
