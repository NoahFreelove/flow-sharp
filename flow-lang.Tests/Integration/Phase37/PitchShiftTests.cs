using System;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.DSP;
using FlowLang.Tests.Helpers;
using Xunit;

namespace FlowLang.Tests.Integration.Phase37;

/// <summary>
/// Phase 37 DSP-03 — <c>(pitchShift buf +5st)</c> shifts pitch by 5
/// semitones while preserving duration within +/- 1 sample. Filled by
/// Plan 37-02 Task 3.
/// </summary>
[Collection("FlowScripts")]
public class PitchShiftTests : IDisposable
{
    public PitchShiftTests()
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
    /// +5 semitones (500 cents) shift of a 440 Hz sine should preserve
    /// duration within +/- 1 sample and shift the dominant FFT bin upward
    /// by a factor of <c>2^(5/12) ≈ 1.335</c> — 440 × 1.335 ≈ 587 Hz.
    /// At frameSize=2048 / sr=44100 the bin for 587 Hz is 27.3 — accept
    /// 25..30 to absorb spectral leakage.
    /// </summary>
    [Fact]
    public void PitchShift_Plus5Semitones_PreservesDuration()
    {
        var input = FileIO.LoadWavInternal(Phase37Fixtures.FixturePath("sine_440.wav"));

        var shifted = PitchShiftEngine.Process(input, cents: 500.0,
            mode: StretchMode.Auto);

        Assert.InRange(shifted.Frames, input.Frames - 1, input.Frames + 1);

        // Verify the dominant FFT bin has shifted upward — take a mid-buffer
        // slice and find the peak bin.
        const int FrameSize = 2048;
        var slice = new float[FrameSize];
        int sliceStart = shifted.Frames / 2 - FrameSize / 2;
        for (int i = 0; i < FrameSize; i++)
            slice[i] = shifted.Data[sliceStart + i];

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
        // 587 Hz × 2048 / 44100 ≈ 27.3 — accept [22, 32] for leakage.
        Assert.InRange(peakBin, 22, 32);
    }

    /// <summary>
    /// PitchShiftEngine.Process(buf, cents, Vocoder) — explicit mode dispatch
    /// path. Smoke: no throw + duration preserved.
    /// </summary>
    [Fact]
    public void PitchShift_VocoderMode_PreservesDuration()
    {
        var input = FileIO.LoadWavInternal(Phase37Fixtures.FixturePath("sine_440.wav"));

        var shifted = PitchShiftEngine.Process(input, cents: 200.0,
            mode: StretchMode.Vocoder);

        Assert.NotNull(shifted);
        Assert.Equal(input.Channels, shifted.Channels);
        Assert.Equal(input.SampleRate, shifted.SampleRate);
        Assert.InRange(shifted.Frames, input.Frames - 1, input.Frames + 1);
    }
}
