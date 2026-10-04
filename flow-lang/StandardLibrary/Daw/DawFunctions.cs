using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Daw;

public static class DawFunctions
{
    public static void Register(InternalFunctionRegistry registry)
    {
        DawContextFunctions.Register(registry);
        registry.Register("dawAssetSample", new FunctionSignature("dawAssetSample", [StringType.Instance]),
            _ => throw new InvalidOperationException("Project samples are available only during a host-supplied DAW build"));
        DawNoteProcessorFunctions.Register(registry);
        foreach (FlowType type in new FlowType[] { DawAudioType.Instance, AudioGraphType.Instance, DawInstrumentType.Instance, DawNoteSequenceType.Instance })
        {
            registry.Register("dawResult", new FunctionSignature("dawResult", [StringType.Instance, type]),
                args => new Value(new DawResultData([(args[0].As<string>(), args[1])]), DawResultType.Instance));
        }
        FlowType[] results = [DawResultType.Instance, new DictType(StringType.Instance, SongType.Instance)];
        foreach (var left in results)
            foreach (var right in results)
                registry.Register("dawCombine", new FunctionSignature("dawCombine", [left, right]), args =>
                    new Value(new DawResultData(DawResultData.From(args[0]).Outputs.Concat(DawResultData.From(args[1]).Outputs)), DawResultType.Instance));
        registry.Register("dawResult", new FunctionSignature("dawResult", [SongType.Instance]),
            args => Result("main", args[0]));
        registry.Register("dawResult", new FunctionSignature("dawResult", [StringType.Instance, SongType.Instance]),
            args => Result(args[0].As<string>(), args[1]));
    }

    private static Value Result(string name, Value song)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var dictionary = DictData.Empty(new DictType(StringType.Instance, SongType.Instance));
        return Value.Dict(dictionary.WithSet(Value.String(name), song));
    }
}
