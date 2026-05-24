using FlowLang.Audio;
using FlowLang.Runtime;
using FlowLang.StandardLibrary.Dict;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;

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
        var setMaxIterSignature = new FunctionSignature("setMaxIterations", [IntType.Instance],
            ParameterNames: ["max"]);
        registry.Register("setMaxIterations", setMaxIterSignature, args =>
        {
            context.MaxIterations = args[0].As<int>();
            return Value.Void();
        });
    }

    /// <summary>
    /// Registers all C# implementations of internal functions.
    /// </summary>
    public static void RegisterAllImplementations(InternalFunctionRegistry registry)
    {
        RegisterStdLib(registry);
        RegisterMath(registry);
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
        Audio.InputFunctions.Register(registry);  // Phase 38 AUDIO-IN-01/02 — (micBuffer duration) capture
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
        var lenStrSignature = new FunctionSignature("len", [StringType.Instance],
            ParameterNames: ["s"]);
        registry.Register("len", lenStrSignature, StdLib.LenString);
        
        // ===== I/O Functions =====
        var printSignature = new FunctionSignature(
            "print",
            [StringType.Instance],
            ParameterNames: ["s"]);
        registry.Register("print", printSignature, StdLib.Print);

        // ===== String Conversion Functions =====

        var strIntSignature = new FunctionSignature("str", [IntType.Instance],
            ParameterNames: ["value"]);
        registry.Register("str", strIntSignature, StdLib.StrInt);

        var strFloatSignature = new FunctionSignature("str", [FloatType.Instance],
            ParameterNames: ["value"]);
        registry.Register("str", strFloatSignature, StdLib.StrFloat);

        var strDoubleSignature = new FunctionSignature("str", [DoubleType.Instance],
            ParameterNames: ["value"]);
        registry.Register("str", strDoubleSignature, StdLib.StrDouble);

        // Phase 26 (STD-02): str overloads for Long + Number — without these,
        // (str Long) is ambiguous (widens to both Float and Double) and (str Number)
        // has no candidate (Number doesn't widen on the str chain).
        var strLongSignature = new FunctionSignature("str", [LongType.Instance],
            ParameterNames: ["value"]);
        registry.Register("str", strLongSignature, StdLib.StrLong);
        var strNumberSignature = new FunctionSignature("str", [NumberType.Instance],
            ParameterNames: ["value"]);
        registry.Register("str", strNumberSignature, StdLib.StrNumber);

        var strStringSignature = new FunctionSignature("str", [StringType.Instance],
            ParameterNames: ["value"]);
        registry.Register("str", strStringSignature, StdLib.StrString);

        var strBoolSignature = new FunctionSignature("str", [BoolType.Instance],
            ParameterNames: ["value"]);
        registry.Register("str", strBoolSignature, StdLib.StrBool);

        var strNoteSignature = new FunctionSignature("str", [NoteType.Instance],
            ParameterNames: ["value"]);
        registry.Register("str", strNoteSignature, StdLib.StrNote);

        // Phase 26.1 SYM-01: (str Symbol) → "#name"
        var strSymbolSignature = new FunctionSignature("str", [SymbolType.Instance],
            ParameterNames: ["value"]);
        registry.Register("str", strSymbolSignature, StdLib.StrSymbol);

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

        var strArraySignature = new FunctionSignature("str", [new ArrayType(VoidType.Instance)],
            ParameterNames: ["value"]);
        registry.Register("str", strArraySignature, StdLib.StrArray);

        var strSequenceSignature = new FunctionSignature("str", [SequenceType.Instance],
            ParameterNames: ["value"]);
        registry.Register("str", strSequenceSignature, args =>
        {
            var seq = args[0].As<SequenceData>();
            return Value.String(seq.ToString());
        });

        var concatSignature = new FunctionSignature("concat", [StringType.Instance, StringType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("concat", concatSignature, StdLib.Concat);

        // ===== Type Conversion Functions =====

        var intToDoubleSignature = new FunctionSignature("intToDouble", [IntType.Instance],
            ParameterNames: ["value"]);
        registry.Register("intToDouble", intToDoubleSignature, StdLib.IntToDouble);

        var doubleToIntSignature = new FunctionSignature("doubleToInt", [DoubleType.Instance],
            ParameterNames: ["value"]);
        registry.Register("doubleToInt", doubleToIntSignature, StdLib.DoubleToInt);

        // ===== Arithmetic Functions =====

        var addIntSignature = new FunctionSignature(
            "add",
            [IntType.Instance, IntType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("add", addIntSignature, StdLib.AddInt);

        var addFloatSignature = new FunctionSignature(
            "add",
            [FloatType.Instance, FloatType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("add", addFloatSignature, StdLib.AddFloat);

        var subFloatSignature = new FunctionSignature(
            "sub",
            [FloatType.Instance, FloatType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("sub", subFloatSignature, StdLib.SubFloat);

        var mulFloatSignature = new FunctionSignature(
            "mul",
            [FloatType.Instance, FloatType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("mul", mulFloatSignature, StdLib.MulFloat);

        var divFloatSignature = new FunctionSignature(
            "div",
            [FloatType.Instance, FloatType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("div", divFloatSignature, StdLib.DivFloat);

        var subSignature = new FunctionSignature(
            "sub",
            [IntType.Instance, IntType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("sub", subSignature, StdLib.SubInt);

        var mulSignature = new FunctionSignature(
            "mul",
            [IntType.Instance, IntType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("mul", mulSignature, StdLib.MulInt);

        var divSignature = new FunctionSignature(
            "div",
            [IntType.Instance, IntType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("div", divSignature, StdLib.DivIntPromote);   // Phase 26 D-08: now returns Double

        // Double overloads for arithmetic
        var addDoubleSignature = new FunctionSignature(
            "add",
            [DoubleType.Instance, DoubleType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("add", addDoubleSignature, StdLib.AddDouble);

        var subDoubleSignature = new FunctionSignature(
            "sub",
            [DoubleType.Instance, DoubleType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("sub", subDoubleSignature, StdLib.SubDouble);

        var mulDoubleSignature = new FunctionSignature(
            "mul",
            [DoubleType.Instance, DoubleType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("mul", mulDoubleSignature, StdLib.MulDouble);

        var divDoubleSignature = new FunctionSignature(
            "div",
            [DoubleType.Instance, DoubleType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("div", divDoubleSignature, StdLib.DivDouble);

        // ===== Phase 26 (STD-02): Long + Number same-type fast paths =====

        var addLongSignature = new FunctionSignature("add", [LongType.Instance, LongType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("add", addLongSignature, StdLib.AddLong);
        var subLongSignature = new FunctionSignature("sub", [LongType.Instance, LongType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("sub", subLongSignature, StdLib.SubLong);
        var mulLongSignature = new FunctionSignature("mul", [LongType.Instance, LongType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("mul", mulLongSignature, StdLib.MulLong);
        var divLongSignature = new FunctionSignature("div", [LongType.Instance, LongType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("div", divLongSignature, StdLib.DivLong);

        var addNumberSignature = new FunctionSignature("add", [NumberType.Instance, NumberType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("add", addNumberSignature, StdLib.AddNumber);
        var subNumberSignature = new FunctionSignature("sub", [NumberType.Instance, NumberType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("sub", subNumberSignature, StdLib.SubNumber);
        var mulNumberSignature = new FunctionSignature("mul", [NumberType.Instance, NumberType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("mul", mulNumberSignature, StdLib.MulNumber);
        var divNumberSignature = new FunctionSignature("div", [NumberType.Instance, NumberType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("div", divNumberSignature, StdLib.DivNumber);

        // ===== Phase 26 (STD-02): (neg) 5-pack (D-07) =====
        var negIntSignature    = new FunctionSignature("neg", [IntType.Instance],
            ParameterNames: ["value"]);
        registry.Register("neg", negIntSignature, StdLib.NegInt);
        var negLongSignature   = new FunctionSignature("neg", [LongType.Instance],
            ParameterNames: ["value"]);
        registry.Register("neg", negLongSignature, StdLib.NegLong);
        var negFloatSignature  = new FunctionSignature("neg", [FloatType.Instance],
            ParameterNames: ["value"]);
        registry.Register("neg", negFloatSignature, StdLib.NegFloat);
        var negDoubleSignature = new FunctionSignature("neg", [DoubleType.Instance],
            ParameterNames: ["value"]);
        registry.Register("neg", negDoubleSignature, StdLib.NegDouble);
        var negNumberSignature = new FunctionSignature("neg", [NumberType.Instance],
            ParameterNames: ["value"]);
        registry.Register("neg", negNumberSignature, StdLib.NegNumber);

        // ===== Phase 26 (STD-02): (idiv Int Int) → Int (D-08) =====
        var idivIntSignature = new FunctionSignature("idiv", [IntType.Instance, IntType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("idiv", idivIntSignature, StdLib.IDivInt);

        // String-to-number conversions
        var stringToIntSignature = new FunctionSignature("stringToInt", [StringType.Instance],
            ParameterNames: ["s"]);
        registry.Register("stringToInt", stringToIntSignature, StdLib.StringToInt);

        var stringToDoubleSignature = new FunctionSignature("stringToDouble", [StringType.Instance],
            ParameterNames: ["s"]);
        registry.Register("stringToDouble", stringToDoubleSignature, StdLib.StringToDouble);

        // ===== Lazy Evaluation Functions =====

        // Note: eval is registered with Lazy<Void> but will work with any Lazy<T>
        // due to special handling in the implementation
        var evalSignature = new FunctionSignature(
            "eval",
            [new LazyType(VoidType.Instance)],
            ParameterNames: ["thunk"]);
        registry.Register("eval", evalSignature, StdLib.Eval);
        
        var ifSignature = new FunctionSignature(
            "if", [BoolType.Instance, new LazyType(VoidType.Instance), new LazyType(VoidType.Instance)],
            ParameterNames: ["cond", "then", "else"]);
        registry.Register("if", ifSignature, StdLib.If);

        // Strict (non-Lazy) if overload — Void-wildcard covers all Bool-T-T concrete shapes
        // (String/String, Double/Double, Int/Int, etc.). The Lazy overload above has higher
        // specificity for Lazy<Void> args, so it wins when args are lazy-wrapped.
        var ifStrictSignature = new FunctionSignature(
            "if", [BoolType.Instance, VoidType.Instance, VoidType.Instance],
            ParameterNames: ["cond", "then", "else"]);
        registry.Register("if", ifStrictSignature, StdLib.IfStrict);


        var andSignature = new FunctionSignature(
            "and", [new LazyType(BoolType.Instance), new LazyType(BoolType.Instance)],
            ParameterNames: ["a", "b"]);
        registry.Register("and", andSignature, StdLib.And);
        
        var andBoolSignature = new FunctionSignature(
            "and", [BoolType.Instance, BoolType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("and", andBoolSignature, StdLib.AndBool);
        
        var orSignature = new FunctionSignature(
            "or", [new LazyType(BoolType.Instance), new LazyType(BoolType.Instance)],
            ParameterNames: ["a", "b"]);
        registry.Register("or", orSignature, StdLib.Or);
        
        var orBoolSignature = new FunctionSignature(
            "or", [BoolType.Instance, BoolType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("or", orBoolSignature, StdLib.OrBool);

        // ===== Equality and Comparison Functions =====
        // VoidType.Instance is used as a wildcard/"any type" parameter in these signatures.
        // The overload resolver treats Void as compatible with all types, allowing these
        // functions to accept arguments of any type.

        var equalsSignature = new FunctionSignature(
            "equals",
            [VoidType.Instance, VoidType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("equals", equalsSignature, StdLib.Equals);

        var sequalsSignature = new FunctionSignature(
            "sequals",
            [VoidType.Instance, VoidType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("sequals", sequalsSignature, StdLib.StrictEquals);

        var ltSignature = new FunctionSignature(
            "lt",
            [VoidType.Instance, VoidType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("lt", ltSignature, StdLib.LessThan);

        var gtSignature = new FunctionSignature(
            "gt",
            [VoidType.Instance, VoidType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("gt", gtSignature, StdLib.GreaterThan);

        var lteSignature = new FunctionSignature(
            "lte",
            [VoidType.Instance, VoidType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("lte", lteSignature, StdLib.LessThanOrEqual);

        var gteSignature = new FunctionSignature(
            "gte",
            [VoidType.Instance, VoidType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("gte", gteSignature, StdLib.GreaterThanOrEqual);
        
        // (Moved random functions to RegisterContextDependentFunctions)
    }

    private static void RegisterMath(InternalFunctionRegistry registry)
    {
        // ===== Trigonometric Functions =====
        registry.Register("sin", new FunctionSignature("sin", [DoubleType.Instance],
                ParameterNames: ["x"]),
            args => Value.Double(Math.Sin(args[0].As<double>())));

        registry.Register("cos", new FunctionSignature("cos", [DoubleType.Instance],
                ParameterNames: ["x"]),
            args => Value.Double(Math.Cos(args[0].As<double>())));

        registry.Register("tan", new FunctionSignature("tan", [DoubleType.Instance],
                ParameterNames: ["x"]),
            args => Value.Double(Math.Tan(args[0].As<double>())));

        // ===== Absolute Value =====
        registry.Register("abs", new FunctionSignature("abs", [DoubleType.Instance],
                ParameterNames: ["x"]),
            args => Value.Double(Math.Abs(args[0].As<double>())));

        registry.Register("abs", new FunctionSignature("abs", [IntType.Instance],
                ParameterNames: ["x"]),
            args => Value.Int(Math.Abs(args[0].As<int>())));

        // ===== Square Root =====
        registry.Register("sqrt", new FunctionSignature("sqrt", [DoubleType.Instance],
                ParameterNames: ["x"]),
            args => Value.Double(Math.Sqrt(args[0].As<double>())));

        // ===== Min / Max =====
        registry.Register("min", new FunctionSignature("min", [DoubleType.Instance, DoubleType.Instance],
                ParameterNames: ["a", "b"]),
            args => Value.Double(Math.Min(args[0].As<double>(), args[1].As<double>())));

        registry.Register("min", new FunctionSignature("min", [IntType.Instance, IntType.Instance],
                ParameterNames: ["a", "b"]),
            args => Value.Int(Math.Min(args[0].As<int>(), args[1].As<int>())));

        registry.Register("max", new FunctionSignature("max", [DoubleType.Instance, DoubleType.Instance],
                ParameterNames: ["a", "b"]),
            args => Value.Double(Math.Max(args[0].As<double>(), args[1].As<double>())));

        registry.Register("max", new FunctionSignature("max", [IntType.Instance, IntType.Instance],
                ParameterNames: ["a", "b"]),
            args => Value.Int(Math.Max(args[0].As<int>(), args[1].As<int>())));

        // ===== Rounding =====
        registry.Register("floor", new FunctionSignature("floor", [DoubleType.Instance],
                ParameterNames: ["x"]),
            args => Value.Int((int)Math.Floor(args[0].As<double>())));

        registry.Register("ceil", new FunctionSignature("ceil", [DoubleType.Instance],
                ParameterNames: ["x"]),
            args => Value.Int((int)Math.Ceiling(args[0].As<double>())));

        registry.Register("round", new FunctionSignature("round", [DoubleType.Instance],
                ParameterNames: ["x"]),
            args => Value.Int((int)Math.Round(args[0].As<double>())));

        // ===== Power / Logarithm =====
        registry.Register("pow", new FunctionSignature("pow", [DoubleType.Instance, DoubleType.Instance],
                ParameterNames: ["base", "exp"]),
            args => Value.Double(Math.Pow(args[0].As<double>(), args[1].As<double>())));

        registry.Register("log", new FunctionSignature("log", [DoubleType.Instance],
                ParameterNames: ["x"]),
            args => Value.Double(Math.Log(args[0].As<double>())));

        // ===== Constants =====
        registry.Register("pi", new FunctionSignature("pi", [],
                ParameterNames: []),
            args => Value.Double(Math.PI));

        registry.Register("tau", new FunctionSignature("tau", [],
                ParameterNames: []),
            args => Value.Double(Math.Tau));

        // Nothing() -> Void. The explicit-void escape hatch for `return (Nothing)`
        // when a proc would otherwise collect non-void expressions before its end.
        registry.Register("Nothing", new FunctionSignature("Nothing", [],
                ParameterNames: []),
            args => Value.Void());

        // ===== Phase 26.1 Beat constructor (DICT-01 Tuple-of-hashables acceptance) =====
        // Flow has no `Beat` literal at top level — durations like `q`, `h`, `e`, `s`, `w`
        // exist only as note-stream suffixes (inside `| C4q D4h |`). DICT-01's
        // Tuple-of-hashables key acceptance needs to construct Beat values in user source.
        // (beat Double) wraps a fractional-beat double in a Beat-typed Value so that
        // `<<C4, (beat 0.25)>>` produces a Tuple<<Note, Beat>> usable as a Dict key.
        registry.Register("beat", new FunctionSignature("beat", [DoubleType.Instance],
                ParameterNames: ["value"]),
            args => Value.Beat(args[0].As<double>()));

        // ===== Phase 26.1 NaN production primitive (REVISION 2) =====
        // Flow has no `nan` literal. (div 0.0 0.0) throws "Division by zero"
        // (see StdLib.DivFloat/DivInt/DivLong/DivDouble — all guard b == 0).
        // (nanFloat) is the canonical IEEE 754 NaN producer for the DICT-03
        // NaN-as-key acceptance shape and any future float-edge-case work.
        // Returns Float (double-backed per Value.Float definition).
        registry.Register("nanFloat", new FunctionSignature("nanFloat", [],
                ParameterNames: []),
            args => Value.Float(double.NaN));
    }

    private static void RegisterCollections(InternalFunctionRegistry registry)
    {
        // ===== Array Functions =====

        var listSignature = new FunctionSignature(
            "list",
            [VoidType.Instance],
            IsVarArgs: true);
        registry.Register("list", listSignature, Collections.List);

        var lenSignature = new FunctionSignature("len", [new ArrayType(VoidType.Instance)],
            ParameterNames: ["arr"]);
        registry.Register("len", lenSignature, Collections.Len);
        
        var headSignature = new FunctionSignature("head", [new ArrayType(VoidType.Instance)],
            ParameterNames: ["arr"]);
        registry.Register("head", headSignature, Collections.Head);

        var tailSignature = new FunctionSignature("tail", [new ArrayType(VoidType.Instance)],
            ParameterNames: ["arr"]);
        registry.Register("tail", tailSignature, Collections.Tail);

        var lastSignature = new FunctionSignature("last", [new ArrayType(VoidType.Instance)],
            ParameterNames: ["arr"]);
        registry.Register("last", lastSignature, Collections.Last);

        var initSignature = new FunctionSignature("init", [new ArrayType(VoidType.Instance)],
            ParameterNames: ["arr"]);
        registry.Register("init", initSignature, Collections.Init);

        var emptySignature = new FunctionSignature("empty", [new ArrayType(VoidType.Instance)],
            ParameterNames: ["arr"]);
        registry.Register("empty", emptySignature, Collections.Empty);

        var reverseSignature = new FunctionSignature("reverse", [new ArrayType(VoidType.Instance)],
            ParameterNames: ["arr"]);
        registry.Register("reverse", reverseSignature, Collections.Reverse);

        var takeSignature = new FunctionSignature("take", [new ArrayType(VoidType.Instance), IntType.Instance],
            ParameterNames: ["arr", "n"]);
        registry.Register("take", takeSignature, Collections.Take);

        var dropSignature = new FunctionSignature("drop", [new ArrayType(VoidType.Instance), IntType.Instance],
            ParameterNames: ["arr", "n"]);
        registry.Register("drop", dropSignature, Collections.Drop);

        // DEFER-01 (Phase 20 plan 20-01): range(Int, Int) + range(Int, Int, Int) -> Array[Int].
        // Standard Pythonic semantics. Two arities registered explicitly (overload resolver disambiguates by exact arity match per 20-RESEARCH Pitfall 3).
        var range2Signature = new FunctionSignature("range", [IntType.Instance, IntType.Instance],
            ParameterNames: ["start", "end"]);
        registry.Register("range", range2Signature, Collections.Range);

        var range3Signature = new FunctionSignature("range", [IntType.Instance, IntType.Instance, IntType.Instance],
            ParameterNames: ["start", "end", "step"]);
        registry.Register("range", range3Signature, Collections.Range);

        // DX-05 (Phase 14 plan 14-01): slice(Array[T], Int, Int) + slice(Sequence, Int, Int).
        // Silent two-sided clamping per CONTEXT D-01. Both overloads ship atomically per D-02.
        // Overload resolver disambiguates by arg 0 type (Array vs Sequence).
        var sliceArraySignature = new FunctionSignature("slice",
            [new ArrayType(VoidType.Instance), IntType.Instance, IntType.Instance],
            ParameterNames: ["arr", "start", "end"]);
        registry.Register("slice", sliceArraySignature, Collections.SliceArray);

        var sliceSeqSignature = new FunctionSignature("slice",
            [SequenceType.Instance, IntType.Instance, IntType.Instance],
            ParameterNames: ["seq", "start", "end"]);
        registry.Register("slice", sliceSeqSignature, Collections.SliceSequence);

        var appendSignature = new FunctionSignature("append", [new ArrayType(VoidType.Instance), VoidType.Instance],
            ParameterNames: ["arr", "element"]);
        registry.Register("append", appendSignature, Collections.Append);

        var prependSignature = new FunctionSignature("prepend", [VoidType.Instance, new ArrayType(VoidType.Instance)],
            ParameterNames: ["element", "arr"]);
        registry.Register("prepend", prependSignature, Collections.Prepend);

        // Note: "concat" is intentionally overloaded for both strings (in RegisterStdLib)
        // and arrays (here). The overload resolver selects the correct one by argument types.
        var concatSignature = new FunctionSignature("concat", [new ArrayType(VoidType.Instance), new ArrayType(VoidType.Instance)],
            ParameterNames: ["a", "b"]);
        registry.Register("concat", concatSignature, Collections.Concat);

        var containsSignature = new FunctionSignature("contains", [new ArrayType(VoidType.Instance), VoidType.Instance],
            ParameterNames: ["arr", "element"]);
        registry.Register("contains", containsSignature, Collections.Contains);
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

        var mixSignature = new FunctionSignature("mix", [BufferType.Instance, BufferType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("mix", mixSignature, Audio.AudioCore.Mix);

        // ===== File I/O Operations =====

        // exportWav(Buffer, String) - default 16-bit
        var exportWavSignature = new FunctionSignature(
            "exportWav",
            [BufferType.Instance, StringType.Instance],
            ParameterNames: ["buf", "path"]);
        registry.Register("exportWav", exportWavSignature, Audio.FileIO.ExportWav);

        // exportWav(Buffer, String, Int) - custom bit depth
        var exportWavWithDepthSignature = new FunctionSignature(
            "exportWav",
            [BufferType.Instance, StringType.Instance, IntType.Instance],
            ParameterNames: ["buf", "path", "bitDepth"]);
        registry.Register("exportWav", exportWavWithDepthSignature, Audio.FileIO.ExportWavWithBitDepth);

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

        // loadWav(String) -> Buffer - load WAV file
        var loadWavSignature = new FunctionSignature("loadWav", [StringType.Instance],
            ParameterNames: ["path"]);
        registry.Register("loadWav", loadWavSignature, Audio.FileIO.LoadWav);

        // DX-15: loadWav(String, Int) -> Buffer — varispeed by semitones (Phase 22 plan 22-02)
        var loadWavSemiSig = new FunctionSignature("loadWav",
            [StringType.Instance, IntType.Instance],
            ParameterNames: ["path", "semitones"]);
        registry.Register("loadWav", loadWavSemiSig, Audio.FileIO.LoadWavSemitones);

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

        var createClipSig = new FunctionSignature("createClip", [DoubleType.Instance, DoubleType.Instance],
            ParameterNames: ["duration", "amplitude"]);
        registry.Register("createClip", createClipSig, Audio.SignalGeneration.CreateClip);

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

        var fadeInSignature = new FunctionSignature(
            "fadeIn",
            [BufferType.Instance, DoubleType.Instance],
            ParameterNames: ["buf", "duration"]);
        registry.Register("fadeIn", fadeInSignature, Audio.BufferHelpers.FadeIn);

        var fadeOutSignature = new FunctionSignature(
            "fadeOut",
            [BufferType.Instance, DoubleType.Instance],
            ParameterNames: ["buf", "duration"]);
        registry.Register("fadeOut", fadeOutSignature, Audio.BufferHelpers.FadeOut);

        // ===== Envelope Operations =====

        var createARSignature = new FunctionSignature(
            "createAR",
            [DoubleType.Instance, DoubleType.Instance, IntType.Instance],
            ParameterNames: ["attack", "release", "sampleRate"]);
        registry.Register("createAR", createARSignature, Audio.EnvelopeProcessor.CreateAR);

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
        Transforms.TransformFunctions.RegisterContextDependent(registry, context);  // Phase 22-05 DX-13 (quantize reads MusicalContext.TimeSignature)
        Audio.Vocalization.VocalizationFunctions.RegisterContextDependent(registry, context);  // Phase 23-02 Task 3 (sing reads MusicalContext.Tuning via SongRenderer.ResolveRenderTuning)
        Audio.MidiExport.RegisterContextDependent(registry, context);  // Phase 23-03 Task 2 D-13 (writeMidi reads MusicalContext.Tuning for non-12-TET advisory)
        // Phase 35 Plan 35-04 TEST-01 — (test "name" body) defers via Lazy<Void>
        // wrap (Pitfall 10 LOAD-BEARING) + 5 assertion primitives. Context-
        // dependent because (test ...) appends a TestRecord to
        // context.TestRegistry; assertions throw AssertionException which
        // TestRunner catches to convert into FAIL outcomes.
        TestFramework.TestFunctions.RegisterTestFramework(registry, context);
        // ===== Random Generator Functions =====

        var randSignature = new FunctionSignature("?", [],
            ParameterNames: []);
        registry.Register("?", randSignature, args => StdLib.Rand(args, context));

        var fixedRandSignature = new FunctionSignature("??", [],
            ParameterNames: []);
        registry.Register("??", fixedRandSignature, args => StdLib.FixedRand(args, context));

        var resetRandSignature = new FunctionSignature("??reset", [],
            ParameterNames: []);
        registry.Register("??reset", resetRandSignature, args => StdLib.FixedRandReset(args, context));

        var setRandSignature = new FunctionSignature("??set", [IntType.Instance],
            ParameterNames: ["seed"]);
        registry.Register("??set", setRandSignature, args => StdLib.FixedRandSet(args, context));

        // ===== Higher-Order Functions =====

        var eachSignature = new FunctionSignature("each", [new ArrayType(VoidType.Instance), FunctionType.Instance],
            ParameterNames: ["arr", "fn"]);
        registry.Register("each", eachSignature, args => Collections.Each(args, context));

        var mapSignature = new FunctionSignature("map", [new ArrayType(VoidType.Instance), FunctionType.Instance],
            ParameterNames: ["arr", "fn"]);
        registry.Register("map", mapSignature, args => Collections.Map(args, context));

        var filterSignature = new FunctionSignature("filter", [new ArrayType(VoidType.Instance), FunctionType.Instance],
            ParameterNames: ["arr", "pred"]);
        registry.Register("filter", filterSignature, args => Collections.Filter(args, context));

        var reduceSignature = new FunctionSignature("reduce", [new ArrayType(VoidType.Instance), VoidType.Instance, FunctionType.Instance],
            ParameterNames: ["arr", "initial", "fn"]);
        registry.Register("reduce", reduceSignature, args => Collections.Reduce(args, context));

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

        // Phase 26.1 dict + tuple-unpack runtime functions (TUP-11 + DICT-01/02/03)
        RegisterDict(registry, context);
    }

    private static void RegisterDict(InternalFunctionRegistry registry, FlowLang.Runtime.ExecutionContext context)
    {
        // ===== (unpack) — runtime first-class apply (TUP-11) — Wave 3 =====
        var unpackSig = new FunctionSignature(
            "unpack",
            new FlowType[] { TupleType.AnyArity, FunctionType.Instance },
            ParameterNames: ["tup", "fn"]);
        registry.Register("unpack", unpackSig,
            args => DictFunctions.Unpack(args, context));

        // ===== Dict ops (DICT-01/02/03) — Wave 4 =====

        // Wildcard Dict<Void, Void> for overload-resolution dispatch — VoidType key
        // is exempted from DictType's defensive IsHashable check.
        var dictWildcard = new DictType(VoidType.Instance, VoidType.Instance);

        // (dict K V K V ...) — flat varargs constructor
        var dictSig = new FunctionSignature("dict",
            new FlowType[] { VoidType.Instance }, IsVarArgs: true);
        registry.Register("dict", dictSig, args => DictFunctions.Dict(args, context));

        // (dictTuple <<K,V>> ...) — tuple-pair varargs constructor
        var dictTupleSig = new FunctionSignature("dictTuple",
            new FlowType[] { TupleType.AnyArity }, IsVarArgs: true);
        registry.Register("dictTuple", dictTupleSig, args => DictFunctions.DictTuple(args, context));

        // (get d k)
        var getSig = new FunctionSignature("get",
            new FlowType[] { dictWildcard, VoidType.Instance },
            ParameterNames: ["d", "k"]);
        registry.Register("get", getSig, args => DictFunctions.Get(args, context));

        // (getOr d k default)
        var getOrSig = new FunctionSignature("getOr",
            new FlowType[] { dictWildcard, VoidType.Instance, VoidType.Instance },
            ParameterNames: ["d", "k", "default"]);
        registry.Register("getOr", getOrSig, args => DictFunctions.GetOr(args, context));

        // (set d k v)
        var setSig = new FunctionSignature("set",
            new FlowType[] { dictWildcard, VoidType.Instance, VoidType.Instance },
            ParameterNames: ["d", "k", "v"]);
        registry.Register("set", setSig, args => DictFunctions.Set(args, context));

        // (remove d k)
        var removeSig = new FunctionSignature("remove",
            new FlowType[] { dictWildcard, VoidType.Instance },
            ParameterNames: ["d", "k"]);
        registry.Register("remove", removeSig, args => DictFunctions.Remove(args, context));

        // (has d k)
        var hasSig = new FunctionSignature("has",
            new FlowType[] { dictWildcard, VoidType.Instance },
            ParameterNames: ["d", "k"]);
        registry.Register("has", hasSig, args => DictFunctions.Has(args, context));

        // (keys d)
        var keysSig = new FunctionSignature("keys", new FlowType[] { dictWildcard },
            ParameterNames: ["d"]);
        registry.Register("keys", keysSig, args => DictFunctions.Keys(args, context));

        // (values d)
        var valuesSig = new FunctionSignature("values", new FlowType[] { dictWildcard },
            ParameterNames: ["d"]);
        registry.Register("values", valuesSig, args => DictFunctions.Values(args, context));

        // (size d) — Int
        var sizeSig = new FunctionSignature("size", new FlowType[] { dictWildcard },
            ParameterNames: ["d"]);
        registry.Register("size", sizeSig, args => DictFunctions.Size(args, context));

        // (merge d1 d2) — last-write-wins
        var mergeSig = new FunctionSignature("merge",
            new FlowType[] { dictWildcard, dictWildcard },
            ParameterNames: ["d1", "d2"]);
        registry.Register("merge", mergeSig, args => DictFunctions.Merge(args, context));

        // (each Dict Function) — SEPARATE overload from existing (each Array Function); Pitfall 6
        var eachDictSig = new FunctionSignature("each",
            new FlowType[] { dictWildcard, FunctionType.Instance },
            ParameterNames: ["d", "fn"]);
        registry.Register("each", eachDictSig, args => DictFunctions.Each(args, context));

        // (map Dict Function) — SEPARATE overload from existing (map Array Function)
        var mapDictSig = new FunctionSignature("map",
            new FlowType[] { dictWildcard, FunctionType.Instance },
            ParameterNames: ["d", "fn"]);
        registry.Register("map", mapDictSig, args => DictFunctions.Map(args, context));

        // (filter Dict Function) — SEPARATE overload from existing (filter Array Function)
        var filterDictSig = new FunctionSignature("filter",
            new FlowType[] { dictWildcard, FunctionType.Instance },
            ParameterNames: ["d", "pred"]);
        registry.Register("filter", filterDictSig, args => DictFunctions.Filter(args, context));
    }

    private static void RegisterBars(InternalFunctionRegistry registry)
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

    private static void RegisterMusicalNotationFunctions(InternalFunctionRegistry registry)
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
            return Value.MusicalNote(note);
        });

        var createRestSignature = new FunctionSignature(
            "createRest",
            [NoteValueType.Instance],
            ParameterNames: ["duration"]);
        registry.Register("createRest", createRestSignature, args =>
        {
            int durationValue = (int)args[0].Data!;
            var rest = Audio.ClassicalComposition.CreateRest(durationValue);
            return Value.MusicalNote(rest);
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
            return Value.TimeSignature(timeSig);
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
            return Value.Bar(bar);
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
            return Value.Bar(bar);
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
            var voiceValues = voices.Select(v => Value.Voice(v)).ToArray();
            return Value.Array(voiceValues, VoiceType.Instance);
        });

        // ===== Sequence Functions =====

        var createSequenceSignature = new FunctionSignature("createSequence", [],
            ParameterNames: []);
        registry.Register("createSequence", createSequenceSignature, args =>
        {
            var sequence = Audio.SequenceRenderer.CreateSequence();
            return Value.Sequence(sequence);
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
            return Value.Sequence(sequence);
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
            var voiceValues = voices.Select(v => Value.Voice(v)).ToArray();
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
            var voiceValues = voices.Select(v => Value.Voice(v)).ToArray();
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
            var voiceValues = voices.Select(v => Value.Voice(v)).ToArray();
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

        var euclideanSignature = new FunctionSignature(
            "euclidean",
            [IntType.Instance, IntType.Instance, NoteType.Instance],
            ParameterNames: ["hits", "steps", "note"]);
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
    private static void RegisterEuclideanOverloads(
        InternalFunctionRegistry registry,
        FlowLang.Runtime.ExecutionContext context)
    {
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

            if (hits <= 0) throw new InvalidOperationException("euclidean: hits must be > 0");
            if (steps <= 0) throw new InvalidOperationException("euclidean: steps must be > 0");
            if (steps > 1024) throw new InvalidOperationException("euclidean: steps exceeds safety limit of 1024");
            if (hits > steps) throw new InvalidOperationException("euclidean: hits must be <= steps");

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

            if (hits <= 0) throw new InvalidOperationException("euclidean: hits must be > 0");
            if (steps <= 0) throw new InvalidOperationException("euclidean: steps must be > 0");
            if (steps > 1024) throw new InvalidOperationException("euclidean: steps exceeds safety limit of 1024");
            if (hits > steps) throw new InvalidOperationException("euclidean: hits must be <= steps");

            // D-17: LOCAL new Random(seed) scoped to THIS call; does NOT read or mutate
            // ExecutionContext.GetRand. Mirrors VariationFunctions.VarySeeded at :71-77.
            var rng = new Random(seed);
            return BuildEuclideanSequence(hits, steps, noteStr, swing, humanize, rng, context);
        });
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
        return Value.Sequence(sequence);
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
