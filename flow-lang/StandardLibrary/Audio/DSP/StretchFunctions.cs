using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
// Disambiguate FlowLang.Runtime.ExecutionContext from System.Threading.ExecutionContext
// (same pattern as PatternFunctions.cs:6-8 / GranularFunctions.cs).
using ExecutionContext = FlowLang.Runtime.ExecutionContext;

namespace FlowLang.StandardLibrary.Audio.DSP;

/// <summary>
/// Phase 37 Plan 37-02 Task 3 — registers the <c>stretch</c> builtin (DSP-02)
/// with a "prefix ladder" of arity overloads from 2 to 9.
///
/// <para>
/// Composer surface (Phase 36-02 named-arg call form):
/// <list type="bullet">
///   <item><description><c>(stretch buf 2.0)</c> — positional, mode defaults
///   to Auto, all knobs default.</description></item>
///   <item><description><c>(stretch buf 2.0 mode=#vocoder)</c> — 3-arg
///   prefix.</description></item>
///   <item><description><c>(stretch buf 2.0 mode=#vocoder frameSize=4096
///   hopSize=1024 overlap=4)</c> — 6-arg prefix.</description></item>
///   <item><description><c>(stretch buf 1.5 mode=#psola frameSize=2048
///   hopSize=512 overlap=4 transientThreshold=0.3 pitchPeriod=200
///   windowSize=600)</c> — 9-arg full surface.</description></item>
/// </list>
/// </para>
///
/// <para>
/// W4 LOCK: all 6 knob names appear in ParameterNames AND forward into
/// <see cref="StretchEngine.Process"/> end-to-end. The 9-arg overload is the
/// canonical end-to-end test surface — every composer-supplied knob lands
/// at the underlying DSP engine.
/// </para>
///
/// <para>
/// Resolver constraint (Phase 36-02 OverloadResolver behavior): named-arg
/// resolution requires <c>positional + named == signature.InputTypes.Count</c>
/// AND each named-arg key to appear in the matching signature's
/// <see cref="FunctionSignature.ParameterNames"/>. Two signatures with
/// identical <see cref="FunctionSignature.InputTypes"/> dedupe under
/// <see cref="FunctionSignature.Equals"/> (which intentionally ignores
/// ParameterNames), so at each arity we register ONE name-ordering shape:
/// the prefix ladder. Sparse-named-arg calls that skip knobs in the middle
/// of the ladder fall back to either the full 9-arg form or the matching
/// prefix arity.
/// </para>
///
/// <para>
/// Identity fast-path on factor=1.0 (Pitfall 11) lives in
/// <see cref="StretchEngine.Process"/> — the builtin shell delegates rather
/// than duplicating.
/// </para>
/// </summary>
public static class StretchFunctions
{
    // The prefix ladder — composer incrementally adds knobs in fixed order.
    // W4 LOCK: all 6 knob names (frameSize, hopSize, overlap,
    // transientThreshold, pitchPeriod, windowSize) declared one-per-line for
    // grep-discoverability per the plan's acceptance criterion.
    private static readonly string[] PrefixParamNames =
    {
        "buffer",
        "factor",
        "mode",
        "frameSize",
        "hopSize",
        "overlap",
        "transientThreshold",
        "pitchPeriod",
        "windowSize",
    };

    private static readonly FlowType[] PrefixParamTypes =
    {
        BufferType.Instance,    // buffer
        DoubleType.Instance,    // factor
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
        // Register the prefix ladder at every arity from 2 to 9.
        for (int arity = 2; arity <= PrefixParamNames.Length; arity++)
        {
            var inputTypes = new FlowType[arity];
            var paramNames = new string[arity];
            for (int i = 0; i < arity; i++)
            {
                inputTypes[i] = PrefixParamTypes[i];
                paramNames[i] = PrefixParamNames[i];
            }
            var sig = new FunctionSignature("stretch", inputTypes, ParameterNames: paramNames);
            int capturedArity = arity;
            registry.Register("stretch", sig,
                args => StretchEffect(args, context, capturedArity));
        }
    }

    /// <summary>
    /// Extract args by position (the resolver fills named-arg slots in the
    /// signature's ParameterNames order). Short-circuit empty buffer,
    /// resolve mode Symbol (with charitable fallback to Auto), then delegate
    /// to <see cref="StretchEngine.Process"/> with the W4 LOCK knob bag.
    /// </summary>
    private static Value StretchEffect(
        IReadOnlyList<Value> args,
        ExecutionContext ctx,
        int arity)
    {
        var buffer = args[0].As<AudioBuffer>();
        double factor = args[1].As<double>();

        // Empty-buffer short-circuit (matches EffectsFunctions:96, 112-113, etc.).
        if (buffer.Frames == 0)
            return Value.Buffer(new AudioBuffer(0, buffer.Channels, buffer.SampleRate));

        // Prefix ladder: args[i] holds the value for PrefixParamNames[i].
        // Slot map (W4 LOCK): 2="mode", 3="frameSize", 4="hopSize",
        // 5="overlap", 6="transientThreshold", 7="pitchPeriod", 8="windowSize".
        StretchMode mode = arity >= 3 ? ResolveStretchMode(args[2].As<string>(), ctx) : StretchMode.Auto;
        int frameSize = arity >= 4 ? args[3].As<int>() : 2048;
        int hopSize = arity >= 5 ? args[4].As<int>() : 512;
        int overlap = arity >= 6 ? args[5].As<int>() : 4;
        double transientThreshold = arity >= 7 ? args[6].As<double>() : 0.3;
        int? pitchPeriod = arity >= 8 ? args[7].As<int>() : (int?)null;
        int? windowSize = arity >= 9 ? args[8].As<int>() : (int?)null;

        var result = StretchEngine.Process(
            buffer, factor, mode,
            frameSize: frameSize, hopSize: hopSize, overlap: overlap,
            transientThreshold: transientThreshold,
            pitchPeriod: pitchPeriod, windowSize: windowSize,
            site: ctx.CurrentCallSite);
        return Value.Buffer(result);
    }

    /// <summary>
    /// Map composer's <c>#vocoder</c> / <c>#psola</c> / <c>#auto</c> Symbol
    /// to <see cref="StretchMode"/>. Unknown symbols fall back to
    /// <see cref="StretchMode.Auto"/> with a one-shot stderr advisory per
    /// 37-PATTERNS.md Pattern E (charitable interpretation).
    /// </summary>
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
        // Phase 44 review CR-03: dedup strict-elevated advisory per
        // ExecutionContext lifetime — hot stretch callers must not
        // accumulate one ErrorReporter entry per call.
        var sentinel = $"stretch:mode:{sym}";
        if (ctx.CallerStrictMode)
        {
            if (ctx.StrictAdvisoryDedup.Add(sentinel))
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] [stretch] unknown mode symbol '#{sym}' — falling back to #auto. " +
                    "Valid options: #vocoder | #psola | #auto.",
                    ctx.CurrentCallSite);
            }
            return StretchMode.Auto;
        }
        RenderingDiagnostics.WarnOnce(
            sentinel,
            $"[stretch] unknown mode symbol '#{sym}' — falling back to #auto. " +
            "Valid options: #vocoder | #psola | #auto.");
        return StretchMode.Auto;
    }
}
