using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.StandardLibrary.Audio.DSP;

namespace FlowLang.StandardLibrary.Audio;

/// <summary>
/// Registers audio DSP effect functions: reverb, lowpass, highpass, bandpass,
/// compress, delay, and gain. All effects return new buffers.
/// Effects apply left-to-right in a chain: <c>tone -> lowpass 800.0 -> reverb 0.3 -> gain -3.0</c>
/// </summary>
public static class EffectsFunctions
{
    /// <summary>
    /// Registers all DSP effect built-in functions.
    /// </summary>
    public static void Register(InternalFunctionRegistry registry)
    {
        RegisterReverb(registry);
        RegisterFilters(registry);
        RegisterCompressor(registry);
        RegisterDelay(registry);
        RegisterGain(registry);
    }

    // ===== Reverb =====

    private static void RegisterReverb(InternalFunctionRegistry registry)
    {
        // reverb(Buffer, Double) -> Buffer — room size only, default damping=0.5, mix=0.3
        var reverbSimpleSig = new FunctionSignature("reverb",
            [BufferType.Instance, DoubleType.Instance]);
        registry.Register("reverb", reverbSimpleSig, ReverbSimple);

        // reverb(Buffer, Double, Double, Double) -> Buffer — room, damping, mix
        var reverbFullSig = new FunctionSignature("reverb",
            [BufferType.Instance, DoubleType.Instance, DoubleType.Instance, DoubleType.Instance]);
        registry.Register("reverb", reverbFullSig, ReverbFull);
    }

    /// <summary>
    /// reverb(Buffer, Double) — applies reverb with room size, default damping and mix.
    /// </summary>
    private static Value ReverbSimple(IReadOnlyList<Value> args)
    {
        var buffer = args[0].As<AudioBuffer>();
        float roomSize = (float)args[1].As<double>();

        if (buffer.Frames == 0)
            return Value.Buffer(new AudioBuffer(0, buffer.Channels, buffer.SampleRate));

        var result = Reverb.Apply(buffer, roomSize, damping: 0.5f, mix: 0.3f);
        return Value.Buffer(result);
    }

    /// <summary>
    /// reverb(Buffer, Double, Double, Double) — applies reverb with room, damping, and mix.
    /// </summary>
    private static Value ReverbFull(IReadOnlyList<Value> args)
    {
        var buffer = args[0].As<AudioBuffer>();
        float roomSize = (float)args[1].As<double>();
        float damping = (float)args[2].As<double>();
        float mix = (float)args[3].As<double>();

        if (buffer.Frames == 0)
            return Value.Buffer(new AudioBuffer(0, buffer.Channels, buffer.SampleRate));

        var result = Reverb.Apply(buffer, roomSize, damping, mix);
        return Value.Buffer(result);
    }

    // ===== Filters =====

    private static void RegisterFilters(InternalFunctionRegistry registry)
    {
        // lowpass(Buffer, Double) -> Buffer — cutoff Hz
        var lowpassSig = new FunctionSignature("lowpass",
            [BufferType.Instance, DoubleType.Instance]);
        registry.Register("lowpass", lowpassSig, LowpassFilter);

        // highpass(Buffer, Double) -> Buffer — cutoff Hz
        var highpassSig = new FunctionSignature("highpass",
            [BufferType.Instance, DoubleType.Instance]);
        registry.Register("highpass", highpassSig, HighpassFilter);

        // bandpass(Buffer, Double, Double) -> Buffer — low Hz, high Hz
        var bandpassSig = new FunctionSignature("bandpass",
            [BufferType.Instance, DoubleType.Instance, DoubleType.Instance]);
        registry.Register("bandpass", bandpassSig, BandpassFilter);
    }

    /// <summary>
    /// lowpass(Buffer, Double) — removes frequencies above cutoff.
    /// </summary>
    private static Value LowpassFilter(IReadOnlyList<Value> args)
    {
        var buffer = args[0].As<AudioBuffer>();
        float cutoff = (float)args[1].As<double>();

        if (buffer.Frames == 0)
            return Value.Buffer(new AudioBuffer(0, buffer.Channels, buffer.SampleRate));

        var result = Filter.Lowpass(buffer, cutoff);
        return Value.Buffer(result);
    }

    /// <summary>
    /// highpass(Buffer, Double) — removes frequencies below cutoff.
    /// </summary>
    private static Value HighpassFilter(IReadOnlyList<Value> args)
    {
        var buffer = args[0].As<AudioBuffer>();
        float cutoff = (float)args[1].As<double>();

        if (buffer.Frames == 0)
            return Value.Buffer(new AudioBuffer(0, buffer.Channels, buffer.SampleRate));

        var result = Filter.Highpass(buffer, cutoff);
        return Value.Buffer(result);
    }

