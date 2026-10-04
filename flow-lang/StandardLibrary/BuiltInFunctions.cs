using FlowLang.Audio;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Dict;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
using FlowLang.Music;

namespace FlowLang.StandardLibrary;

/// <summary>
/// Registers Flow built-in functions with their C# implementations.
/// Actual implementations are in StdLib.cs.
/// </summary>
public static class BuiltInFunctions
{
    // No more static _context !
    /// <summary>
    /// Registers iteration guard functions that need ExecutionContext.
    /// Must be called AFTER ExecutionContext is created (called from FlowEngine).
    /// </summary>
    public static void RegisterIterationGuard(InternalFunctionRegistry registry, FlowLang.Runtime.ExecutionContext context)
    {
        CoreLibrary.RegisterIterationGuard(registry, context);
    }

    /// <summary>
    /// Registers all C# implementations of internal functions.
    /// </summary>
    public static void RegisterAllImplementations(InternalFunctionRegistry registry)
    {
        RegisterStdLib(registry);
        RegisterMath(registry);
        UnitArithmetic.Register(registry);
        // Phase 44 Plan 44-04 (D-08/D-09/D-10) — explicit-conversion builtins:
        // 6 forward (db/hz/ms/sec/cents/semitones) + 4 reverse (double/float/int/long
        // × 6 music types) = 50 always-available registrations. Mode-independent
        // per D-09; composers can incrementally refactor toward strict one call
        // at a time. Mirrors the always-available doubleToInt / intToDouble
        // precedent registered in RegisterStdLib above.
        ConversionFunctions.Register(registry);
        RegisterCollections(registry);
        RegisterBars(registry);
        RegisterMusicalNotationFunctions(registry);
        Audio.EffectsFunctions.Register(registry);
        Audio.PanningFunctions.Register(registry);
        Audio.SongRenderer.Register(registry);
        Audio.TempoRampRenderer.Register(registry);
        Transforms.TransformFunctions.Register(registry);
        Transforms.TransformFunctions.RegisterArticulationTransforms(registry);  // Phase 22-06 DX-14 (legato + portamento)
        Harmony.HarmonyFunctions.Register(registry);
        VisualizationFunctions.Register(registry);
        // Phase 38 AUDIO-IN-01/02 — (micBuffer duration) capture. Moved to
        // RegisterContextDependentFunctions in Phase 44 Plan 44-07 to thread
        // ExecutionContext for strict-mode advisory elevation.
        // Phase 38 Plan 38-06 OSC-01/02 — Network.OscFunctions.Register
        // (oscSend / oscListen / oscStop / oscBundle / oscSendBundle +
        // __enableOscModule marker) is wired into FlowEngine.cs directly
        // because it needs ExecutionContext for the module-activation gate
        // and for invoking handler lambdas via context.Invoker
        // (mirror Phase 33 SfzBuiltins + Phase 39 NotationIoBuiltins pattern).
        // The 5 surface builtins gate on ExecutionContext.OscEnabled
        // (flipped true by `use "@osc"` import). PATTERNS line 832 reference.
        BufferPrinter.Register(registry);
        Composition.PolyrhythmFunctions.Register(registry);
        Composition.VariationFunctions.Register(registry);
        Audio.Vocalization.VocalizationFunctions.Register(registry);
    }

    /// <summary>
    /// Registers all C# implementations including playback functions that need an audio manager.
    /// </summary>
    public static void RegisterAllImplementations(InternalFunctionRegistry registry, AudioPlaybackManager audioManager)
    {
        RegisterAllImplementations(registry);
        RegisterAudio(registry, audioManager);
        Audio.PlaybackFunctions.Register(registry, audioManager);
    }

    /// <summary>
    /// Registers EVERY built-in's name + signature against a stub delegate. For the LSP
    /// (flow-lsp), which only introspects signatures for completion / hover / signature-help
    /// and NEVER invokes the delegate.
    ///
    /// Delivers CONTEXT D-07 "every built-in in InternalFunctionRegistry" in full — no
    /// audio-free carve-out — because it mirrors the signature side of every Register* method
    /// (core + audio core + effects + panning + harmony + transforms + visualization +
    /// composition + vocalization + playback). The AudioPlaybackManager used internally is
    /// never invoked (the stub replaces every real delegate), so this path does not load
    /// PulseAudio native libraries.
    ///
    /// Phase 17 (17-05). Do NOT call from flow-interpreter or FlowEngine — they use
    /// RegisterAllImplementations which supplies real delegates.
    /// </summary>
    public static void RegisterSignaturesOnly(InternalFunctionRegistry registry)
    {
        // Shared stub — every signature registered via this path gets the SAME delegate.
        // Invoking it is always a bug; the LSP only enumerates signatures via EnumerateSignatures.
        Func<IReadOnlyList<Value>, Value> stub = args =>
            throw new NotSupportedException(
                "signatures-only — the LSP does not execute built-ins. " +
                "Use RegisterAllImplementations(registry[, audioManager]) in flow-interpreter.");

        // Proxy forwards every Register call to the target registry but substitutes the
        // real delegate with the shared stub. This lets us reuse EVERY existing Register*
        // body without duplicating signature declarations — a single source of truth.
        var proxy = new StubbingRegistryProxy(registry, stub);

        // Audio-manager-free paths (RegisterAllImplementations no-arg).
        RegisterAllImplementations(proxy);

        // Manager-bound paths. The manager is constructed to satisfy the method signatures
        // of RegisterAudio/PlaybackFunctions.Register, but its real delegates are replaced
        // by the proxy's stub substitution before they reach `registry`. The manager is
        // never invoked and never loads a backend (PulseAudio p/invoke only fires on
        // manager.GetBackend(), which no stub ever calls).
        var dummyAudio = new AudioPlaybackManager();
        RegisterAudio(proxy, dummyAudio);
        Audio.PlaybackFunctions.Register(proxy, dummyAudio);

        // Context-dependent paths (map, filter, reduce, each, random, renderSong-with-lambda,
        // enharmonic, custom oscillator). These require an ExecutionContext to construct
        // their closures; we allocate one bound to the proxy — since the closures are never
        // invoked (the proxy replaces every delegate with the stub), the context is inert.
        var dummyReporter = new FlowLang.Diagnostics.ErrorReporter();
        var dummyContext = new FlowLang.Runtime.ExecutionContext(dummyReporter, proxy);
        RegisterContextDependentFunctions(proxy, dummyContext);
        RegisterIterationGuard(proxy, dummyContext);
        // quick-260702-ijs: play(Sequence)/stream(Sequence) moved into
        // PlaybackFunctions.RegisterContextDependent — enumerate them here too so the
        // LSP signature surface stays complete (BuiltInFunctionsTests "play"/"stream").
        Audio.PlaybackFunctions.RegisterContextDependent(proxy, dummyAudio, dummyContext);
    }

    /// <summary>
    /// Registry proxy used by <see cref="RegisterSignaturesOnly"/>. Overrides Register so
    /// every call forwards to the target registry with the impl replaced by a shared stub.
    /// Keeps signature declarations single-sourced in the existing Register* methods.
    /// </summary>
    private sealed class StubbingRegistryProxy : InternalFunctionRegistry
    {
        private readonly InternalFunctionRegistry _target;
        private readonly Func<IReadOnlyList<Value>, Value> _stub;

        public StubbingRegistryProxy(InternalFunctionRegistry target, Func<IReadOnlyList<Value>, Value> stub)
        {
            _target = target;
            _stub = stub;
        }

        public override void Register(string name, FunctionSignature signature, Func<IReadOnlyList<Value>, Value> implementation)
        {
            // Drop `implementation` on the floor — route (name, signature) to the target
            // with the stub instead. The original lambda is constructed and captured (closure
            // over audioManager, context, etc.) but never stored and never invoked.
            _target.Register(name, signature, _stub);
        }
    }

