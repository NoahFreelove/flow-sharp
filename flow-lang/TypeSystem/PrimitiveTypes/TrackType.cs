namespace FlowLang.TypeSystem.PrimitiveTypes;

/// <summary>
/// Represents a track type for collections of voices.
/// </summary>
public sealed class TrackType : FlowType
{
    private TrackType() { }

    public static TrackType Instance { get; } = new();

    public override string Name => "Track";

    public override int GetSpecificity() => 141;
}
