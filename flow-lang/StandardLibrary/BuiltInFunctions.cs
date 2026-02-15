using FlowLang.Audio;
using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary;

/// <summary>
/// Registers Flow built-in functions with their C# implementations.
/// Actual implementations are in stdlib.cs.
/// </summary>
public static class BuiltInFunctions
{
    /// <summary>
    /// Registers all C# implementations of internal functions.
    /// </summary>
    public static void RegisterAllImplementations(InternalFunctionRegistry registry)
    {
        RegisterStdLib(registry);
        RegisterCollections(registry);
        RegisterAudio(registry);
        RegisterBars(registry);
        RegisterMusicalNotationFunctions(registry);
        Audio.EffectsFunctions.Register(registry);
        Transforms.TransformFunctions.Register(registry);
        Harmony.HarmonyFunctions.Register(registry);
    }

    /// <summary>
    /// Registers all C# implementations including playback functions that need an audio manager.
    /// </summary>
    public static void RegisterAllImplementations(InternalFunctionRegistry registry, AudioPlaybackManager audioManager)
    {
        RegisterAllImplementations(registry);
        Audio.PlaybackFunctions.Register(registry, audioManager);
    }

    private static void RegisterStdLib(InternalFunctionRegistry registry)
    {
        var lenStrSignature = new FunctionSignature("len", [StringType.Instance]);
        registry.Register("len", lenStrSignature, stdlib.LenString);
        
        // ===== I/O Functions =====
        var printSignature = new FunctionSignature(
            "print",
            [StringType.Instance]);
        registry.Register("print", printSignature, stdlib.Print);

        // ===== String Conversion Functions =====

        var strIntSignature = new FunctionSignature("str", [IntType.Instance]);
        registry.Register("str", strIntSignature, stdlib.StrInt);

        var strFloatSignature = new FunctionSignature("str", [FloatType.Instance]);
        registry.Register("str", strFloatSignature, stdlib.StrFloat);

        var strDoubleSignature = new FunctionSignature("str", [DoubleType.Instance]);
        registry.Register("str", strDoubleSignature, stdlib.StrDouble);

        var strStringSignature = new FunctionSignature("str", [StringType.Instance]);
        registry.Register("str", strStringSignature, stdlib.StrString);

        var strBoolSignature = new FunctionSignature("str", [BoolType.Instance]);
        registry.Register("str", strBoolSignature, stdlib.StrBool);

        var strNoteSignature = new FunctionSignature("str", [NoteType.Instance]);
        registry.Register("str", strNoteSignature, stdlib.StrNote);

        var strBarSignature = new FunctionSignature("str", [BarType.Instance]);
        registry.Register("str", strBarSignature, stdlib.StrBar);

        var strSemitoneSignature = new FunctionSignature("str", [SemitoneType.Instance]);
        registry.Register("str", strSemitoneSignature, stdlib.StrSemitone);

        var strCentSignature = new FunctionSignature("str", [CentType.Instance]);
        registry.Register("str", strCentSignature, stdlib.StrCent);

        var strMillisecondSignature = new FunctionSignature("str", [MillisecondType.Instance]);
        registry.Register("str", strMillisecondSignature, stdlib.StrMillisecond);

        var strSecondSignature = new FunctionSignature("str", [SecondType.Instance]);
        registry.Register("str", strSecondSignature, stdlib.StrSecond);

        var strDecibelSignature = new FunctionSignature("str", [DecibelType.Instance]);
        registry.Register("str", strDecibelSignature, stdlib.StrDecibel);

        var strArraySignature = new FunctionSignature("str", [new ArrayType(VoidType.Instance)]);
        registry.Register("str", strArraySignature, stdlib.StrArray);

        var strSequenceSignature = new FunctionSignature("str", [SequenceType.Instance]);
        registry.Register("str", strSequenceSignature, args =>
        {
            var seq = args[0].As<SequenceData>();
            return Value.String(seq.ToString());
        });

        var concatSignature = new FunctionSignature("concat", [StringType.Instance, StringType.Instance]);
        registry.Register("concat", concatSignature, stdlib.Concat);

        // ===== Type Conversion Functions =====

        var intToDoubleSignature = new FunctionSignature("intToDouble", [IntType.Instance]);
        registry.Register("intToDouble", intToDoubleSignature, stdlib.IntToDouble);

        var doubleToIntSignature = new FunctionSignature("doubleToInt", [DoubleType.Instance]);
        registry.Register("doubleToInt", doubleToIntSignature, stdlib.DoubleToInt);

        // ===== Arithmetic Functions =====

        var addIntSignature = new FunctionSignature(
            "add",
            [IntType.Instance, IntType.Instance]);
        registry.Register("add", addIntSignature, stdlib.AddInt);

        var addFloatSignature = new FunctionSignature(
            "add",
            [FloatType.Instance, FloatType.Instance]);
        registry.Register("add", addFloatSignature, stdlib.AddFloat);

        var subSignature = new FunctionSignature(
            "sub",
            [IntType.Instance, IntType.Instance]);
        registry.Register("sub", subSignature, stdlib.SubInt);

        var mulSignature = new FunctionSignature(
            "mul",
            [IntType.Instance, IntType.Instance]);
        registry.Register("mul", mulSignature, stdlib.MulInt);

        var divSignature = new FunctionSignature(
            "div",
            [IntType.Instance, IntType.Instance]);
        registry.Register("div", divSignature, stdlib.DivInt);

        // Float/Double overloads for arithmetic
        var addDoubleSignature = new FunctionSignature(
            "add",
            [DoubleType.Instance, DoubleType.Instance]);
        registry.Register("add", addDoubleSignature, stdlib.AddFloat);

        var subDoubleSignature = new FunctionSignature(
            "sub",
            [DoubleType.Instance, DoubleType.Instance]);
        registry.Register("sub", subDoubleSignature, stdlib.SubDouble);

        var mulDoubleSignature = new FunctionSignature(
            "mul",
            [DoubleType.Instance, DoubleType.Instance]);
        registry.Register("mul", mulDoubleSignature, stdlib.MulDouble);

        var divDoubleSignature = new FunctionSignature(
            "div",
            [DoubleType.Instance, DoubleType.Instance]);
        registry.Register("div", divDoubleSignature, stdlib.DivDouble);

        // String-to-number conversions
        var stringToIntSignature = new FunctionSignature("stringToInt", [StringType.Instance]);
        registry.Register("stringToInt", stringToIntSignature, stdlib.StringToInt);

        var stringToDoubleSignature = new FunctionSignature("stringToDouble", [StringType.Instance]);
        registry.Register("stringToDouble", stringToDoubleSignature, stdlib.StringToDouble);

        // ===== Lazy Evaluation Functions =====

        // Note: eval is registered with Lazy<Void> but will work with any Lazy<T>
        // due to special handling in the implementation
        var evalSignature = new FunctionSignature(
            "eval",
            [new LazyType(VoidType.Instance)]);
        registry.Register("eval", evalSignature, stdlib.Eval);
        
        var ifSignature = new FunctionSignature(
            "if", [BoolType.Instance, new LazyType(VoidType.Instance), new LazyType(VoidType.Instance)]);
        registry.Register("if", ifSignature, stdlib.If);
        
        var andSignature = new FunctionSignature(
            "and", [new LazyType(BoolType.Instance), new LazyType(BoolType.Instance)]);
        registry.Register("and", andSignature, stdlib.And);
        
        var andBoolSignature = new FunctionSignature(
            "and", [BoolType.Instance, BoolType.Instance]);
        registry.Register("and", andBoolSignature, stdlib.AndBool);
        
        var orSignature = new FunctionSignature(
            "or", [new LazyType(BoolType.Instance), new LazyType(BoolType.Instance)]);
        registry.Register("or", orSignature, stdlib.Or);
        
        var orBoolSignature = new FunctionSignature(
            "or", [BoolType.Instance, BoolType.Instance]);
        registry.Register("or", orBoolSignature, stdlib.OrBool);

        // ===== Equality and Comparison Functions =====
        // VoidType.Instance is used as a wildcard/"any type" parameter in these signatures.
        // The overload resolver treats Void as compatible with all types, allowing these
        // functions to accept arguments of any type.

        var equalsSignature = new FunctionSignature(
            "equals",
            [VoidType.Instance, VoidType.Instance]);
        registry.Register("equals", equalsSignature, stdlib.Equals);

        var sequalsSignature = new FunctionSignature(
            "sequals",
            [VoidType.Instance, VoidType.Instance]);
        registry.Register("sequals", sequalsSignature, stdlib.StrictEquals);

        var ltSignature = new FunctionSignature(
            "lt",
            [VoidType.Instance, VoidType.Instance]);
        registry.Register("lt", ltSignature, stdlib.LessThan);

        var gtSignature = new FunctionSignature(
            "gt",
            [VoidType.Instance, VoidType.Instance]);
        registry.Register("gt", gtSignature, stdlib.GreaterThan);

        var lteSignature = new FunctionSignature(
            "lte",
            [VoidType.Instance, VoidType.Instance]);
        registry.Register("lte", lteSignature, stdlib.LessThanOrEqual);

        var gteSignature = new FunctionSignature(
            "gte",
            [VoidType.Instance, VoidType.Instance]);
        registry.Register("gte", gteSignature, stdlib.GreaterThanOrEqual);

        // ===== Random Number Generation =====

        var randSignature = new FunctionSignature("?", []);
        registry.Register("?", randSignature, stdlib.Rand);
        
        var fixedRandSignature = new FunctionSignature("??", []);
        registry.Register("??", fixedRandSignature, stdlib.FixedRand);
        
        var resetRandSignature = new FunctionSignature("??reset", []);
        registry.Register("??reset", resetRandSignature, stdlib.FixedRandReset);
        
        var setRandSignature = new FunctionSignature("??set", [IntType.Instance]);
        registry.Register("??set", setRandSignature, stdlib.FixedRandSet);
    }

