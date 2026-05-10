using FlowLang.Runtime;
using FlowLang.StandardLibrary.Audio.Tuning;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Audio.Vocalization;

/// <summary>
/// Registers vocalization built-in functions: sing, tts, setTtsCommand.
/// Connects the formant synthesis engine and TTS hook to the Flow runtime.
///
/// Phase 23 (Plan 23-02 Task 3, WARNING-2): <see cref="Sing"/> is migrated from
/// context-free to context-dependent registration so it can resolve the active
/// <see cref="RenderTuning"/> per <see cref="SongRenderer.ResolveRenderTuning"/>.
/// Under <c>enable justIntonation;</c> + <c>key Cmajor</c>, an E4 vocalization
/// renders at the JI 5/4 ratio (vs 12-TET ~329.63 Hz).
/// </summary>
public static class VocalizationFunctions
{
    /// <summary>
    /// Registers context-FREE vocalization functions (tts, setTtsCommand) — sing
    /// migrated to <see cref="RegisterContextDependent"/> in Phase 23 Plan 23-02.
    /// </summary>
    public static void Register(InternalFunctionRegistry registry)
    {
        // tts(String) -> Buffer
        var ttsSignature = new FunctionSignature("tts", [StringType.Instance]);
        registry.Register("tts", ttsSignature, Tts);

        // setTtsCommand(String) -> Void
        var setTtsSig = new FunctionSignature("setTtsCommand", [StringType.Instance]);
        registry.Register("setTtsCommand", setTtsSig, SetTtsCommand);
    }

    /// <summary>
    /// Phase 23 Plan 23-02 Task 3: context-dependent registration for <c>sing</c>. The
    /// resolved <see cref="RenderTuning"/> is captured via the closure over
    /// <paramref name="context"/> and applied at call time, so each invocation reads
    /// the live <see cref="MusicalContext"/> top-of-stack and routes through the
    /// tuning-aware <see cref="PitchConversion.NoteToFrequency(MusicalNoteData, RenderTuning)"/>
    /// path. Default tuning short-circuits to byte-identical 12-TET (Pitfall 6).
    /// </summary>
    public static void RegisterContextDependent(
        InternalFunctionRegistry registry,
        FlowLang.Runtime.ExecutionContext context)
    {
        var singSignature = new FunctionSignature("sing",
            [StringType.Instance, NoteType.Instance, DoubleType.Instance]);
        registry.Register("sing", singSignature, args => SingWithContext(args, context));
    }

    /// <summary>
    /// sing(phoneme, pitch, duration) -- synthesize a vowel or consonant-vowel syllable
    /// at the given pitch and duration using formant synthesis. Phase 23: tuning-aware.
    /// </summary>
    private static Value SingWithContext(
        IReadOnlyList<Value> args,
        FlowLang.Runtime.ExecutionContext context)
    {
        string phoneme = args[0].As<string>();
        string noteStr = (string)args[1].Data!;

        // Parse note string into (NoteName, Octave, Alteration).
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

        // Phase 23: route through the tuning-aware NoteToFrequency overload using the
        // section's resolved RenderTuning. Default short-circuits to byte-identical
        // 12-TET so existing Vocalization tutorials remain unchanged.
        var note = new MusicalNoteData(noteName, octave, alteration, durationValue: null, isRest: false);
        var tuning = SongRenderer.ResolveRenderTuning(context.GetMusicalContext());
        double frequencyHz = PitchConversion.NoteToFrequency(note, tuning);

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
