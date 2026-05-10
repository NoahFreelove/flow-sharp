using System;
using FlowLang.StandardLibrary.Audio;
using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase26_2;

/// <summary>
/// Phase 26.2 Wave 0 RED scaffolding — engine-eval facts for the music-typed
/// FX overload set (ERG-02 + ERG-04). All facts skipped in Wave 0; un-skipped
/// in Wave 3 (plan 04) when the FX overload registrations land in
/// <c>flow-lang/StandardLibrary/Audio/EffectsFunctions.cs</c>.
///
/// Each fact follows the per-sample-identity pattern of
/// <see cref="FlowLang.Tests.Unit.QuickFixes.DecibelBeatNumericCompatFacts.GainWithDecibelLiteral_ResolvesAndProducesSameOutputAsDoubleEquivalent"/>:
/// run the call twice — once with a music-typed literal, once with the
/// bare-Double equivalent — and assert per-sample identity within 1e-6f.
///
/// CONTEXT D-08 sentinel + RESEARCH Pitfall 3:
/// <see cref="Reverb_BareDoubleAndSecondVariants_ResolveDistinctlyNoAmbiguity"/>
/// pins that <c>reverb(Buffer, Double, Double)</c> and
/// <c>reverb(Buffer, Double, Second)</c> coexist without ambiguity.
/// </summary>
public class MusicTypeFXOverloadFacts
{
    [Fact]
    public void DelayMillisecond_ResolvesAndProducesSameOutputAsDoubleEquivalent()
    {
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(@"
use ""@std""
use ""@audio""
Buffer src     = (createSineTone 0.05 220.0 0.5)
Buffer dMs     = (delay src 100ms 0.5 0.4)
Buffer dDouble = (delay src 100.0 0.5 0.4)
");
        Assert.True(errorCount == 0, $"Expected no errors, got {errorCount}. Stderr:\n{stderr}");
        var byMs = runner.GetVariable("dMs").As<AudioBuffer>();
        var byDouble = runner.GetVariable("dDouble").As<AudioBuffer>();
        Assert.Equal(byDouble.Data.Length, byMs.Data.Length);
        Assert.True(byMs.Data.Length > 0, "Sine tone buffer must be non-empty");
        for (int i = 0; i < byMs.Data.Length; i++)
        {
            Assert.True(MathF.Abs(byMs.Data[i] - byDouble.Data[i]) < 1e-6f,
                $"Per-sample mismatch at i={i}: ms={byMs.Data[i]}, double={byDouble.Data[i]}");
        }
    }

    [Fact]
    public void CompressDecibelMs_ResolvesAndProducesSameOutputAsDoubleEquivalent()
    {
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(@"
use ""@std""
use ""@audio""
Buffer src     = (createSineTone 0.05 220.0 0.5)
Buffer cMix    = (compress src -12dB 4.0 5ms 100ms)
Buffer cDouble = (compress src -12.0 4.0 5.0 100.0)
");
        Assert.True(errorCount == 0, $"Expected no errors, got {errorCount}. Stderr:\n{stderr}");
        var byMix = runner.GetVariable("cMix").As<AudioBuffer>();
        var byDouble = runner.GetVariable("cDouble").As<AudioBuffer>();
        Assert.Equal(byDouble.Data.Length, byMix.Data.Length);
        Assert.True(byMix.Data.Length > 0, "Sine tone buffer must be non-empty");
        for (int i = 0; i < byMix.Data.Length; i++)
        {
            Assert.True(MathF.Abs(byMix.Data[i] - byDouble.Data[i]) < 1e-6f,
                $"Per-sample mismatch at i={i}: mix={byMix.Data[i]}, double={byDouble.Data[i]}");
        }
    }

    [Fact]
    public void SidechainDecibelMs_ResolvesAndProducesSameOutputAsDoubleEquivalent()
    {
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(@"
use ""@std""
use ""@audio""
Buffer src     = (createSineTone 0.05 220.0 0.5)
Buffer trig    = (createSineTone 0.05 880.0 0.5)
Buffer sMix    = (sidechain src trig -12dB 4.0 5ms 100ms)
Buffer sDouble = (sidechain src trig -12.0 4.0 5.0 100.0)
");
        Assert.True(errorCount == 0, $"Expected no errors, got {errorCount}. Stderr:\n{stderr}");
        var byMix = runner.GetVariable("sMix").As<AudioBuffer>();
        var byDouble = runner.GetVariable("sDouble").As<AudioBuffer>();
        Assert.Equal(byDouble.Data.Length, byMix.Data.Length);
        Assert.True(byMix.Data.Length > 0, "Sine tone buffer must be non-empty");
        for (int i = 0; i < byMix.Data.Length; i++)
        {
            Assert.True(MathF.Abs(byMix.Data[i] - byDouble.Data[i]) < 1e-6f,
                $"Per-sample mismatch at i={i}: mix={byMix.Data[i]}, double={byDouble.Data[i]}");
        }
    }

    /// <summary>
    /// CONTEXT D-08: <c>reverb(Buffer, Double, Second)</c> ships alongside
    /// the existing <c>reverb(Buffer, Double, Double)</c>. Same DSP path,
    /// per-sample identity expected.
    /// </summary>
    [Fact]
    public void ReverbSecond_ResolvesAndProducesSameOutputAsDoubleEquivalent()
    {
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(@"
use ""@std""
use ""@audio""
Buffer src     = (createSineTone 0.05 220.0 0.5)
Buffer rSec    = (reverb src 0.5 1.5s)
Buffer rDouble = (reverb src 0.5 1.5)
");
        Assert.True(errorCount == 0, $"Expected no errors, got {errorCount}. Stderr:\n{stderr}");
        var bySec = runner.GetVariable("rSec").As<AudioBuffer>();
        var byDouble = runner.GetVariable("rDouble").As<AudioBuffer>();
        Assert.Equal(byDouble.Data.Length, bySec.Data.Length);
        Assert.True(bySec.Data.Length > 0, "Sine tone buffer must be non-empty");
        for (int i = 0; i < bySec.Data.Length; i++)
        {
            Assert.True(MathF.Abs(bySec.Data[i] - byDouble.Data[i]) < 1e-6f,
                $"Per-sample mismatch at i={i}: sec={bySec.Data[i]}, double={byDouble.Data[i]}");
        }
    }

    /// <summary>
    /// RESEARCH Pitfall 3 sentinel: Both <c>reverb(Buffer, Double, Double)</c>
    /// and <c>reverb(Buffer, Double, Second)</c> overloads coexist after Wave 3;
    /// neither call site (Double-only or Second-only) must produce
    /// "Ambiguous overload" — bare Double resolves at score 3000 (3× exact
    /// match), Second resolves at score 1000 (one exact match) + 1000 + 1000
    /// per the OverloadResolver scoring scheme. Distinct winners.
    /// </summary>
    [Fact]
    public void Reverb_BareDoubleAndSecondVariants_ResolveDistinctlyNoAmbiguity()
    {
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(@"
use ""@std""
use ""@audio""
Buffer src     = (createSineTone 0.05 220.0 0.5)
Buffer rSec    = (reverb src 0.5 1.5s)
Buffer rDouble = (reverb src 0.5 1.5)
");
        Assert.True(errorCount == 0, $"Expected no errors, got {errorCount}. Stderr:\n{stderr}");
        Assert.False(stderr.Contains("Ambiguous overload", StringComparison.OrdinalIgnoreCase),
            $"reverb(Buffer, Double, Double) and reverb(Buffer, Double, Second) " +
            $"must coexist without ambiguity, got: {stderr}");
    }

    [Fact]
    public void LowpassHertz_ResolvesAndProducesSameOutputAsDoubleEquivalent()
    {
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(@"
use ""@std""
use ""@audio""
Buffer src     = (createSineTone 0.05 1100.0 0.5)
Buffer lHz     = (lowpass src 800Hz)
Buffer lDouble = (lowpass src 800.0)
");
        Assert.True(errorCount == 0, $"Expected no errors, got {errorCount}. Stderr:\n{stderr}");
        var byHz = runner.GetVariable("lHz").As<AudioBuffer>();
        var byDouble = runner.GetVariable("lDouble").As<AudioBuffer>();
        Assert.Equal(byDouble.Data.Length, byHz.Data.Length);
        Assert.True(byHz.Data.Length > 0, "Sine tone buffer must be non-empty");
        for (int i = 0; i < byHz.Data.Length; i++)
        {
            Assert.True(MathF.Abs(byHz.Data[i] - byDouble.Data[i]) < 1e-6f,
                $"Per-sample mismatch at i={i}: hz={byHz.Data[i]}, double={byDouble.Data[i]}");
        }
    }

    [Fact]
    public void HighpassHertz_ResolvesAndProducesSameOutputAsDoubleEquivalent()
    {
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(@"
use ""@std""
use ""@audio""
Buffer src     = (createSineTone 0.05 220.0 0.5)
Buffer hHz     = (highpass src 200Hz)
Buffer hDouble = (highpass src 200.0)
");
        Assert.True(errorCount == 0, $"Expected no errors, got {errorCount}. Stderr:\n{stderr}");
        var byHz = runner.GetVariable("hHz").As<AudioBuffer>();
        var byDouble = runner.GetVariable("hDouble").As<AudioBuffer>();
        Assert.Equal(byDouble.Data.Length, byHz.Data.Length);
        Assert.True(byHz.Data.Length > 0, "Sine tone buffer must be non-empty");
        for (int i = 0; i < byHz.Data.Length; i++)
        {
            Assert.True(MathF.Abs(byHz.Data[i] - byDouble.Data[i]) < 1e-6f,
                $"Per-sample mismatch at i={i}: hz={byHz.Data[i]}, double={byDouble.Data[i]}");
        }
    }

    [Fact]
    public void BandpassHertz_ResolvesAndProducesSameOutputAsDoubleEquivalent()
    {
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(@"
use ""@std""
use ""@audio""
Buffer src     = (createSineTone 0.05 500.0 0.5)
Buffer bHz     = (bandpass src 200Hz 800Hz)
Buffer bDouble = (bandpass src 200.0 800.0)
");
        Assert.True(errorCount == 0, $"Expected no errors, got {errorCount}. Stderr:\n{stderr}");
        var byHz = runner.GetVariable("bHz").As<AudioBuffer>();
        var byDouble = runner.GetVariable("bDouble").As<AudioBuffer>();
        Assert.Equal(byDouble.Data.Length, byHz.Data.Length);
        Assert.True(byHz.Data.Length > 0, "Sine tone buffer must be non-empty");
        for (int i = 0; i < byHz.Data.Length; i++)
        {
            Assert.True(MathF.Abs(byHz.Data[i] - byDouble.Data[i]) < 1e-6f,
                $"Per-sample mismatch at i={i}: hz={byHz.Data[i]}, double={byDouble.Data[i]}");
        }
    }
}
