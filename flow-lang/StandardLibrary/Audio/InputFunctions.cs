using FlowLang.Audio;
using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Audio;

/// <summary>
/// Phase 38 Plan 38-05 AUDIO-IN-01/02 — composer-facing <c>(micBuffer duration)</c>
/// builtin that reads from the default PulseAudio input device, applies the locked
/// -20 dB feedback-guard attenuation on open (Pitfall #24), linear-interpolation
/// resamples to 44.1 kHz if the native rate differs (RESEARCH §J), and returns a
/// composable <see cref="AudioBuffer"/>.
///
/// <para>
/// Capture is opt-in by API — composer explicitly writes <c>(micBuffer 4s)</c> to
/// open the stream. There is no implicit/background capture and no remote network
/// surface (T-38-24 information-disclosure mitigation per the Plan 38-05 threat
/// model).
/// </para>
///
/// <para>
/// Two overloads register the same builtin against both <see cref="SecondType"/>
/// (composer writes <c>(micBuffer 4s)</c>) and <see cref="DoubleType"/> (composer
/// writes <c>(micBuffer 4.0)</c>) per CLAUDE.md "Music Types Quick Reference" —
/// <c>SecondType.IsCompatibleWith(DoubleType)</c> is true but the registry's
/// strict <c>TypesEqual</c> matcher needs both overloads materially registered
/// (PATTERNS Pattern S5).
/// </para>
///
/// <para>
/// Test seam (RESEARCH §I line 1003): <see cref="CaptureOverride"/> and
/// <see cref="NativeRateForTesting"/> are static-mutable hooks intended for the
/// xUnit Facts under <c>flow-lang.Tests/Integration/Phase38/</c>. Default
/// values (null + 44 100) mean "use the real PulseAudio pipeline" — composer
/// code never sees them. Setting <c>CaptureOverride</c> short-circuits the
/// <see cref="PulseAudioCaptureBackend"/> instantiation so CI can exercise the
/// full attenuation + resample logic without a live PulseAudio daemon.
/// </para>
/// </summary>
public static class InputFunctions
{
    /// <summary>
    /// Test-only seam: when non-null, MicBuffer delegates capture to this
    /// callback instead of instantiating a real <see cref="PulseAudioCaptureBackend"/>.
    /// Signature: <c>(nativeRate, channels, durationSeconds) -&gt; samples</c>;
    /// returning <c>null</c> simulates a capture failure (charitable silent-buffer
    /// fallback). Always restore to null in test Dispose/Reset code.
    /// </summary>
    public static Func<int, int, double, float[]?>? CaptureOverride { get; set; }

    /// <summary>
    /// Test-only seam: simulates a non-44.1kHz native device rate so the
    /// resample path can be unit-tested. Default 44 100 = identity passthrough.
    /// </summary>
    public static int NativeRateForTesting { get; set; } = 44_100;

    /// <summary>Target sample rate for all returned buffers — Flow's canonical 44.1 kHz.</summary>
    private const int TargetSampleRate = 44_100;

    /// <summary>Default channel count for capture — stereo per the playback sibling default.</summary>
    private const int DefaultChannels = 2;

    /// <summary>-20 dB attenuation scalar = 10^(-20/20) = 0.1 (Pitfall #24 feedback guard).</summary>
    private const float AttenuationScalar = 0.1f;

    /// <summary>
    /// Registers <c>(micBuffer Second)</c> + <c>(micBuffer Double)</c> overloads
    /// with the internal function registry. Called from
    /// <see cref="BuiltInFunctions.RegisterAllImplementations(InternalFunctionRegistry)"/>
    /// adjacent to <see cref="VisualizationFunctions.Register"/> per
    /// 38-PATTERNS Pattern S4.
    /// </summary>
    public static void Register(InternalFunctionRegistry registry)
    {
        var secondSig = new FunctionSignature("micBuffer", [SecondType.Instance],
            ParameterNames: ["duration"]);
        registry.Register("micBuffer", secondSig, MicBufferFromSecond);

        var doubleSig = new FunctionSignature("micBuffer", [DoubleType.Instance],
            ParameterNames: ["duration"]);
        registry.Register("micBuffer", doubleSig, MicBufferFromDouble);
    }

    private static Value MicBufferFromSecond(IReadOnlyList<Value> args)
    {
        double seconds = args[0].As<double>();
        var buf = MicBuffer(seconds);
        return Value.Buffer(buf);
    }

    private static Value MicBufferFromDouble(IReadOnlyList<Value> args)
    {
        double seconds = args[0].As<double>();
        var buf = MicBuffer(seconds);
        return Value.Buffer(buf);
    }

    /// <summary>
    /// Test-only entry point that returns the raw <see cref="AudioBuffer"/>
    /// without wrapping in a <see cref="Value"/>. Lets the xUnit Facts assert
    /// the buffer's sample data directly without a Flow-runtime round-trip.
    /// </summary>
    public static AudioBuffer? MicBufferForTesting(double durationSeconds)
        => MicBuffer(durationSeconds);

