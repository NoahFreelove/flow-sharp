using System.Globalization;
using Flow.Studio.Model;
using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;

namespace FlowLang.StandardLibrary.Daw;

/// <summary>Captured, immutable worker context. Standalone engines cannot invent
/// a DAW source identity; the build host installs values before user evaluation.</summary>
public static class DawContextFunctions
{
    private static readonly string[] Names = ["dawContextInfo", "dawTempoMap", "dawMeterNumerators", "dawMeterDenominators"];
    public static void Register(InternalFunctionRegistry registry)
    {
        foreach (string name in Names)
            registry.Register(name, new FunctionSignature(name, []), _ => throw new InvalidOperationException("DAW context is available only during a generator build"));
    }
    internal static void Install(InternalFunctionRegistry registry, GenerationContext context, Guid sourceId, long sourceRevision, TimeSpan budget)
    {
        var info = DictData.Empty(new DictType(StringType.Instance, StringType.Instance));
        foreach (var pair in new Dictionary<string, string>
        {
            ["sourceId"] = sourceId.ToString(),
            ["sourceRevision"] = sourceRevision.ToString(CultureInfo.InvariantCulture),
            ["contextRevision"] = context.Revision.ToString(CultureInfo.InvariantCulture),
            ["seed"] = context.Seed.ToString(CultureInfo.InvariantCulture),
            ["tuningSystem"] = context.Tuning.System,
            ["tuningKey"] = context.Tuning.Key,
            ["tuningScala"] = context.Tuning.Scala ?? "",
            ["tuningKeyboardMap"] = context.Tuning.KeyboardMap ?? "",
            ["timeLimitTicks"] = budget.Ticks.ToString(CultureInfo.InvariantCulture)
        }) info = info.WithSet(Value.String(pair.Key), Value.String(pair.Value));
        var tempo = DictData.Empty(new DictType(DoubleType.Instance, DoubleType.Instance));
        foreach (var point in context.Tempo.Changes) tempo = tempo.WithSet(Value.Double(point.Quarter), Value.Double(point.Bpm));
        var numerators = DictData.Empty(new DictType(IntType.Instance, IntType.Instance));
        var denominators = DictData.Empty(new DictType(IntType.Instance, IntType.Instance));
        foreach (var point in context.Meter.Changes)
        {
            var bar = new Value(point.Bar, IntType.Instance);
            numerators = numerators.WithSet(bar, new Value(point.Numerator, IntType.Instance));
            denominators = denominators.WithSet(bar, new Value(point.Denominator, IntType.Instance));
        }
        DictData[] values = [info, tempo, numerators, denominators];
        for (int i = 0; i < Names.Length; i++)
        {
            var captured = Value.Dict(values[i]);
            registry.ReplaceAll(Names[i], new FunctionSignature(Names[i], []), _ => captured);
        }
    }
}
