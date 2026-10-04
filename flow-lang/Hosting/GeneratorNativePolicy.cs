using FlowLang.StandardLibrary;
using FlowLang.StandardLibrary.Audio;
using FlowLang.StandardLibrary.Audio.DSP;
using FlowLang.StandardLibrary.Composition;
using FlowLang.StandardLibrary.Generative;
using FlowLang.StandardLibrary.Harmony;
using FlowLang.StandardLibrary.Patterns;
using FlowLang.StandardLibrary.Transforms;
using FlowLang.TypeSystem;

namespace FlowLang.Hosting;

/// <summary>DAW authoring uses in-memory construction/DSP, never arbitrary native
/// IO. Exact registered signatures are captured from reviewed families. Implicit
/// sampled rendering is separately disabled on the engine.</summary>
internal static class GeneratorNativePolicy
{
    internal static void Populate(HashSet<FunctionSignature> allowed, FlowLang.Runtime.ExecutionContext context)
    {
        PluginNativePolicy.Populate(allowed, context);
        var c = new InternalFunctionRegistry();
        UnitArithmetic.Register(c); ConversionFunctions.Register(c);
        BuiltInFunctions.RegisterBars(c); BuiltInFunctions.RegisterMusicalNotationFunctions(c);
        BuiltInFunctions.RegisterEuclideanOverloads(c, context);
        EffectsFunctions.Register(c); EffectsFunctions.RegisterContextDependent(c, context);
        PanningFunctions.Register(c); SongRenderer.Register(c); SongRenderer.RegisterContextDependent(c, context);
        TempoRampRenderer.Register(c); BeatConversionFunctions.RegisterContextDependent(c, context);
        BeatConstructorFunctions.RegisterContextDependent(c, context);
        TransformFunctions.Register(c); TransformFunctions.RegisterArticulationTransforms(c); TransformFunctions.RegisterContextDependent(c, context);
        HarmonyFunctions.Register(c); HarmonyFunctions.RegisterContextDependent(c, context);
        SongFunctions.Register(c, context); PolyrhythmFunctions.Register(c); VariationFunctions.Register(c);
        PatternFunctions.RegisterContextDependent(c, context); MarkovFunctions.RegisterContextDependent(c, context);
        LsystemFunctions.RegisterContextDependent(c, context); CellularFunctions.RegisterContextDependent(c, context);
        ChaosFunctions.RegisterContextDependent(c, context);
        // Only formant synthesis; external TTS and command configuration stay denied.
        FlowLang.StandardLibrary.Audio.Vocalization.VocalizationFunctions.RegisterContextDependent(c, context);
        FlowLang.StandardLibrary.Improv.StyleRegistry.RegisterBuiltinsOnly(c, context);
        FlowLang.StandardLibrary.Improv.JamFunctions.RegisterContextDependent(c, context);
        GranularFunctions.Register(c, context); StretchFunctions.Register(c, context); PitchShiftFunctions.Register(c, context);
        FlowLang.StandardLibrary.TestFramework.TestFunctions.RegisterTestFramework(c, context);
        foreach (var entry in c.EnumerateSignatures()) foreach (var signature in entry.Value) allowed.Add(signature);
        // These implementations share a registrar with legacy file/device operations.
        // Only explicitly reviewed in-memory operations enter the allowed set.
        var audioNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "createBuffer", "getFrames", "getChannels", "getSampleRate", "getSample", "setSample", "fillBuffer", "mixBuffers", "mix",
            "createOscillatorState", "createSineTone", "createClip", "noise", "resetPhase", "generateSine", "generateSaw", "generateSquare", "generateTriangle",
            "copyBuffer", "sliceBuffer", "appendBuffers", "beatsToFrames", "framesToBeats", "createVoice", "setVoiceGain", "setVoicePan", "setVoiceOffset",
            "createTrack", "addVoice", "setTrackOffset", "setTrackGain", "setTrackPan", "renderTrack", "setMaxVoices", "oscillator"
        };
        foreach (var entry in context.InternalRegistry.EnumerateSignatures())
            foreach (var signature in entry.Value)
                if (audioNames.Contains(signature.Name)) allowed.Add(signature);
    }
}