    private static void RegisterStdLib(InternalFunctionRegistry registry)
    {
        CoreLibrary.RegisterPrimitiveStrings(registry);

        var strNoteSignature = new FunctionSignature("str", [NoteType.Instance],
            ParameterNames: ["value"]);
        registry.Register("str", strNoteSignature, StdLib.StrNote);

        CoreLibrary.RegisterSymbolString(registry);

        var strBarSignature = new FunctionSignature("str", [BarType.Instance],
            ParameterNames: ["value"]);
        registry.Register("str", strBarSignature, StdLib.StrBar);

        var strSemitoneSignature = new FunctionSignature("str", [SemitoneType.Instance],
            ParameterNames: ["value"]);
        registry.Register("str", strSemitoneSignature, StdLib.StrSemitone);

        var strCentSignature = new FunctionSignature("str", [CentType.Instance],
            ParameterNames: ["value"]);
        registry.Register("str", strCentSignature, StdLib.StrCent);

        var strMillisecondSignature = new FunctionSignature("str", [MillisecondType.Instance],
            ParameterNames: ["value"]);
        registry.Register("str", strMillisecondSignature, StdLib.StrMillisecond);

        var strSecondSignature = new FunctionSignature("str", [SecondType.Instance],
            ParameterNames: ["value"]);
        registry.Register("str", strSecondSignature, StdLib.StrSecond);

        var strDecibelSignature = new FunctionSignature("str", [DecibelType.Instance],
            ParameterNames: ["value"]);
        registry.Register("str", strDecibelSignature, StdLib.StrDecibel);

        // sweep-0614: dedicated str(Hertz) overload — "Hz" suffix. Without it,
        // (str 440Hz) is ambiguous between str(Float)/str(Double) because
        // HertzType.IsCompatibleWith covers both at equal specificity (the exact
        // ambiguity class the str(Beat) overload below already handles). Mirrors
        // the AutoStr Hertz branch so (str 440Hz) -> "440Hz" matches (print 440Hz).
        var strHertzSignature = new FunctionSignature("str", [HertzType.Instance],
            ParameterNames: ["value"]);
        registry.Register("str", strHertzSignature, StdLib.StrHertz);

        // Phase 45 D-14: dedicated str(Beat) overload — plain double, no "b" suffix.
        // Without it, (str someBeat) is ambiguous between str(Float)/str(Double)
        // (BeatType.IsCompatibleWith covers both), which blocks the Nb literal's
        // composer-facing print path used by the Phase 45 smoke scripts + Facts.
        var strBeatSignature = new FunctionSignature("str", [BeatType.Instance],
            ParameterNames: ["value"]);
        registry.Register("str", strBeatSignature, StdLib.StrBeat);

        CoreLibrary.RegisterCollectionStrings(registry);

        var strSequenceSignature = new FunctionSignature("str", [SequenceType.Instance],
            ParameterNames: ["value"]);
        registry.Register("str", strSequenceSignature, args =>
        {
            var seq = args[0].As<SequenceData>();
            return Value.String(seq.ToString());
        });

        CoreLibrary.RegisterBasics(registry);

    }

    private static void RegisterMath(InternalFunctionRegistry registry)
    {
        CoreLibrary.RegisterMath(registry);
    }

    private static void RegisterCollections(InternalFunctionRegistry registry)
    {
        CoreLibrary.RegisterArrays(registry);

        var sliceSeqSignature = new FunctionSignature("slice",
            [SequenceType.Instance, IntType.Instance, IntType.Instance],
            ParameterNames: ["seq", "start", "end"]);
        registry.Register("slice", sliceSeqSignature, Collections.SliceSequence);

        CoreLibrary.RegisterArrayOperations(registry);

    }

