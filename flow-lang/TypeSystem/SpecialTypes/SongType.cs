namespace FlowLang.TypeSystem.SpecialTypes;

/// <summary>
/// A reference to a section in a song arrangement, with optional repeat count.
/// </summary>
public class SongSectionRef
{
    public string Name { get; }
    public int RepeatCount { get; }

    public SongSectionRef(string name, int repeatCount = 1)
    {
        Name = name;
        RepeatCount = repeatCount;
    }

    public override string ToString()
    {
        return RepeatCount > 1 ? $"{Name}*{RepeatCount}" : Name;
    }
}

/// <summary>
/// Runtime data for a song: an ordered arrangement of section references.
/// </summary>
public class SongData
{
    public List<SongSectionRef> Sections { get; }
    public Dictionary<string, SectionData> SectionRegistry { get; }

    public SongData(List<SongSectionRef> sections, Dictionary<string, SectionData> sectionRegistry)
    {
        Sections = sections;
        SectionRegistry = sectionRegistry;
    }

    public override string ToString()
    {
        return $"[{string.Join(" ", Sections.Select(s => s.ToString()))}]";
    }
}

/// <summary>
/// Represents a song type in the Flow type system.
/// </summary>
public sealed class SongType : FlowType
{
    private SongType() { }

    public static SongType Instance { get; } = new();

    public override string Name => "Song";

    public override int GetSpecificity() => 140;
}
