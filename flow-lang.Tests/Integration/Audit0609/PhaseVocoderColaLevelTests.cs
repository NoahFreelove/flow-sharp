using System;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.DSP;
using Xunit;

namespace FlowLang.Tests.Integration.Audit0609;

/// <summary>
/// Audit 2026-06-09 §3.2 — <see cref="PhaseVocoder"/> overlap-add had NO COLA
/// normalization: sqrt-Hann analysis × sqrt-Hann synthesis gave a Hann-weighted
/// OLA contribution that was never divided by the accumulated window energy.
/// Reconstruction gain was therefore ≈ 1/factor at the 2048/512 defaults —
/// the verifier measured +6.0 dB as factor→1, exactly 0 dB at factor 2 (which
/// is why the factor-2 tests never caught it), +12 dB at factor 0.5, and severe
/// amplitude-modulation ripple past factor 2. Combined with StretchEngine's
/// factor==1.0 identity fast-path, <c>(stretch buf 1.001)</c> jumped +6 dB vs
/// <c>(stretch buf 1.0)</c>.
///
/// <para>The fix accumulates a parallel window-energy array at each synthesis
/// write position and divides the OLA output by it (epsilon floor), so output
/// level is unity and factor-INDEPENDENT. These tests fail before the fix
/// (off by +12 / +6 / +2.5 / −2.9 dB across the sweep) and pass after.</para>
/// </summary>
public class PhaseVocoderColaLevelTests
{
    private const int SampleRate = 44100;

    private static AudioBuffer Sine(double hz, double seconds, double amp = 0.5)
    {
        int frames = (int)(seconds * SampleRate);
        var b = new AudioBuffer(frames, 1, SampleRate);
        for (int n = 0; n < frames; n++)
            b.Data[n] = (float)(amp * Math.Sin(2.0 * Math.PI * hz * n / SampleRate));
        return b;
    }

    /// <summary>RMS over [start, start+len) on channel-0 of a mono buffer.</summary>
    private static double Rms(AudioBuffer b, int start, int len)
    {
        double s = 0; int c = 0;
        for (int i = start; i < start + len && i < b.Frames; i++)
        {
            double v = b.Data[i];
            s += v * v; c++;
        }
        return c == 0 ? 0.0 : Math.Sqrt(s / c);
    }

    /// <summary>RMS of the middle third — avoids the OLA edge ramp-up/down.</summary>
    private static double MidRms(AudioBuffer b)
    {
        int third = b.Frames / 3;
        return Rms(b, third, third);
    }

    /// <summary>
    /// LEVEL CONTINUITY AT IDENTITY (audit acceptance #1): the RMS of
    /// <c>stretch(buf, 1.001)</c> must be within 0.5 dB of <c>stretch(buf, 1.0)</c>
    /// for a steady sine in vocoder mode. Before the fix the 1.001 path was
    /// +6 dB louder than the 1.0 identity fast-path — an audible jump.
    /// </summary>
    [Fact]
    public void Vocoder_LevelContinuityAtIdentity_Within0_5dB()
    {
        var input = Sine(440.0, 3.0);

        // factor==1.0 returns the input verbatim via the identity fast-path.
        var identity = StretchEngine.Process(input, 1.0, StretchMode.Vocoder);
        var near = StretchEngine.Process(input, 1.001, StretchMode.Vocoder);

        double dbIdentity = MidRms(identity);
        double dbNear = MidRms(near);
        double deltaDb = 20.0 * Math.Log10(dbNear / Math.Max(dbIdentity, 1e-12));

        Assert.True(Math.Abs(deltaDb) <= 0.5,
            $"stretch(buf, 1.001) must be within 0.5 dB of stretch(buf, 1.0); " +
            $"got {deltaDb:+0.00;-0.00} dB (identity RMS={dbIdentity:F5}, near RMS={dbNear:F5})");
    }

    /// <summary>
    /// FACTOR SWEEP (audit acceptance #2): vocoder-stretch RMS must stay within
    /// ±1 dB of the input RMS at factors {0.5, 1.5, 2.0, 3.0} for a steady sine.
    /// Before the fix the sweep was {+12, +2.5, 0, −2.9} dB.
    /// </summary>
    [Theory]
    [InlineData(0.5)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    [InlineData(3.0)]
    public void Vocoder_FactorSweep_RmsWithin1dBOfInput(double factor)
    {
        var input = Sine(440.0, 3.0);
        double inputRms = Rms(input, 0, input.Frames);

        var stretched = PhaseVocoder.Process(input, factor, 2048, 512, 4);
        double outRms = MidRms(stretched);
        double deltaDb = 20.0 * Math.Log10(outRms / Math.Max(inputRms, 1e-12));

        Assert.True(Math.Abs(deltaDb) <= 1.0,
            $"vocoder stretch at factor {factor} must be within ±1 dB of input RMS; " +
            $"got {deltaDb:+0.00;-0.00} dB (input RMS={inputRms:F5}, out RMS={outRms:F5})");
    }

    /// <summary>
    /// Two-run cmp-clean: the same source stretched twice must be byte-identical
    /// (the COLA normalization is a pure function of the input — no PRNG).
    /// </summary>
    [Fact]
    public void Vocoder_TwoRun_ByteIdentical()
    {
        var input = Sine(440.0, 2.0);

        var a = PhaseVocoder.Process(input, 1.5, 2048, 512, 4);
        var b = PhaseVocoder.Process(input, 1.5, 2048, 512, 4);

        Assert.Equal(a.Frames, b.Frames);
        Assert.Equal(a.Data.Length, b.Data.Length);
        for (int i = 0; i < a.Data.Length; i++)
            Assert.Equal(a.Data[i], b.Data[i]);
    }
}
