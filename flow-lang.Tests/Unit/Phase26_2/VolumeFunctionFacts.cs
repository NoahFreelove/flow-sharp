using System;
using FlowLang.StandardLibrary.Audio;
using FlowLang.Tests.Fixtures;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
using Xunit;

namespace FlowLang.Tests.Unit.Phase26_2;

/// <summary>
/// Phase 26.2 Wave 4: collection definition that disables parallelization for tests
/// asserting on stderr produced by the C# runtime via Console.Error.WriteLine.
/// FlowEngineRunner.cs uses Console.SetError(StringWriter) which is process-global —
/// parallel test execution causes competing redirections to route warnings to the
/// wrong test's capture buffer. VolumeFunctionFacts is the first test class that
/// asserts on a runtime-emitted Console.Error message (NegativeMusicLiteralFacts
/// asserts on ErrorReporter contents flushed via _stderr.WriteLine directly,
/// which bypasses the parallel issue). Marking with DisableParallelization keeps
/// the rest of the suite parallel while serializing the at-risk class.
/// </summary>
[CollectionDefinition("ConsoleCapture", DisableParallelization = true)]
public class ConsoleCaptureCollection { }

/// <summary>
/// Phase 26.2 Wave 0 RED scaffolding — engine-eval facts for the
/// <c>volume(Buffer, Double)</c> linear-multiplier function (ERG-03 D-04..D-07).
///
/// Volume vs. Gain semantic split:
///   - <c>gain(Buffer, Double|Decibel)</c> → dB. <c>(gain buf 0.5)</c> = +0.5 dB.
///     EXISTING — STAYS dB across all of Phase 26.2 per CONTEXT D-07.
///   - <c>volume(Buffer, Double)</c> → linear multiplier. <c>(volume buf 0.5)</c> = halve samples.
///     NEW in Wave 4 (plan 05).
///
/// All facts except the gain dB-canary are skipped in Wave 0; un-skipped in
/// Wave 4 when <c>volume</c> ships in audio.flow + EffectsFunctions.cs.
///
/// The <see cref="Gain_BareDoubleSubOne_StillDecibel_RegressionCanary"/> fact
/// is GREEN immediately and STAYS GREEN through every wave — it is the
/// regression sentinel for D-07 (the dB semantics of bare-Double <c>gain</c>
/// must not silently drift to linear).
///
/// [Collection("ConsoleCapture")] serializes this class against other test
/// classes that also redirect Console.Error via FlowEngineRunner — the
/// volume clip warning fires through Console.Error.WriteLine (mirroring
/// GainEffect's pattern) and parallel test runners' competing
/// Console.SetError calls otherwise route the warning to a different
/// StringWriter, producing flaky empty-stderr failures.
/// </summary>
[Collection("ConsoleCapture")]
public class VolumeFunctionFacts
{
    [Fact]
    public void Volume_HalfMultiplier_HalvesSamples()
    {
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(@"
use ""@std""
use ""@audio""
Buffer src    = (createSineTone 0.05 220.0 0.5)
Buffer halved = (volume src 0.5)
");
        Assert.True(errorCount == 0, $"Expected no errors, got {errorCount}. Stderr:\n{stderr}");
        var src = runner.GetVariable("src").As<AudioBuffer>();
        var halved = runner.GetVariable("halved").As<AudioBuffer>();
        Assert.Equal(src.Data.Length, halved.Data.Length);
        Assert.True(src.Data.Length > 0, "Sine tone buffer must be non-empty");

        int compared = 0;
        for (int i = 0; i < src.Data.Length; i++)
        {
            if (MathF.Abs(src.Data[i]) < 1e-6f) continue;
            Assert.True(MathF.Abs(halved.Data[i] - src.Data[i] * 0.5f) < 1e-6f,
                $"Per-sample mismatch at i={i}: halved={halved.Data[i]}, expected={src.Data[i] * 0.5f}");
            compared++;
        }
        Assert.True(compared > 100,
            $"Expected at least 100 comparable samples, got {compared} — fact did not exercise volume path.");
    }

    [Fact]
    public void Volume_DoubleMultiplier_DoublesAndEmitsClipWarning()
    {
        using var runner = new FlowEngineRunner();
        // Source amplitude 0.8 so that doubling produces samples beyond ±1.0 (max ~1.6),
        // exercising the wouldClip path; with the prior 0.5 amp the discretized sine peaks
        // hit ~0.99999976 doubled (< 1.0 within float precision) and never tripped the
        // warning. Volume impl mirrors GainEffect — no clamping, just a stderr warning —
        // so the per-sample expected is bare `src * 2.0f`, not Math.Clamp(...).
        var (_, _, stderr, errorCount) = runner.RunSource(@"
use ""@std""
use ""@audio""
Buffer src     = (createSineTone 0.05 220.0 0.8)
Buffer doubled = (volume src 2.0)
");
        Assert.True(errorCount == 0, $"Expected no errors, got {errorCount}. Stderr:\n{stderr}");
        var src = runner.GetVariable("src").As<AudioBuffer>();
        var doubled = runner.GetVariable("doubled").As<AudioBuffer>();
        Assert.Equal(src.Data.Length, doubled.Data.Length);
        Assert.True(src.Data.Length > 0, "Sine tone buffer must be non-empty");

        // Per-sample: doubled[i] == src[i] * 2.0 (no clamp — VolumeEffect mirrors GainEffect:
        // it warns on clip but doesn't actually saturate the buffer; that's the user's call
        // via compress / a future limiter).
        int compared = 0;
        for (int i = 0; i < src.Data.Length; i++)
        {
            if (MathF.Abs(src.Data[i]) < 1e-6f) continue;
            float expected = src.Data[i] * 2.0f;
            Assert.True(MathF.Abs(doubled.Data[i] - expected) < 1e-6f,
                $"Per-sample mismatch at i={i}: doubled={doubled.Data[i]}, expected={expected}");
            compared++;
        }
        Assert.True(compared > 100, $"Expected at least 100 comparable samples, got {compared}.");

        Assert.Contains("Warning: volume(2", stderr);
        Assert.Contains("clipping", stderr, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Volume_NegativeMultiplier_Rejected()
    {
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(@"
use ""@std""
use ""@audio""
Buffer src = (createSineTone 0.05 220.0 0.5)
Buffer bad = (volume src -0.5)
");
        Assert.True(errorCount > 0,
            $"Expected errorCount > 0 for negative volume multiplier, got {errorCount}. Stderr:\n{stderr}");
        // VolumeEffect's InvalidOperationException message should mention "non-negative" or "negative"
        // (see RESEARCH Code Examples §4 line 356).
        bool mentionsNonNegative = stderr.Contains("non-negative", StringComparison.OrdinalIgnoreCase)
                                 || stderr.Contains("negative", StringComparison.OrdinalIgnoreCase);
        Assert.True(mentionsNonNegative,
            $"Expected stderr to mention 'non-negative' or 'negative' for the rejected volume call, got: {stderr}");
    }

    /// <summary>
    /// CONTEXT D-07 sentinel — STAYS GREEN through all of Phase 26.2.
    ///
    /// Verifies <c>(gain buf 0.5)</c> resolves to the existing dB-interpreting
    /// <c>gain(Buffer, Double)</c> overload — applying +0.5 dB (≈ 1.0593× linear)
    /// rather than getting reinterpreted as a linear-half multiplier. If this
    /// fact regresses, we have silently flipped <c>gain</c> from dB to linear
    /// semantics — the exact policy mistake D-07 forbids.
    /// </summary>
    [Fact]
    public void Gain_BareDoubleSubOne_StillDecibel_RegressionCanary()
    {
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(@"
use ""@std""
use ""@audio""
Buffer src       = (createSineTone 0.05 220.0 0.5)
Buffer attenDb05 = (gain src 0.5)
Buffer attenDb00 = (gain src 0.0)
");
        Assert.True(errorCount == 0, $"Expected no errors, got {errorCount}. Stderr:\n{stderr}");
        var attenDb05 = runner.GetVariable("attenDb05").As<AudioBuffer>();
        var attenDb00 = runner.GetVariable("attenDb00").As<AudioBuffer>();
        Assert.Equal(attenDb00.Data.Length, attenDb05.Data.Length);
        Assert.True(attenDb05.Data.Length > 0, "Sine tone buffer must be non-empty");

        // 0.5 dB == 10^(0.5/20) ≈ 1.0593 linear; 0 dB == 1.0
        // So attenDb05 / attenDb00 ≈ 1.0593 per non-zero sample.
        const float expectedRatio = 1.0593f;
        int compared = 0;
        for (int i = 0; i < attenDb00.Data.Length; i++)
        {
            if (MathF.Abs(attenDb00.Data[i]) < 1e-6f) continue;
            float ratio = attenDb05.Data[i] / attenDb00.Data[i];
            Assert.InRange(ratio, expectedRatio - 0.001f, expectedRatio + 0.001f);
            compared++;
        }
        Assert.True(compared > 100,
            $"Expected at least 100 comparable samples, got {compared} — fact did not exercise gain path. " +
            $"If this fact regresses while compared count drops, gain may have silently flipped from dB to linear.");
    }

    [Fact]
    public void Volume_FloatArgument_WidensToDouble()
    {
        using var runner = new FlowEngineRunner();
        var (_, _, stderr, errorCount) = runner.RunSource(@"
use ""@std""
use ""@audio""
Float multF   = 0.75
Buffer src    = (createSineTone 0.05 220.0 0.5)
Buffer scaled = (volume src multF)
");
        Assert.True(errorCount == 0, $"Expected no errors, got {errorCount}. Stderr:\n{stderr}");
        var src = runner.GetVariable("src").As<AudioBuffer>();
        var scaled = runner.GetVariable("scaled").As<AudioBuffer>();
        Assert.Equal(src.Data.Length, scaled.Data.Length);

        int compared = 0;
        for (int i = 0; i < src.Data.Length; i++)
        {
            if (MathF.Abs(src.Data[i]) < 1e-6f) continue;
            Assert.True(MathF.Abs(scaled.Data[i] - src.Data[i] * 0.75f) < 1e-6f,
                $"Per-sample mismatch at i={i}: scaled={scaled.Data[i]}, expected={src.Data[i] * 0.75f}");
            compared++;
        }
        Assert.True(compared > 100, $"Expected at least 100 comparable samples, got {compared}.");
    }
}
