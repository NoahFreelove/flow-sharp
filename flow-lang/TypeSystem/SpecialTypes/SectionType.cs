namespace FlowLang.TypeSystem.SpecialTypes;

/// <summary>
/// Runtime data for a section: named group of sequences with a musical context snapshot.
/// </summary>
public class SectionData
{
    public string Name { get; }
    public Dictionary<string, SequenceData> Sequences { get; }
    public Runtime.MusicalContext? Context { get; }
    public Core.SourceLocation? SourceLocation { get; }

    public SectionData(string name, Dictionary<string, SequenceData> sequences, Runtime.MusicalContext? context, Core.SourceLocation? sourceLocation = null)
    {
        Name = name;
        Sequences = sequences;
        Context = context;
        SourceLocation = sourceLocation;
    }

    public override string ToString()
    {
        return $"Section[{Name}, {Sequences.Count} sequences]";
    }
}

/// <summary>
/// Represents a section type in the Flow type system.
/// </summary>
public sealed class SectionType : FlowType
{
    private SectionType() { }

    public static SectionType Instance { get; } = new();

    public override string Name => "Section";

    public override int GetSpecificity() => 138;
}
