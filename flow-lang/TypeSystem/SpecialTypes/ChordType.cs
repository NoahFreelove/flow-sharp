namespace FlowLang.TypeSystem.SpecialTypes;

/// <summary>
/// Runtime data for a chord: root, quality, octave, and expanded note names.
/// </summary>
public class ChordData
{
    public string Root { get; }
    public string Quality { get; }
    public int Octave { get; }
    public string[] NoteNames { get; }

    public ChordData(string root, string quality, int octave, string[] noteNames)
    {
        Root = root;
        Quality = quality;
        Octave = octave;
        NoteNames = noteNames;
    }

    public override string ToString()
    {
        return $"{Root}{Quality} [{string.Join(" ", NoteNames)}]";
    }
}

/// <summary>
/// Represents a chord type in the Flow type system.
/// </summary>
public sealed class ChordType : FlowType
{
    private ChordType() { }

    public static ChordType Instance { get; } = new();

    public override string Name => "Chord";

    public override int GetSpecificity() => 136;
}
