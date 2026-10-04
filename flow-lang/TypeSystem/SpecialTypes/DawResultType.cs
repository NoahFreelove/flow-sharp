namespace FlowLang.TypeSystem.SpecialTypes;

public sealed class DawResultType : FlowType
{
    public static DawResultType Instance { get; } = new();
    private DawResultType() { }
    public override string Name => "DawResult";
    public override int GetSpecificity() => 150;
}
