using FlowLang.Diagnostics;
using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;
using System.Numerics;
using ExecutionContext = FlowLang.Runtime.ExecutionContext;

namespace FlowLang.StandardLibrary;

/// <summary>
/// Standard library implementations for Flow built-in functions.
/// </summary>
public static class StdLib
{
    /// <summary>
    /// Returns the length of a string.
    /// </summary>
    public static Value LenString(IReadOnlyList<Value> args)
    {
        var str = args[0].As<string>();
        return Value.Int(str.Length);
    }

    // ===== I/O Functions =====

    /// <summary>
    /// Prints a string to console.
    /// </summary>
    public static Value Print(IReadOnlyList<Value> args)
    {
        Console.WriteLine(args[0].As<string>());
        return Value.Void();
    }

    // ===== String Conversion Functions =====

    /// <summary>
    /// Converts an Int to string.
    /// </summary>
    public static Value StrInt(IReadOnlyList<Value> args)
    {
        return Value.String(args[0].ToString());
    }

    /// <summary>
    /// Converts a Float to string.
    /// </summary>
    public static Value StrFloat(IReadOnlyList<Value> args)
    {
        return Value.String(args[0].ToString());
    }

    /// <summary>
    /// Converts a Double to string.
    /// </summary>
    public static Value StrDouble(IReadOnlyList<Value> args)
    {
        return Value.String(args[0].ToString());
    }

    /// <summary>
    /// Converts a Long to string. Phase 26 — without this, (str Long) is ambiguous
    /// because Long widens to both Float and Double.
    /// </summary>
    public static Value StrLong(IReadOnlyList<Value> args)
    {
        return Value.String(args[0].ToString());
    }

    /// <summary>
    /// Converts a Number (BigInteger) to string. Phase 26 — without this, (str Number)
    /// has no candidate (Number doesn't widen anywhere on the str chain).
    /// </summary>
    public static Value StrNumber(IReadOnlyList<Value> args)
    {
        return Value.String(args[0].ToString());
    }

    /// <summary>
    /// Returns a String as-is.
    /// </summary>
    public static Value StrString(IReadOnlyList<Value> args)
    {
        return Value.String(args[0].As<string>());
    }

    /// <summary>
    /// Converts a Bool to string.
    /// </summary>
    public static Value StrBool(IReadOnlyList<Value> args)
    {
        return Value.String(args[0].ToString());
    }

    /// <summary>
    /// Converts a Note to string.
    /// </summary>
    public static Value StrNote(IReadOnlyList<Value> args)
    {
        return Value.String(args[0].As<string>());
    }

    /// <summary>
    /// Converts a Symbol to string — prints with leading <c>#</c> per Phase 26.1 SYM-01
    /// (so <c>(str #kick)</c> → <c>"#kick"</c>, matching the literal source form).
    /// </summary>
    public static Value StrSymbol(IReadOnlyList<Value> args)
    {
        return Value.String("#" + args[0].As<string>());
    }

    /// <summary>
    /// Converts a Bar to string.
    /// </summary>
    public static Value StrBar(IReadOnlyList<Value> args)
    {
        var bar = args[0].As<BarData>();
        return Value.String(bar.ToString());
    }

    /// <summary>
    /// Converts a Semitone to string with sign and "st" suffix.
    /// </summary>
    public static Value StrSemitone(IReadOnlyList<Value> args)
    {
        var value = args[0].As<int>();
        return Value.String($"{(value >= 0 ? "+" : "")}{value}st");
    }

    /// <summary>
    /// Converts a Cent to string with sign and "c" suffix.
    /// </summary>
    public static Value StrCent(IReadOnlyList<Value> args)
    {
        var value = args[0].As<double>();
        return Value.String($"{(value >= 0 ? "+" : "")}{value}c");
    }

    /// <summary>
    /// Converts a Millisecond to string with "ms" suffix.
    /// </summary>
    public static Value StrMillisecond(IReadOnlyList<Value> args)
    {
        return Value.String($"{args[0].As<double>()}ms");
    }

    /// <summary>
    /// Converts a Second to string with "s" suffix.
    /// </summary>
    public static Value StrSecond(IReadOnlyList<Value> args)
    {
        return Value.String($"{args[0].As<double>()}s");
    }

    /// <summary>
    /// Converts a Decibel to string with sign and "dB" suffix.
    /// </summary>
    public static Value StrDecibel(IReadOnlyList<Value> args)
    {
        var value = args[0].As<double>();
        return Value.String($"{(value >= 0 ? "+" : "")}{value}dB");
    }