    private static void RegisterAudio(InternalFunctionRegistry registry, AudioPlaybackManager audioManager)
    {
        // ===== Core Buffer Operations =====

        var createBufferSignature = new FunctionSignature(
            "createBuffer",
            [IntType.Instance, IntType.Instance, IntType.Instance],
            ParameterNames: ["frames", "channels", "sampleRate"]);
        registry.Register("createBuffer", createBufferSignature, Audio.AudioCore.CreateBuffer);

        var getFramesSignature = new FunctionSignature("getFrames", [BufferType.Instance],
            ParameterNames: ["buf"]);
        registry.Register("getFrames", getFramesSignature, Audio.AudioCore.GetFrames);

        var getChannelsSignature = new FunctionSignature("getChannels", [BufferType.Instance],
            ParameterNames: ["buf"]);
        registry.Register("getChannels", getChannelsSignature, Audio.AudioCore.GetChannels);

        var getSampleRateSignature = new FunctionSignature("getSampleRate", [BufferType.Instance],
            ParameterNames: ["buf"]);
        registry.Register("getSampleRate", getSampleRateSignature, Audio.AudioCore.GetSampleRate);

        var getSampleSignature = new FunctionSignature(
            "getSample",
            [BufferType.Instance, IntType.Instance, IntType.Instance],
            ParameterNames: ["buf", "frame", "channel"]);
        registry.Register("getSample", getSampleSignature, Audio.AudioCore.GetSample);

        var setSampleSignature = new FunctionSignature(
            "setSample",
            [BufferType.Instance, IntType.Instance, IntType.Instance, DoubleType.Instance],
            ParameterNames: ["buf", "frame", "channel", "value"]);
        registry.Register("setSample", setSampleSignature, Audio.AudioCore.SetSample);

        var fillBufferSignature = new FunctionSignature(
            "fillBuffer",
            [BufferType.Instance, DoubleType.Instance],
            ParameterNames: ["buf", "value"]);
        registry.Register("fillBuffer", fillBufferSignature, Audio.AudioCore.FillBuffer);

        var mixBuffersSignature = new FunctionSignature(
            "mixBuffers",
            [BufferType.Instance, BufferType.Instance, DoubleType.Instance, DoubleType.Instance],
            ParameterNames: ["a", "b", "gainA", "gainB"]);
        registry.Register("mixBuffers", mixBuffersSignature, Audio.AudioCore.MixBuffers);

        // Quick 260701-vqz: decibel-typed gains. (mixBuffers a b -3dB -3dB) previously
        // coerced the raw dB numbers into the linear slots (-3.0 linear = 3x AND
        // phase-inverted). dB converts to a linear multiplier here (10^(dB/20)).
        var mixBuffersDbSignature = new FunctionSignature(
            "mixBuffers",
            [BufferType.Instance, BufferType.Instance, DecibelType.Instance, DecibelType.Instance],
            ParameterNames: ["a", "b", "gainA", "gainB"]);
        registry.Register("mixBuffers", mixBuffersDbSignature, args => Audio.AudioCore.MixBuffers([
            args[0], args[1],
            Value.Double(Math.Pow(10.0, args[2].As<double>() / 20.0)),
            Value.Double(Math.Pow(10.0, args[3].As<double>() / 20.0))]));

        var mixSignature = new FunctionSignature("mix", [BufferType.Instance, BufferType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("mix", mixSignature, Audio.AudioCore.Mix);

        // ===== File I/O Operations =====

        // writeWav(String, Buffer) - primary name, path-first arg order (matches writeMidi)
        var writeWavSignature = new FunctionSignature(
            "writeWav",
            [StringType.Instance, BufferType.Instance],
            ParameterNames: ["path", "buf"]);
        registry.Register("writeWav", writeWavSignature, Audio.FileIO.WriteWav);

        // writeWav(String, Buffer, Int) - with bit depth
        var writeWavWithDepthSignature = new FunctionSignature(
            "writeWav",
            [StringType.Instance, BufferType.Instance, IntType.Instance],
            ParameterNames: ["path", "buf", "bitDepth"]);
        registry.Register("writeWav", writeWavWithDepthSignature, Audio.FileIO.WriteWavWithBitDepth);

        // play-song (#9): writeWav(String, Song) — convenience export. Renders the
        // arrangement with per-sequence instrument routing, then writes the mix.
        var writeWavSongSignature = new FunctionSignature(
            "writeWav",
            [StringType.Instance, SongType.Instance],
            ParameterNames: ["path", "song"]);
        registry.Register("writeWav", writeWavSongSignature, Audio.FileIO.WriteWavSong);

        // play-song (#9): writeWav(String, Song, String) — forces ONE synth for all.
        var writeWavSongSynthSignature = new FunctionSignature(
            "writeWav",
            [StringType.Instance, SongType.Instance, StringType.Instance],
            ParameterNames: ["path", "song", "synthType"]);
        registry.Register("writeWav", writeWavSongSynthSignature, Audio.FileIO.WriteWavSongWithSynth);

        // loadWav(String) -> Buffer - load WAV file
        var loadWavSignature = new FunctionSignature("loadWav", [StringType.Instance],
            ParameterNames: ["path"]);
        registry.Register("loadWav", loadWavSignature, Audio.FileIO.LoadWav);

        // DX-15: loadWav(String, Int) -> Buffer — varispeed by semitones (Phase 22 plan 22-02)
        var loadWavSemiSig = new FunctionSignature("loadWav",
            [StringType.Instance, IntType.Instance],
            ParameterNames: ["path", "semitones"]);
        registry.Register("loadWav", loadWavSemiSig, Audio.FileIO.LoadWavSemitones);

        // sweep-260620 soft-overload: loadWav(String, Semitone) — SemitoneType backing IS int
        // (whole-numbers-by-design), so the existing LoadWavSemitones lambda reads args[1].As<int>()
        // directly, exactly as the loadWav(String, Int) path does.
        var loadWavSemitoneSig = new FunctionSignature("loadWav",
            [StringType.Instance, SemitoneType.Instance],
            ParameterNames: ["path", "semitones"]);
        registry.Register("loadWav", loadWavSemitoneSig, Audio.FileIO.LoadWavSemitones);

        // DX-15: loadWav(String, Double) -> Buffer — varispeed by ratio (Phase 22 plan 22-02)
        var loadWavRatioSig = new FunctionSignature("loadWav",
            [StringType.Instance, DoubleType.Instance],
            ParameterNames: ["path", "ratio"]);
        registry.Register("loadWav", loadWavRatioSig, Audio.FileIO.LoadWavRatio);

        // writeMidi(String, Song) -> Void migrated to RegisterContextDependentFunctions
        // (Phase 23 Plan 23-03 Task 2). The context-dependent registration lets writeMidi
        // read MusicalContext.Tuning and emit the D-13 advisory warning under non-12-TET.
        // MIDI bytes are unchanged — still 12-TET — so the migration is non-breaking.

        // ===== Signal Generation Operations =====

        var createOscillatorStateSignature = new FunctionSignature(
            "createOscillatorState",
            [DoubleType.Instance, IntType.Instance],
            ParameterNames: ["frequency", "sampleRate"]);
        registry.Register("createOscillatorState", createOscillatorStateSignature, Audio.SignalGeneration.CreateOscillatorState);

        // sweep-260620 soft-overload: createOscillatorState(Hertz, Int) — Hertz CLR backing IS
        // double, so the existing CreateOscillatorState lambda reads args[0].As<double>() directly.
        var createOscillatorStateHzSig = new FunctionSignature(
            "createOscillatorState",
            [HertzType.Instance, IntType.Instance],
            ParameterNames: ["frequency", "sampleRate"]);
        registry.Register("createOscillatorState", createOscillatorStateHzSig, Audio.SignalGeneration.CreateOscillatorState);

        var createSineToneSig = new FunctionSignature("createSineTone", [DoubleType.Instance, DoubleType.Instance, DoubleType.Instance],
            ParameterNames: ["duration", "frequency", "amplitude"]);
        registry.Register("createSineTone", createSineToneSig, Audio.SignalGeneration.CreateSineTone);

        // Phase 26.2 ERG-04: createSineTone(Double, Hertz, Double) — explicit frequency-type ergonomics.
        // Delegates to the same CreateSineTone lambda; Hertz's CLR backing IS double
        // (Value.Hertz factory wraps a double), so args[1].As<double>() reads it
        // directly without per-overload coercion.
        var createSineToneHzSig = new FunctionSignature("createSineTone", [DoubleType.Instance, HertzType.Instance, DoubleType.Instance],
            ParameterNames: ["duration", "frequency", "amplitude"]);
        registry.Register("createSineTone", createSineToneHzSig, Audio.SignalGeneration.CreateSineTone);

        // 2026-06-10 hello-world fix: frequency-FIRST Hertz overload. The documented
        // canonical call is (createSineTone 440Hz 1.0 0.5) — README, CLAUDE.md music-types
        // table, the playground hello snippet — but only duration-first overloads existed,
        // so 440Hz coerced into the DURATION slot (Hertz.IsCompatibleWith(Double), +500
        // compatible tier) and the hello-world silently built 440 SECONDS of an inaudible
        // 1 Hz wave. The exact Hertz match in slot 0 (+1000) makes this overload win every
        // Hertz-first call; both duration-first forms are unchanged. The lambda reorders
        // into the positional (duration, frequency, amplitude) shape CreateSineTone reads.
        var createSineToneHzFirstSig = new FunctionSignature("createSineTone", [HertzType.Instance, DoubleType.Instance, DoubleType.Instance],
            ParameterNames: ["frequency", "duration", "amplitude"]);
        registry.Register("createSineTone", createSineToneHzFirstSig,
            args => Audio.SignalGeneration.CreateSineTone([args[1], args[0], args[2]]));

        // sweep-260620 soft-overload: duration-first Second-typed forms. Second's CLR backing
        // IS double and the duration sits in slot 0 (the positional shape CreateSineTone reads),
        // so both reuse the same lambda with no reorder.
        var createSineToneSecSig = new FunctionSignature("createSineTone",
            [SecondType.Instance, DoubleType.Instance, DoubleType.Instance],
            ParameterNames: ["duration", "frequency", "amplitude"]);
        registry.Register("createSineTone", createSineToneSecSig, Audio.SignalGeneration.CreateSineTone);

        var createSineToneSecHzSig = new FunctionSignature("createSineTone",
            [SecondType.Instance, HertzType.Instance, DoubleType.Instance],
            ParameterNames: ["duration", "frequency", "amplitude"]);
        registry.Register("createSineTone", createSineToneSecHzSig, Audio.SignalGeneration.CreateSineTone);

        var createClipSig = new FunctionSignature("createClip", [DoubleType.Instance, DoubleType.Instance],
            ParameterNames: ["duration", "amplitude"]);
        registry.Register("createClip", createClipSig, Audio.SignalGeneration.CreateClip);

        // sweep-260620 soft-overload: createClip(Second, Double) — Second backing IS double,
        // reuses the same CreateClip lambda (duration in slot 0).
        var createClipSecSig = new FunctionSignature("createClip", [SecondType.Instance, DoubleType.Instance],
            ParameterNames: ["duration", "amplitude"]);
        registry.Register("createClip", createClipSecSig, Audio.SignalGeneration.CreateClip);

        // White noise -- wraps SynthUtils.GenerateWhiteNoise. Four arities; resolver disambiguates by arg count.
        var noise1Sig = new FunctionSignature("noise", [DoubleType.Instance],
            ParameterNames: ["seconds"]);
        registry.Register("noise", noise1Sig, Audio.SignalGeneration.Noise1);

        var noise2Sig = new FunctionSignature("noise", [DoubleType.Instance, DoubleType.Instance],
            ParameterNames: ["seconds", "amplitude"]);
        registry.Register("noise", noise2Sig, Audio.SignalGeneration.Noise2);

        var noise3Sig = new FunctionSignature("noise", [DoubleType.Instance, DoubleType.Instance, IntType.Instance],
            ParameterNames: ["seconds", "amplitude", "channels"]);
        registry.Register("noise", noise3Sig, Audio.SignalGeneration.Noise3);

        var noise4Sig = new FunctionSignature("noise", [DoubleType.Instance, DoubleType.Instance, IntType.Instance, IntType.Instance],
            ParameterNames: ["seconds", "amplitude", "channels", "sampleRate"]);
        registry.Register("noise", noise4Sig, Audio.SignalGeneration.Noise);

        // sweep-260620 soft-overload: Second-in-seconds-slot variants for all 4 noise arities.
        // Second backing IS double → each reuses the matching Noise lambda directly.
        var noise1SecSig = new FunctionSignature("noise", [SecondType.Instance],
            ParameterNames: ["seconds"]);
        registry.Register("noise", noise1SecSig, Audio.SignalGeneration.Noise1);

        var noise2SecSig = new FunctionSignature("noise", [SecondType.Instance, DoubleType.Instance],
            ParameterNames: ["seconds", "amplitude"]);
        registry.Register("noise", noise2SecSig, Audio.SignalGeneration.Noise2);

        var noise3SecSig = new FunctionSignature("noise", [SecondType.Instance, DoubleType.Instance, IntType.Instance],
            ParameterNames: ["seconds", "amplitude", "channels"]);
        registry.Register("noise", noise3SecSig, Audio.SignalGeneration.Noise3);

        var noise4SecSig = new FunctionSignature("noise", [SecondType.Instance, DoubleType.Instance, IntType.Instance, IntType.Instance],
            ParameterNames: ["seconds", "amplitude", "channels", "sampleRate"]);
        registry.Register("noise", noise4SecSig, Audio.SignalGeneration.Noise);

        var resetPhaseSignature = new FunctionSignature(
            "resetPhase",
            [OscillatorStateType.Instance],
            ParameterNames: ["state"]);
        registry.Register("resetPhase", resetPhaseSignature, Audio.SignalGeneration.ResetPhase);

        var generateSineSignature = new FunctionSignature(
            "generateSine",
            [BufferType.Instance, OscillatorStateType.Instance, DoubleType.Instance],
            ParameterNames: ["buf", "state", "amplitude"]);
        registry.Register("generateSine", generateSineSignature, Audio.SignalGeneration.GenerateSine);

        var generateSawSignature = new FunctionSignature(
            "generateSaw",
            [BufferType.Instance, OscillatorStateType.Instance, DoubleType.Instance],
            ParameterNames: ["buf", "state", "amplitude"]);
        registry.Register("generateSaw", generateSawSignature, Audio.SignalGeneration.GenerateSaw);

        var generateSquareSignature = new FunctionSignature(
            "generateSquare",
            [BufferType.Instance, OscillatorStateType.Instance, DoubleType.Instance],
            ParameterNames: ["buf", "state", "amplitude"]);
        registry.Register("generateSquare", generateSquareSignature, Audio.SignalGeneration.GenerateSquare);

        var generateTriangleSignature = new FunctionSignature(
            "generateTriangle",
            [BufferType.Instance, OscillatorStateType.Instance, DoubleType.Instance],
            ParameterNames: ["buf", "state", "amplitude"]);
        registry.Register("generateTriangle", generateTriangleSignature, Audio.SignalGeneration.GenerateTriangle);

        // ===== Buffer Helper Operations =====

        var copyBufferSignature = new FunctionSignature(
            "copyBuffer",
            [BufferType.Instance],
            ParameterNames: ["buf"]);
        registry.Register("copyBuffer", copyBufferSignature, Audio.BufferHelpers.CopyBuffer);

        var sliceBufferSignature = new FunctionSignature(
            "sliceBuffer",
            [BufferType.Instance, IntType.Instance, IntType.Instance],
            ParameterNames: ["buf", "start", "end"]);
        registry.Register("sliceBuffer", sliceBufferSignature, Audio.BufferHelpers.SliceBuffer);

        var appendBuffersSignature = new FunctionSignature(
            "appendBuffers",
            [BufferType.Instance, BufferType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("appendBuffers", appendBuffersSignature, Audio.BufferHelpers.AppendBuffers);

        var scaleBufferSignature = new FunctionSignature(
            "scaleBuffer",
            [BufferType.Instance, DoubleType.Instance],
            ParameterNames: ["buf", "factor"]);
        registry.Register("scaleBuffer", scaleBufferSignature, Audio.BufferHelpers.ScaleBuffer);

        // Quick 260701-vqz: decibel-typed factor — (scaleBuffer buf -6dB) previously
        // coerced raw -6.0 into the linear slot (phase-inverted 6x). Same 10^(dB/20)
        // conversion as the volume/mixBuffers Decibel overloads.
        var scaleBufferDbSignature = new FunctionSignature(
            "scaleBuffer",
            [BufferType.Instance, DecibelType.Instance],
            ParameterNames: ["buf", "factor"]);
        registry.Register("scaleBuffer", scaleBufferDbSignature, args => Audio.BufferHelpers.ScaleBuffer([
            args[0], Value.Double(Math.Pow(10.0, args[1].As<double>() / 20.0))]));

        var fadeInSignature = new FunctionSignature(
            "fadeIn",
            [BufferType.Instance, DoubleType.Instance],
            ParameterNames: ["buf", "duration"]);
        registry.Register("fadeIn", fadeInSignature, Audio.BufferHelpers.FadeIn);

        // sweep-260620 soft-overload: fadeIn(Buffer, Second) — Second backing IS double, reuses FadeIn.
        var fadeInSecSignature = new FunctionSignature(
            "fadeIn",
            [BufferType.Instance, SecondType.Instance],
            ParameterNames: ["buf", "duration"]);
        registry.Register("fadeIn", fadeInSecSignature, Audio.BufferHelpers.FadeIn);

        var fadeOutSignature = new FunctionSignature(
            "fadeOut",
            [BufferType.Instance, DoubleType.Instance],
            ParameterNames: ["buf", "duration"]);
        registry.Register("fadeOut", fadeOutSignature, Audio.BufferHelpers.FadeOut);

        // sweep-260620 soft-overload: fadeOut(Buffer, Second) — Second backing IS double, reuses FadeOut.
        var fadeOutSecSignature = new FunctionSignature(
            "fadeOut",
            [BufferType.Instance, SecondType.Instance],
            ParameterNames: ["buf", "duration"]);
        registry.Register("fadeOut", fadeOutSecSignature, Audio.BufferHelpers.FadeOut);

        // ===== Envelope Operations =====

        var createARSignature = new FunctionSignature(
            "createAR",
            [DoubleType.Instance, DoubleType.Instance, IntType.Instance],
            ParameterNames: ["attack", "release", "sampleRate"]);
        registry.Register("createAR", createARSignature, Audio.EnvelopeProcessor.CreateAR);

        // sweep-260620 soft-overload: createAR(Second, Second, Int) — both attack/release are
        // time slots; Second backing IS double, so the existing CreateAR lambda reads them directly.
        var createARSecSignature = new FunctionSignature(
            "createAR",
            [SecondType.Instance, SecondType.Instance, IntType.Instance],
            ParameterNames: ["attack", "release", "sampleRate"]);
        registry.Register("createAR", createARSecSignature, Audio.EnvelopeProcessor.CreateAR);

        var createADSRSignature = new FunctionSignature(
            "createADSR",
            [DoubleType.Instance, DoubleType.Instance, DoubleType.Instance, DoubleType.Instance, IntType.Instance],
            ParameterNames: ["attack", "decay", "sustain", "release", "sampleRate"]);
        registry.Register("createADSR", createADSRSignature, Audio.EnvelopeProcessor.CreateADSR);

        var applyEnvelopeSignature = new FunctionSignature(
            "applyEnvelope",
            [BufferType.Instance, EnvelopeType.Instance],
            ParameterNames: ["buf", "env"]);
        registry.Register("applyEnvelope", applyEnvelopeSignature, Audio.EnvelopeProcessor.ApplyEnvelope);

        // ===== Timeline Operations =====

        var setBPMSignature = new FunctionSignature(
            "setBPM",
            [DoubleType.Instance],
            ParameterNames: ["bpm"]);
        registry.Register("setBPM", setBPMSignature, Audio.Timeline.SetBPM);

        var getBPMSignature = new FunctionSignature("getBPM", [],
            ParameterNames: []);
        registry.Register("getBPM", getBPMSignature, Audio.Timeline.GetBPM);

        var beatsToFramesSignature = new FunctionSignature(
            "beatsToFrames",
            [DoubleType.Instance, IntType.Instance],
            ParameterNames: ["beats", "sampleRate"]);
        registry.Register("beatsToFrames", beatsToFramesSignature, Audio.Timeline.BeatsToFrames);

        // sweep-260620 soft-overload: beatsToFrames(Beat, Int). CRITICAL — reads the RAW
        // As<double>() beat count with NO beat-true-to-sig 4/denom multiplier (storage is
        // already quarter-relative). Reuses the existing BeatsToFrames lambda directly.
        var beatsToFramesBeatSignature = new FunctionSignature(
            "beatsToFrames",
            [BeatType.Instance, IntType.Instance],
            ParameterNames: ["beats", "sampleRate"]);
        registry.Register("beatsToFrames", beatsToFramesBeatSignature, Audio.Timeline.BeatsToFrames);

        var framesToBeatsSignature = new FunctionSignature(
            "framesToBeats",
            [IntType.Instance, IntType.Instance],
            ParameterNames: ["frames", "sampleRate"]);
        registry.Register("framesToBeats", framesToBeatsSignature, Audio.Timeline.FramesToBeats);

        var createVoiceSignature = new FunctionSignature(
            "createVoice",
            [BufferType.Instance, DoubleType.Instance],
            ParameterNames: ["buf", "offset"]);
        registry.Register("createVoice", createVoiceSignature, Audio.Timeline.CreateVoice);

        // sweep-260620 soft-overload: createVoice(Buffer, Beat). CRITICAL — reads the RAW
        // As<double>() beat offset with NO timesig multiplier (quarter-relative storage).
        // Reuses the existing CreateVoice lambda directly.
        var createVoiceBeatSignature = new FunctionSignature(
            "createVoice",
            [BufferType.Instance, BeatType.Instance],
            ParameterNames: ["buf", "offset"]);
        registry.Register("createVoice", createVoiceBeatSignature, Audio.Timeline.CreateVoice);

        var setVoiceGainSignature = new FunctionSignature(
            "setVoiceGain",
            [VoiceType.Instance, DoubleType.Instance],
            ParameterNames: ["voice", "gain"]);
        registry.Register("setVoiceGain", setVoiceGainSignature, Audio.Timeline.SetVoiceGain);

        var setVoicePanSignature = new FunctionSignature(
            "setVoicePan",
            [VoiceType.Instance, DoubleType.Instance],
            ParameterNames: ["voice", "pan"]);
        registry.Register("setVoicePan", setVoicePanSignature, Audio.Timeline.SetVoicePan);

        var setVoiceOffsetSignature = new FunctionSignature(
            "setVoiceOffset",
            [VoiceType.Instance, DoubleType.Instance],
            ParameterNames: ["voice", "offset"]);
        registry.Register("setVoiceOffset", setVoiceOffsetSignature, Audio.Timeline.SetVoiceOffset);

        // sweep-260620 soft-overload: setVoiceOffset(Voice, Beat). CRITICAL — RAW As<double>()
        // beat offset, NO timesig multiplier. Reuses the existing SetVoiceOffset lambda directly.
        var setVoiceOffsetBeatSignature = new FunctionSignature(
            "setVoiceOffset",
            [VoiceType.Instance, BeatType.Instance],
            ParameterNames: ["voice", "offset"]);
        registry.Register("setVoiceOffset", setVoiceOffsetBeatSignature, Audio.Timeline.SetVoiceOffset);

        var createTrackSignature = new FunctionSignature(
            "createTrack",
            [IntType.Instance, IntType.Instance],
            ParameterNames: ["channels", "sampleRate"]);
        registry.Register("createTrack", createTrackSignature, Audio.Timeline.CreateTrack);

        var addVoiceSignature = new FunctionSignature(
            "addVoice",
            [TrackType.Instance, VoiceType.Instance],
            ParameterNames: ["track", "voice"]);
        registry.Register("addVoice", addVoiceSignature, Audio.Timeline.AddVoice);

        var setTrackOffsetSignature = new FunctionSignature(
            "setTrackOffset",
            [TrackType.Instance, DoubleType.Instance],
            ParameterNames: ["track", "offset"]);
        registry.Register("setTrackOffset", setTrackOffsetSignature, Audio.Timeline.SetTrackOffset);

        // sweep-260620 soft-overload: setTrackOffset(Track, Beat). CRITICAL — RAW As<double>()
        // beat offset, NO timesig multiplier. Reuses the existing SetTrackOffset lambda directly.
        var setTrackOffsetBeatSignature = new FunctionSignature(
            "setTrackOffset",
            [TrackType.Instance, BeatType.Instance],
            ParameterNames: ["track", "offset"]);
        registry.Register("setTrackOffset", setTrackOffsetBeatSignature, Audio.Timeline.SetTrackOffset);

        var setTrackGainSignature = new FunctionSignature(
            "setTrackGain",
            [TrackType.Instance, DoubleType.Instance],
            ParameterNames: ["track", "gain"]);
        registry.Register("setTrackGain", setTrackGainSignature, Audio.Timeline.SetTrackGain);

        var setTrackPanSignature = new FunctionSignature(
            "setTrackPan",
            [TrackType.Instance, DoubleType.Instance],
            ParameterNames: ["track", "pan"]);
        registry.Register("setTrackPan", setTrackPanSignature, Audio.Timeline.SetTrackPan);

        var renderTrackSignature = new FunctionSignature(
            "renderTrack",
            [TrackType.Instance, DoubleType.Instance],
            ParameterNames: ["track", "duration"]);
        registry.Register("renderTrack", renderTrackSignature, Audio.Timeline.RenderTrack);

        // sweep-260620 soft-overload: renderTrack(Track, Beat). CRITICAL — RAW As<double>()
        // duration-in-beats, NO timesig multiplier. Reuses the existing RenderTrack lambda directly.
        var renderTrackBeatSignature = new FunctionSignature(
            "renderTrack",
            [TrackType.Instance, BeatType.Instance],
            ParameterNames: ["track", "duration"]);
        registry.Register("renderTrack", renderTrackBeatSignature, Audio.Timeline.RenderTrack);

        // ===== Voice Allocation =====

        var setMaxVoicesSignature = new FunctionSignature("setMaxVoices", [IntType.Instance],
            ParameterNames: ["max"]);
        registry.Register("setMaxVoices", setMaxVoicesSignature, args =>
        {
            int maxVoices = args[0].As<int>();
            if (maxVoices < 1)
                throw new InvalidOperationException("maxVoices must be at least 1");
            audioManager.MaxVoices = maxVoices;
            return Value.Void();
        });

        // ===== Custom Oscillator Registration =====

        // (Moved oscillator higher-order functions to RegisterContextDependentFunctions)

        // oscillator(String, Void[]) - register custom wavetable from pre-built array
        var oscillatorArraySignature = new FunctionSignature("oscillator", [StringType.Instance, new ArrayType(VoidType.Instance)],
            ParameterNames: ["name", "wavetable"]);
        registry.Register("oscillator", oscillatorArraySignature, args =>
        {
            string name = args[0].As<string>();
            var floatArray = args[1].As<IReadOnlyList<Value>>();
            if (floatArray.Count == 0)
                throw new InvalidOperationException("oscillator: wavetable array must not be empty");
            Audio.SynthesizerFactory.RegisterWavetable(name, ExtractWavetable(floatArray));
            return Value.Void();
        });
    }

    internal static float[] ExtractWavetable(IReadOnlyList<Value> floatArray)
    {
        float[] wavetable = new float[floatArray.Count];
        for (int i = 0; i < floatArray.Count; i++)
        {
            var val = floatArray[i].Data;
            wavetable[i] = val is double d ? (float)d : val is float f ? f : val is int intVal ? (float)intVal : Convert.ToSingle(val);
        }
        return wavetable;
    }

    /// <summary>
    /// Registers standard library functions that require the ExecutionContext.
    /// </summary>
    public static void RegisterContextDependentFunctions(InternalFunctionRegistry registry, FlowLang.Runtime.ExecutionContext context)
    {
        Audio.SongRenderer.RegisterContextDependent(registry, context);
        Composition.SongFunctions.Register(registry, context);
        Harmony.HarmonyFunctions.RegisterContextDependent(registry, context);
        RegisterEuclideanOverloads(registry, context);  // Phase 15 DX-09 (swing/humanize/seed)
        Audio.EffectsFunctions.RegisterContextDependent(registry, context);  // Phase 22-04 DX-12 (NoteValue-rate delay synced to MusicalContext.Tempo)
        Audio.BeatConversionFunctions.RegisterContextDependent(registry, context);  // Phase 43 D-08 — beatToSec + secToBeat tempo-aware conversion (closes AUDIT.md §1 Beat-orphan anchor)
        Audio.BeatConstructorFunctions.RegisterContextDependent(registry, context);  // Phase 45 D-05 — pragma-aware (beat N) constructor
        Transforms.TransformFunctions.RegisterContextDependent(registry, context);  // Phase 22-05 DX-13 (quantize reads MusicalContext.TimeSignature)
        Audio.Vocalization.VocalizationFunctions.RegisterContextDependent(registry, context);  // Phase 23-02 Task 3 (sing reads MusicalContext.Tuning via SongRenderer.ResolveRenderTuning)
        Audio.MidiExport.RegisterContextDependent(registry, context);  // Phase 23-03 Task 2 D-13 (writeMidi reads MusicalContext.Tuning for non-12-TET advisory)
#if !FLOW_WEB
        // Phase 47 D-47-08: Audio/InputFunctions.cs is csproj-stripped on Web
        // (per Plan 47-01) because it depends on PulseAudioCaptureBackend.cs
        // which is itself a P/Invoke target unavailable in browser sandbox.
        // (micBuffer) is composer-invoked, so Web-target composers see a
        // "Function not found" error if they call it; the v1.6 backlog item
        // is to add a getUserMedia-backed WebMicBuffer surface here.
        Audio.InputFunctions.RegisterContextDependent(registry, context);  // Phase 44 Plan 44-07 — strict-mode advisory elevation
#endif
        // Phase 35 Plan 35-04 TEST-01 — (test "name" body) defers via Lazy<Void>
        // wrap (Pitfall 10 LOAD-BEARING) + 5 assertion primitives. Context-
        // dependent because (test ...) appends a TestRecord to
        // context.TestRegistry; assertions throw AssertionException which
        // TestRunner catches to convert into FAIL outcomes.
        TestFramework.TestFunctions.RegisterTestFramework(registry, context);
        CoreLibrary.RegisterCallbacks(registry, context);

        // ===== Custom Oscillator Registration (Higher Order) =====

        var oscillatorSignature = new FunctionSignature("oscillator", [StringType.Instance, FunctionType.Instance],
            ParameterNames: ["name", "fn"]);
        registry.Register("oscillator", oscillatorSignature, args =>
        {
            string name = args[0].As<string>();
            var proc = args[1].As<FunctionOverload>();
            int tableSize = 2048;
            var result = proc.IsInternal ? proc.Implementation!(new List<Value> { Value.Int(tableSize) }) : context.Invoker!.ExecuteUserFunctionWithCaptures(proc.Declaration!, new List<Value> { Value.Int(tableSize) }, proc.CapturedVariables);
            var floatArray = result.As<IReadOnlyList<Value>>();
            Audio.SynthesizerFactory.RegisterWavetable(name, ExtractWavetable(floatArray));
            return Value.Void();
        });

        var oscillatorWithSizeSignature = new FunctionSignature("oscillator", [StringType.Instance, FunctionType.Instance, IntType.Instance],
            ParameterNames: ["name", "fn", "tableSize"]);
        registry.Register("oscillator", oscillatorWithSizeSignature, args =>
        {
            string name = args[0].As<string>();
            var proc = args[1].As<FunctionOverload>();
            int tableSize = args[2].As<int>();
            if (tableSize < 64) tableSize = 64;
            var result = proc.IsInternal ? proc.Implementation!(new List<Value> { Value.Int(tableSize) }) : context.Invoker!.ExecuteUserFunctionWithCaptures(proc.Declaration!, new List<Value> { Value.Int(tableSize) }, proc.CapturedVariables);
            var floatArray = result.As<IReadOnlyList<Value>>();
            Audio.SynthesizerFactory.RegisterWavetable(name, ExtractWavetable(floatArray));
            return Value.Void();
        });

        CoreLibrary.RegisterContextFunctions(registry, context);

    }

    internal static void RegisterBars(InternalFunctionRegistry registry)
    {
        // ===== Bar Operations =====

        var createBarSignature = new FunctionSignature("createBar", [],
            ParameterNames: []);
        registry.Register("createBar", createBarSignature, Bars.CreateBar);

        var createBarWithNoteSignature = new FunctionSignature(
            "createBarWithNote",
            [NoteType.Instance],
            ParameterNames: ["note"]);
        registry.Register("createBarWithNote", createBarWithNoteSignature, Bars.CreateBarWithNote);

        var createBarFromNotesSignature = new FunctionSignature(
            "createBarFromNotes",
            [new ArrayType(NoteType.Instance)],
            ParameterNames: ["notes"]);
        registry.Register("createBarFromNotes", createBarFromNotesSignature, Bars.CreateBarFromNotes);

        var addNoteToBarSignature = new FunctionSignature(
            "addNoteToBar",
            [BarType.Instance, NoteType.Instance],
            ParameterNames: ["bar", "note"]);
        registry.Register("addNoteToBar", addNoteToBarSignature, Bars.AddNoteToBar);

        var getNoteFromBarSignature = new FunctionSignature(
            "getNoteFromBar",
            [BarType.Instance, IntType.Instance],
            ParameterNames: ["bar", "index"]);
        registry.Register("getNoteFromBar", getNoteFromBarSignature, Bars.GetNoteFromBar);

        var barLengthSignature = new FunctionSignature("barLength", [BarType.Instance],
            ParameterNames: ["bar"]);
        registry.Register("barLength", barLengthSignature, Bars.BarLength);

        var setTimeSignatureSignature = new FunctionSignature(
            "setTimeSignature",
            [BarType.Instance, IntType.Instance, IntType.Instance],
            ParameterNames: ["bar", "numerator", "denominator"]);
        registry.Register("setTimeSignature", setTimeSignatureSignature, Bars.SetTimeSignature);

        var getTimeSignatureSignature = new FunctionSignature(
            "getTimeSignature",
            [BarType.Instance],
            ParameterNames: ["bar"]);
        registry.Register("getTimeSignature", getTimeSignatureSignature, Bars.GetTimeSignature);
    }

    internal static void RegisterMusicalNotationFunctions(InternalFunctionRegistry registry)
    {
        // ===== Musical Note Creation =====

        var createMusicalNoteSignature = new FunctionSignature(
            "createMusicalNote",
            [NoteType.Instance, NoteValueType.Instance],
            ParameterNames: ["pitch", "duration"]);
        registry.Register("createMusicalNote", createMusicalNoteSignature, args =>
        {
            string pitchStr = (string)args[0].Data!;
            int durationValue = (int)args[1].Data!;
            var note = Audio.ClassicalComposition.CreateMusicalNote(pitchStr, durationValue);
            return MusicValue.MusicalNote(note);
        });

        var createRestSignature = new FunctionSignature(
            "createRest",
            [NoteValueType.Instance],
            ParameterNames: ["duration"]);
        registry.Register("createRest", createRestSignature, args =>
        {
            int durationValue = (int)args[0].Data!;
            var rest = Audio.ClassicalComposition.CreateRest(durationValue);
            return MusicValue.MusicalNote(rest);
        });

        // ===== Time Signature =====

        var createTimeSignatureSignature = new FunctionSignature(
            "createTimeSignature",
            [IntType.Instance, IntType.Instance],
            ParameterNames: ["numerator", "denominator"]);
        registry.Register("createTimeSignature", createTimeSignatureSignature, args =>
        {
            int numerator = (int)args[0].Data!;
            int denominator = (int)args[1].Data!;
            var timeSig = Audio.ClassicalComposition.CreateTimeSignature(numerator, denominator);
            return MusicValue.TimeSignature(timeSig);
        });

        // ===== Musical Bar Creation =====

        var createMusicalBarSignature = new FunctionSignature(
            "createMusicalBar",
            [new ArrayType(NoteType.Instance), TimeSignatureType.Instance],
            ParameterNames: ["notes", "timeSig"]);
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
            return MusicValue.Bar(bar);
        });

        // ===== Incremental Bar Building =====

        var createEmptyMusicalBarSignature = new FunctionSignature(
            "createEmptyMusicalBar",
            [TimeSignatureType.Instance],
            ParameterNames: ["timeSig"]);
        registry.Register("createEmptyMusicalBar", createEmptyMusicalBarSignature, args =>
        {
            var timeSig = (TimeSignatureData)args[0].Data!;
            var bar = Audio.ClassicalComposition.CreateEmptyMusicalBar(timeSig);
            return MusicValue.Bar(bar);
        });

        var tryAddNoteToBarSignature = new FunctionSignature(
            "tryAddNoteToBar",
            [BarType.Instance, MusicalNoteType.Instance],
            ParameterNames: ["bar", "note"]);
        registry.Register("tryAddNoteToBar", tryAddNoteToBarSignature, args =>
        {
            var bar = (BarData)args[0].Data!;
            var note = (MusicalNoteData)args[1].Data!;
            bool success = Audio.ClassicalComposition.TryAddNoteToBar(bar, note);
            return Value.Bool(success);
        });

        var addNoteToBarSignature = new FunctionSignature(
            "addNoteToBar",
            [BarType.Instance, MusicalNoteType.Instance],
            ParameterNames: ["bar", "note"]);
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
            [NoteValueType.Instance, IntType.Instance],
            ParameterNames: ["noteValue", "denominator"]);
        registry.Register("noteValueToBeats", noteValueToBeatsSignature, args =>
        {
            int noteValueEnum = (int)args[0].Data!;
            int denominator = (int)args[1].Data!;
            double beats = Audio.MusicalConversions.NoteValueToBeats(noteValueEnum, denominator);
            return Value.Double(beats);
        });

        var validateBarDurationSignature = new FunctionSignature(
            "validateBarDuration",
            [BarType.Instance, TimeSignatureType.Instance],
            ParameterNames: ["bar", "timeSig"]);
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
            [BarType.Instance],
            ParameterNames: ["bar"]);
        registry.Register("getRemainingBeats", getRemainingBeatsSignature, args =>
        {
            var bar = (BarData)args[0].Data!;
            double remaining = Audio.MusicalConversions.GetRemainingBeats(bar);
            return Value.Double(remaining);
        });

        var wouldFitSignature = new FunctionSignature(
            "wouldFit",
            [BarType.Instance, MusicalNoteType.Instance],
            ParameterNames: ["bar", "note"]);
        registry.Register("wouldFit", wouldFitSignature, args =>
        {
            var bar = (BarData)args[0].Data!;
            var note = (MusicalNoteData)args[1].Data!;
            bool fits = Audio.MusicalConversions.WouldFit(bar, note);
            return Value.Bool(fits);
        });

        var calculateOverflowSignature = new FunctionSignature(
            "calculateOverflow",
            [BarType.Instance],
            ParameterNames: ["bar"]);
        registry.Register("calculateOverflow", calculateOverflowSignature, args =>
        {
            var bar = (BarData)args[0].Data!;
            double overflow = Audio.MusicalConversions.CalculateOverflow(bar);
            return Value.Double(overflow);
        });

        // ===== Bar Rendering =====

        var renderBarToVoicesSignature = new FunctionSignature(
            "renderBarToVoices",
            [BarType.Instance, StringType.Instance, IntType.Instance, DoubleType.Instance],
            ParameterNames: ["bar", "synth", "sampleRate", "bpm"]);
        registry.Register("renderBarToVoices", renderBarToVoicesSignature, args =>
        {
            var bar = (BarData)args[0].Data!;
            string synthType = (string)args[1].Data!;
            int sampleRate = (int)args[2].Data!;
            double bpm = (double)args[3].Data!;

            var voices = Audio.BarRenderer.RenderBarToVoices(bar, synthType, sampleRate, bpm);
            var voiceValues = voices.Select(v => MusicValue.Voice(v)).ToArray();
            return Value.Array(voiceValues, VoiceType.Instance);
        });

        // ===== Sequence Functions =====

        var createSequenceSignature = new FunctionSignature("createSequence", [],
            ParameterNames: []);
        registry.Register("createSequence", createSequenceSignature, args =>
        {
            var sequence = Audio.SequenceRenderer.CreateSequence();
            return MusicValue.Sequence(sequence);
        });

        var addBarToSequenceSignature = new FunctionSignature(
            "addBarToSequence",
            [SequenceType.Instance, BarType.Instance],
            ParameterNames: ["seq", "bar"]);
        registry.Register("addBarToSequence", addBarToSequenceSignature, args =>
        {
            var sequence = (SequenceData)args[0].Data!;
            var bar = (BarData)args[1].Data!;
            Audio.SequenceRenderer.AddBarToSequence(sequence, bar);
            return MusicValue.Sequence(sequence);
        });

        var renderSequenceToVoicesSignature = new FunctionSignature(
            "renderSequenceToVoices",
            [SequenceType.Instance, StringType.Instance, IntType.Instance, DoubleType.Instance],
            ParameterNames: ["seq", "synth", "sampleRate", "bpm"]);
        registry.Register("renderSequenceToVoices", renderSequenceToVoicesSignature, args =>
        {
            var sequence = (SequenceData)args[0].Data!;
            string synthType = (string)args[1].Data!;
            int sampleRate = (int)args[2].Data!;
            double bpm = (double)args[3].Data!;

            var voices = Audio.SequenceRenderer.RenderSequenceToVoices(sequence, synthType, sampleRate, bpm);
            var voiceValues = voices.Select(v => MusicValue.Voice(v)).ToArray();
            return Value.Array(voiceValues, VoiceType.Instance);
        });

        // ===== Manual Bar Positioning =====

        var renderBarAtBeatSignature = new FunctionSignature(
            "renderBarAtBeat",
            [BarType.Instance, DoubleType.Instance, StringType.Instance, IntType.Instance, DoubleType.Instance],
            ParameterNames: ["bar", "beat", "synth", "sampleRate", "bpm"]);
        registry.Register("renderBarAtBeat", renderBarAtBeatSignature, args =>
        {
            var bar = (BarData)args[0].Data!;
            double beatOffset = (double)args[1].Data!;
            string synthType = (string)args[2].Data!;
            int sampleRate = (int)args[3].Data!;
            double bpm = (double)args[4].Data!;

            var voices = Audio.BarRenderer.RenderBarAtBeat(bar, beatOffset, synthType, sampleRate, bpm);
            var voiceValues = voices.Select(v => MusicValue.Voice(v)).ToArray();
            return Value.Array(voiceValues, VoiceType.Instance);
        });

        // Phase 43 D-09 — renderBarAtBeat(Bar, Beat, String, Int, Double) overload.
        // Beat is fractional-double-backed (BeatType.cs:25-28), so the underlying
        // CLR cast args[1].Data is a double — identical implementation to the
        // Double overload. Registering as a distinct signature lets composers
        // pass Beat-typed values explicitly (e.g. `(renderBarAtBeat bar 1.0b ...)`)
        // and reach the exact-match +1000 slot per OverloadResolver scoring
        // (vs. the compat-match +500 that bare Beat→Double would otherwise hit).
        // bpm is supplied explicitly via args[4], so no MusicalContext read here.
        var renderBarAtBeatBeatSignature = new FunctionSignature(
            "renderBarAtBeat",
            [BarType.Instance, BeatType.Instance, StringType.Instance, IntType.Instance, DoubleType.Instance],
            ParameterNames: ["bar", "beat", "synth", "sampleRate", "bpm"]);
        registry.Register("renderBarAtBeat", renderBarAtBeatBeatSignature, args =>
        {
            var bar = (BarData)args[0].Data!;
            double beatOffset = (double)args[1].Data!;
            string synthType = (string)args[2].Data!;
            int sampleRate = (int)args[3].Data!;
            double bpm = (double)args[4].Data!;

            var voices = Audio.BarRenderer.RenderBarAtBeat(bar, beatOffset, synthType, sampleRate, bpm);
            var voiceValues = voices.Select(v => MusicValue.Voice(v)).ToArray();
            return Value.Array(voiceValues, VoiceType.Instance);
        });

        var renderBarAtTimeSignature = new FunctionSignature(
            "renderBarAtTime",
            [BarType.Instance, DoubleType.Instance, StringType.Instance, IntType.Instance, DoubleType.Instance],
            ParameterNames: ["bar", "time", "synth", "sampleRate", "bpm"]);
        registry.Register("renderBarAtTime", renderBarAtTimeSignature, args =>
        {
            var bar = (BarData)args[0].Data!;
            double timeSeconds = (double)args[1].Data!;
            string synthType = (string)args[2].Data!;
            int sampleRate = (int)args[3].Data!;
            double bpm = (double)args[4].Data!;

            var voices = Audio.BarRenderer.RenderBarAtTime(bar, timeSeconds, synthType, sampleRate, bpm);
            var voiceValues = voices.Select(v => MusicValue.Voice(v)).ToArray();
            return Value.Array(voiceValues, VoiceType.Instance);
        });

        // ===== Pitch Conversion =====

        var noteToFrequencySignature = new FunctionSignature(
            "noteToFrequency",
            [NoteType.Instance],
            ParameterNames: ["note"]);
        registry.Register("noteToFrequency", noteToFrequencySignature, args =>
        {
            if (args[0].Data is string stringNote)
            {
                var (noteName, octave, alteration) = NoteType.Parse(stringNote);
                return Value.Double(Audio.PitchConversion.NoteToFrequency(noteName, octave, alteration));
            }
            else if (args[0].Data is MusicalNoteData musicalNoteData)
            {
                return Value.Double(Audio.PitchConversion.NoteToFrequency(musicalNoteData.NoteName, musicalNoteData.Octave, musicalNoteData.Alteration));
            }
            throw new Exception("Invalid argument data for noteToFrequency");
        });

        // ===== Euclidean Rhythm =====
        // sweep-0614: the base 3-arg (Int, Int, Note) overload moved to
        // RegisterEuclideanOverloads so it can thread ExecutionContext for the
        // charitable degenerate-input handling (D-v1.5-05) shared with the
        // swing/humanize overloads. See TryValidateEuclideanInputs.
    }

