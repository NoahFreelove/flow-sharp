using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary;

/// <summary>
/// Arithmetic that keeps units: (add 100ms 50ms) is 150ms, (mul 2.5s 2) is 5s,
/// (div 1s 250ms) is the plain ratio 4. Milliseconds and seconds mix, taking the
/// first operand's unit. Declared in std.flow next to the numeric overloads.
/// </summary>
public static class UnitArithmetic
{
    // Double-backed unit types and their factories.
    private static readonly (FlowType Type, Func<double, Value> Make)[] DoubleUnits =
    [
        (MillisecondType.Instance, Value.Millisecond),
        (SecondType.Instance, Value.Second),
        (DecibelType.Instance, Value.Decibel),
        (HertzType.Instance, Value.Hertz),
        (CentType.Instance, Value.Cent),
        (BeatType.Instance, Value.Beat),
    ];

    public static void Register(InternalFunctionRegistry registry)
    {
        foreach (var (type, make) in DoubleUnits)
        {
            Reg(registry, "add", type, type, args => make(D(args[0]) + D(args[1])));
            Reg(registry, "sub", type, type, args => make(D(args[0]) - D(args[1])));
            Reg(registry, "mul", type, DoubleType.Instance, args => make(D(args[0]) * D(args[1])));
            Reg(registry, "mul", DoubleType.Instance, type, args => make(D(args[0]) * D(args[1])));
            Reg(registry, "div", type, DoubleType.Instance, args => make(D(args[0]) / NonZero(D(args[1]))));
            Reg(registry, "div", type, type, args => Value.Double(D(args[0]) / NonZero(D(args[1]))));
        }

        // Multiplying two unit values (2s × 3s, 2s × 3Hz) gives a plain number, as it
        // did before unit-preserving arithmetic; explicit overloads keep the call from
        // tying between mul(Unit, Double) and mul(Double, Unit).
        foreach (var (a, _) in DoubleUnits)
            foreach (var (b, _) in DoubleUnits)
                Reg(registry, "mul", a, b, args => Value.Double(D(args[0]) * D(args[1])));

        // Milliseconds and seconds combine; the result uses the first operand's unit.
        Reg(registry, "add", MillisecondType.Instance, SecondType.Instance,
            args => Value.Millisecond(D(args[0]) + D(args[1]) * 1000.0));
        Reg(registry, "sub", MillisecondType.Instance, SecondType.Instance,
            args => Value.Millisecond(D(args[0]) - D(args[1]) * 1000.0));
        Reg(registry, "add", SecondType.Instance, MillisecondType.Instance,
            args => Value.Second(D(args[0]) + D(args[1]) / 1000.0));
        Reg(registry, "sub", SecondType.Instance, MillisecondType.Instance,
            args => Value.Second(D(args[0]) - D(args[1]) / 1000.0));
        Reg(registry, "div", MillisecondType.Instance, SecondType.Instance,
            args => Value.Double(D(args[0]) / NonZero(D(args[1]) * 1000.0)));
        Reg(registry, "div", SecondType.Instance, MillisecondType.Instance,
            args => Value.Double(D(args[0]) * 1000.0 / NonZero(D(args[1]))));

        // Semitones are whole numbers.
        var st = SemitoneType.Instance;
        Reg(registry, "add", st, st, args => Value.Semitone(I(args[0]) + I(args[1])));
        Reg(registry, "sub", st, st, args => Value.Semitone(I(args[0]) - I(args[1])));
        Reg(registry, "mul", st, IntType.Instance, args => Value.Semitone(I(args[0]) * I(args[1])));
        Reg(registry, "mul", IntType.Instance, st, args => Value.Semitone(I(args[0]) * I(args[1])));
        Reg(registry, "mul", st, st, args => Value.Int(I(args[0]) * I(args[1])));
    }

    /// <summary>
    /// When both values are durations (ms or s), returns them in seconds so
    /// equality and ordering compare the same quantity.
    /// </summary>
    public static bool TryAsSeconds(Value a, Value b, out double aSeconds, out double bSeconds)
    {
        aSeconds = bSeconds = 0;
        if (!IsTime(a) || !IsTime(b)) return false;
        aSeconds = Seconds(a);
        bSeconds = Seconds(b);
        return true;
    }

    private static bool IsTime(Value v) => v.Type is MillisecondType or SecondType;

    private static double Seconds(Value v) => v.Type is MillisecondType ? D(v) / 1000.0 : D(v);

    private static void Reg(InternalFunctionRegistry registry, string name, FlowType a, FlowType b,
        Func<IReadOnlyList<Value>, Value> impl) =>
        registry.Register(name, new FunctionSignature(name, [a, b], ParameterNames: ["a", "b"]), impl);

    private static double D(Value v) => Convert.ToDouble(v.Data);

    private static int I(Value v) => Convert.ToInt32(v.Data);

    private static double NonZero(double divisor) =>
        divisor == 0 ? throw new InvalidOperationException("Division by zero") : divisor;
}