    private static void RegisterCollections(InternalFunctionRegistry registry)
    {
        // ===== Array Functions =====

        var listSignature = new FunctionSignature(
            "list",
            [VoidType.Instance],
            IsVarArgs: true);
        registry.Register("list", listSignature, collections.List);

        var lenSignature = new FunctionSignature("len", [new ArrayType(VoidType.Instance)]);
        registry.Register("len", lenSignature, collections.Len);
        
        var headSignature = new FunctionSignature("head", [new ArrayType(VoidType.Instance)]);
        registry.Register("head", headSignature, collections.Head);

        var tailSignature = new FunctionSignature("tail", [new ArrayType(VoidType.Instance)]);
        registry.Register("tail", tailSignature, collections.Tail);

        var lastSignature = new FunctionSignature("last", [new ArrayType(VoidType.Instance)]);
        registry.Register("last", lastSignature, collections.Last);

        var initSignature = new FunctionSignature("init", [new ArrayType(VoidType.Instance)]);
        registry.Register("init", initSignature, collections.Init);

        var emptySignature = new FunctionSignature("empty", [new ArrayType(VoidType.Instance)]);
        registry.Register("empty", emptySignature, collections.Empty);

        var reverseSignature = new FunctionSignature("reverse", [new ArrayType(VoidType.Instance)]);
        registry.Register("reverse", reverseSignature, collections.Reverse);

        var takeSignature = new FunctionSignature("take", [new ArrayType(VoidType.Instance), IntType.Instance]);
        registry.Register("take", takeSignature, collections.Take);

        var dropSignature = new FunctionSignature("drop", [new ArrayType(VoidType.Instance), IntType.Instance]);
        registry.Register("drop", dropSignature, collections.Drop);

        var appendSignature = new FunctionSignature("append", [new ArrayType(VoidType.Instance), VoidType.Instance]);
        registry.Register("append", appendSignature, collections.Append);

        var prependSignature = new FunctionSignature("prepend", [VoidType.Instance, new ArrayType(VoidType.Instance)]);
        registry.Register("prepend", prependSignature, collections.Prepend);

        // Note: "concat" is intentionally overloaded for both strings (in RegisterStdLib)
        // and arrays (here). The overload resolver selects the correct one by argument types.
        var concatSignature = new FunctionSignature("concat", [new ArrayType(VoidType.Instance), new ArrayType(VoidType.Instance)]);
        registry.Register("concat", concatSignature, collections.Concat);

        var containsSignature = new FunctionSignature("contains", [new ArrayType(VoidType.Instance), VoidType.Instance]);
        registry.Register("contains", containsSignature, collections.Contains);

        // ===== Higher-Order Functions =====

        var eachSignature = new FunctionSignature("each", [new ArrayType(VoidType.Instance), FunctionType.Instance]);
        registry.Register("each", eachSignature, collections.Each);

        var mapSignature = new FunctionSignature("map", [new ArrayType(VoidType.Instance), FunctionType.Instance]);
        registry.Register("map", mapSignature, collections.Map);

        var filterSignature = new FunctionSignature("filter", [new ArrayType(VoidType.Instance), FunctionType.Instance]);
        registry.Register("filter", filterSignature, collections.Filter);

        var reduceSignature = new FunctionSignature("reduce", [new ArrayType(VoidType.Instance), VoidType.Instance, FunctionType.Instance]);
        registry.Register("reduce", reduceSignature, collections.Reduce);
    }

