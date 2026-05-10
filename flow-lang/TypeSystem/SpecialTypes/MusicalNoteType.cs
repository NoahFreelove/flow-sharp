namespace FlowLang.TypeSystem.SpecialTypes;

/// <summary>
/// Represents a MusicalNote type (MusicalNoteData) - distinct from Note (pitch string).
/// Compatible with NoteType for backwards compatibility.
/// </summary>
public sealed class MusicalNoteType : FlowType
{
    private MusicalNoteType() { }
    public static MusicalNoteType Instance { get; } = new();
    public override string Name => "MusicalNote";
    public override int GetSpecificity() => 131;

    public override bool IsCompatibleWith(FlowType other)
    {
        return other is MusicalNoteType || other is NoteType || base.IsCompatibleWith(other);
    }

    public override bool CanConvertTo(FlowType other)
    {
        return other is NoteType || base.CanConvertTo(other);
    }
}