    /// <summary>
    /// Converts a Beat to string as a PLAIN double — NO "b" suffix (Phase 45 D-14).
    /// Emitting "0.5b" would break round-trip under <c>enable beat-true-to-sig;</c>
    /// (e.g. 0.5b in 6/8 evaluates to 0.25 quarters; re-parsing "0.25b" under the
    /// same pragma re-multiplies to 0.125 — a different value). Composers treat
    /// Beat as a tagged double for printing. A dedicated overload is REQUIRED:
    /// without it, <c>(str someBeat)</c> is ambiguous between str(Float)/str(Double)
    /// because BeatType.IsCompatibleWith covers both at equal specificity.
    /// </summary>
    public static Value StrBeat(IReadOnlyList<Value> args)
    {
        return Value.String($"{args[0].As<double>()}");
    }

    /// <summary>
    /// Converts an Array to string.
    /// </summary>
    public static Value StrArray(IReadOnlyList<Value> args)
    {
        return Value.String(args[0].ToString());
    }
    
    public static Value Concat(IReadOnlyList<Value> args)
    {
        var arg1 = args[0].As<string>();
        var arg2 = args[1].As<string>();

        return Value.String(arg1 + arg2);
    }

    // ===== Type Conversion Functions =====

    /// <summary>
    /// Converts an Int to Double.
    /// </summary>
    public static Value IntToDouble(IReadOnlyList<Value> args)
    {
        int value = args[0].As<int>();
        return Value.Double((double)value);
    }

    /// <summary>
    /// Converts a Double to Int (truncates).
    /// </summary>
    public static Value DoubleToInt(IReadOnlyList<Value> args)
    {
        double value = args[0].As<double>();
        return Value.Int((int)value);
    }

    // ===== Arithmetic Functions =====

    /// <summary>
    /// Adds two integers.
    /// </summary>
    public static Value AddInt(IReadOnlyList<Value> args)
    {
        var a = args[0].As<int>();
        var b = args[1].As<int>();
        return Value.Int(a + b);
    }

    /// <summary>
    /// Adds two floats.
    /// </summary>
    public static Value AddFloat(IReadOnlyList<Value> args)
    {
        var a = args[0].As<double>();
        var b = args[1].As<double>();
        return Value.Float(a + b);
    }

    /// <summary>
    /// Subtracts two floats.
    /// </summary>
    public static Value SubFloat(IReadOnlyList<Value> args)
    {
        var a = args[0].As<double>();
        var b = args[1].As<double>();
        return Value.Float(a - b);
    }

    /// <summary>
    /// Multiplies two floats.
    /// </summary>
    public static Value MulFloat(IReadOnlyList<Value> args)
    {
        var a = args[0].As<double>();
        var b = args[1].As<double>();
        return Value.Float(a * b);
    }

    /// <summary>
    /// Divides two floats.
    /// </summary>
    public static Value DivFloat(IReadOnlyList<Value> args)
    {
        var a = args[0].As<double>();
        var b = args[1].As<double>();
        if (b == 0) throw new InvalidOperationException("Division by zero");
        return Value.Float(a / b);
    }

    /// <summary>
    /// Adds two doubles.
    /// </summary>
    public static Value AddDouble(IReadOnlyList<Value> args)
    {
        var a = args[0].As<double>();
        var b = args[1].As<double>();
        return Value.Double(a + b);
    }

    /// <summary>
    /// Subtracts two integers.
    /// </summary>
    public static Value SubInt(IReadOnlyList<Value> args)
    {
        var a = args[0].As<int>();
        var b = args[1].As<int>();
        return Value.Int(a - b);
    }

    /// <summary>
    /// Multiplies two integers.
    /// </summary>
    public static Value MulInt(IReadOnlyList<Value> args)
    {
        var a = args[0].As<int>();
        var b = args[1].As<int>();
        return Value.Int(a * b);
    }

    /// <summary>
    /// Divides two integers.
    /// </summary>
    public static Value DivInt(IReadOnlyList<Value> args)
    {
        var a = args[0].As<int>();
        var b = args[1].As<int>();
        if (b == 0) throw new InvalidOperationException("Division by zero");
        return Value.Int(a / b);
    }

    /// <summary>
    /// Subtracts two doubles.
    /// </summary>
    public static Value SubDouble(IReadOnlyList<Value> args)
    {
        var a = args[0].As<double>();
        var b = args[1].As<double>();
        return Value.Double(a - b);
    }

