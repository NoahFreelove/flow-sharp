using System;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.DSP;
using Xunit;

namespace FlowLang.Tests.Integration.Phase37;

/// <summary>
/// Phase 37 DSP-01 — granular jitter PRNG routes through
/// <see cref="PrngRegistry"/> per D-v1.5-06. Two consecutive renders at the
/// same git SHA produce byte-identical output. Filled by Plan 37-01 Task 3
/// (this plan) alongside <c>GranularEngine.cs</c>.
/// </summary>
[Collection("FlowScripts")]
public class GranularDeterminismTests : IDisposable
{
    public GranularDeterminismTests()
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
    /// Two distinct <see cref="PrngRegistry"/> instances called with the same
    /// SourceLocation + args produce byte-identical output. Verifies the
    /// per-key Random is seeded deterministically (FNV-1a from
    /// site + name + salt — independent of process state).
    /// </summary>
    [Fact]
    public void Granular_TwoRuns_SamePrngRegistry_AreByteIdentical()
    {
        var input = MakeSine(1.0, 440.0, 44100);
        var site = new FlowLang.Core.SourceLocation(42, 7, "<granular-determinism>");

        var prng1 = new PrngRegistry();
        var prng2 = new PrngRegistry();

        var r1 = GranularEngine.Apply(input, 0.050, 20.0, 0.3, WindowKind.Hann, prng1, site);
        var r2 = GranularEngine.Apply(input, 0.050, 20.0, 0.3, WindowKind.Hann, prng2, site);

        Assert.Equal(r1.Data.Length, r2.Data.Length);
        for (int i = 0; i < r1.Data.Length; i++)
        {
            Assert.Equal(r1.Data[i], r2.Data[i]);
        }
    }

    /// <summary>
    /// <see cref="PrngRegistry.ResetAtRenderBoundary"/> resets PRNG state so
    /// the call AFTER reset produces the same bytes as the FIRST call. This
    /// is the contract that makes two-run cmp-clean determinism hold across
    /// <c>renderSong</c> / <c>writeWav</c> boundaries.
    /// </summary>
    [Fact]
    public void Granular_ResetAtRenderBoundary_ResetsPrng()
    {
        var input = MakeSine(1.0, 440.0, 44100);
        var site = new FlowLang.Core.SourceLocation(99, 1, "<granular-reset>");
        var prng = new PrngRegistry();

        var first = GranularEngine.Apply(input, 0.050, 20.0, 0.3, WindowKind.Hann, prng, site);

        // Reset the registry — the next call must restart from the same seed.
        prng.ResetAtRenderBoundary();

        var afterReset = GranularEngine.Apply(input, 0.050, 20.0, 0.3, WindowKind.Hann, prng, site);

        Assert.Equal(first.Data.Length, afterReset.Data.Length);
        for (int i = 0; i < first.Data.Length; i++)
        {
            Assert.Equal(first.Data[i], afterReset.Data[i]);
        }
    }
}
