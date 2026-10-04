namespace FlowLang.TypeSystem.SpecialTypes;
public sealed class DawNoteSequenceType : FlowType
{
    public static DawNoteSequenceType Instance { get; } = new();
    private DawNoteSequenceType() { }
    public override string Name => "DawNoteSequence";
    public override int GetSpecificity() => 150;
}