    /// <summary>
    /// Multiplies two doubles.
    /// </summary>
    public static Value MulDouble(IReadOnlyList<Value> args)
    {
        var a = args[0].As<double>();
        var b = args[1].As<double>();
        return Value.Double(a * b);
    }

    /// <summary>
    /// Divides two doubles.
    /// </summary>
    public static Value DivDouble(IReadOnlyList<Value> args)
    {
        var a = args[0].As<double>();
        var b = args[1].As<double>();
        if (b == 0) throw new InvalidOperationException("Division by zero");
        return Value.Double(a / b);
    }

    // ===== Phase 26 Long arithmetic (D-05 fast path) =====
    public static Value AddLong(IReadOnlyList<Value> args)
        => Value.Long(args[0].As<long>() + args[1].As<long>());
    public static Value SubLong(IReadOnlyList<Value> args)
        => Value.Long(args[0].As<long>() - args[1].As<long>());
    public static Value MulLong(IReadOnlyList<Value> args)
        => Value.Long(args[0].As<long>() * args[1].As<long>());
    public static Value DivLong(IReadOnlyList<Value> args)
    {
        var a = args[0].As<long>();
        var b = args[1].As<long>();
        if (b == 0L) throw new InvalidOperationException("Division by zero");
        return Value.Long(a / b);
    }

    // ===== Phase 26 Number arithmetic (BigInteger; D-05 fast path) =====
    public static Value AddNumber(IReadOnlyList<Value> args)
        => Value.Number(args[0].As<BigInteger>() + args[1].As<BigInteger>());
    public static Value SubNumber(IReadOnlyList<Value> args)
        => Value.Number(args[0].As<BigInteger>() - args[1].As<BigInteger>());
    public static Value MulNumber(IReadOnlyList<Value> args)
        => Value.Number(args[0].As<BigInteger>() * args[1].As<BigInteger>());
    public static Value DivNumber(IReadOnlyList<Value> args)
    {
        var a = args[0].As<BigInteger>();
        var b = args[1].As<BigInteger>();
        if (b.IsZero) throw new InvalidOperationException("Division by zero");
        return Value.Number(a / b);
    }

    // ===== Phase 26 (neg) 5-pack (D-07) =====
    public static Value NegInt(IReadOnlyList<Value> args)
        => Value.Int(-args[0].As<int>());
    public static Value NegLong(IReadOnlyList<Value> args)
        => Value.Long(-args[0].As<long>());
    public static Value NegFloat(IReadOnlyList<Value> args)
        => Value.Float(-args[0].As<double>());   // FloatType is double-backed in Value.Float
    public static Value NegDouble(IReadOnlyList<Value> args)
        => Value.Double(-args[0].As<double>());
    public static Value NegNumber(IReadOnlyList<Value> args)
        => Value.Number(-args[0].As<BigInteger>());

    // ===== Phase 26 integer-division (D-08) =====
    /// <summary>
    /// Truncating integer division: (idiv 1 2) -> 0. D-08.
    /// </summary>
    public static Value IDivInt(IReadOnlyList<Value> args)
    {
        var a = args[0].As<int>();
        var b = args[1].As<int>();
        if (b == 0) throw new InvalidOperationException("Integer division by zero");
        return Value.Int(a / b);
    }
    /// <summary>
    /// Auto-promoting Int/Int division returning Double: (div 1 2) -> 0.5. D-08.
    /// </summary>
    public static Value DivIntPromote(IReadOnlyList<Value> args)
    {
        var a = args[0].As<int>();
        var b = args[1].As<int>();
        if (b == 0) throw new InvalidOperationException("Division by zero");
        return Value.Double((double)a / b);
    }

    /// <summary>
    /// Converts a string to an Int. Returns Void on failure.
    /// </summary>
    public static Value StringToInt(IReadOnlyList<Value> args)
    {
        var str = args[0].As<string>();
        if (int.TryParse(str, out int result))
            return Value.Int(result);
        return Value.Void();
    }

    /// <summary>
    /// Converts a string to a Double. Returns Void on failure.
    /// </summary>
    public static Value StringToDouble(IReadOnlyList<Value> args)
    {
        var str = args[0].As<string>();
        if (double.TryParse(str, out double result))
            return Value.Double(result);
        return Value.Void();
    }

    // ===== Lazy Evaluation Functions =====

    /// <summary>
    /// Evaluates a lazy value.
    /// </summary>
    public static Value Eval(IReadOnlyList<Value> args)
    {
        var lazyValue = args[0];
        var thunk = lazyValue.As<Thunk>();
        return thunk.Force();
    }


