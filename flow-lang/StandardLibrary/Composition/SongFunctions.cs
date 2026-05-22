using FlowLang.Runtime;
using FlowLang.TypeSystem;
using FlowLang.TypeSystem.PrimitiveTypes;
using FlowLang.TypeSystem.SpecialTypes;

namespace FlowLang.StandardLibrary.Composition;

public static class SongFunctions
{
    public static void Register(InternalFunctionRegistry registry, FlowLang.Runtime.ExecutionContext context)
    {
        var createSongSignature = new FunctionSignature("createSong", [StringType.Instance],
            ParameterNames: ["title"]);
        registry.Register("createSong", createSongSignature, args => CreateSong(args, context));

        var addBarSignature = new FunctionSignature("addBarToSong", [SongType.Instance, StringType.Instance],
            ParameterNames: ["song", "name"]);
        registry.Register("addBarToSong", addBarSignature, args => AddBarToSong(args, context));

        var addBarRepeatSignature = new FunctionSignature("addBarToSong", [SongType.Instance, StringType.Instance, IntType.Instance],
            ParameterNames: ["song", "name", "repeat"]);
        registry.Register("addBarToSong", addBarRepeatSignature, args => AddBarToSong(args, context));

        var addSeqSignature = new FunctionSignature("addBarToSong", [SongType.Instance, SequenceType.Instance],
            ParameterNames: ["song", "seq"]);
        registry.Register("addBarToSong", addSeqSignature, args => AddSequenceToSong(args, context));
    }

    private static Value AddSequenceToSong(IReadOnlyList<Value> args, FlowLang.Runtime.ExecutionContext context)
    {
        var song = args[0].As<SongData>();
        var seq = args[1].As<SequenceData>();

        string name = "adhoc_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var sequences = new Dictionary<string, SequenceData> { { "main", seq } };
        var section = new SectionData(name, sequences, context.GetMusicalContext());
        song.SectionRegistry[name] = section;
        song.Sections.Add(new SongSectionRef(name, 1));
        return Value.Void();
    }

    private static Value AddBarToSong(IReadOnlyList<Value> args, FlowLang.Runtime.ExecutionContext context)
    {
        var song = args[0].As<SongData>();
        var name = args[1].As<string>();
        int repeat = args.Count > 2 ? args[2].As<int>() : 1;

        song.Sections.Add(new SongSectionRef(name, repeat));
        return Value.Void();
    }

    private static Value CreateSong(IReadOnlyList<Value> args, FlowLang.Runtime.ExecutionContext context)
    {
        // string title = args[0].As<string>(); // Title is currently unused in SongData but good for signature
        var sections = new List<SongSectionRef>();
        var sectionRegistry = new Dictionary<string, SectionData>(context.SectionRegistry);
        
        return Value.Song(new SongData(sections, sectionRegistry));
    }
}
