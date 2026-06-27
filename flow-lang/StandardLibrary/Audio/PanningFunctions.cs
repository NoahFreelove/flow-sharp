using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.StandardLibrary.Audio.DSP;

namespace FlowLang.StandardLibrary.Audio;

/// <summary>
/// Registers the pan built-in function for stereo panning.
/// pan(Buffer, Double) -> Buffer applies constant-power stereo panning.
/// Composable via flow operator: tone -> pan(-0.3)
/// </summary>
public static class PanningFunctions
{
    /// <summary>
    /// Registers all panning built-in functions.
    /// </summary>
    public static void Register(InternalFunctionRegistry registry)
    {
        // pan(Buffer, Double) -> Buffer — constant-power stereo panning
        var panSig = new FunctionSignature("pan",
            [BufferType.Instance, DoubleType.Instance],
            ParameterNames: ["buf", "pan"]);
        registry.Register("pan", panSig, PanEffect);
    }

    /// <summary>
    /// pan(Buffer, Double) — applies constant-power stereo panning.
    /// Pan value: -1.0 = hard left, 0.0 = center, 1.0 = hard right.
    /// Always returns a stereo buffer (mono inputs promoted to stereo).
    /// </summary>
    private static Value PanEffect(IReadOnlyList<Value> args)
    {
        var buffer = args[0].As<AudioBuffer>();
        var panValue = (float)args[1].As<double>();

        if (buffer.Frames == 0)
            return Value.Buffer(new AudioBuffer(0, 2, buffer.SampleRate));

        var result = Panner.Apply(buffer, panValue);
        return Value.Buffer(result);
    }
}