    /// <summary>
    /// Core implementation. Emits the feedback-guard advisory on every call
    /// (one-shot per process via <see cref="RenderingDiagnostics.WarnOnce"/>),
    /// captures samples (via real backend or test seam), resamples to 44.1 kHz
    /// if needed, applies the -20 dB scalar, returns a wrapped
    /// <see cref="AudioBuffer"/>. Charitable failure path: silent buffer +
    /// error advisory (D-v1.5-05 + Pitfall #12 "live session never dies mid-set").
    /// </summary>
    private static AudioBuffer? MicBuffer(double durationSeconds)
    {
        // (1) Always emit the attenuation advisory — composer must know feedback
        //     guard is engaged (UI-SPEC line 335).
        RenderingDiagnostics.WarnOnce(
            "audio-in-attenuate:open",
            "[audio-in] mic stream attenuated -20 dB on open to prevent feedback");

        if (durationSeconds <= 0.0)
        {
            // Zero-duration capture is a composer no-op (e.g. (micBuffer 0s)).
            return new AudioBuffer(0, DefaultChannels, TargetSampleRate);
        }

        int channels = DefaultChannels;
        int nativeRate = NativeRateForTesting;
        float[]? rawSamples;

        // (2) Capture via test seam OR real PulseAudio backend.
        if (CaptureOverride is not null)
        {
            rawSamples = CaptureOverride(nativeRate, channels, durationSeconds);
        }
        else
        {
            rawSamples = CaptureFromRealBackend(nativeRate, channels, durationSeconds, out nativeRate);
        }

        // (3) Charitable fallback: capture failure → silent buffer + advisory.
        //     Composer's `live` session continues to play (Pitfall #12).
        if (rawSamples is null)
        {
            Console.Error.WriteLine($"[audio-in] capture failed at duration {durationSeconds}s — returning silent buffer");
            int silentFrames = (int)(durationSeconds * TargetSampleRate);
            return new AudioBuffer(silentFrames, channels, TargetSampleRate);
        }

        // (4) Resample to 44.1 kHz if native rate differs (UI-SPEC line 336).
        float[] resampled;
        if (nativeRate != TargetSampleRate)
        {
            RenderingDiagnostics.WarnOnce(
                $"audio-in-resample:{nativeRate}",
                $"[audio-in] resampling capture stream from {nativeRate} Hz to {TargetSampleRate} Hz (linear interpolation)");
            resampled = ResampleLinear(rawSamples, nativeRate, TargetSampleRate, channels);
        }
        else
        {
            resampled = rawSamples;
        }

        // (5) Apply -20 dB attenuation scalar to every sample.
        for (int i = 0; i < resampled.Length; i++)
        {
            resampled[i] *= AttenuationScalar;
        }

        // (6) Wrap into AudioBuffer and return.
        int frameCount = resampled.Length / channels;
        var buf = new AudioBuffer(frameCount, channels, TargetSampleRate);
        Array.Copy(resampled, buf.Data, resampled.Length);
        return buf;
    }

    /// <summary>
    /// Real-PulseAudio capture path. Instantiates <see cref="PulseAudioCaptureBackend"/>,
    /// calls Initialize + CaptureSamples, surfaces the device's native rate back
    /// to the caller via the out-parameter, disposes the backend.
    /// </summary>
    private static float[]? CaptureFromRealBackend(int requestedRate, int channels, double durationSeconds, out int nativeRate)
    {
        nativeRate = requestedRate;
        var backend = new PulseAudioCaptureBackend(requestedRate, channels);
        try
        {
            if (!backend.Initialize(out var initError))
            {
                if (initError is not null)
                    Console.Error.WriteLine($"[audio-in] {initError}");
                return null;
            }
            nativeRate = backend.SampleRate;
            int totalFrames = (int)(durationSeconds * nativeRate);
            var samples = backend.CaptureSamples(totalFrames, out var captureError);
            if (samples is null && captureError is not null)
                Console.Error.WriteLine($"[audio-in] {captureError}");
            return samples;
        }
        finally
        {
            backend.Dispose();
        }
    }

    /// <summary>
    /// Per-channel linear-interpolation resampler. Per RESEARCH §J lines
    /// 1041-1066 — output frame count = ceil(inputFrames / ratio), where
    /// ratio = inputRate / outputRate; per output index, interpolate between
    /// the two nearest input samples. Identity fast-path when rates match
    /// (preserves byte-identical determinism for the 44.1 kHz native path).
    /// </summary>
    public static float[] ResampleLinear(float[] input, int inputRate, int outputRate, int channels)
    {
        if (inputRate == outputRate) return input;
        if (input.Length == 0) return input;

        double ratio = (double)inputRate / outputRate;
        int inputFrames = input.Length / channels;
        int outputFrames = (int)Math.Ceiling(inputFrames / ratio);
        var output = new float[outputFrames * channels];

        for (int outFrame = 0; outFrame < outputFrames; outFrame++)
        {
            double inFracIdx = outFrame * ratio;
            int inIdxLo = (int)Math.Floor(inFracIdx);
            int inIdxHi = Math.Min(inIdxLo + 1, inputFrames - 1);
            float t = (float)(inFracIdx - inIdxLo);

            for (int ch = 0; ch < channels; ch++)
            {
                float lo = input[inIdxLo * channels + ch];
                float hi = input[inIdxHi * channels + ch];
                output[outFrame * channels + ch] = lo + (hi - lo) * t;
            }
        }
        return output;
    }
}
