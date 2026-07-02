using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
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
        RegisterSidechain(registry);
        RegisterVolume(registry);  // Phase 26.2 ERG-03 (D-04..D-07) — linear-multiplier alternative to gain(dB)
    }

    // ===== Reverb =====

    private static void RegisterReverb(InternalFunctionRegistry registry)
    {
        // reverb(Buffer, Double) -> Buffer — room size only, default damping=0.5, mix=0.3
        var reverbSimpleSig = new FunctionSignature("reverb",
            [BufferType.Instance, DoubleType.Instance],
            ParameterNames: ["buf", "room"]);
        registry.Register("reverb", reverbSimpleSig, ReverbSimple);

        // reverb(Buffer, Double, Double, Double) -> Buffer — room, damping, mix
        var reverbFullSig = new FunctionSignature("reverb",
            [BufferType.Instance, DoubleType.Instance, DoubleType.Instance, DoubleType.Instance],
            ParameterNames: ["buf", "room", "damping", "mix"]);
        registry.Register("reverb", reverbFullSig, ReverbFull);

        // Phase 26.2 ERG-02: reverb(Buffer, Double, Second) — decay time as Second.
        // This is the ONLY 3-arg reverb overload (siblings are 2-arg and 4-arg), and
        // Second.IsCompatibleWith(Double) means a bare third Double also resolves
        // here — so `(reverb buf 0.5 1.5s)` and `(reverb buf 0.5 1.5)` invoke this
        // same lambda and per-sample identity (MusicTypeFXOverloadFacts) holds by
        // construction. Full overload-score deliberation: Phase 26.2 decision log.
        var reverbSecondSig = new FunctionSignature("reverb",
            [BufferType.Instance, DoubleType.Instance, SecondType.Instance],
            ParameterNames: ["buf", "room", "decay"]);
        registry.Register("reverb", reverbSecondSig, args =>
        {
            var buffer = args[0].As<AudioBuffer>();
            float roomSize = (float)args[1].As<double>();
            // Second arg's CLR Data IS double (Value.Second factory); Wave-1
            // Second.IsCompatibleWith(Double) ALSO routes a bare 1.5 here, so
            // both source forms converge on this lambda.
            float decaySec = (float)args[2].As<double>();
            // Map decay → damping using a deterministic, bounded formula. Same
            // formula used for both calls (since both calls land here), so
            // per-sample identity is guaranteed regardless of the formula.
            float damping = (float)Math.Clamp(0.7 - decaySec * 0.15, 0.1, 0.7);
            const float mix = 0.3f;  // matches ReverbSimple default

            if (buffer.Frames == 0)
                return Value.Buffer(new AudioBuffer(0, buffer.Channels, buffer.SampleRate));

            var result = Reverb.Apply(buffer, roomSize, damping, mix);
            return Value.Buffer(result);
        });
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
            [BufferType.Instance, DoubleType.Instance],
            ParameterNames: ["buf", "cutoff"]);
        registry.Register("lowpass", lowpassSig, LowpassFilter);

        // highpass(Buffer, Double) -> Buffer — cutoff Hz
        var highpassSig = new FunctionSignature("highpass",
            [BufferType.Instance, DoubleType.Instance],
            ParameterNames: ["buf", "cutoff"]);
        registry.Register("highpass", highpassSig, HighpassFilter);

        // bandpass(Buffer, Double, Double) -> Buffer — low Hz, high Hz
        var bandpassSig = new FunctionSignature("bandpass",
            [BufferType.Instance, DoubleType.Instance, DoubleType.Instance],
            ParameterNames: ["buf", "low", "high"]);
        registry.Register("bandpass", bandpassSig, BandpassFilter);

        // Phase 26.2 ERG-04: Hertz-typed overloads — explicit frequency-type
        // ergonomics. Delegates to the same LowpassFilter/HighpassFilter/
        // BandpassFilter lambdas; Hertz's CLR backing IS double (Value.Hertz
        // factory wraps a double), so args[1].As<double>() reads it directly
        // without per-overload coercion.
        var lowpassHzSig = new FunctionSignature("lowpass",
            [BufferType.Instance, HertzType.Instance],
            ParameterNames: ["buf", "cutoff"]);
        registry.Register("lowpass", lowpassHzSig, LowpassFilter);

        var highpassHzSig = new FunctionSignature("highpass",
            [BufferType.Instance, HertzType.Instance],
            ParameterNames: ["buf", "cutoff"]);
        registry.Register("highpass", highpassHzSig, HighpassFilter);

        var bandpassHzSig = new FunctionSignature("bandpass",
            [BufferType.Instance, HertzType.Instance, HertzType.Instance],
            ParameterNames: ["buf", "low", "high"]);
        registry.Register("bandpass", bandpassHzSig, BandpassFilter);
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
            [BufferType.Instance, DoubleType.Instance, DoubleType.Instance],
            ParameterNames: ["buf", "threshold", "ratio"]);
        registry.Register("compress", compressSimpleSig, CompressSimple);

        // compress(Buffer, Double, Double, Double, Double) -> Buffer — threshold, ratio, attack ms, release ms
        var compressFullSig = new FunctionSignature("compress",
            [BufferType.Instance, DoubleType.Instance, DoubleType.Instance,
             DoubleType.Instance, DoubleType.Instance],
            ParameterNames: ["buf", "threshold", "ratio", "attack", "release"]);
        registry.Register("compress", compressFullSig, CompressFull);

        // Phase 26.2 ERG-02 + D-10: compress(Buffer, Decibel, Double, Millisecond, Millisecond)
        // — full music-typed overload. Documents at the type-system level that
        // threshold IS a dB value and attack/release ARE millisecond times.
        // Delegates to existing CompressFull lambda; Decibel's and Millisecond's
        // CLR backing IS double (Value.Decibel / Value.Millisecond factories),
        // so args[i].As<double>() reads each one directly.
        var compressMusicTypedSig = new FunctionSignature("compress",
            [BufferType.Instance, DecibelType.Instance, DoubleType.Instance,
             MillisecondType.Instance, MillisecondType.Instance],
            ParameterNames: ["buf", "threshold", "ratio", "attack", "release"]);
        registry.Register("compress", compressMusicTypedSig, CompressFull);

        // sweep-260620 soft-overload: simple Decibel compress(Buffer, Decibel, Double)
        // (CORRECTNESS — only the full 5-arg form was dB-typed; the simple 3-arg form
        // forced a raw Double threshold). Decibel's CLR backing IS double, so the existing
        // CompressSimple lambda reads args[1].As<double>() directly.
        var compressSimpleDbSig = new FunctionSignature("compress",
            [BufferType.Instance, DecibelType.Instance, DoubleType.Instance],
            ParameterNames: ["buf", "threshold", "ratio"]);
        registry.Register("compress", compressSimpleDbSig, CompressSimple);
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
            [BufferType.Instance, DoubleType.Instance, DoubleType.Instance, DoubleType.Instance],
            ParameterNames: ["buf", "timeMs", "feedback", "mix"]);
        registry.Register("delay", delaySig, DelayEffect);

        // Phase 26.2 ERG-02: delay(Buffer, Millisecond, Double, Double) — explicit ms ergonomics.
        // Delegates to existing DelayEffect lambda; Millisecond's CLR backing IS double
        // (Value.Millisecond factory wraps a double — see Value.cs:36), so
        // args[1].As<double>() reads it directly without per-overload coercion.
        var delayMsSig = new FunctionSignature("delay",
            [BufferType.Instance, MillisecondType.Instance, DoubleType.Instance, DoubleType.Instance],
            ParameterNames: ["buf", "timeMs", "feedback", "mix"]);
        registry.Register("delay", delayMsSig, DelayEffect);
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

    /// <summary>
    /// DX-12 (Phase 22 plan 22-04): convert a NoteValue enum + BPM into milliseconds.
    /// QUARTER at 120 BPM = 60_000/120 = 500ms; EIGHTH = 250ms; SIXTEENTH = 125ms;
    /// WHOLE = 4 × quarter; HALF = 2 × quarter; THIRTYSECOND = quarter / 8.
    /// Out-of-range enum values fall through to the quarter fallback (charitable D-07,
    /// matches threat T-22-V5-14 mitigation: no exception, no crash).
    /// Public visibility required for cross-assembly Facts (no InternalsVisibleTo configured).
    /// </summary>
    public static double NoteValueToMs(NoteValueType.Value nv, double bpm)
    {
        double quarterMs = 60_000.0 / bpm;
        return nv switch
        {
            NoteValueType.Value.WHOLE        => quarterMs * 4,
            NoteValueType.Value.HALF         => quarterMs * 2,
            NoteValueType.Value.QUARTER      => quarterMs,
            NoteValueType.Value.EIGHTH       => quarterMs / 2,
            NoteValueType.Value.SIXTEENTH    => quarterMs / 4,
            NoteValueType.Value.THIRTYSECOND => quarterMs / 8,
            _                                 => quarterMs,
        };
    }

    /// <summary>
    /// DX-12 (Phase 22 plan 22-04): registers the NoteValue-rate delay overload that reads
    /// MusicalContext.Tempo at call time. Existing ms-rate delay (Register/RegisterDelay)
    /// stays byte-identical — this method only ADDS a new signature, never mutates the
    /// existing one.
    ///
    /// Called from <see cref="BuiltInFunctions.RegisterContextDependentFunctions"/> alongside
    /// <c>RegisterEuclideanOverloads</c>. The closure captures <paramref name="context"/> so
    /// that the active tempo is read fresh at each call (inside or outside a tempo block).
    /// </summary>
    public static void RegisterContextDependent(
        InternalFunctionRegistry registry,
        FlowLang.Runtime.ExecutionContext context)
    {
        // delay(Buffer, NoteValue, Double, Double) -> Buffer — rate (NoteValue), feedback, mix
        var delaySyncedSig = new FunctionSignature("delay",
            [BufferType.Instance, NoteValueType.Instance, DoubleType.Instance, DoubleType.Instance],
            ParameterNames: ["buf", "rate", "feedback", "mix"]);
        registry.Register("delay", delaySyncedSig, args =>
        {
            var buffer = args[0].As<AudioBuffer>();
            int noteValueEnum = args[1].As<int>();
            float feedback = (float)args[2].As<double>();
            float mix = (float)args[3].As<double>();

            // Read tempo fresh from the active MusicalContext (inside a tempo block) or
            // fall back to 120 BPM when no tempo is active. Matches the
            // `context.GetMusicalContext().Tempo ?? 120.0` pattern used throughout the
            // interpreter (Interpreter.cs:200, Interpreter.cs:210).
            double bpm = context.GetMusicalContext().Tempo ?? 120.0;
            double delayMs = NoteValueToMs((NoteValueType.Value)noteValueEnum, bpm);

            if (buffer.Frames == 0)
                return Value.Buffer(new AudioBuffer(0, buffer.Channels, buffer.SampleRate));

            // Delegate to the same DSP routine as the ms-rate path — both overloads converge
            // at Delay.Apply, which is the regression-stable boundary.
            var result = Delay.Apply(buffer, (float)delayMs, feedback, mix);
            return Value.Buffer(result);
        });

        // Phase 43 D-09 — delay(Buffer, Beat, Double, Double) -> Buffer.
        // Beat is fractional-double-backed (BeatType.cs:25-28); the conversion math
        // mirrors the NoteValue path with `beats * 60_000.0 / bpm` instead of the
        // NoteValueToMs enum lookup. Same Delay.Apply DSP entry point so the
        // perceptual output is RMS-equivalent to (delay buf (beats * 60_000 / bpm)ms ...)
        // under matching tempo.
        //
        // Per RESEARCH A5 + Pitfall 5: registering this overload alongside the
        // existing Buffer/Double/Double/Double and Buffer/NoteValue/Double/Double
        // overloads does NOT ambiguate dispatch. The OverloadResolver scores
        // exact-match Beat at +1000 over compat-match Buffer/Double's +500 when
        // the second arg is Beat-typed; the bare-Double path stays +1000 exact.
        var delayBeatSig = new FunctionSignature("delay",
            [BufferType.Instance, BeatType.Instance, DoubleType.Instance, DoubleType.Instance],
            ParameterNames: ["buf", "beats", "feedback", "mix"]);
        registry.Register("delay", delayBeatSig, args =>
        {
            var buffer = args[0].As<AudioBuffer>();
            double beats = args[1].As<double>();
            float feedback = (float)args[2].As<double>();
            float mix = (float)args[3].As<double>();

            double bpm = context.GetMusicalContext().Tempo ?? 120.0;
            // Walk frames manually (mirrors BeatConversionFunctions.AnyFrameHasTempo)
            // because GetMusicalContext()'s tier-3 default would hide "no explicit tempo".
            if (!HasExplicitTempo(context))
            {
                Diagnostics.RenderingDiagnostics.WarnOnce(
                    "delay-beat-no-tempo",
                    "[delay] no active tempo — defaulting to 120 BPM (use tempo N { ... } to set explicitly)");
            }

            double delayMs = beats * (60_000.0 / bpm);

            if (buffer.Frames == 0)
                return Value.Buffer(new AudioBuffer(0, buffer.Channels, buffer.SampleRate));

            var result = Delay.Apply(buffer, (float)delayMs, feedback, mix);
            return Value.Buffer(result);
        });
    }

    /// <summary>
    /// Walks the <see cref="FlowLang.Runtime.StackFrame"/> parent chain looking
    /// for an explicit <see cref="FlowLang.Runtime.MusicalContext.Tempo"/>
    /// assignment. Per Phase 30 REQ-4, <see cref="FlowLang.Runtime.ExecutionContext.GetMusicalContext"/>
    /// always reports a non-null Tempo (defaulting to 120 BPM at tier 3) so
    /// callers needing to detect "tempo block in scope" must walk frames
    /// directly. Mirrors the helper in
    /// <see cref="BeatConversionFunctions"/>.
    /// </summary>
    private static bool HasExplicitTempo(FlowLang.Runtime.ExecutionContext context)
    {
        for (var f = context.CurrentFrame; f != null; f = f.Parent)
        {
            if (f.MusicalContext is { Tempo: not null }) return true;
        }
        return false;
    }

    // ===== Gain =====

    private static void RegisterGain(InternalFunctionRegistry registry)
    {
        // gain(Buffer, Double) -> Buffer — gain in dB
        var gainSig = new FunctionSignature("gain",
            [BufferType.Instance, DoubleType.Instance],
            ParameterNames: ["buf", "db"]);
        registry.Register("gain", gainSig, GainEffect);

        // gain(Buffer, Decibel) -> Buffer — same dB semantics, exact-match score (1000)
        // beats the compat path's score (500) and gives parity with transpose(Sequence, Cent)
        // for documentation/discoverability. Delegates to the same GainEffect lambda; the
        // underlying value's data is already a double (see Value.Decibel in Runtime/Value.cs).
        var gainDecibelSig = new FunctionSignature("gain",
            [BufferType.Instance, DecibelType.Instance],
            ParameterNames: ["buf", "db"]);
        registry.Register("gain", gainDecibelSig, GainEffect);
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

    // ===== Sidechain Compression =====

    private static void RegisterSidechain(InternalFunctionRegistry registry)
    {
        // sidechain(Buffer source, Buffer trigger, Double threshold, Double ratio) -> Buffer
        var sidechainSimpleSig = new FunctionSignature("sidechain",
            [BufferType.Instance, BufferType.Instance, DoubleType.Instance, DoubleType.Instance],
            ParameterNames: ["buf", "sidechain", "threshold", "ratio"]);
        registry.Register("sidechain", sidechainSimpleSig, SidechainSimple);

        // sidechain(Buffer source, Buffer trigger, Double threshold, Double ratio, Double attackMs, Double releaseMs) -> Buffer
        var sidechainFullSig = new FunctionSignature("sidechain",
            [BufferType.Instance, BufferType.Instance, DoubleType.Instance, DoubleType.Instance,
             DoubleType.Instance, DoubleType.Instance],
            ParameterNames: ["buf", "sidechain", "threshold", "ratio", "attack", "release"]);
        registry.Register("sidechain", sidechainFullSig, SidechainFull);

        // Phase 26.2 ERG-02 + D-10: sidechain(Buffer, Buffer, Decibel, Double, Millisecond, Millisecond)
        // — full music-typed overload (parallel to compress's music-typed shape).
        // Delegates to existing SidechainFull lambda. Decibel/Millisecond CLR
        // backing IS double, so args[i].As<double>() reads each one directly.
        var sidechainMusicTypedSig = new FunctionSignature("sidechain",
            [BufferType.Instance, BufferType.Instance, DecibelType.Instance, DoubleType.Instance,
             MillisecondType.Instance, MillisecondType.Instance],
            ParameterNames: ["buf", "sidechain", "threshold", "ratio", "attack", "release"]);
        registry.Register("sidechain", sidechainMusicTypedSig, SidechainFull);

        // sweep-260620 soft-overload: simple Decibel sidechain(Buffer, Buffer, Decibel, Double)
        // (CORRECTNESS — only the full 6-arg form was dB-typed). Decibel's CLR backing IS double,
        // so the existing SidechainSimple lambda reads args[2].As<double>() directly.
        var sidechainSimpleDbSig = new FunctionSignature("sidechain",
            [BufferType.Instance, BufferType.Instance, DecibelType.Instance, DoubleType.Instance],
            ParameterNames: ["buf", "sidechain", "threshold", "ratio"]);
        registry.Register("sidechain", sidechainSimpleDbSig, SidechainSimple);
    }

    /// <summary>
    /// sidechain(Buffer source, Buffer trigger, Double threshold, Double ratio)
    /// When piped: bass -> sidechain(kick, -12.0, 4.0) becomes sidechain(bass, kick, -12.0, 4.0)
    /// where args[0]=source (piped), args[1]=trigger.
    /// </summary>
    private static Value SidechainSimple(IReadOnlyList<Value> args)
    {
        var source = args[0].As<AudioBuffer>();
        var trigger = args[1].As<AudioBuffer>();
        float threshold = (float)args[2].As<double>();
        float ratio = (float)args[3].As<double>();

        if (source.Frames == 0)
            return Value.Buffer(new AudioBuffer(0, source.Channels, source.SampleRate));

        var result = SidechainCompressor.Apply(source, trigger, threshold, ratio);
        return Value.Buffer(result);
    }

    /// <summary>
    /// sidechain(Buffer source, Buffer trigger, Double threshold, Double ratio, Double attackMs, Double releaseMs)
    /// Full control over attack and release times.
    /// </summary>
    private static Value SidechainFull(IReadOnlyList<Value> args)
    {
        var source = args[0].As<AudioBuffer>();
        var trigger = args[1].As<AudioBuffer>();
        float threshold = (float)args[2].As<double>();
        float ratio = (float)args[3].As<double>();
        float attackMs = (float)args[4].As<double>();
        float releaseMs = (float)args[5].As<double>();

        if (source.Frames == 0)
            return Value.Buffer(new AudioBuffer(0, source.Channels, source.SampleRate));

        var result = SidechainCompressor.Apply(source, trigger, threshold, ratio, attackMs, releaseMs);
        return Value.Buffer(result);
    }

    // ===== Volume (Phase 26.2 ERG-03) =====

    /// <summary>
    /// Phase 26.2 ERG-03 (D-04 / D-05 / D-06):
    /// volume(Buffer, Double) — applies a LINEAR amplitude multiplier (0.5 = half-amp,
    /// 2.0 = double-amp). Distinct from gain (which interprets its 2nd arg as decibels).
    /// CONTEXT D-04: function-name-based split documents the unit choice; composer picks
    /// gain for dB / volume for linear by semantic intent.
    /// CONTEXT D-05: single overload — Float / Int / Long inputs reach it via the existing
    /// primitive widening chain (Int → Long → Float → Double).
    /// </summary>
    private static void RegisterVolume(InternalFunctionRegistry registry)
    {
        var volumeSig = new FunctionSignature("volume",
            [BufferType.Instance, DoubleType.Instance],
            ParameterNames: ["buf", "factor"]);
        registry.Register("volume", volumeSig, VolumeEffect);

        // Quick 260701-vqz: decibel-typed volume. (volume buf +6dB) previously coerced
        // the raw 6.0 into the linear slot (~+15.6 dB); (volume buf -6dB) threw the
        // negative-multiplier error. dB converts to linear (10^(dB/20)) — always
        // non-negative, so D-06's negative-linear rejection can't fire on this path.
        var volumeDbSig = new FunctionSignature("volume",
            [BufferType.Instance, DecibelType.Instance],
            ParameterNames: ["buf", "factor"]);
        registry.Register("volume", volumeDbSig, args => VolumeEffect([
            args[0], Value.Double(Math.Pow(10.0, args[1].As<double>() / 20.0))]));
    }

    /// <summary>
    /// volume(Buffer, Double) — applies linear-multiplier scaling.
    /// CONTEXT D-06: rejects negative multipliers (volume can't phase-invert; that's a
    /// future invertPhase() function); emits stderr Warning when post-multiplication
    /// samples exceed 1.0 (mirrors GainEffect clipping behavior verbatim).
    /// Body: copy of GainEffect (lines 397-424) minus the dB-to-linear conversion line.
    /// </summary>
    private static Value VolumeEffect(IReadOnlyList<Value> args)
    {
        var buffer = args[0].As<AudioBuffer>();
        double linearMultiplier = args[1].As<double>();

        // CONTEXT D-06: reject negative volume (would phase-invert; out of scope per CONTEXT § Deferred Ideas).
        if (linearMultiplier < 0)
        {
            throw new InvalidOperationException(
                $"volume: linear multiplier must be non-negative; received {linearMultiplier}. " +
                "Use gain(buf, dB) for dB-based attenuation, or a future invertPhase(buf) for phase inversion.");
        }

        if (buffer.Frames == 0)
            return Value.Buffer(new AudioBuffer(0, buffer.Channels, buffer.SampleRate));

        var result = new AudioBuffer(buffer.Frames, buffer.Channels, buffer.SampleRate);

        bool wouldClip = false;
        for (int i = 0; i < buffer.Data.Length; i++)
        {
            float sample = buffer.Data[i] * (float)linearMultiplier;
            if (Math.Abs(sample) > 1f) wouldClip = true;
            result.Data[i] = sample;
        }

        // CONTEXT D-06: only warn when the result actually clips (mirrors GainEffect line 417 condition shape;
        // for volume the symmetric gate is `linearMultiplier > 1.0` — analogous to GainEffect's `gainDb > 0`,
        // since attenuation never causes clipping).
        if (wouldClip && linearMultiplier > 1.0)
        {
            Console.Error.WriteLine(
                $"Warning: volume({linearMultiplier:F2}×) causes clipping. Consider reducing volume or applying compression first.");
        }

        return Value.Buffer(result);
    }
}
