using System;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio.DSP;
using Xunit;

namespace FlowLang.Tests.Integration.Phase37;

/// <summary>
/// Phase 37 DSP-01 — Hann / Gaussian / Tukey window helpers shape +
/// distinctness. Filled by Plan 37-01 Task 2 (this plan) alongside
/// <c>WindowFunctions.cs</c>.
///
/// <para>
/// Class shape mirrors Phase33SfzSmokeTests:24-44 — [Collection("FlowScripts")]
/// + IDisposable + RenderingDiagnostics.ResetForTesting() + FlowConfig.Reset()
/// in both ctor and Dispose so the shared one-shot stderr sentinel set + the
/// FlowConfig.Active singleton don't leak across parallel test workers.
/// </para>
/// </summary>
[Collection("FlowScripts")]
public class WindowFunctionTests : IDisposable
{
    public WindowFunctionTests()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    public void Dispose()
    {
        RenderingDiagnostics.ResetForTesting();
        FlowConfig.Reset();
    }

    /// <summary>
    /// Hann / Gaussian / Tukey at length 1024 each have envelope endpoints
    /// near zero (&lt; 0.05) and a center sample near unity (&gt; 0.9).
    /// Closed-form contract per 37-RESEARCH.md §Pattern 4.
    /// </summary>
    [Fact]
    public void Windows_HaveExpectedShape_HannGaussianTukey()
    {
        const int length = 1024;
        float[] hann = WindowFunctions.Hann(length);
        float[] gauss = WindowFunctions.Gaussian(length);
        float[] tukey = WindowFunctions.Tukey(length);

        Assert.Equal(length, hann.Length);
        Assert.Equal(length, gauss.Length);
        Assert.Equal(length, tukey.Length);

        // Endpoints near zero — verifies the windowing envelope tapers in/out.
        Assert.True(hann[0] < 0.05f, $"Hann[0]={hann[0]}");
        Assert.True(hann[length - 1] < 0.05f, $"Hann[last]={hann[length - 1]}");
        Assert.True(gauss[0] < 0.05f, $"Gauss[0]={gauss[0]}");
        Assert.True(gauss[length - 1] < 0.05f, $"Gauss[last]={gauss[length - 1]}");
        // Tukey endpoints with α=0.5: the first 25% is Hann roll-on, so [0]=0.
        Assert.True(tukey[0] < 0.05f, $"Tukey[0]={tukey[0]}");
        Assert.True(tukey[length - 1] < 0.05f, $"Tukey[last]={tukey[length - 1]}");

        // Center sample near unity — verifies the peak lands mid-window.
        Assert.True(hann[length / 2] > 0.9f, $"Hann[mid]={hann[length / 2]}");
        Assert.True(gauss[length / 2] > 0.9f, $"Gauss[mid]={gauss[length / 2]}");
        Assert.True(tukey[length / 2] > 0.9f, $"Tukey[mid]={tukey[length / 2]}");
    }

    /// <summary>
    /// Three windowing options produce DIFFERENT output at the same length —
    /// SOMEWHERE in the curve each pair differs by ≥ 0.05 (Hann's sin² shape,
    /// Gaussian's exp(-x²) shape, and Tukey's flat-top-with-Hann-edges shape
    /// are mathematically distinct closed forms). Required by DSP-01
    /// must_haves truth: "Hann (default), Gaussian, and Tukey windowing
    /// options each produce DIFFERENT output for the same input/seed."
    /// </summary>
    [Fact]
    public void Windows_ProduceDifferentOutputs_HannVsGaussianVsTukey()
    {
        const int length = 1024;
        float[] hann = WindowFunctions.Hann(length);
        float[] gauss = WindowFunctions.Gaussian(length);
        float[] tukey = WindowFunctions.Tukey(length);

        AssertPairDiffersSomewhere(hann, gauss, "Hann", "Gauss");
        AssertPairDiffersSomewhere(hann, tukey, "Hann", "Tukey");
        AssertPairDiffersSomewhere(gauss, tukey, "Gauss", "Tukey");
    }

    private static void AssertPairDiffersSomewhere(float[] a, float[] b, string aName, string bName)
    {
        float maxDiff = 0f;
        int maxIdx = -1;
        for (int i = 0; i < a.Length; i++)
        {
            float d = Math.Abs(a[i] - b[i]);
            if (d > maxDiff) { maxDiff = d; maxIdx = i; }
        }
        Assert.True(maxDiff >= 0.05f,
            $"{aName} and {bName} differ by at most {maxDiff} (at index {maxIdx}); expected ≥ 0.05 somewhere — windows are not distinct");
    }
}
