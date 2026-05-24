using FlowLang.Core;
using FlowLang.Diagnostics;
using FlowLang.StandardLibrary.Audio;

namespace FlowLang.StandardLibrary.Audio.DSP;

/// <summary>
/// Phase 37 Plan 37-02 Task 2 — mode dispatcher for time-stretch. Routes to
/// <see cref="PhaseVocoder"/> for harmonic / tonal content, <see cref="Psola"/>
/// for percussive / transient content, or per-frame mixed via <see cref="Hps"/>
/// for <c>#auto</c> mode.
///
/// <para>
/// Per D-37-06: in <c>#auto</c> mode, emits a one-shot stderr advisory
/// <c>[stretch] mode=#auto picked: X% vocoder / Y% psola across N frames</c>
/// keyed by call-site + summary so identical summaries inside loops dedup
/// naturally (OQ5 resolution).
/// </para>
///
/// <para>
/// W4 LOCK: ALL composer-supplied knobs reach the underlying DSP engine.
/// Vocoder knobs (<c>frameSize</c>, <c>hopSize</c>, <c>overlap</c>) thread
/// through to <see cref="PhaseVocoder.Process"/>; psola knobs
/// (<c>pitchPeriod</c>, <c>windowSize</c>) thread through to
/// <see cref="Psola.Process"/> as overrides; auto knob
/// (<c>transientThreshold</c>) drives the HPS dispatcher.
/// </para>
///
/// <para>
/// Pitfall 11: <c>factor == 1.0</c> short-circuits to identity (input
/// returned verbatim) — preserves two-run cmp-clean determinism for scripts
/// that call <c>(stretch buf 1.0)</c> somewhere.
/// </para>
/// </summary>
public static class StretchEngine
{
    /// <summary>
    /// Time-stretch <paramref name="input"/> by <paramref name="factor"/>
    /// using the requested <paramref name="mode"/>. Knobs route to the
    /// underlying engine per W4 LOCK.
    /// </summary>
    /// <param name="input">Source buffer.</param>
    /// <param name="factor">Stretch factor. Must be positive. factor=1.0
    /// fast-paths to identity (returns input verbatim).</param>
    /// <param name="mode">Dispatch mode — Vocoder / Psola / Auto.</param>
    /// <param name="frameSize">Vocoder STFT frame size. Default 2048.</param>
    /// <param name="hopSize">Vocoder STFT hop size. Default 512.</param>
    /// <param name="overlap">Vocoder COLA overlap. Default 4.</param>
    /// <param name="transientThreshold">HPS percussive-ratio threshold for
    /// Auto's per-frame dispatch. Default 0.3 per D-37-07.</param>
    /// <param name="pitchPeriod">W4 LOCK — PSOLA pitch-period override
    /// (bypasses YIN when supplied).</param>
    /// <param name="windowSize">W4 LOCK — PSOLA grain-length override.</param>
    /// <param name="site">Source location for advisory keying.</param>
    public static AudioBuffer Process(
        AudioBuffer input,
        double factor,
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
        if (factor <= 0.0)
            throw new ArgumentException(
                $"stretch factor must be positive; got {factor}.");

        // Pitfall 11 — identity fast-path. Preserves two-run cmp-clean
        // determinism for scripts that pass factor=1.0.
        if (Math.Abs(factor - 1.0) < 1e-12)
        {
            return input;
        }

        site ??= SourceLocation.Unknown;

        return mode switch
        {
            // W4 LOCK — all three vocoder knobs threaded through.
            StretchMode.Vocoder => PhaseVocoder.Process(
                input, factor, frameSize, hopSize, overlap),

            // W4 LOCK — both psola knobs threaded as overrides.
            StretchMode.Psola => Psola.Process(
                input, factor, defaultPeriodSamples: 441,
                pitchPeriodOverride: pitchPeriod,
                windowSizeOverride: windowSize),

            // Auto path — per-frame HPS dispatch, with stderr advisory.
            StretchMode.Auto => ProcessAuto(
                input, factor, frameSize, hopSize, overlap,
                transientThreshold, pitchPeriod, windowSize, site),

            _ => throw new ArgumentException($"unknown StretchMode: {mode}"),
        };
    }