    // ===== Phase 15 DX-09: euclidean swing + humanize + seed overloads =====
    //
    // Two additional euclidean overloads that accent hit velocities based on swing
    // and optionally perturb them with a seeded uniform-random humanize factor.
    //
    // Semantics (from 15-CONTEXT.md):
    //   D-05  swing clamped to [-1.0, 1.0]
    //   D-06  on-beat = step index divisible by gridStep = max(1, steps / hits)
    //   D-07  accent is a raw velocity delta (no multiplier)
    //   D-08  asymmetric accent: only the accented set moves; the other set stays at base
    //         (positive swing accents on-beats; negative swing accents off-beats)
    //   D-09  humanize unit = fractional velocity on [0, 1] scale
    //   D-10  humanize clamped to [0, 1]
    //   D-11  uniform distribution over [-humanize, +humanize]
    //   D-12  perturbed velocity clamped to [0, 1] (NOT reflected)
    //   D-17  seed constructs a LOCAL new Random(seed) per-call; does NOT touch
    //         ExecutionContext.GetRand — isolates the PRNG from global seeded state.
    //
    // Security: steps > 1024 raises InvalidOperationException (15-RESEARCH §Security Domain).
    //
    // Base velocity: reads MusicalContext.Velocity ?? 0.63 (matches
    // NoteStreamCompiler.cs:341 default-mf semantics).
    internal static void RegisterEuclideanOverloads(
        InternalFunctionRegistry registry,
        FlowLang.Runtime.ExecutionContext context)
    {
        // euclidean(Int, Int, Note) -> Sequence (base overload — sweep-0614 moved
        // here from RegisterMusicalNotationFunctions to thread ExecutionContext for
        // the charitable degenerate-input contract).
        var euclideanSignature = new FunctionSignature(
            "euclidean",
            [IntType.Instance, IntType.Instance, NoteType.Instance],
            ParameterNames: ["hits", "steps", "note"]);
        registry.Register("euclidean", euclideanSignature, args =>
        {
            int hits = (int)args[0].Data!;
            int steps = (int)args[1].Data!;
            string noteStr = (string)args[2].Data!;

            if (!TryValidateEuclideanInputs(ref hits, ref steps, context))
                return MusicValue.Sequence(new SequenceData());

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
            return MusicValue.Sequence(sequence);
        });

        // euclidean(Int, Int, Note, Double) -> Sequence
        var euclideanSwingSig = new FunctionSignature(
            "euclidean",
            [IntType.Instance, IntType.Instance, NoteType.Instance, DoubleType.Instance],
            ParameterNames: ["hits", "steps", "note", "swing"]);
        registry.Register("euclidean", euclideanSwingSig, args =>
        {
            int hits = (int)args[0].Data!;
            int steps = (int)args[1].Data!;
            string noteStr = (string)args[2].Data!;
            double swing = (double)args[3].Data!;

            if (!TryValidateEuclideanInputs(ref hits, ref steps, context))
                return MusicValue.Sequence(new SequenceData());

            return BuildEuclideanSequence(hits, steps, noteStr, swing, humanize: 0.0, rng: null, context);
        });

        // euclidean(Int, Int, Note, Double, Double, Int) -> Sequence
        var euclideanHumanSig = new FunctionSignature(
            "euclidean",
            [IntType.Instance, IntType.Instance, NoteType.Instance,
             DoubleType.Instance, DoubleType.Instance, IntType.Instance],
            ParameterNames: ["hits", "steps", "note", "swing", "humanize", "seed"]);
        registry.Register("euclidean", euclideanHumanSig, args =>
        {
            int hits = (int)args[0].Data!;
            int steps = (int)args[1].Data!;
            string noteStr = (string)args[2].Data!;
            double swing = (double)args[3].Data!;
            double humanize = (double)args[4].Data!;
            int seed = (int)args[5].Data!;

            if (!TryValidateEuclideanInputs(ref hits, ref steps, context))
                return MusicValue.Sequence(new SequenceData());

            // D-17: LOCAL new Random(seed) scoped to THIS call; does NOT read or mutate
            // ExecutionContext.GetRand. Mirrors VariationFunctions.VarySeeded at :71-77.
            var rng = new Random(seed);
            return BuildEuclideanSequence(hits, steps, noteStr, swing, humanize, rng, context);
        });
    }

