using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary;

/// <summary>
/// Phase 44 Plan 44-04 — explicit-conversion builtins. Six forward-direction
/// constructors map raw numeric / idempotent tagged input to the six tagged
/// music types (Decibel / Hertz / Millisecond / Second / Cent / Semitone),
/// and four reverse-direction extractors (<c>double</c>, <c>float</c>,
/// <c>int</c>, <c>long</c>) accept all six tagged music types.
///
/// <para>
/// Decisions implemented:
/// <list type="bullet">
///   <item>D-08 — five-pack overload shape (Int + Long + Float + Double +
///   idempotent target) for db / hz / ms / sec / cents. <c>(semitones x)</c>
///   is Int-ONLY per the SemitoneType whole-numbers-by-design contract
///   (<c>SemitoneType.cs:22-25</c> — <c>IsCompatibleWith(IntType)</c> only,
///   NOT Float/Double/Long); the Semitone-input idempotent overload also
///   ships so <c>(semitones +2st)</c> works.</item>
///   <item>D-09 — ALL six forward builtins are AVAILABLE IN BOTH MODES
///   (mode-independent registration). Composers refactoring TOWARD strict
///   can test-drive conversions one call at a time. Mirrors the
///   always-available <c>doubleToInt</c> / <c>intToDouble</c> precedent at
///   <c>BuiltInFunctions.cs:238-244</c>.</item>
///   <item>D-10 — reverse-direction backfill: 4 extractors × 6 tagged types
///   = 24 registrations. <c>(int 100ms)</c> floors via <c>Math.Floor</c>
///   matching the existing <c>doubleToInt</c> convention. Semitone is
///   Int-backed so the extractor reads <c>args[0].As&lt;int&gt;()</c>; the
///   other 5 tagged types are double-backed and read
///   <c>args[0].As&lt;double&gt;()</c>.</item>
/// </list>
/// </para>
///
/// <para>
/// Threat-model mitigations applied (Plan 44-04 §threat_model):
/// T-44-04-01 (lossy floor drift) — every <c>(int &lt;music type&gt;)</c>
/// uses <c>(int)Math.Floor(d)</c>, mirroring the existing
/// <c>StdLib.DoubleToInt</c> floor convention; negative-input case pinned
/// by <c>Fact_IntFromSecond_FloorsNegativeCorrectly</c>. T-44-04-02 (table
/// growth, ~50 entries) — accepted (linear scan over 413+ existing
/// registrations dominates). T-44-04-03 (information disclosure) — n/a
/// (pure conversions, no PRNG, no clock, no I/O — preserves two-run
/// cmp-clean determinism per CLAUDE.md Conventions).
/// </para>
/// </summary>
public static class ConversionFunctions
{
    /// <summary>
    /// Entry point — wires all 50 conversion overloads into the registry.
    /// Called from <see cref="BuiltInFunctions.RegisterAllImplementations"/>
    /// adjacent to the other modular Register* invocations. Mode-independent
    /// (D-09 + D-10): every overload registered here ships in both strict
    /// and non-strict files, with identical behavior.
    /// </summary>
    public static void Register(InternalFunctionRegistry registry)
    {
        // ===== Forward direction (raw numeric → tagged music type) =====
        RegisterDecibel(registry);
        RegisterHertz(registry);
        RegisterMillisecond(registry);
        RegisterSecond(registry);
        RegisterCent(registry);
        RegisterSemitone(registry);

        // ===== Reverse direction (tagged music type → raw numeric) =====
        RegisterReverseExtractors(registry);
    }

    /// <summary>
    /// D-08 — <c>(db x)</c>: Int / Long / Float / Double / Decibel-idempotent
    /// → Decibel. Five overloads. Float and Double share a materializer
    /// because Flow Float is CLR double per CLAUDE.md "Music Types Quick
    /// Reference". The Decibel-input overload preserves the underlying
    /// CLR double, so <c>(db -12dB)</c> is byte-identical to <c>-12dB</c>.
    /// </summary>
    private static void RegisterDecibel(InternalFunctionRegistry registry)
    {
        foreach (var (sourceType, materializer) in NumericFiveSourceTypes())
        {
            var sig = new FunctionSignature("db", [sourceType], ParameterNames: ["x"]);
            registry.Register("db", sig, args => Value.Decibel(materializer(args[0])));
        }
        // Idempotent target overload (D-08 "idempotent on target tagged type").
        var idemSig = new FunctionSignature("db", [DecibelType.Instance], ParameterNames: ["x"]);
        registry.Register("db", idemSig, args => Value.Decibel(args[0].As<double>()));
    }

