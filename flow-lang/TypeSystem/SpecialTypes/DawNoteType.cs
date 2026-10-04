namespace FlowLang.TypeSystem.SpecialTypes;
public sealed class DawNoteType : FlowType
{
    public static DawNoteType Instance { get; } = new();
    private DawNoteType() { }
    public override string Name => "DawNote";
    public override int GetSpecificity() => 150;
}
