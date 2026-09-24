using FlowLang.Runtime;
using FlowLang.StandardLibrary.Dict;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;

namespace FlowLang.StandardLibrary;

/// <summary>The essential general-purpose library. Registration does not import a prelude.</summary>
public static class CoreLibrary
{
    // Internal registration slices let the compatibility host interleave domain
    // overloads in their historical order. Equal-score resolution is order-sensitive.

    public static void Register(InternalFunctionRegistry registry)
    {
        RegisterPrimitiveStrings(registry);
        RegisterSymbolString(registry);
        RegisterCollectionStrings(registry);
        RegisterBasics(registry);
        RegisterMath(registry);
        RegisterConversions(registry);
        RegisterArrays(registry);
        RegisterArrayOperations(registry);
    }

    public static void Register(InternalFunctionRegistry registry, FlowLang.Runtime.ExecutionContext context)
    {
        Register(registry);
        RegisterCallbacks(registry, context);
        RegisterContextFunctions(registry, context);
        RegisterIterationGuard(registry, context);
    }

    internal static void RegisterIterationGuard(InternalFunctionRegistry registry, FlowLang.Runtime.ExecutionContext context)
    {
        var setMaxIterSignature = new FunctionSignature("setMaxIterations", [IntType.Instance],
            ParameterNames: ["max"]);
        registry.Register("setMaxIterations", setMaxIterSignature, args =>
        {
            // Scripts may lower their loop limit freely but cannot raise it above the
            // host's ceiling (EngineOptions.MaxIterationsCeiling).
            int requested = args[0].As<int>();
            int ceiling = context.Session.MaxIterationsCeiling;
            if (requested > ceiling)
            {
                Diagnostics.RenderingDiagnostics.WarnOnce(
                    $"budget-max-iterations:{requested}",
                    $"[budget] setMaxIterations {requested} exceeds the host limit of {ceiling}; using {ceiling}");
                requested = ceiling;
            }
            context.MaxIterations = requested;
            return Value.Void();
        });

        // break-control (0615) — the `(break)` call-position builtin for loop control.
        // Valid inside while/for bodies INCLUDING lazy-wrapped positions (if/and/or
        // branches), because it resolves at eval time rather than parse time like the
        // `break` keyword. Throws a BreakSignal that the innermost loop's
        // `catch (BreakSignal)` consumes (nested-loop break affects the INNERMOST loop —
        // the signal unwinds to the first enclosing catch). `context.LoopDepth` is the
        // dynamic loop-nesting counter maintained by Execute{For,While}Statement and
        // zeroed across proc-call boundaries.
        //
        // CHARITABLE house style (D-v1.5-05): `(break)` outside any loop is a NO-OP plus
        // a one-shot stderr advisory — NEVER an exception. A stray break in dead code or
        // a copy-pasted snippet shouldn't crash a composer's render.
        var breakSig = new FunctionSignature("break", [], ParameterNames: []);
        registry.Register("break", breakSig, args =>
        {
            if (context.LoopDepth > 0)
                throw new FlowLang.Interpreter.BreakSignal();

            FlowLang.Diagnostics.RenderingDiagnostics.WarnOnce(
                "break-outside-loop",
                "[break] (break) called outside any loop — ignored. " +
                "(break) only exits a for/while loop body.");
            return Value.Void();
        });

        // break-control (0615) — `(continue)` is the call-position sibling of `(break)`,
        // registered so the now-parseable prefix form has a home (the parser recognizes
        // both keyword tokens as call names). Throws a ContinueSignal that the innermost
        // loop's `catch (ContinueSignal)` consumes — skip to the next iteration.
        // Charitable no-op + one-shot advisory outside any loop, matching `(break)`.
        var continueSig = new FunctionSignature("continue", [], ParameterNames: []);
        registry.Register("continue", continueSig, args =>
        {
            if (context.LoopDepth > 0)
                throw new FlowLang.Interpreter.ContinueSignal();

            FlowLang.Diagnostics.RenderingDiagnostics.WarnOnce(
                "continue-outside-loop",
                "[continue] (continue) called outside any loop — ignored. " +
                "(continue) only skips to the next for/while iteration.");
            return Value.Void();
        });
    }
    internal static void RegisterPrimitiveStrings(InternalFunctionRegistry registry)
    {
        var lenStrSignature = new FunctionSignature("len", [StringType.Instance],
            ParameterNames: ["s"]);
        registry.Register("len", lenStrSignature, CoreStdLib.LenString);

        // `length` is a documented alias of `len` for the String overload —
        // welcomes composers reaching for the longer name (ergonomics-first).
        var lengthStrSignature = new FunctionSignature("length", [StringType.Instance],
            ParameterNames: ["s"]);
        registry.Register("length", lengthStrSignature, CoreStdLib.LenString);
        
        // ===== I/O Functions =====
        var printSignature = new FunctionSignature(
            "print",
            [StringType.Instance],
            ParameterNames: ["s"]);
        registry.Register("print", printSignature, CoreStdLib.Print);

        // ===== String Conversion Functions =====

        var strIntSignature = new FunctionSignature("str", [IntType.Instance],
            ParameterNames: ["value"]);
        registry.Register("str", strIntSignature, CoreStdLib.StrInt);

        var strFloatSignature = new FunctionSignature("str", [FloatType.Instance],
            ParameterNames: ["value"]);
        registry.Register("str", strFloatSignature, CoreStdLib.StrFloat);

        var strDoubleSignature = new FunctionSignature("str", [DoubleType.Instance],
            ParameterNames: ["value"]);
        registry.Register("str", strDoubleSignature, CoreStdLib.StrDouble);

        // Phase 26 (STD-02): str overloads for Long + Number — without these,
        // (str Long) is ambiguous (widens to both Float and Double) and (str Number)
        // has no candidate (Number doesn't widen on the str chain).
        var strLongSignature = new FunctionSignature("str", [LongType.Instance],
            ParameterNames: ["value"]);
        registry.Register("str", strLongSignature, CoreStdLib.StrLong);
        var strNumberSignature = new FunctionSignature("str", [NumberType.Instance],
            ParameterNames: ["value"]);
        registry.Register("str", strNumberSignature, CoreStdLib.StrNumber);

        var strStringSignature = new FunctionSignature("str", [StringType.Instance],
            ParameterNames: ["value"]);
        registry.Register("str", strStringSignature, CoreStdLib.StrString);

        var strBoolSignature = new FunctionSignature("str", [BoolType.Instance],
            ParameterNames: ["value"]);
        registry.Register("str", strBoolSignature, CoreStdLib.StrBool);

    }