    /// <summary>
    /// D-08 — <c>(hz x)</c>: Int / Long / Float / Double / Hertz-idempotent →
    /// Hertz. Hertz lex-time canonicalization (kHz → Hz × 1000 per
    /// <c>HertzType.cs:51-57</c>) is unaffected; <c>(hz 1.5kHz)</c> already
    /// arrives at the Hertz overload as a canonical 1500.0 Hertz value.
    /// </summary>
    private static void RegisterHertz(InternalFunctionRegistry registry)
    {
        foreach (var (sourceType, materializer) in NumericFiveSourceTypes())
        {
            var sig = new FunctionSignature("hz", [sourceType], ParameterNames: ["x"]);
            registry.Register("hz", sig, args => Value.Hertz(materializer(args[0])));
        }
        var idemSig = new FunctionSignature("hz", [HertzType.Instance], ParameterNames: ["x"]);
        registry.Register("hz", idemSig, args => Value.Hertz(args[0].As<double>()));
    }

    /// <summary>
    /// D-08 — <c>(ms x)</c>: Int / Long / Float / Double / Millisecond-idempotent
    /// → Millisecond.
    /// </summary>
    private static void RegisterMillisecond(InternalFunctionRegistry registry)
    {
        foreach (var (sourceType, materializer) in NumericFiveSourceTypes())
        {
            var sig = new FunctionSignature("ms", [sourceType], ParameterNames: ["x"]);
            registry.Register("ms", sig, args => Value.Millisecond(materializer(args[0])));
        }
        var idemSig = new FunctionSignature("ms", [MillisecondType.Instance], ParameterNames: ["x"]);
        registry.Register("ms", idemSig, args => Value.Millisecond(args[0].As<double>()));
    }

    /// <summary>
    /// D-08 — <c>(sec x)</c>: Int / Long / Float / Double / Second-idempotent
    /// → Second.
    /// </summary>
    private static void RegisterSecond(InternalFunctionRegistry registry)
    {
        foreach (var (sourceType, materializer) in NumericFiveSourceTypes())
        {
            var sig = new FunctionSignature("sec", [sourceType], ParameterNames: ["x"]);
            registry.Register("sec", sig, args => Value.Second(materializer(args[0])));
        }
        var idemSig = new FunctionSignature("sec", [SecondType.Instance], ParameterNames: ["x"]);
        registry.Register("sec", idemSig, args => Value.Second(args[0].As<double>()));
    }

    /// <summary>
    /// D-08 — <c>(cents x)</c>: Int / Long / Float / Double / Cent-idempotent
    /// → Cent.
    /// </summary>
    private static void RegisterCent(InternalFunctionRegistry registry)
    {
        foreach (var (sourceType, materializer) in NumericFiveSourceTypes())
        {
            var sig = new FunctionSignature("cents", [sourceType], ParameterNames: ["x"]);
            registry.Register("cents", sig, args => Value.Cent(materializer(args[0])));
        }
        var idemSig = new FunctionSignature("cents", [CentType.Instance], ParameterNames: ["x"]);
        registry.Register("cents", idemSig, args => Value.Cent(args[0].As<double>()));
    }

    /// <summary>
    /// D-08 carve-out — <c>(semitones x)</c> is Int-ONLY (whole-numbers-by-design
    /// per <c>SemitoneType.cs:22-25</c>). Float / Double / Long inputs fall
    /// through OverloadResolver to "No matching overload for 'semitones'" in
    /// BOTH modes. Semitone-input idempotent overload also ships so
    /// <c>(semitones +2st)</c> works. Semitone is the only tagged music type
    /// whose CLR backing is <c>int</c> (per <c>Value.Semitone(int)</c>
    /// factory at <c>Value.cs:34</c>) so the idempotent path reads
    /// <c>args[0].As&lt;int&gt;()</c>, NOT <c>As&lt;double&gt;()</c>.
    /// </summary>
    private static void RegisterSemitone(InternalFunctionRegistry registry)
    {
        var intSig = new FunctionSignature("semitones", [IntType.Instance], ParameterNames: ["x"]);
        registry.Register("semitones", intSig, args => Value.Semitone(args[0].As<int>()));

        var idemSig = new FunctionSignature("semitones", [SemitoneType.Instance], ParameterNames: ["x"]);
        registry.Register("semitones", idemSig, args => Value.Semitone(args[0].As<int>()));
    }