    public static Value If(IReadOnlyList<Value> args)
    {
        var cond = args[0].As<bool>();
        var if_true = args[1].As<Thunk>();
        var otherwise = args[2].As<Thunk>();

        if (cond)
        {
            return if_true.Force();
        }
        else
        {
            return otherwise.Force();
        }
    }

    /// <summary>
    /// Strict (non-Lazy) if overload. Both branches are eagerly evaluated
    /// at the call site (the interpreter resolves args before dispatch),
    /// but only the selected value is returned. Matches the Lazy-if contract
    /// for concrete (non-Thunk) arguments. Uses Void-wildcard dispatch.
    /// </summary>
    public static Value IfStrict(IReadOnlyList<Value> args)
    {
        var cond = args[0].As<bool>();
        return cond ? args[1] : args[2];
    }


    public static Value And(IReadOnlyList<Value> args)
    {
        var leftLazy = args[0];                                                                                         
        var rightLazy = args[1];
        
        if (leftLazy.Type is not LazyType { InnerType: BoolType })                                                      
            throw new InvalidOperationException($"Expected Lazy<Bool>, got {leftLazy.Type}");                           
        if (rightLazy.Type is not LazyType { InnerType: BoolType })                                                     
            throw new InvalidOperationException($"Expected Lazy<Bool>, got {rightLazy.Type}");  
        
        var left = args[0].As<Thunk>();
        var right = args[1].As<Thunk>();

        bool lres = left.Force().As<bool>();
        if (!lres)
        {
            return Value.Bool(false);
        }
        bool rres = right.Force().As<bool>();

        return Value.Bool(rres);
    }
    
    public static Value AndBool(IReadOnlyList<Value> args)
    {
        var left = args[0].As<bool>();
        var right = args[1].As<bool>();
        return Value.Bool(left && right);
    }
    
    public static Value Or(IReadOnlyList<Value> args)
    {
        var leftLazy = args[0];                                                                                         
        var rightLazy = args[1];
        
        if (leftLazy.Type is not LazyType { InnerType: BoolType })                                                      
            throw new InvalidOperationException($"Expected Lazy<Bool>, got {leftLazy.Type}");                           
        if (rightLazy.Type is not LazyType { InnerType: BoolType })                                                     
            throw new InvalidOperationException($"Expected Lazy<Bool>, got {rightLazy.Type}");  
        
        var left = args[0].As<Thunk>();
        var right = args[1].As<Thunk>();

        bool lres = left.Force().As<bool>();
        if (lres)
        {
            return Value.Bool(true);
        }
        bool rres = right.Force().As<bool>();

        return Value.Bool(rres);
    }
    
    public static Value OrBool(IReadOnlyList<Value> args)
    {
        var left = args[0].As<bool>();
        var right = args[1].As<bool>();
        return Value.Bool(left || right);
    }

    // ===== Equality and Comparison Functions =====

    /// <summary>
    /// Loose equality with type conversion (like JavaScript ==).
    /// </summary>
    public static Value Equals(IReadOnlyList<Value> args)
    {
        return Value.Bool(Utils.LooseEquals(args[0], args[1]));
    }

    /// <summary>
    /// Strict equality - type and value must match (like JavaScript ===).
    /// </summary>
    public static Value StrictEquals(IReadOnlyList<Value> args)
    {
        return Value.Bool(Utils.StrictEquals(args[0], args[1]));
    }

    /// <summary>
    /// Less than comparison for numeric types.
    /// </summary>
    public static Value LessThan(IReadOnlyList<Value> args)
    {
        return Value.Bool(Utils.CompareNumeric(args[0], args[1]) < 0);
    }

    /// <summary>
    /// Greater than comparison for numeric types.
    /// </summary>
    public static Value GreaterThan(IReadOnlyList<Value> args)
    {
        return Value.Bool(Utils.CompareNumeric(args[0], args[1]) > 0);
    }

    /// <summary>
    /// Less than or equal comparison for numeric types.
    /// </summary>
    public static Value LessThanOrEqual(IReadOnlyList<Value> args)
    {
        return Value.Bool(Utils.CompareNumeric(args[0], args[1]) <= 0);
    }

    /// <summary>
    /// Greater than or equal comparison for numeric types.
    /// </summary>
    public static Value GreaterThanOrEqual(IReadOnlyList<Value> args)
    {
        return Value.Bool(Utils.CompareNumeric(args[0], args[1]) >= 0);
    }


