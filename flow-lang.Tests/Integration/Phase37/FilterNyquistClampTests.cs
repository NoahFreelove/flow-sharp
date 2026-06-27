using System;
using FlowLang.Diagnostics;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.DSP;
using Xunit;

namespace FlowLang.Tests.Integration.Phase37;

/// <summary>
/// Sweep fix 0614 (gap-dsp): lowpass / highpass / bandpass used to THROW an
/// ArgumentException when the cutoff hit (or exceeded) the Nyquist frequency
/// (sampleRate / 2), and lowpass/highpass threw on a non-positive cutoff. A
/// composer passing <c>(lowpass buf 22050.0)</c> at 44.1 kHz got a session-
/// killing "Unexpected error" + exit code 1 — a violation of the CLAUDE.md
/// charitable-interpretation policy. After the fix the cutoff is clamped into
/// the realisable range (20 Hz floor, Nyquist - 1 ceiling) with a one-shot
/// WarnOnce advisory, never an exception.
/// </summary>
[Collection("FlowScripts")]
public class FilterNyquistClampTests : IDisposable
{
    private const int SampleRate = 44100;

    public FilterNyquistClampTests() => RenderingDiagnostics.ResetForTesting();
    public void Dispose() => RenderingDiagnostics.ResetForTesting();

    private static AudioBuffer Sine(double hz = 440.0, double seconds = 0.25, double amp = 0.5)
    {
        int frames = (int)(seconds * SampleRate);
        var b = new AudioBuffer(frames, 1, SampleRate);
        for (int n = 0; n < frames; n++)
            b.Data[n] = (float)(amp * Math.Sin(2.0 * Math.PI * hz * n / SampleRate));
        return b;
    }

    [Fact]
    public void Lowpass_AtNyquist_ClampsInsteadOfThrowing_AndWarnsOnce()
    {
        var input = Sine();
        float nyquist = SampleRate / 2f; // 22050 Hz

        var result = Filter.Lowpass(input, nyquist); // exactly Nyquist — used to throw
        Assert.Equal(input.Frames, result.Frames);

        Assert.True(RenderingDiagnostics.WasWarnedForTesting("filter:cutoff_nyquist_clamp"),
            "lowpass at Nyquist must emit the clamp advisory once");
    }

    [Fact]
    public void Lowpass_AboveNyquist_DoesNotThrow()
    {
        var input = Sine();
        // 22000 Hz is below Nyquist (no clamp); 30000 Hz is above (must clamp, not throw).
        var below = Filter.Lowpass(input, 22000f);
        var above = Filter.Lowpass(input, 30000f);
        Assert.Equal(input.Frames, below.Frames);
        Assert.Equal(input.Frames, above.Frames);
        Assert.True(RenderingDiagnostics.WasWarnedForTesting("filter:cutoff_nyquist_clamp"));
    }

    [Fact]
    public void Highpass_AtNyquist_ClampsInsteadOfThrowing()
    {
        var input = Sine();
        var result = Filter.Highpass(input, SampleRate / 2f);
        Assert.Equal(input.Frames, result.Frames);
        Assert.True(RenderingDiagnostics.WasWarnedForTesting("filter:cutoff_nyquist_clamp"));
    }

    [Fact]
    public void Lowpass_NonPositiveCutoff_ClampsTo20Hz_AndWarns()
    {
        var input = Sine();
        var result = Filter.Lowpass(input, 0f);
        Assert.Equal(input.Frames, result.Frames);
        Assert.True(RenderingDiagnostics.WasWarnedForTesting("filter:cutoff_low_clamp"),
            "non-positive cutoff must emit the low-clamp advisory once");
    }

    [Fact]
    public void Bandpass_HighAtNyquist_ClampsInsteadOfThrowing()
    {
        var input = Sine();
        float nyquist = SampleRate / 2f;
        var result = Filter.Bandpass(input, 1000f, nyquist); // used to throw
        Assert.Equal(input.Frames, result.Frames);
        Assert.True(RenderingDiagnostics.WasWarnedForTesting("bandpass:high_nyquist_clamp"));
    }

    [Fact]
    public void Bandpass_LowNonPositive_ClampsTo20Hz()
    {
        var input = Sine();
        var result = Filter.Bandpass(input, 0f, 4000f);
        Assert.Equal(input.Frames, result.Frames);
        Assert.True(RenderingDiagnostics.WasWarnedForTesting("bandpass:low_clamp"));
    }

    [Fact]
    public void Bandpass_InvertedBand_WidensInsteadOfThrowing()
    {
        var input = Sine();
        // highHz <= lowHz used to throw; now it widens to a minimal band.
        var result = Filter.Bandpass(input, 2000f, 1000f);
        Assert.Equal(input.Frames, result.Frames);
        Assert.True(RenderingDiagnostics.WasWarnedForTesting("bandpass:band_inverted"));
    }

    [Fact]
    public void Lowpass_NormalCutoff_NoAdvisory()
    {
        var input = Sine();
        var result = Filter.Lowpass(input, 8000f);
        Assert.Equal(input.Frames, result.Frames);
        Assert.False(RenderingDiagnostics.WasWarnedForTesting("filter:cutoff_nyquist_clamp"));
        Assert.False(RenderingDiagnostics.WasWarnedForTesting("filter:cutoff_low_clamp"));
    }

    /// <summary>
    /// End-to-end: the composer-facing repro from the finding — a script that
    /// applies a Nyquist-boundary lowpass must run clean (no error, exit 0
    /// behavior), proving the charitable clamp reaches the builtin layer.
    /// </summary>
    [Fact]
    public void Lowpass_NyquistCutoff_ViaFlowScript_RunsClean()
    {
        using var runner = new FlowLang.Tests.Fixtures.FlowEngineRunner();
        var (ok, _, err, errs) = runner.RunSource(
            "use \"@audio\"\n" +
            "Buffer src = (createSineTone 440Hz 0.2 0.5)\n" +
            "Buffer lp = (lowpass src 22050.0)\n" +
            "(print \"done\")");
        Assert.True(ok, $"Nyquist lowpass script must not error: {err}");
        Assert.Equal(0, errs);
    }
}
