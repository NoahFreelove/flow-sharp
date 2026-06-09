using System;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.DSP;
using Xunit;

namespace FlowLang.Tests.Integration.Audit0609;

/// <summary>
/// Audit 2026-06-09 §3.4 — <see cref="Psola"/> time-stretch mapped each input
/// epoch to <c>round(inEpoch × factor)</c>, so output epochs were spaced
/// <c>period × factor</c> apart while each Hann grain spanned only
/// <c>2 × period</c>. At factor 2 adjacent grains abutted at their near-zero
/// Hann tails → amplitude nulls at the pitch rate (buzzy tremolo); at factor &lt; 1
/// grains piled up above unity.
///
/// <para>The fix places output epochs on a uniform ONE-PERIOD grid over the
/// output length and sources each grain from the nearest input epoch
/// (<c>outEpoch / factor</c>) — constant 50% grain overlap and level at all
/// factors. These tests fail before the fix ((max−min)/mean ≈ 1.3, near
/// full-depth nulls) and pass after ((max−min)/mean &lt; 0.3).</para>
/// </summary>
public class PsolaStretchAmTests
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

    /// <summary>
    /// PSOLA AM (audit acceptance #3): for a steady 220 Hz sine stretched ×2 in
    /// #psola, the short-window RMS envelope over the middle of the output must
    /// have <c>(max − min) / mean &lt; 0.3</c>. Before the fix this was ~1.3
    /// (envelope dipped to near-zero every two pitch periods).
    /// </summary>
    [Fact]
    public void Psola_Steady220_StretchedBy2_NoFullDepthAmNulls()
    {
        var input = Sine(220.0, 1.0);
        var stretched = Psola.Process(input, factor: 2.0);

        // Short-window RMS envelope over the MIDDLE THIRD (avoids edge ramps).
        // 256-sample window ≈ 5.8 ms — long enough to span a 220 Hz period
        // (~200 samples) so the metric measures cross-period amplitude
        // modulation, not the within-period sine shape.
        const int Window = 256;
        int from = stretched.Frames / 3;
        int to = 2 * stretched.Frames / 3;

        double max = 0.0, min = double.MaxValue, sum = 0.0;
        int count = 0;
        for (int i = from; i + Window < to; i += Window)
        {
            double e = Rms(stretched, i, Window);
            max = Math.Max(max, e);
            min = Math.Min(min, e);
            sum += e;
            count++;
        }

        Assert.True(count > 0, "expected at least one envelope window in the middle third");
        double mean = sum / count;
        double depth = (max - min) / Math.Max(mean, 1e-12);

        Assert.True(depth < 0.3,
            $"PSOLA ×2 on a steady sine must not produce full-depth AM nulls; " +
            $"got (max−min)/mean = {depth:F4} (max={max:F5}, min={min:F5}, mean={mean:F5})");
    }

    /// <summary>
    /// Companion guard at factor &lt; 1: grains must not pile up above unity.
    /// The fix's one-period output grid keeps the level bounded; before the fix
    /// factor 0.5 piled grains and pushed the envelope above the input level.
    /// </summary>
    [Fact]
    public void Psola_Steady220_StretchedByHalf_StaysNearInputLevel()
    {
        var input = Sine(220.0, 1.0);
        double inputRms = Rms(input, 0, input.Frames);

        var stretched = Psola.Process(input, factor: 0.5);
        int third = stretched.Frames / 3;
        double midRms = Rms(stretched, third, third);

        // PSOLA is not COLA-perfect like the vocoder, so allow a wider window
        // (±3 dB) — the point is that the level does NOT blow up well above
        // unity from grain pile-up (pre-fix it overshot).
        double deltaDb = 20.0 * Math.Log10(midRms / Math.Max(inputRms, 1e-12));
        Assert.True(Math.Abs(deltaDb) <= 3.0,
            $"PSOLA ×0.5 must stay near input level (no grain pile-up); " +
            $"got {deltaDb:+0.00;-0.00} dB (input RMS={inputRms:F5}, out RMS={midRms:F5})");
    }

    /// <summary>
    /// Two-run cmp-clean: same source PSOLA-stretched twice → byte-identical
    /// (deterministic; no PRNG on the path).
    /// </summary>
    [Fact]
    public void Psola_TwoRun_ByteIdentical()
    {
        var input = Sine(220.0, 1.0);

        var a = Psola.Process(input, factor: 2.0);
        var b = Psola.Process(input, factor: 2.0);

        Assert.Equal(a.Frames, b.Frames);
        Assert.Equal(a.Data.Length, b.Data.Length);
        for (int i = 0; i < a.Data.Length; i++)
            Assert.Equal(a.Data[i], b.Data[i]);
    }
}