    /// <summary>
    /// sweep-0614 — charitable degenerate-input handling for all three
    /// <c>euclidean</c> overloads (D-v1.5-05 generative contract). Previously each
    /// overload threw <see cref="InvalidOperationException"/> on hits&lt;=0 / steps&lt;=0 /
    /// steps&gt;1024 / hits&gt;steps, which propagated to FlowEngine's catch-all and
    /// rendered as a location-less "0:0: error: Unexpected error: ...". This brings
    /// euclidean in line with its sibling generative primitives (cellular/markov/
    /// lsystem/lorenz) which clamp/empty + WarnOnce and never throw.
    ///
    /// <para>Returns <c>true</c> to PROCEED (with possibly-clamped <paramref name="hits"/>
    /// / <paramref name="steps"/>), <c>false</c> to return an empty Sequence. Follows
    /// Phase-44 Pattern S3: strict-mode elevates the advisory to a located
    /// <c>ctx.ErrorReporter.ReportError</c>; otherwise <see cref="RenderingDiagnostics.WarnOnce"/>.
    /// Preserves the Phase-15 T-15-08 DoS guard (steps&gt;1024 → clamp to 1024).</para>
    /// </summary>
    private static bool TryValidateEuclideanInputs(
        ref int hits, ref int steps,
        FlowLang.Runtime.ExecutionContext context)
    {
        var site = context.CurrentCallSite;

        // hits <= 0 or steps <= 0 → empty sequence + advisory.
        if (hits <= 0 || steps <= 0)
        {
            if (context.CallerStrictMode)
            {
                context.ErrorReporter.ReportError(
                    $"[strict] [euclidean] hits/steps must be > 0 — got hits={hits}, steps={steps} at {site}; returning empty sequence",
                    site);
            }
            else
            {
                FlowLang.Diagnostics.RenderingDiagnostics.WarnOnce(
                    $"euclidean:nonpositive:{site}:{hits}:{steps}",
                    $"[euclidean] hits/steps must be > 0 — got hits={hits}, steps={steps} at {site}; returning empty sequence");
            }
            return false;
        }

        // steps > 1024 → clamp (T-15-08 / T-36-19 DoS guard, kept as a clamp).
        if (steps > 1024)
        {
            if (context.CallerStrictMode)
            {
                context.ErrorReporter.ReportError(
                    $"[strict] [euclidean] steps clamped to 1024 — got steps={steps} (> 1024) at {site}",
                    site);
            }
            else
            {
                FlowLang.Diagnostics.RenderingDiagnostics.WarnOnce(
                    $"euclidean:steps-cap:{site}:{steps}",
                    $"[euclidean] steps {steps} > 1024 at {site}; clamped to 1024 (T-36-19 DoS guard)");
            }
            steps = 1024;
        }

        // hits > steps → clamp hits down to steps.
        if (hits > steps)
        {
            if (context.CallerStrictMode)
            {
                context.ErrorReporter.ReportError(
                    $"[strict] [euclidean] hits clamped to steps — got hits={hits} > steps={steps} at {site}",
                    site);
            }
            else
            {
                FlowLang.Diagnostics.RenderingDiagnostics.WarnOnce(
                    $"euclidean:hits-cap:{site}:{hits}:{steps}",
                    $"[euclidean] hits {hits} > steps {steps} at {site}; clamped to {steps}");
            }
            hits = steps;
        }

        return true;
    }

