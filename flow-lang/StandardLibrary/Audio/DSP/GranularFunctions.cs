using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
// Disambiguate FlowLang.Runtime.ExecutionContext from System.Threading.ExecutionContext
// (same pattern as PatternFunctions.cs:6-8).
using ExecutionContext = FlowLang.Runtime.ExecutionContext;

namespace FlowLang.StandardLibrary.Audio.DSP;

/// <summary>
/// Phase 37 Plan 37-01 Task 3 — registers the <c>granular</c> builtin
/// (DSP-01). Three overloads per 37-RESEARCH.md §Pattern 4 +
/// 37-PATTERNS.md §GranularFunctions.cs:
///
/// <list type="number">
///   <item><description>Plain positional fallback
///   <c>granular(Buffer, Double, Double, Double)</c> — grain in seconds,
///   density in Hz, jitter in [0, 1], windowing defaults to Hann.</description></item>
///   <item><description>Music-typed primary form
///   <c>granular(Buffer, Millisecond, Hertz, Double)</c> — composer
///   ergonomics; CLR backing types are double so the same lambda body
///   handles both overloads.</description></item>
///   <item><description>Full surface
///   <c>granular(Buffer, Millisecond, Hertz, Double, Symbol)</c> — adds
///   explicit windowing pick (<c>#hann</c> / <c>#gaussian</c> /
///   <c>#tukey</c>). Unknown symbol falls back to <c>#hann</c> with one-shot
///   stderr advisory per RESEARCH §Pattern E charitable interpretation.</description></item>
/// </list>
///
/// <para>
/// Each registration carries <see cref="FunctionSignature.ParameterNames"/>
/// so the universal named-argument call form
/// (<c>granular grain=50ms density=20Hz jitter=0.3 windowing=#hann</c>)
/// resolves through Phase 36-02's surface.
/// </para>
///
/// <para>
/// PRNG via <see cref="ExecutionContext.PrngRegistry"/> keyed by
/// <c>(ctx.CurrentCallSite, "granular_offset" | "granular_timing")</c>
/// per D-v1.5-06 + Pitfall 8. NO direct <c>new Random(</c> — granular
/// determinism inherits the two-run cmp-clean contract.
/// </para>
/// </summary>
public static class GranularFunctions
{
    public static void Register(InternalFunctionRegistry registry, ExecutionContext context)
    {
        RegisterPositional(registry, context);
        RegisterMusicTyped(registry, context);
        RegisterMusicTypedWithWindow(registry, context);
    }

    // ====================================================================
    // Overload 1 — Plain positional: granular(Buffer, Double, Double, Double)
    // ====================================================================

    private static void RegisterPositional(InternalFunctionRegistry registry, ExecutionContext context)
    {
        var sig = new FunctionSignature("granular",
            [BufferType.Instance, DoubleType.Instance, DoubleType.Instance, DoubleType.Instance],
            ParameterNames: ["buffer", "grain", "density", "jitter"]);
        registry.Register("granular", sig, args => GranularEffect(args, context, hasWindowSymbol: false, grainIsMs: false));
    }

    // ====================================================================
    // Overload 2 — Music-typed: granular(Buffer, Millisecond, Hertz, Double)
    // ====================================================================

    private static void RegisterMusicTyped(InternalFunctionRegistry registry, ExecutionContext context)
    {
        var sig = new FunctionSignature("granular",
            [BufferType.Instance, MillisecondType.Instance, HertzType.Instance, DoubleType.Instance],
            ParameterNames: ["buffer", "grain", "density", "jitter"]);
        registry.Register("granular", sig, args => GranularEffect(args, context, hasWindowSymbol: false, grainIsMs: true));
    }

    // ====================================================================
    // Overload 3 — Music-typed + windowing pick:
    //   granular(Buffer, Millisecond, Hertz, Double, Symbol)
    // ====================================================================

