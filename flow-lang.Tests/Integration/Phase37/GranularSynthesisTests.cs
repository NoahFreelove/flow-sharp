using System;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.DSP;
using Xunit;

namespace FlowLang.Tests.Integration.Phase37;

/// <summary>
/// Phase 37 DSP-01 — granular builtin composability with reverb / gain / pan /
/// filter. Filled by Plan 37-01 Task 3 (this plan) alongside
/// <c>GranularEngine.cs</c> + <c>GranularFunctions.cs</c>.
/// </summary>
[Collection("FlowScripts")]
public class GranularSynthesisTests : IDisposable
{
    public GranularSynthesisTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    private static AudioBuffer MakeSine(double seconds, double freq, int sampleRate)
    {
        int frames = (int)(seconds * sampleRate);
        var buf = new AudioBuffer(frames, 1, sampleRate);
        for (int i = 0; i < frames; i++)
        {
            buf.Data[i] = (float)Math.Sin(2.0 * Math.PI * freq * i / sampleRate);
        }
        return buf;
    }

    /// <summary>
    /// Smoke: granular returns a Buffer with the same Frames / Channels /
    /// SampleRate as input (granular is texture, NOT time-stretch).
    /// </summary>
    [Fact]
    public void Granular_ReturnsBuffer_SameLengthAndChannels()
    {
        var input = MakeSine(2.0, 440.0, 44100);
        var prng = new PrngRegistry();
        var result = GranularEngine.Apply(
            input,
            grainSeconds: 0.050,
            densityHz: 20.0,
            jitter: 0.3,
            window: WindowKind.Hann,
            prng,
            FlowLang.Core.SourceLocation.Unknown);

        Assert.NotNull(result);
        Assert.Equal(input.Frames, result.Frames);
        Assert.Equal(input.Channels, result.Channels);
        Assert.Equal(input.SampleRate, result.SampleRate);
    }

    /// <summary>
    /// Composability: granular → reverb chain produces non-zero output across
    /// the majority of the buffer (verifies the output buffer is intact for
    /// downstream effect chaining).
    /// </summary>
    [Fact]
    public void Granular_ComposesWithReverb()
    {
        var input = MakeSine(1.0, 440.0, 44100);
        var prng = new PrngRegistry();
        var grained = GranularEngine.Apply(
            input, 0.050, 20.0, 0.3, WindowKind.Hann, prng,
            FlowLang.Core.SourceLocation.Unknown);
        var reverbed = Reverb.Apply(grained, roomSize: 0.5f, damping: 0.5f, mix: 0.3f);

        // audit-0609 §3.8: Reverb.Apply now extends the output beyond the input
        // to carry the decay tail — the output is at LEAST as long as the input.
        Assert.True(reverbed.Frames >= input.Frames,
            $"granular→reverb output ({reverbed.Frames} frames) must be at least as long as input ({input.Frames} frames)");
        // Non-zero samples should cover the majority of the original-length portion.
        int inputSamples = input.Frames * reverbed.Channels;
        int nonZero = 0;
        for (int i = 0; i < inputSamples; i++)
            if (Math.Abs(reverbed.Data[i]) > 1e-6f) nonZero++;
        Assert.True(nonZero > inputSamples / 2,
            $"expected granular→reverb chain to produce non-zero output across >50% of input-length samples; got {nonZero}/{inputSamples}");
    }

    /// <summary>
    /// Input validation per Security Domain V5: grain &lt;= 0 OR density &lt;= 0
    /// throw <see cref="ArgumentException"/> at entry. Composer-supplied
    /// pathological values are rejected up front rather than producing
    /// silently broken output.
    /// </summary>
    [Fact]
    public void Granular_ThrowsOnInvalidGrain()
    {
        var input = MakeSine(1.0, 440.0, 44100);
        var prng = new PrngRegistry();

        Assert.Throws<ArgumentException>(() =>
            GranularEngine.Apply(input, grainSeconds: 0.0, densityHz: 20.0,
                jitter: 0.3, window: WindowKind.Hann, prng,
                FlowLang.Core.SourceLocation.Unknown));

        Assert.Throws<ArgumentException>(() =>
            GranularEngine.Apply(input, grainSeconds: 0.050, densityHz: -1.0,
                jitter: 0.3, window: WindowKind.Hann, prng,
                FlowLang.Core.SourceLocation.Unknown));
    }

    /// <summary>
    /// sweep-0614 regression — a grain LONGER than the source buffer must not
    /// collapse to near-silence. Pre-fix grainSamples was unclamped, so the
    /// Hann window's unity peak (at grainSamples/2) landed far past the buffer
    /// end; OverlapAddGrain only ever applied the near-zero leading edge of the
    /// window, attenuating a constant-0.5 buffer to a peak of ~0.0006. After
    /// the clamp-to-buffer-length fix the window spans the buffer and energy is
    /// preserved (peak near the input amplitude). Also asserts the charitable
    /// one-shot advisory fires.
    /// </summary>
    [Fact]
    public void Granular_GrainLongerThanBuffer_PreservesEnergy_AndWarns()
    {
        // 500-sample constant-0.5 buffer; grain = 1.0s (= 44100 samples >> 500).
        const int frames = 500;
        var input = new AudioBuffer(frames, 1, 44100);
        for (int i = 0; i < frames; i++) input.Data[i] = 0.5f;

        var prng = new PrngRegistry();
        var site = new FlowLang.Core.SourceLocation(7, 3);
        var result = GranularEngine.Apply(
            input, grainSeconds: 1.0, densityHz: 10.0, jitter: 0.0,
            window: WindowKind.Hann, prng, site);

        Assert.Equal(frames, result.Frames);

        float peak = 0f;
        for (int i = 0; i < result.Data.Length; i++)
            peak = Math.Max(peak, Math.Abs(result.Data[i]));

        // Pre-fix peak ~0.0006 (effective silence). Post-fix the Hann peak
        // (~1.0) lands mid-buffer on real 0.5 samples → peak near 0.5.
        Assert.True(peak > 0.25f,
            $"expected grain-clamp to preserve energy (peak near input 0.5); got peak {peak:F6}");

        Assert.True(
            RenderingDiagnostics.WasWarnedForTesting($"granular:grain-clamp:{site.Line}:{site.Column}"),
            "expected a one-shot [granular] grain-clamp advisory to fire");
    }

    /// <summary>
    /// jitter = 0.0 produces deterministic non-stochastic output — grains
    /// land at exact density intervals, identical source offset each pass.
    /// (The PRNG IS consulted — the jitter multiplier zeros the draw
    /// envelope, so the offsets collapse to t and emit to t.)
    /// </summary>
    [Fact]
    public void Granular_JitterZero_IsDeterministic()
    {
        var input = MakeSine(1.0, 440.0, 44100);
        var prng1 = new PrngRegistry();
        var prng2 = new PrngRegistry();

        var r1 = GranularEngine.Apply(input, 0.050, 20.0, 0.0, WindowKind.Hann,
            prng1, FlowLang.Core.SourceLocation.Unknown);
        var r2 = GranularEngine.Apply(input, 0.050, 20.0, 0.0, WindowKind.Hann,
            prng2, FlowLang.Core.SourceLocation.Unknown);

        Assert.Equal(r1.Data.Length, r2.Data.Length);
        for (int i = 0; i < r1.Data.Length; i++)
        {
            Assert.Equal(r1.Data[i], r2.Data[i]);
        }
    }
}
