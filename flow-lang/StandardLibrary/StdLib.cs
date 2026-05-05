using FlowLang.Runtime;
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
}
