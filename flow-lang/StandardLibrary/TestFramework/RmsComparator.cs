using System;
using FlowLang.StandardLibrary.Audio;

namespace FlowLang.StandardLibrary.TestFramework;

/// <summary>
/// Phase 35 Plan 35-04 TEST-01 — pure RMS-windowed comparison helper, lifted
/// from <c>flow-lang.Tests/Helpers/RmsRegressionTests.cs:71-145</c> so both
/// the runtime <c>(assertWithinDb)</c> builtin and the xUnit
/// <c>AssertRmsWithinTolerance</c> helper can share a single source of
/// truth without the runtime taking a dependency on Xunit.Assert.
///
/// <para>
/// Per SPEC-8: window size 100 ms, tolerance ±0.5 dB, RMS units 20·log₁₀
/// with a -120 dB clamp for silence (matches typical DAW noise floors).
/// The clamp prevents log(0) on quiet sections.
/// </para>
///
/// <para>
/// Phase 35 PATTERNS.md Bucket 2b §RmsComparator.cs Notable Departures:
/// canonical location is <c>flow-lang/StandardLibrary/TestFramework/</c>
/// (NOT <c>flow-lang.Tests/Helpers/</c>) so the runtime builtin has
/// visibility without forcing flow-lang to reference xUnit.
/// </para>
/// </summary>
public static class RmsComparator
{
    /// <summary>
    /// SPEC-8 locked default — tests legitimately overriding this band must
    /// document why at the call site (see RmsRegressionTests.ValidateOverride
    /// for the xUnit path's override contract).
    /// </summary>
    public const double DefaultToleranceDb = 0.5;

    /// <summary>
    /// SPEC-8 locked default window — 100 ms at 44.1 kHz produces 4410-sample
    /// windows. Chosen so the windowing catches transient differences
    /// (envelope attacks, click artifacts) without being so small that
    /// individual cycles of a 440 Hz tone leak windowed-RMS into the noise.
    /// </summary>
    public const double DefaultWindowMs = 100.0;

    /// <summary>
    /// Returns the maximum absolute dB deviation between corresponding RMS
    /// windows of <paramref name="a"/> and <paramref name="b"/>. The two
    /// buffers MUST agree on Frames, Channels, and SampleRate — mismatched
    /// metadata throws <see cref="ArgumentException"/> (callers either
    /// already validated or rely on the throw to surface the regression).
    /// </summary>
    /// <param name="a">First buffer (typically the rendered output).</param>
    /// <param name="b">Second buffer (typically the baseline).</param>
    /// <param name="windowMs">Window size in milliseconds. Defaults to
    /// SPEC-8 100 ms.</param>
    /// <returns>Maximum per-window absolute dB difference, in dB.</returns>
    public static double MaxWindowDeviationDb(
        AudioBuffer a,
        AudioBuffer b,
        double windowMs = DefaultWindowMs)
    {
        if (a is null) throw new ArgumentNullException(nameof(a));
        if (b is null) throw new ArgumentNullException(nameof(b));
        if (a.SampleRate != b.SampleRate)
            throw new ArgumentException(
                $"SampleRate mismatch: {a.SampleRate} vs {b.SampleRate}");
        if (a.Channels != b.Channels)
            throw new ArgumentException(
                $"Channel count mismatch: {a.Channels} vs {b.Channels}");
        if (a.Frames != b.Frames)
            throw new ArgumentException(
                $"Frame count mismatch: {a.Frames} vs {b.Frames}");

        int windowSamples = (int)(a.SampleRate * windowMs / 1000.0);
        if (windowSamples < 1) windowSamples = 1;
        int totalWindows = (int)Math.Ceiling((double)a.Frames / windowSamples);

        double maxDelta = 0.0;
        for (int win = 0; win < totalWindows; win++)
        {
            int start = win * windowSamples;
            int end = Math.Min(start + windowSamples, a.Frames);

            double rmsA = ComputeRms(a, start, end);
            double rmsB = ComputeRms(b, start, end);
            double dbA = ToDb(rmsA);
            double dbB = ToDb(rmsB);
            double delta = Math.Abs(dbA - dbB);
            if (delta > maxDelta) maxDelta = delta;
        }
        return maxDelta;
    }

    /// <summary>
    /// Returns the (windowIndex, startMs, endMs, dbA, dbB, delta) for the
    /// first window whose absolute dB difference exceeds <paramref name="toleranceDb"/>.
    /// Returns <c>null</c> if no window exceeds the tolerance. Used by the
    /// xUnit helper to produce SPEC-8's failure diagnostic without forcing
    /// the runtime path to format the same message.
    /// </summary>
    public static (int windowIndex, int startMs, int endMs, double dbA, double dbB, double delta)?
        FirstWindowExceedingTolerance(
            AudioBuffer a,
            AudioBuffer b,
            double toleranceDb,
            double windowMs = DefaultWindowMs)
    {
        if (a.SampleRate != b.SampleRate || a.Channels != b.Channels || a.Frames != b.Frames)
            throw new ArgumentException("Buffer metadata mismatch (sample rate / channels / frames).");

        int windowSamples = (int)(a.SampleRate * windowMs / 1000.0);
        if (windowSamples < 1) windowSamples = 1;
        int totalWindows = (int)Math.Ceiling((double)a.Frames / windowSamples);

        for (int win = 0; win < totalWindows; win++)
        {
            int start = win * windowSamples;
            int end = Math.Min(start + windowSamples, a.Frames);

            double dbA = ToDb(ComputeRms(a, start, end));
            double dbB = ToDb(ComputeRms(b, start, end));
            double delta = Math.Abs(dbA - dbB);
            if (delta > toleranceDb)
            {
                int startMs = (int)(start * 1000.0 / a.SampleRate);
                int endMs = (int)(end * 1000.0 / a.SampleRate);
                return (win, startMs, endMs, dbA, dbB, delta);
            }
        }
        return null;
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
        // Clamp at -120 dB to avoid log(0) on silent windows. Matches typical
        // DAW noise floors so quiet-vs-silent comparisons surface as ~0 dB
        // delta, not as a -∞ vs finite-dB blowup.
        return rms < 1e-6 ? -120.0 : 20.0 * Math.Log10(rms);
    }
}
