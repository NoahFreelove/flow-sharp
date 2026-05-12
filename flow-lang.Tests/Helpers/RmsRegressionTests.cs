using System;
using System.IO;
using FlowLang.StandardLibrary.Audio;
using Xunit;

namespace FlowLang.Tests.Helpers;

/// <summary>
/// Phase 28 SPEC-8: RMS-windowed regression assertion. Compares a rendered
/// <see cref="AudioBuffer"/> against a committed baseline WAV file. Asserts:
///   1. Frame count, channel count, and sample rate exactly match the baseline
///      (Phase 28 runtime is deterministic; any drift is a real regression).
///   2. Per-window RMS in dB matches within <paramref name="toleranceDb"/>.
///
/// Default tolerance ±0.5 dB / 100 ms windows is locked by SPEC-8. Per-test
/// override allowed via <paramref name="toleranceDb"/> — when overriding, the
/// caller MUST supply <paramref name="overrideReason"/> documenting why the
/// test legitimately exceeds the default band.
///
/// Diagnostic format follows SPEC-8: on failure, the assertion message is
/// <c>"RMS deviation in window N (XXXms-YYYms): expected -A dB, got -B dB"</c>.
/// </summary>
public static class RmsRegressionTests
{
    public const double DefaultToleranceDb = 0.5;
    public const double DefaultWindowMs = 100.0;

    /// <summary>
    /// AudioBuffer overload — renders a fresh AudioBuffer (typically a
    /// from-scratch synthesizer call) against the committed baseline. Round-trips
    /// the rendered buffer through <see cref="FileIO.WriteWav"/> + WavReader so
    /// both compared buffers carry the same TPDF dither + int16 quantization.
    /// Without the round-trip, silent regions in the fresh render compare as
    /// -120 dB but the baseline reads back as ~-91 dB (quiet dither noise) →
    /// spurious diagnostic. Use this overload when the rendered AudioBuffer
    /// has NOT yet been through FileIO.WriteWav.
    /// </summary>
    public static void AssertRmsWithinTolerance(
        AudioBuffer rendered,
        string baselineWavPath,
        double windowMs = DefaultWindowMs,
        double toleranceDb = DefaultToleranceDb,
        string? overrideReason = null)
    {
        ValidateOverride(toleranceDb, overrideReason);
        string tempPath = Path.Combine(Path.GetTempPath(),
            $"flow_rms_compare_{Guid.NewGuid():N}.wav");
        try
        {
            var args = new System.Collections.Generic.List<FlowLang.Runtime.Value>
            {
                FlowLang.Runtime.Value.String(tempPath),
                FlowLang.Runtime.Value.Buffer(rendered),
            };
            FileIO.WriteWav(args);
            AssertWavMatchesBaseline(tempPath, baselineWavPath, windowMs, toleranceDb, overrideReason);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    /// <summary>
    /// File-path overload — both arguments are paths to already-dithered WAV
    /// files. Use this when the caller's pipeline (e.g. a Flow script's own
    /// <c>writeWav</c>) has already written the rendered audio to disk; that
    /// file went through dither once and so did the baseline, so a single
    /// read+compare is correct (no double-dither).
    /// </summary>
    public static void AssertWavMatchesBaseline(
        string renderedWavPath,
        string baselineWavPath,
        double windowMs = DefaultWindowMs,
        double toleranceDb = DefaultToleranceDb,
        string? overrideReason = null)
    {
        ValidateOverride(toleranceDb, overrideReason);
        Assert.True(File.Exists(renderedWavPath), $"Rendered WAV missing: {renderedWavPath}");
        Assert.True(File.Exists(baselineWavPath), $"Baseline WAV missing: {baselineWavPath}");

        var rendered = WavReader.ReadWav(renderedWavPath);
        var baseline = WavReader.ReadWav(baselineWavPath);

        Assert.Equal(baseline.SampleRate, rendered.SampleRate);
        Assert.Equal(baseline.Channels, rendered.Channels);
        Assert.Equal(baseline.Frames, rendered.Frames);

        int windowSamples = (int)(rendered.SampleRate * windowMs / 1000.0);
        if (windowSamples < 1) windowSamples = 1;
        int totalWindows = (int)Math.Ceiling((double)rendered.Frames / windowSamples);

        for (int win = 0; win < totalWindows; win++)
        {
            int start = win * windowSamples;
            int end = Math.Min(start + windowSamples, rendered.Frames);
            double rmsRendered = ComputeRms(rendered, start, end);
            double rmsBaseline = ComputeRms(baseline, start, end);

            double dbRendered = ToDb(rmsRendered);
            double dbBaseline = ToDb(rmsBaseline);
            double dbDelta = Math.Abs(dbRendered - dbBaseline);

            if (dbDelta > toleranceDb)
            {
                int startMs = (int)(start * 1000.0 / rendered.SampleRate);
                int endMs = (int)(end * 1000.0 / rendered.SampleRate);
                Assert.Fail(
                    $"RMS deviation in window {win} ({startMs}ms-{endMs}ms): " +
                    $"expected {dbBaseline:F2} dB, got {dbRendered:F2} dB " +
                    $"(delta {dbDelta:F2} dB exceeds tolerance {toleranceDb} dB)" +
                    (overrideReason != null ? $". Override reason: {overrideReason}" : ""));
            }
        }
    }

    private static void ValidateOverride(double toleranceDb, string? overrideReason)
    {
        if (Math.Abs(toleranceDb - DefaultToleranceDb) > 1e-9 && string.IsNullOrEmpty(overrideReason))
            throw new ArgumentException(
                $"Non-default tolerance ({toleranceDb} dB) requires overrideReason explaining why " +
                "this test legitimately exceeds the SPEC-8 locked ±0.5 dB / 100ms band.");
    }

    private static double ComputeRms(AudioBuffer buf, int startFrame, int endFrame)
    {
        double sumSquares = 0.0;
        int count = 0;
        for (int i = startFrame; i < endFrame; i++)
        {
            for (int ch = 0; ch < buf.Channels; ch++)
            {
                double s = buf.GetSample(i, ch);
                sumSquares += s * s;
                count++;
            }
        }
        return count == 0 ? 0.0 : Math.Sqrt(sumSquares / count);
    }

    private static double ToDb(double rms)
    {
        // Avoid log(0) — clamp at -120 dB for silence (matches typical DAW noise floors).
        return rms < 1e-6 ? -120.0 : 20.0 * Math.Log10(rms);
    }
}
