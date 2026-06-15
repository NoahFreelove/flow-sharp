using FlowLang.Core;
using FlowLang.Runtime;

namespace FlowLang.StandardLibrary.Audio.DSP;

/// <summary>
/// Phase 37 Plan 37-01 Task 3 — granular synthesis engine (DSP-01).
/// Buffer-in / Buffer-out — grain scheduler pulls grain-sized chunks at
/// jittered offsets, applies window, overlap-adds at <c>densityHz</c>.
/// Per 37-RESEARCH.md §Pattern 4 + 37-PATTERNS.md §GranularEngine.cs
/// (Reverb.Apply skeleton + PrngRegistry draw pattern).
///
/// <para>
/// Granular is TEXTURE (cloud, grain) — NOT time-stretch. Output length =
/// input length. Time-stretch is Plan 37-02's <c>stretch</c> builtin.
/// </para>
///
/// <para>
/// PRNG routing per D-v1.5-06: jitter draws go through
/// <see cref="PrngRegistry.NextDouble"/> keyed by
/// <c>(callSite, "granular_offset" | "granular_timing")</c>. The two
/// generator names are DISTINCT per Pitfall 8 of 37-RESEARCH.md — sharing
/// a single key would cause source-offset and emit-time draws to alias.
/// Reseeded at <c>renderSong</c>/<c>writeWav</c> boundary by the engine's
/// <see cref="PrngRegistry.ResetAtRenderBoundary"/> call → two-run
/// cmp-clean determinism preserved.
/// </para>
///
/// <para>
/// Cost model (per CLAUDE.md update planned by Plan 37-01 Task 3 follow-up):
/// CPU cost ≈ density × grain × sampleRate × output-duration. Composer is
/// trusted not to set pathological values; <c>density=1000Hz</c> ×
/// <c>grain=100ms</c> = 100× overlap will render slowly but won't crash.
/// </para>
/// </summary>
public static class GranularEngine
{
    /// <summary>
    /// Apply granular synthesis to <paramref name="input"/>, returning a fresh
    /// AudioBuffer of the same length / channels / sample rate.
    /// </summary>
    /// <param name="input">Source buffer — sampled from at jittered offsets.</param>
    /// <param name="grainSeconds">Length of each grain. Must be positive.</param>
    /// <param name="densityHz">Grains per second. Must be positive.</param>
    /// <param name="jitter">Stochastic spread on source offset + emit time.
    /// 0.0 = deterministic (grains land exactly on the density grid, identical
    /// source offset each pass); larger values widen the cloud.</param>
    /// <param name="window">Window envelope applied per grain.</param>
    /// <param name="prng">Per-engine PRNG registry — keyed by (callSite, name)
    /// for two-run cmp-clean determinism per D-v1.5-06.</param>
    /// <param name="site">Source location of the composer's <c>(granular ...)</c>
    /// call — used as the PRNG key together with the generator name.</param>
    /// <exception cref="ArgumentException">If any parameter is out of range.</exception>
    public static AudioBuffer Apply(
        AudioBuffer input,
        double grainSeconds,
        double densityHz,
        double jitter,
        WindowKind window,
        PrngRegistry prng,
        SourceLocation site)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(prng);
        if (grainSeconds <= 0.0)
            throw new ArgumentException(
                $"granular: grain must be positive (seconds); got {grainSeconds}.");
        if (densityHz <= 0.0)
            throw new ArgumentException(
                $"granular: density must be positive (Hz); got {densityHz}.");
        if (jitter < 0.0)
            throw new ArgumentException(
                $"granular: jitter must be non-negative; got {jitter}.");
        if (input.Frames <= 0)
            throw new ArgumentException(
                $"granular: input buffer must contain at least one frame; got {input.Frames}.");

        int channels = input.Channels;
        int sampleRate = input.SampleRate;
        int frames = input.Frames;

