using System;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.DSP;
using FlowLang.Tests.Helpers;
using Xunit;

namespace FlowLang.Tests.Integration.Phase37;

/// <summary>
/// Phase 37 DSP-02 — <c>(stretch buf 2.0 mode=#vocoder)</c> doubles audio
/// length within +/- frame slack and preserves the dominant pitch bin
/// (Pitfall 1 phasiness gate enforced via dominant-bin check). Filled by
/// Plan 37-02 Task 1 (phase vocoder core) + Task 2 (knob threading) +
/// Task 3 (peak-to-sideband ratio).
/// </summary>
[Collection("FlowScripts")]
public class StretchVocoderTests : IDisposable
{
    public StretchVocoderTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
        Phase37Fixtures.EnsureFixturesExist();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    /// <summary>
    /// Task 1 acceptance: a 440 Hz sine stretched 2× via the phase vocoder
    /// retains its dominant pitch (FFT bin survives the stretch). Output
    /// length is roughly 2× the input within frameSize slack.
    /// </summary>
    [Fact]
    public void PhaseVocoder_Sine440_StretchedBy2_PreservesPitch()
    {
        var input = FileIO.LoadWavInternal(Phase37Fixtures.FixturePath("sine_440.wav"));
        Assert.Equal(44100, input.SampleRate);
        Assert.Equal(1, input.Channels);

        const int FrameSize = 2048;
        var stretched = PhaseVocoder.Process(input, factor: 2.0,
            frameSize: FrameSize, hopSize: 512, overlap: 4);

        // Length: roughly 2× input ± frameSize slack.
        int expected = input.Frames * 2;
        Assert.InRange(stretched.Frames, expected - FrameSize, expected + 2 * FrameSize);

        // Pitch: dominant FFT bin in a representative slice should land on
        // the 440 Hz bin (20 ± 2 at frameSize=2048, sr=44100).
        // 440 Hz × 2048 / 44100 = bin 20.43 → expect peak at bin 20.
        var slice = new float[FrameSize];
        int sliceStart = stretched.Frames / 2 - FrameSize / 2; // mid-buffer slice
        for (int i = 0; i < FrameSize; i++) slice[i] = stretched.Data[sliceStart + i];

        Fft.Forward(slice, out double[] re, out double[] im);
        int peakBin = 0;
        double peakMag = 0;
        for (int k = 1; k < FrameSize / 2; k++)
        {
            double mag = Math.Sqrt(re[k] * re[k] + im[k] * im[k]);
            if (mag > peakMag)
            {
                peakMag = mag;
                peakBin = k;
            }
        }
        // Expected bin 20 (440 Hz); accept ±2 bins for windowing leakage.
        Assert.InRange(peakBin, 18, 22);
    }

    /// <summary>
    /// Pitfall 1 gate: a 440 Hz sine stretched 2× via identity phase locking
    /// must keep its peak bin well above the neighbouring sideband bins —
    /// 12 dB peak-to-nearest-sideband ratio is the threshold the plan locks
    /// in. Identity phase locking (Laroche-Dolson) is what makes this hold;
    /// a naive vocoder would smear into a noisy chord-like spectrum.
    /// </summary>
    [Fact]
    public void StretchVocoder_PhaseLocking_NoExcessivePhasiness()
    {
        var input = FileIO.LoadWavInternal(Phase37Fixtures.FixturePath("sine_440.wav"));

        const int FrameSize = 2048;
        var stretched = PhaseVocoder.Process(input, factor: 2.0,
            frameSize: FrameSize, hopSize: 512, overlap: 4);

        var slice = new float[FrameSize];
        int sliceStart = stretched.Frames / 2 - FrameSize / 2;
        for (int i = 0; i < FrameSize; i++) slice[i] = stretched.Data[sliceStart + i];

        Fft.Forward(slice, out double[] re, out double[] im);

        int peakBin = 0;
        double peakMag = 0;
        var magnitudes = new double[FrameSize / 2];
        for (int k = 1; k < FrameSize / 2; k++)
        {
            double mag = Math.Sqrt(re[k] * re[k] + im[k] * im[k]);
            magnitudes[k] = mag;
            if (mag > peakMag)
            {
                peakMag = mag;
                peakBin = k;
            }
        }
        // Skip the peak's main-lobe neighbours (±2 bins). Look at the next
        // bin outside the main lobe (±3 bins) — that's the first sideband.
        // Peak-to-sideband ≥ 12 dB == magnitude ratio ≥ 10^(12/20) ≈ 3.98.
        double sidebandMag = Math.Max(
            magnitudes[Math.Max(1, peakBin - 3)],
            magnitudes[Math.Min(FrameSize / 2 - 1, peakBin + 3)]);
        double dbRatio = 20.0 * Math.Log10(peakMag / Math.Max(sidebandMag, 1e-12));
        Assert.True(dbRatio >= 12.0,
            $"expected peak-to-sideband ≥ 12 dB (Pitfall 1 gate); got {dbRatio:F2} dB " +
            $"(peak={peakMag:F4} at bin {peakBin}, sideband={sidebandMag:F4})");
    }

    /// <summary>
    /// W4 LOCK: non-default vocoder knobs (frameSize=4096) reach
    /// <see cref="PhaseVocoder.Process"/> via <see cref="StretchEngine.Process"/>
    /// without being silently dropped. Smoke test — verifies no throw +
    /// reasonable output length at the non-default knob.
    /// </summary>
    [Fact]
    public void StretchEngine_VocoderKnobsThreaded_FrameSize4096_Works()
    {
        var input = FileIO.LoadWavInternal(Phase37Fixtures.FixturePath("sine_440.wav"));

        var result = StretchEngine.Process(input, factor: 2.0,
            mode: StretchMode.Vocoder,
            frameSize: 4096, hopSize: 1024, overlap: 4);

        Assert.NotNull(result);
        Assert.Equal(input.SampleRate, result.SampleRate);
        Assert.Equal(input.Channels, result.Channels);
        int expected = input.Frames * 2;
        // 4096-sized frame doubles the boundary slack.
        Assert.InRange(result.Frames, expected - 4096, expected + 2 * 4096);
    }
}
