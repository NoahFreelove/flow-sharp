using System;
using FlowLang.StandardLibrary.Audio;
using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase26_2;

/// <summary>
/// Phase 26.2 Wave 0 RED scaffolding — engine-eval facts for Hertz overloads
/// on the <c>createXxxTone</c> family (ERG-04). All facts skipped in Wave 0;
/// un-skipped in Wave 3 (plan 04) when:
///   - <c>createSineTone(Double, Hertz, Double)</c> ships as a C# registration
///     (parallel to its existing Double registration in
///     <c>flow-lang/StandardLibrary/Audio/SignalGenerationFunctions.cs</c>)
///   - <c>createSawTone(Double, Hertz, Double)</c>,
///     <c>createSquareTone(Double, Hertz, Double)</c>,
///     <c>createTriangleTone(Double, Hertz, Double)</c> ship as Flow-side
///     <c>proc</c> overloads in <c>flow-lang/audio.flow</c> that delegate to
///     their existing Double-form definitions.
///
/// Per-sample identity within 1e-6f between Hertz-call and Double-equivalent.
///
/// CONTEXT note (Q3 from RESEARCH): Hertz overloads land on the EXISTING
/// long-form <c>createXxxTone</c> family — NOT new short <c>sine</c>/<c>saw</c>/
/// <c>square</c>/<c>triangle</c> builtins. (Existing short-name builtins for
/// per-sample oscillator state remain untouched — they're a different surface.)
/// </summary>
public class HertzFXOverloadFacts
{
    [Fact]
    public void CreateSineToneHertz_ResolvesAndProducesSameOutputAsDoubleEquivalent()
    {
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(@"
use ""@std""
use ""@audio""
Buffer aHz     = (createSineTone 0.05 440Hz 0.5)
Buffer aDouble = (createSineTone 0.05 440.0 0.5)
");
        Assert.True(errorCount == 0, $"Expected no errors, got {errorCount}. Stderr:\n{stderr}");
        var byHz = runner.GetVariable("aHz").As<AudioBuffer>();
        var byDouble = runner.GetVariable("aDouble").As<AudioBuffer>();
        Assert.Equal(byDouble.Data.Length, byHz.Data.Length);
        Assert.True(byHz.Data.Length > 0, "Sine tone buffer must be non-empty");
        for (int i = 0; i < byHz.Data.Length; i++)
        {
            Assert.True(MathF.Abs(byHz.Data[i] - byDouble.Data[i]) < 1e-6f,
                $"Per-sample mismatch at i={i}: hz={byHz.Data[i]}, double={byDouble.Data[i]}");
        }
    }

    [Fact]
    public void CreateSawToneHertz_ResolvesViaFlowProcOverload()
    {
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(@"
use ""@std""
use ""@audio""
Buffer aHz     = (createSawTone 0.05 440Hz 0.5)
Buffer aDouble = (createSawTone 0.05 440.0 0.5)
");
        Assert.True(errorCount == 0, $"Expected no errors, got {errorCount}. Stderr:\n{stderr}");
        var byHz = runner.GetVariable("aHz").As<AudioBuffer>();
        var byDouble = runner.GetVariable("aDouble").As<AudioBuffer>();
        Assert.Equal(byDouble.Data.Length, byHz.Data.Length);
        Assert.True(byHz.Data.Length > 0, "Saw tone buffer must be non-empty");
        for (int i = 0; i < byHz.Data.Length; i++)
        {
            Assert.True(MathF.Abs(byHz.Data[i] - byDouble.Data[i]) < 1e-6f,
                $"Per-sample mismatch at i={i}: hz={byHz.Data[i]}, double={byDouble.Data[i]}");
        }
    }

    [Fact]
    public void CreateSquareToneHertz_ResolvesViaFlowProcOverload()
    {
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(@"
use ""@std""
use ""@audio""
Buffer aHz     = (createSquareTone 0.05 440Hz 0.5)
Buffer aDouble = (createSquareTone 0.05 440.0 0.5)
");
        Assert.True(errorCount == 0, $"Expected no errors, got {errorCount}. Stderr:\n{stderr}");
        var byHz = runner.GetVariable("aHz").As<AudioBuffer>();
        var byDouble = runner.GetVariable("aDouble").As<AudioBuffer>();
        Assert.Equal(byDouble.Data.Length, byHz.Data.Length);
        Assert.True(byHz.Data.Length > 0, "Square tone buffer must be non-empty");
        for (int i = 0; i < byHz.Data.Length; i++)
        {
            Assert.True(MathF.Abs(byHz.Data[i] - byDouble.Data[i]) < 1e-6f,
                $"Per-sample mismatch at i={i}: hz={byHz.Data[i]}, double={byDouble.Data[i]}");
        }
    }

    [Fact]
    public void CreateTriangleToneHertz_ResolvesViaFlowProcOverload()
    {
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(@"
use ""@std""
use ""@audio""
Buffer aHz     = (createTriangleTone 0.05 440Hz 0.5)
Buffer aDouble = (createTriangleTone 0.05 440.0 0.5)
");
        Assert.True(errorCount == 0, $"Expected no errors, got {errorCount}. Stderr:\n{stderr}");
        var byHz = runner.GetVariable("aHz").As<AudioBuffer>();
        var byDouble = runner.GetVariable("aDouble").As<AudioBuffer>();
        Assert.Equal(byDouble.Data.Length, byHz.Data.Length);
        Assert.True(byHz.Data.Length > 0, "Triangle tone buffer must be non-empty");
        for (int i = 0; i < byHz.Data.Length; i++)
        {
            Assert.True(MathF.Abs(byHz.Data[i] - byDouble.Data[i]) < 1e-6f,
                $"Per-sample mismatch at i={i}: hz={byHz.Data[i]}, double={byDouble.Data[i]}");
        }
    }
}
