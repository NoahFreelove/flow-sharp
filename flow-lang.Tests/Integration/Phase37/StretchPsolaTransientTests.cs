using System;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.DSP;
using FlowLang.Tests.Helpers;
using Xunit;

namespace FlowLang.Tests.Integration.Phase37;

/// <summary>
/// Phase 37 DSP-02 — <c>(stretch buf 2.0 mode=#psola)</c> preserves the
/// transient onset position within +/- 5 ms after a 2× time-stretch. Filled
/// by Plan 37-02 Task 1 (PSOLA core + YIN pitch detector + W4 LOCK override).
/// </summary>
[Collection("FlowScripts")]
public class StretchPsolaTransientTests : IDisposable
{
    public StretchPsolaTransientTests()
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
    /// Task 1 acceptance: a 200 ms synthetic kick stretched 2× via PSOLA
    /// preserves its onset within 5 ms (220 frames at 44.1 kHz). The first
    /// sample whose magnitude exceeds 0.1 should land in [0, 220].
    /// </summary>
    [Fact]
    public void Psola_Kick_StretchedBy2_PreservesOnset()
    {
        var input = FileIO.LoadWavInternal(Phase37Fixtures.FixturePath("kick_hit.wav"));
        Assert.Equal(44100, input.SampleRate);

        var stretched = Psola.Process(input, factor: 2.0);

        // Find first significant sample (|sample| > 0.1).
        int onsetFrame = -1;
        for (int i = 0; i < stretched.Frames; i++)
        {
            if (Math.Abs(stretched.Data[i]) > 0.1f)
            {
                onsetFrame = i;
                break;
            }
        }
        Assert.True(onsetFrame >= 0,
            $"expected to find a transient onset in the stretched kick; scanned {stretched.Frames} frames");
        // 5 ms tolerance at 44.1 kHz = 220 samples (RESEARCH §Pattern 2).
        Assert.InRange(onsetFrame, 0, 220);
    }

    /// <summary>
    /// W4 LOCK: when <c>pitchPeriodOverride</c> is supplied, YIN detection
    /// MUST be skipped — the supplied period drives epoch spacing. Verified
    /// indirectly by passing a deliberately wrong override (200 samples on
    /// a 440 Hz sine whose true period is ~100 samples) and confirming the
    /// result differs from the YIN-detected path (proves the override took
    /// effect rather than YIN winning).
    /// </summary>
    [Fact]
    public void Psola_WithPitchPeriodOverride_SkipsYin()
    {
        var input = FileIO.LoadWavInternal(Phase37Fixtures.FixturePath("sine_440.wav"));

        // Default path — YIN finds ~100 sample period for 440 Hz sine.
        var resultYin = Psola.Process(input, factor: 1.5);

        // Override path — force 200-sample period; YIN bypassed entirely.
        var resultOverride = Psola.Process(input, factor: 1.5,
            pitchPeriodOverride: 200);

        // Both must produce the same length (depends only on factor).
        Assert.Equal(resultYin.Frames, resultOverride.Frames);

        // But the byte content must differ — proves override changed epoch
        // spacing (and therefore grain placement).
        bool anyDifference = false;
        int samplesToCheck = Math.Min(resultYin.Data.Length, resultOverride.Data.Length);
        for (int i = 0; i < samplesToCheck; i++)
        {
            if (Math.Abs(resultYin.Data[i] - resultOverride.Data[i]) > 1e-4f)
            {
                anyDifference = true;
                break;
            }
        }
        Assert.True(anyDifference,
            "expected pitchPeriodOverride to change the output bytes — proves YIN was bypassed");

        // Additionally, override with windowSizeOverride to verify the grain
        // length is honored separately from the period (W4 LOCK acceptance).
        var resultBothOverrides = Psola.Process(input, factor: 1.5,
            pitchPeriodOverride: 200, windowSizeOverride: 600);

        bool windowChangedOutput = false;
        for (int i = 0; i < samplesToCheck; i++)
        {
            if (Math.Abs(resultOverride.Data[i] - resultBothOverrides.Data[i]) > 1e-4f)
            {
                windowChangedOutput = true;
                break;
            }
        }
        Assert.True(windowChangedOutput,
            "expected windowSizeOverride to change the output bytes vs same period without windowSize override");
    }

    /// <summary>
    /// Sanity: YIN detects the fundamental period of a 440 Hz sine to within
    /// 5% accuracy (period ≈ 100 samples at 44.1 kHz; tolerance ±5).
    /// </summary>
    [Fact]
    public void Psola_DetectPitchPeriod_440Hz_Within5Percent()
    {
        int sampleRate = 44100;
        // Build a 2048-sample 440 Hz sine frame.
        var frame = new float[2048];
        for (int n = 0; n < 2048; n++)
        {
            frame[n] = 0.5f * (float)Math.Sin(2.0 * Math.PI * 440.0 * n / sampleRate);
        }

        int period = Psola.DetectPitchPeriod(frame, sampleRate);
        // 44100 / 440 = 100.227 samples — accept [95, 105].
        Assert.InRange(period, 95, 105);
    }
}
