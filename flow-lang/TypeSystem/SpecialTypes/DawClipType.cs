namespace FlowLang.TypeSystem.SpecialTypes;
public sealed class DawClipType : FlowType
{
    public static DawClipType Instance { get; } = new();
    private DawClipType() { }
    public override string Name => "DawClip";
    public override int GetSpecificity() => 150;
}