    /// <summary>
    /// Returns a random Float between 0.0 and 1.0.
    /// </summary>
    public static Value Rand(IReadOnlyList<Value> args, ExecutionContext context)
    {
        return Value.Float(context.GetRand().NextSingle());
    }
    
    public static Value FixedRand(IReadOnlyList<Value> args, ExecutionContext context)
    {
        return Value.Float(context.GetRand(true).NextSingle());
    }
    
    public static Value FixedRandReset(IReadOnlyList<Value> args, ExecutionContext context)
    {
        context.ResetGen();
        return Value.Void();
    }
    
    public static Value FixedRandSet(IReadOnlyList<Value> args, ExecutionContext context)
    {
        var val = args[0];
        if (val.Type is not IntType)
            throw new InvalidOperationException($"Expected Int, got {val.Type}");

        context.SetSeed(val.As<int>());
        return Value.Void();
    }

    // ===== Phase 44 Plan 44-08 — Charitable non-strict helpers + strict-aware =====
    // Pre-strict bug fix per ROADMAP line 404 — `(print Int x)` charitably auto-strs
    // via AutoStr in non-strict, `if Int x` truthy-coerces, `(not Int 0)` returns
    // true (charitable wildcard). Strict mode (Plan 44-09 follow-up) layers the
    // Bool-required / String-required checks via `ctx.CallerStrictMode`. Plan 44-08
    // lands the strict-error TEXT here; Plan 44-09's REQ-STRICT-09 test suite pins
    // exact wording via the strict-error-manifest.csv.

    /// <summary>
    /// Stringifies a <see cref="Value"/> for the non-strict <c>(print)</c> charitable
    /// path. Functionally equivalent to <c>(str x)</c> — dispatches by
    /// <see cref="Value.Type"/> so the result matches the existing per-type
    /// <c>StrInt</c> / <c>StrDouble</c> / <c>StrSemitone</c> / etc. format
    /// conventions documented in CLAUDE.md §"Music Types Quick Reference".
    /// <para>
    /// String inputs return the underlying string raw (no enclosing quotes —
    /// matches the existing <see cref="Print"/> contract). All other inputs
    /// match their per-type <c>(str)</c> overload byte-for-byte. Unknown /
    /// reference-identity types fall back to <see cref="Value.ToString"/>.
    /// </para>
    /// </summary>
    public static string AutoStr(Value v)
    {
        if (v.Type is StringType) return v.As<string>();
        if (v.Type is IntType) return v.As<int>().ToString();
        if (v.Type is LongType) return v.As<long>().ToString();
        if (v.Type is FloatType) return v.As<double>().ToString();
        if (v.Type is DoubleType) return v.As<double>().ToString();
        if (v.Type is NumberType) return v.As<BigInteger>().ToString();
        if (v.Type is BoolType) return v.As<bool>() ? "true" : "false";
        if (v.Type is NoteType) return v.As<string>();
        if (v.Type is SymbolType) return "#" + v.As<string>();
        if (v.Type is SemitoneType)
        {
            var st = v.As<int>();
            return $"{(st >= 0 ? "+" : "")}{st}st";
        }
        if (v.Type is CentType)
        {
            var c = v.As<double>();
            return $"{(c >= 0 ? "+" : "")}{c}c";
        }
        if (v.Type is MillisecondType) return $"{v.As<double>()}ms";
        if (v.Type is SecondType) return $"{v.As<double>()}s";
        if (v.Type is DecibelType)
        {
            var dB = v.As<double>();
            return $"{(dB >= 0 ? "+" : "")}{dB}dB";
        }
        if (v.Type is HertzType) return $"{v.As<double>()}Hz";
        if (v.Type is VoidType) return "()";
        // Sequence / Bar / Chord / Song / Section / Tuple / Dict / Array / Tuning /
        // Sfz / MarkovModel / LsystemModel / OscHandle etc. — fall through to
        // Value.ToString which already handles each (and reference-identity types
        // print their canonical description). Pitfall 6 (NewLineChars) does not
        // apply here — we are NOT writing structured docs.
        return v.ToString();
    }

    /// <summary>
    /// Non-strict charitable <c>(print)</c> impl backing the Void-wildcard
    /// overload registered alongside the existing String overload. In strict
    /// mode (caller's <c>CallerStrictMode == true</c>) emits the canonical
    /// <c>[strict] (print) requires String — got &lt;Type&gt;</c> error
    /// through <see cref="ExecutionContext.ErrorReporter"/> and returns
    /// <see cref="Value.Void"/> without printing. Pitfall 3: the explicit
    /// String overload scores +1000 vs Void-wildcard +500 so
    /// <c>(print "hello")</c> never reaches this method.
    /// </summary>
    public static Value PrintAny(IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        if (ctx.CallerStrictMode)
        {
            ctx.ErrorReporter.ReportError(
                $"[strict] (print) requires String — got {args[0].Type}",
                ctx.CurrentCallSite);
            return Value.Void();
        }
        Console.WriteLine(AutoStr(args[0]));
        return Value.Void();
    }