        // Clamp the grain to the buffer length. A grain longer than the source
        // would put the window's unity peak (at grainSamples/2) far PAST the
        // buffer end, so OverlapAddGrain — which reads from the grain start and
        // breaks at the buffer boundary — would only ever apply the near-zero
        // LEADING edge of the window, collapsing the output to near-silence.
        // Clamping to `frames` keeps the window peak inside the available
        // material and preserves energy. Charitable per house style: emit a
        // one-shot advisory rather than throwing or silently mangling audio.
        int rawGrainSamples = Math.Max(1, (int)(grainSeconds * sampleRate));
        int grainSamples = Math.Min(rawGrainSamples, frames);
        if (rawGrainSamples > frames)
        {
            FlowLang.Diagnostics.RenderingDiagnostics.WarnOnce(
                $"granular:grain-clamp:{site.Line}:{site.Column}",
                $"[granular] grain longer than buffer — clamped to buffer length ({frames} frames) at line {site.Line}.");
        }
        int grainPeriodSamples = Math.Max(1, (int)(sampleRate / densityHz));

        // Pre-compute the window curve once per call — the same envelope shape
        // applies to every grain.
        float[] windowCurve = window switch
        {
            WindowKind.Hann => WindowFunctions.Hann(grainSamples),
            WindowKind.Gaussian => WindowFunctions.Gaussian(grainSamples),
            WindowKind.Tukey => WindowFunctions.Tukey(grainSamples),
            _ => WindowFunctions.Hann(grainSamples), // defensive default
        };

        var result = new AudioBuffer(frames, channels, sampleRate);

        // Schedule grain emit times across the buffer. For each scheduled time
        // t = 0, period, 2·period, ... draw two jitter values:
        //   offsetDraw → where in the source buffer this grain reads from
        //   timeDraw   → where in the output buffer this grain emits to
        // Both draws span [-1, +1] (rescaled from PrngRegistry's [0, 1) draw).
        for (int t = 0; t < frames; t += grainPeriodSamples)
        {
            double offsetDraw = prng.NextDouble(site, "granular_offset") * 2.0 - 1.0;
            double timeDraw = prng.NextDouble(site, "granular_timing") * 2.0 - 1.0;

            int sourceFrame = ClampInt(
                t + (int)(offsetDraw * jitter * grainSamples),
                0, frames - 1);
            int emitFrame = ClampInt(
                t + (int)(timeDraw * jitter * grainPeriodSamples),
                0, frames - 1);

            OverlapAddGrain(result, input, sourceFrame, emitFrame,
                grainSamples, windowCurve);
        }

        return result;
    }

    /// <summary>
    /// Defensive clamp — keeps source/emit frame indices inside the buffer.
    /// </summary>
    private static int ClampInt(int v, int lo, int hi)
    {
        if (v < lo) return lo;
        if (v > hi) return hi;
        return v;
    }

    /// <summary>
    /// Reads one windowed grain from <paramref name="source"/> starting at
    /// <paramref name="sourceFrame"/> and adds it into <paramref name="target"/>
    /// starting at <paramref name="emitFrame"/>. Both source-end and emit-end
    /// indices clamp against the target's frame count. Per-channel; matches
    /// <see cref="Reverb"/>'s helper-method extraction pattern.
    /// </summary>
    private static void OverlapAddGrain(
        AudioBuffer target,
        AudioBuffer source,
        int sourceFrame,
        int emitFrame,
        int grainSamples,
        float[] windowCurve)
    {
        int channels = target.Channels;
        int sourceFrames = source.Frames;
        int targetFrames = target.Frames;

        for (int k = 0; k < grainSamples; k++)
        {
            int srcIdx = sourceFrame + k;
            int dstIdx = emitFrame + k;
            if (srcIdx >= sourceFrames || dstIdx >= targetFrames) break;

            float envelope = windowCurve[k];
            for (int ch = 0; ch < channels; ch++)
            {
                target.Data[dstIdx * channels + ch] +=
                    source.Data[srcIdx * channels + ch] * envelope;
            }
        }
    }
}

/// <summary>
/// Phase 37 DSP-01 — windowing kind for granular grain envelope.
/// Maps the composer's <c>windowing=#hann | #gaussian | #tukey</c>
/// Symbol arg to <see cref="WindowFunctions"/>.
/// </summary>
public enum WindowKind
{
    /// <summary>Hann (default) — smooth roll-off, minimal spectral leakage.</summary>
    Hann,
    /// <summary>Gaussian σ=0.4 — softer onset than Hann.</summary>
    Gaussian,
    /// <summary>Tukey α=0.5 — flat top + Hann roll-off at edges.</summary>
    Tukey,
}