    private static Value BuildEuclideanSequence(
        int hits, int steps, string noteStr,
        double swing, double humanize, Random? rng,
        FlowLang.Runtime.ExecutionContext context)
    {
        var (noteName, octave, alteration) = NoteType.Parse(noteStr);
        var pattern = Bjorklund(hits, steps);

        var duration = steps switch
        {
            <= 4 => NoteValueType.Value.QUARTER,
            <= 8 => NoteValueType.Value.EIGHTH,
            <= 16 => NoteValueType.Value.SIXTEENTH,
            _ => NoteValueType.Value.THIRTYSECOND
        };

        // Base velocity: MusicalContext.Velocity ?? 0.63 (matches NoteStreamCompiler.cs:341).
        double baseVelocity = context.GetMusicalContext().Velocity ?? 0.63;

        // D-05..D-08: swing clamp + on-beat detection + asymmetric accent.
        int gridStep = Math.Max(1, steps / hits);
        double swingClamped = Math.Clamp(swing, -1.0, 1.0);
        double accentAmount = Math.Abs(swingClamped);
        bool accentOnBeats = swingClamped >= 0.0;

        // D-10: humanize clamp.
        double humanizeClamped = Math.Clamp(humanize, 0.0, 1.0);

        var notes = new List<MusicalNoteData>();
        for (int i = 0; i < pattern.Length; i++)
        {
            bool isHit = pattern[i];
            if (!isHit)
            {
                notes.Add(new MusicalNoteData(' ', 0, 0, (int)duration, isRest: true));
                continue;
            }

            double v = baseVelocity;
            bool onBeat = (i % gridStep) == 0;
            bool accented = accentOnBeats == onBeat;
            if (accented) v += accentAmount;

            // D-11: uniform perturbation in [-humanize, +humanize].
            if (rng != null && humanizeClamped > 0.0)
            {
                double jitter = (rng.NextDouble() * 2.0 - 1.0) * humanizeClamped;
                v += jitter;
                // D-12: clamp, not reflect. MusicalNoteData ctor also clamps — belt-and-braces.
                v = Math.Max(0.0, Math.Min(1.0, v));
            }

            notes.Add(new MusicalNoteData(noteName, octave, alteration, (int)duration,
                isRest: false, velocity: v));
        }

        var timeSig = new TimeSignatureData(4, 4);
        var bar = new BarData(notes, timeSig);
        var sequence = new SequenceData();
        sequence.AddBar(bar);
        return MusicValue.Sequence(sequence);
    }

    /// <summary>
    /// Bjorklund algorithm: distributes hits evenly across steps.
    /// </summary>
    private static bool[] Bjorklund(int hits, int steps)
    {
        if (hits < 0) throw new ArgumentOutOfRangeException(nameof(hits), "Hits cannot be negative.");
        if (steps <= 0) throw new ArgumentOutOfRangeException(nameof(steps), "Steps must be positive.");

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
