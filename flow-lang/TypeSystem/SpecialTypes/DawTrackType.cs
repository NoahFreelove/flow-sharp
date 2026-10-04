namespace FlowLang.TypeSystem.SpecialTypes;
public sealed class DawTrackType : FlowType
{
    public static DawTrackType Instance { get; } = new();
    private DawTrackType() { }
    public override string Name => "DawTrack";
    public override int GetSpecificity() => 150;
}
