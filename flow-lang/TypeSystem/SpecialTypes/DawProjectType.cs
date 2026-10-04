namespace FlowLang.TypeSystem.SpecialTypes;
public sealed class DawProjectType : FlowType
{
    public static DawProjectType Instance { get; } = new();
    private DawProjectType() { }
    public override string Name => "DawProject";
    public override int GetSpecificity() => 150;
}