    /// <summary>
    /// D-10 — reverse-direction backfill. For each of the 4 extractors
    /// (<c>double</c>, <c>float</c>, <c>int</c>, <c>long</c>) and each of
    /// the 6 tagged music types (Decibel / Hertz / Cent / Millisecond /
    /// Second / Semitone), register one overload — 24 registrations total.
    /// Semitone is Int-backed; the other 5 are double-backed.
    /// <c>(int &lt;fractional music type&gt;)</c> floors via <c>Math.Floor</c>
    /// matching the existing <c>StdLib.DoubleToInt</c> convention
    /// (T-44-04-01 mitigation).
    /// </summary>
    private static void RegisterReverseExtractors(InternalFunctionRegistry registry)
    {
        var musicTypes = new FlowType[]
        {
            DecibelType.Instance,
            HertzType.Instance,
            CentType.Instance,
            MillisecondType.Instance,
            SecondType.Instance,
            SemitoneType.Instance,
        };

        foreach (var t in musicTypes)
        {
            bool isSemitone = ReferenceEquals(t, SemitoneType.Instance);

            // (double <music type>) — Semitone uses As<int>(), others As<double>().
            var dblSig = new FunctionSignature("double", [t], ParameterNames: ["value"]);
            registry.Register("double", dblSig, args =>
                Value.Double(isSemitone ? args[0].As<int>() : args[0].As<double>()));

            // (float <music type>) — Float is CLR double per CLAUDE.md.
            var fltSig = new FunctionSignature("float", [t], ParameterNames: ["value"]);
            registry.Register("float", fltSig, args =>
                Value.Float(isSemitone ? args[0].As<int>() : args[0].As<double>()));

            // (int <music type>) — floor for fractional types; identity for Semitone (Int-backed).
            var intSig = new FunctionSignature("int", [t], ParameterNames: ["value"]);
            registry.Register("int", intSig, args =>
                Value.Int(isSemitone ? args[0].As<int>() : (int)Math.Floor(args[0].As<double>())));

            // (long <music type>) — floor for fractional types; identity for Semitone.
            var lngSig = new FunctionSignature("long", [t], ParameterNames: ["value"]);
            registry.Register("long", lngSig, args =>
                Value.Long(isSemitone ? args[0].As<int>() : (long)Math.Floor(args[0].As<double>())));
        }

        // Phase 44 Plan 44-09 Task 2 — primitive numeric cross-casts. Without
        // these, strict-mode composers have no escape hatch for cross-type
        // comparisons / arithmetic: `(double 1)` would fail overload resolution
        // because Int → Double widening is disabled under strict (Plan 44-03).
        // Each of the 4 extractors (double/float/int/long) accepts every
        // primitive numeric source (Int/Long/Float/Double). Identity casts
        // (e.g. `(double 1.0)`) are no-ops; widening / narrowing follows the
        // CLAUDE.md "Float × Double is fine" + StdLib.DoubleToInt floor
        // convention.
        var numericPrims = new (FlowType src, Func<Value, double> toDbl, Func<Value, long> toLng)[]
        {
            (IntType.Instance,    v => (double)v.As<int>(),    v => (long)v.As<int>()),
            (LongType.Instance,   v => (double)v.As<long>(),   v => v.As<long>()),
            (FloatType.Instance,  v => v.As<double>(),         v => (long)Math.Floor(v.As<double>())),
            (DoubleType.Instance, v => v.As<double>(),         v => (long)Math.Floor(v.As<double>())),
        };

        foreach (var (src, toDbl, toLng) in numericPrims)
        {
            // (double Int|Long|Float|Double)
            var dblSig = new FunctionSignature("double", [src], ParameterNames: ["value"]);
            registry.Register("double", dblSig, args => Value.Double(toDbl(args[0])));

            // (float Int|Long|Float|Double) — Flow Float is CLR double.
            var fltSig = new FunctionSignature("float", [src], ParameterNames: ["value"]);
            registry.Register("float", fltSig, args => Value.Float(toDbl(args[0])));

            // (int Int|Long|Float|Double) — floor matches StdLib.DoubleToInt.
            var intSig = new FunctionSignature("int", [src], ParameterNames: ["value"]);
            registry.Register("int", intSig, args =>
                Value.Int(src == IntType.Instance ? args[0].As<int>() : (int)toLng(args[0])));

            // (long Int|Long|Float|Double)
            var lngSig = new FunctionSignature("long", [src], ParameterNames: ["value"]);
            registry.Register("long", lngSig, args => Value.Long(toLng(args[0])));
        }
    }

    /// <summary>
    /// Shared numeric-source-type table for the 5-overload pack
    /// (Int / Long / Float / Double + one slot for the idempotent target).
    /// Returned as a 4-tuple of <c>(FlowType, Func&lt;Value, double&gt;)</c>
    /// — the idempotent slot is registered separately in each Register*
    /// method because the target type differs per builtin (Decibel vs.
    /// Hertz vs. ...).
    /// </summary>
    private static IEnumerable<(FlowType sourceType, Func<Value, double> materializer)>
        NumericFiveSourceTypes()
    {
        yield return (IntType.Instance,    v => (double)v.As<int>());
        yield return (LongType.Instance,   v => (double)v.As<long>());
        yield return (FloatType.Instance,  v => v.As<double>());   // Flow Float is CLR double
        yield return (DoubleType.Instance, v => v.As<double>());
    }
}