    /// <summary>
    /// Phase 44 Plan 44-08 Task 2 — charitable truthy-coerce helper. Mirrors
    /// Python / JavaScript truthy conventions while preserving Flow's
    /// reference-identity rule for music types (a non-null Sequence / Chord /
    /// Song / Tuning / Sfz / etc. is truthy by presence; collection types
    /// are falsy iff empty). Reused by <see cref="IfTruthy"/>,
    /// <see cref="NotCharitable"/>, and Task 3's
    /// <c>AndLastTruthy</c> / <c>OrLastTruthy</c> so the non-strict
    /// charitable rule stays in one place.
    /// </summary>
    public static bool TruthyCoerce(Value v)
    {
        if (v.Type is VoidType) return false;
        if (v.Data is null) return false;
        if (v.Type is BoolType) return v.As<bool>();
        if (v.Type is IntType) return v.As<int>() != 0;
        if (v.Type is LongType) return v.As<long>() != 0L;
        if (v.Type is FloatType || v.Type is DoubleType)
        {
            var d = v.As<double>();
            return d != 0.0 && !double.IsNaN(d);
        }
        if (v.Type is NumberType) return !v.As<BigInteger>().IsZero;
        if (v.Type is StringType) return !string.IsNullOrEmpty(v.As<string>());
        if (v.Type is SymbolType) return true;  // any non-null Symbol is truthy
        // Arrays / Tuples — falsy iff empty.
        if (v.Data is IReadOnlyList<Value> list) return list.Count > 0;
        // Dicts — falsy iff empty.
        if (v.Type is DictType && v.Data is DictData dd)
            return dd.Entries.Count > 0;
        // Music tagged-numeric types: presence = truthy (Decibel/Hz/Cent/ms/sec/st
        // values are NOT special-cased on zero — composers write -inf via
        // -Infinity, not 0).
        // Sequence / Chord / Song / Section / Tuning / Sfz / MarkovModel /
        // LsystemModel / OscHandle / Voice / Track / Buffer / Function — all
        // non-null reference-identity values are truthy by presence.
        return true;
    }

    /// <summary>
    /// Non-strict charitable <c>(if cond then else)</c> with truthy-coerce on
    /// <paramref name="args"/>[0]. Strict mode (caller's
    /// <c>CallerStrictMode == true</c>) emits
    /// <c>[strict] (if) requires Bool — got &lt;Type&gt;</c> for any non-Bool
    /// cond. Both branches are eagerly evaluated by the interpreter before
    /// dispatch (matches the <see cref="IfStrict"/> contract — only the
    /// selected branch's value is returned).
    /// </summary>
    public static Value IfTruthy(IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        if (ctx.CallerStrictMode && args[0].Type is not BoolType)
        {
            ctx.ErrorReporter.ReportError(
                $"[strict] (if) requires Bool — got {args[0].Type}",
                ctx.CurrentCallSite);
            return Value.Void();
        }
        return TruthyCoerce(args[0]) ? args[1] : args[2];
    }

    /// <summary>
    /// Non-strict charitable <c>(not x)</c> — returns Bool but accepts any
    /// value. <c>(not 0)</c> → <c>true</c>, <c>(not "hello")</c> → <c>false</c>,
    /// <c>(not | C4 |)</c> → <c>false</c>. Strict mode emits
    /// <c>[strict] (not) requires Bool — got &lt;Type&gt;</c> for non-Bool args.
    /// Per RESEARCH A6, this is the FIRST registration of <c>(not)</c> in the
    /// InternalFunctionRegistry (<c>flow-lang/test.flow:39</c> previously
    /// commented on its absence).
    /// </summary>
    public static Value NotCharitable(IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        if (ctx.CallerStrictMode && args[0].Type is not BoolType)
        {
            ctx.ErrorReporter.ReportError(
                $"[strict] (not) requires Bool — got {args[0].Type}",
                ctx.CurrentCallSite);
            return Value.Bool(false);
        }
        return Value.Bool(!TruthyCoerce(args[0]));
    }