    internal static void RegisterSymbolString(InternalFunctionRegistry registry)
    {
        // Phase 26.1 SYM-01: (str Symbol) → "#name"
        var strSymbolSignature = new FunctionSignature("str", [SymbolType.Instance],
            ParameterNames: ["value"]);
        registry.Register("str", strSymbolSignature, CoreStdLib.StrSymbol);

    }

    internal static void RegisterCollectionStrings(InternalFunctionRegistry registry)
    {
        var strArraySignature = new FunctionSignature("str", [new ArrayType(VoidType.Instance)],
            ParameterNames: ["value"]);
        registry.Register("str", strArraySignature, CoreStdLib.StrArray);

        // Tuples and dicts format like their literal forms: <<1, "a">> and {"a": 1}.
        var strTupleSignature = new FunctionSignature("str", [TupleType.AnyArity],
            ParameterNames: ["value"]);
        registry.Register("str", strTupleSignature, CoreStdLib.StrArray);
        var strDictSignature = new FunctionSignature("str", [new DictType(VoidType.Instance, VoidType.Instance)],
            ParameterNames: ["value"]);
        registry.Register("str", strDictSignature, CoreStdLib.StrArray);

    }

    internal static void RegisterBasics(InternalFunctionRegistry registry)
    {
        var concatSignature = new FunctionSignature("concat", [StringType.Instance, StringType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("concat", concatSignature, CoreStdLib.Concat);

        // ===== Type Conversion Functions =====

        var intToDoubleSignature = new FunctionSignature("intToDouble", [IntType.Instance],
            ParameterNames: ["value"]);
        registry.Register("intToDouble", intToDoubleSignature, CoreStdLib.IntToDouble);

        var doubleToIntSignature = new FunctionSignature("doubleToInt", [DoubleType.Instance],
            ParameterNames: ["value"]);
        registry.Register("doubleToInt", doubleToIntSignature, CoreStdLib.DoubleToInt);

        // ===== Arithmetic Functions =====

        var addIntSignature = new FunctionSignature(
            "add",
            [IntType.Instance, IntType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("add", addIntSignature, CoreStdLib.AddInt);

        var addFloatSignature = new FunctionSignature(
            "add",
            [FloatType.Instance, FloatType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("add", addFloatSignature, CoreStdLib.AddFloat);

        var subFloatSignature = new FunctionSignature(
            "sub",
            [FloatType.Instance, FloatType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("sub", subFloatSignature, CoreStdLib.SubFloat);

        var mulFloatSignature = new FunctionSignature(
            "mul",
            [FloatType.Instance, FloatType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("mul", mulFloatSignature, CoreStdLib.MulFloat);

        var divFloatSignature = new FunctionSignature(
            "div",
            [FloatType.Instance, FloatType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("div", divFloatSignature, CoreStdLib.DivFloat);

        var subSignature = new FunctionSignature(
            "sub",
            [IntType.Instance, IntType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("sub", subSignature, CoreStdLib.SubInt);

        var mulSignature = new FunctionSignature(
            "mul",
            [IntType.Instance, IntType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("mul", mulSignature, CoreStdLib.MulInt);

        var divSignature = new FunctionSignature(
            "div",
            [IntType.Instance, IntType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("div", divSignature, CoreStdLib.DivIntPromote);   // Phase 26 D-08: now returns Double

        // Double overloads for arithmetic
        var addDoubleSignature = new FunctionSignature(
            "add",
            [DoubleType.Instance, DoubleType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("add", addDoubleSignature, CoreStdLib.AddDouble);

        var subDoubleSignature = new FunctionSignature(
            "sub",
            [DoubleType.Instance, DoubleType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("sub", subDoubleSignature, CoreStdLib.SubDouble);

        var mulDoubleSignature = new FunctionSignature(
            "mul",
            [DoubleType.Instance, DoubleType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("mul", mulDoubleSignature, CoreStdLib.MulDouble);

        var divDoubleSignature = new FunctionSignature(
            "div",
            [DoubleType.Instance, DoubleType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("div", divDoubleSignature, CoreStdLib.DivDouble);

        // ===== Phase 26 (STD-02): Long + Number same-type fast paths =====

        var addLongSignature = new FunctionSignature("add", [LongType.Instance, LongType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("add", addLongSignature, CoreStdLib.AddLong);
        var subLongSignature = new FunctionSignature("sub", [LongType.Instance, LongType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("sub", subLongSignature, CoreStdLib.SubLong);
        var mulLongSignature = new FunctionSignature("mul", [LongType.Instance, LongType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("mul", mulLongSignature, CoreStdLib.MulLong);
        var divLongSignature = new FunctionSignature("div", [LongType.Instance, LongType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("div", divLongSignature, CoreStdLib.DivLong);

        var addNumberSignature = new FunctionSignature("add", [NumberType.Instance, NumberType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("add", addNumberSignature, CoreStdLib.AddNumber);
        var subNumberSignature = new FunctionSignature("sub", [NumberType.Instance, NumberType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("sub", subNumberSignature, CoreStdLib.SubNumber);
        var mulNumberSignature = new FunctionSignature("mul", [NumberType.Instance, NumberType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("mul", mulNumberSignature, CoreStdLib.MulNumber);
        var divNumberSignature = new FunctionSignature("div", [NumberType.Instance, NumberType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("div", divNumberSignature, CoreStdLib.DivNumber);

        // ===== Phase 26 (STD-02): (neg) 5-pack (D-07) =====
        var negIntSignature    = new FunctionSignature("neg", [IntType.Instance],
            ParameterNames: ["value"]);
        registry.Register("neg", negIntSignature, CoreStdLib.NegInt);
        var negLongSignature   = new FunctionSignature("neg", [LongType.Instance],
            ParameterNames: ["value"]);
        registry.Register("neg", negLongSignature, CoreStdLib.NegLong);
        var negFloatSignature  = new FunctionSignature("neg", [FloatType.Instance],
            ParameterNames: ["value"]);
        registry.Register("neg", negFloatSignature, CoreStdLib.NegFloat);
        var negDoubleSignature = new FunctionSignature("neg", [DoubleType.Instance],
            ParameterNames: ["value"]);
        registry.Register("neg", negDoubleSignature, CoreStdLib.NegDouble);
        var negNumberSignature = new FunctionSignature("neg", [NumberType.Instance],
            ParameterNames: ["value"]);
        registry.Register("neg", negNumberSignature, CoreStdLib.NegNumber);

        // ===== Phase 26 (STD-02): (idiv Int Int) → Int (D-08) =====
        var idivIntSignature = new FunctionSignature("idiv", [IntType.Instance, IntType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("idiv", idivIntSignature, CoreStdLib.IDivInt);

        // Floor modulo: the result takes the divisor's sign, so pitch-class math
        // such as (mod -1 12) gives 11.
        registry.Register("mod", new FunctionSignature("mod", [IntType.Instance, IntType.Instance],
            ParameterNames: ["a", "b"]), CoreStdLib.ModInt);
        registry.Register("mod", new FunctionSignature("mod", [DoubleType.Instance, DoubleType.Instance],
            ParameterNames: ["a", "b"]), CoreStdLib.ModDouble);

        registry.Register("split", new FunctionSignature("split", [StringType.Instance, StringType.Instance],
            ParameterNames: ["s", "separator"]), CoreStdLib.Split);

        // String-to-number conversions
        var stringToIntSignature = new FunctionSignature("stringToInt", [StringType.Instance],
            ParameterNames: ["s"]);
        registry.Register("stringToInt", stringToIntSignature, CoreStdLib.StringToInt);

        var stringToDoubleSignature = new FunctionSignature("stringToDouble", [StringType.Instance],
            ParameterNames: ["s"]);
        registry.Register("stringToDouble", stringToDoubleSignature, CoreStdLib.StringToDouble);

        // ===== Lazy Evaluation Functions =====

        // Note: eval is registered with Lazy<Void> but will work with any Lazy<T>
        // due to special handling in the implementation
        var evalSignature = new FunctionSignature(
            "eval",
            [new LazyType(VoidType.Instance)],
            ParameterNames: ["thunk"]);
        registry.Register("eval", evalSignature, CoreStdLib.Eval);
        
        var ifSignature = new FunctionSignature(
            "if", [BoolType.Instance, new LazyType(VoidType.Instance), new LazyType(VoidType.Instance)],
            ParameterNames: ["cond", "then", "else"]);
        registry.Register("if", ifSignature, CoreStdLib.If);

        // Strict (non-Lazy) if overload — Void-wildcard covers all Bool-T-T concrete shapes
        // (String/String, Double/Double, Int/Int, etc.). The Lazy overload above has higher
        // specificity for Lazy<Void> args, so it wins when args are lazy-wrapped.
        var ifStrictSignature = new FunctionSignature(
            "if", [BoolType.Instance, VoidType.Instance, VoidType.Instance],
            ParameterNames: ["cond", "then", "else"]);
        registry.Register("if", ifStrictSignature, CoreStdLib.IfStrict);


        var andSignature = new FunctionSignature(
            "and", [new LazyType(BoolType.Instance), new LazyType(BoolType.Instance)],
            ParameterNames: ["a", "b"]);
        registry.Register("and", andSignature, CoreStdLib.And);
        
        var andBoolSignature = new FunctionSignature(
            "and", [BoolType.Instance, BoolType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("and", andBoolSignature, CoreStdLib.AndBool);
        
        var orSignature = new FunctionSignature(
            "or", [new LazyType(BoolType.Instance), new LazyType(BoolType.Instance)],
            ParameterNames: ["a", "b"]);
        registry.Register("or", orSignature, CoreStdLib.Or);
        
        var orBoolSignature = new FunctionSignature(
            "or", [BoolType.Instance, BoolType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("or", orBoolSignature, CoreStdLib.OrBool);

        // ===== Equality and Comparison Functions =====
        // VoidType.Instance is used as a wildcard/"any type" parameter in these signatures.
        // The overload resolver treats Void as compatible with all types, allowing these
        // functions to accept arguments of any type.
        //
        // Phase 44 Plan 44-09 Task 2 — equals/lt/gt/lte/gte migrated to
        // RegisterContextDependentFunctions so strict-aware EqualsCharitable /
        // {GreaterThan,LessThan}{,OrEqual}Charitable can read ctx.CallerStrictMode
        // and emit `[strict] cross-type comparison ...` errors per D-11. The
        // sequals (strict equality) builtin stays here — its semantics are
        // mode-independent and never differs based on strict mode.

        var sequalsSignature = new FunctionSignature(
            "sequals",
            [VoidType.Instance, VoidType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("sequals", sequalsSignature, CoreStdLib.StrictEquals);

        // (Moved random functions to RegisterContextDependentFunctions)
        // (Moved equals/lt/gt/lte/gte to RegisterContextDependentFunctions — Plan 44-09 Task 2)
    }

    internal static void RegisterMath(InternalFunctionRegistry registry)
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

        // (Moved (beat Double) → Beat constructor to BeatConstructorFunctions.RegisterContextDependent
        //  in Phase 45 D-05 — pragma-aware multiplier reads ctx.BeatTrueToSig at call time.)

        // ===== Phase 26.1 NaN production primitive (REVISION 2) =====
        // Flow has no `nan` literal. (div 0.0 0.0) throws "Division by zero"
        // (see CoreStdLib.DivFloat/DivInt/DivLong/DivDouble — all guard b == 0).
        // (nanFloat) is the canonical IEEE 754 NaN producer for the DICT-03
        // NaN-as-key acceptance shape and any future float-edge-case work.
        // Returns Float (double-backed per Value.Float definition).
        registry.Register("nanFloat", new FunctionSignature("nanFloat", [],
                ParameterNames: []),
            args => Value.Float(double.NaN));
    }
    internal static void RegisterArrays(InternalFunctionRegistry registry)
    {
        // ===== Array Functions =====

        var listSignature = new FunctionSignature(
            "list",
            [VoidType.Instance],
            IsVarArgs: true);
        registry.Register("list", listSignature, CoreCollections.List);

        var lenSignature = new FunctionSignature("len", [new ArrayType(VoidType.Instance)],
            ParameterNames: ["arr"]);
        registry.Register("len", lenSignature, CoreCollections.Len);

        // `length` is a documented alias of `len` for the Array overload.
        var lengthSignature = new FunctionSignature("length", [new ArrayType(VoidType.Instance)],
            ParameterNames: ["arr"]);
        registry.Register("length", lengthSignature, CoreCollections.Len);

        var headSignature = new FunctionSignature("head", [new ArrayType(VoidType.Instance)],
            ParameterNames: ["arr"]);
        registry.Register("head", headSignature, CoreCollections.Head);

        var tailSignature = new FunctionSignature("tail", [new ArrayType(VoidType.Instance)],
            ParameterNames: ["arr"]);
        registry.Register("tail", tailSignature, CoreCollections.Tail);

        var lastSignature = new FunctionSignature("last", [new ArrayType(VoidType.Instance)],
            ParameterNames: ["arr"]);
        registry.Register("last", lastSignature, CoreCollections.Last);

        var initSignature = new FunctionSignature("init", [new ArrayType(VoidType.Instance)],
            ParameterNames: ["arr"]);
        registry.Register("init", initSignature, CoreCollections.Init);

        var emptySignature = new FunctionSignature("empty", [new ArrayType(VoidType.Instance)],
            ParameterNames: ["arr"]);
        registry.Register("empty", emptySignature, CoreCollections.Empty);

        var reverseSignature = new FunctionSignature("reverse", [new ArrayType(VoidType.Instance)],
            ParameterNames: ["arr"]);
        registry.Register("reverse", reverseSignature, CoreCollections.Reverse);

        var sortSignature = new FunctionSignature("sort", [new ArrayType(VoidType.Instance)],
            ParameterNames: ["arr"]);
        registry.Register("sort", sortSignature, CoreCollections.Sort);

        var takeSignature = new FunctionSignature("take", [new ArrayType(VoidType.Instance), IntType.Instance],
            ParameterNames: ["arr", "n"]);
        registry.Register("take", takeSignature, CoreCollections.Take);

        var dropSignature = new FunctionSignature("drop", [new ArrayType(VoidType.Instance), IntType.Instance],
            ParameterNames: ["arr", "n"]);
        registry.Register("drop", dropSignature, CoreCollections.Drop);

        // DEFER-01 (Phase 20 plan 20-01): range(Int, Int) + range(Int, Int, Int) -> Array[Int].
        // Standard Pythonic semantics. Two arities registered explicitly (overload resolver disambiguates by exact arity match per 20-RESEARCH Pitfall 3).
        var range2Signature = new FunctionSignature("range", [IntType.Instance, IntType.Instance],
            ParameterNames: ["start", "end"]);
        registry.Register("range", range2Signature, CoreCollections.Range);

        var range3Signature = new FunctionSignature("range", [IntType.Instance, IntType.Instance, IntType.Instance],
            ParameterNames: ["start", "end", "step"]);
        registry.Register("range", range3Signature, CoreCollections.Range);

        // DX-05 (Phase 14 plan 14-01): slice(Array[T], Int, Int) + slice(Sequence, Int, Int).
        // Silent two-sided clamping per CONTEXT D-01. Both overloads ship atomically per D-02.
        // Overload resolver disambiguates by arg 0 type (Array vs Sequence).
        var sliceArraySignature = new FunctionSignature("slice",
            [new ArrayType(VoidType.Instance), IntType.Instance, IntType.Instance],
            ParameterNames: ["arr", "start", "end"]);
        registry.Register("slice", sliceArraySignature, CoreCollections.SliceArray);

    }

    internal static void RegisterArrayOperations(InternalFunctionRegistry registry)
    {
        var appendSignature = new FunctionSignature("append", [new ArrayType(VoidType.Instance), VoidType.Instance],
            ParameterNames: ["arr", "element"]);
        registry.Register("append", appendSignature, CoreCollections.Append);

        var prependSignature = new FunctionSignature("prepend", [VoidType.Instance, new ArrayType(VoidType.Instance)],
            ParameterNames: ["element", "arr"]);
        registry.Register("prepend", prependSignature, CoreCollections.Prepend);

        // Note: "concat" is intentionally overloaded for both strings (in RegisterStdLib)
        // and arrays (here). The overload resolver selects the correct one by argument types.
        var concatSignature = new FunctionSignature("concat", [new ArrayType(VoidType.Instance), new ArrayType(VoidType.Instance)],
            ParameterNames: ["a", "b"]);
        registry.Register("concat", concatSignature, CoreCollections.Concat);

        var containsSignature = new FunctionSignature("contains", [new ArrayType(VoidType.Instance), VoidType.Instance],
            ParameterNames: ["arr", "element"]);
        registry.Register("contains", containsSignature, CoreCollections.Contains);
    }

    internal static void RegisterCallbacks(InternalFunctionRegistry registry, FlowLang.Runtime.ExecutionContext context)
    {
        // ===== Random Generator Functions =====

        var randSignature = new FunctionSignature("?", [],
            ParameterNames: []);
        registry.Register("?", randSignature, args => CoreStdLib.Rand(args, context));

        var fixedRandSignature = new FunctionSignature("??", [],
            ParameterNames: []);
        registry.Register("??", fixedRandSignature, args => CoreStdLib.FixedRand(args, context));

        var resetRandSignature = new FunctionSignature("??reset", [],
            ParameterNames: []);
        registry.Register("??reset", resetRandSignature, args => CoreStdLib.FixedRandReset(args, context));

        var setRandSignature = new FunctionSignature("??set", [IntType.Instance],
            ParameterNames: ["seed"]);
        registry.Register("??set", setRandSignature, args => CoreStdLib.FixedRandSet(args, context));

        // ===== Higher-Order Functions =====

        var eachSignature = new FunctionSignature("each", [new ArrayType(VoidType.Instance), FunctionType.Instance],
            ParameterNames: ["arr", "fn"]);
        registry.Register("each", eachSignature, args => CoreCollections.Each(args, context));

        var mapSignature = new FunctionSignature("map", [new ArrayType(VoidType.Instance), FunctionType.Instance],
            ParameterNames: ["arr", "fn"]);
        registry.Register("map", mapSignature, args => CoreCollections.Map(args, context));

        var filterSignature = new FunctionSignature("filter", [new ArrayType(VoidType.Instance), FunctionType.Instance],
            ParameterNames: ["arr", "pred"]);
        registry.Register("filter", filterSignature, args => CoreCollections.Filter(args, context));

        var reduceSignature = new FunctionSignature("reduce", [new ArrayType(VoidType.Instance), VoidType.Instance, FunctionType.Instance],
            ParameterNames: ["arr", "initial", "fn"]);
        registry.Register("reduce", reduceSignature, args => CoreCollections.Reduce(args, context));

        var zipSignature = new FunctionSignature("zip",
            [new ArrayType(VoidType.Instance), new ArrayType(VoidType.Instance)],
            ParameterNames: ["a", "b"]);
        registry.Register("zip", zipSignature, CoreCollections.Zip);

    }

    internal static void RegisterContextFunctions(InternalFunctionRegistry registry, FlowLang.Runtime.ExecutionContext context)
    {
        // Phase 26.1 dict + tuple-unpack runtime functions (TUP-11 + DICT-01/02/03)
        RegisterDict(registry, context);

        // ===== Phase 44 Plan 44-08 — non-strict charitable overloads =====
        // Pre-strict bug fix per ROADMAP line 404 + D-12 last-truthy and/or +
        // RESEARCH A6 (not) base registration. Void-wildcard overloads
        // co-exist with existing String / Bool / Lazy<Bool> typed overloads
        // — OverloadResolver scoring (+1000 exact / +500 compatible) ensures
        // (print "hello") / (if true ...) / (and true false) continue
        // hitting their typed-path byte-identical. The wildcards only fire
        // on non-typed args where today's pipeline errors with "no matching
        // overload". Strict tightening is layered on top by Plan 44-09.

        // (print Void) — charitable AutoStr in non-strict; [strict] error
        // in strict. Pitfall 3: String overload still scores +1000.
        var printAnySig = new FunctionSignature("print", [VoidType.Instance],
            ParameterNames: ["s"]);
        registry.Register("print", printAnySig, args => CoreStdLib.PrintAny(args, context));

        // (if Void Void Void) — truthy-coerce in non-strict; [strict] error
        // on non-Bool cond in strict. Existing if(Bool, Lazy, Lazy) and
        // if(Bool, Void, Void) overloads stay; they out-score the wildcard
        // when cond is Bool.
        var ifAnySig = new FunctionSignature("if",
            [VoidType.Instance, VoidType.Instance, VoidType.Instance],
            ParameterNames: ["cond", "then", "else"]);
        registry.Register("if", ifAnySig, args => CoreStdLib.IfTruthy(args, context));

        // (not Void) — FIRST registration of `not` per RESEARCH A6. Strict
        // path requires Bool; non-strict charitable truthy.
        var notAnySig = new FunctionSignature("not", [VoidType.Instance],
            ParameterNames: ["x"]);
        registry.Register("not", notAnySig, args => CoreStdLib.NotCharitable(args, context));

        // (and Void Void) — D-12 last-truthy semantics in non-strict.
        // Existing and(Bool, Bool) overload (+1000) wins for the Bool-Bool
        // case; this wildcard (+500) fires for everything else. (and 1 "foo")
        // → "foo"; (and false 1) → false (first falsy short-circuit).
        var andAnySig = new FunctionSignature("and",
            [VoidType.Instance, VoidType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("and", andAnySig, args => CoreStdLib.AndLastTruthy(args, context));

        // (or Void Void) — D-12 last-truthy semantics in non-strict.
        // (or false 42) → 42; (or "" "fallback") → "fallback"
        // (first truthy after short-circuit, matches CPython `or`).
        var orAnySig = new FunctionSignature("or",
            [VoidType.Instance, VoidType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("or", orAnySig, args => CoreStdLib.OrLastTruthy(args, context));

        // ===== Phase 44 Plan 44-09 Task 2 — strict-aware equals + comparisons =====
        // Migrated from RegisterStdLib so the impls can read ctx.CallerStrictMode
        // and emit canonical strict-mode errors. Non-strict behavior is byte-
        // identical to the previous Utils.LooseEquals / Utils.CompareNumeric paths.
        // D-11: (equals 1 1.0) strict → false (set-theoretic);
        // (gt|lt|gte|lte 1 1.0) strict → error.

        var equalsSig = new FunctionSignature(
            "equals",
            [VoidType.Instance, VoidType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("equals", equalsSig, args => CoreStdLib.EqualsCharitable(args, context));

        var ltSig = new FunctionSignature(
            "lt",
            [VoidType.Instance, VoidType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("lt", ltSig, args => CoreStdLib.LessThanCharitable(args, context));

        var gtSig = new FunctionSignature(
            "gt",
            [VoidType.Instance, VoidType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("gt", gtSig, args => CoreStdLib.GreaterThanCharitable(args, context));

        var lteSig = new FunctionSignature(
            "lte",
            [VoidType.Instance, VoidType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("lte", lteSig, args => CoreStdLib.LessThanOrEqualCharitable(args, context));

        var gteSig = new FunctionSignature(
            "gte",
            [VoidType.Instance, VoidType.Instance],
            ParameterNames: ["a", "b"]);
        registry.Register("gte", gteSig, args => CoreStdLib.GreaterThanOrEqualCharitable(args, context));
    }

    internal static void RegisterDict(InternalFunctionRegistry registry, FlowLang.Runtime.ExecutionContext context)
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
    internal static void RegisterConversions(InternalFunctionRegistry registry)
    {
        // Phase 44 Plan 44-09 Task 2 — primitive numeric cross-casts. Without
        // these, strict-mode composers have no escape hatch for cross-type
        // comparisons / arithmetic: `(double 1)` would fail overload resolution
        // because Int → Double widening is disabled under strict (Plan 44-03).
        // Each of the 4 extractors (double/float/int/long) accepts every
        // primitive numeric source, including its own type (see below).
        //
        // Identity casts ((double Double), (int Int), ...) ARE registered. Without
        // them `(double 2.5)` was ambiguous: a Double argument matches the unit-typed
        // overloads (double Decibel), (double Hertz), ... equally well, because those
        // unit types accept Double. The exact identity overload always wins.
        //
        // Phase 44 review WR-05: `(int Long)` previously silently truncated
        // via `(int)(long)` cast — e.g. `(int 5_000_000_000L)` produced
        // Int.MinValue. Switch to Math.Clamp so out-of-range Long values
        // pin to int.MinValue / int.MaxValue rather than silently overflowing.
        // Math.Clamp(Long, int.MinValue, int.MaxValue) returns a Long in
        // range; the outer (int) cast is then safe.
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
            {
                var dblSig = new FunctionSignature("double", [src], ParameterNames: ["value"]);
                registry.Register("double", dblSig, args => Value.Double(toDbl(args[0])));
            }

            // (float Int|Long|Float|Double)
            // Flow Float is CLR double, so the body is the same toDbl materializer.
            {
                var fltSig = new FunctionSignature("float", [src], ParameterNames: ["value"]);
                registry.Register("float", fltSig, args => Value.Float(toDbl(args[0])));
            }

            // (int Int|Long|Float|Double)
            // WR-05: Long source clamps to [int.MinValue, int.MaxValue] rather
            // than silently truncating. Float/Double sources go through toLng
            // (which floor-rounds) then clamp via the same path — Math.Floor
            // of a large Double still overflows long, so the materializer
            // result is clamped before cast.
            {
                var intSig = new FunctionSignature("int", [src], ParameterNames: ["value"]);
                registry.Register("int", intSig, args =>
                {
                    long asLong = toLng(args[0]);
                    long clamped = Math.Clamp(asLong, int.MinValue, int.MaxValue);
                    return Value.Int((int)clamped);
                });
            }

            // (long Int|Long|Float|Double)
            {
                var lngSig = new FunctionSignature("long", [src], ParameterNames: ["value"]);
                registry.Register("long", lngSig, args => Value.Long(toLng(args[0])));
            }
        }
    }
}
