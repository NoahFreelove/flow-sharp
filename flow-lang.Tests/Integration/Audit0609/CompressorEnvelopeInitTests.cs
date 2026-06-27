using System;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.DSP;
using Xunit;

namespace FlowLang.Tests.Integration.Audit0609;

/// <summary>
/// Audit 2026-06-09 §3.1 — the compressor's smoothed envelope is the APPLIED
/// GAIN in dB (0 = unity), but it was initialized to -96f (a leftover from a
/// signal-level follower), so every <c>compress</c>/<c>sidechain</c> call
/// release-curved up from silence: the first ~450 ms of output was effectively
/// muted and faded in. These tests pin the fix: with no gain reduction called
/// for, the first 100 ms must match the steady-state level.
/// </summary>
public class CompressorEnvelopeInitTests
{
    private const int SampleRate = 44100;
    private const int WindowFrames = SampleRate / 10; // 100 ms

    private static AudioBuffer ConstantSine(double amplitude, double seconds = 1.0)
    {
        int frames = (int)(seconds * SampleRate);
        var buf = new AudioBuffer(frames, 1, SampleRate);
        for (int i = 0; i < frames; i++)
            buf.SetSample(i, 0, (float)(amplitude * Math.Sin(2 * Math.PI * 220.0 * i / SampleRate)));
        return buf;
    }

    private static double Rms(AudioBuffer buf, int startFrame, int frameCount)
    {
        double sum = 0;
        int end = Math.Min(startFrame + frameCount, buf.Frames);
        int n = 0;
        for (int i = startFrame; i < end; i++)
        {
            float s = buf.GetSample(i, 0);
            sum += s * s;
            n++;
        }
        return n == 0 ? 0 : Math.Sqrt(sum / n);
    }

    [Fact]
    public void Compress_BelowThreshold_DoesNotFadeIn()
    {
        // Input peaks at -6 dBFS, threshold -3 dB → no gain reduction anywhere;
        // the output's first 100 ms must already be at steady-state level.
        var input = ConstantSine(0.5);
        var output = Compressor.Apply(input, thresholdDb: -3f, ratio: 4f);

        double head = Rms(output, 0, WindowFrames);
        double mid = Rms(output, output.Frames / 2, WindowFrames);

        Assert.True(mid > 1e-4, "steady-state output unexpectedly silent");
        double deltaDb = 20 * Math.Log10(head / mid);
        Assert.True(Math.Abs(deltaDb) < 0.5,
            $"first 100 ms is {deltaDb:F2} dB off steady state — compressor is fading in from the envelope floor");
    }

    [Fact]
    public void Sidechain_SilentTrigger_DoesNotDuckSourceAtStart()
    {
        // Trigger is pure silence → no ducking should ever engage; the source's
        // first 100 ms must pass through at steady-state level.
        var source = ConstantSine(0.5);
        var trigger = new AudioBuffer(source.Frames, 1, SampleRate); // all zeros
        var output = SidechainCompressor.Apply(source, trigger, thresholdDb: -20f, ratio: 8f);

        double head = Rms(output, 0, WindowFrames);
        double mid = Rms(output, output.Frames / 2, WindowFrames);

        Assert.True(mid > 1e-4, "steady-state output unexpectedly silent");
        double deltaDb = 20 * Math.Log10(head / mid);
        Assert.True(Math.Abs(deltaDb) < 0.5,
            $"first 100 ms is {deltaDb:F2} dB off steady state — sidechain is ducking a silent trigger");
    }
}
