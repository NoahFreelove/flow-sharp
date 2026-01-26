using FlowLang.Runtime;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary;

/// <summary>
/// Standard library implementations for Flow built-in functions.
/// </summary>
public static class stdlib
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
        if (b == 0) throw new DivideByZeroException();
        return Value.Int(a / b);
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
    public static Value Rand(IReadOnlyList<Value> args)
    {
        return Value.Float(Utils.FRand());
    }
    
    public static Value FixedRand(IReadOnlyList<Value> args)
    {
        return Value.Float(Utils.FRand(true));
    }
    
    public static Value FixedRandReset(IReadOnlyList<Value> args)
    {
        Utils.ResetGen();
        return Value.Void();
    }
    
    public static Value FixedRandSet(IReadOnlyList<Value> args)
    {
        var val = args[0];
        if (val.Type is not IntType)                                                      
            throw new InvalidOperationException($"Expected Int, got {val.Type}"); 
        
        Utils.SetSeed(val.As<int>());
        return Value.Void();
    }
}