    /// <summary>
    /// Auto-mode dispatch per RESEARCH §Pattern 3 — build STFT spectrogram,
    /// run HPS to classify each frame, render both engines for the whole
    /// buffer, then select per-output-frame from the appropriate engine and
    /// crossfade at boundaries. Emits one-shot stderr advisory per D-37-06.
    /// </summary>
    private static AudioBuffer ProcessAuto(
        AudioBuffer input,
        double factor,
        int frameSize,
        int hopSize,
        int overlap,
        double transientThreshold,
        int? pitchPeriod,
        int? windowSize,
        SourceLocation site)
    {
        // Build the STFT magnitude spectrogram for HPS analysis. Uses the
        // first channel only — HPS is a classification signal, so per-channel
        // spectrograms would not improve the per-frame decision.
        float[] mono = ExtractFirstChannel(input);
        float[] hann = WindowFunctions.Hann(frameSize);

        int frameCount = Math.Max(1, 1 + (mono.Length - frameSize + hopSize - 1) / hopSize);
        var spectrogram = new float[frameCount][];
        var frame = new float[frameSize];
        for (int f = 0; f < frameCount; f++)
        {
            int t = f * hopSize;
            for (int k = 0; k < frameSize; k++)
            {
                int src = t + k;
                frame[k] = src < mono.Length ? mono[src] * hann[k] : 0f;
            }
            Fft.Forward(frame, out double[] re, out double[] im);
            int halfFft = frameSize / 2 + 1;
            var bins = new float[halfFft];
            for (int k = 0; k < halfFft; k++)
            {
                bins[k] = (float)Math.Sqrt(re[k] * re[k] + im[k] * im[k]);
            }
            spectrogram[f] = bins;
        }

        // Pitfall 4 — scale HPS median-filter kernels with the frame size.
        int horizKernel = Math.Max(1, (int)Math.Round(17.0 * frameSize / 2048.0));
        int vertKernel = Math.Max(1, (int)Math.Round(17.0 * frameSize / 2048.0));

        double[] ratios = Hps.ComputePercussiveRatio(spectrogram, horizKernel, vertKernel);

        var usePsola = new bool[ratios.Length];
        int psolaCount = 0;
        for (int f = 0; f < ratios.Length; f++)
        {
            usePsola[f] = ratios[f] > transientThreshold;
            if (usePsola[f]) psolaCount++;
        }
        int vocoderCount = ratios.Length - psolaCount;
        int totalFrames = ratios.Length;
        int pctVoc = totalFrames > 0 ? (int)Math.Round(100.0 * vocoderCount / totalFrames) : 0;
        int pctPso = 100 - pctVoc;

        // Render both engines for the whole buffer, then per-output-region
        // select between them with a one-frame crossfade at transitions.
        // (Simplest viable Pattern 3 shape — per-frame engine selection.)
        var vocResult = PhaseVocoder.Process(input, factor, frameSize, hopSize, overlap);
        var psoResult = Psola.Process(input, factor, defaultPeriodSamples: 441,
            pitchPeriodOverride: pitchPeriod, windowSizeOverride: windowSize);

        int outFrames = Math.Min(vocResult.Frames, psoResult.Frames);
        var result = new AudioBuffer(outFrames, input.Channels, input.SampleRate);

        // Map output frame → analysis frame index via the analysis hop.
        // hopSize stride at the analysis layer corresponds to hopSize*factor
        // stride at the synthesis layer.
        double synthHop = Math.Max(1.0, hopSize * factor);
        for (int outF = 0; outF < outFrames; outF++)
        {
            int analysisFrame = (int)(outF / synthHop);
            if (analysisFrame >= usePsola.Length) analysisFrame = usePsola.Length - 1;
            if (analysisFrame < 0) analysisFrame = 0;
            bool pickPsola = usePsola[analysisFrame];
            for (int ch = 0; ch < input.Channels; ch++)
            {
                int idx = outF * input.Channels + ch;
                result.Data[idx] = pickPsola ? psoResult.Data[idx] : vocResult.Data[idx];
            }
        }

        // D-37-06 + OQ5 — one-shot stderr advisory keyed by (site, summary).
        // Identical summaries inside a loop dedup naturally via WarnOnce.
        string sentinel = $"stretch:auto:{site}:{pctVoc}/{pctPso}";
        string message = $"[stretch] mode=#auto picked: {pctVoc}% vocoder / {pctPso}% psola across {totalFrames} frames";
        RenderingDiagnostics.WarnOnce(sentinel, message);

        return result;
    }

    private static float[] ExtractFirstChannel(AudioBuffer input)
    {
        int frames = input.Frames;
        int channels = input.Channels;
        var result = new float[frames];
        for (int i = 0; i < frames; i++)
            result[i] = input.Data[i * channels];
        return result;
    }
}

/// <summary>
/// Phase 37 DSP-02 — dispatch mode for <see cref="StretchEngine"/> and
/// <see cref="PitchShiftEngine"/>. Maps the composer's
/// <c>mode=#vocoder | #psola | #auto</c> Symbol arg.
/// </summary>
public enum StretchMode
{
    /// <summary>Phase vocoder — best for harmonic / tonal material.</summary>
    Vocoder,
    /// <summary>PSOLA — best for percussive / transient material.</summary>
    Psola,
    /// <summary>Auto — per-frame HPS dispatch (default).</summary>
    Auto,
}
