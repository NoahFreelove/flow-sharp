using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
// Disambiguate FlowLang.Runtime.ExecutionContext from System.Threading.ExecutionContext.
using ExecutionContext = FlowLang.Runtime.ExecutionContext;

namespace FlowLang.StandardLibrary.Audio.DSP;

/// <summary>
/// Phase 37 Plan 37-02 Task 3 — registers the <c>pitchShift</c> builtin
/// (DSP-03). Three composer-facing parameter shapes for the cent argument
/// (Double / Cent / Semitone), each with overloads at every named-arg arity
/// from 2 to 9 — mirrors <see cref="StretchFunctions"/>.
///
/// <para>
/// Surface examples per 37-PATTERNS.md PitchShiftFunctions + Phase 36-02:
/// <list type="bullet">
///   <item><description><c>(pitchShift buf -200)</c> — Double cents.</description></item>
///   <item><description><c>(pitchShift buf +50c)</c> — Cent literal.</description></item>
///   <item><description><c>(pitchShift buf +2st)</c> — Semitone literal
///   (CLR-backed by Int; multiplied by 100 to get cents).</description></item>
///   <item><description><c>(pitchShift buf +5st mode=#auto frameSize=4096
///   transientThreshold=0.25)</c> — full named-arg knob bag.</description></item>
/// </list>
/// </para>
///
/// <para>
/// W4 LOCK — all 6 knob names appear in ParameterNames AND forward into
/// <see cref="PitchShiftEngine.Process"/> which threads them through to
/// <see cref="StretchEngine.Process"/>.
/// </para>
///
/// <para>
/// Identity fast-path on cents=0 (Pitfall 11) lives in
/// <see cref="PitchShiftEngine.Process"/> — the builtin shell delegates.
/// </para>
/// </summary>
public static class PitchShiftFunctions
{
    // Parameter name schedule for the Double / Cent cents variants. The
    // second arg uses the label "cents".
    // W4 LOCK: all 6 knob names (frameSize, hopSize, overlap,
    // transientThreshold, pitchPeriod, windowSize) declared one-per-line for
    // grep-discoverability per the plan's acceptance criterion.
    private static readonly string[] CentsParamNames =
    {
        "buffer",
        "cents",
        "mode",
        "frameSize",
        "hopSize",
        "overlap",
        "transientThreshold",
        "pitchPeriod",
        "windowSize",
    };

    // Parameter name schedule for the Semitone variant — second arg is
    // labelled "semitones" so composer-side named-arg `semitones=` resolves.
    private static readonly string[] SemitonesParamNames =
    {
        "buffer",
        "semitones",
        "mode",
        "frameSize",
        "hopSize",
        "overlap",
        "transientThreshold",
        "pitchPeriod",
        "windowSize",
    };

    private static readonly FlowType[] KnobTypes =
    {
        SymbolType.Instance,    // mode
        IntType.Instance,       // frameSize
        IntType.Instance,       // hopSize
        IntType.Instance,       // overlap
        DoubleType.Instance,    // transientThreshold
        IntType.Instance,       // pitchPeriod
        IntType.Instance,       // windowSize
    };

    public static void Register(InternalFunctionRegistry registry, ExecutionContext context)
    {
        // Three parallel arity ladders — one per cents-arg type variant.
        RegisterLadder(registry, context, DoubleType.Instance, CentsParamNames,
            semitonesToCentsConversion: false);
        RegisterLadder(registry, context, CentType.Instance, CentsParamNames,
            semitonesToCentsConversion: false);
        RegisterLadder(registry, context, SemitoneType.Instance, SemitonesParamNames,
            semitonesToCentsConversion: true);
    }