    private static void RegisterMusicTypedWithWindow(InternalFunctionRegistry registry, ExecutionContext context)
    {
        var sig = new FunctionSignature("granular",
            [BufferType.Instance, MillisecondType.Instance, HertzType.Instance, DoubleType.Instance, SymbolType.Instance],
            ParameterNames: ["buffer", "grain", "density", "jitter", "windowing"]);
        registry.Register("granular", sig, args => GranularEffect(args, context, hasWindowSymbol: true, grainIsMs: true));
    }

    // ====================================================================
    // Shared lambda body
    // ====================================================================

    /// <summary>
    /// Builtin body — extracts args from the three overloads' positional
    /// shape, resolves the windowing pick (with charitable fallback on unknown
    /// symbol), short-circuits empty buffers, and delegates to
    /// <see cref="GranularEngine.Apply"/>.
    /// </summary>
    private static Value GranularEffect(
        IReadOnlyList<Value> args,
        ExecutionContext ctx,
        bool hasWindowSymbol,
        bool grainIsMs)
    {
        var buffer = args[0].As<AudioBuffer>();
        // Millisecond's CLR backing is double (Value.Millisecond) — convert to
        // seconds when the overload's grain arg is the Millisecond-typed one.
        double rawGrain = args[1].As<double>();
        double grainSec = grainIsMs ? rawGrain / 1000.0 : rawGrain;
        // Hertz's CLR backing is also double (Value.Hertz) — no per-overload conversion.
        double densityHz = args[2].As<double>();
        double jitter = args[3].As<double>();

        WindowKind kind = WindowKind.Hann;
        if (hasWindowSymbol)
        {
            // Symbol's CLR Data IS its name string per Value.cs:111-117.
            string sym = args[4].As<string>();
            kind = ResolveWindowKind(sym, ctx);
        }

        // Empty-buffer short-circuit (matches EffectsFunctions.cs:96, 112-113, etc.).
        if (buffer.Frames == 0)
            return Value.Buffer(new AudioBuffer(0, buffer.Channels, buffer.SampleRate));

        var result = GranularEngine.Apply(
            buffer, grainSec, densityHz, jitter, kind,
            ctx.PrngRegistry, ctx.CurrentCallSite);
        return Value.Buffer(result);
    }

    /// <summary>
    /// Map composer's <c>#hann</c> / <c>#gaussian</c> / <c>#tukey</c> Symbol
    /// to <see cref="WindowKind"/>. Unknown symbols fall back to
    /// <see cref="WindowKind.Hann"/> with a one-shot stderr advisory per
    /// 37-RESEARCH.md Pattern E (charitable interpretation).
    /// </summary>
    private static WindowKind ResolveWindowKind(string sym, ExecutionContext ctx)
    {
        return sym switch
        {
            "hann" => WindowKind.Hann,
            "gaussian" => WindowKind.Gaussian,
            "tukey" => WindowKind.Tukey,
            _ => FallbackToHann(sym, ctx),
        };
    }

    private static WindowKind FallbackToHann(string sym, ExecutionContext ctx)
    {
        // Phase 44 Plan 44-06: strict-mode elevation per D-06/D-07.
        // Phase 44 review CR-03: dedup strict-elevated advisory per
        // ExecutionContext lifetime, mirroring the WarnOnce sentinel in
        // the non-strict path. Without this, every grain in
        // `(granular buf grain=1ms ...)` would record a fresh
        // ErrorReporter entry.
        var sentinel = $"granular:windowing:{sym}";
        if (ctx.CallerStrictMode)
        {
            if (ctx.StrictAdvisoryDedup.Add(sentinel))
            {
                ctx.ErrorReporter.ReportError(
                    $"[strict] [granular] unknown windowing symbol '#{sym}' — falling back to #hann. " +
                    "Valid options: #hann | #gaussian | #tukey.",
                    ctx.CurrentCallSite);
            }
            return WindowKind.Hann;
        }
        RenderingDiagnostics.WarnOnce(
            sentinel,
            $"[granular] unknown windowing symbol '#{sym}' — falling back to #hann. " +
            "Valid options: #hann | #gaussian | #tukey.");
        return WindowKind.Hann;
    }
}
