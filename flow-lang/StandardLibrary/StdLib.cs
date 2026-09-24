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
    public static Value LenString(IReadOnlyList<Value> args) => CoreStdLib.LenString(args);
    public static Value Print(IReadOnlyList<Value> args) => CoreStdLib.Print(args);
    public static Value StrInt(IReadOnlyList<Value> args) => CoreStdLib.StrInt(args);
    public static Value StrFloat(IReadOnlyList<Value> args) => CoreStdLib.StrFloat(args);
    public static Value StrDouble(IReadOnlyList<Value> args) => CoreStdLib.StrDouble(args);
    public static Value StrLong(IReadOnlyList<Value> args) => CoreStdLib.StrLong(args);
    public static Value StrNumber(IReadOnlyList<Value> args) => CoreStdLib.StrNumber(args);
    public static Value StrString(IReadOnlyList<Value> args) => CoreStdLib.StrString(args);
    public static Value StrBool(IReadOnlyList<Value> args) => CoreStdLib.StrBool(args);
    public static Value StrNote(IReadOnlyList<Value> args)
    {
        return Value.String(args[0].As<string>());
    }

    public static Value StrSymbol(IReadOnlyList<Value> args) => CoreStdLib.StrSymbol(args);
    public static Value StrBar(IReadOnlyList<Value> args)
    {
        var bar = args[0].As<BarData>();
        return Value.String(bar.ToString());
    }

    public static Value StrSemitone(IReadOnlyList<Value> args)
    {
        var value = args[0].As<int>();
        return Value.String($"{(value >= 0 ? "+" : "")}{value}st");
    }

    public static Value StrCent(IReadOnlyList<Value> args)
    {
        var value = args[0].As<double>();
        return Value.String($"{(value >= 0 ? "+" : "")}{Num(value)}c");
    }

    public static Value StrMillisecond(IReadOnlyList<Value> args)
    {
        return Value.String($"{Num(args[0].As<double>())}ms");
    }

    public static Value StrSecond(IReadOnlyList<Value> args)
    {
        return Value.String($"{Num(args[0].As<double>())}s");
    }

    public static Value StrDecibel(IReadOnlyList<Value> args)
    {
        var value = args[0].As<double>();
        return Value.String($"{(value >= 0 ? "+" : "")}{Num(value)}dB");
    }

    public static Value StrHertz(IReadOnlyList<Value> args)
    {
        return Value.String($"{Num(args[0].As<double>())}Hz");
    }

    public static Value StrBeat(IReadOnlyList<Value> args)
    {
        return Value.String(Num(args[0].As<double>()));
    }

    public static Value StrArray(IReadOnlyList<Value> args) => CoreStdLib.StrArray(args);
    public static Value ModInt(IReadOnlyList<Value> args) => CoreStdLib.ModInt(args);
    public static Value ModDouble(IReadOnlyList<Value> args) => CoreStdLib.ModDouble(args);
    public static Value Split(IReadOnlyList<Value> args) => CoreStdLib.Split(args);
    public static Value Concat(IReadOnlyList<Value> args) => CoreStdLib.Concat(args);
    public static Value IntToDouble(IReadOnlyList<Value> args) => CoreStdLib.IntToDouble(args);
    public static Value DoubleToInt(IReadOnlyList<Value> args) => CoreStdLib.DoubleToInt(args);
    public static Value AddInt(IReadOnlyList<Value> args) => CoreStdLib.AddInt(args);
    public static Value AddFloat(IReadOnlyList<Value> args) => CoreStdLib.AddFloat(args);
    public static Value SubFloat(IReadOnlyList<Value> args) => CoreStdLib.SubFloat(args);
    public static Value MulFloat(IReadOnlyList<Value> args) => CoreStdLib.MulFloat(args);
    public static Value DivFloat(IReadOnlyList<Value> args) => CoreStdLib.DivFloat(args);
    public static Value AddDouble(IReadOnlyList<Value> args) => CoreStdLib.AddDouble(args);
    public static Value SubInt(IReadOnlyList<Value> args) => CoreStdLib.SubInt(args);
    public static Value MulInt(IReadOnlyList<Value> args) => CoreStdLib.MulInt(args);
    public static Value DivInt(IReadOnlyList<Value> args) => CoreStdLib.DivInt(args);
    public static Value SubDouble(IReadOnlyList<Value> args) => CoreStdLib.SubDouble(args);
    public static Value MulDouble(IReadOnlyList<Value> args) => CoreStdLib.MulDouble(args);
    public static Value DivDouble(IReadOnlyList<Value> args) => CoreStdLib.DivDouble(args);
    public static Value AddLong(IReadOnlyList<Value> args) => CoreStdLib.AddLong(args);
    public static Value SubLong(IReadOnlyList<Value> args) => CoreStdLib.SubLong(args);
    public static Value MulLong(IReadOnlyList<Value> args) => CoreStdLib.MulLong(args);
    public static Value DivLong(IReadOnlyList<Value> args) => CoreStdLib.DivLong(args);
    public static Value AddNumber(IReadOnlyList<Value> args) => CoreStdLib.AddNumber(args);
    public static Value SubNumber(IReadOnlyList<Value> args) => CoreStdLib.SubNumber(args);
    public static Value MulNumber(IReadOnlyList<Value> args) => CoreStdLib.MulNumber(args);
    public static Value DivNumber(IReadOnlyList<Value> args) => CoreStdLib.DivNumber(args);
    public static Value NegInt(IReadOnlyList<Value> args) => CoreStdLib.NegInt(args);
    public static Value NegLong(IReadOnlyList<Value> args) => CoreStdLib.NegLong(args);
    public static Value NegFloat(IReadOnlyList<Value> args) => CoreStdLib.NegFloat(args);
    public static Value NegDouble(IReadOnlyList<Value> args) => CoreStdLib.NegDouble(args);
    public static Value NegNumber(IReadOnlyList<Value> args) => CoreStdLib.NegNumber(args);
    public static Value IDivInt(IReadOnlyList<Value> args) => CoreStdLib.IDivInt(args);
    public static Value DivIntPromote(IReadOnlyList<Value> args) => CoreStdLib.DivIntPromote(args);
    public static Value StringToInt(IReadOnlyList<Value> args) => CoreStdLib.StringToInt(args);
    public static Value StringToDouble(IReadOnlyList<Value> args) => CoreStdLib.StringToDouble(args);
    public static Value Eval(IReadOnlyList<Value> args) => CoreStdLib.Eval(args);
    public static Value If(IReadOnlyList<Value> args) => CoreStdLib.If(args);
    public static Value IfStrict(IReadOnlyList<Value> args) => CoreStdLib.IfStrict(args);
    public static Value And(IReadOnlyList<Value> args) => CoreStdLib.And(args);
    public static Value AndBool(IReadOnlyList<Value> args) => CoreStdLib.AndBool(args);
    public static Value Or(IReadOnlyList<Value> args) => CoreStdLib.Or(args);
    public static Value OrBool(IReadOnlyList<Value> args) => CoreStdLib.OrBool(args);
    public static Value Equals(IReadOnlyList<Value> args) => CoreStdLib.Equals(args);
    public static Value StrictEquals(IReadOnlyList<Value> args) => CoreStdLib.StrictEquals(args);
    public static Value LessThan(IReadOnlyList<Value> args) => CoreStdLib.LessThan(args);
    public static Value GreaterThan(IReadOnlyList<Value> args) => CoreStdLib.GreaterThan(args);
    public static Value LessThanOrEqual(IReadOnlyList<Value> args) => CoreStdLib.LessThanOrEqual(args);
    public static Value GreaterThanOrEqual(IReadOnlyList<Value> args) => CoreStdLib.GreaterThanOrEqual(args);
    public static Value Rand(IReadOnlyList<Value> args, ExecutionContext context) => CoreStdLib.Rand(args, context);
    public static Value FixedRand(IReadOnlyList<Value> args, ExecutionContext context) => CoreStdLib.FixedRand(args, context);
    public static Value FixedRandReset(IReadOnlyList<Value> args, ExecutionContext context) => CoreStdLib.FixedRandReset(args, context);
    public static Value FixedRandSet(IReadOnlyList<Value> args, ExecutionContext context) => CoreStdLib.FixedRandSet(args, context);
    public static string AutoStr(Value v) => CoreStdLib.AutoStr(v);
    public static Value PrintAny(IReadOnlyList<Value> args, ExecutionContext ctx) => CoreStdLib.PrintAny(args, ctx);
    public static bool TruthyCoerce(Value v) => CoreStdLib.TruthyCoerce(v);
    public static Value IfTruthy(IReadOnlyList<Value> args, ExecutionContext ctx) => CoreStdLib.IfTruthy(args, ctx);
    public static Value NotCharitable(IReadOnlyList<Value> args, ExecutionContext ctx) => CoreStdLib.NotCharitable(args, ctx);
    public static Value AndLastTruthy(IReadOnlyList<Value> args, ExecutionContext ctx) => CoreStdLib.AndLastTruthy(args, ctx);
    public static Value OrLastTruthy(IReadOnlyList<Value> args, ExecutionContext ctx) => CoreStdLib.OrLastTruthy(args, ctx);
    public static Value EqualsCharitable(IReadOnlyList<Value> args, ExecutionContext ctx) => CoreStdLib.EqualsCharitable(args, ctx);
    public static Value GreaterThanCharitable(IReadOnlyList<Value> args, ExecutionContext ctx) => CoreStdLib.GreaterThanCharitable(args, ctx);
    public static Value LessThanCharitable(IReadOnlyList<Value> args, ExecutionContext ctx) => CoreStdLib.LessThanCharitable(args, ctx);
    public static Value GreaterThanOrEqualCharitable(IReadOnlyList<Value> args, ExecutionContext ctx) => CoreStdLib.GreaterThanOrEqualCharitable(args, ctx);
    public static Value LessThanOrEqualCharitable(IReadOnlyList<Value> args, ExecutionContext ctx) => CoreStdLib.LessThanOrEqualCharitable(args, ctx);

    internal static string Num(double value) => ValueFormatter.Number(value);
    internal static Value ForceIfLazy(Value value) => CoreStdLib.ForceIfLazy(value);
}
