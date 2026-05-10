using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Audio.Vocalization;

/// <summary>
/// Registers vocalization built-in functions: sing, tts, setTtsCommand.
/// Connects the formant synthesis engine and TTS hook to the Flow runtime.
/// </summary>
public static class VocalizationFunctions
{
    /// <summary>
    /// Registers all vocalization functions with the interpreter.
    /// </summary>
    public static void Register(InternalFunctionRegistry registry)
    {
        // sing(String, Note, Double) -> Buffer
        var singSignature = new FunctionSignature("sing",
            [StringType.Instance, NoteType.Instance, DoubleType.Instance]);
        registry.Register("sing", singSignature, Sing);

        // tts(String) -> Buffer
        var ttsSignature = new FunctionSignature("tts", [StringType.Instance]);
        registry.Register("tts", ttsSignature, Tts);

        // setTtsCommand(String) -> Void
        var setTtsSig = new FunctionSignature("setTtsCommand", [StringType.Instance]);
        registry.Register("setTtsCommand", setTtsSig, SetTtsCommand);
    }

    /// <summary>
    /// sing(phoneme, pitch, duration) -- synthesize a vowel or consonant-vowel syllable
    /// at the given pitch and duration using formant synthesis.
    /// </summary>
    private static Value Sing(IReadOnlyList<Value> args)
    {
        string phoneme = args[0].As<string>();
        string noteStr = (string)args[1].Data!;

        // Parse note string to frequency via PitchConversion
        char noteName = noteStr[0];
        int alteration = 0;
        int idx = 1;

        if (idx < noteStr.Length && noteStr[idx] == '#')
        {
            alteration = 1;
            idx++;
        }
        else if (idx < noteStr.Length && noteStr[idx] == 'b')
        {
            alteration = -1;
            idx++;
        }

        int octave = int.Parse(noteStr[idx..]);
        double frequencyHz = PitchConversion.NoteToFrequency(noteName, octave, alteration);

        double duration = args[2].As<double>();

        var result = FormantSynthesizer.SynthesizeSyllable(phoneme, frequencyHz, duration);
        return Value.Buffer(result);
    }

    /// <summary>
    /// tts(text) -- run external TTS engine on text, return audio buffer.
    /// </summary>
    private static Value Tts(IReadOnlyList<Value> args)
    {
        string text = args[0].As<string>();
        var result = TtsHook.RunTts(text);
        return Value.Buffer(result);
    }

    /// <summary>
    /// setTtsCommand(command) -- set the external TTS command string.
    /// </summary>
    private static Value SetTtsCommand(IReadOnlyList<Value> args)
    {
        string command = args[0].As<string>();
        TtsHook.SetCommand(command);
        return Value.Void();
    }
}