    private static void RegisterAudio(InternalFunctionRegistry registry)
    {
        // ===== Core Buffer Operations =====

        var createBufferSignature = new FunctionSignature(
            "createBuffer",
            [IntType.Instance, IntType.Instance, IntType.Instance]);
        registry.Register("createBuffer", createBufferSignature, Audio.AudioCore.CreateBuffer);

        var getFramesSignature = new FunctionSignature("getFrames", [BufferType.Instance]);
        registry.Register("getFrames", getFramesSignature, Audio.AudioCore.GetFrames);

        var getChannelsSignature = new FunctionSignature("getChannels", [BufferType.Instance]);
        registry.Register("getChannels", getChannelsSignature, Audio.AudioCore.GetChannels);

        var getSampleRateSignature = new FunctionSignature("getSampleRate", [BufferType.Instance]);
        registry.Register("getSampleRate", getSampleRateSignature, Audio.AudioCore.GetSampleRate);

        var getSampleSignature = new FunctionSignature(
            "getSample",
            [BufferType.Instance, IntType.Instance, IntType.Instance]);
        registry.Register("getSample", getSampleSignature, Audio.AudioCore.GetSample);

        var setSampleSignature = new FunctionSignature(
            "setSample",
            [BufferType.Instance, IntType.Instance, IntType.Instance, DoubleType.Instance]);
        registry.Register("setSample", setSampleSignature, Audio.AudioCore.SetSample);

        var fillBufferSignature = new FunctionSignature(
            "fillBuffer",
            [BufferType.Instance, DoubleType.Instance]);
        registry.Register("fillBuffer", fillBufferSignature, Audio.AudioCore.FillBuffer);

        var mixBuffersSignature = new FunctionSignature(
            "mixBuffers",
            [BufferType.Instance, BufferType.Instance, DoubleType.Instance, DoubleType.Instance]);
        registry.Register("mixBuffers", mixBuffersSignature, Audio.AudioCore.MixBuffers);

        // ===== File I/O Operations =====

        // exportWav(Buffer, String) - default 16-bit
        var exportWavSignature = new FunctionSignature(
            "exportWav",
            [BufferType.Instance, StringType.Instance]);
        registry.Register("exportWav", exportWavSignature, Audio.FileIO.ExportWav);

        // exportWav(Buffer, String, Int) - custom bit depth
        var exportWavWithDepthSignature = new FunctionSignature(
            "exportWav",
            [BufferType.Instance, StringType.Instance, IntType.Instance]);
        registry.Register("exportWav", exportWavWithDepthSignature, Audio.FileIO.ExportWavWithBitDepth);

        // ===== Signal Generation Operations =====

        var createOscillatorStateSignature = new FunctionSignature(
            "createOscillatorState",
            [DoubleType.Instance, IntType.Instance]);
        registry.Register("createOscillatorState", createOscillatorStateSignature, Audio.SignalGeneration.CreateOscillatorState);

        var resetPhaseSignature = new FunctionSignature(
            "resetPhase",
            [OscillatorStateType.Instance]);
        registry.Register("resetPhase", resetPhaseSignature, Audio.SignalGeneration.ResetPhase);

        var generateSineSignature = new FunctionSignature(
            "generateSine",
            [BufferType.Instance, OscillatorStateType.Instance, DoubleType.Instance]);
        registry.Register("generateSine", generateSineSignature, Audio.SignalGeneration.GenerateSine);

        var generateSawSignature = new FunctionSignature(
            "generateSaw",
            [BufferType.Instance, OscillatorStateType.Instance, DoubleType.Instance]);
        registry.Register("generateSaw", generateSawSignature, Audio.SignalGeneration.GenerateSaw);

        var generateSquareSignature = new FunctionSignature(
            "generateSquare",
            [BufferType.Instance, OscillatorStateType.Instance, DoubleType.Instance]);
        registry.Register("generateSquare", generateSquareSignature, Audio.SignalGeneration.GenerateSquare);

        var generateTriangleSignature = new FunctionSignature(
            "generateTriangle",
            [BufferType.Instance, OscillatorStateType.Instance, DoubleType.Instance]);
        registry.Register("generateTriangle", generateTriangleSignature, Audio.SignalGeneration.GenerateTriangle);

        // ===== Buffer Helper Operations =====

        var copyBufferSignature = new FunctionSignature(
            "copyBuffer",
            [BufferType.Instance]);
        registry.Register("copyBuffer", copyBufferSignature, Audio.BufferHelpers.CopyBuffer);

        var sliceBufferSignature = new FunctionSignature(
            "sliceBuffer",
            [BufferType.Instance, IntType.Instance, IntType.Instance]);
        registry.Register("sliceBuffer", sliceBufferSignature, Audio.BufferHelpers.SliceBuffer);

        var appendBuffersSignature = new FunctionSignature(
            "appendBuffers",
            [BufferType.Instance, BufferType.Instance]);
        registry.Register("appendBuffers", appendBuffersSignature, Audio.BufferHelpers.AppendBuffers);

        var scaleBufferSignature = new FunctionSignature(
            "scaleBuffer",
            [BufferType.Instance, DoubleType.Instance]);
        registry.Register("scaleBuffer", scaleBufferSignature, Audio.BufferHelpers.ScaleBuffer);

        // ===== Envelope Operations =====

        var createARSignature = new FunctionSignature(
            "createAR",
            [DoubleType.Instance, DoubleType.Instance, IntType.Instance]);
        registry.Register("createAR", createARSignature, Audio.EnvelopeProcessor.CreateAR);

        var createADSRSignature = new FunctionSignature(
            "createADSR",
            [DoubleType.Instance, DoubleType.Instance, DoubleType.Instance, DoubleType.Instance, IntType.Instance]);
        registry.Register("createADSR", createADSRSignature, Audio.EnvelopeProcessor.CreateADSR);

        var applyEnvelopeSignature = new FunctionSignature(
            "applyEnvelope",
            [BufferType.Instance, EnvelopeType.Instance]);
        registry.Register("applyEnvelope", applyEnvelopeSignature, Audio.EnvelopeProcessor.ApplyEnvelope);

        // ===== Timeline Operations =====

        var setBPMSignature = new FunctionSignature(
            "setBPM",
            [DoubleType.Instance]);
        registry.Register("setBPM", setBPMSignature, Audio.Timeline.SetBPM);

        var getBPMSignature = new FunctionSignature("getBPM", []);
        registry.Register("getBPM", getBPMSignature, Audio.Timeline.GetBPM);

        var beatsToFramesSignature = new FunctionSignature(
            "beatsToFrames",
            [DoubleType.Instance, IntType.Instance]);
        registry.Register("beatsToFrames", beatsToFramesSignature, Audio.Timeline.BeatsToFrames);

        var framesToBeatsSignature = new FunctionSignature(
            "framesToBeats",
            [IntType.Instance, IntType.Instance]);
        registry.Register("framesToBeats", framesToBeatsSignature, Audio.Timeline.FramesToBeats);

        var createVoiceSignature = new FunctionSignature(
            "createVoice",
            [BufferType.Instance, DoubleType.Instance]);
        registry.Register("createVoice", createVoiceSignature, Audio.Timeline.CreateVoice);

        var setVoiceGainSignature = new FunctionSignature(
            "setVoiceGain",
            [VoiceType.Instance, DoubleType.Instance]);
        registry.Register("setVoiceGain", setVoiceGainSignature, Audio.Timeline.SetVoiceGain);

        var setVoicePanSignature = new FunctionSignature(
            "setVoicePan",
            [VoiceType.Instance, DoubleType.Instance]);
        registry.Register("setVoicePan", setVoicePanSignature, Audio.Timeline.SetVoicePan);

        var createTrackSignature = new FunctionSignature(
            "createTrack",
            [IntType.Instance, IntType.Instance]);
        registry.Register("createTrack", createTrackSignature, Audio.Timeline.CreateTrack);

        var addVoiceSignature = new FunctionSignature(
            "addVoice",
            [TrackType.Instance, VoiceType.Instance]);
        registry.Register("addVoice", addVoiceSignature, Audio.Timeline.AddVoice);

        var setTrackOffsetSignature = new FunctionSignature(
            "setTrackOffset",
            [TrackType.Instance, DoubleType.Instance]);
        registry.Register("setTrackOffset", setTrackOffsetSignature, Audio.Timeline.SetTrackOffset);

        var setTrackGainSignature = new FunctionSignature(
            "setTrackGain",
            [TrackType.Instance, DoubleType.Instance]);
        registry.Register("setTrackGain", setTrackGainSignature, Audio.Timeline.SetTrackGain);

        var setTrackPanSignature = new FunctionSignature(
            "setTrackPan",
            [TrackType.Instance, DoubleType.Instance]);
        registry.Register("setTrackPan", setTrackPanSignature, Audio.Timeline.SetTrackPan);

        var renderTrackSignature = new FunctionSignature(
            "renderTrack",
            [TrackType.Instance, DoubleType.Instance]);
        registry.Register("renderTrack", renderTrackSignature, Audio.Timeline.RenderTrack);
    }

