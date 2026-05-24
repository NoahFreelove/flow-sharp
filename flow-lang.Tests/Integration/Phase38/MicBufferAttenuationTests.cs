using System;
using System.IO;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using Xunit;

namespace FlowLang.Tests.Integration.Phase38;

/// <summary>
/// Phase 38 Plan 38-05 AUDIO-IN-01 — micBuffer attenuation + advisory tests.
///
/// Uses the test seam <see cref="InputFunctions.CaptureOverride"/> injected at
/// test setup to bypass <see cref="FlowLang.Audio.PulseAudioCaptureBackend"/>
/// — CI cannot rely on a live PulseAudio daemon (RESEARCH §I line 1003 "test
/// seam" recommendation). The seam delivers a synthetic 0.5-amplitude sine at
/// the requested rate so we can assert the -20 dB attenuation scalar
/// (×0.1) is applied to every sample of the returned buffer.
///
/// One-shot stderr advisory dedup is exercised by capturing
/// <see cref="Console.Error"/> across two consecutive MicBuffer calls and
/// asserting the attenuation advisory appears EXACTLY ONCE — RenderingDiagnostics
/// is reset by the IDisposable lifecycle so a fresh process is simulated per Fact.
/// </summary>
[Collection("FlowScripts")]
public class MicBufferAttenuationTests : IDisposable
{
    private readonly TextWriter _origErr;
    private readonly StringWriter _errCapture;
    private readonly Func<int, int, double, float[]?>? _origOverride;

    public MicBufferAttenuationTests()
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
    /// Helper: seam delegate returning a steady 0.5-amplitude mono signal
    /// (NOT a sine — flat 0.5 makes the attenuation assertion crisp).
    /// </summary>
    private static Func<int, int, double, float[]?> ConstantSeam(float amplitude)
        => (rate, channels, seconds) =>
        {
            int frames = (int)(seconds * rate);
            var samples = new float[frames * channels];
            for (int i = 0; i < samples.Length; i++) samples[i] = amplitude;
            return samples;
        };

    /// <summary>
    /// Single MicBuffer call emits the attenuation advisory exactly once;
    /// a second call within the same process does NOT re-emit it (dedup via
    /// RenderingDiagnostics sentinel key "audio-in-attenuate:open").
    /// </summary>
    [Fact]
    public void MicBuffer_Open_EmitsAttenuationAdvisoryOnce()
    {
        InputFunctions.CaptureOverride = ConstantSeam(0.5f);

        InputFunctions.MicBufferForTesting(durationSeconds: 0.1);
        InputFunctions.MicBufferForTesting(durationSeconds: 0.1);

        string captured = _errCapture.ToString();
        int firstIdx = captured.IndexOf("[audio-in] mic stream attenuated -20 dB on open to prevent feedback", StringComparison.Ordinal);
        int secondIdx = captured.IndexOf("[audio-in] mic stream attenuated -20 dB on open to prevent feedback", firstIdx + 1, StringComparison.Ordinal);

        Assert.NotEqual(-1, firstIdx);  // appears at least once
        Assert.Equal(-1, secondIdx);    // never twice (one-shot dedup)
    }

    /// <summary>
    /// -20 dB scalar (×0.1) is applied to every sample. Seam returns flat 0.5;
    /// the returned buffer's peak must be ≈0.05 (= 0.5 × 0.1).
    /// </summary>
    [Fact]
    public void MicBuffer_AppliesMinus20dBScalar()
    {
        InputFunctions.CaptureOverride = ConstantSeam(0.5f);

        var buf = InputFunctions.MicBufferForTesting(durationSeconds: 0.1);

        Assert.NotNull(buf);
        Assert.True(buf!.Data.Length > 0);
        // Every sample = 0.5 * 0.1 = 0.05, within float epsilon
        for (int i = 0; i < buf.Data.Length; i++)
        {
            Assert.Equal(0.05f, buf.Data[i], precision: 5);
        }
    }

    /// <summary>
    /// Charitable failure path: seam returns null (capture failed) → MicBuffer
    /// returns a silent buffer of the requested duration + emits an error
    /// advisory. Composer's `live` session keeps running (Pitfall #12).
    /// </summary>
    [Fact]
    public void MicBuffer_CaptureFails_ReturnsSilentBufferAndAdvisory()
    {
        InputFunctions.CaptureOverride = (rate, channels, seconds) => null;

        var buf = InputFunctions.MicBufferForTesting(durationSeconds: 0.5);

        Assert.NotNull(buf);
        Assert.Equal(44100, buf!.SampleRate);
        Assert.True(buf.Frames > 0);
        // Silent buffer — every sample should be 0
        for (int i = 0; i < buf.Data.Length; i++)
        {
            Assert.Equal(0f, buf.Data[i]);
        }
        Assert.Contains("[audio-in] capture failed", _errCapture.ToString(), StringComparison.Ordinal);
    }
}