    /// <summary>
    /// bandpass(Buffer, Double, Double) — keeps frequencies between low and high cutoffs.
    /// </summary>
    private static Value BandpassFilter(IReadOnlyList<Value> args)
    {
        var buffer = args[0].As<AudioBuffer>();
        float lowHz = (float)args[1].As<double>();
        float highHz = (float)args[2].As<double>();

        if (buffer.Frames == 0)
            return Value.Buffer(new AudioBuffer(0, buffer.Channels, buffer.SampleRate));

        var result = Filter.Bandpass(buffer, lowHz, highHz);
        return Value.Buffer(result);
    }

    // ===== Compressor =====

    private static void RegisterCompressor(InternalFunctionRegistry registry)
    {
        // compress(Buffer, Double, Double) -> Buffer — threshold dB, ratio
        var compressSimpleSig = new FunctionSignature("compress",
            [BufferType.Instance, DoubleType.Instance, DoubleType.Instance]);
        registry.Register("compress", compressSimpleSig, CompressSimple);

        // compress(Buffer, Double, Double, Double, Double) -> Buffer — threshold, ratio, attack ms, release ms
        var compressFullSig = new FunctionSignature("compress",
            [BufferType.Instance, DoubleType.Instance, DoubleType.Instance,
             DoubleType.Instance, DoubleType.Instance]);
        registry.Register("compress", compressFullSig, CompressFull);
    }

    /// <summary>
    /// compress(Buffer, Double, Double) — compresses with threshold and ratio, default attack/release.
    /// </summary>
    private static Value CompressSimple(IReadOnlyList<Value> args)
    {
        var buffer = args[0].As<AudioBuffer>();
        float threshold = (float)args[1].As<double>();
        float ratio = (float)args[2].As<double>();

        if (buffer.Frames == 0)
            return Value.Buffer(new AudioBuffer(0, buffer.Channels, buffer.SampleRate));

        var result = Compressor.Apply(buffer, threshold, ratio);
        return Value.Buffer(result);
    }

    /// <summary>
    /// compress(Buffer, Double, Double, Double, Double) — compresses with full control.
    /// </summary>
    private static Value CompressFull(IReadOnlyList<Value> args)
    {
        var buffer = args[0].As<AudioBuffer>();
        float threshold = (float)args[1].As<double>();
        float ratio = (float)args[2].As<double>();
        float attackMs = (float)args[3].As<double>();
        float releaseMs = (float)args[4].As<double>();

        if (buffer.Frames == 0)
            return Value.Buffer(new AudioBuffer(0, buffer.Channels, buffer.SampleRate));

        var result = Compressor.Apply(buffer, threshold, ratio, attackMs, releaseMs);
        return Value.Buffer(result);
    }

    // ===== Delay =====

    private static void RegisterDelay(InternalFunctionRegistry registry)
    {
        // delay(Buffer, Double, Double, Double) -> Buffer — time ms, feedback, mix
        var delaySig = new FunctionSignature("delay",
            [BufferType.Instance, DoubleType.Instance, DoubleType.Instance, DoubleType.Instance]);
        registry.Register("delay", delaySig, DelayEffect);
    }

    /// <summary>
    /// delay(Buffer, Double, Double, Double) — feedback delay with time, feedback, and mix.
    /// </summary>
    private static Value DelayEffect(IReadOnlyList<Value> args)
    {
        var buffer = args[0].As<AudioBuffer>();
        float delayMs = (float)args[1].As<double>();
        float feedback = (float)args[2].As<double>();
        float mix = (float)args[3].As<double>();

        if (buffer.Frames == 0)
            return Value.Buffer(new AudioBuffer(0, buffer.Channels, buffer.SampleRate));

        var result = Delay.Apply(buffer, delayMs, feedback, mix);
        return Value.Buffer(result);
    }

    // ===== Gain =====

    private static void RegisterGain(InternalFunctionRegistry registry)
    {
        // gain(Buffer, Double) -> Buffer — gain in dB
        var gainSig = new FunctionSignature("gain",
            [BufferType.Instance, DoubleType.Instance]);
        registry.Register("gain", gainSig, GainEffect);
    }

    /// <summary>
    /// gain(Buffer, Double) — applies gain in dB. Negative values attenuate, positive values amplify.
    /// Returns a new buffer with the gain applied.
    /// </summary>
    private static Value GainEffect(IReadOnlyList<Value> args)
    {
        var buffer = args[0].As<AudioBuffer>();
        double gainDb = args[1].As<double>();

        if (buffer.Frames == 0)
            return Value.Buffer(new AudioBuffer(0, buffer.Channels, buffer.SampleRate));

        float gainLinear = (float)Math.Pow(10.0, gainDb / 20.0);

        var result = new AudioBuffer(buffer.Frames, buffer.Channels, buffer.SampleRate);

        bool wouldClip = false;
        for (int i = 0; i < buffer.Data.Length; i++)
        {
            float sample = buffer.Data[i] * gainLinear;
            if (Math.Abs(sample) > 1f) wouldClip = true;
            result.Data[i] = sample;
        }

        if (wouldClip && gainDb > 0)
        {
            Console.Error.WriteLine(
                $"Warning: gain({gainDb:F1} dB) causes clipping. Consider reducing gain or applying compression first.");
        }

        return Value.Buffer(result);
    }
}