    private static void RegisterBars(InternalFunctionRegistry registry)
    {
        // ===== Bar Operations =====

        var createBarSignature = new FunctionSignature("createBar", []);
        registry.Register("createBar", createBarSignature, Bars.CreateBar);

        var createBarWithNoteSignature = new FunctionSignature(
            "createBarWithNote",
            [NoteType.Instance]);
        registry.Register("createBarWithNote", createBarWithNoteSignature, Bars.CreateBarWithNote);

        var createBarFromNotesSignature = new FunctionSignature(
            "createBarFromNotes",
            [new ArrayType(NoteType.Instance)]);
        registry.Register("createBarFromNotes", createBarFromNotesSignature, Bars.CreateBarFromNotes);

        var addNoteToBarSignature = new FunctionSignature(
            "addNoteToBar",
            [BarType.Instance, NoteType.Instance]);
        registry.Register("addNoteToBar", addNoteToBarSignature, Bars.AddNoteToBar);

        var getNoteFromBarSignature = new FunctionSignature(
            "getNoteFromBar",
            [BarType.Instance, IntType.Instance]);
        registry.Register("getNoteFromBar", getNoteFromBarSignature, Bars.GetNoteFromBar);

        var barLengthSignature = new FunctionSignature("barLength", [BarType.Instance]);
        registry.Register("barLength", barLengthSignature, Bars.BarLength);

        var setTimeSignatureSignature = new FunctionSignature(
            "setTimeSignature",
            [BarType.Instance, IntType.Instance, IntType.Instance]);
        registry.Register("setTimeSignature", setTimeSignatureSignature, Bars.SetTimeSignature);

        var getTimeSignatureSignature = new FunctionSignature(
            "getTimeSignature",
            [BarType.Instance]);
        registry.Register("getTimeSignature", getTimeSignatureSignature, Bars.GetTimeSignature);
    }