    /// <summary>
    /// Phase 44 Plan 44-08 Task 3 — non-strict charitable <c>(and a b)</c>
    /// with D-12 last-truthy return semantics (composer Area 4.2 choice,
    /// RESOLVED per RESEARCH Open Question 2): short-circuit on the first
    /// falsy operand and return THAT operand verbatim; otherwise return the
    /// LAST operand. v1.5 breaking change vs the prior Bool-only
    /// <see cref="AndBool"/> shape; permitted under D-v1.5-01 pre-traction
    /// latitude (project_pre_public_no_legacy_burden memo).
    /// <para>
    /// Phase 44 Plan 44-09 Task 1 layers the strict-mode Bool-required
    /// tightening per D-12: when <c>ctx.CallerStrictMode</c> is true, ALL
    /// operands MUST be <see cref="BoolType"/> — otherwise emit
    /// <c>[strict] (and) requires Bool — got &lt;Type&gt;</c> via the
    /// ErrorReporter and return <see cref="Value.Bool(false)"/>. Strict + all-Bool
    /// preserves the existing pre-44-08 semantics: short-circuit on first
    /// false, return <c>Bool(a &amp;&amp; b &amp;&amp; ...)</c>. The Bool-typed
    /// <see cref="AndBool"/> overload (+1000 specificity) wins for the typical
    /// <c>(and true false)</c> call regardless of mode and stays
    /// byte-identical.
    /// </para>
    /// </summary>
    public static Value AndLastTruthy(IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        if (args.Count == 0) return Value.Bool(true);
        if (ctx.CallerStrictMode)
        {
            // D-12 strict: Bool-required across every operand. Emit error on
            // first non-Bool arg + return Bool(false) (no charitable fall-through
            // — strict mode is opt-in fail-fast surface).
            for (int i = 0; i < args.Count; i++)
            {
                if (args[i].Type is not BoolType)
                {
                    ctx.ErrorReporter.ReportError(
                        $"[strict] (and) requires Bool — got {args[i].Type}",
                        ctx.CurrentCallSite);
                    return Value.Bool(false);
                }
            }
            // All-Bool strict path — same Bool-return as AndBool. Short-circuit
            // on first false; otherwise return Bool(all-true).
            for (int i = 0; i < args.Count; i++)
            {
                if (!args[i].As<bool>()) return Value.Bool(false);
            }
            return Value.Bool(true);
        }
        // Non-strict charitable last-truthy (Plan 44-08 Task 3 unchanged).
        Value last = args[0];
        if (!TruthyCoerce(last)) return last;
        for (int i = 1; i < args.Count; i++)
        {
            if (!TruthyCoerce(args[i])) return args[i];
            last = args[i];
        }
        return last;
    }

    /// <summary>
    /// Non-strict charitable <c>(or a b)</c> with D-12 last-truthy return
    /// semantics — first truthy operand wins (returned verbatim);
    /// otherwise the LAST operand is returned (matches CPython <c>or</c>).
    /// See <see cref="AndLastTruthy"/> for the D-12 / D-v1.5-01 migration
    /// rationale.
    /// <para>
    /// Phase 44 Plan 44-09 Task 1 layers the strict-mode Bool-required
    /// tightening per D-12: when <c>ctx.CallerStrictMode</c> is true, ALL
    /// operands MUST be <see cref="BoolType"/> — otherwise emit
    /// <c>[strict] (or) requires Bool — got &lt;Type&gt;</c> via the
    /// ErrorReporter and return <see cref="Value.Bool(false)"/>. Strict + all-Bool
    /// preserves the existing pre-44-08 semantics: short-circuit on first
    /// true, return <c>Bool(a || b || ...)</c>.
    /// </para>
    /// </summary>
    public static Value OrLastTruthy(IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        if (args.Count == 0) return Value.Bool(false);
        if (ctx.CallerStrictMode)
        {
            // D-12 strict: Bool-required across every operand.
            for (int i = 0; i < args.Count; i++)
            {
                if (args[i].Type is not BoolType)
                {
                    ctx.ErrorReporter.ReportError(
                        $"[strict] (or) requires Bool — got {args[i].Type}",
                        ctx.CurrentCallSite);
                    return Value.Bool(false);
                }
            }
            // All-Bool strict path — same Bool-return as OrBool. Short-circuit
            // on first true; otherwise return Bool(any-true).
            for (int i = 0; i < args.Count; i++)
            {
                if (args[i].As<bool>()) return Value.Bool(true);
            }
            return Value.Bool(false);
        }
        // Non-strict charitable last-truthy (Plan 44-08 Task 3 unchanged).
        Value last = args[0];
        if (TruthyCoerce(last)) return last;
        for (int i = 1; i < args.Count; i++)
        {
            if (TruthyCoerce(args[i])) return args[i];
            last = args[i];
        }
        return last;
    }

