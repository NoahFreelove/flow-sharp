using FlowLang.Core;
using FlowLang.StandardLibrary.Audio;

namespace FlowLang.StandardLibrary.Audio.DSP;

/// <summary>
/// Phase 37 Plan 37-02 Task 2 — pitch-shift via stretch + resample inverse
/// remap. Threads the full W4 LOCK knob bag through to <see cref="StretchEngine"/>.
///
/// <para>
/// Algorithm per RESEARCH §Pattern 1:
/// <list type="number">
///   <item><description>Compute pitch-shift ratio <c>r = 2^(cents/1200)</c>
///   (cent-precision semitone formula).</description></item>
///   <item><description>Time-stretch by <c>r</c> via
///   <see cref="StretchEngine"/> — longer for upshift, shorter for
///   downshift.</description></item>
///   <item><description>Resample by <c>r</c> back to the original length via
///   linear interpolation — frequency content shifts by <c>r</c> after the
///   resample.</description></item>
/// </list>
/// </para>
///
/// <para>
/// Both operations use <c>r</c> (NOT <c>1/r</c>): an upshift stretches the
/// buffer LONGER, then the resample reads <c>outFrame × r</c> over the
/// original frame count, staying in-bounds and yielding correct pitch at
/// ~unity level. A prior revision stretched by <c>1/r</c>, which made the
/// stretched buffer SHORTER than the resample read region for upshifts — the
/// resample then clamped to the last frame (flat DC tail) for most of the
/// output, producing near-silent / wrong-pitch upward shifts.
/// </para>
///
/// <para>
/// Pitfall 11 — <c>cents == 0</c> short-circuits to identity (input returned
/// verbatim).
/// </para>
/// </summary>
public static class PitchShiftEngine
{
    /// <summary>
    /// Pitch-shift <paramref name="input"/> by <paramref name="cents"/>
    /// while preserving duration. Positive cents raise pitch, negative
    /// lower it. cents=0 fast-paths to identity.
    /// </summary>
    public static AudioBuffer Process(
        AudioBuffer input,
        double cents,
        StretchMode mode = StretchMode.Auto,
        int frameSize = 2048,
        int hopSize = 512,
        int overlap = 4,
        double transientThreshold = 0.3,
        int? pitchPeriod = null,
        int? windowSize = null,
        SourceLocation? site = null)
    {
        ArgumentNullException.ThrowIfNull(input);

        // Pitfall 11 — identity fast-path. Preserves two-run cmp-clean.
        if (Math.Abs(cents) < 1e-9)
        {
            return input;
        }

        double ratio = Math.Pow(2.0, cents / 1200.0);

        // W4 LOCK — stretch by ratio with full knob bag forwarded.
        // Pitch-shift-via-resample identity: stretch by r (LONGER for an
        // upshift), then resample by r below. Both legs use r — stretching by
        // 1/r here would leave the stretched buffer shorter than the resample
        // read region for upshifts, clamping most of the output to a flat DC
        // tail (near-silent, wrong pitch).
        AudioBuffer stretched = StretchEngine.Process(
            input, factor: ratio, mode: mode,
            frameSize: frameSize, hopSize: hopSize, overlap: overlap,
            transientThreshold: transientThreshold,
            pitchPeriod: pitchPeriod, windowSize: windowSize,
            site: site);

        // Linear-interpolation resample by ratio back to the input length.
        // Walk each output frame and sample the stretched buffer at
        // outFrame * ratio. With factor=ratio, an upshift's stretched buffer
        // (~inFrames*ratio long) is exactly the region this loop reads
        // (outFrames*ratio = inFrames*ratio), so it stays in-bounds and the
        // pitch shifts up by ratio at ~unity level.
        int channels = input.Channels;
        int outFrames = input.Frames;
        var result = new AudioBuffer(outFrames, channels, input.SampleRate);

        for (int outF = 0; outF < outFrames; outF++)
        {
            double srcF = outF * ratio;
            int srcF0 = (int)Math.Floor(srcF);
            int srcF1 = srcF0 + 1;
            double frac = srcF - srcF0;
            if (srcF0 < 0) srcF0 = 0;
            if (srcF1 >= stretched.Frames) srcF1 = stretched.Frames - 1;
            if (srcF0 >= stretched.Frames) srcF0 = stretched.Frames - 1;

            for (int ch = 0; ch < channels; ch++)
            {
                float a = stretched.Data[srcF0 * channels + ch];
                float b = stretched.Data[srcF1 * channels + ch];
                result.Data[outF * channels + ch] = (float)(a * (1.0 - frac) + b * frac);
            }
        }
        return result;
    }
}