    private static void RegisterMusicalNotationFunctions(InternalFunctionRegistry registry)
    {
        // ===== Musical Note Creation =====

        var createMusicalNoteSignature = new FunctionSignature(
            "createMusicalNote",
            [NoteType.Instance, NoteValueType.Instance]);
        registry.Register("createMusicalNote", createMusicalNoteSignature, args =>
        {
            string pitchStr = (string)args[0].Data!;
            int durationValue = (int)args[1].Data!;
            var note = Audio.ClassicalComposition.CreateMusicalNote(pitchStr, durationValue);
            return Value.MusicalNote(note);
        });

        var createRestSignature = new FunctionSignature(
            "createRest",
            [NoteValueType.Instance]);
        registry.Register("createRest", createRestSignature, args =>
        {
            int durationValue = (int)args[0].Data!;
            var rest = Audio.ClassicalComposition.CreateRest(durationValue);
            return Value.MusicalNote(rest);
        });

        // ===== Time Signature =====

        var createTimeSignatureSignature = new FunctionSignature(
            "createTimeSignature",
            [IntType.Instance, IntType.Instance]);
        registry.Register("createTimeSignature", createTimeSignatureSignature, args =>
        {
            int numerator = (int)args[0].Data!;
            int denominator = (int)args[1].Data!;
            var timeSig = Audio.ClassicalComposition.CreateTimeSignature(numerator, denominator);
            return Value.TimeSignature(timeSig);
        });

        // ===== Musical Bar Creation =====

        var createMusicalBarSignature = new FunctionSignature(
            "createMusicalBar",
            [new ArrayType(NoteType.Instance), TimeSignatureType.Instance]);
        registry.Register("createMusicalBar", createMusicalBarSignature, args =>
        {
            var notesArray = (IReadOnlyList<Value>)args[0].Data!;
            var notes = new List<MusicalNoteData>();
            foreach (var noteValue in notesArray)
            {
                notes.Add((MusicalNoteData)noteValue.Data!);
            }

            var timeSig = (TimeSignatureData)args[1].Data!;
            var bar = Audio.ClassicalComposition.CreateMusicalBar(notes, timeSig);
            return Value.Bar(bar);
        });

        // ===== Incremental Bar Building =====

        var createEmptyMusicalBarSignature = new FunctionSignature(
            "createEmptyMusicalBar",
            [TimeSignatureType.Instance]);
        registry.Register("createEmptyMusicalBar", createEmptyMusicalBarSignature, args =>
        {
            var timeSig = (TimeSignatureData)args[0].Data!;
            var bar = Audio.ClassicalComposition.CreateEmptyMusicalBar(timeSig);
            return Value.Bar(bar);
        });

        var tryAddNoteToBarSignature = new FunctionSignature(
            "tryAddNoteToBar",
            [BarType.Instance, NoteType.Instance]);
        registry.Register("tryAddNoteToBar", tryAddNoteToBarSignature, args =>
        {
            var bar = (BarData)args[0].Data!;
            var note = (MusicalNoteData)args[1].Data!;
            bool success = Audio.ClassicalComposition.TryAddNoteToBar(bar, note);
            return Value.Bool(success);
        });

        var addNoteToBarSignature = new FunctionSignature(
            "addNoteToBar",
            [BarType.Instance, NoteType.Instance]);
        registry.Register("addNoteToBar", addNoteToBarSignature, args =>
        {
            var bar = (BarData)args[0].Data!;
            var note = (MusicalNoteData)args[1].Data!;
            Audio.ClassicalComposition.AddNoteToBar(bar, note);
            return Value.Void();
        });

        // ===== Musical Conversions =====

        var noteValueToBeatsSignature = new FunctionSignature(
            "noteValueToBeats",
            [NoteValueType.Instance, IntType.Instance]);
        registry.Register("noteValueToBeats", noteValueToBeatsSignature, args =>
        {
            int noteValueEnum = (int)args[0].Data!;
            int denominator = (int)args[1].Data!;
            double beats = Audio.MusicalConversions.NoteValueToBeats(noteValueEnum, denominator);
            return Value.Double(beats);
        });

        var validateBarDurationSignature = new FunctionSignature(
            "validateBarDuration",
            [BarType.Instance, TimeSignatureType.Instance]);
        registry.Register("validateBarDuration", validateBarDurationSignature, args =>
        {
            var bar = (BarData)args[0].Data!;
            var timeSig = (TimeSignatureData)args[1].Data!;
            bool isValid = Audio.MusicalConversions.ValidateBarDuration(bar, timeSig);
            return Value.Bool(isValid);
        });

        // ===== Bar Validation Helpers =====

        var getRemainingBeatsSignature = new FunctionSignature(
            "getRemainingBeats",
            [BarType.Instance]);
        registry.Register("getRemainingBeats", getRemainingBeatsSignature, args =>
        {
            var bar = (BarData)args[0].Data!;
            double remaining = Audio.MusicalConversions.GetRemainingBeats(bar);
            return Value.Double(remaining);
        });

        var wouldFitSignature = new FunctionSignature(
            "wouldFit",
            [BarType.Instance, NoteType.Instance]);
        registry.Register("wouldFit", wouldFitSignature, args =>
        {
            var bar = (BarData)args[0].Data!;
            var note = (MusicalNoteData)args[1].Data!;
            bool fits = Audio.MusicalConversions.WouldFit(bar, note);
            return Value.Bool(fits);
        });

        var calculateOverflowSignature = new FunctionSignature(
            "calculateOverflow",
            [BarType.Instance]);
        registry.Register("calculateOverflow", calculateOverflowSignature, args =>
        {
            var bar = (BarData)args[0].Data!;
            double overflow = Audio.MusicalConversions.CalculateOverflow(bar);
            return Value.Double(overflow);
        });

        // ===== Bar Rendering =====

        var renderBarToVoicesSignature = new FunctionSignature(
            "renderBarToVoices",
            [BarType.Instance, StringType.Instance, IntType.Instance, DoubleType.Instance]);
        registry.Register("renderBarToVoices", renderBarToVoicesSignature, args =>
        {
            var bar = (BarData)args[0].Data!;
            string synthType = (string)args[1].Data!;
            int sampleRate = (int)args[2].Data!;
            double bpm = (double)args[3].Data!;

            var voices = Audio.BarRenderer.RenderBarToVoices(bar, synthType, sampleRate, bpm);
            var voiceValues = voices.Select(v => Value.Voice(v)).ToArray();
            return Value.Array(voiceValues, VoiceType.Instance);
        });

        // ===== Sequence Functions =====

        var createSequenceSignature = new FunctionSignature("createSequence", []);
        registry.Register("createSequence", createSequenceSignature, args =>
        {
            var sequence = Audio.SequenceRenderer.CreateSequence();
            return Value.Sequence(sequence);
        });

        var addBarToSequenceSignature = new FunctionSignature(
            "addBarToSequence",
            [SequenceType.Instance, BarType.Instance]);
        registry.Register("addBarToSequence", addBarToSequenceSignature, args =>
        {
            var sequence = (SequenceData)args[0].Data!;
            var bar = (BarData)args[1].Data!;
            Audio.SequenceRenderer.AddBarToSequence(sequence, bar);
            return Value.Sequence(sequence);
        });

        var renderSequenceToVoicesSignature = new FunctionSignature(
            "renderSequenceToVoices",
            [SequenceType.Instance, StringType.Instance, IntType.Instance, DoubleType.Instance]);
        registry.Register("renderSequenceToVoices", renderSequenceToVoicesSignature, args =>
        {
            var sequence = (SequenceData)args[0].Data!;
            string synthType = (string)args[1].Data!;
            int sampleRate = (int)args[2].Data!;
            double bpm = (double)args[3].Data!;

            var voices = Audio.SequenceRenderer.RenderSequenceToVoices(sequence, synthType, sampleRate, bpm);
            var voiceValues = voices.Select(v => Value.Voice(v)).ToArray();
            return Value.Array(voiceValues, VoiceType.Instance);
        });

        // ===== Manual Bar Positioning =====

        var renderBarAtBeatSignature = new FunctionSignature(
            "renderBarAtBeat",
            [BarType.Instance, DoubleType.Instance, StringType.Instance, IntType.Instance, DoubleType.Instance]);
        registry.Register("renderBarAtBeat", renderBarAtBeatSignature, args =>
        {
            var bar = (BarData)args[0].Data!;
            double beatOffset = (double)args[1].Data!;
            string synthType = (string)args[2].Data!;
            int sampleRate = (int)args[3].Data!;
            double bpm = (double)args[4].Data!;

            var voices = Audio.BarRenderer.RenderBarAtBeat(bar, beatOffset, synthType, sampleRate, bpm);
            var voiceValues = voices.Select(v => Value.Voice(v)).ToArray();
            return Value.Array(voiceValues, VoiceType.Instance);
        });

        var renderBarAtTimeSignature = new FunctionSignature(
            "renderBarAtTime",
            [BarType.Instance, DoubleType.Instance, StringType.Instance, IntType.Instance, DoubleType.Instance]);
        registry.Register("renderBarAtTime", renderBarAtTimeSignature, args =>
        {
            var bar = (BarData)args[0].Data!;
            double timeSeconds = (double)args[1].Data!;
            string synthType = (string)args[2].Data!;
            int sampleRate = (int)args[3].Data!;
            double bpm = (double)args[4].Data!;

            var voices = Audio.BarRenderer.RenderBarAtTime(bar, timeSeconds, synthType, sampleRate, bpm);
            var voiceValues = voices.Select(v => Value.Voice(v)).ToArray();
            return Value.Array(voiceValues, VoiceType.Instance);
        });

        // ===== Pitch Conversion =====

        var noteToFrequencySignature = new FunctionSignature(
            "noteToFrequency",
            [NoteType.Instance]);
        registry.Register("noteToFrequency", noteToFrequencySignature, args =>
        {
            string noteStr = (string)args[0].Data!;
            var (noteName, octave, alteration) = NoteType.Parse(noteStr);
            double frequency = Audio.PitchConversion.NoteToFrequency(noteName, octave, alteration);
            return Value.Double(frequency);
        });

        // ===== Euclidean Rhythm =====

        var euclideanSignature = new FunctionSignature(
            "euclidean",
            [IntType.Instance, IntType.Instance, NoteType.Instance]);
        registry.Register("euclidean", euclideanSignature, args =>
        {
            int hits = (int)args[0].Data!;
            int steps = (int)args[1].Data!;
            string noteStr = (string)args[2].Data!;

            if (hits <= 0) throw new InvalidOperationException("euclidean: hits must be > 0");
            if (steps <= 0) throw new InvalidOperationException("euclidean: steps must be > 0");
            if (hits > steps) throw new InvalidOperationException("euclidean: hits must be <= steps");

            var (noteName, octave, alteration) = NoteType.Parse(noteStr);

            // Bjorklund algorithm for euclidean rhythm
            var pattern = Bjorklund(hits, steps);

            // Choose duration based on steps count
            var duration = steps switch
            {
                <= 4 => NoteValueType.Value.QUARTER,
                <= 8 => NoteValueType.Value.EIGHTH,
                <= 16 => NoteValueType.Value.SIXTEENTH,
                _ => NoteValueType.Value.THIRTYSECOND
            };

            var notes = new List<MusicalNoteData>();
            foreach (bool isHit in pattern)
            {
                if (isHit)
                    notes.Add(new MusicalNoteData(noteName, octave, alteration, (int)duration, isRest: false));
                else
                    notes.Add(new MusicalNoteData(' ', 0, 0, (int)duration, isRest: true));
            }

            var timeSig = new TimeSignatureData(4, 4);
            var bar = new BarData(notes, timeSig);
            var sequence = new SequenceData();
            sequence.AddBar(bar);
            return Value.Sequence(sequence);
        });
    }

    /// <summary>
    /// Bjorklund algorithm: distributes hits evenly across steps.
    /// </summary>
    private static bool[] Bjorklund(int hits, int steps)
    {
        if (hits >= steps)
            return Enumerable.Repeat(true, steps).ToArray();

        // Build groups using the Euclidean algorithm
        var groups = new List<List<bool>>();
        for (int i = 0; i < steps; i++)
            groups.Add(new List<bool> { i < hits });

        int splitPoint = hits;
        int remainder = steps - hits;

        while (remainder > 1)
        {
            int distribute = Math.Min(splitPoint, remainder);
            for (int i = 0; i < distribute; i++)
            {
                groups[i].AddRange(groups[groups.Count - 1]);
                groups.RemoveAt(groups.Count - 1);
            }
            remainder = groups.Count - (splitPoint < remainder ? splitPoint : distribute);
            splitPoint = distribute;
        }

        return groups.SelectMany(g => g).ToArray();
    }
}