    // ===== Phase 44 Plan 44-09 Task 2 — Charitable strict-aware comparisons + equals =====
    // D-11 strict equality vs comparison asymmetry:
    //  - (equals 1 1.0) strict → false (set-theoretic; defensible answer).
    //  - (gt|lt|gte|lte 1 1.0) strict → error (no defined cross-type ordering).
    // Non-strict path PRESERVED — Utils.LooseEquals numeric coercion + Utils.CompareNumeric.

    /// <summary>
    /// Phase 44 Plan 44-09 Task 2 — context-dependent charitable
    /// <c>(equals a b)</c>. Routes through
    /// <see cref="Utils.LooseEqualsStrict"/> which short-circuits to
    /// <c>false</c> on cross-type strict (D-11 set-theoretic). Non-strict
    /// behavior is byte-identical to Plan 44-08's <see cref="Equals"/> —
    /// <see cref="Utils.LooseEquals"/> retains JS-style numeric coercion.
    /// </summary>
    public static Value EqualsCharitable(IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        return Value.Bool(Utils.LooseEqualsStrict(args[0], args[1], ctx));
    }

    /// <summary>
    /// Phase 44 Plan 44-09 Task 2 — context-dependent charitable
    /// <c>(gt a b)</c>. Strict mode + cross-type emits the canonical
    /// <c>[strict] cross-type comparison &lt;T1&gt; vs &lt;T2&gt; — use explicit
    /// (double x) / (int x)</c> error. Same-type strict + non-strict route
    /// through <see cref="Utils.CompareNumeric"/> byte-identical.
    /// </summary>
    public static Value GreaterThanCharitable(IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        if (ctx.CallerStrictMode && !args[0].Type.Equals(args[1].Type))
        {
            ctx.ErrorReporter.ReportError(
                $"[strict] cross-type comparison {args[0].Type} vs {args[1].Type} — use explicit (double x) / (int x)",
                ctx.CurrentCallSite);
            return Value.Bool(false);
        }
        return Value.Bool(Utils.CompareNumeric(args[0], args[1]) > 0);
    }

    /// <summary>
    /// Phase 44 Plan 44-09 Task 2 — context-dependent charitable
    /// <c>(lt a b)</c>. See <see cref="GreaterThanCharitable"/> for D-11
    /// strict cross-type semantics.
    /// </summary>
    public static Value LessThanCharitable(IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        if (ctx.CallerStrictMode && !args[0].Type.Equals(args[1].Type))
        {
            ctx.ErrorReporter.ReportError(
                $"[strict] cross-type comparison {args[0].Type} vs {args[1].Type} — use explicit (double x) / (int x)",
                ctx.CurrentCallSite);
            return Value.Bool(false);
        }
        return Value.Bool(Utils.CompareNumeric(args[0], args[1]) < 0);
    }

    /// <summary>
    /// Phase 44 Plan 44-09 Task 2 — context-dependent charitable
    /// <c>(gte a b)</c>. See <see cref="GreaterThanCharitable"/> for D-11
    /// strict cross-type semantics.
    /// </summary>
    public static Value GreaterThanOrEqualCharitable(IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        if (ctx.CallerStrictMode && !args[0].Type.Equals(args[1].Type))
        {
            ctx.ErrorReporter.ReportError(
                $"[strict] cross-type comparison {args[0].Type} vs {args[1].Type} — use explicit (double x) / (int x)",
                ctx.CurrentCallSite);
            return Value.Bool(false);
        }
        return Value.Bool(Utils.CompareNumeric(args[0], args[1]) >= 0);
    }

    /// <summary>
    /// Phase 44 Plan 44-09 Task 2 — context-dependent charitable
    /// <c>(lte a b)</c>. See <see cref="GreaterThanCharitable"/> for D-11
    /// strict cross-type semantics.
    /// </summary>
    public static Value LessThanOrEqualCharitable(IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        if (ctx.CallerStrictMode && !args[0].Type.Equals(args[1].Type))
        {
            ctx.ErrorReporter.ReportError(
                $"[strict] cross-type comparison {args[0].Type} vs {args[1].Type} — use explicit (double x) / (int x)",
                ctx.CurrentCallSite);
            return Value.Bool(false);
        }
        return Value.Bool(Utils.CompareNumeric(args[0], args[1]) <= 0);
    }
}
