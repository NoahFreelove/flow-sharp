using System;
using System.IO;
using FlowLang.Diagnostics;
using FlowLang.StandardLibrary.Audio;
using Xunit;

namespace FlowLang.Tests.Integration.Phase38;

/// <summary>
/// Phase 38 Plan 38-05 AUDIO-IN-02 — micBuffer linear-interp resample tests.
///
/// Uses the <see cref="InputFunctions.CaptureOverride"/> seam to inject fixture
/// data at known native rates (48 000 + 44 100). Asserts:
///   - native 48 000 → output 44 100 with frame count = 44 100 ± 1 sample
///     (per 38-VALIDATION.md line 63 tolerance)
///   - native 44 100 → identity passthrough, no resample advisory fires
///   - one-shot resample advisory dedup keyed by native rate
///     ("audio-in-resample:&lt;N&gt;")
/// </summary>
[Collection("FlowScripts")]
public class MicBufferResampleTests : IDisposable
{
    private readonly TextWriter _origErr;
    private readonly StringWriter _errCapture;
    private readonly Func<int, int, double, float[]?>? _origOverride;

    public MicBufferResampleTests()
    {
        RenderingDiagnostics.ResetForTesting();
        _origErr = Console.Error;
        _errCapture = new StringWriter();
        Console.SetError(_errCapture);
        _origOverride = InputFunctions.CaptureOverride;
    }

    public void Dispose()
    {
        Console.SetError(_origErr);
        _errCapture.Dispose();
        InputFunctions.CaptureOverride = _origOverride;
        RenderingDiagnostics.ResetForTesting();
    }

    /// <summary>
    /// Helper: synthetic 440 Hz sine fixture at the requested rate. Mirrors
    /// the Phase38FixtureGenerator formula so test+fixture stay aligned even
    /// when the on-disk WAV is regenerated.
    /// </summary>
    private static Func<int, int, double, float[]?> SineSeam(double freqHz, int forceRate)
        => (_, channels, seconds) =>
        {
            int frames = (int)(seconds * forceRate);
            var samples = new float[frames * channels];
            for (int f = 0; f < frames; f++)
            {
                float v = (float)(Math.Sin(2.0 * Math.PI * freqHz * f / forceRate) * 0.5);
                for (int ch = 0; ch < channels; ch++)
                    samples[f * channels + ch] = v;
            }
            return samples;
        };

    /// <summary>
    /// Native 48 kHz fixture → output 44 100 Hz buffer with frame count
    /// within ±1 sample of 44 100 (1 second of capture). VALIDATION.md
    /// line 63 tolerance.
    /// </summary>
    [Fact]
    public void MicBuffer_NativeRate48000_ResamplesTo44100PreservingDuration()
    {
        InputFunctions.NativeRateForTesting = 48_000;
        InputFunctions.CaptureOverride = SineSeam(440.0, 48_000);

        var buf = InputFunctions.MicBufferForTesting(durationSeconds: 1.0);

        Assert.NotNull(buf);
        Assert.Equal(44_100, buf!.SampleRate);
        Assert.InRange(buf.Frames, 44_099, 44_101);
    }

    /// <summary>
    /// 48 kHz → 44.1 kHz fires the resample advisory exactly once per native
    /// rate per process. Two consecutive calls at the same native rate must
    /// dedup.
    /// </summary>
    [Fact]
    public void MicBuffer_NativeRate48000_EmitsResampleAdvisoryOnce()
    {
        InputFunctions.NativeRateForTesting = 48_000;
        InputFunctions.CaptureOverride = SineSeam(440.0, 48_000);

        InputFunctions.MicBufferForTesting(durationSeconds: 0.1);
        InputFunctions.MicBufferForTesting(durationSeconds: 0.1);

        string captured = _errCapture.ToString();
        const string needle = "[audio-in] resampling capture stream from 48000 Hz to 44100 Hz (linear interpolation)";
        int firstIdx = captured.IndexOf(needle, StringComparison.Ordinal);
        int secondIdx = captured.IndexOf(needle, firstIdx + 1, StringComparison.Ordinal);

        Assert.NotEqual(-1, firstIdx);
        Assert.Equal(-1, secondIdx);
    }

    /// <summary>
    /// Native 44.1 kHz → identity (no resample, no advisory). Frame count
    /// matches the requested duration exactly.
    /// </summary>
    [Fact]
    public void MicBuffer_NativeRate44100_NoResample()
    {
        InputFunctions.NativeRateForTesting = 44_100;
        InputFunctions.CaptureOverride = SineSeam(440.0, 44_100);

        var buf = InputFunctions.MicBufferForTesting(durationSeconds: 1.0);

        Assert.NotNull(buf);
        Assert.Equal(44_100, buf!.SampleRate);
        Assert.Equal(44_100, buf.Frames);
        Assert.DoesNotContain("[audio-in] resampling", _errCapture.ToString());
    }
}
