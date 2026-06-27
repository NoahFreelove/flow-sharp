using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.DSP;
using FlowLang.Tests.Helpers;
using Xunit;

namespace FlowLang.Tests.Integration.Phase37;

/// <summary>
/// Phase 37 DSP-02 — <c>(stretch buf 2.0 mode=#auto)</c> emits stderr
/// <c>[stretch] mode=#auto picked: X% vocoder / Y% psola across N frames</c>
/// exactly once per call (D-37-06 + OQ5 sentinel keying). Filled by Plan
/// 37-02 Task 2.
/// </summary>
[Collection("FlowScripts")]
public class StretchAutoAdvisoryTests : IDisposable
{
    public StretchAutoAdvisoryTests()
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
    /// Captures stderr while <paramref name="action"/> runs; returns the
    /// captured text. Restores Console.Error on exit.
    /// </summary>
    private static string CaptureStderr(Action action)
    {
        var original = Console.Error;
        var sb = new StringBuilder();
        var writer = new StringWriter(sb);
        Console.SetError(writer);
        try
        {
            action();
        }
        finally
        {
            Console.SetError(original);
        }
        return sb.ToString();
    }

    /// <summary>
    /// On a pure 440 Hz sine, HPS should classify most frames as harmonic →
    /// advisory says mostly vocoder (≥ 60% vocoder).
    /// </summary>
    [Fact]
    public void StretchAuto_OnSine440_EmitsAdvisoryWithMostlyVocoder()
    {
        var input = FileIO.LoadWavInternal(Phase37Fixtures.FixturePath("sine_440.wav"));

        string stderr = CaptureStderr(() =>
        {
            var _ = StretchEngine.Process(input, factor: 2.0, mode: StretchMode.Auto,
                site: new SourceLocation(10, 5, "sine_test.flow"));
        });

        var match = Regex.Match(stderr,
            @"\[stretch\] mode=#auto picked: (\d+)% vocoder / (\d+)% psola across (\d+) frames");
        Assert.True(match.Success,
            $"expected advisory line in stderr; got:\n{stderr}");
        int pctVoc = int.Parse(match.Groups[1].Value);
        int pctPso = int.Parse(match.Groups[2].Value);
        int totalFrames = int.Parse(match.Groups[3].Value);
        Assert.True(pctVoc > 60,
            $"expected mostly vocoder for pure sine; got {pctVoc}% vocoder / {pctPso}% psola");
        Assert.True(totalFrames > 0);
    }

    /// <summary>
    /// On a kick transient, HPS should classify many frames as percussive →
    /// advisory says non-zero psola percentage.
    /// </summary>
    [Fact]
    public void StretchAuto_OnKick_EmitsAdvisoryWithNonZeroPsola()
    {
        var input = FileIO.LoadWavInternal(Phase37Fixtures.FixturePath("kick_hit.wav"));

        string stderr = CaptureStderr(() =>
        {
            var _ = StretchEngine.Process(input, factor: 2.0, mode: StretchMode.Auto,
                site: new SourceLocation(20, 5, "kick_test.flow"));
        });

        var match = Regex.Match(stderr,
            @"\[stretch\] mode=#auto picked: (\d+)% vocoder / (\d+)% psola across (\d+) frames");
        Assert.True(match.Success,
            $"expected advisory line in stderr; got:\n{stderr}");
        int pctPso = int.Parse(match.Groups[2].Value);
        // We don't insist on >50% psola — the kick is mostly tonal body with
        // a sharp click. We just require psola > 0 (HPS detected at least
        // one transient frame).
        Assert.True(pctPso > 0,
            $"expected non-zero psola classification on a kick; got {pctPso}% psola");
    }

    /// <summary>
    /// Two consecutive calls at the same site with identical input must emit
    /// the advisory ONCE total — proves the sentinel key dedups (OQ5 spec).
    /// </summary>
    [Fact]
    public void StretchAuto_SameInputSameSite_DedupsAdvisory()
    {
        var input = FileIO.LoadWavInternal(Phase37Fixtures.FixturePath("sine_440.wav"));
        var site = new SourceLocation(30, 5, "dedup_test.flow");

        string stderr = CaptureStderr(() =>
        {
            var _ = StretchEngine.Process(input, factor: 2.0, mode: StretchMode.Auto, site: site);
            var __ = StretchEngine.Process(input, factor: 2.0, mode: StretchMode.Auto, site: site);
        });

        var matches = Regex.Matches(stderr,
            @"\[stretch\] mode=#auto picked: \d+% vocoder / \d+% psola across \d+ frames");
        Assert.Equal(1, matches.Count);
    }
}
