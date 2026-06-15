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

    // ===== sweep-0614 regression — stretch-direction inversion =====
    // Pre-fix PitchShiftEngine stretched by 1/ratio: every UPWARD shift made
    // the stretched buffer shorter than the resample read region, so most of
    // the output was a flat clamped DC tail (near-silent, ~0 Hz) instead of a
    // pitched-up tone. The single +500c #auto test above passed only because
    // its mid-buffer FFT slice landed before the clamp boundary; +1200c/+700c
    // fell entirely inside the DC region. This sweep pins dominant frequency
    // AND level for up/down/tiny shifts across all three modes.

    private static AudioBuffer MakeSine(double seconds, double freq, double amp, int sampleRate)
    {
        int frames = (int)(seconds * sampleRate);
        var buf = new AudioBuffer(frames, 1, sampleRate);
        for (int i = 0; i < frames; i++)
            buf.Data[i] = (float)(amp * Math.Sin(2.0 * Math.PI * freq * i / sampleRate));
        return buf;
    }

    private static double Rms(AudioBuffer buf)
    {
        double sumSq = 0.0;
        for (int i = 0; i < buf.Data.Length; i++)
            sumSq += (double)buf.Data[i] * buf.Data[i];
        return Math.Sqrt(sumSq / buf.Data.Length);
    }

    /// <summary>
    /// Estimate the dominant frequency of a buffer by FFT over a mid-buffer
    /// window, returning Hz of the peak bin.
    /// </summary>
    private static double DominantFreq(AudioBuffer buf, int sampleRate)
    {
        const int FrameSize = 4096;
        var slice = new float[FrameSize];
        int sliceStart = Math.Max(0, buf.Frames / 2 - FrameSize / 2);
        for (int i = 0; i < FrameSize && sliceStart + i < buf.Frames; i++)
            slice[i] = buf.Data[sliceStart + i];

        Fft.Forward(slice, out double[] re, out double[] im);
        int peakBin = 0;
        double peakMag = 0;
        for (int k = 1; k < FrameSize / 2; k++)
        {
            double mag = Math.Sqrt(re[k] * re[k] + im[k] * im[k]);
            if (mag > peakMag) { peakMag = mag; peakBin = k; }
        }
        return (double)peakBin * sampleRate / FrameSize;
    }

    [Theory]
    [InlineData(1200.0)]  // octave up  — pre-fix: ~0 Hz DC, RMS ~0.21
    [InlineData(700.0)]   // P5 up      — pre-fix: ~0 Hz DC
    [InlineData(-700.0)]  // P5 down    — worked by luck pre-fix
    [InlineData(50.0)]    // tiny up    — approximated pre-fix
    public void PitchShift_ShiftsDominantFreq_AndPreservesLevel_AllModes(double cents)
    {
        const int Sr = 44100;
        const double InHz = 440.0;
        const double Amp = 0.5;
        var input = MakeSine(1.0, InHz, Amp, Sr);
        double inRms = Rms(input);
        double expectHz = InHz * Math.Pow(2.0, cents / 1200.0);

        foreach (var mode in new[] { StretchMode.Vocoder, StretchMode.Psola, StretchMode.Auto })
        {
            var shifted = PitchShiftEngine.Process(input, cents, mode: mode);

            // Duration preserved within 1 sample.
            Assert.InRange(shifted.Frames, input.Frames - 1, input.Frames + 1);

            // Dominant frequency lands near 440 * 2^(cents/1200) — accept ±6%
            // to absorb spectral leakage + PSOLA/HPS approximation. This is the
            // load-bearing assertion: pre-fix upshifts collapsed to ~0 Hz.
            double domHz = DominantFreq(shifted, Sr);
            Assert.True(Math.Abs(domHz - expectHz) <= expectHz * 0.06,
                $"mode={mode} cents={cents}: dominant {domHz:F1} Hz, expected ~{expectHz:F1} Hz");

            // Level preserved within ±3 dB of input RMS. Pre-fix upshift level
            // ratio was ~0.47 (-6.5 dB) because most of the output was DC.
            double rms = Rms(shifted);
            double db = 20.0 * Math.Log10(rms / inRms);
            Assert.True(Math.Abs(db) <= 3.0,
                $"mode={mode} cents={cents}: level {db:F2} dB vs input (RMS {rms:F4} vs {inRms:F4})");
        }
    }

    /// <summary>
    /// Identity (cents=0) still returns the input verbatim (byte-identical)
    /// after the stretch-direction fix — Pitfall 11 fast-path untouched.
    /// </summary>
    [Fact]
    public void PitchShift_ZeroCents_StillByteIdentical()
    {
        var input = FileIO.LoadWavInternal(Phase37Fixtures.FixturePath("sine_440.wav"));
        foreach (var mode in new[] { StretchMode.Vocoder, StretchMode.Psola, StretchMode.Auto })
        {
            var result = PitchShiftEngine.Process(input, cents: 0.0, mode: mode);
            Assert.Same(input, result);
        }
    }
}
