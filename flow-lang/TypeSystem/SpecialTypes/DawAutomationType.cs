namespace FlowLang.TypeSystem.SpecialTypes;
public sealed class DawAutomationType : FlowType
{
    public static DawAutomationType Instance { get; } = new();
    private DawAutomationType() { }
    public override string Name => "DawAutomation";
    public override int GetSpecificity() => 150;
}