    /// <summary>
    /// Register pitchShift overloads at every arity from 2 to 9 for a given
    /// second-arg type (Double / Cent / Semitone). Each arity's signature
    /// uses the appropriate ParameterNames (cents/semitones) and binds the
    /// knob types in the natural order.
    /// </summary>
    private static void RegisterLadder(
        InternalFunctionRegistry registry,
        ExecutionContext context,
        FlowType centsArgType,
        string[] paramNames,
        bool semitonesToCentsConversion)
    {
        for (int arity = 2; arity <= paramNames.Length; arity++)
        {
            var inputTypes = new FlowType[arity];
            var names = new string[arity];
            inputTypes[0] = BufferType.Instance;
            inputTypes[1] = centsArgType;
            names[0] = paramNames[0];
            names[1] = paramNames[1];
            for (int i = 2; i < arity; i++)
            {
                inputTypes[i] = KnobTypes[i - 2];
                names[i] = paramNames[i];
            }
            var sig = new FunctionSignature("pitchShift", inputTypes, ParameterNames: names);
            int capturedArity = arity;
            bool capturedSemitones = semitonesToCentsConversion;
            registry.Register("pitchShift", sig,
                args => PitchShiftEffect(args, context, capturedArity, capturedSemitones));
        }
    }

    /// <summary>
    /// Extract args, convert semitones→cents when necessary, short-circuit
    /// empty buffer, resolve the mode Symbol with charitable fallback to
    /// Auto, then delegate to <see cref="PitchShiftEngine.Process"/>.
    /// </summary>
    private static Value PitchShiftEffect(
        IReadOnlyList<Value> args,
        ExecutionContext ctx,
        int arity,
        bool semitonesToCents)
    {
        var buffer = args[0].As<AudioBuffer>();

        double cents;
        if (semitonesToCents)
        {
            // Semitone CLR backing IS int (Value.Semitone factory).
            int semitones = args[1].As<int>();
            cents = semitones * 100.0;
        }
        else
        {
            // Double / Cent both back to double per Value factory.
            cents = args[1].As<double>();
        }

        if (buffer.Frames == 0)
            return Value.Buffer(new AudioBuffer(0, buffer.Channels, buffer.SampleRate));

        StretchMode mode = StretchMode.Auto;
        if (arity >= 3)
        {
            string sym = args[2].As<string>();
            mode = ResolveStretchMode(sym, ctx);
        }

        int frameSize = arity >= 4 ? args[3].As<int>() : 2048;
        int hopSize = arity >= 5 ? args[4].As<int>() : 512;
        int overlap = arity >= 6 ? args[5].As<int>() : 4;
        double transientThreshold = arity >= 7 ? args[6].As<double>() : 0.3;
        int? pitchPeriod = arity >= 8 ? args[7].As<int>() : (int?)null;
        int? windowSize = arity >= 9 ? args[8].As<int>() : (int?)null;

        var result = PitchShiftEngine.Process(
            buffer, cents, mode,
            frameSize: frameSize, hopSize: hopSize, overlap: overlap,
            transientThreshold: transientThreshold,
            pitchPeriod: pitchPeriod, windowSize: windowSize,
            site: ctx.CurrentCallSite);
        return Value.Buffer(result);
    }

    private static StretchMode ResolveStretchMode(string sym, ExecutionContext ctx)
    {
        return sym switch
        {
            "vocoder" => StretchMode.Vocoder,
            "psola" => StretchMode.Psola,
            "auto" => StretchMode.Auto,
            _ => FallbackToAuto(sym, ctx),
        };
    }

    private static StretchMode FallbackToAuto(string sym, ExecutionContext ctx)
    {
        // Phase 44 Plan 44-06: strict-mode elevation per D-06/D-07.
        if (ctx.CallerStrictMode)
        {
            ctx.ErrorReporter.ReportError(
                $"[strict] [pitchShift] unknown mode symbol '#{sym}' — falling back to #auto. " +
                "Valid options: #vocoder | #psola | #auto.",
                ctx.CurrentCallSite);
            return StretchMode.Auto;
        }
        RenderingDiagnostics.WarnOnce(
            $"pitchShift:mode:{sym}",
            $"[pitchShift] unknown mode symbol '#{sym}' — falling back to #auto. " +
            "Valid options: #vocoder | #psola | #auto.");
        return StretchMode.Auto;
    }
}
